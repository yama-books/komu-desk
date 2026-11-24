using System.IO.Compression;
using System.Text;
using KomuDesk.Core;
internal static class ImportCases
{
    public static int Run()
    {
        int passed=0;
        void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);passed++;}
        var dir=Path.Combine(Path.GetTempPath(),"komu-import-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        try
        {
            string path=Path.Combine(dir,"fictional.xlsx");
            Build(path,false);
            var original=File.ReadAllBytes(path);
            var plan=LegacyImport.Prepare(path,"架空学年",true);
            Check(plan.DailyRows==3&&plan.ExamDays==1,"legacy daily/exam dates");
            Check(plan.EventRows==2,"same-day duplicate-looking events preserved");
            Check(plan.TotalCached==4&&plan.TotalRecalculated==4,"legacy cache reconciliation");
            Check(plan.Data.Actuals.Count==0,"cached plan not confirmed actual");
            Check(plan.Data.Classes.Any(c=>c.Name=="A")&&plan.Data.Subjects.Any(s=>s.Name=="国語"),"shared string excludes phonetic annotation");
            Check(plan.Data.Timetables[0].To=="2026-04-08","no extrapolation beyond daily table");
            Check(LegacyImport.Prepare(path,"架空学年",false).TotalRecalculated==2,"legacy exclude exam");
            Check(File.ReadAllBytes(path).SequenceEqual(original),"source unchanged");
            Check(!LegacyImport.IsEmpty(plan.Data),"repeat initial import blocked");
            Build(path,true);
            try{LegacyImport.Prepare(path,"架空学年",true);throw new Exception("missing cache accepted");}catch(InvalidDataException){Check(true,"missing cache rejected");}
        }
        finally{Directory.Delete(dir,true);}
        return passed;
    }
    private static void Build(string path,bool broken)
    {
        const string ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string rel="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        using var f=File.Create(path);using var z=new ZipArchive(f,ZipArchiveMode.Create);
        void Put(string name,string xml){using var w=new StreamWriter(z.CreateEntry(name).Open(),new UTF8Encoding(false));w.Write(xml);}
        string Cell(string a,string v)=>$"<c r='{a}' t='inlineStr'><is><t>{v}</t></is></c>";
        string Row(int n,string cells)=>$"<row r='{n}'>{cells}</row>";
        string Sheet(string rows)=>$"<worksheet xmlns='{ns}'><sheetData>{rows}</sheetData></worksheet>";
        Put("xl/workbook.xml",$"<workbook xmlns='{ns}' xmlns:r='{rel}'><sheets><sheet name='{LegacyImport.EventsSheet}' sheetId='1' r:id='rId1'/><sheet name='{LegacyImport.TimetableSheet}' sheetId='2' r:id='rId2'/><sheet name='{LegacyImport.DailySheet}' sheetId='3' r:id='rId3'/></sheets></workbook>");
        Put("xl/_rels/workbook.xml.rels","<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'>"+string.Concat(Enumerable.Range(1,3).Select(i=>$"<Relationship Id='rId{i}' Type='{rel}/worksheet' Target='worksheets/sheet{i}.xml'/>"))+"</Relationships>");
        Put("xl/sharedStrings.xml",$"<sst xmlns='{ns}'><si><t>A・国語</t><rPh sb='0' eb='1'><t>よみ</t></rPh></si></sst>");
        Put("xl/worksheets/sheet1.xml",Sheet(Row(1,Cell("B1","2026-04-06")+Cell("C1","架空行事"))+Row(2,Cell("B2","2026-04-06")+Cell("C2","架空行事"))));
        string week=Row(2,string.Concat("BCDEFGHI".Select((c,i)=>Cell(c+"2",(i+1).ToString()))));
        for(int r=3;r<=8;r++)week+=Row(r,Cell("A"+r,(r-2).ToString())+(r==3?$"<c r='B{r}' t='s'><v>0</v></c>":r==4?Cell("B"+r,"B・国語"):""));
        Put("xl/worksheets/sheet2.xml",Sheet(week));
        string daily=Row(5,Cell("D5","2026-04-06")+Cell("E5","1")+(broken?"<c r='G5'><f>INDEX(A1:B2,1,1)</f></c>":"<c r='G5' t='str'><f>INDEX(A1:B2,1,1)</f><v>A・国語</v></c>")+Cell("H5","B・国語"))
            +Row(6,Cell("D6","2026-04-07")+Cell("E6","7"))
            +Row(7,Cell("D7","2026-04-08")+Cell("E7","8")+Cell("G7","【考査】")+Cell("H7","A・国語")+Cell("I7","B・国語"));
        Put("xl/worksheets/sheet3.xml",Sheet(daily));
    }
}
