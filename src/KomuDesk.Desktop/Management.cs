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
        var panel=new Border{Background=Soft,Child=PageScroll(nav)};Grid.SetColumn(panel,0);root.Children.Add(panel);
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
        void Modal(Action action){action();if(!closing&&PendingUpdate==null)Refresh();}
        void Refresh()
        {
            Nav();
            if(management!=null)management.Title="KOMU DESK · "+items.FirstOrDefault(x=>x.Id==managementPage).Label;
            main.Content=managementPage switch{
                "calendar"=>BuildAgenda(()=>Refresh(),Modal),
                "timetable"=>BuildTimetablePage(Modal),
                "counts"=>BuildCountsPage(Modal),
                "tasks"=>BuildTasksPage(Modal),
                "reports"=>BuildReportsContent(management??this),
                "google"=>BuildGoogleOverview(Modal),
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
        foreach(var e in entries){var b=Tool(e.Icon,e.Label,e.Action);b.HorizontalAlignment=HorizontalAlignment.Left;p.Children.Add(b);}
        return PageScroll(p);
    }
    private string GoogleSummary(DateTime date)
    {
        var s=googleState?.Snapshot;
        if(googleState==null)return demo?"Google：架空デモ・外部通信なし":"Google：未連携 / "+googleStatus;
        if(s==null)return "Google：未取得 / "+googleStatus;
        return "Google：最終取得 "+s.LastSuccessUtc.ToLocalTime().ToString("M/d HH:mm")+" / "+(string.CompareOrdinal(SchoolData.Iso(date),s.From)<0||string.CompareOrdinal(SchoolData.Iso(date),s.To)>0?"同期範囲外":googleStatus);
    }
    private IReadOnlyList<AgendaRow> Rows(DateTime d,bool filtered=true)
    {
        var rows=Agenda.Local(data,d,DateTime.Now,showCompleted).ToList();
        var s=googleState?.Snapshot;
        if(s!=null&&googleState!=null&&string.CompareOrdinal(SchoolData.Iso(d),s.From)>=0&&string.CompareOrdinal(SchoolData.Iso(d),s.To)<=0)
            rows.AddRange(s.Events.Where(e=>googleState.Session.SelectedIds.Contains(e.CalendarId)&&e.OnDate(d)).Select(e=>{
                int? from=null,to=null;
                if(!e.AllDay)
                {
                    var a=DateTimeOffset.Parse(e.Start,System.Globalization.CultureInfo.InvariantCulture).LocalDateTime;
                    var b=DateTimeOffset.Parse(e.End,System.Globalization.CultureInfo.InvariantCulture).LocalDateTime;
                    from=(int)Math.Clamp((a-d.Date).TotalMinutes,0,1439);to=(int)Math.Clamp(Math.Ceiling((b-d.Date).TotalMinutes),0,1440);
                }
                return new AgendaRow(d.Date,"google",e.CalendarId+":"+e.Id+":"+e.Start,e.Title,e.DisplayTime(d),e.AllDay?0:e.DisplayTime(d)=="継続"?1:2,StartMinute:from,EndMinute:to);
            }));
        return Agenda.Sort(rows.Where(r=>!filtered||agendaSources[Array.IndexOf(SourceIds,r.Source)]));
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
        var w=Dialog(id==null?"予定を追加":"予定を編集",470,450);Action done=w.Close;
        w.Content=EventEditor(d,id,()=>done(),dirty=>done=ProtectDraft(w,dirty));w.ShowDialog();
    }
    private void EditAgendaTask(string? id,DateTime? due=null)
    {
        var w=Dialog(id==null?"タスク追加":"タスク編集",470,420);Action done=w.Close;
        w.Content=TaskEditor(id,due,()=>done(),dirty=>done=ProtectDraft(w,dirty));w.ShowDialog();
    }
    private void EditDay(DateTime d)
    {
        var draft=data.Clone();var date=SchoolData.Iso(d);var item=draft.Days.SingleOrDefault(x=>x.Date==date);
        var w=Dialog("日別変更 · "+DayText(d),460,520);var p=new StackPanel{Margin=new Thickness(16)};
        var closed=new CheckBox{Content="この日は全休",IsChecked=item?.Closed==true,Margin=new Thickness(0,8,0,8)};
        var choices=new ComboBox{ItemsSource=new[]{"通常曜日","日曜日","月曜日","火曜日","水曜日","木曜日","金曜日","土曜日"},SelectedIndex=item?.Weekday is int n?n+1:0,MinHeight=32};
        var reason=new TextBox{Text=item?.Reason??"",MinHeight=32};
        var profile=new ComboBox{ItemsSource=new[]{new Choice("","通常時程")}.Concat(data.BellProfiles.Select(p=>new Choice(p.Id,p.Name))).ToArray(),DisplayMemberPath="Label",SelectedValuePath="Id",SelectedValue=item?.ProfileId??""};
        var limit=new ComboBox{ItemsSource=new[]{"全時限"}.Concat(Enumerable.Range(1,20).Select(n=>n+"限まで")).ToArray(),SelectedIndex=item?.MaxPeriod??0};
        var actions=new WrapPanel();
        Add(actions,Tool("save","保存",()=>{
            draft.Days.RemoveAll(x=>x.Date==date);
            if(closed.IsChecked==true||choices.SelectedIndex>0||reason.Text.Trim()!=""||profile.SelectedIndex>0||limit.SelectedIndex>0)draft.Days.Add(new DayChange{Date=date,Closed=closed.IsChecked==true,Weekday=choices.SelectedIndex>0?choices.SelectedIndex-1:null,ProfileId=(string?)profile.SelectedValue??"",MaxPeriod=limit.SelectedIndex>0?limit.SelectedIndex:null,Reason=reason.Text.Trim()});
            if(Commit(draft))w.Close();
        }),Tool("close","取消",w.Close),Tool("edit","個別時限・一括変更",()=>{w.Close();DayChanges();}));
        Add(p,Text(DayText(d),18,true),Text("曜日を変更する場合だけ選択してください。既存確定実績は変更しません。",12,false,Muted),closed,Text("適用曜日"),choices,Text("時程"),profile,Text("上限時限"),limit,Text("理由"),reason,actions);
        w.Content=PageScroll(p);w.ShowDialog();
    }
    private FrameworkElement DayDetail(DateTime d,Action<Action> modal)
    {
        var p=new StackPanel();Add(p,Text(DayText(d),18,true),Text(GoogleSummary(d),11,false,Muted));
        if(DayScheduleLabel(d)!="")p.Children.Add(Text(DayScheduleLabel(d),13,true));
        if(!LessonEngine.Day(data,d,DateTime.Now).Configured)p.Children.Add(Text("この日の時間割は未設定です。",12,false,Muted));
        var rows=Rows(d);
        if(rows.Count==0)p.Children.Add(Text("表示する予定はありません。",13,false,Muted));
        foreach(var r in rows)p.Children.Add(AgendaLine(r,modal));
        var actions=new WrapPanel();Add(actions,Tool("add","予定追加",()=>modal(()=>EditAgendaEvent(d))),Tool("edit","日別変更",()=>modal(()=>EditDay(d))),Tool("add","タスク",()=>modal(()=>EditAgendaTask(null,d))));p.Children.Add(actions);
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
        Add(toolbar,Tool("sync","同期",()=>modal(GoogleSettings)),Tool("spreadsheet","Excel予定",()=>modal(ImportMore)),Tool("calendar","変更候補",()=>modal(ScheduleReview)),Tool("add","予定",()=>modal(()=>EditAgendaEvent(agendaDate))));
        header.Children.Add(toolbar);
        var filters=new WrapPanel{Margin=new Thickness(0,5,0,5)};filters.Children.Add(Glyph("filter",18));
        for(int i=0;i<SourceIds.Length;i++){int n=i;var cb=new CheckBox{Content=SourceNames[i],IsChecked=agendaSources[i],Margin=new Thickness(5),VerticalContentAlignment=VerticalAlignment.Center};cb.Click+=(_,_)=>{agendaSources[n]=cb.IsChecked==true;refresh();};filters.Children.Add(cb);}
        var completed=new CheckBox{Content="完了も表示",IsChecked=showCompleted,Margin=new Thickness(5)};completed.Click+=(_,_)=>{showCompleted=completed.IsChecked==true;refresh();};filters.Children.Add(completed);header.Children.Add(filters);
        if(agendaMode=="一覧")
        {
            var ranges=new WrapPanel();
            void Range(DateTime a,DateTime b){agendaFrom=a;agendaTo=b;agendaDate=a;refresh();}
            Add(ranges,Btn("今日",()=>Range(DateTime.Today,DateTime.Today)),Btn("今週",()=>Range(Agenda.WeekStart(DateTime.Today),Agenda.WeekStart(DateTime.Today).AddDays(6))),Btn("今月",()=>Range(Agenda.MonthStart(DateTime.Today),Agenda.MonthStart(DateTime.Today).AddMonths(1).AddDays(-1))));
            var a=new DatePicker{SelectedDate=agendaFrom,Width=130};var b=new DatePicker{SelectedDate=agendaTo,Width=130};
            Add(ranges,a,Text("～"),b,Btn("表示",()=>{try{if(a.SelectedDate is DateTime x&&b.SelectedDate is DateTime z){Agenda.Range(x,z);Range(x,z);}}catch(Exception ex){Error(ex);}}));header.Children.Add(ranges);
        }
        DockPanel.SetDock(header,Dock.Top);outer.Children.Add(header);
        if(agendaMode=="日"){outer.Children.Add(PageScroll(DayDetail(agendaDate,modal)));return outer;}
        if(agendaMode=="一覧"){outer.Children.Add(AgendaList(modal));return outer;}
        if(agendaMode=="週"){outer.Children.Add(WeekTimeline(refresh,modal));return outer;}
        var days=new Grid();for(int i=0;i<7;i++)days.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        days.RowDefinitions.Add(new RowDefinition{Height=new GridLength(24)});
        int gridRows=agendaMode=="月"?6:1;for(int i=0;i<gridRows;i++)days.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        void Place(UIElement element,int row,int col){Grid.SetRow(element,row);Grid.SetColumn(element,col);days.Children.Add(element);}
        var first=agendaMode=="月"?Agenda.WeekStart(Agenda.MonthStart(agendaDate)):Agenda.WeekStart(agendaDate);
        int headerIndex=0;foreach(string label in new[]{"月","火","水","木","金","土","日"}){var h=Text(label,12,true,Muted);h.TextAlignment=TextAlignment.Center;Place(h,0,headerIndex++);}
        int length=agendaMode=="月"?42:7;
        for(int i=0;i<length;i++)
        {
            var d=first.AddDays(i);var rows=Rows(d);var content=new StackPanel();
            Add(content,Text(d.Day.ToString(),14,d==agendaDate,d.Month==agendaDate.Month?Ink:Muted));
            if(rows.Count>0){foreach(var r in rows.Take(agendaMode=="週"?5:2)){var t=Text(r.Time+" "+r.Title,10,false,Muted);t.TextWrapping=TextWrapping.NoWrap;t.TextTrimming=TextTrimming.CharacterEllipsis;content.Children.Add(t);}if(rows.Count>(agendaMode=="週"?5:2))content.Children.Add(Text("ほか "+(rows.Count-(agendaMode=="週"?5:2))+"件",10,false,Muted));}
            var b=WidgetButton(content,DayText(d)+" "+rows.Count+"件",()=>{agendaDate=d;refresh();});b.MinHeight=64;b.Padding=new Thickness(4,3,4,3);b.Margin=new Thickness(1);b.HorizontalContentAlignment=HorizontalAlignment.Stretch;
            if(d==agendaDate){b.Background=Soft;b.BorderBrush=Ink;b.BorderThickness=new Thickness(2);}else if(d==DateTime.Today)b.BorderBrush=Muted;
            Place(b,i/7+1,i%7);
        }
        var split=new Grid();var left=new ColumnDefinition{Width=new GridLength(2,GridUnitType.Star)};var right=new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)};
        split.ColumnDefinitions.Add(left);split.ColumnDefinitions.Add(right);split.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});split.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        var monthPanel=new StackPanel();Add(monthPanel,Text(agendaDate.ToString("yyyy年M月"),18,true),days);
        var details=DayDetail(agendaDate,modal);Grid.SetColumn(details,1);split.Children.Add(monthPanel);split.Children.Add(details);
        split.SizeChanged+=(_,_)=>{bool narrow=split.ActualWidth<690;right.Width=narrow?new GridLength(0):new GridLength(1,GridUnitType.Star);Grid.SetColumn(details,narrow?0:1);Grid.SetRow(details,narrow?1:0);};
        outer.Children.Add(PageScroll(split));return outer;
    }
    internal void SetAgendaModeForSmoke(string mode){agendaMode=mode;}
    internal void CheckUi3Interactions()
    {
        if(!demo)throw new InvalidOperationException("demo only");
        agendaMode="月";agendaDate=new DateTime(2026,11,27);
        Content=BuildManagementContent("calendar");UpdateLayout();
        IEnumerable<DependencyObject> Walk(DependencyObject node)
        {
            yield return node;
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)
                foreach(var item in Walk(VisualTreeHelper.GetChild(node,i)))yield return item;
        }
        Button Find(string label)=>Walk(this).OfType<Button>().First(b=>System.Windows.Automation.AutomationProperties.GetName(b)==label);
        void Click(string label){Find(label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));UpdateLayout();}
        if(Template.FindName("captionButtons",this) is not StackPanel caption||caption.Children.Count<2)throw new InvalidOperationException("chrome buttons");
        Click("次へ");if(agendaDate.Month!=12)throw new InvalidOperationException("next month");
        Click("前へ");if(agendaDate!=new DateTime(2026,11,27))throw new InvalidOperationException("previous month");
        Click(DayText(new DateTime(2026,11,26))+" "+Rows(new DateTime(2026,11,26)).Count+"件");
        if(agendaDate.Day!=26)throw new InvalidOperationException("date selection");
        foreach(string mode in new[]{"週","日","一覧","月"}){Click(mode);if(agendaMode!=mode)throw new InvalidOperationException("calendar mode");}
        var box=Walk(this).OfType<CheckBox>().First(x=>Equals(x.Content,"授業"));box.IsChecked=false;box.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));UpdateLayout();
        if(agendaSources[2])throw new InvalidOperationException("agenda filter");
        Click("タスク");if(managementPage!="tasks")throw new InvalidOperationException("navigation");
        Click("カレンダー");if(managementPage!="calendar"||agendaDate.Day!=26)throw new InvalidOperationException("navigation persistence");
        agendaSources[2]=true;agendaDate=DateTime.Today;agendaMode="月";Content=BuildManagementContent("calendar");UpdateLayout();
    }
    internal void CheckUiGlyphs(){foreach(var id in GlyphPaths.Keys){var g=Glyph(id);g.Measure(new Size(30,30));}}
    private FrameworkElement BuildHome(Action<string> select,Action<Action> modal)
    {
        var p=new StackPanel();Add(p,Text("ホーム",24,true),Text(DayText(DateTime.Today)+"  "+DateTime.Now.ToString("HH:mm"),18));
        var dayRows=LessonEngine.Day(data,DateTime.Today,DateTime.Now).Lessons;
        var nextLesson=dayRows.FirstOrDefault(l=>l.State=="remaining");
        var nextCard=new StackPanel();Add(nextCard,Text("次の授業",12,false,Muted),Text(nextLesson==null?"本日の次の授業はありません":nextLesson.Period+"限  "+data.GroupName(nextLesson.GroupId),20,true));
        p.Children.Add(CardBox(nextCard));
        var daily=new UniformGrid{Columns=2};daily.SizeChanged+=(_,_)=>{int n=daily.ActualWidth<540?1:2;if(daily.Columns!=n)daily.Columns=n;};
        foreach(var section in new[]{("今日の授業",new[]{"lesson"}),("今日の予定",new[]{"local","google"})})
        {
            var card=new StackPanel();Add(card,Text(section.Item1,16,true));
            var rows=Rows(DateTime.Today,false).Where(r=>section.Item2.Contains(r.Source)).ToArray();
            if(rows.Length==0)card.Children.Add(Text("表示する項目はありません。",12,false,Muted));
            foreach(var r in rows)card.Children.Add(AgendaLine(r,modal));
            daily.Children.Add(CardBox(card));
        }
        p.Children.Add(daily);
        var tasks=new StackPanel();Add(tasks,Text("期限超過・今日のタスク",16,true));
        foreach(var t in data.Tasks.Where(t=>!t.Done&&t.Due!=""&&string.CompareOrdinal(t.Due,SchoolData.Iso(DateTime.Today))<=0).OrderBy(t=>t.Due))tasks.Children.Add(Btn(t.Due+"  "+t.Title,()=>modal(()=>EditAgendaTask(t.Id))));
        tasks.Children.Add(Tool("tasks","タスク一覧",()=>select("tasks")));p.Children.Add(CardBox(tasks));
        var tools=new WrapPanel();Add(tools,Tool("calendar","カレンダー",()=>select("calendar")),Tool("timetable","時間割",()=>select("timetable")),Tool("lesson-count","授業数",()=>select("counts")));p.Children.Add(tools);return PageScroll(p);
    }
    private FrameworkElement BuildTimetablePage(Action<Action> modal)
    {
        var p=new StackPanel();Add(p,Text("時間割",24,true));
        var bar=new WrapPanel();Add(bar,Tool("edit","かんたん時間割入力",()=>modal(PaintWeekly)),Tool("classes","担当授業登録",()=>modal(RegisterGroups)),Tool("bell","特別時程",()=>modal(SpecialProfiles)),Tool("timetable","時間割の版",()=>modal(Versions)),Tool("bell","時程",()=>modal(Bells)),Tool("edit","日別変更",()=>modal(DayChanges)));p.Children.Add(bar);
        var start=Agenda.WeekStart(agendaDate);var grid=new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(78)});
        for(int i=0;i<7;i++)grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(112)});
        var dates=Enumerable.Range(0,7).Select(i=>start.AddDays(i)).ToArray();
        var days=dates.Select(d=>LessonEngine.Day(data,d,DateTime.Now)).ToArray();
        var periods=data.Bells.Select(x=>x.Number).Concat(days.SelectMany(d=>d.Lessons.Select(l=>l.Period))).Distinct().Order().ToArray();
        for(int i=0;i<=periods.Length;i++)grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        void Put(UIElement element,int r,int c){Grid.SetRow(element,r);Grid.SetColumn(element,c);grid.Children.Add(element);}
        Put(Text("時限",12,true,Muted),0,0);
        for(int j=0;j<7;j++)Put(Text(DayText(dates[j])+(days[j].Configured?"":" · 未設定"),11,true),0,j+1);
        for(int i=0;i<periods.Length;i++)
        {
            int n=periods[i];var bell=data.Bells.Find(b=>b.Number==n);var label=new StackPanel();
            Add(label,Text(n+"限",14,true),Text("時刻は各日の授業に表示",10,false,Muted));Put(CardBox(label),i+1,0);
            for(int j=0;j<7;j++)
            {
                var d=dates[j];var l=days[j].Lessons.FirstOrDefault(l=>l.Period==n);
                if(l==null){var empty=Text(days[j].Configured?"—":"未設定",11,false,Muted);empty.TextAlignment=TextAlignment.Center;Put(new Border{Margin=new Thickness(2),MinHeight=64,Background=White,CornerRadius=new CornerRadius(6),Child=empty},i+1,j+1);continue;}
                var labels=new WrapPanel();AddGroupLabels(labels,l.GroupId);
                var cell=new StackPanel();Add(cell,labels,Text(l.Start+"–"+l.End,10,false,Muted),Text(StateText(new AgendaRow(d,"lesson","",data.GroupName(l.GroupId),l.Start,2,l.State,l)),10,false,Muted));
                var button=WidgetButton(cell,"授業詳細 "+DayText(d)+" "+n+"限",()=>modal(()=>LessonDetail(l)));button.Margin=new Thickness(2);button.MinHeight=64;button.HorizontalContentAlignment=HorizontalAlignment.Left;Put(button,i+1,j+1);
            }
        }
        var nav=new WrapPanel();Add(nav,Tool("chevron-left","前週",()=>{agendaDate=agendaDate.AddDays(-7);refreshManagement?.Invoke();}),Text(DayText(start)+" ～ "+DayText(start.AddDays(6))),Tool("chevron-right","翌週",()=>{agendaDate=agendaDate.AddDays(7);refreshManagement?.Invoke();}));Add(p,nav,new ScrollViewer{Content=grid,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled});return PageScroll(p);
    }
    private sealed record CountEntry(string Id,string Name,int Confirmed,int Estimated,int Remaining,int Forecast,int Cancelled,int Unresolved);
    private FrameworkElement BuildCountsPage(Action<Action> modal)
    {
        var root=new DockPanel();var top=new StackPanel();Add(top,Text("授業数",24,true),Text("実績・推定・残予定を区別します。考査は"+(data.CountExams?"含む":"除く")+"設定です。",12,false,Muted));
        var yearStart=SchoolData.ParseDate(data.YearFrom);var yearEnd=SchoolData.ParseDate(data.YearTo);
        var a=new DatePicker{SelectedDate=countFrom is DateTime ca&&ca>=yearStart&&ca<=yearEnd?ca:yearStart};
        var b=new DatePicker{SelectedDate=countTo is DateTime cb&&cb>=yearStart&&cb<=yearEnd?cb:yearEnd};
        var grid=Table();grid.IsReadOnly=true;grid.SelectionMode=DataGridSelectionMode.Single;
        foreach(var (label,key) in new[]{("授業","Name"),("確定","Confirmed"),("推定","Estimated"),("残予定","Remaining"),("見込み","Forecast"),("休講","Cancelled"),("要確認","Unresolved")})grid.Columns.Add(Col(label,key));
        var summary=Text("",12,false,Muted);
        void Count()
        {
            try
            {
                if(a.SelectedDate is not DateTime from||b.SelectedDate is not DateTime to)return;
                var counts=LessonEngine.Count(data,from,to,DateTime.Now);
                grid.ItemsSource=counts.Select(c=>new CountEntry(c.GroupId,data.GroupName(c.GroupId),c.Confirmed,c.Estimated,c.Remaining,c.Forecast,c.Cancelled,c.Unresolved)).ToArray();
                countFrom=from;countTo=to;
                var missing=Enumerable.Range(0,(to-from).Days+1).Count(n=>!LessonEngine.Day(data,from.AddDays(n),DateTime.Now).Configured);
                summary.Text=$"確定 {counts.Sum(c=>c.Confirmed)} / 推定 {counts.Sum(c=>c.Estimated)} / 残予定 {counts.Sum(c=>c.Remaining)} / 未設定 {missing}日";
            }
            catch(Exception ex){Error(ex);}
        }
        void Detail()
        {
            if(grid.SelectedItem is not CountEntry item||countFrom is not DateTime from||countTo is not DateTime to)return;
            modal(()=>{
                var w=Dialog("授業日一覧 · "+item.Name,690,560);var p=new DockPanel{Margin=new Thickness(12)};
                var title=Text(item.Name+"  "+SchoolData.Iso(from)+" ～ "+SchoolData.Iso(to),16,true);DockPanel.SetDock(title,Dock.Top);p.Children.Add(title);
                var list=new StackPanel();
                foreach(var d in Enumerable.Range(0,(to-from).Days+1).Select(n=>from.AddDays(n)))
                    foreach(var l in LessonEngine.Day(data,d,DateTime.Now).Lessons.Where(l=>l.GroupId==item.Id))
                    {var row=new AgendaRow(d,"lesson",l.Date+":"+l.Period,item.Name,l.Start,2,l.State,l);var line=new DockPanel();var date=Text(DayText(d)+" "+l.Period+"限",12,true);date.Width=120;DockPanel.SetDock(date,Dock.Left);line.Children.Add(date);line.Children.Add(AgendaLine(row,action=>{w.Close();action();}));list.Children.Add(line);}
                if(list.Children.Count==0)list.Children.Add(Text("対象期間に授業はありません。",13,false,Muted));
                p.Children.Add(PageScroll(list));w.Content=p;w.ShowDialog();
            });
        }
        var bar=new WrapPanel();Add(bar,a,Text("～"),b,Tool("lesson-count","集計",Count),Tool("printer","帳票",()=>modal(Reports)),Tool("edit","授業日詳細",Detail));Add(top,bar,summary);DockPanel.SetDock(top,Dock.Top);Add(root,top,grid);
        grid.MouseDoubleClick+=(_,_)=>Detail();Count();return root;
    }
    private FrameworkElement BuildTasksPage(Action<Action> modal)
    {
        var p=new StackPanel();Add(p,Text("タスク",24,true),Tool("add","タスク追加",()=>modal(()=>EditAgendaTask(null))),Tool("edit","一括編集",()=>modal(Tasks)));
        var search=new TextBox{Text=taskSearch,MinHeight=30,Margin=new Thickness(3)};var completed=new CheckBox{Content="完了済みも表示",IsChecked=taskCompleted,Margin=new Thickness(4)};Add(p,Text("検索",12,false,Muted),search,completed);
        var items=new StackPanel();p.Children.Add(items);
        void Fill(){taskSearch=search.Text;taskCompleted=completed.IsChecked==true;items.Children.Clear();string now=SchoolData.Iso(DateTime.Today);
            string Group(Todo t)=>t.Done?"完了":t.Due==""?"期限なし":string.CompareOrdinal(t.Due,now)<0?"期限超過":t.Due==now?"今日":"近日";
            foreach(string group in new[]{"期限超過","今日","近日","期限なし","完了"})
            {
                var rows=data.Tasks.Where(t=>Group(t)==group&&t.Title.Contains(search.Text,StringComparison.CurrentCultureIgnoreCase)).OrderBy(t=>t.Due).ToArray();
                if(rows.Length==0||group=="完了"&&!taskCompleted)continue;items.Children.Add(Text(group+"  "+rows.Length+"件",14,true));
                foreach(var t in rows){var row=new DockPanel();var cb=new CheckBox{IsChecked=t.Done,Margin=new Thickness(5),VerticalAlignment=VerticalAlignment.Center};cb.Click+=(_,_)=>{var d=data.Clone();d.Tasks.Single(x=>x.Id==t.Id).Done=cb.IsChecked==true;if(Commit(d))Fill();else cb.IsChecked=t.Done;};DockPanel.SetDock(cb,Dock.Left);row.Children.Add(cb);row.Children.Add(WidgetButton(t.Title+"  "+t.Due,"編集",()=>modal(()=>EditAgendaTask(t.Id)),false));items.Children.Add(CardBox(row));}
            }
            if(items.Children.Count==0)items.Children.Add(Text("タスクはありません。",13,false,Muted));
        }
        search.TextChanged+=(_,_)=>Fill();completed.Click+=(_,_)=>Fill();Fill();return PageScroll(p);
    }
    private FrameworkElement BuildSettingsPage(Action<Action> modal)
    {
        var p=new StackPanel();Add(p,Text("設定",24,true),Text("授業設定・外観・データ管理",12,false,Muted));
        void Group(string name,params (string id,string label,string hint,Action a)[] entries){p.Children.Add(Text(name,14,true));var g=new UniformGrid{Columns=3};foreach(var e in entries)g.Children.Add(Tile(e.id,e.label,e.hint,()=>modal(e.a)));p.Children.Add(g);}
        Group("授業設定",("classes","学年・色","バッジ",Grades),("classes","クラス・色","バッジ",Classes),("notebook","科目","登録",Subjects),("classes","担当授業","一括登録",RegisterGroups),("classes","担当授業詳細","個別編集",Groups),("bell","特別時程","短縮・変則",SpecialProfiles),("calendar","予定連動ルール","完全一致・自動設定",RuleSettings),("calendar","変更候補","確認・適用",ScheduleReview),("bell","時程","開始・終了",Bells),("timetable","時間割の版","適用期間",Versions));
        Group("外観・年度",("appearance","年度・表示","考査・空き",Year),("cat","アイコン","10種類",TraySettings));
        Group("データ管理",("spreadsheet","Excel初期移行","既定3シート",ImportExcel),("spreadsheet","Excel予定追加","列指定・プレビュー",ImportMore),("backup","バックアップ","ローカル",Backup),("restore","復元","読込",Restore));
        return PageScroll(p);
    }
}
