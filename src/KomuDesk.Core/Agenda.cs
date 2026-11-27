namespace KomuDesk.Core;

public sealed record AgendaRow(DateTime Day,string Source,string Key,string Title,string Time,int Order,string State="",Lesson? Lesson=null);
public static class Agenda
{
    public static DateTime WeekStart(DateTime day)=>day.Date.AddDays(-((int)day.DayOfWeek+6)%7);
    public static DateTime MonthStart(DateTime day)=>new(day.Year,day.Month,1);
    public static IReadOnlyList<DateTime> Range(DateTime from,DateTime to)
    {
        int days=(to.Date-from.Date).Days+1;
        if(days<1||days>366)throw new ArgumentException("表示期間は1〜366日です。");
        return Enumerable.Range(0,days).Select(n=>from.Date.AddDays(n)).ToArray();
    }
    public static IReadOnlyList<AgendaRow> Local(SchoolData data,DateTime day,DateTime now,bool completed=false)
    {
        var date=SchoolData.Iso(day);
        var rows=new List<AgendaRow>();
        rows.AddRange(data.Events.Where(e=>e.Date==date).Select(e=>new AgendaRow(day.Date,"local",e.Id,e.Title,e.Time==""?"終日":e.Time,e.Time==""?0:2)));
        rows.AddRange(LessonEngine.Day(data,day,now).Lessons.Select(l=>new AgendaRow(day.Date,"lesson",l.Date+":"+l.Period+":"+l.GroupId,data.GroupName(l.GroupId),l.Start==""?"時刻未設定":l.Start,l.Start==""?3:2,l.State,l)));
        rows.AddRange(data.Tasks.Where(t=>t.Due==date&&(completed||!t.Done)).Select(t=>new AgendaRow(day.Date,"task",t.Id,t.Title,"期限",4,t.Done?"done":"open")));
        return Sort(rows);
    }
    public static IReadOnlyList<AgendaRow> Sort(IEnumerable<AgendaRow> rows)=>rows.OrderBy(r=>r.Day).ThenBy(r=>r.Order).ThenBy(r=>r.Time,StringComparer.Ordinal).ThenBy(r=>r.Source,StringComparer.Ordinal).ThenBy(r=>r.Key,StringComparer.Ordinal).ToArray();
}
