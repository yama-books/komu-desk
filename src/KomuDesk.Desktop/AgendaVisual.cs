using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using KomuDesk.Core;

namespace KomuDesk.Desktop;
internal sealed partial class DeskWindow
{
    private sealed record AgendaTone(Brush Fill,Brush Accent,string Label);
    private static readonly AgendaTone LocalTone=new(BrushOf("#E8EFF8"),BrushOf("#365B89"),"ローカル予定");
    private static readonly AgendaTone GoogleTone=new(BrushOf("#EAE5F5"),BrushOf("#66518C"),"Google予定");
    private static readonly AgendaTone LessonTone=new(BrushOf("#E0EFE6"),BrushOf("#356B50"),"授業");
    private static readonly AgendaTone TaskTone=new(BrushOf("#F7EBCF"),BrushOf("#805A19"),"タスク");
    private static readonly AgendaTone LateTone=new(BrushOf("#F7E5E3"),BrushOf("#9B3936"),"期限超過");
    private static readonly AgendaTone QuietTone=new(BrushOf("#EEEEEC"),BrushOf("#666666"),"完了・休講");
    private static AgendaTone Tone(string source)=>source switch{"local"=>LocalTone,"google"=>GoogleTone,"lesson"=>LessonTone,"task"=>TaskTone,_=>QuietTone};
    private AgendaTone ToneFor(AgendaRow row)
    {
        if(row.State is "cancelled" or "done")return QuietTone;
        if(row.Source=="task"&&row.Day<DateTime.Today)return LateTone;
        if(row.Source=="lesson"&&row.Lesson!=null)
        {
            var group=data.Groups.Find(g=>g.Id==row.Lesson.GroupId);
            var cls=data.Classes.Find(c=>c.Id==group?.ClassId);
            if(cls!=null)
            {
                var color=((SolidColorBrush)BrushOf(cls.Color)).Color;
                byte Mix(byte c)=>(byte)Math.Round(c*.42+255*.58);
                byte Dark(byte c)=>(byte)Math.Round(c*.43);
                return new(new SolidColorBrush(Color.FromRgb(Mix(color.R),Mix(color.G),Mix(color.B))),new SolidColorBrush(Color.FromRgb(Dark(color.R),Dark(color.G),Dark(color.B))),"授業");
            }
        }
        return Tone(row.Source);
    }
    private static FrameworkElement ToneMark(AgendaTone tone,double height=12)=>new Border{Width=4,Height=height,Background=tone.Accent,CornerRadius=new CornerRadius(2),Margin=new Thickness(0,0,5,0),VerticalAlignment=VerticalAlignment.Center};
    private static Border ToneCard(UIElement child,AgendaTone tone)=>new(){Background=tone.Fill,CornerRadius=new CornerRadius(6),Padding=new Thickness(5,3,5,3),Margin=new Thickness(0,2,0,2),Child=child};
    private FrameworkElement MonthEntry(AgendaRow row)
    {
        var tone=ToneFor(row);var p=new DockPanel{LastChildFill=true};var mark=ToneMark(tone,12);DockPanel.SetDock(mark,Dock.Left);p.Children.Add(mark);
        var t=Text(row.Title,11,false,Ink);t.TextWrapping=TextWrapping.NoWrap;t.TextTrimming=TextTrimming.CharacterEllipsis;p.Children.Add(t);
        var card=ToneCard(p,tone);card.ToolTip=row.Time+" "+row.Title+" · "+StateText(row);card.Padding=new Thickness(3,1,3,1);card.Margin=new Thickness(0,1,0,1);return card;
    }
    private static void PaintAgenda(Button button,AgendaTone tone)
    {
        button.Background=tone.Fill;button.BorderBrush=tone.Accent;button.Foreground=Ink;
    }
    private static FrameworkElement ColorLegend()
    {
        var p=new WrapPanel{Margin=new Thickness(0,3,0,3)};
        foreach(var tone in new[]{LocalTone,GoogleTone,LessonTone,TaskTone,LateTone,QuietTone})
        {
            var item=Horizontal();item.Margin=new Thickness(0,0,9,3);Add(item,ToneMark(tone),Text(tone.Label,11,false,Muted));p.Children.Add(item);
        }
        return p;
    }
    // Preserve the entire native date picker and calendar popup, including IME and accessibility.
    private static void StyleCalendarButton(DatePicker picker)
    {
        picker.ApplyTemplate();
        if(picker.Template?.FindName("PART_Button",picker) is not Button button)return;
        button.Template=Flat;button.Content=Glyph("calendar-month",16);
        button.Background=White;button.BorderBrush=Line;button.BorderThickness=new Thickness(1);
        button.Padding=new Thickness(3);button.MinWidth=25;button.MinHeight=25;button.Focusable=false;
        button.ToolTip="カレンダーを開く";
        System.Windows.Automation.AutomationProperties.SetName(button,"カレンダーを開く");
    }
    private static FrameworkElement DateShortcuts(DatePicker date,Action? activate=null)
    {
        var row=new WrapPanel{Margin=new Thickness(0,3,0,3)};
        foreach(var (label,offset) in new[]{("今日",0),("明日",1),("1週間後",7)})
            row.Children.Add(Tool("calendar-day",label,()=>{activate?.Invoke();date.SelectedDate=DateTime.Today.AddDays(offset);}));
        return row;
    }
    private static FrameworkElement FormPreview(string icon,AgendaTone tone,TextBlock title,TextBlock details)
    {
        var p=new DockPanel();var g=Glyph(icon,20);g.Margin=new Thickness(0,0,9,0);DockPanel.SetDock(g,Dock.Left);p.Children.Add(g);
        var s=new StackPanel();Add(s,Text(tone.Label,11,true,tone.Accent),title,details);p.Children.Add(s);return ToneCard(p,tone);
    }
    internal void CheckCalendarPicker()
    {
        if(!demo)throw new InvalidOperationException("demo only");
        var p=new DatePicker{SelectedDate=DateTime.Today};Content=p;UpdateLayout();StyleCalendarButton(p);
        if(p.Template.FindName("PART_Button",p) is not Button b||b.Content is not Viewbox||p.Template.FindName("PART_TextBox",p) is not DatePickerTextBox||p.Template.FindName("PART_Popup",p) is not Popup)throw new InvalidOperationException("date picker template parts");
        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));UpdateLayout();
        if(!p.IsDropDownOpen)throw new InvalidOperationException("calendar popup open");
        p.IsDropDownOpen=false;p.SelectedDate=DateTime.Today.AddDays(1);
        if(p.SelectedDate!=DateTime.Today.AddDays(1))throw new InvalidOperationException("calendar date selection");
        Content=BuildManagementContent("calendar");UpdateLayout();
    }
}
