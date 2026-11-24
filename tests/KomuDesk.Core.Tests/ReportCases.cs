using KomuDesk.Core;
using System.IO.Compression;
using System.Xml.Linq;
internal static class ReportCases
{
    public static int Run()
    {
        int passed=0;
        void Check(bool ok,string title){if(!ok)throw new Exception(title);Console.WriteLine("PASS "+title);passed++;}
        var date=new DateTime(2026,4,6);
        var d=SchoolData.Empty(date);
        var g=new Grade{Name="架空学年"};var c=new SchoolClass{Name="A",GradeId=g.Id};var s=new Subject{Name="架空科目"};var group=new TeachingGroup{ClassId=c.Id,SubjectId=s.Id};
        d.Grades.Add(g);d.Classes.Add(c);d.Subjects.Add(s);d.Groups.Add(group);
        d.Timetables[0].Slots.Add(new Slot{Day=1,Period=1,GroupId=group.Id});
        d.Timetables[0].Slots.Add(new Slot{Day=1,Period=2,GroupId=group.Id});
        d.Changes.Add(new LessonChange{Date=SchoolData.Iso(date),Period=2,Action="cancel",Reason="架空休講"});
        var now=date.AddHours(12);
        LessonEngine.Confirm(d,LessonEngine.Day(d,date,now).Lessons[0],true,"=1+1 & <架空>");
        d.Validate();
        var lines=LessonReports.Lines(d,date,date,now);
        Check(lines.Count==2&&lines[0].Number==1&&lines[1].Number==null,"report numbering skips cancelled");
        Check(lines[0].State=="確定"&&lines[0].Note.StartsWith("="),"report actual note preserved");
        var path=Path.Combine(Path.GetTempPath(),"report-"+Guid.NewGuid().ToString("N")+".xlsx");
        try
        {
            LessonReports.WriteXlsx(path,LessonReports.Tables(d,date,date,now));
            using(var wb=new LegacyWorkbook(path))
            {
                Check(wb.SheetNames.Count==2,"report workbook has two sheets");
                var cells=wb.Rows("授業日・進度");
                Check(cells[2]["I"].Value=="=1+1 & <架空>"&&!cells[2]["I"].HasFormula,"report note safely stored as text");
            }
            try{LessonReports.WriteXlsx(path,LessonReports.Tables(d,date,date,now));throw new Exception("overwrite accepted");}catch(IOException){Check(true,"report does not overwrite");}
        }
        finally{if(File.Exists(path))File.Delete(path);}
        return passed;
    }
}
