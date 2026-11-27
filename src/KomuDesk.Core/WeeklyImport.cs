using System.Globalization;
namespace KomuDesk.Core;
public sealed record WeeklyCell(int Day,int Period,string Label);
public sealed record WeeklyPlan(SchoolData Data,IReadOnlyList<WeeklyCell> Cells,int AddedGroups,int ReplacedSlots);
public static class WeeklyImport
{
    public static IReadOnlyList<WeeklyCell> Read(string path)
    {
        using var book=new LegacyWorkbook(path);var rows=book.Rows(LegacyImport.TimetableSheet);
        string Get(Dictionary<string,LegacyWorkbook.Cell> row,string col)
        {
            if(!row.TryGetValue(col,out var cell))return "";
            if(cell.Error||cell.CacheMissing)throw new InvalidDataException("時間割に計算値なし・セルエラーがあります。");
            return cell.Value.Trim();
        }
        if(!rows.TryGetValue(2,out var header)||"BCDEFGHI".Select((c,i)=>Get(header,c.ToString())==(i+1).ToString(CultureInfo.InvariantCulture)).Any(ok=>!ok))throw new InvalidDataException("元の時間割（入力）シートの曜日コード行と一致しません。");
        var cells=new List<WeeklyCell>();
        for(int r=3;r<=8;r++)
        {
            if(!rows.TryGetValue(r,out var row)||Get(row,"A")!=(r-2).ToString(CultureInfo.InvariantCulture))throw new InvalidDataException("元の時間割の時限行と一致しません。");
            for(int j=0;j<7;j++)
            {
                var label=Get(row,((char)('B'+j)).ToString());if(label is "" or "-" or "－" or "―")continue;
                if(label is "考査" or "【考査】")throw new InvalidDataException("通常の週間時間割に考査マーカーがあります。");
                cells.Add(new((j+1)%7,r-2,label));
            }
        }
        if(cells.Count==0)throw new InvalidDataException("授業が見つかりません。空の時間割は取り込みません。");
        return cells;
    }
    public static WeeklyPlan Prepare(SchoolData source,string versionId,string grade,IReadOnlyList<WeeklyCell> cells)
    {
        if(string.IsNullOrWhiteSpace(grade)||cells.Count==0)throw new InvalidDataException("学年と時間割を確認してください。");
        grade=grade.Trim();var draft=source.Clone();var version=draft.Timetables.Single(t=>t.Id==versionId);
        int old=version.Slots.Count,before=draft.Groups.Count;var slots=new List<Slot>();
        foreach(var cell in cells)
        {
            if(!draft.Bells.Any(b=>b.Number==cell.Period))throw new InvalidDataException("先に"+cell.Period+"限の通常時程を登録してください。");
            var parts=cell.Label.Split('・',2);string cls=parts[0].Trim(),subject=parts.Length==2?parts[1].Trim():"その他授業";
            SchoolSetup.Register(draft,grade,subject,new[]{cls});
            var g=draft.Grades.Single(x=>x.Name==grade);var c=draft.Classes.Single(x=>x.GradeId==g.Id&&x.Name==cls);var s=draft.Subjects.Single(x=>x.Name==subject);
            var matches=draft.Groups.Where(x=>x.ClassId==c.Id&&x.SubjectId==s.Id).ToArray();
            if(matches.Length!=1)throw new InvalidDataException("同じ担当授業が複数あります。個別編集で整理してください。");
            slots.Add(new Slot{Day=cell.Day,Period=cell.Period,GroupId=matches[0].Id});
        }
        version.Slots=slots;draft.Validate();return new(draft,cells,draft.Groups.Count-before,old);
    }
}
