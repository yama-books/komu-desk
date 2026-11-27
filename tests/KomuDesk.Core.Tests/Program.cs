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
Equal(Agenda.Minute("09:45"),585,"timeline minute parsing");
Equal(Agenda.Minute(""),null,"timeline no invented time");
var t1=new AgendaRow(monday,"lesson","1","a","09:00",2,StartMinute:540,EndMinute:600);
var t2=new AgendaRow(monday,"google","2","b","09:15",2,StartMinute:555,EndMinute:630);
var t3=new AgendaRow(monday,"local","3","point","09:30",2,StartMinute:570);
var t4=new AgendaRow(monday,"local","4","later","12:00",2,StartMinute:720);
var t5=new AgendaRow(monday,"local","5","all day","終日",0);
var positions=Agenda.Place(new[]{t1,t2,t3,t4,t5});
Equal(positions.Count,4,"timeline excludes untimed");
Equal(positions.Where(p=>p.Start<700).Select(p=>p.Lane).Distinct().Count(),3,"timeline overlap lanes");
Equal(positions.Single(p=>p.Row.Key=="4").Lanes,1,"timeline separate overlap group");
Equal(positions.Single(p=>p.Row.Key=="3").Row.EndMinute,null,"timeline point retains unknown duration");
Equal(Agenda.Place(new[]{t4 with{StartMinute=1439}})[0].End,1440,"timeline midnight clamp");
var setup=SchoolData.Empty(new DateTime(2026,11,27));
Equal(SchoolSetup.Register(setup,"学年A","科目A",new[]{"A","B","A"}),2,"setup unique groups");
Equal(SchoolSetup.Register(setup,"学年A","科目A",new[]{"A"}),0,"setup idempotent groups");
var groupId=setup.Groups[0].Id;
setup.Timetables[0].Slots.AddRange(new[]{new Slot{Day=1,Period=1,GroupId=groupId},new Slot{Day=1,Period=5,GroupId=groupId}});
var special=new BellProfile{Name="短縮4限",Bells=new(){new Bell{Number=1,Start="09:00",End="09:30"}}};setup.BellProfiles.Add(special);
var date=new DateTime(2026,12,3);var input=new[]{new ScheduleInput(date,"月曜時間割","local"),new ScheduleInput(date,"短縮４限","local")};
var suggestion=SchoolSetup.Suggest(setup,input,date.AddDays(-1)).Single();
Equal(suggestion.Weekday,1,"schedule weekday");
Equal(suggestion.ProfileId,special.Id,"schedule fullwidth and profile");
Equal(suggestion.Automatic,false,"schedule defaults review");
Equal(SchoolSetup.Apply(setup,suggestion,date.AddDays(-1)),true,"schedule apply");
setup.Validate();
var specialDay=LessonEngine.Day(setup,date,date.AddDays(-1));
Equal(specialDay.Lessons.Single(l=>l.Period==1).Start,"09:00","schedule special start");
Equal(specialDay.Lessons.Single(l=>l.Period==5).State,"cancelled","schedule cutoff");
Equal(SchoolSetup.Apply(setup,suggestion,date.AddDays(-1)),false,"schedule preserves existing change");
var conflict=SchoolSetup.Suggest(setup,new[]{new ScheduleInput(date.AddDays(1),"月曜時間割","local"),new ScheduleInput(date.AddDays(1),"火曜時間割","local")},date).Single();
Equal(conflict.Block!="",true,"schedule conflict");
Equal(SchoolSetup.Suggest(setup,new[]{new ScheduleInput(date.AddDays(1),"休校のお知らせ","local")},date).Count,0,"schedule not substring closure");
var pasted=SchoolSetup.PasteSlots(setup,"A・科目A\t\t\t\t",new[]{1});
Equal(pasted.Single().GroupId,groupId,"setup pasted slot");
var autoDate=date.AddDays(2);
setup.ScheduleRules.Add(new ScheduleRule{Title="特例",Source="local",Weekday=1,Automatic=true});
var auto=SchoolSetup.Suggest(setup,new[]{new ScheduleInput(autoDate,"特例","local")},date).Single();
Equal(auto.Automatic,true,"explicit automatic rule");
Equal(SchoolSetup.Suggest(setup,new[]{new ScheduleInput(autoDate,"特例","google")},date).Count,0,"rule source isolation");
setup.Actuals.Add(new ActualLesson{Date=SchoolData.Iso(autoDate),Period=1,GroupId=groupId,Start="08:40",End="09:30",Held=true});
Equal(SchoolSetup.Suggest(setup,new[]{new ScheduleInput(autoDate,"特例","local")},date).Single().Block!="",true,"confirmed day protected");
Equal(SchoolSetup.Apply(setup,auto,date),false,"stale suggestion actual guard");
Equal(LessonEngine.Day(setup,autoDate,autoDate).Lessons.Single(l=>l.Period==1).State,"confirmed","actual survives special schedule");
Equal(SchoolSetup.Suggest(setup,new[]{new ScheduleInput(date.AddDays(-1),"月曜時間割","local")},date).Single().Block!="",true,"past protected");
var migrationPath=Path.Combine(Path.GetTempPath(),"komu-schema-"+Guid.NewGuid().ToString("N")+".json");
try{var migrationStore=new LocalStore(migrationPath);migrationStore.Save(setup);Equal(migrationStore.Load(date).Schema,2,"special schedule schema rollback guard");Equal(migrationStore.Load(date).BellProfiles.Count,1,"profile round trip");}finally{File.Delete(migrationPath);}
var rosterSchool=SchoolData.Empty(new DateTime(2026,4,1));
SchoolSetup.Register(rosterSchool,"架空学年","架空科目",new[]{"A","B"});
var rosterGrade=rosterSchool.Grades[0].Id;
var rosterMap=new Dictionary<string,string>{{"1",rosterSchool.Classes[0].Id},{"2",rosterSchool.Classes[1].Id}};
var rosterText="\t組クミ\t番バン\t3氏名シメイ\t姓(カナ)\t名(カナ)\n101\t１\t１\t架空生徒甲\tカクウ\tコウ\n102\t1\t2\t\t\t\n201\t2\t1\t架空生徒乙\tカクウ\tオツ";
var rosterParsed=RosterImport.Parse(rosterText);
Equal(rosterParsed.Rows.Count,2,"roster original six columns");
Equal(rosterParsed.BlankRows,1,"roster empty name excluded");
Equal(rosterParsed.InvalidRows.Count,0,"roster original headers");
var rosterPreview=RosterImport.Preview(rosterSchool,rosterGrade,rosterParsed,rosterMap,Array.Empty<RosterStudent>());
var rosterSaved=RosterImport.Add(rosterSchool.YearFrom,Array.Empty<RosterStudent>(),rosterPreview);
Equal(rosterSaved.Count,2,"roster separate classes same number");
Equal(RosterImport.Preview(rosterSchool,rosterGrade,rosterParsed,rosterMap,rosterSaved).Count(r=>r.Status=="既存"),2,"roster reimport no duplicates");
var changed=RosterImport.Parse("1\t1\t架空別名\t\t");
Equal(RosterImport.Preview(rosterSchool,rosterGrade,changed,rosterMap,rosterSaved).Single().Status,"差異・要確認","roster no automatic overwrite");
var duplicated=RosterImport.Parse("1\t1\t架空甲\t\t\n1\t1\t架空乙\t\t");
Equal(RosterImport.Preview(rosterSchool,rosterGrade,duplicated,rosterMap,Array.Empty<RosterStudent>()).Count(r=>r.Status=="重複・要確認"),2,"roster input conflict");
try{RosterImport.Preview(rosterSchool,rosterGrade,rosterParsed,new Dictionary<string,string>{{"1",rosterSchool.Classes[0].Id},{"2",rosterSchool.Classes[0].Id}},rosterSaved);throw new Exception("duplicate mapping accepted");}catch(InvalidDataException){Equal(true,true,"roster explicit one-to-one mapping");}
Console.WriteLine($"TOTAL {passed} PASS");
