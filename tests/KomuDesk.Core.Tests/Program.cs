using KomuDesk.Core;

int passed=0;
void Equal<T>(T actual,T expected,string label)
{
    if(!EqualityComparer<T>.Default.Equals(actual,expected)) throw new Exception($"{label}: expected {expected}, got {actual}");
    Console.WriteLine("PASS "+label);passed++;
}
void Reject(Action action,string label)
{
    try {action();} catch(InvalidDataException){Console.WriteLine("PASS "+label);passed++;return;}
    throw new Exception("Expected validation failure: "+label);
}
var monday=new DateTime(2026,4,6);
var tue=monday.AddDays(1);
var now=monday.AddHours(12);
var data=SchoolData.Empty(monday);
var grade=new Grade{Name="架空学年"};
var cls=new SchoolClass{Name="A組",GradeId=grade.Id};
var cls2=new SchoolClass{Name="B組",GradeId=grade.Id};
var subject=new Subject{Name="架空科目"};
var a=new TeachingGroup{ClassId=cls.Id,SubjectId=subject.Id};
var b=new TeachingGroup{ClassId=cls2.Id,SubjectId=subject.Id};
data.Grades.Add(grade);data.Classes.AddRange(new[]{cls,cls2});data.Subjects.Add(subject);data.Groups.AddRange(new[]{a,b});
foreach(int day in new[]{1,2}){data.Timetables[0].Slots.Add(new Slot{Day=day,Period=1,GroupId=a.Id});data.Timetables[0].Slots.Add(new Slot{Day=day,Period=2,GroupId=b.Id});}
data.Validate();
var counts=LessonEngine.Count(data,monday,tue,now);
Equal(counts.Sum(x=>x.Forecast),4,"four lessons");
Equal(counts.Sum(x=>x.Estimated),2,"two estimated");
Equal(counts.Sum(x=>x.Remaining),2,"two future");
Equal(counts.Single(x=>x.GroupId==a.Id).Forecast,2,"A count");
var closed=data.Clone();closed.Days.Add(new DayChange{Date=SchoolData.Iso(tue),Closed=true});closed.Validate();
Equal(LessonEngine.Count(closed,monday,tue,now).Sum(x=>x.Forecast),2,"whole day closed");
var replacement=data.Clone();replacement.Changes.Add(new LessonChange{Date=SchoolData.Iso(tue),Period=1,Action="replace",GroupId=a.Id,Kind="exam"});replacement.Validate();
Equal(LessonEngine.Count(replacement,monday,tue,now).Sum(x=>x.Forecast),3,"exclude exam");
replacement.CountExams=true;
Equal(LessonEngine.Count(replacement,monday,tue,now).Sum(x=>x.Forecast),4,"include exam");
var moved=data.Clone();
moved.Changes.Add(new LessonChange{Date=SchoolData.Iso(tue),Period=1,Action="cancel"});
moved.Changes.Add(new LessonChange{Date=SchoolData.Iso(tue.AddDays(1)),Period=1,Action="replace",GroupId=a.Id});moved.Validate();
Equal(LessonEngine.Count(moved,monday,tue.AddDays(1),now).Sum(x=>x.Forecast),4,"manual cancel + add");
var l=LessonEngine.Day(data,monday,now).Lessons[0];
LessonEngine.Confirm(data,l,true,"fictional note");data.Validate();
data.Timetables[0].Slots.RemoveAll(x=>x.Day==1&&x.Period==1);
Equal(LessonEngine.Day(data,monday,now).Lessons[0].State,"confirmed","actual preserved after plan change");
Equal(LessonEngine.Day(data,monday,now).Lessons[0].Note,"fictional note","note preserved");
var conflicting=closed.Clone();conflicting.Changes.Add(new LessonChange{Date=SchoolData.Iso(tue),Period=1,Action="replace",GroupId=a.Id});conflicting.Validate();
Equal(LessonEngine.Day(conflicting,tue,now).Lessons[0].State,"unresolved","closure conflict");
var invalid=data.Clone();invalid.Timetables.Add(new Timetable{From=data.YearFrom,To=data.YearTo});
Reject(invalid.Validate,"overlapping versions rejected");
invalid=data.Clone();invalid.Grades[0].Color="blue";Reject(invalid.Validate,"invalid color rejected");
invalid=data.Clone();invalid.Groups.Clear();Reject(invalid.Validate,"referenced deletion rejected");
Equal(LessonEngine.MaxAbsence(20,1,2,true),9,"strict half even");
Equal(LessonEngine.MaxAbsence(21,1,2,true),10,"strict half odd");
Equal(LessonEngine.MaxAbsence(0,1,2,true),null,"zero invalid");
var temp=Path.Combine(Path.GetTempPath(),"komu-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
try
{
    var path=Path.Combine(temp,"data.json");var store=new LocalStore(path);
    Equal(store.Load(monday).Groups.Count,0,"empty first run");
    store.Save(data);Equal(store.Load(monday).Actuals[0].Note,"fictional note","round trip");
    data.Tasks.Add(new Todo{Title="架空タスク",Done=true});store.Save(data);
    Equal(store.Load(monday).Tasks[0].Done,true,"task persisted");
    Equal(File.Exists(path+".previous"),true,"previous snapshot");
    var backup=Path.Combine(temp,"backup.json");store.Backup(data,backup);
    Equal(new LocalStore(backup).Load(monday).Tasks.Count,1,"backup read");
    var wrong=data.Clone();wrong.YearFrom="not a date";
    try{store.Save(wrong);throw new Exception("invalid save accepted");}catch(FormatException){}
    Equal(store.Load(monday).Tasks.Count,1,"invalid save leaves old file");
}
finally{Directory.Delete(temp,true);}
passed+=ImportCases.Run();
passed+=await CalendarCases.Run();
passed+=ReportCases.Run();
passed+=UpdateCases.Run();
var leap=new DateTime(2028,2,29);
Equal(Agenda.WeekStart(new DateTime(2026,11,1)),new DateTime(2026,10,26),"agenda Sunday starts Monday");
Equal(Agenda.WeekStart(monday),monday,"agenda Monday stays");
Equal(Agenda.Range(new DateTime(2028,2,28),leap.AddDays(1)).Count,3,"agenda leap range inclusive");
Equal(Agenda.Range(new DateTime(2026,12,31),new DateTime(2027,1,1)).Count,2,"agenda year boundary");
Equal(Agenda.Range(new DateTime(2028,1,1),new DateTime(2028,12,31)).Count,366,"agenda leap full year");
try{Agenda.Range(monday,monday.AddDays(366));throw new Exception("range accepted");}catch(ArgumentException){Equal(true,true,"agenda range limit");}
try{Agenda.Range(tue,monday);throw new Exception("reverse accepted");}catch(ArgumentException){Equal(true,true,"agenda reverse rejected");}
var agenda=data.Clone();
agenda.Events.AddRange(new[]{new LocalEvent{Date=SchoolData.Iso(monday),Title="same",Time=""},new LocalEvent{Date=SchoolData.Iso(monday),Title="same",Time=""},new LocalEvent{Date=SchoolData.Iso(monday),Title="timed",Time="10:00"}});
agenda.Tasks.AddRange(new[]{new Todo{Title="due",Due=SchoolData.Iso(monday)},new Todo{Title="completed",Due=SchoolData.Iso(monday),Done=true},new Todo{Title="no due",Due=""}});
var view=Agenda.Local(agenda,monday,now);
Equal(view.Count(r=>r.Source=="local"),3,"agenda preserves same-title events");
Equal(view[0].Time,"終日","agenda all-day first");
Equal(view.Count(r=>r.Source=="task"),1,"agenda only due incomplete");
Equal(Agenda.Local(agenda,monday,now,true).Count(r=>r.Source=="task"),2,"agenda show completed");
Equal(view.Single(r=>r.Lesson?.State=="confirmed").Lesson!.Note,"fictional note","agenda confirmed record preserved");
Equal(view.Last().Source,"task","agenda due tasks last");
Equal(Agenda.Local(closed,tue,now).Count(r=>r.State=="cancelled"),2,"agenda closure state");
Equal(Agenda.Local(replacement,tue,now).Count(r=>r.Lesson?.Kind=="exam"),1,"agenda exam state");
Console.WriteLine($"TOTAL {passed} PASS");
