using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;
using KomuDesk.Core;

namespace KomuDesk.Desktop;

internal sealed partial class DeskWindow
{
    private readonly TextBlock dateLabel = new();
    private readonly TextBlock nextCaption = new();
    private readonly WrapPanel nextLabels = new();
    private readonly TextBlock footerStatus = new();
    private readonly TextBlock nextTime = new();
    private DatePicker? widgetDatePicker;
    private string widgetLessonSignature="";
    private string WidgetLessonSignature(DateTime now)=>string.Join("|",LessonEngine.Day(data,day,now).Lessons.Select(l=>l.Period+":"+l.State));
    private DateTime renderedMinute = DateTime.MinValue;
    private static readonly Brush Soft = BrushOf("#EFEFEC"), White = BrushOf("#FFFFFF");
    private static ControlTemplate FlatTemplate()
    {
        // State-driven, short motion on the painted face, never the hit area.
        var transition=SystemParameters.ClientAreaAnimation?"0:0:0.12":"0:0:0";
        var press=SystemParameters.ClientAreaAnimation?"0:0:0.07":"0:0:0";
        var scale=SystemParameters.ClientAreaAnimation?"0.98":"1";
        var shift=SystemParameters.ClientAreaAnimation?"1":"0";
        return (ControlTemplate)System.Windows.Markup.XamlReader.Parse($$"""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Button">
          <Grid Background="Transparent" SnapsToDevicePixels="True">
            <VisualStateManager.VisualStateGroups>
              <VisualStateGroup x:Name="CommonStates">
                <VisualStateGroup.Transitions>
                  <VisualTransition GeneratedDuration="{{transition}}" />
                  <VisualTransition To="Pressed" GeneratedDuration="{{press}}" />
                </VisualStateGroup.Transitions>
                <VisualState x:Name="Normal"/>
                <VisualState x:Name="MouseOver">
                  <Storyboard><DoubleAnimation Storyboard.TargetName="wash" Storyboard.TargetProperty="Opacity" To="0.06" Duration="0"/></Storyboard>
                </VisualState>
                <VisualState x:Name="Pressed">
                  <Storyboard>
                    <DoubleAnimation Storyboard.TargetName="wash" Storyboard.TargetProperty="Opacity" To="0.13" Duration="0"/>
                    <DoubleAnimation Storyboard.TargetName="face" Storyboard.TargetProperty="(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleX)" To="{{scale}}" Duration="0"/>
                    <DoubleAnimation Storyboard.TargetName="face" Storyboard.TargetProperty="(UIElement.RenderTransform).(TransformGroup.Children)[0].(ScaleTransform.ScaleY)" To="{{scale}}" Duration="0"/>
                    <DoubleAnimation Storyboard.TargetName="face" Storyboard.TargetProperty="(UIElement.RenderTransform).(TransformGroup.Children)[1].(TranslateTransform.Y)" To="{{shift}}" Duration="0"/>
                  </Storyboard>
                </VisualState>
                <VisualState x:Name="Disabled">
                  <Storyboard><DoubleAnimation Storyboard.TargetName="face" Storyboard.TargetProperty="Opacity" To="0.48" Duration="0"/></Storyboard>
                </VisualState>
              </VisualStateGroup>
            </VisualStateManager.VisualStateGroups>
            <Grid x:Name="face" RenderTransformOrigin="0.5,0.5">
              <Grid.RenderTransform><TransformGroup><ScaleTransform/><TranslateTransform/></TransformGroup></Grid.RenderTransform>
              <Border x:Name="outline" CornerRadius="8" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}"/>
              <Border x:Name="wash" Background="#202020" CornerRadius="8" Opacity="0" IsHitTestVisible="False"/>
              <ContentPresenter Margin="{TemplateBinding Padding}" HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="Center" RecognizesAccessKey="True"/>
            </Grid>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="outline" Property="BorderBrush" Value="#555555"/><Setter TargetName="outline" Property="BorderThickness" Value="2"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
        """);
    }
    private static readonly ControlTemplate Flat = FlatTemplate();
    private static Button WidgetButton(object label, string tip, Action action, bool compact=true)
    {
        var b=new Button {Content=label,ToolTip=tip,Template=Flat,Foreground=Ink,Background=compact?White:Brushes.Transparent,
            BorderBrush=compact?Line:Brushes.Transparent,BorderThickness=new Thickness(1),Padding=new Thickness(compact?5:8,4,compact?5:8,4),
            MinHeight=28,Margin=new Thickness(1,0,1,0),FontSize=12,HorizontalContentAlignment=HorizontalAlignment.Center,
            Cursor=System.Windows.Input.Cursors.Hand};
        System.Windows.Automation.AutomationProperties.SetName(b,tip);
        b.Click+=(_,_)=>action();return b;
    }
    private static Border Hairline()=>new(){Height=1,Background=Line,Margin=new Thickness(0,11,0,11)};
    private static TextBlock Small(string s)=>Text(s,11,false,Muted);
    private static DockPanel Heading(string title, params Button[] buttons)
    {
        var row=new DockPanel{Margin=new Thickness(0,0,0,5)};
        var tools=Horizontal();DockPanel.SetDock(tools,Dock.Right);foreach(var b in buttons)tools.Children.Add(b);row.Children.Add(tools);
        var label=Text(title,13,true);row.Children.Add(label);return row;
    }
    private static FrameworkElement Cat()=>AssetIcon("cat",60);
    private void AddGroupLabels(Panel panel,string groupId)
    {
        var group=data.Groups.Find(x=>x.Id==groupId);
        var cls=data.Classes.Find(x=>x.Id==group?.ClassId);
        var grade=data.Grades.Find(x=>x.Id==cls?.GradeId);
        if(grade!=null)panel.Children.Add(Badge(grade.Name,grade.Color));
        if(cls!=null)panel.Children.Add(Badge(cls.Name,cls.Color));
        var name=data.Subjects.Find(x=>x.Id==group?.SubjectId)?.Name??"未登録";
        if(!string.IsNullOrWhiteSpace(group?.Name))name+=" "+group.Name;
        panel.Children.Add(Text(name,13,true));
    }
    private void BuildWidget()
    {
        ApplyDeskChrome(this,false);
        var root=new DockPanel{Margin=new Thickness(12,8,12,10)};
        var header=new StackPanel();
        var brand=new DockPanel{Margin=new Thickness(0,0,0,6)};
        var buttons=Horizontal();DockPanel.SetDock(buttons,Dock.Right);
        Add(buttons,Tool("settings","管理",Menu),Tool("pin","最前面切替",ToggleTop,false),Tool("compact","ミニ表示切替",ToggleMini,false));
        brand.Children.Add(buttons);
        brand.Children.Add(Text("TODAY",11,true,Muted));header.Children.Add(brand);
        var timeRow=new DockPanel();
        var cat=Cat();DockPanel.SetDock(cat,Dock.Right);timeRow.Children.Add(cat);
        var timeStack=new StackPanel();
        dateLabel.FontSize=12;dateLabel.Foreground=Muted;
        clock.FontSize=37;clock.FontWeight=FontWeights.Light;clock.Foreground=Ink;clock.FontFamily=new FontFamily("Segoe UI");
        Add(timeStack,dateLabel,clock);timeRow.Children.Add(timeStack);header.Children.Add(timeRow);
        var nextStack=new StackPanel();
        nextCaption.FontSize=11;nextCaption.Foreground=Muted;nextCaption.Margin=new Thickness(0,0,0,5);
        var nextRow=new DockPanel();
        nextTime.FontSize=25;nextTime.FontWeight=FontWeights.SemiBold;nextTime.Foreground=Ink;nextTime.Margin=new Thickness(0,0,12,0);nextTime.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(nextTime,Dock.Left);
        nextRow.Children.Add(nextTime);nextLabels.VerticalAlignment=VerticalAlignment.Center;nextRow.Children.Add(nextLabels);
        next.FontSize=11;next.Foreground=Muted;next.TextWrapping=TextWrapping.Wrap;next.Margin=new Thickness(0,3,0,0);
        Add(nextStack,nextCaption,nextRow,next);
        header.Children.Add(new Border{BorderBrush=Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Background=White,Padding=new Thickness(11,8,11,8),Margin=new Thickness(0,8,0,4),Child=nextStack});
        DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var footer=new DockPanel{Margin=new Thickness(0,8,0,0)};
        var links=Horizontal();DockPanel.SetDock(links,Dock.Right);Add(links,WidgetButton(IconText("lesson-count","集計",14),"授業数集計",Counts),WidgetButton(IconText("printer","帳票",14),"授業日一覧・帳票",Reports));footer.Children.Add(links);
        updateBadge=WidgetButton(IconText("update","更新あり",14),"アプリのアップデート",Updates);updateBadge.Visibility=Visibility.Collapsed;footer.Children.Add(updateBadge);
        DockPanel.SetDock(updateBadge,Dock.Left);
        footerStatus.FontSize=10;footerStatus.Foreground=Muted;footerStatus.VerticalAlignment=VerticalAlignment.Center;footerStatus.TextWrapping=TextWrapping.Wrap;footer.Children.Add(footerStatus);
        DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        root.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        Content=root;
    }
    private void UpdateWidgetClock(DateTime now)
    {
        dateLabel.Text=now.ToString("yyyy.MM.dd  ddd",System.Globalization.CultureInfo.GetCultureInfo("ja-JP"));
        clock.Text=now.ToString("HH:mm");
        var list=LessonEngine.Day(data,now.Date,now).Lessons;
        var active=list.FirstOrDefault(l=>l.State=="remaining"&&now>=now.Date.Add(SchoolData.ParseTime(l.Start))&&now<now.Date.Add(SchoolData.ParseTime(l.End)));
        var upcoming=list.FirstOrDefault(l=>l.State=="remaining"&&now<now.Date.Add(SchoolData.ParseTime(l.Start)));
        var lesson=active??upcoming;
        nextLabels.Children.Clear();
        if(lesson==null){nextCaption.Text="次の授業";nextTime.Text="—";next.Text="本日の次の授業はありません";return;}
        nextCaption.Text=active!=null?"授業中": "次の授業まで";
        var target=now.Date.Add(SchoolData.ParseTime(active!=null?lesson.End:lesson.Start));
        var minutes=Math.Max(0,(int)Math.Ceiling((target-now).TotalMinutes));
        nextTime.Text=minutes>=60?$"{minutes/60}:{minutes%60:00}":$"{minutes}分";
        AddGroupLabels(nextLabels,lesson.GroupId);
        next.Text=$"{lesson.Period}限   {lesson.Start}–{lesson.End}"+(active!=null?"  終了まで":"  開始");
    }
    private void RenderWidget()
    {
        widgetLessonSignature=WidgetLessonSignature(DateTime.Now);widgetDatePicker=null;
        body.Children.Clear();body.Visibility=data.Display.Mini?Visibility.Collapsed:Visibility.Visible;
        footerStatus.Text=demo?"架空デモ · 保存なし":googleState==null?"ローカル保存":"ローカル保存 · Google";
        RefreshUpdateNotice();
        UpdateWidgetClock(DateTime.Now);
        if(data.Display.Mini)return;
        var dateRow=new DockPanel{Margin=new Thickness(0,5,0,8)};
        var nav=Horizontal();DockPanel.SetDock(nav,Dock.Right);
        Add(nav,WidgetButton(Glyph("chevron-left",15),"前日",()=>{day=day.AddDays(-1);Render();}),WidgetButton(Glyph("chevron-right",15),"翌日",()=>{day=day.AddDays(1);Render();}),WidgetButton(GlyphText("calendar-today","今日",14),"今日に戻る",()=>{day=DateTime.Today;Render();}));
        dateRow.Children.Add(nav);
        var picker=new DatePicker{SelectedDate=day,Width=126,FontSize=12,BorderThickness=new Thickness(0),Background=Paper,VerticalAlignment=VerticalAlignment.Center};
        widgetDatePicker=picker;
        picker.SelectedDateChanged+=(_,_)=>{if(picker.SelectedDate.HasValue){day=picker.SelectedDate.Value.Date;Render();}};
        dateRow.Children.Add(picker);body.Children.Add(dateRow);
        body.Children.Add(Heading("TIME TABLE",WidgetButton(GlyphText("edit","編集",14),"週間時間割を編集",Weekly),WidgetButton(GlyphText("edit","変更",14),"日別変更",DayChanges)));
        var now=DateTime.Now;var result=LessonEngine.Day(data,day,now);
        if(!result.Configured)body.Children.Add(Small("この日の時間割は未設定です。"));
        var highlight=day.Date==now.Date?result.Lessons.FirstOrDefault(l=>l.State=="remaining"):null;
        foreach(var n in data.Bells.Select(x=>x.Number).Concat(result.Lessons.Select(x=>x.Period)).Distinct().Order())
        {
            var l=result.Lessons.FirstOrDefault(x=>x.Period==n);var b=data.Bells.Find(x=>x.Number==n);
            if(l==null&&!data.Display.ShowFree)continue;
            var row=new DockPanel{LastChildFill=true,MinHeight=30};
            var prefix=Small($"{n}限  {l?.Start??b?.Start??""}");prefix.Width=76;DockPanel.SetDock(prefix,Dock.Left);row.Children.Add(prefix);
            if(l==null)row.Children.Add(Small("空き時間"));
            else
            {
                string state=l.State switch{"confirmed"=>"✓ 確定","estimated"=>"推定","cancelled"=>"休講","unresolved"=>"要確認",_=>ReferenceEquals(l,highlight)?"次":"予定"};
                var status=Small(state);DockPanel.SetDock(status,Dock.Right);row.Children.Add(status);
                var labels=new WrapPanel{VerticalAlignment=VerticalAlignment.Center};AddGroupLabels(labels,l.GroupId);
                if(l.Kind=="exam")labels.Children.Add(Small("考査"));
                var button=WidgetButton(labels,"授業詳細",()=>LessonDetail(l),false);button.Padding=new Thickness(0,2,0,2);button.HorizontalContentAlignment=HorizontalAlignment.Left;
                row.Children.Add(button);
            }
            body.Children.Add(new Border{Background=ReferenceEquals(l,highlight)&&l!=null?Soft:Brushes.Transparent,CornerRadius=new CornerRadius(5),Padding=new Thickness(5,1,5,1),Child=row});
        }
        body.Children.Add(Hairline());
        body.Children.Add(Heading("予定 · "+day.ToString("M/d"),WidgetButton(GlyphText("calendar","一覧",14),"カレンダーを開く",OpenAgenda),WidgetButton(Glyph("add",16),"予定追加",()=>EditAgendaEvent(day)),WidgetButton(Glyph("sync",16),"Googleカレンダー設定",GoogleSettings)));
        var events=data.Events.Where(e=>e.Date==SchoolData.Iso(day)).Select(e=>(Time:e.Time==""?"終日":e.Time,Title:e.Title,Source:"")).ToList();
        var snapshot=googleState?.Snapshot;
        if(snapshot!=null&&string.CompareOrdinal(SchoolData.Iso(day),snapshot.From)>=0&&string.CompareOrdinal(SchoolData.Iso(day),snapshot.To)<=0)
            events.AddRange(snapshot.Events.Where(e=>googleState!.Session.SelectedIds.Contains(e.CalendarId)&&e.OnDate(day)).Select(e=>(e.DisplayTime(day),e.Title,"G")));
        foreach(var e in events.OrderBy(x=>x.Time=="終日"?0:x.Time=="継続"?1:2).ThenBy(x=>x.Time))
        {
            var row=new DockPanel{Margin=new Thickness(5,3,5,3)};
            var t=Small(e.Time);t.Width=62;DockPanel.SetDock(t,Dock.Left);row.Children.Add(t);
            if(e.Source!=""){var source=Small(e.Source);source.ToolTip="Googleカレンダー";DockPanel.SetDock(source,Dock.Right);row.Children.Add(source);}
            row.Children.Add(Text(e.Title,13));body.Children.Add(row);
        }
        if(events.Count==0)body.Children.Add(Small("予定なし"));
        if(googleState!=null){body.Children.Add(Small(googleStatus));if(snapshot!=null)body.Children.Add(Small("最終取得 "+snapshot.LastSuccessUtc.ToLocalTime().ToString("M/d HH:mm")+"（保存済み）"));}
        body.Children.Add(Hairline());
        body.Children.Add(Heading("TASKS",WidgetButton(Glyph("add",16),"タスク追加",()=>EditAgendaTask(null,day))));
        foreach(var task in data.Tasks.OrderBy(t=>t.Done).ThenBy(t=>t.Due==""?"9999":t.Due).ToList())
        {
            var text=Text(task.Title+(task.Due==""?"":$"  {task.Due}"),13,false,task.Done?Muted:Ink);
            if(task.Done)text.TextDecorations=TextDecorations.Strikethrough;
            var cb=new CheckBox{Content=text,IsChecked=task.Done,Margin=new Thickness(5,5,5,5),VerticalContentAlignment=VerticalAlignment.Center};
            cb.Click+=(_,_)=>{var d=data.Clone();d.Tasks.Single(x=>x.Id==task.Id).Done=cb.IsChecked==true;if(!Commit(d))cb.IsChecked=task.Done;};
            body.Children.Add(cb);
        }
        if(data.Tasks.Count==0)body.Children.Add(Small("タスクなし"));
    }
}
