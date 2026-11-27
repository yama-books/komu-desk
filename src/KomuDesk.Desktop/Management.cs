using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using KomuDesk.Core;

namespace KomuDesk.Desktop;
internal sealed partial class DeskWindow
{
    private Window? management;
    private string managementPage="home";
    private DateTime agendaDate=DateTime.Today;
    private string agendaMode="月";
    private DateTime agendaFrom=DateTime.Today,agendaTo=DateTime.Today.AddDays(30);
    private bool[] agendaSources={true,true,true,true};
    private bool showCompleted;
    private Action? refreshManagement;
    private static readonly string[] SourceIds={"local","google","lesson","task"};
    private static readonly string[] SourceNames={"ローカル予定","Google予定","授業","タスク"};
    private static string DayText(DateTime d)=>d.ToString("M月d日（ddd）",System.Globalization.CultureInfo.GetCultureInfo("ja-JP"));
    private static Border CardBox(UIElement child)=>new(){Background=White,BorderBrush=Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(12),Margin=new Thickness(3),Child=child};
    private static ScrollViewer PageScroll(UIElement child)=>new(){Content=child,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
    private void OpenManagement(string page="home",DateTime? date=null)
    {
        if(date.HasValue){agendaDate=date.Value.Date;agendaFrom=agendaDate;agendaTo=agendaDate.AddDays(30);}
        managementPage=page;
        if(management!=null){refreshManagement?.Invoke();if(management.WindowState==WindowState.Minimized)management.WindowState=WindowState.Normal;management.Activate();return;}
        var w=Dialog("KOMU DESK · 管理",1040,720);
        w.MinWidth=Math.Min(720,SystemParameters.WorkArea.Width);w.MinHeight=Math.Min(480,SystemParameters.WorkArea.Height);
        management=w;w.Closed+=(_,_)=>{management=null;refreshManagement=null;};
        w.Content=BuildManagementContent(page);
        w.Show();
    }
    private void OpenAgenda()=>OpenManagement("calendar",day);
    internal FrameworkElement BuildManagementContent(string initial)
    {
        managementPage=initial;
        var root=new Grid();
        var navCol=new ColumnDefinition{Width=new GridLength(176)};
        root.ColumnDefinitions.Add(navCol);root.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        var nav=new StackPanel{Margin=new Thickness(7)};
        var panel=new Border{Background=Soft,Child=nav};Grid.SetColumn(panel,0);root.Children.Add(panel);
        var main=new ContentControl{Margin=new Thickness(12,8,12,12),HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch};
        Grid.SetColumn(main,1);root.Children.Add(main);
        bool compact=false,manualCompact=false;
        var items=new (string Id,string Icon,string Label)[]{("home","home","ホーム"),("calendar","calendar","カレンダー"),("timetable","timetable","時間割"),("counts","lesson-count","授業数"),("tasks","tasks","タスク"),("reports","printer","帳票"),("google","connection","Google連携"),("settings","settings","設定"),("updates","update","アップデート")};
        void Select(string id){managementPage=id;Refresh();}
        void Nav()
        {
            nav.Children.Clear();navCol.Width=new GridLength(compact?52:176);
            var toggle=Tool("menu",compact?"メニュー展開":"メニュー折りたたみ",()=>{manualCompact=!compact;compact=manualCompact||root.ActualWidth<760;Nav();},false);nav.Children.Add(toggle);
            foreach(var item in items)
            {
                if(item.Id=="google")nav.Children.Add(Hairline());
                var b=Tool(item.Icon,item.Label,()=>Select(item.Id),!compact);
                b.HorizontalContentAlignment=compact?HorizontalAlignment.Center:HorizontalAlignment.Left;
                if(item.Id==managementPage){b.Background=White;b.BorderBrush=Ink;}
                nav.Children.Add(b);
            }
        }
        void Modal(Action action){action();Refresh();}
        void Refresh()
        {
            Nav();
            main.Content=managementPage switch{
                "calendar"=>BuildAgenda(()=>Refresh(),Modal),
                "timetable"=>BuildTimetablePage(Modal),
                "counts"=>BuildCountsPage(Modal),
                "tasks"=>BuildTasksPage(Modal),
                "reports"=>SimplePage("帳票","授業日一覧を期間・担当授業で指定し、Excel出力または印刷します。",("printer","帳票を開く",()=>Modal(Reports))),
                "google"=>SimplePage("Google連携",GoogleSummary(agendaDate),("connection","認証・対象カレンダー・同期",()=>Modal(GoogleSettings))),
                "settings"=>BuildSettingsPage(Modal),
                "updates"=>SimplePage("アップデート","現在 "+UpdatePackage.Tag(UpdateInstall.Current)+" · 更新確認・容量管理",("update","更新と旧版保持設定",()=>Modal(Updates))),
                _=>BuildHome(Select,Modal)
            };
        }
        root.SizeChanged+=(_,_)=>{bool next=manualCompact||root.ActualWidth<760;if(next!=compact){compact=next;Nav();}};
        refreshManagement=Refresh;
        Refresh();return root;
    }
    private static FrameworkElement SimplePage(string title,string detail,params (string Icon,string Label,Action Action)[] entries)
    {
        var p=new StackPanel();Add(p,Text(title,24,true),Text(detail,13,false,Muted));
        foreach(var e in entries)p.Children.Add(Tool(e.Icon,e.Label,e.Action));
        return PageScroll(p);
    }
    private string GoogleSummary(DateTime date)
    {
        var s=googleState?.Snapshot;
        if(googleState==null)return demo?"Google：架空デモ・外部通信なし":"Google：未連携 / "+googleStatus;
        if(s==null)return "Google：未取得 / "+googleStatus;
        return "Google：最終取得 "+s.LastSuccessUtc.ToLocalTime().ToString("M/d HH:mm")+" / "+(string.CompareOrdinal(SchoolData.Iso(date),s.From)<0||string.CompareOrdinal(SchoolData.Iso(date),s.To)>0?"同期範囲外":googleStatus);
    }
    private IReadOnlyList<AgendaRow> Rows(DateTime d)
    {
        var rows=Agenda.Local(data,d,DateTime.Now,showCompleted).ToList();
        var s=googleState?.Snapshot;
        if(s!=null&&googleState!=null&&string.CompareOrdinal(SchoolData.Iso(d),s.From)>=0&&string.CompareOrdinal(SchoolData.Iso(d),s.To)<=0)
            rows.AddRange(s.Events.Where(e=>googleState.Session.SelectedIds.Contains(e.CalendarId)&&e.OnDate(d)).Select(e=>new AgendaRow(d.Date,"google",e.CalendarId+":"+e.Id+":"+e.Start,e.Title,e.DisplayTime(d),e.AllDay?0:e.DisplayTime(d)=="継続"?1:2)));
        return Agenda.Sort(rows.Where(r=>agendaSources[Array.IndexOf(SourceIds,r.Source)]));
    }
    private static string StateText(AgendaRow r)=>r.Source=="lesson"?(r.Lesson?.Kind=="exam"?"考査 · ":"")+(r.State switch{"confirmed"=>"確定","estimated"=>"推定","cancelled"=>"休講","unresolved"=>"要確認",_=>"予定"}):r.Source=="task"?(r.State=="done"?"完了":"未完了"):r.Source=="google"?"Google · 読取専用":"ローカル";
    private static string SourceIcon(string source)=>source switch{"lesson"=>"timetable","task"=>"tasks","google"=>"connection",_=>"calendar"};
    private FrameworkElement AgendaLine(AgendaRow r,Action<Action> modal)
    {
        var line=new DockPanel{Margin=new Thickness(0,2,0,2),MinHeight=29};
        var time=Text(r.Time,11,false,Muted);time.Width=72;DockPanel.SetDock(time,Dock.Left);line.Children.Add(time);
        var state=Text(StateText(r),11,false,Muted);state.Margin=new Thickness(8,0,0,0);DockPanel.SetDock(state,Dock.Right);line.Children.Add(state);
        var label=new WrapPanel{VerticalAlignment=VerticalAlignment.Center};
        var icon=Glyph(SourceIcon(r.Source),15);icon.Margin=new Thickness(0,0,6,0);label.Children.Add(icon);
        if(r.Lesson!=null)AddGroupLabels(label,r.Lesson.GroupId);else label.Children.Add(Text(r.Title,13));
        var b=WidgetButton(label,r.Title+" "+StateText(r),()=>modal(()=>OpenAgendaRow(r)),false);b.HorizontalContentAlignment=HorizontalAlignment.Left;
        line.Children.Add(b);return line;
    }
    private void OpenAgendaRow(AgendaRow r)
    {
        if(r.Lesson!=null){LessonDetail(r.Lesson);return;}
        if(r.Source=="local"){EditAgendaEvent(r.Day,r.Key);return;}
        if(r.Source=="task"){EditAgendaTask(r.Key,r.Day);return;}
        var w=Dialog("Google予定 · 読み取り専用",450,280);var p=new StackPanel{Margin=new Thickness(16)};
        Add(p,Text(r.Title,20,true),Text(DayText(r.Day)+" "+r.Time),Text("Googleカレンダーから取得した予定です。編集・削除はGoogle側で行ってください。",12,false,Muted),Tool("close","閉じる",w.Close));w.Content=p;w.ShowDialog();
    }
    private void EditAgendaEvent(DateTime d,string? id=null)
    {
        var draft=data.Clone();var item=id==null?new LocalEvent{Date=SchoolData.Iso(d)}:draft.Events.Single(x=>x.Id==id);
        var w=Dialog(id==null?"予定を追加":"予定を編集",450,330);var p=new StackPanel{Margin=new Thickness(16)};
        var title=new TextBox{Text=item.Title,MinHeight=32};var date=new DatePicker{SelectedDate=SchoolData.ParseDate(item.Date)};var time=new TextBox{Text=item.Time,MinHeight=32};
        var bar=new WrapPanel();Add(bar,Tool("save","保存",()=>{if(date.SelectedDate==null)return;item.Title=title.Text.Trim();item.Date=SchoolData.Iso(date.SelectedDate.Value);item.Time=time.Text.Trim();if(id==null&&!draft.Events.Contains(item))draft.Events.Add(item);if(Commit(draft))w.Close();}),Tool("close","取消",w.Close));
        if(id!=null)bar.Children.Add(Tool("trash","削除",()=>{if(MessageBox.Show(w,"このローカル予定を削除しますか？","確認",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;draft.Events.Remove(item);if(Commit(draft))w.Close();}));
        Add(p,Text("予定名"),title,Text("日付"),date,Text("開始時刻（HH:mm、空欄は終日）"),time,bar);w.Content=p;w.ShowDialog();
    }
    private void EditAgendaTask(string? id,DateTime? due=null)
    {
        var draft=data.Clone();var item=id==null?new Todo{Due=due.HasValue?SchoolData.Iso(due.Value):""}:draft.Tasks.Single(t=>t.Id==id);
        var w=Dialog(id==null?"タスク追加":"タスク編集",450,300);var p=new StackPanel{Margin=new Thickness(16)};
        var title=new TextBox{Text=item.Title,MinHeight=32};var date=new DatePicker{SelectedDate=item.Due==""?null:SchoolData.ParseDate(item.Due)};var done=new CheckBox{Content="完了",IsChecked=item.Done,Margin=new Thickness(0,8,0,8)};
        var bar=new WrapPanel();Add(bar,Tool("save","保存",()=>{item.Title=title.Text.Trim();item.Due=date.SelectedDate.HasValue?SchoolData.Iso(date.SelectedDate.Value):"";item.Done=done.IsChecked==true;if(id==null&&!draft.Tasks.Contains(item))draft.Tasks.Add(item);if(Commit(draft))w.Close();}),Tool("close","取消",w.Close));
        if(id!=null)bar.Children.Add(Tool("trash","削除",()=>{if(MessageBox.Show(w,"このタスクを削除しますか？","確認",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;draft.Tasks.Remove(item);if(Commit(draft))w.Close();}));
        Add(p,Text("タスク名"),title,Text("期限（空欄可）"),date,done,bar);w.Content=p;w.ShowDialog();
    }
    private FrameworkElement DayDetail(DateTime d,Action<Action> modal)
    {
        var p=new StackPanel();Add(p,Text(DayText(d),18,true),Text(GoogleSummary(d),11,false,Muted));
        if(!LessonEngine.Day(data,d,DateTime.Now).Configured)p.Children.Add(Text("この日の時間割は未設定です。",12,false,Muted));
        var rows=Rows(d);
        if(rows.Count==0)p.Children.Add(Text("表示する予定はありません。",13,false,Muted));
        foreach(var r in rows)p.Children.Add(AgendaLine(r,modal));
        var actions=new WrapPanel();Add(actions,Tool("add","予定追加",()=>modal(()=>EditAgendaEvent(d))),Tool("edit","日別変更",()=>modal(DayChanges)),Tool("add","タスク",()=>modal(()=>EditAgendaTask(null,d))));p.Children.Add(actions);
        return CardBox(p);
    }
    private FrameworkElement BuildAgenda(Action refresh,Action<Action> modal)
    {
        var outer=new DockPanel();var header=new StackPanel();
        Add(header,Text("カレンダー",24,true));
        var toolbar=new WrapPanel();
        void Shift(int n){agendaDate=agendaMode=="月"?agendaDate.AddMonths(n):agendaDate.AddDays(n*(agendaMode=="週"?7:1));if(agendaMode=="一覧"){int length=(agendaTo-agendaFrom).Days+1;agendaFrom=agendaFrom.AddDays(n*length);agendaTo=agendaTo.AddDays(n*length);agendaDate=agendaFrom;}refresh();}
        Add(toolbar,Tool("chevron-left","前へ",()=>Shift(-1),false),Tool("calendar-today","今日",()=>{agendaDate=DateTime.Today;agendaFrom=agendaDate;agendaTo=agendaDate.AddDays(30);refresh();}),Tool("chevron-right","次へ",()=>Shift(1),false));
        var pick=new DatePicker{SelectedDate=agendaDate,Width=135,Margin=new Thickness(3),VerticalAlignment=VerticalAlignment.Center};
        pick.SelectedDateChanged+=(_,_)=>{if(pick.SelectedDate is DateTime d){agendaDate=d.Date;if(agendaMode=="一覧"){agendaFrom=d;agendaTo=d.AddDays(30);}refresh();}};toolbar.Children.Add(pick);
        foreach(var pair in new[]{("月","calendar-month"),("週","calendar-week"),("日","calendar-day"),("一覧","calendar-list")})
        {var b=Tool(pair.Item2,pair.Item1,()=>{agendaMode=pair.Item1;refresh();});if(agendaMode==pair.Item1){b.Background=Soft;b.BorderBrush=Ink;}toolbar.Children.Add(b);}
        Add(toolbar,Tool("sync","同期",()=>modal(GoogleSettings)),Tool("add","予定",()=>modal(()=>EditAgendaEvent(agendaDate))));
        header.Children.Add(toolbar);
        var filters=new WrapPanel{Margin=new Thickness(0,5,0,5)};
        for(int i=0;i<SourceIds.Length;i++){int n=i;var cb=new CheckBox{Content=SourceNames[i],IsChecked=agendaSources[i],Margin=new Thickness(5),VerticalContentAlignment=VerticalAlignment.Center};cb.Click+=(_,_)=>{agendaSources[n]=cb.IsChecked==true;refresh();};filters.Children.Add(cb);}
        var completed=new CheckBox{Content="完了も表示",IsChecked=showCompleted,Margin=new Thickness(5)};completed.Click+=(_,_)=>{showCompleted=completed.IsChecked==true;refresh();};filters.Children.Add(completed);header.Children.Add(filters);
        if(agendaMode=="一覧")
        {
            var ranges=new WrapPanel();
            void Range(DateTime a,DateTime b){agendaFrom=a;agendaTo=b;refresh();}
            Add(ranges,Btn("今日",()=>Range(DateTime.Today,DateTime.Today)),Btn("今週",()=>Range(Agenda.WeekStart(DateTime.Today),Agenda.WeekStart(DateTime.Today).AddDays(6))),Btn("今月",()=>Range(Agenda.MonthStart(DateTime.Today),Agenda.MonthStart(DateTime.Today).AddMonths(1).AddDays(-1))));
            var a=new DatePicker{SelectedDate=agendaFrom,Width=130};var b=new DatePicker{SelectedDate=agendaTo,Width=130};
            Add(ranges,a,Text("～"),b,Btn("表示",()=>{try{if(a.SelectedDate is DateTime x&&b.SelectedDate is DateTime z){Agenda.Range(x,z);Range(x,z);}}catch(Exception ex){Error(ex);}}));header.Children.Add(ranges);
        }
        DockPanel.SetDock(header,Dock.Top);outer.Children.Add(header);
        if(agendaMode=="日"){outer.Children.Add(PageScroll(DayDetail(agendaDate,modal)));return outer;}
        if(agendaMode=="一覧")
        {
            var items=Agenda.Range(agendaFrom,agendaTo).SelectMany(d=>Rows(d)).ToArray();
            // A virtualized list; each entry carries the date so long ranges do not instantiate every row.
            var list=new ListBox{ItemsSource=items,BorderThickness=new Thickness(0),Background=Paper};
            VirtualizingStackPanel.SetIsVirtualizing(list,true);VirtualizingStackPanel.SetVirtualizationMode(list,VirtualizationMode.Recycling);
            var template=new DataTemplate(typeof(AgendaRow));var f=new FrameworkElementFactory(typeof(TextBlock));f.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("Title"));template.VisualTree=f;
            // Display date/source/time alongside title using a read-only grid, double click opens detail.
            var grid=new DataGrid{ItemsSource=items.Select(r=>new{Date=DayText(r.Day),r.Time,Source=SourceNames[Array.IndexOf(SourceIds,r.Source)],r.Title,State=StateText(r),Row=r}).ToArray(),AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,RowHeaderWidth=0,EnableRowVirtualization=true,EnableColumnVirtualization=true};
            foreach(var (label,key) in new[]{("日付","Date"),("時刻","Time"),("種類","Source"),("内容","Title"),("状態","State")})grid.Columns.Add(Col(label,key));
            var view=new DockPanel();var count=Text(items.Length==0?"表示する予定はありません。":$"{items.Length}件 · 行をダブルクリックして詳細",12,false,Muted);DockPanel.SetDock(count,Dock.Top);view.Children.Add(count);
            grid.MouseDoubleClick+=(_,_)=>{if(grid.SelectedItem is object selected&&selected.GetType().GetProperty("Row")?.GetValue(selected) is AgendaRow r)modal(()=>OpenAgendaRow(r));};
            view.Children.Add(grid);outer.Children.Add(view);return outer;
        }
        var days=new UniformGrid{Columns=7};var first=agendaMode=="月"?Agenda.WeekStart(Agenda.MonthStart(agendaDate)):Agenda.WeekStart(agendaDate);
        foreach(string label in new[]{"月","火","水","木","金","土","日"}){var h=Text(label,12,true,Muted);h.TextAlignment=TextAlignment.Center;days.Children.Add(h);}
        int length=agendaMode=="月"?42:7;
        for(int i=0;i<length;i++)
        {
            var d=first.AddDays(i);var rows=Rows(d);var content=new StackPanel();
            Add(content,Text(d.Day.ToString(),14,d==agendaDate,d.Month==agendaDate.Month?Ink:Muted));
            if(rows.Count>0){foreach(var r in rows.Take(agendaMode=="週"?5:2)){var t=Text(r.Time+" "+r.Title,10,false,Muted);t.TextWrapping=TextWrapping.NoWrap;t.TextTrimming=TextTrimming.CharacterEllipsis;content.Children.Add(t);}if(rows.Count>(agendaMode=="週"?5:2))content.Children.Add(Text("ほか "+(rows.Count-(agendaMode=="週"?5:2))+"件",10,false,Muted));}
            var b=WidgetButton(content,DayText(d)+" "+rows.Count+"件",()=>{agendaDate=d;refresh();});b.MinHeight=agendaMode=="週"?115:62;b.Margin=new Thickness(1);b.HorizontalContentAlignment=HorizontalAlignment.Stretch;
            if(d==agendaDate){b.Background=Soft;b.BorderBrush=Ink;b.BorderThickness=new Thickness(2);}else if(d==DateTime.Today)b.BorderBrush=Muted;
            days.Children.Add(b);
        }
        var split=new Grid();var left=new ColumnDefinition{Width=new GridLength(2,GridUnitType.Star)};var right=new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)};
        split.ColumnDefinitions.Add(left);split.ColumnDefinitions.Add(right);split.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});split.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        var monthPanel=new StackPanel();Add(monthPanel,Text(agendaDate.ToString("yyyy年M月"),18,true),days);
        var details=DayDetail(agendaDate,modal);Grid.SetColumn(details,1);split.Children.Add(monthPanel);split.Children.Add(details);
        split.SizeChanged+=(_,_)=>{bool narrow=split.ActualWidth<690;right.Width=narrow?new GridLength(0):new GridLength(1,GridUnitType.Star);Grid.SetColumn(details,narrow?0:1);Grid.SetRow(details,narrow?1:0);};
        outer.Children.Add(PageScroll(split));return outer;
    }
    private FrameworkElement BuildHome(Action<string> select,Action<Action> modal)
    {
        var p=new StackPanel();Add(p,Text("ホーム",24,true),Text(DayText(DateTime.Today)+"  "+DateTime.Now.ToString("HH:mm"),18));
        var dayRows=LessonEngine.Day(data,DateTime.Today,DateTime.Now).Lessons;
        var nextLesson=dayRows.FirstOrDefault(l=>l.State=="remaining");
        var nextCard=new StackPanel();Add(nextCard,Text("次の授業",12,false,Muted),Text(nextLesson==null?"本日の次の授業はありません":nextLesson.Period+"限  "+data.GroupName(nextLesson.GroupId),20,true));
        p.Children.Add(CardBox(nextCard));p.Children.Add(DayDetail(DateTime.Today,modal));
        var tasks=new StackPanel();Add(tasks,Text("期限超過・今日のタスク",16,true));
        foreach(var t in data.Tasks.Where(t=>!t.Done&&t.Due!=""&&string.CompareOrdinal(t.Due,SchoolData.Iso(DateTime.Today))<=0).OrderBy(t=>t.Due))tasks.Children.Add(Btn(t.Due+"  "+t.Title,()=>modal(()=>EditAgendaTask(t.Id))));
        tasks.Children.Add(Tool("tasks","タスク一覧",()=>select("tasks")));p.Children.Add(CardBox(tasks));
        var tools=new WrapPanel();Add(tools,Tool("calendar","カレンダー",()=>select("calendar")),Tool("timetable","時間割",()=>select("timetable")),Tool("lesson-count","授業数",()=>select("counts")));p.Children.Add(tools);return PageScroll(p);
    }
    private FrameworkElement BuildTimetablePage(Action<Action> modal)
    {
        var p=new StackPanel();Add(p,Text("時間割",24,true));
        var bar=new WrapPanel();Add(bar,Tool("edit","週間時間割を編集",()=>modal(Weekly)),Tool("timetable","時間割の版",()=>modal(Versions)),Tool("bell","時程",()=>modal(Bells)),Tool("edit","日別変更",()=>modal(DayChanges)));p.Children.Add(bar);
        var start=Agenda.WeekStart(agendaDate);var grid=new UniformGrid{Columns=7};
        for(int i=0;i<7;i++){var d=start.AddDays(i);var column=new StackPanel();Add(column,Text(DayText(d),12,true));foreach(var l in LessonEngine.Day(data,d,DateTime.Now).Lessons){var labels=new WrapPanel();AddGroupLabels(labels,l.GroupId);var s=new StackPanel();Add(s,Text(l.Period+"限 "+l.Start,11,false,Muted),labels,Text(StateText(new AgendaRow(d,"lesson","",data.GroupName(l.GroupId),l.Start,2,l.State,l)),10,false,Muted));column.Children.Add(WidgetButton(s,"授業詳細",()=>modal(()=>LessonDetail(l))));}grid.Children.Add(CardBox(column));}
        var nav=new WrapPanel();Add(nav,Tool("chevron-left","前週",()=>{agendaDate=agendaDate.AddDays(-7);refreshManagement?.Invoke();}),Text(DayText(start)+" ～ "+DayText(start.AddDays(6))),Tool("chevron-right","翌週",()=>{agendaDate=agendaDate.AddDays(7);refreshManagement?.Invoke();}));Add(p,nav,grid);return PageScroll(p);
    }
    private FrameworkElement BuildCountsPage(Action<Action> modal)
    {
        var root=new DockPanel();var top=new StackPanel();Add(top,Text("授業数",24,true),Text("実績・推定・残予定を区別します。",12,false,Muted));
        var a=new DatePicker{SelectedDate=SchoolData.ParseDate(data.YearFrom)},b=new DatePicker{SelectedDate=SchoolData.ParseDate(data.YearTo)};
        var grid=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,RowHeaderWidth=0};
        foreach(var (label,key) in new[]{("授業","Name"),("確定","Confirmed"),("推定","Estimated"),("残予定","Remaining"),("見込み","Forecast"),("休講","Cancelled"),("要確認","Unresolved")})grid.Columns.Add(Col(label,key));
        void Count(){try{if(a.SelectedDate is DateTime from&&b.SelectedDate is DateTime to)grid.ItemsSource=LessonEngine.Count(data,from,to,DateTime.Now).Select(c=>new{Name=data.GroupName(c.GroupId),c.Confirmed,c.Estimated,c.Remaining,c.Forecast,c.Cancelled,c.Unresolved}).ToArray();}catch(Exception ex){Error(ex);}}
        var bar=new WrapPanel();Add(bar,a,Text("～"),b,Tool("lesson-count","集計",Count),Tool("printer","帳票",()=>modal(Reports)));top.Children.Add(bar);DockPanel.SetDock(top,Dock.Top);Add(root,top,grid);Count();return root;
    }
    private FrameworkElement BuildTasksPage(Action<Action> modal)
    {
        var p=new StackPanel();Add(p,Text("タスク",24,true),Tool("add","タスク追加",()=>modal(()=>EditAgendaTask(null))),Tool("edit","一括編集",()=>modal(Tasks)));
        var search=new TextBox{MinHeight=30,Margin=new Thickness(3)};Add(p,Text("検索",12,false,Muted),search);
        var items=new StackPanel();p.Children.Add(items);
        void Fill(){items.Children.Clear();string now=SchoolData.Iso(DateTime.Today);
            string Group(Todo t)=>t.Done?"完了":t.Due==""?"期限なし":string.CompareOrdinal(t.Due,now)<0?"期限超過":t.Due==now?"今日":"近日";
            foreach(string group in new[]{"期限超過","今日","近日","期限なし","完了"})
            {
                var rows=data.Tasks.Where(t=>Group(t)==group&&t.Title.Contains(search.Text,StringComparison.CurrentCultureIgnoreCase)).OrderBy(t=>t.Due).ToArray();
                if(rows.Length==0)continue;items.Children.Add(Text(group,14,true));
                foreach(var t in rows){var row=new DockPanel();var cb=new CheckBox{IsChecked=t.Done,Margin=new Thickness(5),VerticalAlignment=VerticalAlignment.Center};cb.Click+=(_,_)=>{var d=data.Clone();d.Tasks.Single(x=>x.Id==t.Id).Done=cb.IsChecked==true;if(Commit(d))Fill();else cb.IsChecked=t.Done;};DockPanel.SetDock(cb,Dock.Left);row.Children.Add(cb);row.Children.Add(WidgetButton(t.Title+"  "+t.Due,"編集",()=>modal(()=>EditAgendaTask(t.Id)),false));items.Children.Add(CardBox(row));}
            }
            if(items.Children.Count==0)items.Children.Add(Text("タスクはありません。",13,false,Muted));
        }
        search.TextChanged+=(_,_)=>Fill();Fill();return PageScroll(p);
    }
    private FrameworkElement BuildSettingsPage(Action<Action> modal)
    {
        var p=new StackPanel();Add(p,Text("設定",24,true),Text("授業設定・外観・データ管理",12,false,Muted));
        void Group(string name,params (string id,string label,string hint,Action a)[] entries){p.Children.Add(Text(name,14,true));var g=new UniformGrid{Columns=3};foreach(var e in entries)g.Children.Add(Tile(e.id,e.label,e.hint,()=>modal(e.a)));p.Children.Add(g);}
        Group("授業設定",("classes","学年・色","バッジ",Grades),("classes","クラス・色","バッジ",Classes),("notebook","科目","登録",Subjects),("classes","担当授業","組と科目",Groups),("bell","時程","開始・終了",Bells),("timetable","時間割の版","適用期間",Versions));
        Group("外観・年度",("appearance","年度・表示","考査・空き",Year),("cat","アイコン","10種類",TraySettings));
        Group("データ管理",("spreadsheet","Excel取込","初期データ",ImportExcel),("backup","バックアップ","ローカル",Backup),("restore","復元","読込",Restore));
        return PageScroll(p);
    }
}
