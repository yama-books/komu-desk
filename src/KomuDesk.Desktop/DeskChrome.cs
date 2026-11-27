using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;

namespace KomuDesk.Desktop;
internal sealed partial class DeskWindow
{
    // The control template owns chrome, so existing dialogs can continue assigning Content.
    private void ApplyDeskChrome(Window w,bool maximize=true)
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
                <Image x:Name="brandIcon" DockPanel.Dock="Left" Width="22" Height="22" Margin="10,0,7,0"/>
                <TextBlock Text="{TemplateBinding Title}" Foreground="#202020" FontSize="13" FontWeight="SemiBold" VerticalAlignment="Center" TextTrimming="CharacterEllipsis"/>
              </DockPanel>
              <ContentPresenter ContentSource="Content" Margin="{TemplateBinding Padding}"/>
            </DockPanel>
          </Border>
        </ControlTemplate>
        """);
        w.Template=template;
        void Wire()
        {
            w.ApplyTemplate();
            if(template.FindName("brandIcon",w) is Image image)image.Source=TrayPng(data.Display.TrayIcon);
            if(template.FindName("captionButtons",w) is not StackPanel panel||panel.Children.Count>0)return;
            void AddButton(string id,string label,Action action)
            {
                var b=Tool(id,label,action,false);b.Margin=new Thickness(1);b.Width=34;b.MinHeight=28;
                WindowChrome.SetIsHitTestVisibleInChrome(b,true);panel.Children.Add(b);
            }
            AddButton("minimize","最小化",()=>SystemCommands.MinimizeWindow(w));
            if(maximize){AddButton("maximize","最大化・元に戻す",()=>{if(w.WindowState==WindowState.Maximized)SystemCommands.RestoreWindow(w);else SystemCommands.MaximizeWindow(w);});var b=(Button)panel.Children[panel.Children.Count-1];w.StateChanged+=(_,_)=>b.Content=Glyph(w.WindowState==WindowState.Maximized?"window-restore":"maximize");}
            AddButton("close","閉じる",()=>SystemCommands.CloseWindow(w));
        }
        Wire();w.Loaded+=(_,_)=>Wire();
        w.StateChanged+=(_,_)=>{
            if(template.FindName("frame",w) is Border b)b.Padding=w.WindowState==WindowState.Maximized?SystemParameters.WindowResizeBorderThickness:new Thickness(0);
        };
    }
}
