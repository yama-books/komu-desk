namespace KomuDesk.Core;

public sealed record AgendaRow(DateTime Day,string Source,string Key,string Title,string Time,int Order,string State="",Lesson? Lesson=null,int? StartMinute=null,int? EndMinute=null);
public sealed record AgendaPlacement(AgendaRow Row,int Start,int End,int Lane,int Lanes);
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
        rows.AddRange(data.Events.Where(e=>e.Date==date).Select(e=>new AgendaRow(day.Date,"local",e.Id,e.Title,e.Time==""?"終日":e.Time,e.Time==""?0:2,StartMinute:Minute(e.Time))));
        rows.AddRange(LessonEngine.Day(data,day,now).Lessons.Select(l=>new AgendaRow(day.Date,"lesson",l.Date+":"+l.Period+":"+l.GroupId,data.GroupName(l.GroupId),l.Start==""?"時刻未設定":l.Start,l.Start==""?3:2,l.State,l,Minute(l.Start),Minute(l.End))));
        rows.AddRange(data.Tasks.Where(t=>t.Due==date&&(completed||!t.Done)).Select(t=>new AgendaRow(day.Date,"task",t.Id,t.Title,"期限",4,t.Done?"done":"open")));
        return Sort(rows);
    }
    public static int? Minute(string time)=>TimeSpan.TryParseExact(time,@"hh\:mm",System.Globalization.CultureInfo.InvariantCulture,out var t)?(int)t.TotalMinutes:null;
    // Layout-only minimum footprint; a start-only event is still a point, not a duration.
    public static IReadOnlyList<AgendaPlacement> Place(IEnumerable<AgendaRow> rows,int minimum=45)
    {
        var timed=rows.Where(r=>r.StartMinute is >=0 and <1440).Select(r=>(Row:r,Start:r.StartMinute!.Value,End:Math.Min(1440,Math.Max(r.EndMinute??r.StartMinute.Value,r.StartMinute.Value+minimum)))).OrderBy(r=>r.Start).ThenByDescending(r=>r.End).ToArray();
        var output=new List<AgendaPlacement>();var group=new List<(AgendaRow Row,int Start,int End,int Lane)>();var laneEnds=new List<int>();int boundary=-1;
        void Flush(){foreach(var p in group)output.Add(new(p.Row,p.Start,p.End,p.Lane,laneEnds.Count));group.Clear();laneEnds.Clear();}
        foreach(var r in timed){if(r.Start>=boundary){Flush();boundary=-1;}int lane=laneEnds.FindIndex(e=>e<=r.Start);if(lane<0){lane=laneEnds.Count;laneEnds.Add(r.End);}else laneEnds[lane]=r.End;group.Add((r.Row,r.Start,r.End,lane));boundary=Math.Max(boundary,r.End);}Flush();return output;
    }
    public static IReadOnlyList<AgendaRow> Sort(IEnumerable<AgendaRow> rows)=>rows.OrderBy(r=>r.Day).ThenBy(r=>r.Order).ThenBy(r=>r.Time,StringComparer.Ordinal).ThenBy(r=>r.Source,StringComparer.Ordinal).ThenBy(r=>r.Key,StringComparer.Ordinal).ToArray();
}
