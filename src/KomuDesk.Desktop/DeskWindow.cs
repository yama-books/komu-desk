using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using KomuDesk.Core;
using Microsoft.Win32;

namespace KomuDesk.Desktop;

internal sealed partial class DeskWindow : Window
{
    private readonly LocalStore store;
    private SchoolData data;
    private readonly bool demo;
    private DateTime day = DateTime.Today;
    private DateTime lastToday = DateTime.Today;
    private readonly StackPanel body = new();
    private readonly TextBlock clock = new();
    private readonly TextBlock next = new();
    private readonly DispatcherTimer timer = new() { Interval=TimeSpan.FromSeconds(5) };
    private bool closing;
    private static readonly Brush Paper = BrushOf("#F7F7F5"), Ink = BrushOf("#202020"), Muted = BrushOf("#606060"), Line = BrushOf("#D4D4D4");
    private static Brush BrushOf(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
    private static TextBlock Text(string value, double size=14, bool strong=false, Brush? color=null) => new()
    { Text=value, FontSize=size, FontWeight=strong?FontWeights.SemiBold:FontWeights.Normal, Foreground=color??Ink, TextWrapping=TextWrapping.Wrap, VerticalAlignment=VerticalAlignment.Center };
    private static Button Btn(string label, Action action)
    {
        var b=new Button { Content=label, Margin=new Thickness(2), Padding=new Thickness(8,4,8,4), MinHeight=28 };
        b.Click += (_,_) => action(); return b;
    }
    private static StackPanel Horizontal() => new() { Orientation=Orientation.Horizontal };
    private static void Add(Panel p, params UIElement[] items) { foreach(var x in items) p.Children.Add(x); }
    private static Border Rule() => new() { Height=1, Background=Line, Margin=new Thickness(0,10,0,10) };
    private static Border Badge(string title, string hex)
    {
        var color=(Color)ColorConverter.ConvertFromString(hex);
        double Linear(byte b) { var x=b/255.0; return x<=0.04045?x/12.92:Math.Pow((x+0.055)/1.055,2.4); }
        double l=0.2126*Linear(color.R)+0.7152*Linear(color.G)+0.0722*Linear(color.B);
        var fg=l>0.179?Brushes.Black:Brushes.White;
        return new Border { Background=new SolidColorBrush(color), CornerRadius=new CornerRadius(4), Padding=new Thickness(6,2,6,2), Margin=new Thickness(0,1,4,1), Child=Text(title,12,true,fg) };
    }
    public DeskWindow(LocalStore store, SchoolData data, bool demo)
    {
        this.store=store; this.data=data; this.demo=demo;
        Title="KOMU DESK · 校務デスク  v0.1.1-preview"+(demo?" [DEMO]":"");
        FontFamily=new FontFamily("Yu Gothic UI, Meiryo, Segoe UI");
        Background=Paper; Foreground=Ink; MinWidth=280; MinHeight=260;
        Width=Math.Clamp(data.Display.Width,280,800); Height=Math.Clamp(data.Display.Height,260,1000);
        Topmost=data.Display.Topmost;
        var work=SystemParameters.WorkArea;
        WindowStartupLocation=WindowStartupLocation.Manual;
        Width=Math.Min(Width,work.Width); Height=Math.Min(Height,work.Height);
        Left=data.Display.Left<0?work.Right-Width-12:Math.Clamp(data.Display.Left,work.Left,Math.Max(work.Left,work.Right-Width));
        Top=data.Display.Top<0?work.Top+12:Math.Clamp(data.Display.Top,work.Top,Math.Max(work.Top,work.Bottom-Height));
        BuildWidget();
        if(data.Display.Mini)Height=260;
        timer.Tick+=(_,_)=>Tick(); timer.Start();
        Closing+=OnClosing; InitGoogle(); Render(); Tick();
    }
    private void Error(Exception e) => MessageBox.Show(this, e is InvalidDataException or ArgumentException or InvalidOperationException or FormatException ? e.Message : "処理に失敗しました。元データは維持されています。 ("+e.GetType().Name+")", "KOMU DESK");
    private bool Commit(SchoolData candidate)
    {
        try { candidate.Validate(); if(!demo) store.Save(candidate); data=candidate; Render(); return true; }
        catch(Exception e) { Error(e); return false; }
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        timer.Stop(); if(demo||closing) return;
        var d=data.Clone(); d.Display.Left=Left; d.Display.Top=Top; d.Display.Width=Width; if(!data.Display.Mini)d.Display.Height=Height;
        try { store.Save(d); closing=true; }
        catch(Exception ex) { Error(ex); e.Cancel=true; timer.Start(); }
    }
    private void ToggleTop() { var d=data.Clone(); d.Display.Topmost=!Topmost; if(Commit(d)) Topmost=d.Display.Topmost; }
    private void ToggleMini() { var d=data.Clone(); if(!d.Display.Mini)d.Display.Height=Height; d.Display.Mini=!d.Display.Mini; if(Commit(d)) Height=d.Display.Mini?260:Math.Min(SystemParameters.WorkArea.Height,Math.Max(540,d.Display.Height)); }
    private void Tick()
    {
        var now=DateTime.Now;
        if(lastToday!=now.Date){if(day.Date==lastToday)day=now.Date;lastToday=now.Date;Render();}
        var minute=new DateTime(now.Year,now.Month,now.Day,now.Hour,now.Minute,0);
        if(renderedMinute!=minute){renderedMinute=minute;Render();}
    }
    private void Render()=>RenderWidget();
    private void Menu()
    {
        var w=Dialog("設定・管理",420,560); var p=new StackPanel { Margin=new Thickness(14) };
        void Item(string name,Action action)=>p.Children.Add(Btn(name,()=>{w.Close();action();}));
        Item("1  学年・色",Grades); Item("2  クラス・色",Classes); Item("3  科目",Subjects); Item("4  担当授業",Groups);
        Item("5  時程",Bells); Item("6  時間割の版・有効期間",Versions); Item("7  週間時間割",Weekly);
        Item("年度・考査計上・表示",Year); Item("日別変更・個別変更",DayChanges);
        Item("Googleカレンダー（任意）",GoogleSettings); Item("既存Excelから初期取り込み",ImportExcel); Item("予定",Events); Item("タスク",Tasks); Item("授業数集計",Counts); Item("授業日一覧・帳票",Reports); Item("バックアップ（別ファイル）",Backup); Item("バックアップから復元",Restore);
        w.Content=new ScrollViewer { Content=p, VerticalScrollBarVisibility=ScrollBarVisibility.Auto }; w.ShowDialog();
    }
    private Window Dialog(string title,double width=760,double height=500)=>new()
    { Title=title, Owner=this, WindowStartupLocation=WindowStartupLocation.CenterOwner, Width=width, Height=height, MinWidth=320, MinHeight=240, FontFamily=FontFamily, Background=Paper, Foreground=Ink };
    private static DataGridTextColumn Col(string title,string property)=>new()
    { Header=title, Binding=new Binding(property){UpdateSourceTrigger=UpdateSourceTrigger.LostFocus}, Width=new DataGridLength(1,DataGridLengthUnitType.Star) };
    private static DataGridCheckBoxColumn CheckCol(string title,string property)=>new(){Header=title,Binding=new Binding(property),Width=100};
    private static DataGridComboBoxColumn ComboCol(string title,string property,System.Collections.IEnumerable values,string value,string display)=>new()
    { Header=title, ItemsSource=values, SelectedValuePath=value, DisplayMemberPath=display, SelectedValueBinding=new Binding(property), Width=new DataGridLength(1,DataGridLengthUnitType.Star) };
    private void Edit<T>(string title, string hint, Func<SchoolData,List<T>> get, Action<SchoolData,List<T>> set, params DataGridColumn[] columns) where T:class,new()
    {
        var d=data.Clone(); var rows=new ObservableCollection<T>(get(d)); var w=Dialog(title);
        var p=new DockPanel { Margin=new Thickness(14) };
        var tip=Text(hint,13,false,Muted); tip.Margin=new Thickness(0,0,0,8); DockPanel.SetDock(tip,Dock.Top); p.Children.Add(tip);
        var grid=new DataGrid { ItemsSource=rows, AutoGenerateColumns=false, CanUserAddRows=false, CanUserDeleteRows=false, SelectionMode=DataGridSelectionMode.Extended, HeadersVisibility=DataGridHeadersVisibility.Column, RowHeaderWidth=0, Margin=new Thickness(0,0,0,8) };
        foreach(var c in columns) grid.Columns.Add(c);
        var bar=Horizontal(); DockPanel.SetDock(bar,Dock.Bottom);
        Add(bar,Btn("追加",()=>{rows.Add(new T());grid.ScrollIntoView(rows.Last());}),Btn("削除",()=>{ foreach(var row in grid.SelectedItems.Cast<T>().ToList()) rows.Remove(row); }),
            Btn("保存",()=>{ if(!grid.CommitEdit(DataGridEditingUnit.Cell,true)||!grid.CommitEdit(DataGridEditingUnit.Row,true)) return; set(d,rows.ToList()); if(Commit(d)) w.Close(); }),Btn("取消",w.Close));
        p.Children.Add(bar);p.Children.Add(grid);w.Content=p;w.ShowDialog();
    }
    private record Choice(string Id,string Label);
    private Choice[] GroupChoices()=>data.Groups.Select(g=>new Choice(g.Id,data.GroupName(g.Id))).ToArray();
    private void Grades()=>Edit<Grade>("学年","色は #RRGGBB 形式（例 #D9E8D8）。",d=>d.Grades,(d,l)=>d.Grades=l,Col("学年名","Name"),Col("色","Color"));
    private void Classes()=>Edit<SchoolClass>("クラス","学年を選択してクラスを登録します。",d=>d.Classes,(d,l)=>d.Classes=l,ComboCol("学年","GradeId",data.Grades,"Id","Name"),Col("クラス","Name"),Col("色","Color"));
    private void Subjects()=>Edit<Subject>("科目","科目名を登録します。",d=>d.Subjects,(d,l)=>d.Subjects=l,Col("科目","Name"));
    private void Groups()=>Edit<TeachingGroup>("担当授業","クラスと科目を選択。補足名は任意です。",d=>d.Groups,(d,l)=>d.Groups=l,ComboCol("クラス","ClassId",data.Classes.Select(c=>new Choice(c.Id,(data.Grades.Find(g=>g.Id==c.GradeId)?.Name??"")+" "+c.Name)).ToArray(),"Id","Label"),ComboCol("科目","SubjectId",data.Subjects,"Id","Name"),Col("補足名","Name"));
    private void Bells()=>Edit<Bell>("時程","時刻は HH:mm。時限は重複不可です。",d=>d.Bells,(d,l)=>d.Bells=l,Col("時限","Number"),Col("開始","Start"),Col("終了","End"));
    private void Versions()=>Edit<Timetable>("時間割の版","有効期間は yyyy-MM-dd。既存版と重複しないように設定します。",d=>d.Timetables,(d,l)=>d.Timetables=l,Col("名称","Name"),Col("開始日","From"),Col("終了日","To"));
    private void Events()=>Edit<LocalEvent>("予定","日付 yyyy-MM-dd、時刻 HH:mm（空欄は終日）。",d=>d.Events,(d,l)=>d.Events=l,Col("日付","Date"),Col("時刻","Time"),Col("予定名","Title"));
    private void Tasks()=>Edit<Todo>("タスク","期限は yyyy-MM-dd または空欄。",d=>d.Tasks,(d,l)=>d.Tasks=l,Col("タスク","Title"),Col("期限","Due"),CheckCol("完了","Done"));
    private void DayChanges()
    {
        var w=Dialog("日別変更",400,220);var p=new StackPanel{Margin=new Thickness(14)};
        Add(p,Btn("一日単位・曜日変更",()=>{w.Close();Edit<DayChange>("日別変更","曜日：0=日、1=月…6=土、空欄は元の曜日。全休をチェックすると休講。",d=>d.Days,(d,l)=>d.Days=l,Col("日付","Date"),CheckCol("全休","Closed"),Col("曜日","Weekday"),Col("理由","Reason"));}),
            Btn("個別の時限変更",()=>{w.Close();Edit<LessonChange>("個別変更","cancel=休講、replace=置換・追加。normal=通常、exam=考査。",d=>d.Changes,(d,l)=>d.Changes=l,Col("日付","Date"),Col("時限","Period"),Col("処理","Action"),ComboCol("授業","GroupId",GroupChoices(),"Id","Label"),Col("種別","Kind"),Col("理由","Reason"));}));
        w.Content=p;w.ShowDialog();
    }
    private void Weekly()
    {
        if(data.Timetables.Count==0){MessageBox.Show(this,"先に時間割の版を登録してください。");return;}
        var w=Dialog("週間時間割",920,530); var p=new DockPanel{Margin=new Thickness(12)};
        var select=new ComboBox{ItemsSource=data.Timetables.Select(t=>new Choice(t.Id,$"{t.Name}  {t.From} ～ {t.To}")).ToArray(),DisplayMemberPath="Label",SelectedValuePath="Id",Margin=new Thickness(0,0,0,10)};
        DockPanel.SetDock(select,Dock.Top);p.Children.Add(select);
        var panel=new StackPanel(); var footer=Horizontal(); DockPanel.SetDock(footer,Dock.Bottom);p.Children.Add(footer);
        var area=new ScrollViewer{Content=panel,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};p.Children.Add(area);
        var choices=new[]{new Choice("","空き")}.Concat(GroupChoices()).ToArray();
        var boxes=new List<(int day,int period,ComboBox box)>(); string? versionId=null;
        void Build()
        {
            versionId=select.SelectedValue as string;if(versionId==null)return;
            panel.Children.Clear();boxes.Clear();var t=data.Timetables.Single(x=>x.Id==versionId);
            var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(58)});
            for(int i=1;i<=7;i++)grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(113)});
            var bells=data.Bells.OrderBy(b=>b.Number).ToArray();
            for(int i=0;i<=bells.Length;i++)grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            void Put(UIElement x,int r,int c){Grid.SetRow(x,r);Grid.SetColumn(x,c);grid.Children.Add(x);}
            string[] names={"月","火","水","木","金","土","日"};
            for(int i=0;i<7;i++)Put(Text(names[i],14,true),0,i+1);
            for(int i=0;i<bells.Length;i++)
            {
                int n=bells[i].Number;Put(Text($"{n}限",13),i+1,0);
                for(int j=0;j<7;j++)
                {
                    int weekday=(j+1)%7;
                    var cb=new ComboBox{ItemsSource=choices,DisplayMemberPath="Label",SelectedValuePath="Id",SelectedValue=t.Slots.FirstOrDefault(x=>x.Day==weekday&&x.Period==n)?.GroupId??"",Margin=new Thickness(2),MinHeight=38};
                    boxes.Add((weekday,n,cb));Put(cb,i+1,j+1);
                }
            }
            panel.Children.Add(grid);
        }
        select.SelectionChanged+=(_,_)=>Build();select.SelectedValue=data.Timetables.FirstOrDefault(t=>SchoolData.ParseDate(t.From)<=day&&SchoolData.ParseDate(t.To)>=day)?.Id??data.Timetables[0].Id;
        Add(footer,Btn("選択した版を保存",()=>{if(versionId==null)return;var d=data.Clone();d.Timetables.Single(x=>x.Id==versionId).Slots=boxes.Where(x=>(x.box.SelectedValue as string)!="").Select(x=>new Slot{Day=x.day,Period=x.period,GroupId=(string)x.box.SelectedValue}).ToList();if(Commit(d))w.Close();}),Btn("取消",w.Close));
        panel.Children.Add(Text("版を切り替えると未保存の編集は破棄されます。",12,false,Muted));w.Content=p;w.ShowDialog();
    }
    private void Year()
    {
        var w=Dialog("年度・表示設定",400,330);var p=new StackPanel{Margin=new Thickness(14)};
        var from=new TextBox{Text=data.YearFrom};var to=new TextBox{Text=data.YearTo};
        var exam=new CheckBox{Content="考査を授業数に含める",IsChecked=data.CountExams,Margin=new Thickness(0,10,0,0)};
        var free=new CheckBox{Content="空き時間を表示",IsChecked=data.Display.ShowFree};
        Add(p,Text("年度開始 (yyyy-MM-dd)"),from,Text("年度終了"),to,exam,free,Text("時間割の有効期間も年度内に設定してください。",12,false,Muted),
            Btn("保存",()=>{var d=data.Clone();d.YearFrom=from.Text;d.YearTo=to.Text;d.CountExams=exam.IsChecked==true;d.Display.ShowFree=free.IsChecked==true;if(Commit(d))w.Close();}));
        w.Content=p;w.ShowDialog();
    }
    private void LessonDetail(Lesson l)
    {
        var w=Dialog("授業詳細",420,380);var p=new StackPanel{Margin=new Thickness(14)};
        Add(p,Text($"{l.Date}  {l.Period}限  {data.GroupName(l.GroupId)}",18,true),Text($"{l.Start}–{l.End}  {l.State}",13,false,Muted),Text("実施記録・メモ"));
        var previous=data.Actuals.Where(x=>x.GroupId==l.GroupId&&x.Held&&(string.CompareOrdinal(x.Date,l.Date)<0||(x.Date==l.Date&&x.Period<l.Period))&&!string.IsNullOrWhiteSpace(x.Note)).OrderByDescending(x=>x.Date).ThenByDescending(x=>x.Period).FirstOrDefault();
        if(previous!=null)p.Children.Add(Text("前回 "+previous.Date+"："+previous.Note,12,false,Muted));
        var note=new TextBox{Text=l.Note,AcceptsReturn=true,Height=70,TextWrapping=TextWrapping.Wrap};p.Children.Add(note);
        var a=data.Actuals.Find(x=>x.Date==l.Date&&x.Period==l.Period);
        if(a!=null) Add(p,Btn("メモを保存",()=>{var d=data.Clone();d.Actuals.Single(x=>x.Date==l.Date&&x.Period==l.Period).Note=note.Text;if(Commit(d))w.Close();}));
        else if(l.State=="estimated")
        {
            void Confirm(bool held){var d=data.Clone();try{LessonEngine.Confirm(d,l,held,note.Text);if(Commit(d))w.Close();}catch(Exception e){Error(e);}}
            Add(p,Btn("実施済みとして確定",()=>Confirm(true)),Btn("実施しなかったとして確定",()=>Confirm(false)));
        }
        else p.Children.Add(Text("終了時刻を過ぎた授業を確定できます。",12,false,Muted));
        w.Content=new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};w.ShowDialog();
    }
    private void Counts()
    {
        var w=Dialog("授業数集計",840,450);var p=new DockPanel{Margin=new Thickness(14)};
        var bar=Horizontal();DockPanel.SetDock(bar,Dock.Top);
        var start=new DatePicker{SelectedDate=SchoolData.ParseDate(data.YearFrom)};var end=new DatePicker{SelectedDate=SchoolData.ParseDate(data.YearTo)};
        var summary=Text("",12,false,Muted);DockPanel.SetDock(summary,Dock.Bottom);
        var grid=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,RowHeaderWidth=0};
        foreach(var (label,key) in new[]{("担当授業","Name"),("確定","Confirmed"),("推定","Estimated"),("残予定","Remaining"),("見込み","Forecast"),("休講","Cancelled"),("要確認","Unresolved")})grid.Columns.Add(Col(label,key));
        void Refresh(){try{if(start.SelectedDate==null||end.SelectedDate==null)return;var a=start.SelectedDate.Value;var b=end.SelectedDate.Value;var now=DateTime.Now;var counts=LessonEngine.Count(data,a,b,now);grid.ItemsSource=counts.Select(c=>new{Name=data.GroupName(c.GroupId),c.Confirmed,c.Estimated,c.Remaining,c.Forecast,c.Cancelled,c.Unresolved}).ToList();int missing=0;for(var d=a;d<=b;d=d.AddDays(1))if(!LessonEngine.Day(data,d,now).Configured)missing++;summary.Text=$"考査 {(data.CountExams?"含む":"除く")} / 未設定日 {missing}日 / 推定は未確定";}catch(Exception e){Error(e);}}
        Add(bar,Text("期間 "),start,Text(" ～ "),end,Btn("集計",Refresh));Add(p,bar,summary,grid);w.Content=p;Refresh();w.ShowDialog();
    }
    private void ImportExcel()
    {
        if(demo){MessageBox.Show(this,"通常起動時に利用できます。デモには取り込みません。");return;}
        if(!LegacyImport.IsEmpty(data)){MessageBox.Show(this,"初期移行専用です。既存データがあるため上書きしません。別の初期データへの移行は後続版で対応します。");return;}
        var dlg=new OpenFileDialog{Filter="Excel (*.xlsx)|*.xlsx",CheckFileExists=true,Multiselect=false};
        if(dlg.ShowDialog(this)!=true)return;
        var w=Dialog("Excel初期取り込み",590,500);var p=new StackPanel{Margin=new Thickness(14)};
        var grade=new TextBox{Text="未分類"};var exam=new CheckBox{Content="考査を授業数に含める",IsChecked=true,Margin=new Thickness(0,6,0,6)};
        var preview=Text("まだ読み込んでいません。",13,false,Muted);ImportPlan? plan=null;
        var apply=Btn("確認して取り込む",()=>{if(plan==null||!LegacyImport.IsEmpty(data))return; if(MessageBox.Show(w,"表示した予定・時間割をローカルに登録します。元Excelは変更せず、名列は読み込みません。よろしいですか？","確認",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;plan.Data.Display=data.Display;if(Commit(plan.Data)){day=SchoolData.ParseDate(plan.Data.Timetables[0].From);Render();w.Close();}});apply.IsEnabled=false;
        void Invalidate(){plan=null;apply.IsEnabled=false;preview.Text="設定が変わりました。再プレビューしてください。";}
        grade.TextChanged+=(_,_)=>Invalidate();exam.Checked+=(_,_)=>Invalidate();exam.Unchecked+=(_,_)=>Invalidate();
        Add(p,Text("授業数管理Excelの既定形式（3シートのみ）",16,true),Text("仮の学年名"),grade,exam,
            Btn("読み込み・プレビュー",()=>{try{plan=LegacyImport.Prepare(dlg.FileName,grade.Text,exam.IsChecked==true);var t=plan.Data.Timetables[0];var counts=LessonEngine.Count(plan.Data,SchoolData.ParseDate(t.From),SchoolData.ParseDate(t.To),SchoolData.ParseDate(t.To).AddDays(1));preview.Text=$"対象期間：{t.From} ～ {t.To}\n日別表 {plan.DailyRows}日 / 年間予定 {plan.EventRows}件 / 担当授業 {plan.GroupCount}件 / 考査 {plan.ExamDays}日\n保存値の授業数 {plan.TotalCached} / 再計算 {plan.TotalRecalculated}（一致）\n"+string.Join("\n",counts.Select(c=>plan.Data.GroupName(c.GroupId)+"  "+c.Forecast+"回"))+"\n\n"+string.Join("\n",plan.Warnings);apply.IsEnabled=true;}catch(Exception e){plan=null;apply.IsEnabled=false;Error(e);}}),preview,apply,Btn("取消",w.Close));
        w.Content=new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};w.ShowDialog();
    }
    private void Backup()
    {
        if(demo){MessageBox.Show(this,"デモモードでは保存しません。");return;}
        var dlg=new SaveFileDialog{Filter="KOMU DESK JSON (*.json)|*.json",FileName="komu-backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json",OverwritePrompt=true};
        if(dlg.ShowDialog(this)!=true)return;
        try{store.Backup(data,dlg.FileName);MessageBox.Show(this,"バックアップを作成しました。");}catch(Exception e){Error(e);}
    }
}
