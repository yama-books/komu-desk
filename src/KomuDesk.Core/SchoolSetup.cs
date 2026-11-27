using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KomuDesk.Core;

public class BellProfile
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string Name {get;set;}="";
    public List<Bell> Bells {get;set;}=new();
}
public class ScheduleRule
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string Title {get;set;}="";
    public string Source {get;set;}="local";
    public int? Weekday {get;set;}
    public int? MaxPeriod {get;set;}
    public bool Closed {get;set;}
    public string ProfileId {get;set;}="";
    public bool Automatic {get;set;}
}
public sealed record ScheduleInput(DateTime Date,string Title,string Source);
public sealed record ScheduleSuggestion(DateTime Date,string Title,int? Weekday,int? MaxPeriod,bool Closed,string ProfileId,bool Automatic,string Block)
{
    public string Summary(SchoolData data)=>string.Join(" / ",new[]{Weekday.HasValue?new[]{"日","月","火","水","木","金","土"}[Weekday.Value]+"曜時間割":"",MaxPeriod.HasValue?MaxPeriod+"限まで":"",Closed?"全休":"",data.BellProfiles.Find(p=>p.Id==ProfileId)?.Name??""}.Where(s=>s!=""));
}
public static class SchoolSetup
{
    private static string Normalize(string value)=>value.Normalize(NormalizationForm.FormKC).Trim();
    private static readonly string[] Colors={"#DDE5EF","#EAD9D9","#D9E8D8","#E9DFCF","#E4DDED","#E5E5E5"};
    public static int Register(SchoolData d,string grade,string subject,IEnumerable<string> classes)
    {
        grade=grade.Trim();subject=subject.Trim();var names=classes.Select(c=>c.Trim()).Where(c=>c!="").Distinct(StringComparer.Ordinal).ToArray();
        if(grade==""||subject==""||names.Length==0||names.Length>100)throw new InvalidDataException("学年・科目・クラスを確認してください。");
        var g=d.Grades.SingleOrDefault(g=>g.Name==grade);
        if(g==null){g=new Grade{Name=grade};d.Grades.Add(g);}
        var s=d.Subjects.SingleOrDefault(s=>s.Name==subject);
        if(s==null){s=new Subject{Name=subject};d.Subjects.Add(s);}
        int added=0;
        foreach(var name in names)
        {
            var c=d.Classes.SingleOrDefault(c=>c.GradeId==g.Id&&c.Name==name);
            if(c==null){c=new SchoolClass{Name=name,GradeId=g.Id,Color=Colors[d.Classes.Count%Colors.Length]};d.Classes.Add(c);}
            if(!d.Groups.Any(x=>x.ClassId==c.Id&&x.SubjectId==s.Id)){d.Groups.Add(new TeachingGroup{ClassId=c.Id,SubjectId=s.Id});added++;}
        }
        return added;
    }
    public static IReadOnlyList<Slot> PasteSlots(SchoolData d,string text,IReadOnlyList<int> periods)
    {
        var lines=text.Replace("\r\n","\n").TrimEnd('\r','\n').Split('\n');
        if(lines.Length!=periods.Count)throw new InvalidDataException("時限行数が一致しません。見出しを除いたマスだけをコピーしてください。");
        var result=new List<Slot>();
        for(int i=0;i<lines.Length;i++)
        {
            var cells=lines[i].TrimEnd('\r').Split('\t');
            if(cells.Length!=7&&cells.Length!=5&&cells.Length!=6)throw new InvalidDataException("月曜から5〜7列を貼り付けてください。");
            for(int j=0;j<cells.Length;j++)
            {
                var label=Normalize(cells[j]);if(label==""||label=="-"||label=="―")continue;
                var matches=d.Groups.Where(g=>Normalize(d.GroupName(g.Id))==label||Normalize(d.Classes.Single(c=>c.Id==g.ClassId).Name+"・"+d.Subjects.Single(s=>s.Id==g.SubjectId).Name)==label).ToArray();
                if(matches.Length!=1)throw new InvalidDataException($"{i+1}行 {j+1}列：担当授業を一意に特定できません。");
                result.Add(new Slot{Day=(j+1)%7,Period=periods[i],GroupId=matches[0].Id});
            }
        }
        return result;
    }
    public static IReadOnlyList<Bell> BellsFor(SchoolData d,DayChange? day)
    {
        if(day?.ProfileId is string id&&id!="")return d.BellProfiles.Single(p=>p.Id==id).Bells;
        return d.Bells;
    }
    public static IReadOnlyList<ScheduleSuggestion> Suggest(SchoolData d,IEnumerable<ScheduleInput> inputs,DateTime today)
    {
        var result=new List<ScheduleSuggestion>();
        foreach(var group in inputs.GroupBy(x=>x.Date.Date).OrderBy(x=>x.Key))
        {
            var date=group.Key;
            var matches=new List<(ScheduleRule Rule,string Title)>();
            foreach(var input in group)
            {
                var title=Normalize(input.Title);
                var rules=d.ScheduleRules.Where(r=>Normalize(r.Title)==title&&r.Source==input.Source).ToArray();
                if(rules.Length>0){matches.AddRange(rules.Select(r=>(r,input.Title)));continue;}
                var weekday=Regex.Match(title,@"^([日月火水木金土])曜(?:日)?(?:時間割|授業)$");
                var cutoff=Regex.Match(title,@"^([1-9][0-9]?)限まで$");
                var shortened=Regex.Match(title,@"^短縮(?:([1-9][0-9]?)限|授業)$");
                if(weekday.Success)matches.Add((new ScheduleRule{Weekday="日月火水木金土".IndexOf(weekday.Groups[1].Value)},input.Title));
                else if(cutoff.Success&&int.Parse(cutoff.Groups[1].Value)<=20)matches.Add((new ScheduleRule{MaxPeriod=int.Parse(cutoff.Groups[1].Value)},input.Title));
                else if(title is "休校" or "代休")matches.Add((new ScheduleRule{Closed=true},input.Title));
                else if(shortened.Success)
                {
                    var profiles=d.BellProfiles.Where(p=>Normalize(p.Name)==title).ToArray();
                    if(profiles.Length==1)matches.Add((new ScheduleRule{ProfileId=profiles[0].Id,MaxPeriod=shortened.Groups[1].Success?int.Parse(shortened.Groups[1].Value):null},input.Title));
                    else result.Add(new(date,input.Title,null,null,false,"",false,"対応する短縮時程を登録してください。"));
                }
                else if(title.Contains("考査")||title.Contains("体育祭")||title.Contains("文化祭"))
                    result.Add(new(date,input.Title,null,null,false,"",false,"授業への影響を手動で確認してください。"));
            }
            if(matches.Count==0)continue;
            var weekdays=matches.Select(x=>x.Rule.Weekday).OfType<int>().Distinct().ToArray();
            var limits=matches.Select(x=>x.Rule.MaxPeriod).OfType<int>().Distinct().ToArray();
            var profiles2=matches.Select(x=>x.Rule.ProfileId).Where(x=>x!="").Distinct().ToArray();
            bool closed=matches.Any(x=>x.Rule.Closed);
            string iso=SchoolData.Iso(date);
            string block=date<today.Date?"過去の日程は変更候補としてのみ表示":
                date<SchoolData.ParseDate(d.YearFrom)||date>SchoolData.ParseDate(d.YearTo)?"年度外":
                d.Actuals.Any(x=>x.Date==iso)?"確定済み授業あり":
                d.Days.Any(x=>x.Date==iso)||d.Changes.Any(x=>x.Date==iso)?"既存の日別・個別変更あり":
                weekdays.Length>1||limits.Length>1||profiles2.Length>1||closed&&(weekdays.Length+limits.Length+profiles2.Length>0)?"変更候補が競合しています。":"";
            if(result.Any(r=>r.Date==date&&r.Block!="")&&block=="")block="同日に未解決の行事があります。";
            result.Add(new(date,string.Join(" / ",matches.Select(x=>x.Title).Distinct()),weekdays.FirstOrDefaultNullable(),limits.FirstOrDefaultNullable(),closed,profiles2.FirstOrDefault()??"",matches.All(x=>x.Rule.Automatic),block));
        }
        return result;
    }
    private static int? FirstOrDefaultNullable(this int[] a)=>a.Length==0?null:a[0];
    public static bool Apply(SchoolData d,ScheduleSuggestion suggestion,DateTime today)
    {
        var iso=SchoolData.Iso(suggestion.Date);
        if(suggestion.Block!=""||suggestion.Date<today.Date||d.Days.Any(x=>x.Date==iso)||d.Changes.Any(x=>x.Date==iso)||d.Actuals.Any(x=>x.Date==iso))return false;
        if(!suggestion.Closed&&suggestion.Weekday==null&&suggestion.MaxPeriod==null&&suggestion.ProfileId=="")return false;
        d.Days.Add(new DayChange{Date=iso,Weekday=suggestion.Weekday,MaxPeriod=suggestion.MaxPeriod,ProfileId=suggestion.ProfileId,Closed=suggestion.Closed,Reason="予定連動: "+suggestion.Title});
        return true;
    }
}
public sealed record EventImportRow(int Row,string Date,string Title,string Time,string Status);
public static class EventImport
{
    public static IReadOnlyList<EventImportRow> Prepare(SchoolData d,string path,string sheet,string dateColumn,string titleColumn,string timeColumn,int start)
    {
        using var book=new LegacyWorkbook(path);var rows=book.Rows(sheet);
        if(start<1||start>100000||!Regex.IsMatch(dateColumn,@"^[A-Z]{1,3}$")||!Regex.IsMatch(titleColumn,@"^[A-Z]{1,3}$")||(timeColumn!=""&&!Regex.IsMatch(timeColumn,@"^[A-Z]{1,3}$"))||dateColumn==titleColumn||timeColumn==dateColumn||timeColumn==titleColumn)throw new InvalidDataException("列・開始行を確認してください。");
        var candidates=new List<(int Row,string Date,string Title,string Time)>();
        var output=new List<EventImportRow>();
        foreach(var (number,row) in rows.OrderBy(x=>x.Key).Where(x=>x.Key>=start))
        {
            if(output.Count+candidates.Count>=10000)throw new InvalidDataException("予定は1万行までです。");
            string Get(string col)
            {
                if(col==""||!row.TryGetValue(col,out var cell))return "";
                if(cell.Error||cell.CacheMissing)throw new InvalidDataException("計算値なし・セルエラー");
                return cell.Value.Trim();
            }
            try
            {
                string date=Get(dateColumn),title=Get(titleColumn),time=Get(timeColumn);if(date==""&&title==""&&time=="")continue;
                if(date==""||title=="")throw new InvalidDataException("日付または名称なし");
                if(time!=""&&double.TryParse(time,NumberStyles.Float,CultureInfo.InvariantCulture,out double fraction))
                {
                    if(!double.IsFinite(fraction)||fraction<0||fraction>=1)throw new InvalidDataException("時刻が不正");
                    int min=(int)Math.Round(fraction*1440);if(min>=1440)throw new InvalidDataException("時刻が不正");
                    time=$"{min/60:00}:{min%60:00}";
                }
                if(time!=""&&Agenda.Minute(time)==null)throw new InvalidDataException("時刻が不正");
                candidates.Add((number,SchoolData.Iso(book.Date(date)),title,time));
            }
            catch(Exception e)when(e is InvalidDataException or FormatException or ArgumentException){output.Add(new(number,"","","","要確認: "+e.Message));}
        }
        return Classify(d,candidates,output);
    }
    public static IReadOnlyList<EventImportRow> Paste(SchoolData d,string text)
    {
        if(text.Length>2_000_000)throw new InvalidDataException("貼り付けが大きすぎます。");
        var candidates=new List<(int Row,string Date,string Title,string Time)>();var errors=new List<EventImportRow>();
        var lines=text.Replace("\r\n","\n").Split('\n');if(lines.Length>10000)throw new InvalidDataException("予定は1万行までです。");
        for(int i=0;i<lines.Length;i++)
        {
            var line=lines[i].TrimEnd('\r');if(string.IsNullOrWhiteSpace(line))continue;
            var cols=line.Split('\t');
            if(cols.Length is <2 or >3||!DateTime.TryParseExact(cols[0].Trim(),new[]{"yyyy-MM-dd","yyyy/M/d","yyyy/MM/dd"},CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)||cols[1].Trim()==""||(cols.Length==3&&cols[2].Trim()!=""&&Agenda.Minute(cols[2].Trim())==null))
            {errors.Add(new(i+1,"","","","要確認: 日付・予定名・時刻を確認"));continue;}
            candidates.Add((i+1,SchoolData.Iso(date),cols[1].Trim(),cols.Length==3?cols[2].Trim():""));
        }
        return Classify(d,candidates,errors);
    }
    private static IReadOnlyList<EventImportRow> Classify(SchoolData d,List<(int Row,string Date,string Title,string Time)> candidates,List<EventImportRow> output)
    {
        var existing=d.Events.GroupBy(e=>(e.Date,e.Title,e.Time)).ToDictionary(g=>g.Key,g=>g.Count());
        var seen=new Dictionary<(string,string,string),int>();
        foreach(var row in candidates)
        {
            var key=(row.Date,row.Title,row.Time);int n=seen.GetValueOrDefault(key)+1;seen[key]=n;
            output.Add(new(row.Row,row.Date,row.Title,row.Time,n<=existing.GetValueOrDefault(key)?"既存":"追加"));
        }
        return output.OrderBy(r=>r.Row).ToArray();
    }
    public static int Add(SchoolData d,IEnumerable<EventImportRow> rows)
    {
        int n=0;foreach(var row in rows.Where(r=>r.Status=="追加")){d.Events.Add(new LocalEvent{Date=row.Date,Title=row.Title,Time=row.Time});n++;}return n;
    }
}
