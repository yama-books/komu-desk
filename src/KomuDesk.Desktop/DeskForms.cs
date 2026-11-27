using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Automation;
using KomuDesk.Core;

namespace KomuDesk.Desktop;
internal sealed partial class DeskWindow
{
    internal static void InstallFormStyles(Application app)
    {
        // Native WPF input templates retain keyboard/IME, selection and date-picker behavior.
        void StyleFor(Type type,params (DependencyProperty Property,object Value)[] values)
        {
            var s=new Style(type);foreach(var (p,v) in values)s.Setters.Add(new Setter(p,v));app.Resources[type]=s;
        }
        StyleFor(typeof(TextBox),(Control.BackgroundProperty,White),(Control.ForegroundProperty,Ink),(Control.BorderBrushProperty,Line),(Control.PaddingProperty,new Thickness(8,5,8,5)),(FrameworkElement.MinHeightProperty,32.0),(Control.FontSizeProperty,13.0),(Control.VerticalContentAlignmentProperty,VerticalAlignment.Center));
        StyleFor(typeof(ComboBox),(Control.ForegroundProperty,Ink),(Control.BorderBrushProperty,Line),(Control.PaddingProperty,new Thickness(6,4,6,4)),(FrameworkElement.MinHeightProperty,32.0),(Control.FontSizeProperty,13.0));
        StyleFor(typeof(DatePicker),(Control.ForegroundProperty,Ink),(FrameworkElement.MinHeightProperty,34.0),(Control.FontSizeProperty,13.0),(Control.TemplateProperty,CalendarPickerTemplate()));
        StyleFor(typeof(CheckBox),(Control.ForegroundProperty,Ink),(Control.FontSizeProperty,13.0),(FrameworkElement.MinHeightProperty,24.0));
    }
    private static void Field(Panel p,string label,Control input,string? hint=null)
    {
        var caption=new Label{Content=label,Target=input,Foreground=Ink,FontWeight=FontWeights.SemiBold,Padding=new Thickness(0,9,0,4),FontSize=12};
        AutomationProperties.SetName(input,label);p.Children.Add(caption);p.Children.Add(input);
        if(hint!=null)p.Children.Add(Text(hint,11,false,Muted));
    }
    private static Button Primary(string icon,string label,Action action)
    {
        var b=Tool(icon,label,action);b.Background=Soft;b.BorderBrush=Ink;b.FontWeight=FontWeights.SemiBold;return b;
    }
    private static FrameworkElement FormLayout(string title,string hint,Panel fields,Panel actions)
    {
        var root=new DockPanel{Margin=new Thickness(16)};
        var header=new StackPanel{Margin=new Thickness(0,0,0,10)};Add(header,Text(title,20,true),Text(hint,12,false,Muted));
        DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var footer=new StackPanel{Margin=new Thickness(0,6,0,0)};Add(footer,Rule(),actions);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        root.Children.Add(PageScroll(CardBox(fields)));return root;
    }
    private static Action ProtectDraft(Window w,Func<bool> dirty)
    {
        bool accepted=false;
        w.Closing+=(_,e)=>{if(!accepted&&dirty()&&MessageBox.Show(w,"未保存の変更があります。保存せず閉じますか？","変更の確認",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)e.Cancel=true;};
        return ()=>{accepted=true;w.Close();};
    }
    internal FrameworkElement BuildFormForSmoke(bool task)=>task?TaskEditor(null,DateTime.Today,()=>{}):EventEditor(DateTime.Today,null,()=>{});
    internal void CheckFormInteractions()
    {
        if(!demo)throw new InvalidOperationException("demo only");
        IEnumerable<DependencyObject> Walk(DependencyObject n){yield return n;for(int i=0;i<VisualTreeHelper.GetChildrenCount(n);i++)foreach(var child in Walk(VisualTreeHelper.GetChild(n,i)))yield return child;}
        T Find<T>(string label) where T:FrameworkElement=>Walk(this).OfType<T>().First(x=>AutomationProperties.GetName(x)==label);
        void Click(string label){Find<Button>(label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));UpdateLayout();}
        int before=data.Events.Count;bool saved=false;
        Content=EventEditor(DateTime.Today,null,()=>saved=true);UpdateLayout();
        Click("保存");if(saved||data.Events.Count!=before)throw new Exception("empty event validation");
        Find<TextBox>("予定名").Text="検査用架空予定";Click("保存");
        if(!saved||data.Events.Count!=before+1||data.Events.Last().Time!="")throw new Exception("event save");
        before=data.Events.Count;saved=false;Content=EventEditor(DateTime.Today,null,()=>saved=true);UpdateLayout();Find<TextBox>("予定名").Text="架空時刻検査";Click("明日");Click("15:30");Click("保存");if(!saved||data.Events.Count!=before+1||data.Events.Last().Date!=SchoolData.Iso(DateTime.Today.AddDays(1))||data.Events.Last().Time!="15:30")throw new Exception("event shortcut save");
        before=data.Tasks.Count;saved=false;Content=TaskEditor(null,null,()=>saved=true);UpdateLayout();Find<TextBox>("タスク名").Text="検査用架空タスク";Click("保存");
        if(!saved||data.Tasks.Count!=before+1||data.Tasks.Last().Due!="")throw new Exception("task save");
        before=data.Tasks.Count;saved=false;Content=TaskEditor(null,null,()=>saved=true);UpdateLayout();Find<TextBox>("タスク名").Text="架空期限検査";Click("1週間後");Click("保存");if(!saved||data.Tasks.Count!=before+1||data.Tasks.Last().Due!=SchoolData.Iso(DateTime.Today.AddDays(7)))throw new Exception("task shortcut save");
        agendaFrom=DateTime.Today;agendaTo=DateTime.Today;agendaMode="一覧";Content=BuildManagementContent("calendar");UpdateLayout();
        var query=Find<TextBox>("予定検索");query.Text="検査用架空予定";UpdateLayout();
        var table=Walk(this).OfType<DataGrid>().First();if(table.Items.Count!=1)throw new Exception("agenda search");
        query.Text="存在しない架空検索";UpdateLayout();if(table.Items.Count!=0)throw new Exception("agenda empty search");
        query.Text="";UpdateLayout();
    }
    private FrameworkElement EventEditor(DateTime day,string? id,Action close,Action<Func<bool>>? guard=null)
    {
        var original=id==null?new LocalEvent{Date=SchoolData.Iso(day)}:data.Events.Single(x=>x.Id==id);
        var title=new TextBox{Text=original.Title,FontSize=16,MinHeight=40};var date=new DatePicker{SelectedDate=SchoolData.ParseDate(original.Date)};
        var time=new TextBox{Text=original.Time,MaxLength=5};var allDay=new CheckBox{Content="終日",IsChecked=original.Time==""};
        time.IsEnabled=allDay.IsChecked!=true;allDay.Click+=(_,_)=>time.IsEnabled=allDay.IsChecked!=true;
        var error=Text("",12,false,LateTone.Accent);var fields=new StackPanel();
        var previewTitle=Text("",15,true);var previewDetail=Text("",12,false,LocalTone.Accent);fields.Children.Add(FormPreview("calendar-day",LocalTone,previewTitle,previewDetail));Field(fields,"予定名",title);Field(fields,"日付",date);fields.Children.Add(DateShortcuts(date));fields.Children.Add(allDay);Field(fields,"開始時刻",time,"24時間表記（例 15:30）。終了時刻は登録しません。");var timeButtons=new WrapPanel();foreach(var value in new[]{"08:30","12:00","15:30","17:00"})timeButtons.Children.Add(WidgetButton(value,value,()=>{allDay.IsChecked=false;time.IsEnabled=true;time.Text=value;}));fields.Children.Add(timeButtons);fields.Children.Add(error);
        string CurrentTime()=>allDay.IsChecked==true?"":time.Text.Trim();
        void Preview(){previewTitle.Text=string.IsNullOrWhiteSpace(title.Text)?"新しい予定":title.Text.Trim();previewDetail.Text=(date.SelectedDate is DateTime x?DayText(x):"日付未選択")+" · "+(allDay.IsChecked==true?"終日":time.Text==""?"時刻未入力":time.Text);error.Text="";}
        title.TextChanged+=(_,_)=>Preview();date.SelectedDateChanged+=(_,_)=>Preview();time.TextChanged+=(_,_)=>Preview();allDay.Checked+=(_,_)=>Preview();allDay.Unchecked+=(_,_)=>Preview();Preview();
        title.Loaded+=(_,_)=>{if(!demo)title.Focus();};
        guard?.Invoke(()=>title.Text.Trim()!=original.Title||date.SelectedDate!=SchoolData.ParseDate(original.Date)||CurrentTime()!=original.Time);
        // DatePicker commits native typed date on focus loss; do not silently accept invalid text.
        var actions=new WrapPanel();
        void Save()
        {
            if(string.IsNullOrWhiteSpace(title.Text)){error.Text="予定名を入力してください。";title.Focus();return;}
            if(date.SelectedDate is not DateTime d){error.Text="日付を選択してください。";date.Focus();return;}
            string t=CurrentTime();if(allDay.IsChecked!=true&&Agenda.Minute(t)==null){error.Text="時刻を HH:mm で入力してください。";time.Focus();return;}
            var draft=data.Clone();var item=id==null?new LocalEvent():draft.Events.Single(x=>x.Id==id);item.Title=title.Text.Trim();item.Date=SchoolData.Iso(d);item.Time=t;
            if(id==null)draft.Events.Add(item);
            ApplyAutomatic(draft);
            if(Commit(draft))close();
        }
        var save=Primary("save","保存",Save);save.IsDefault=true;actions.Children.Add(save);
        if(id!=null)actions.Children.Add(Tool("trash","削除",()=>{if(MessageBox.Show(management??this,"このローカル予定を削除しますか？","確認",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;var draft=data.Clone();draft.Events.RemoveAll(x=>x.Id==id);if(Commit(draft))close();}));
        var cancel=Tool("close","取消",()=>{var owner=Window.GetWindow(fields);if(owner!=null&&owner!=this)owner.Close();});actions.Children.Add(cancel);
        return FormLayout(id==null?"予定を追加":"予定を編集","このPCだけに保存する予定です。",fields,actions);
    }
    private FrameworkElement TaskEditor(string? id,DateTime? due,Action close,Action<Func<bool>>? guard=null)
    {
        var original=id==null?new Todo{Due=due.HasValue?SchoolData.Iso(due.Value):""}:data.Tasks.Single(x=>x.Id==id);
        var title=new TextBox{Text=original.Title};var date=new DatePicker{SelectedDate=original.Due==""?null:SchoolData.ParseDate(original.Due)};
        var noDue=new CheckBox{Content="期限なし",IsChecked=original.Due==""};var done=new CheckBox{Content="完了",IsChecked=original.Done,Margin=new Thickness(0,9,0,0)};
        date.IsEnabled=noDue.IsChecked!=true;noDue.Click+=(_,_)=>date.IsEnabled=noDue.IsChecked!=true;
        var fields=new StackPanel();var error=Text("",12,false,LateTone.Accent);
        var previewTitle=Text("",15,true);var previewDetail=Text("",12,false,TaskTone.Accent);fields.Children.Add(FormPreview("tasks",TaskTone,previewTitle,previewDetail));Field(fields,"タスク名",title);fields.Children.Add(noDue);Field(fields,"期限",date);fields.Children.Add(DateShortcuts(date,()=>{noDue.IsChecked=false;date.IsEnabled=true;}));Add(fields,done,error);
        string CurrentDue()=>noDue.IsChecked==true?"":date.SelectedDate.HasValue?SchoolData.Iso(date.SelectedDate.Value):"";
        void Preview(){previewTitle.Text=string.IsNullOrWhiteSpace(title.Text)?"新しいタスク":title.Text.Trim();previewDetail.Text=(noDue.IsChecked==true?"期限なし":date.SelectedDate is DateTime d?DayText(d):"期限未選択")+(done.IsChecked==true?" · 完了":" · 未完了");error.Text="";}
        title.TextChanged+=(_,_)=>Preview();date.SelectedDateChanged+=(_,_)=>Preview();noDue.Checked+=(_,_)=>Preview();noDue.Unchecked+=(_,_)=>Preview();done.Checked+=(_,_)=>Preview();done.Unchecked+=(_,_)=>Preview();Preview();
        title.Loaded+=(_,_)=>{if(!demo)title.Focus();};
        guard?.Invoke(()=>title.Text.Trim()!=original.Title||CurrentDue()!=original.Due||done.IsChecked!=original.Done||(noDue.IsChecked!=true&&!date.SelectedDate.HasValue));
        var actions=new WrapPanel();
        void Save()
        {
            if(string.IsNullOrWhiteSpace(title.Text)){error.Text="タスク名を入力してください。";title.Focus();return;}
            if(noDue.IsChecked!=true&&!date.SelectedDate.HasValue){error.Text="期限を選択するか、期限なしを選んでください。";return;}
            var draft=data.Clone();var item=id==null?new Todo():draft.Tasks.Single(x=>x.Id==id);item.Title=title.Text.Trim();item.Due=CurrentDue();item.Done=done.IsChecked==true;if(id==null)draft.Tasks.Add(item);
            if(Commit(draft))close();
        }
        var save=Primary("save","保存",Save);save.IsDefault=true;actions.Children.Add(save);
        if(id!=null)actions.Children.Add(Tool("trash","削除",()=>{if(MessageBox.Show(management??this,"このタスクを削除しますか？","確認",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;var draft=data.Clone();draft.Tasks.RemoveAll(x=>x.Id==id);if(Commit(draft))close();}));
        var cancel=Tool("close","取消",()=>{var owner=Window.GetWindow(fields);if(owner!=null&&owner!=this)owner.Close();});cancel.IsCancel=true;actions.Children.Add(cancel);
        return FormLayout(id==null?"タスクを追加":"タスクを編集","期限と完了状態はカレンダーにも反映します。",fields,actions);
    }
}
