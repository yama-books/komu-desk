using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace KomuDesk.Core;

// Read-only OpenXML cache reader; deliberately no formula evaluator or external links.
public sealed class LegacyWorkbook : IDisposable
{
    private const long MaxTotal = 100_000_000, MaxXml = 20_000_000;
    private readonly FileStream file;
    private readonly ZipArchive archive;
    private readonly Dictionary<string,string> sheets = new(StringComparer.Ordinal);
    private readonly List<string> shared = new();
    private readonly bool date1904;
    private static readonly XNamespace M = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    public LegacyWorkbook(string path)
    {
        file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (file.Length > 50_000_000) throw new InvalidDataException("Excelファイルが大きすぎます。");
            archive = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen:true);
            if (archive.Entries.Count > 5000 || archive.Entries.Sum(e=>e.Length) > MaxTotal) throw new InvalidDataException("Excel展開量の上限を超えています。");
            if (archive.Entries.Any(e=>e.FullName.StartsWith("/") || e.FullName.Contains('\\') || e.FullName.Split('/').Contains(".."))) throw new InvalidDataException("安全でない内部パスです。");
            var workbook = Read("xl/workbook.xml");
            date1904 = (string?)workbook.Root?.Element(M+"workbookPr")?.Attribute("date1904") is "1" or "true";
            var relations=Read("xl/_rels/workbook.xml.rels").Root!.Elements().ToDictionary(x=>(string)x.Attribute("Id")!,x=>x);
            foreach(var s in workbook.Descendants(M+"sheet"))
            {
                string name=(string)s.Attribute("name")!;
                string id=(string)s.Attribute(R+"id")!;
                if(!relations.TryGetValue(id,out var rel) || (string?)rel.Attribute("TargetMode")=="External" || !((string?)rel.Attribute("Type")??"").EndsWith("/worksheet",StringComparison.Ordinal)) continue;
                string target=(string)rel.Attribute("Target")!;
                string resolved=target.StartsWith("/")?target.TrimStart('/'):"xl/"+target;
                if(!Regex.IsMatch(resolved,@"^xl/worksheets/[A-Za-z0-9_.-]+\.xml$")) throw new InvalidDataException("非対応のワークシート内部パスです。");
                sheets.Add(name,resolved);
            }
            if(archive.GetEntry("xl/sharedStrings.xml")!=null) foreach(var s in Read("xl/sharedStrings.xml").Root!.Elements(M+"si")) shared.Add(TextOf(s));
        }
        catch { file.Dispose(); throw; }
    }
    public IReadOnlyCollection<string> SheetNames => sheets.Keys;
    private XDocument Read(string name)
    {
        var entry=archive.GetEntry(name)??throw new InvalidDataException("Excelの必要な内部データがありません。");
        if(entry.Length>MaxXml) throw new InvalidDataException("ExcelのXMLが大きすぎます。");
        using var stream=entry.Open();
        using var xml=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaxXml});
        return XDocument.Load(xml);
    }
    private static string TextOf(XElement x) => string.Concat(x.Elements().Where(e=>e.Name==M+"t"||e.Name==M+"r").Select(e=>e.Name==M+"t"?e.Value:string.Concat(e.Elements(M+"t").Select(t=>t.Value))));
    public sealed record Cell(string Value, bool HasFormula, bool CacheMissing, bool Error);
    public Dictionary<int,Dictionary<string,Cell>> Rows(string sheet)
    {
        if(!sheets.TryGetValue(sheet,out var target)) throw new InvalidDataException("選択したシートが見つかりません。");
        var result=new Dictionary<int,Dictionary<string,Cell>>();
        foreach(var row in Read(target).Descendants(M+"row"))
        {
            if(!int.TryParse((string?)row.Attribute("r"),out int number)||number<1||number>100000) throw new InvalidDataException("行番号が不正です。");
            var cells=new Dictionary<string,Cell>(StringComparer.Ordinal);
            foreach(var c in row.Elements(M+"c"))
            {
                string address=(string?)c.Attribute("r")??"";
                var match=Regex.Match(address,@"^([A-Z]{1,3})([0-9]{1,6})$");
                if(!match.Success) throw new InvalidDataException("セル位置が不正です。");
                string col=match.Groups[1].Value;
                string type=(string?)c.Attribute("t")??"";
                var v=c.Element(M+"v"); bool formula=c.Element(M+"f")!=null;
                string text=v?.Value??"";
                if(type=="s"&&text!="")
                {
                    if(!int.TryParse(text,out int si)||si<0||si>=shared.Count) throw new InvalidDataException("文字列参照が不正です。");
                    text=shared[si];
                }
                if(type=="inlineStr"&&c.Element(M+"is") is XElement inline)text=TextOf(inline);
                cells.Add(col,new Cell(text,formula,formula&&(v==null||v.Value==""),type=="e"));
            }
            if(cells.Count>0)result.Add(number,cells);
        }
        return result;
    }
    public DateTime Date(string text)
    {
        if(double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out double serial))
        {
            if(!double.IsFinite(serial)||serial<0||serial>100000||Math.Abs(serial-Math.Round(serial))>0.0000001)throw new InvalidDataException("日付セルに時刻や不正な値があります。");
            if(date1904)return new DateTime(1904,1,1).AddDays(serial);
            if(serial<61)throw new InvalidDataException("1900年初頭の日付は対象外です。");
            return DateTime.FromOADate(serial).Date;
        }
        if(DateTime.TryParseExact(text,new[]{"yyyy-MM-dd","yyyy/M/d","yyyy/MM/dd"},CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))return date.Date;
        throw new InvalidDataException("日付形式を読み取れません。");
    }
    public void Dispose(){archive.Dispose();file.Dispose();}
}
