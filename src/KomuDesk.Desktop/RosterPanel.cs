using System.IO;
using System.Windows;
using System.Windows.Controls;
using KomuDesk.Core;
namespace KomuDesk.Desktop;
internal sealed partial class DeskWindow
{
    private List<RosterStudent> demoRoster=new();
    private List<RosterStudent> ReadRoster()=>demo?demoRoster.ToList():RosterVault.Load(RosterVault.PathName);
    private void SaveRoster(List<RosterStudent> rows){if(demo)demoRoster=rows;else RosterVault.Save(RosterVault.PathName,rows);}
    private sealed record RosterDisplay(string Id,string Class,int Number,string Name,string SurnameKana,string GivenKana);
    private sealed record RosterDisplayPreview(int Row,string ClassCode,string Class,int Number,string Name,string Status);
    private void RosterSettings()
    {
        if(data.Classes.Count==0){MessageBox.Show(management??this,"先に学年とクラスを登録してください。");return;}
        try
        {
            var existing=ReadRoster();var w=Dialog("生徒名列",880,690);
            var tabs=new TabControl{Margin=new Thickness(12)};var roster=new TabItem{Header="登録済み名列"};var paste=new TabItem{Header="Excelから貼り付け"};tabs.Items.Add(roster);tabs.Items.Add(paste);
            var list=new DockPanel();var top=new StackPanel();
            var filter=new ComboBox{ItemsSource=new[]{new Choice("","全クラス")}.Concat(data.Classes.Select(c=>new Choice(c.Id,(data.Grades.Find(g=>g.Id==c.GradeId)?.Name??"")+" "+c.Name))).ToArray(),DisplayMemberPath="Label",SelectedValuePath="Id",SelectedIndex=0};
            var status=Text("",12,false,Muted);Add(top,Text("登録済み名列",20,true),Text("年度 "+data.YearFrom+" ～ "+data.YearTo+" · この画面だけで氏名を表示",12,false,Muted),filter,status);DockPanel.SetDock(top,Dock.Top);list.Children.Add(top);
            var grid=Table();grid.IsReadOnly=true;grid.SelectionMode=DataGridSelectionMode.Extended;
            foreach(var (label,key) in new[]{("組","Class"),("番","Number"),("氏名","Name"),("姓カナ","SurnameKana"),("名カナ","GivenKana")})grid.Columns.Add(Col(label,key));
            void Refresh()
            {
                var id=filter.SelectedValue as string??"";
                var rows=existing.Where(r=>r.Year==data.YearFrom&&(id==""||r.ClassId==id)).OrderBy(r=>r.ClassId).ThenBy(r=>r.Number).Select(r=>new RosterDisplay(r.Id,data.Classes.Find(c=>c.Id==r.ClassId)?.Name??"未登録クラス",r.Number,r.Name,r.SurnameKana,r.GivenKana)).ToArray();
                grid.ItemsSource=rows;status.Text=rows.Length+"人";
            }
            filter.SelectionChanged+=(_,_)=>Refresh();
            var bar=new WrapPanel();Add(bar,Tool("trash","選択した名列を削除",()=>{
                var ids=grid.SelectedItems.Cast<RosterDisplay>().Select(r=>r.Id).ToHashSet();if(ids.Count==0)return;
                if(MessageBox.Show(w,ids.Count+"件の名列を削除しますか？","確認",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
                try{var next=existing.Where(r=>!ids.Contains(r.Id)).ToList();SaveRoster(next);existing=next;Refresh();}catch{MessageBox.Show(w,"名列の保存に失敗しました。既存データを確認してください。");}
            }),Tool("spreadsheet","貼り付けへ",()=>tabs.SelectedItem=paste),Tool("close","閉じる",w.Close));
            DockPanel.SetDock(bar,Dock.Bottom);list.Children.Add(bar);list.Children.Add(grid);roster.Content=list;
            paste.Content=RosterPasteView(w,()=>existing,rows=>{SaveRoster(rows);existing=rows;Refresh();tabs.SelectedItem=roster;});
            Refresh();w.Content=tabs;w.ShowDialog();
        }
        catch{MessageBox.Show(management??this,"名列を読み込めませんでした。既存ファイルは上書きしていません。");}
    }
    private FrameworkElement RosterPasteView(Window owner,Func<List<RosterStudent>> current,Action<List<RosterStudent>> saved)
    {
        var root=new DockPanel();var top=new StackPanel();
        Add(top,Text("元Excel「名列貼り付け」",20,true),Text("A:F（コード・組・番・氏名・姓カナ・名カナ）またはB:Fをコピー。空欄氏名は除外。",12,false,Muted));
        var grade=new ComboBox{ItemsSource=data.Grades.Select(g=>new Choice(g.Id,g.Name)).ToArray(),DisplayMemberPath="Label",SelectedValuePath="Id",SelectedIndex=data.Grades.Count==1?0:-1};Field(top,"対象学年",grade);
        var input=new TextBox{AcceptsReturn=true,AcceptsTab=true,Height=85,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto};Field(top,"貼り付け欄",input);
        var mapping=new WrapPanel();var mappings=new Dictionary<string,ComboBox>();var status=Text("対象学年を選び、Excelの範囲を貼り付けてください。",12,false,Muted);
        var grid=Table();grid.IsReadOnly=true;foreach(var (label,key) in new[]{("元行","Row"),("元の組","ClassCode"),("登録先","Class"),("番","Number"),("氏名","Name"),("判定","Status")})grid.Columns.Add(Col(label,key));
        RosterParse? parsed=null;IReadOnlyList<RosterPreview>? plan=null;Button? apply=null;
        void Invalidate(){plan=null;grid.ItemsSource=null;if(apply!=null)apply.IsEnabled=false;}
        void Reset(){Invalidate();parsed=null;mappings.Clear();mapping.Children.Clear();status.Text="組の対応を確認してください。";}
        void Parse()
        {
            Reset();
            try
            {
                parsed=RosterImport.Parse(input.Text);var classes=data.Classes.Where(c=>c.GradeId==(grade.SelectedValue as string)).ToArray();
                if(classes.Length==0){status.Text="対象学年・クラスを登録してください。";return;}
                foreach(var code in parsed.Rows.Select(r=>r.ClassCode).Distinct().Order())
                {
                    var cb=new ComboBox{ItemsSource=classes.Select(c=>new Choice(c.Id,c.Name)).ToArray(),DisplayMemberPath="Label",SelectedValuePath="Id",Width=110};
                    var match=classes.Where(c=>RosterImport.NormalizeCode(c.Name)==code).ToArray();if(match.Length==1)cb.SelectedValue=match[0].Id;
                    cb.SelectionChanged+=(_,_)=>Invalidate();mappings.Add(code,cb);
                    var item=new StackPanel{Margin=new Thickness(3)};Field(item,"元の組 "+code,cb);mapping.Children.Add(item);
                }
                status.Text=$"氏名あり {parsed.Rows.Count}行 / 空欄 {parsed.BlankRows}行 / 形式エラー {parsed.InvalidRows.Count}行。組の対応を指定してプレビュー。";
            }
            catch{status.Text="貼り付け形式を確認してください。";}
        }
        void Preview()
        {
            Invalidate();if(parsed==null)return;
            try
            {
                var map=mappings.ToDictionary(k=>k.Key,k=>k.Value.SelectedValue as string??"");
                plan=RosterImport.Preview(data,grade.SelectedValue as string??"",parsed,map,current());
                grid.ItemsSource=plan.Select(p=>new RosterDisplayPreview(p.Input.Row,p.Input.ClassCode,p.ClassName,p.Input.Number,p.Input.Name,p.Status)).ToArray();
                status.Text=$"追加 {plan.Count(p=>p.Status=="追加")} / 既存 {plan.Count(p=>p.Status=="既存")} / 要確認 {plan.Count(p=>p.Status.Contains("要確認"))+parsed.InvalidRows.Count}。差異は自動上書きしません。";
                if(parsed.InvalidRows.Count>0)status.Text+=" 形式エラー行: "+string.Join(", ",parsed.InvalidRows.Take(10));
                if(apply!=null)apply.IsEnabled=plan.Any(p=>p.Status=="追加");
            }
            catch{status.Text="元の組と登録先クラスを一対一で指定してください。";}
        }
        grade.SelectionChanged+=(_,_)=>Reset();input.TextChanged+=(_,_)=>Reset();
        var tools=new WrapPanel();Add(tools,Tool("spreadsheet","クリップボードから貼付",()=>{try{input.Text=System.Windows.Clipboard.GetText();Parse();}catch{status.Text="クリップボードを読み込めませんでした。";}}),Tool("classes","組を確認",Parse),Tool("check","プレビュー",Preview));
        Add(top,tools,new ScrollViewer{Content=mapping,MaxHeight=125,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},status);DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
        var footer=new StackPanel();var actions=new WrapPanel();
        apply=Primary("save","追加する",()=>{
            if(plan==null)return;
            if(MessageBox.Show(owner,"追加対象だけを登録します。既存の氏名を変更・削除しません。","確認",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
            try{var next=RosterImport.Add(data.YearFrom,current(),plan);saved(next);input.Clear();Reset();}catch{status.Text="保存に失敗しました。元の名列は変更していません。";}
        });apply.IsEnabled=false;Add(actions,apply,Tool("close","貼り付けを消去",()=>input.Clear()));
        Add(footer,Text("名列はWindowsユーザーに紐づく暗号化ファイルに保存。通常のバックアップ・Google連携・帳票には含めません。",11,false,Muted),actions);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);root.Children.Add(grid);return root;
    }
    internal void CheckRosterVault()
    {
        if(!demo)throw new InvalidOperationException("demo only");
        var root=Path.Combine(Path.GetTempPath(),"komu-roster-"+Guid.NewGuid().ToString("N"));var path=Path.Combine(root,"roster.bin");
        try
        {
            var list=new List<RosterStudent>{new(Guid.NewGuid().ToString("N"),"2026-04-01","fictional-class",1,"架空名列テスト","カクウ","テスト","101")};
            RosterVault.Save(path,list);if(!RosterVault.Load(path).SequenceEqual(list))throw new Exception("roster roundtrip");
            if(System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains(list[0].Name))throw new Exception("plaintext roster");
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
