using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KomuDesk.Core;
using Microsoft.Win32;

namespace KomuDesk.Desktop;
internal sealed partial class DeskWindow
{
    private void RegisterGroups()
    {
        var w=Dialog("担当授業を一括登録",510,460);var p=new StackPanel();
        var grade=new ComboBox{IsEditable=true,ItemsSource=data.Grades.Select(g=>g.Name).Distinct().ToArray()};
        var subject=new ComboBox{IsEditable=true,ItemsSource=data.Subjects.Select(s=>s.Name).Distinct().ToArray()};
        var classes=new TextBox{AcceptsReturn=true,MinHeight=75,TextWrapping=TextWrapping.Wrap};
        var choices=new WrapPanel();var preview=Text("",12,false,Muted);
        void ShowClasses(){choices.Children.Clear();foreach(var c in data.Classes.Where(c=>data.Grades.Any(g=>g.Id==c.GradeId&&g.Name==grade.Text)))choices.Children.Add(WidgetButton(c.Name,c.Name,()=>{classes.Text+=(classes.Text.Trim()==""?"":"、")+c.Name;}));}
        grade.SelectionChanged+=(_,_)=>Dispatcher.BeginInvoke(new Action(ShowClasses));grade.LostKeyboardFocus+=(_,_)=>ShowClasses();
        Field(p,"学年（既存または新規）",grade);Field(p,"科目（既存または新規）",subject);Field(p,"クラス名",classes,"読点・カンマ・改行で区切ります。特別講座は任意のクラス名で登録できます。");Add(p,choices,preview);
        var actions=new WrapPanel();Add(actions,Primary("save","一括登録",()=>{try{var draft=data.Clone();int n=SchoolSetup.Register(draft,grade.Text,subject.Text,classes.Text.Split(new[]{'、',',','，','\r','\n','\t'},StringSplitOptions.RemoveEmptyEntries));if(Commit(draft)){preview.Text=n+"授業を追加しました。既存授業は維持しました。";w.Close();}}catch(Exception ex){Error(ex);}}),Tool("close","取消",w.Close));
        w.Content=FormLayout("担当授業を一括登録","同じ学年・クラス・科目は重複登録しません。",p,actions);w.ShowDialog();
    }
    private void PaintWeekly()
    {
        if(data.Timetables.Count==0){Versions();if(data.Timetables.Count==0)return;}
        if(data.Groups.Count==0){RegisterGroups();if(data.Groups.Count==0)return;}
        var w=Dialog("週間時間割・かんたん入力",1040,690);var root=new DockPanel{Margin=new Thickness(12)};
        var top=new StackPanel();var select=new ComboBox{ItemsSource=data.Timetables.Select(t=>new Choice(t.Id,t.Name+" "+t.From+" ～ "+t.To)).ToArray(),DisplayMemberPath="Label",SelectedValuePath="Id"};
        var palette=new WrapPanel();var hint=Text("",12,false,Muted);var grid=new Grid();var slots=new Dictionary<(int Day,int Period),string>();string? version=null;string? brush=null;bool restoring=false;
        var periods=data.Bells.OrderBy(b=>b.Number).Select(b=>b.Number).ToArray();
        bool Dirty()=>version!=null&&!slots.OrderBy(x=>x.Key).SequenceEqual(data.Timetables.Single(t=>t.Id==version).Slots.ToDictionary(x=>(x.Day,x.Period),x=>x.GroupId).OrderBy(x=>x.Key));
        string Label(string id)=>data.GroupName(id);
        Brush Color(string id){var group=data.Groups.Find(g=>g.Id==id);return group==null?White:BrushOf(data.Classes.Single(c=>c.Id==group.ClassId).Color);}
        void Palette()
        {
            palette.Children.Clear();
            foreach(var group in data.Groups.Select(g=>g.Id).Append(""))
            {
                string id=group;var b=WidgetButton(id==""?"消去":Label(id),id==""?"消去":Label(id),()=>{brush=id;Palette();});
                b.Background=Color(id);b.Foreground=Ink;b.BorderBrush=brush==id?Ink:Line;b.BorderThickness=new Thickness(brush==id?2:1);palette.Children.Add(b);
            }
        }
        void Draw()
        {
            grid.Children.Clear();grid.ColumnDefinitions.Clear();grid.RowDefinitions.Clear();
            grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(64)});for(int i=0;i<7;i++)grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(118)});
            for(int i=0;i<=periods.Length;i++)grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            void Put(UIElement e,int row,int col){Grid.SetRow(e,row);Grid.SetColumn(e,col);grid.Children.Add(e);}
            int c=1;foreach(string name in new[]{"月","火","水","木","金","土","日"})Put(Text(name,14,true),0,c++);
            for(int i=0;i<periods.Length;i++)
            {
                int n=periods[i];Put(Text(n+"限",13,true),i+1,0);
                for(int j=0;j<7;j++)
                {
                    var key=((j+1)%7,n);string id=slots.GetValueOrDefault(key,"");
                    var b=WidgetButton(id==""?"＋":Label(id),"時限 "+n+" "+j,()=>{if(brush==null)return;if(brush=="")slots.Remove(key);else slots[key]=brush;Draw();});
                    b.MinHeight=58;b.Padding=new Thickness(3);b.Margin=new Thickness(2);b.Background=Color(id);b.Foreground=Ink;Put(b,i+1,j+1);
                }
            }
            hint.Text="週 "+slots.Count+"コマ　"+string.Join(" / ",slots.Values.GroupBy(x=>x).Select(g=>Label(g.Key)+" "+g.Count()));
        }
        void Load()
        {
            version=select.SelectedValue as string;if(version==null)return;
            slots=data.Timetables.Single(t=>t.Id==version).Slots.ToDictionary(x=>(x.Day,x.Period),x=>x.GroupId);Draw();
        }
        select.SelectionChanged+=(_,_)=>{if(restoring)return;if(Dirty()&&MessageBox.Show(w,"未保存の変更を破棄しますか？","確認",MessageBoxButton.YesNo)!=MessageBoxResult.Yes){restoring=true;select.SelectedValue=version;restoring=false;return;}Load();};
        var actions=new WrapPanel();var done=ProtectDraft(w,Dirty);
        Add(actions,Primary("save","保存",()=>{if(version==null)return;var draft=data.Clone();draft.Timetables.Single(t=>t.Id==version).Slots=slots.Select(k=>new Slot{Day=k.Key.Day,Period=k.Key.Period,GroupId=k.Value}).ToList();if(Commit(draft))done();}),
            Tool("spreadsheet","Excelのマスを貼り付け",()=>{try{var parsed=SchoolSetup.PasteSlots(data,System.Windows.Clipboard.GetText(),periods);if(MessageBox.Show(w,"貼り付け内容で選択中の週間配置を置換します。保存前に確認できます。","確認",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;slots=parsed.ToDictionary(x=>(x.Day,x.Period),x=>x.GroupId);Draw();}catch(Exception ex){Error(ex);}}),
            Tool("close","取消",w.Close));
        Add(top,Text("授業を選んでマスをクリック",20,true),select,palette,hint);DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);
        root.Children.Add(new ScrollViewer{Content=grid,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        select.SelectedValue=data.Timetables.FirstOrDefault(t=>SchoolData.ParseDate(t.From)<=day&&SchoolData.ParseDate(t.To)>=day)?.Id??data.Timetables[0].Id;brush=data.Groups[0].Id;Palette();w.Content=root;w.ShowDialog();
    }
    private void ImportMore()
    {
        if(demo)return;
        var dialog=new OpenFileDialog{Filter="Excel (*.xlsx)|*.xlsx",CheckFileExists=true};
        if(dialog.ShowDialog(management??this)!=true)return;ImportEventsFile(dialog.FileName);
    }
    private void PasteEvents()
    {
        if(demo)return;
        var w=Dialog("予定表の貼り付け",760,610);var root=new DockPanel{Margin=new Thickness(12)};
        var top=new StackPanel();Add(top,Text("Excelから予定を貼り付け",20,true),Text("日付・予定名・任意の時刻の順。見出しは除いてコピーしてください。",12,false,Muted));var input=new TextBox{Text=System.Windows.Clipboard.GetText(),AcceptsReturn=true,AcceptsTab=true,Height=100,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};top.Children.Add(input);
        var grid=Table();grid.IsReadOnly=true;foreach(var (label,key) in new[]{("行","Row"),("日付","Date"),("予定名","Title"),("時刻","Time"),("判定","Status")})grid.Columns.Add(Col(label,key));
        IReadOnlyList<EventImportRow>? rows=null;var status=Text("",12,false,Muted);Button? apply=null;
        void Preview(){try{rows=EventImport.Paste(data,input.Text);grid.ItemsSource=rows;status.Text="追加 "+rows.Count(r=>r.Status=="追加")+" / 既存 "+rows.Count(r=>r.Status=="既存")+" / 要確認 "+rows.Count(r=>r.Status.StartsWith("要確認"));apply!.IsEnabled=rows.Any(r=>r.Status=="追加");}catch(Exception ex){Error(ex);}}
        input.TextChanged+=(_,_)=>{rows=null;grid.ItemsSource=null;apply!.IsEnabled=false;status.Text="再プレビューしてください。";};Add(top,Tool("spreadsheet","プレビュー",Preview),status);DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
        var bar=new WrapPanel();apply=Primary("save","追加のみ取り込む",()=>{if(rows==null)return;var draft=data.Clone();int n=EventImport.Add(draft,rows);int automatic=ApplyAutomatic(draft);if(MessageBox.Show(w,$"{n}件を追加・自動ルール {automatic}日。よろしいですか？","確認",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;if(Commit(draft)){w.Close();ScheduleReview();}});apply.IsEnabled=false;Add(bar,apply,Tool("close","取消",w.Close));DockPanel.SetDock(bar,Dock.Bottom);root.Children.Add(bar);root.Children.Add(grid);w.Content=root;w.ShowDialog();
    }
    private void ImportEventsFile(string path)
    {
        try
        {
            string[] names;using(var book=new LegacyWorkbook(path))names=book.SheetNames.ToArray();
            if(names.Length==0)throw new InvalidDataException("シートがありません。");
            var w=Dialog("Excel予定表を追加取り込み",850,650);var root=new DockPanel{Margin=new Thickness(12)};
            var top=new StackPanel();var sheet=new ComboBox{ItemsSource=names,SelectedItem=names.Contains(LegacyImport.EventsSheet)?LegacyImport.EventsSheet:names[0]};
            var date=new TextBox{Text="B",Width=48};var title=new TextBox{Text="C",Width=48};var time=new TextBox{Width=48};var start=new TextBox{Text="1",Width=56};
            var columns=new WrapPanel();Add(columns,Text("日付列 "),date,Text(" 名称列 "),title,Text(" 時刻列 "),time,Text(" 開始行 "),start);
            var status=Text("列を確認してプレビューしてください。時刻列は任意です。",12,false,Muted);var grid=Table();grid.IsReadOnly=true;grid.SelectionMode=DataGridSelectionMode.Extended;
            foreach(var (label,key) in new[]{("元行","Row"),("日付","Date"),("予定","Title"),("時刻","Time"),("判定","Status")})grid.Columns.Add(Col(label,key));
            IReadOnlyList<EventImportRow>? rows=null;Button? apply=null;
            void Invalidate(){rows=null;grid.ItemsSource=null;if(apply!=null)apply.IsEnabled=false;status.Text="再プレビューしてください。";}
            void Preview(){try{rows=EventImport.Prepare(data,path,(string)sheet.SelectedItem,date.Text.Trim().ToUpperInvariant(),title.Text.Trim().ToUpperInvariant(),time.Text.Trim().ToUpperInvariant(),int.Parse(start.Text));grid.ItemsSource=rows;status.Text=$"追加 {rows.Count(r=>r.Status=="追加")} / 既存 {rows.Count(r=>r.Status=="既存")} / 要確認 {rows.Count(r=>r.Status.StartsWith("要確認"))}。既存予定を更新・削除しません。";apply!.IsEnabled=rows.Any(r=>r.Status=="追加");}catch(Exception ex){Invalidate();Error(ex);}}
            sheet.SelectionChanged+=(_,_)=>Invalidate();foreach(var input in new[]{date,title,time,start})input.TextChanged+=(_,_)=>Invalidate();
            Add(top,Text("予定表取り込み",22,true),Text("読み取るシート・列・開始行を選択。名列は選択しないでください。",12,false,Muted),sheet,columns,Tool("spreadsheet","プレビュー",Preview),status);
            DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
            var footer=new WrapPanel();
            apply=Primary("save","追加のみ取り込む",()=>{if(rows==null)return;var draft=data.Clone();int n=EventImport.Add(draft,rows);int automatic=ApplyAutomatic(draft);if(MessageBox.Show(w,$"{n}件を追加します。登録済み自動ルールの適用 {automatic}日。既存予定や確定授業は変更しません。","確認",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;if(Commit(draft)){w.Close();ScheduleReview();}});apply.IsEnabled=false;Add(footer,apply,Tool("close","取消",w.Close));DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);root.Children.Add(grid);w.Content=root;w.ShowDialog();
        }
        catch(Exception ex){Error(ex);}
    }
    private void SpecialProfiles()
    {
        var w=Dialog("特別時程",680,610);var root=new DockPanel{Margin=new Thickness(12)};var draft=data.Clone();
        var selector=new ComboBox{DisplayMemberPath="Name",ItemsSource=draft.BellProfiles};var name=new TextBox();var grid=Table();grid.SelectionMode=DataGridSelectionMode.Extended;grid.Columns.Add(Col("時限","Number"));grid.Columns.Add(Col("開始","Start"));grid.Columns.Add(Col("終了","End"));
        BellProfile? item=null;var rows=new System.Collections.ObjectModel.ObservableCollection<Bell>();grid.ItemsSource=rows;
        void Load(BellProfile p){item=p;name.Text=p.Name;rows.Clear();foreach(var b in p.Bells)rows.Add(new Bell{Number=b.Number,Start=b.Start,End=b.End});}
        selector.SelectionChanged+=(_,_)=>{if(selector.SelectedItem is BellProfile p)Load(p);};
        void New(){selector.SelectedIndex=-1;Load(new BellProfile{Name="",Bells=data.Bells.Select(b=>new Bell{Number=b.Number,Start=b.Start,End=b.End}).ToList()});}
        var top=new StackPanel();Add(top,Text("特別時程テンプレート",20,true),Text("通常時程を複製し、開始・終了時刻を変更。不要な時限は削除します。",12,false,Muted),selector,Tool("add","通常時程から新規",New));Field(top,"時程名（例：短縮4限）",name);DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
        var actions=new WrapPanel();Add(actions,Tool("trash","選択時限を除く",()=>{foreach(var b in grid.SelectedItems.Cast<Bell>().ToArray())rows.Remove(b);}),Primary("save","この時程を保存",()=>{if(item==null)return;if(!grid.CommitEdit(DataGridEditingUnit.Cell,true)||!grid.CommitEdit(DataGridEditingUnit.Row,true))return;var d=data.Clone();if(d.Days.Any(x=>x.ProfileId==item.Id)){MessageBox.Show(w,"使用中の時程は変更できません。新規時程として登録してください。");return;}d.BellProfiles.RemoveAll(p=>p.Id==item.Id);d.BellProfiles.Add(new BellProfile{Id=item.Id,Name=name.Text.Trim(),Bells=rows.ToList()});if(Commit(d))w.Close();}),Tool("close","取消",w.Close));
        DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);root.Children.Add(grid);New();w.Content=root;w.ShowDialog();
    }
    private void RuleSettings()
    {
        var profiles=new[]{new Choice("","通常時程")}.Concat(data.BellProfiles.Select(p=>new Choice(p.Id,p.Name))).ToArray();
        Edit<ScheduleRule>("予定連動ルール","予定名は完全一致（全角半角は正規化）。sourceは local / google。曜日0=日、1=月〜6=土。時程・上限・全休から指定。自動は初期OFF。既存変更・過去・実績・競合は保護します。",d=>d.ScheduleRules,(d,l)=>d.ScheduleRules=l,Col("予定名","Title"),ComboCol("出所","Source",new[]{new Choice("local","ローカル"),new Choice("google","Google")},"Id","Label"),Col("曜日","Weekday"),Col("上限時限","MaxPeriod"),ComboCol("時程","ProfileId",profiles,"Id","Label"),CheckCol("全休","Closed"),CheckCol("自動","Automatic"));
    }
    private IEnumerable<ScheduleInput> ScheduleInputs(SchoolData d)
    {
        foreach(var e in d.Events)yield return new(SchoolData.ParseDate(e.Date),e.Title,"local");
        var state=googleState;var snapshot=state?.Snapshot;if(snapshot==null||state==null)yield break;
        var from=SchoolData.ParseDate(d.YearFrom);var to=SchoolData.ParseDate(d.YearTo);
        foreach(var date in Enumerable.Range(0,(to-from).Days+1).Select(n=>from.AddDays(n)))
            if(string.CompareOrdinal(SchoolData.Iso(date),snapshot.From)>=0&&string.CompareOrdinal(SchoolData.Iso(date),snapshot.To)<=0)
                foreach(var e in snapshot.Events.Where(e=>state.Session.SelectedIds.Contains(e.CalendarId)&&e.OnDate(date)))yield return new(date,e.Title,"google");
    }
    private int ApplyAutomatic(SchoolData draft)
    {
        return SchoolSetup.Suggest(draft,ScheduleInputs(draft),DateTime.Today).Where(s=>s.Automatic&&s.Date>DateTime.Today).Count(s=>SchoolSetup.Apply(draft,s,DateTime.Today));
    }
    private void ScheduleReview()
    {
        var w=Dialog("予定から時間割変更",800,620);var root=new DockPanel{Margin=new Thickness(12)};
        var top=new StackPanel();Add(top,Text("時間割変更候補",22,true),Text("既存変更・確定授業・過去・競合は自動処理しません。短縮時程は登録後に候補になります。",12,false,Muted));DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
        var list=new StackPanel();var choices=new List<(ScheduleSuggestion Suggestion,CheckBox Check)>();
        foreach(var s in SchoolSetup.Suggest(data,ScheduleInputs(data),DateTime.Today))
        {
            var p=new StackPanel();var check=new CheckBox{Content="適用する",IsEnabled=s.Block=="",IsChecked=false};
            Add(p,Text(DayText(s.Date)+" · "+s.Title,14,true),Text(s.Summary(data),13),Text(s.Block,12,false,Muted),check);list.Children.Add(CardBox(p));choices.Add((s,check));
        }
        if(choices.Count==0)list.Children.Add(Text("変更候補はありません。",13,false,Muted));
        var footer=new WrapPanel();Add(footer,Primary("check","選択した変更を適用",()=>{var draft=data.Clone();int n=choices.Where(x=>x.Check.IsChecked==true).Count(x=>SchoolSetup.Apply(draft,x.Suggestion,DateTime.Today));if(n==0)return;if(Commit(draft))w.Close();}),Tool("close","閉じる",w.Close));
        DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);root.Children.Add(PageScroll(list));w.Content=root;w.ShowDialog();
    }
    private string DayScheduleLabel(DateTime d)
    {
        var change=data.Days.Find(x=>x.Date==SchoolData.Iso(d));if(change==null)return "";
        return string.Join(" / ",new[]{change.Closed?"全休":"",change.Weekday.HasValue?new[]{"日","月","火","水","木","金","土"}[change.Weekday.Value]+"曜時間割":"",data.BellProfiles.Find(p=>p.Id==change.ProfileId)?.Name??"",change.MaxPeriod.HasValue?change.MaxPeriod+"限まで":""}.Where(x=>x!=""));
    }
}
