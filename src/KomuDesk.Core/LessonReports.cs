using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Text;

namespace KomuDesk.Core;
public sealed record ReportLine(string Date,int Period,string Group,string Kind,string State,string Start,string End,string Note,int? Number);
public sealed record ReportTable(string Name,string[] Headers,IReadOnlyList<object?[]> Rows);
public static class LessonReports
{
    public static List<ReportLine> Lines(SchoolData data,DateTime from,DateTime to,DateTime now,string? groupId=null)
    {
        _=LessonEngine.Count(data,from,to,now);
        var counts=new Dictionary<string,int>();
        var rows=new List<ReportLine>();
        for(var date=from.Date;date<=to.Date;date=date.AddDays(1))
        foreach(var l in LessonEngine.Day(data,date,now).Lessons)
        {
            if(groupId!=null&&l.GroupId!=groupId)continue;
            int? n=null;
            if((l.Kind!="exam"||data.CountExams)&&l.State is "confirmed" or "estimated" or "remaining")
            {n=counts.GetValueOrDefault(l.GroupId)+1;counts[l.GroupId]=n.Value;}
            rows.Add(new(l.Date,l.Period,data.GroupName(l.GroupId),l.Kind=="exam"?"考査":"通常",l.State switch{"confirmed"=>"確定","estimated"=>"推定","remaining"=>"予定","cancelled"=>"休講・不実施",_=>"要確認"},l.Start,l.End,l.Note,n));
        }
        return rows;
    }
    public static IReadOnlyList<ReportTable> Tables(SchoolData data,DateTime from,DateTime to,DateTime now,string? groupId=null)
    {
        var lines=Lines(data,from,to,now,groupId);
        var summary=LessonEngine.Count(data,from,to,now).Where(x=>groupId==null||x.GroupId==groupId).Select(x=>new object?[]{data.GroupName(x.GroupId),x.Confirmed,x.Estimated,x.Remaining,x.Forecast,x.Cancelled,x.Unresolved}).ToList();
        return new[]{
            new ReportTable("授業数",new[]{"担当授業","確定","推定","残予定","見込み","休講","要確認"},summary),
            new ReportTable("授業日・進度",new[]{"日付","時限","担当授業","種別","状態","開始","終了","期間内回次","記録・理由"},lines.Select(x=>new object?[]{x.Date,x.Period,x.Group,x.Kind,x.State,x.Start,x.End,x.Number,x.Note}).ToList())
        };
    }
    public static void WriteXlsx(string path,IReadOnlyList<ReportTable> tables)
    {
        if(tables.Count==0||tables.Count>10)throw new ArgumentException("出力シート数が不正です。");
        string full=Path.GetFullPath(path);
        if(!full.EndsWith(".xlsx",StringComparison.OrdinalIgnoreCase)||File.Exists(full))throw new IOException("新しいxlsxファイル名を選択してください。");
        string temp=full+"."+Guid.NewGuid().ToString("N")+".tmp";
        const string m="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string rel="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        const string pkg="http://schemas.openxmlformats.org/package/2006/relationships";
        try
        {
            using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write))
            using(var zip=new ZipArchive(stream,ZipArchiveMode.Create))
            {
                void Put(string name,Action<XmlWriter> write)
                {
                    using var entry=zip.CreateEntry(name).Open();
                    using var xml=XmlWriter.Create(entry,new XmlWriterSettings{Encoding=new UTF8Encoding(false),CloseOutput=false});
                    xml.WriteStartDocument();write(xml);xml.WriteEndDocument();
                }
                Put("[Content_Types].xml",w=>{
                    string ns="http://schemas.openxmlformats.org/package/2006/content-types";
                    w.WriteStartElement("Types",ns);
                    void Item(string name,string key,string value,string type){w.WriteStartElement(name,ns);w.WriteAttributeString(key,value);w.WriteAttributeString("ContentType",type);w.WriteEndElement();}
                    Item("Default","Extension","rels","application/vnd.openxmlformats-package.relationships+xml");
                    Item("Default","Extension","xml","application/xml");
                    Item("Override","PartName","/xl/workbook.xml","application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
                    for(int i=1;i<=tables.Count;i++)Item("Override","PartName",$"/xl/worksheets/sheet{i}.xml","application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
                    w.WriteEndElement();
                });
                void Relationship(XmlWriter w,string id,string type,string target){w.WriteStartElement("Relationship",pkg);w.WriteAttributeString("Id",id);w.WriteAttributeString("Type",rel+"/"+type);w.WriteAttributeString("Target",target);w.WriteEndElement();}
                Put("_rels/.rels",w=>{w.WriteStartElement("Relationships",pkg);Relationship(w,"rId1","officeDocument","xl/workbook.xml");w.WriteEndElement();});
                Put("xl/_rels/workbook.xml.rels",w=>{w.WriteStartElement("Relationships",pkg);for(int i=1;i<=tables.Count;i++)Relationship(w,"rId"+i,"worksheet",$"worksheets/sheet{i}.xml");w.WriteEndElement();});
                Put("xl/workbook.xml",w=>{
                    w.WriteStartElement("workbook",m);w.WriteStartElement("sheets",m);
                    for(int i=1;i<=tables.Count;i++){w.WriteStartElement("sheet",m);w.WriteAttributeString("name",tables[i-1].Name);w.WriteAttributeString("sheetId",i.ToString());w.WriteAttributeString("r","id",rel,"rId"+i);w.WriteEndElement();}
                    w.WriteEndElement();w.WriteEndElement();
                });
                for(int i=1;i<=tables.Count;i++)
                {
                    var table=tables[i-1];
                    Put($"xl/worksheets/sheet{i}.xml",w=>{
                        w.WriteStartElement("worksheet",m);w.WriteStartElement("cols",m);
                        for(int col=1;col<=table.Headers.Length;col++){w.WriteStartElement("col",m);w.WriteAttributeString("min",col.ToString());w.WriteAttributeString("max",col.ToString());w.WriteAttributeString("width",table.Headers[col-1].Contains("記録")?"36":table.Headers[col-1]=="担当授業"?"28":"14");w.WriteAttributeString("customWidth","1");w.WriteEndElement();}
                        w.WriteEndElement();w.WriteStartElement("sheetData",m);
                        void Row(int n,IEnumerable<object?> values)
                        {
                            w.WriteStartElement("row",m);w.WriteAttributeString("r",n.ToString(CultureInfo.InvariantCulture));int c=0;
                            foreach(var value in values)
                            {
                                c++;if(value==null)continue;
                                string address=((char)('A'+c-1)).ToString()+n.ToString(CultureInfo.InvariantCulture);
                                w.WriteStartElement("c",m);w.WriteAttributeString("r",address);
                                if(value is int num){w.WriteElementString("v",m,num.ToString(CultureInfo.InvariantCulture));}
                                else{w.WriteAttributeString("t","inlineStr");w.WriteStartElement("is",m);w.WriteStartElement("t",m);w.WriteAttributeString("xml","space","http://www.w3.org/XML/1998/namespace","preserve");w.WriteString(value.ToString()??"");w.WriteEndElement();w.WriteEndElement();}
                                w.WriteEndElement();
                            }
                            w.WriteEndElement();
                        }
                        Row(1,table.Headers);int row=2;foreach(var values in table.Rows)Row(row++,values);
                        w.WriteEndElement();w.WriteEndElement();
                    });
                }
            }
            File.Move(temp,full);
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
