using KomuDesk.Core;

namespace KomuDesk.Desktop;

internal static class DemoData
{
    public static SchoolData Create(DateTime today)
    {
        var d = SchoolData.Empty(today);
        var grade = new Grade { Name = "高1" }; d.Grades.Add(grade);
        var subject = new Subject { Name = "古文" }; d.Subjects.Add(subject);
        foreach (var (name,color) in new[] { ("A組","#DDE5EF"), ("B組","#EAD9D9"), ("C組","#E9DFCF") })
        {
            var c = new SchoolClass { Name=name, Color=color, GradeId=grade.Id }; d.Classes.Add(c);
            d.Groups.Add(new TeachingGroup { ClassId=c.Id, SubjectId=subject.Id });
        }
        for (int day = 1; day <= 6; day++)
        {
            d.Timetables[0].Slots.Add(new Slot { Day=day, Period=1, GroupId=d.Groups[(day-1)%3].Id });
            d.Timetables[0].Slots.Add(new Slot { Day=day, Period=3, GroupId=d.Groups[day%3].Id });
            d.Timetables[0].Slots.Add(new Slot { Day=day, Period=5, GroupId=d.Groups[(day+1)%3].Id });
        }
        d.Events.Add(new LocalEvent { Date=SchoolData.Iso(today), Title="校内行事（架空）" });
        d.Events.Add(new LocalEvent { Date=SchoolData.Iso(today), Time="15:30", Title="打ち合わせ（架空）" });
        d.Tasks.Add(new Todo { Title="配布資料の準備", Due=SchoolData.Iso(today) });
        d.Tasks.Add(new Todo { Title="教材の確認" });
        d.Validate(); return d;
    }
}
