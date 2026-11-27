using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;

namespace KomuDesk.Desktop;
internal sealed partial class DeskWindow
{
    // The control template owns chrome, so existing dialogs can continue assigning Content.
    private static void ApplyDeskChrome(Window w,bool maximize=true)
    {
        w.WindowStyle=WindowStyle.None;
        w.ResizeMode=ResizeMode.CanResize;
        w.AllowsTransparency=false;
        WindowChrome.SetWindowChrome(w,new WindowChrome{CaptionHeight=38,ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(0),UseAeroCaptionButtons=false});
        var template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Window">
          <Border x:Name="frame" BorderBrush="#D4D4D4" BorderThickness="1" Background="{TemplateBinding Background}">
            <DockPanel>
              <DockPanel DockPanel.Dock="Top" Height="38" Background="#FFFFFF" LastChildFill="True">
                <StackPanel x:Name="captionButtons" DockPanel.Dock="Right" Orientation="Horizontal" Margin="0,2,5,2"/>
                <Image Source="{TemplateBinding Icon}" DockPanel.Dock="Left" Width="22" Height="22" Margin="10,0,7,0"/>
                <TextBlock Text="{TemplateBinding Title}" Foreground="#202020" FontSize="13" FontWeight="SemiBold" VerticalAlignment="Center" TextTrimming="CharacterEllipsis"/>
              </DockPanel>
              <ContentPresenter ContentSource="Content" Margin="{TemplateBinding Padding}"/>
            </DockPanel>
          </Border>
        </ControlTemplate>
        """);
        w.Template=template;
        w.ApplyTemplate();
        if(template.FindName("captionButtons",w) is StackPanel panel)
        {
            void AddButton(string id,string label,Action action)
            {
                var b=Tool(id,label,action,false);b.Margin=new Thickness(1);b.Width=34;b.MinHeight=28;
                WindowChrome.SetIsHitTestVisibleInChrome(b,true);panel.Children.Add(b);
            }
            AddButton("minimize","最小化",()=>SystemCommands.MinimizeWindow(w));
            if(maximize)AddButton("maximize","最大化・元に戻す",()=>{if(w.WindowState==WindowState.Maximized)SystemCommands.RestoreWindow(w);else SystemCommands.MaximizeWindow(w);});
            AddButton("close","閉じる",()=>SystemCommands.CloseWindow(w));
        }
        w.StateChanged+=(_,_)=>{
            if(template.FindName("frame",w) is Border b)b.Padding=w.WindowState==WindowState.Maximized?SystemParameters.WindowResizeBorderThickness:new Thickness(0);
        };
    }
}
