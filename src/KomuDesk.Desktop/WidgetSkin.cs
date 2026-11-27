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
    private DateTime renderedMinute = DateTime.MinValue;
    private static readonly Brush Soft = BrushOf("#EFEFEC"), White = BrushOf("#FFFFFF");
    private static ControlTemplate FlatTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border), "face");
        border.SetValue(Border.CornerRadiusProperty,new CornerRadius(5));
        border.SetBinding(Border.BackgroundProperty,new Binding("Background"){RelativeSource=RelativeSource.TemplatedParent});
        border.SetBinding(Border.BorderBrushProperty,new Binding("BorderBrush"){RelativeSource=RelativeSource.TemplatedParent});
        border.SetBinding(Border.BorderThicknessProperty,new Binding("BorderThickness"){RelativeSource=RelativeSource.TemplatedParent});
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetBinding(FrameworkElement.MarginProperty,new Binding("Padding"){RelativeSource=RelativeSource.TemplatedParent});
        presenter.SetBinding(FrameworkElement.HorizontalAlignmentProperty,new Binding("HorizontalContentAlignment"){RelativeSource=RelativeSource.TemplatedParent});
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
        border.AppendChild(presenter);
        var template=new ControlTemplate(typeof(Button)){VisualTree=border};
        var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Border.BackgroundProperty,Soft,"face"));template.Triggers.Add(hover);
        var pressed=new Trigger{Property=ButtonBase.IsPressedProperty,Value=true};pressed.Setters.Add(new Setter(Border.BackgroundProperty,Line,"face"));template.Triggers.Add(pressed);
        var focus=new Trigger{Property=UIElement.IsKeyboardFocusedProperty,Value=true};focus.Setters.Add(new Setter(Border.BorderBrushProperty,Muted,"face"));focus.Setters.Add(new Setter(Border.BorderThicknessProperty,new Thickness(1),"face"));template.Triggers.Add(focus);
        return template;
    }
    private static readonly ControlTemplate Flat = FlatTemplate();
    private static Button WidgetButton(object label, string tip, Action action, bool compact=true)
    {
        var b=new Button {Content=label,ToolTip=tip,Template=Flat,Foreground=Muted,Background=Brushes.Transparent,
            BorderBrush=Brushes.Transparent,BorderThickness=new Thickness(1),Padding=new Thickness(compact?5:8,3,compact?5:8,3),
            MinHeight=26,Margin=new Thickness(1,0,1,0),FontSize=12,HorizontalContentAlignment=HorizontalAlignment.Center,
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
    private static FrameworkElement Cat()
    {
        var canvas=new Canvas{Width=65,Height=46,IsHitTestVisible=false};
        var outline=new System.Windows.Shapes.Path{Stroke=Ink,StrokeThickness=1.4,StrokeLineJoin=PenLineJoin.Round,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,
            Data=Geometry.Parse("M 12,24 L 12,8 L 23,16 L 37,16 L 48,8 L 48,30 Q 48,39 30,39 Q 12,39 12,30 Z M 24,39 Q 13,44 28,44 L 49,44 Q 62,44 60,33 M 27,29 L 30,31 L 33,29 M 10,26 L 3,24 M 10,30 L 3,31 M 50,26 L 56,24 M 50,30 L 57,31")};
        canvas.Children.Add(outline);
        foreach(double x in new[]{22d,36d}){var eye=new Ellipse{Width=2.8,Height=2.8,Fill=Ink};Canvas.SetLeft(eye,x);Canvas.SetTop(eye,23);canvas.Children.Add(eye);}
        return canvas;
    }
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
        WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.CanResize; AllowsTransparency=false;
        WindowChrome.SetWindowChrome(this,new WindowChrome{CaptionHeight=0,ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(0),UseAeroCaptionButtons=false});
        var root=new DockPanel{Margin=new Thickness(16,12,16,12)};
        var header=new StackPanel();
        var brand=new DockPanel{Margin=new Thickness(0,0,0,12)};
        var buttons=Horizontal();DockPanel.SetDock(buttons,Dock.Right);
        Add(buttons,WidgetButton("⚙","設定・管理",Menu),WidgetButton("⌖","最前面切替",ToggleTop),WidgetButton("▫","ミニ表示切替",ToggleMini),
            WidgetButton("−","最小化",()=>WindowState=WindowState.Minimized),WidgetButton("×","閉じる",Close));
        brand.Children.Add(buttons);
        var logo=Text("KOMU DESK",12,true);logo.Cursor=System.Windows.Input.Cursors.SizeAll;logo.ToolTip="ドラッグして移動";
        logo.MouseLeftButtonDown+=(_,e)=>{if(e.LeftButton==System.Windows.Input.MouseButtonState.Pressed)try{DragMove();}catch(InvalidOperationException){}};
        brand.Children.Add(logo);
        header.Children.Add(brand);
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
        var links=Horizontal();DockPanel.SetDock(links,Dock.Right);Add(links,WidgetButton("集計","授業数集計",Counts),WidgetButton("帳票","授業日一覧・帳票",Reports));footer.Children.Add(links);
        updateBadge=WidgetButton("更新あり","アプリのアップデート",Updates);updateBadge.Visibility=Visibility.Collapsed;footer.Children.Add(updateBadge);
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
        body.Children.Clear();body.Visibility=data.Display.Mini?Visibility.Collapsed:Visibility.Visible;
        footerStatus.Text=demo?"架空デモ · 保存なし":googleState==null?"ローカル保存":"ローカル保存 · Google";
        RefreshUpdateNotice();
        UpdateWidgetClock(DateTime.Now);
        if(data.Display.Mini)return;
        var dateRow=new DockPanel{Margin=new Thickness(0,5,0,8)};
        var nav=Horizontal();DockPanel.SetDock(nav,Dock.Right);
        Add(nav,WidgetButton("‹","前日",()=>{day=day.AddDays(-1);Render();}),WidgetButton("›","翌日",()=>{day=day.AddDays(1);Render();}),WidgetButton("今日","今日に戻る",()=>{day=DateTime.Today;Render();}));
        dateRow.Children.Add(nav);
        var picker=new DatePicker{SelectedDate=day,Width=126,FontSize=12,BorderThickness=new Thickness(0),Background=Paper,VerticalAlignment=VerticalAlignment.Center};
        picker.SelectedDateChanged+=(_,_)=>{if(picker.SelectedDate.HasValue){day=picker.SelectedDate.Value.Date;Render();}};
        dateRow.Children.Add(picker);body.Children.Add(dateRow);
        body.Children.Add(Heading("TIME TABLE",WidgetButton("編集","週間時間割を編集",Weekly),WidgetButton("変更","日別変更",DayChanges)));
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
        body.Children.Add(Heading("CALENDAR",WidgetButton("＋","ローカル予定を編集",Events),WidgetButton("同期","Googleカレンダー設定",GoogleSettings)));
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
        body.Children.Add(Heading("TASKS",WidgetButton("＋","タスクを編集",Tasks)));
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
