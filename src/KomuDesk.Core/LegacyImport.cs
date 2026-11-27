using System.Globalization;

namespace KomuDesk.Core;

public sealed record ImportPlan(SchoolData Data, int DailyRows, int EventRows, int GroupCount, int ExamDays, int TotalCached, int TotalRecalculated, IReadOnlyList<string> Warnings);
public static class LegacyImport
{
    public const string EventsSheet = "年間予定(Google用貼付)";
    public const string TimetableSheet = "時間割（入力）";
    public const string DailySheet = "実施授業";
    public static bool IsEmpty(SchoolData d)=>d.BellProfiles.Count==0&&d.ScheduleRules.Count==0&&d.Grades.Count==0&&d.Classes.Count==0&&d.Subjects.Count==0&&d.Groups.Count==0&&d.Events.Count==0&&d.Tasks.Count==0&&d.Days.Count==0&&d.Changes.Count==0&&d.Actuals.Count==0&&d.Timetables.All(t=>t.Slots.Count==0);
    private static string Get(Dictionary<string,LegacyWorkbook.Cell> row,string col,bool strict=true)
    {
        if(!row.TryGetValue(col,out var c))return "";
        if(strict&&(c.Error||c.CacheMissing))throw new InvalidDataException("必要なセルの計算キャッシュがないかエラーです。元Excelを再計算して保存してください。");
        return c.Value.Trim();
    }
    private static bool EmptySlot(string s)=>string.IsNullOrWhiteSpace(s)||s is "-" or "－" or "―";
    private static bool ExamMarker(string s)=>s is "【考査】" or "考査";
    private static int Number(string s,int low,int high)
    {
        if(!int.TryParse(s,NumberStyles.Integer,CultureInfo.InvariantCulture,out int n)||n<low||n>high)throw new InvalidDataException("時限または曜日コードが不正です。");
        return n;
    }
    public static ImportPlan Prepare(string path,string gradeLabel,bool includeExams)
    {
        if(string.IsNullOrWhiteSpace(gradeLabel))throw new InvalidDataException("学年の仮名称を入力してください。");
        using var workbook=new LegacyWorkbook(path);
        var rows=workbook.Rows(DailySheet);
        var weekly=workbook.Rows(TimetableSheet);
        var events=workbook.Rows(EventsSheet);
        var days=new List<(DateTime Date,int Code,string[] Labels)>();
        foreach(var (number,row) in rows.OrderBy(x=>x.Key))
        {
            if(number<5||!row.ContainsKey("D"))continue;
            string value=Get(row,"D");if(value=="")continue;
            var date=workbook.Date(value);int code=Number(Get(row,"E"),1,8);
            var labels="GHIJKL".Select(c=>Get(row,c.ToString())).ToArray();
            if(code==7&&labels.Any(s=>!EmptySlot(s)))throw new InvalidDataException("授業なしの行に授業値があります。元Excelを確認してください。");
            if(code!=8&&labels.Any(ExamMarker))throw new InvalidDataException("通常日の考査マーカーを確認してください。");
            days.Add((date,code,labels));
        }
        if(days.Count==0||days.Count>366)throw new InvalidDataException("日別授業の期間が空、または1年度を超えています。");
        days=days.OrderBy(x=>x.Date).ToList();
        if(days.Select(d=>d.Date).Distinct().Count()!=days.Count||days[^1].Date-days[0].Date!=TimeSpan.FromDays(days.Count-1))throw new InvalidDataException("日別授業に重複日・欠落日があります。");
        var data=SchoolData.Empty(days[0].Date);data.CountExams=includeExams;
        if(days[^1].Date>SchoolData.ParseDate(data.YearTo))throw new InvalidDataException("年度をまたぐ日別表は分割してください。");
        var version=data.Timetables[0];version.Name="Excel取り込み版";version.From=SchoolData.Iso(days[0].Date);version.To=SchoolData.Iso(days[^1].Date);
        var grade=new Grade{Name=gradeLabel.Trim()};data.Grades.Add(grade);
        var mapping=new Dictionary<string,string>(StringComparer.Ordinal);
        string Group(string label)
        {
            if(mapping.TryGetValue(label,out string? existing))return existing;
            var parts=label.Split('・',2);
            string cls=parts.Length==2?parts[0].Trim():label;
            string subject=parts.Length==2?parts[1].Trim():"その他授業";
            if(cls==""||subject=="")throw new InvalidDataException("授業名称を確認してください。");
            var c=data.Classes.Find(x=>x.Name==cls);
            if(c==null){c=new SchoolClass{Name=cls,GradeId=grade.Id};data.Classes.Add(c);}
            var s=data.Subjects.Find(x=>x.Name==subject);
            if(s==null){s=new Subject{Name=subject};data.Subjects.Add(s);}
            var g=data.Groups.Find(x=>x.ClassId==c.Id&&x.SubjectId==s.Id);
            if(g==null){g=new TeachingGroup{ClassId=c.Id,SubjectId=s.Id};data.Groups.Add(g);}
            mapping.Add(label,g.Id);return g.Id;
        }
        // The supported template stores Monday..Sunday in B:H, period numbers A3:A8.
        // Code 8 is a special exam day, not a weekly weekday. Daily cached cells supply its exact assignments.
        if(!weekly.TryGetValue(2,out var header)||"BCDEFGHI".Select((c,i)=>Get(header,c.ToString())==(i+1).ToString(CultureInfo.InvariantCulture)).Any(ok=>!ok))throw new InvalidDataException("時間割の曜日コード行が既定形式と異なります。");
        for(int r=3;r<=8;r++)
        {
            if(!weekly.TryGetValue(r,out var row)||Number(Get(row,"A"),1,6)!=r-2)throw new InvalidDataException("時間割の時限行が既定形式と異なります。");
            for(int col=0;col<7;col++)
            {
                var label=Get(row,((char)('B'+col)).ToString());
                if(!EmptySlot(label))version.Slots.Add(new Slot{Day=(col+1)%7,Period=r-2,GroupId=Group(label)});
            }
        }
        int total=0,examDays=0;
        var expected=new Dictionary<string,int>();
        foreach(var day in days)
        {
            string iso=SchoolData.Iso(day.Date);
            if(day.Code==7){data.Days.Add(new DayChange{Date=iso,Closed=true,Reason="Excel曜日コード7"});continue;}
            bool exam=day.Code==8;if(exam)examDays++;
            int weekday=exam?(int)day.Date.DayOfWeek:day.Code%7;
            if(!exam&&weekday!=(int)day.Date.DayOfWeek)data.Days.Add(new DayChange{Date=iso,Weekday=weekday,Reason="Excel曜日変更"});
            for(int p=1;p<=6;p++)
            {
                var label=day.Labels[p-1];
                bool empty=EmptySlot(label)||(exam&&ExamMarker(label));
                var planned=version.Slots.SingleOrDefault(s=>s.Day==weekday&&s.Period==p)?.GroupId;
                if(empty)
                {
                    if(planned!=null)data.Changes.Add(new LessonChange{Date=iso,Period=p,Action="cancel",Reason="Excel日別表"});
                    continue;
                }
                string group=Group(label);
                if(!exam||includeExams){total++;expected[group]=expected.GetValueOrDefault(group)+1;}
                if(exam||group!=planned)data.Changes.Add(new LessonChange{Date=iso,Period=p,Action="replace",GroupId=group,Kind=exam?"exam":"normal",Reason="Excel日別表"});
            }
        }
        foreach(var (number,row) in events.OrderBy(x=>x.Key))
        {
            if(!row.ContainsKey("B"))continue;
            string date=Get(row,"B");string title=Get(row,"C");
            if(date==""&&title=="")continue;
            if(date==""||title=="")throw new InvalidDataException("年間予定の日付または名称が欠けています。");
            data.Events.Add(new LocalEvent{Date=SchoolData.Iso(workbook.Date(date)),Title=title});
        }
        data.Validate();
        var counts=LessonEngine.Count(data,days[0].Date,days[^1].Date,days[^1].Date.AddDays(1));
        int recalculated=counts.Sum(x=>x.Forecast);
        if(recalculated!=total||counts.Any(c=>c.Forecast!=expected.GetValueOrDefault(c.GroupId)))throw new InvalidDataException("取り込み後の授業数が日別キャッシュと一致しません。");
        return new ImportPlan(data,days.Count,data.Events.Count,data.Groups.Count,examDays,total,recalculated,new[]{
            "計算式ではなく、Excelが最後に保存した値を読み取ります。実施確定にはせず、予定として登録します。",
            "期間外の時間割は未設定になります。時程は仮値です。設定画面で確認してください。",
            "学年は仮分類です。色、学年・科目名を編集できます。名列・出欠・進度シートは取り込みません。",
            "年間予定は表示のみ。授業への影響は日別授業表の結果から反映し、予定名から自動取消しません。"
        });
    }
}
