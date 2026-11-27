namespace KomuDesk.Core;

public sealed record Lesson(string Date, int Period, string GroupId, string Start, string End, string Kind, string State, string Note = "");
public sealed record DayResult(string Date, bool Configured, IReadOnlyList<Lesson> Lessons);
public sealed record LessonCount(string GroupId, int Confirmed, int Estimated, int Remaining, int Cancelled, int Unresolved)
{
    public int Forecast => Confirmed + Estimated + Remaining;
}
public static class LessonEngine
{
    public static DayResult Day(SchoolData data, DateTime date, DateTime now)
    {
        var iso = SchoolData.Iso(date);
        var version = data.Timetables.SingleOrDefault(t => SchoolData.ParseDate(t.From) <= date.Date && SchoolData.ParseDate(t.To) >= date.Date);
        var change = data.Days.SingleOrDefault(x => x.Date == iso);
        var day = change?.Weekday ?? (int)date.DayOfWeek;
        var planned = version?.Slots.Where(x => x.Day == day).ToDictionary(x => x.Period) ?? new Dictionary<int, Slot>();
        var edits = data.Changes.Where(x => x.Date == iso).ToDictionary(x => x.Period);
        var actuals = data.Actuals.Where(x => x.Date == iso).ToDictionary(x => x.Period);
        var result = new List<Lesson>();
        foreach (int n in planned.Keys.Concat(edits.Keys).Concat(actuals.Keys).Distinct().Order())
        {
            if (actuals.TryGetValue(n, out var a))
            {
                result.Add(new Lesson(iso,n,a.GroupId,a.Start,a.End,a.Kind,a.Held ? "confirmed" : "cancelled",a.Note));
                continue;
            }
            var group = planned.GetValueOrDefault(n)?.GroupId;
            var bell = SchoolSetup.BellsFor(data,change).SingleOrDefault(b => b.Number == n);
            bool outside=(change?.MaxPeriod is int max&&n>max)||(change?.ProfileId!=""&&change?.ProfileId!=null&&bell==null);
            var normalBell=data.Bells.SingleOrDefault(b=>b.Number==n);
            var edit = edits.GetValueOrDefault(n);
            var kind = edit?.Action == "replace" ? edit.Kind : "normal";
            if (edit?.Action == "replace") group = edit.GroupId;
            if (group == null) continue;
            if(outside){result.Add(new Lesson(iso,n,group,normalBell?.Start??"",normalBell?.End??"",kind,edit?.Action=="replace"?"unresolved":"cancelled",edit?.Reason??change?.Reason??"特別時程"));continue;}
            if (bell == null) { result.Add(new Lesson(iso,n,group,"","",kind,"unresolved")); continue; }
            var closed = change?.Closed == true;
            var state = edit?.Action == "replace" && closed ? "unresolved" :
                closed || edit?.Action == "cancel" ? "cancelled" :
                date.Date.Add(SchoolData.ParseTime(bell.End)) <= now ? "estimated" : "remaining";
            result.Add(new Lesson(iso,n,group,bell.Start,bell.End,kind,state,edit?.Reason ?? change?.Reason ?? ""));
        }
        return new DayResult(iso,version != null || change?.Closed == true,result);
    }
    public static IReadOnlyList<LessonCount> Count(SchoolData data, DateTime start, DateTime end, DateTime now)
    {
        if (start.Date > end.Date || (end.Date - start.Date).TotalDays > 730) throw new ArgumentException("集計範囲を確認してください。");
        if (start.Date < SchoolData.ParseDate(data.YearFrom) || end.Date > SchoolData.ParseDate(data.YearTo)) throw new ArgumentException("年度内の範囲を選択してください。");
        var all = new List<Lesson>();
        for (var day = start.Date; day <= end.Date; day = day.AddDays(1)) all.AddRange(Day(data,day,now).Lessons);
        return data.Groups.Select(g =>
        {
            var lessons = all.Where(l => l.GroupId == g.Id && (l.Kind != "exam" || data.CountExams)).ToList();
            int N(string state) => lessons.Count(l => l.State == state);
            return new LessonCount(g.Id,N("confirmed"),N("estimated"),N("remaining"),N("cancelled"),N("unresolved"));
        }).ToList();
    }
    public static int? MaxAbsence(int total, int p, int q, bool strict)
    {
        if (total <= 0 || p <= 0 || q <= 0 || p > q) return null;
        long n = (long)total * p;
        long required = strict ? n / q + 1 : (n + q - 1) / q;
        return required > total ? null : total - (int)required;
    }
    public static void Confirm(SchoolData data, Lesson lesson, bool held, string note)
    {
        if (lesson.State is "cancelled" or "unresolved" or "confirmed") throw new InvalidOperationException("確定可能な授業ではありません。");
        if (data.Actuals.Any(a => a.Date == lesson.Date && a.Period == lesson.Period)) throw new InvalidOperationException("既に確定済みです。");
        data.Actuals.Add(new ActualLesson { Date=lesson.Date, Period=lesson.Period, GroupId=lesson.GroupId, Start=lesson.Start, End=lesson.End, Kind=lesson.Kind, Held=held, Note=note });
    }
}
