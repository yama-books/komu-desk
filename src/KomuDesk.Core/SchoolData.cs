using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KomuDesk.Core;

public class Grade { public string Id { get; set; } = Guid.NewGuid().ToString("N"); public string Name { get; set; } = ""; public string Color { get; set; } = "#D9E8D8"; }
public class SchoolClass { public string Id { get; set; } = Guid.NewGuid().ToString("N"); public string GradeId { get; set; } = ""; public string Name { get; set; } = ""; public string Color { get; set; } = "#DDE5EF"; }
public class Subject { public string Id { get; set; } = Guid.NewGuid().ToString("N"); public string Name { get; set; } = ""; }
public class TeachingGroup { public string Id { get; set; } = Guid.NewGuid().ToString("N"); public string ClassId { get; set; } = ""; public string SubjectId { get; set; } = ""; public string Name { get; set; } = ""; }
public class Bell { public int Number { get; set; } = 1; public string Start { get; set; } = "08:40"; public string End { get; set; } = "09:30"; }
public class Slot { public int Day { get; set; } = 1; public int Period { get; set; } = 1; public string GroupId { get; set; } = ""; }
public class Timetable { public string Id { get; set; } = Guid.NewGuid().ToString("N"); public string Name { get; set; } = "通常"; public string From { get; set; } = ""; public string To { get; set; } = ""; public List<Slot> Slots { get; set; } = new(); }
public class LocalEvent { public string Id { get; set; } = Guid.NewGuid().ToString("N"); public string Date { get; set; } = DateTime.Today.ToString("yyyy-MM-dd"); public string Time { get; set; } = ""; public string Title { get; set; } = ""; }
public class Todo { public string Id { get; set; } = Guid.NewGuid().ToString("N"); public string Title { get; set; } = ""; public string Due { get; set; } = ""; public bool Done { get; set; } }
public class DayChange { public string Date { get; set; } = DateTime.Today.ToString("yyyy-MM-dd"); public bool Closed { get; set; } public string ProfileId { get; set; } = ""; public int? MaxPeriod { get; set; } public int? Weekday { get; set; } public string Reason { get; set; } = ""; }
public class LessonChange { public string Date { get; set; } = DateTime.Today.ToString("yyyy-MM-dd"); public int Period { get; set; } = 1; public string Action { get; set; } = "cancel"; public string GroupId { get; set; } = ""; public string Kind { get; set; } = "normal"; public string Reason { get; set; } = ""; }
public class ActualLesson { public string Date { get; set; } = ""; public int Period { get; set; } public string GroupId { get; set; } = ""; public string Kind { get; set; } = "normal"; public string Start { get; set; } = ""; public string End { get; set; } = ""; public bool Held { get; set; } public string Note { get; set; } = ""; }
public class Preferences { public int UpdateKeepPrevious { get; set; } = 1; public int TrayIcon { get; set; } = 2; public bool Topmost { get; set; } public bool Mini { get; set; } public bool ShowFree { get; set; } = true; public double Left { get; set; } = -1; public double Top { get; set; } = -1; public double Width { get; set; } = 370; public double Height { get; set; } = 740; }
public class SchoolData
{
    public int Schema { get; set; } = 1;
    public string YearFrom { get; set; } = "";
    public string YearTo { get; set; } = "";
    public List<Grade> Grades { get; set; } = new();
    public List<SchoolClass> Classes { get; set; } = new();
    public List<Subject> Subjects { get; set; } = new();
    public List<TeachingGroup> Groups { get; set; } = new();
    public List<Bell> Bells { get; set; } = new();
    public List<BellProfile> BellProfiles { get; set; } = new();
    public List<ScheduleRule> ScheduleRules { get; set; } = new();
    public List<Timetable> Timetables { get; set; } = new();
    public List<LocalEvent> Events { get; set; } = new();
    public List<Todo> Tasks { get; set; } = new();
    public List<DayChange> Days { get; set; } = new();
    public List<LessonChange> Changes { get; set; } = new();
    public List<ActualLesson> Actuals { get; set; } = new();
    public Preferences Display { get; set; } = new();
    public bool CountExams { get; set; }
    public static SchoolData Empty(DateTime today)
    {
        var start = new DateTime(today.Month >= 4 ? today.Year : today.Year - 1, 4, 1);
        var d = new SchoolData { YearFrom = Iso(start), YearTo = Iso(start.AddYears(1).AddDays(-1)) };
        string[] begins = { "08:40", "09:40", "10:40", "11:40", "13:20", "14:20" };
        string[] ends = { "09:30", "10:30", "11:30", "12:30", "14:10", "15:10" };
        for (int i = 0; i < begins.Length; i++) d.Bells.Add(new Bell { Number = i + 1, Start = begins[i], End = ends[i] });
        d.Timetables.Add(new Timetable { From = d.YearFrom, To = d.YearTo });
        return d;
    }
    public static string Iso(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static DateTime ParseDate(string s) => DateTime.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static TimeSpan ParseTime(string s) => TimeSpan.ParseExact(s, @"hh\:mm", CultureInfo.InvariantCulture);
    public SchoolData Clone() => JsonSerializer.Deserialize<SchoolData>(JsonSerializer.Serialize(this))!;
    public string GroupName(string id)
    {
        var g = Groups.Find(x => x.Id == id);
        if (g == null) return "未登録";
        var c = Classes.Find(x => x.Id == g.ClassId);
        var grade = Grades.Find(x => x.Id == c?.GradeId);
        return string.Join(" ", new[] { grade?.Name, c?.Name, Subjects.Find(x => x.Id == g.SubjectId)?.Name, g.Name }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }
    public void Validate()
    {
        void Require(bool ok, string msg) { if (!ok) throw new InvalidDataException(msg); }
        void Unique<T>(IEnumerable<T> keys, string label) { Require(keys.Count() == keys.Distinct().Count(), label + "が重複しています。"); }
        bool Color(string s) => Regex.IsMatch(s ?? "", "^#[0-9a-fA-F]{6}$");
        bool Name(string s) => !string.IsNullOrWhiteSpace(s);
        var from = ParseDate(YearFrom); var to = ParseDate(YearTo);
        Require(Schema is 1 or 2, "対応していない保存形式です。");
        Require(from <= to && (to - from).TotalDays <= 730, "年度の期間を確認してください。");
        Unique(Grades.Select(x => x.Id), "学年ID"); Unique(Classes.Select(x => x.Id), "クラスID");
        Unique(Subjects.Select(x => x.Id), "科目ID"); Unique(Groups.Select(x => x.Id), "授業ID");
        Unique(Timetables.Select(x => x.Id), "時間割ID"); Unique(Tasks.Select(x => x.Id), "タスクID"); Unique(Events.Select(x => x.Id), "予定ID");
        foreach (var x in Grades) Require(Name(x.Name) && Color(x.Color), "学年名・色（#RRGGBB）を確認してください。");
        foreach (var x in Classes) Require(Name(x.Name) && Color(x.Color) && Grades.Any(g => g.Id == x.GradeId), "クラスの学年・名称・色を確認してください。");
        foreach (var x in Subjects) Require(Name(x.Name), "科目名が必要です。");
        foreach (var x in Groups) Require(Classes.Any(c => c.Id == x.ClassId) && Subjects.Any(s => s.Id == x.SubjectId), "授業のクラス・科目を選択してください。");
        Unique(Bells.Select(x => x.Number), "時限");
        TimeSpan previous = TimeSpan.Zero;
        foreach (var b in Bells.OrderBy(x => x.Number))
        {
            var start = ParseTime(b.Start); var end = ParseTime(b.End);
            Require(b.Number >= 1 && b.Number <= 20 && start >= previous && end > start && end.TotalHours < 24, "時程の順序・時刻を確認してください。");
            previous = end;
        }
        Unique(BellProfiles.Select(p=>p.Id),"時程ID");Unique(BellProfiles.Select(p=>p.Name),"時程名");
        foreach(var profile in BellProfiles)
        {
            Require(Name(profile.Name)&&profile.Bells.Count>0,"特別時程名・時限を確認してください。");
            Unique(profile.Bells.Select(b=>b.Number),"特別時程の時限");
            TimeSpan endBefore=TimeSpan.Zero;
            foreach(var b in profile.Bells.OrderBy(b=>b.Number))
            {
                var a=ParseTime(b.Start);var z=ParseTime(b.End);
                Require(Bells.Any(x=>x.Number==b.Number)&&a>=endBefore&&z>a&&z.TotalHours<24,"特別時程の時刻・時限が不正です。");endBefore=z;
            }
        }
        Unique(ScheduleRules.Select(r=>r.Id),"連動ルールID");
        Unique(ScheduleRules.Select(r=>(r.Source,r.Title.Normalize(System.Text.NormalizationForm.FormKC).Trim())),"連動ルール");
        foreach(var r in ScheduleRules)
            Require(Name(r.Title)&&r.Source is "local" or "google"&&(r.Weekday==null||r.Weekday is >=0 and <=6)&&(r.MaxPeriod==null||r.MaxPeriod is >=1 and <=20)&&(r.ProfileId==""||BellProfiles.Any(p=>p.Id==r.ProfileId))&&(r.Closed||r.Weekday!=null||r.MaxPeriod!=null||r.ProfileId!="")&&(!r.Closed||r.Weekday==null&&r.MaxPeriod==null&&r.ProfileId==""),"連動ルールを確認してください。");
        DateTime? last = null;
        foreach (var t in Timetables.OrderBy(x => ParseDate(x.From)))
        {
            var a = ParseDate(t.From); var z = ParseDate(t.To);
            Require(a >= from && z <= to && a <= z && (last == null || a > last), "時間割の有効期間が重複または年度外です。");
            last = z;
            Unique(t.Slots.Select(x => (x.Day, x.Period)), "時間割枠");
            foreach (var x in t.Slots) Require(x.Day is >= 0 and <= 6 && Bells.Any(b => b.Number == x.Period) && Groups.Any(g => g.Id == x.GroupId), "時間割の曜日・時限・授業を確認してください。");
        }
        foreach (var e in Events) { ParseDate(e.Date); if (e.Time != "") ParseTime(e.Time); Require(Name(e.Title), "予定名が必要です。"); }
        foreach (var t in Tasks) { if (t.Due != "") ParseDate(t.Due); Require(Name(t.Title), "タスク名が必要です。"); }
        Unique(Days.Select(x => x.Date), "日別変更");
        foreach (var x in Days) { var d = ParseDate(x.Date); Require(d >= from && d <= to && (x.Weekday == null || x.Weekday is >= 0 and <= 6) && (x.MaxPeriod==null||x.MaxPeriod is >=1 and <=20) && (x.ProfileId==""||BellProfiles.Any(p=>p.Id==x.ProfileId)), "日別変更の日付・曜日を確認してください。"); }
        Unique(Changes.Select(x => (x.Date, x.Period)), "個別変更");
        foreach (var x in Changes)
        {
            var d = ParseDate(x.Date);
            Require(d >= from && d <= to && Bells.Any(b => b.Number == x.Period) && (x.Action == "cancel" || x.Action == "replace"), "個別変更を確認してください。");
            if (x.Action == "replace") Require(Groups.Any(g => g.Id == x.GroupId) && x.Kind is "normal" or "exam", "置換授業を確認してください。");
        }
        Unique(Actuals.Select(x => (x.Date, x.Period)), "確定実績");
        foreach (var a in Actuals)
        {
            var d = ParseDate(a.Date); var start = ParseTime(a.Start); var end = ParseTime(a.End);
            Require(d >= from && d <= to && a.Period >= 1 && Groups.Any(g => g.Id == a.GroupId) && start < end && a.Kind is "normal" or "exam", "確定実績が不正です。");
        }
    }
}
