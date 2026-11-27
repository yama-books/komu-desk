using System.Globalization;
using System.Text;
namespace KomuDesk.Core;
public sealed record RosterStudent(string Id,string Year,string ClassId,int Number,string Name,string SurnameKana,string GivenKana,string SourceCode);
public sealed record RosterInput(int Row,string Code,string ClassCode,int Number,string Name,string SurnameKana,string GivenKana);
public sealed record RosterParse(IReadOnlyList<RosterInput> Rows,int BlankRows,IReadOnlyList<int> InvalidRows);
public sealed record RosterPreview(RosterInput Input,string ClassName,string ClassId,string Status);
public static class RosterImport
{
    public static string NormalizeCode(string s)=>s.Normalize(NormalizationForm.FormKC).Trim();
    public static RosterParse Parse(string text)
    {
        if(text.Length>2_000_000)throw new InvalidDataException("名列が大きすぎます。");
        var lines=text.Replace("\r\n","\n").Split('\n');if(lines.Length>10001)throw new InvalidDataException("1万行まで取り込めます。");
        var rows=new List<RosterInput>();var invalid=new List<int>();int blank=0;
        for(int i=0;i<lines.Length;i++)
        {
            if(string.IsNullOrWhiteSpace(lines[i]))continue;
            var c=lines[i].TrimEnd('\r').Split('\t');int offset=c.Length==6?1:c.Length==5?0:-1;
            if(offset<0){invalid.Add(i+1);continue;}
            string code=offset==1?NormalizeCode(c[0]):"",cls=NormalizeCode(c[offset]),num=NormalizeCode(c[offset+1]),name=c[offset+2].Trim(),surname=c[offset+3].Trim(),given=c[offset+4].Trim();
            if(i==0&&(cls.Contains("組")||cls=="クラス")&&num.Contains("番")&&(name.Contains("氏名")||name.Contains("名前")))continue;
            if(name==""){blank++;continue;}
            if(cls.Length==0||cls.Length>40||code.Length>80||name.Length>100||surname.Length>100||given.Length>100||!int.TryParse(num,NumberStyles.None,CultureInfo.InvariantCulture,out int n)||n<1||n>9999){invalid.Add(i+1);continue;}
            rows.Add(new(i+1,code,cls,n,name,surname,given));
        }
        return new(rows,blank,invalid);
    }
    public static IReadOnlyList<RosterPreview> Preview(SchoolData school,string gradeId,RosterParse input,IReadOnlyDictionary<string,string> mapping,IReadOnlyList<RosterStudent> existing)
    {
        if(!school.Grades.Any(g=>g.Id==gradeId))throw new InvalidDataException("学年を指定してください。");
        var classes=school.Classes.Where(c=>c.GradeId==gradeId).ToDictionary(c=>c.Id);
        var codes=input.Rows.Select(x=>x.ClassCode).Distinct().ToArray();
        if(codes.Any(c=>!mapping.ContainsKey(c)||!classes.ContainsKey(mapping[c]))||codes.Select(c=>mapping[c]).Distinct().Count()!=codes.Length)throw new InvalidDataException("組コードと対象クラスを一対一で対応させてください。");
        var repeated=input.Rows.GroupBy(r=>(mapping[r.ClassCode],r.Number)).Where(g=>g.Count()>1).Select(g=>g.Key).ToHashSet();
        var old=existing.Where(s=>s.Year==school.YearFrom).ToDictionary(s=>(s.ClassId,s.Number));
        return input.Rows.Select(r=>{
            string id=mapping[r.ClassCode];var key=(id,r.Number);
            string status=repeated.Contains(key)?"重複・要確認":!old.TryGetValue(key,out var s)?"追加":s.Name==r.Name&&s.SurnameKana==r.SurnameKana&&s.GivenKana==r.GivenKana?"既存":"差異・要確認";
            return new RosterPreview(r,classes[id].Name,id,status);
        }).ToArray();
    }
    public static List<RosterStudent> Add(string year,IReadOnlyList<RosterStudent> existing,IReadOnlyList<RosterPreview> preview)
    {
        var result=existing.ToList();var keys=existing.Select(s=>(s.Year,s.ClassId,s.Number)).ToHashSet();
        foreach(var p in preview.Where(p=>p.Status=="追加"))
        {
            var r=p.Input;if(!keys.Add((year,p.ClassId,r.Number)))continue;
            result.Add(new(Guid.NewGuid().ToString("N"),year,p.ClassId,r.Number,r.Name,r.SurnameKana,r.GivenKana,r.Code));
        }
        Validate(result);return result;
    }
    public static void Validate(IReadOnlyList<RosterStudent> students)
    {
        if(students.Count>20000||students.Select(s=>s.Id).Distinct().Count()!=students.Count||students.Select(s=>(s.Year,s.ClassId,s.Number)).Distinct().Count()!=students.Count||
           students.Any(s=>!Guid.TryParseExact(s.Id,"N",out _)||string.IsNullOrWhiteSpace(s.ClassId)||s.Number is <1 or >9999||string.IsNullOrWhiteSpace(s.Name)||s.Name.Length>100||s.SurnameKana.Length>100||s.GivenKana.Length>100||s.SourceCode.Length>80||
              !DateTime.TryParseExact(s.Year,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _)))throw new InvalidDataException("名列データの形式が不正です。");
    }
}
