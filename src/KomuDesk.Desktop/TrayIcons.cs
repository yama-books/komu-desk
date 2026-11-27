using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms=System.Windows.Forms;
using Drawing=System.Drawing;

namespace KomuDesk.Desktop;
internal sealed partial class DeskWindow
{
    internal static readonly string[] TrayNames={"のぞきねこ暦","お昼寝カレンダー","暦の中のねこ","ねこの後ろ姿","黒い窓とねこ","チェックとねこ","ひと休み","暦を見つめるねこ","机からひょっこり","肉球"};
    private Forms.NotifyIcon? tray;
    private Forms.ContextMenuStrip? trayMenu;
    private Drawing.Icon? nativeTrayIcon;
    private static int ValidTray(int id)=>id>=1&&id<=TrayNames.Length?id:2;
    private static readonly int[] IconSizes={16,20,24,32,40,48,64,128,256};
    private static Uri TrayUri(int id,string ext,bool lightInk=false)=>new($"pack://application:,,,/KOMU-DESK;component/Assets/Tray/cat-{ValidTray(id):00}{(lightInk?"-light":"")}.{ext}");
    private static bool LightInk()
    {
        try{return Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","SystemUsesLightTheme",1) is int value&&value==0;}
        catch{return false;}
    }
    private static int IconSize(int requested)=>IconSizes.OrderBy(n=>Math.Abs(n-requested)).ThenByDescending(n=>n).First();
    private static BitmapSource TrayPng(int id)
    {
        var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.UriSource=TrayUri(id,"png");bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.EndInit();bitmap.Freeze();return bitmap;
    }
    private static Drawing.Icon TrayNative(int id,int size,bool lightInk=false)
    {
        using var stream=Application.GetResourceStream(TrayUri(id,"ico",lightInk))?.Stream??throw new InvalidDataException("アイコンがありません。");
        using var icon=new Drawing.Icon(stream,size,size);
        return (Drawing.Icon)icon.Clone();
    }
    private static ImageSource TrayWindowImage(int id,int size,bool lightInk=false)
    {
        using var stream=Application.GetResourceStream(TrayUri(id,"ico"))?.Stream??throw new InvalidDataException("アイコンがありません。");
        var decoder=new IconBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
        var frame=decoder.Frames.OrderBy(f=>Math.Abs(f.PixelWidth-size)).ThenByDescending(f=>f.PixelWidth).First();frame.Freeze();return frame;
    }
    private void ApplyTrayIcon(int id)
    {
        bool lightInk=LightInk();
        var dpi=VisualTreeHelper.GetDpi(this).DpiScaleX;
        Icon=TrayWindowImage(id,IconSize((int)Math.Round(32*dpi)),lightInk);
        if(tray==null)return;
        var fresh=TrayNative(id,IconSize((int)Math.Round(16*dpi)),lightInk);
        var old=nativeTrayIcon;
        try{tray.Icon=fresh;nativeTrayIcon=fresh;}
        catch{fresh.Dispose();throw;}
        old?.Dispose();
    }
    private void BringDesk()
    {
        if(!IsVisible)Show();
        if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;
        Activate();
    }
    private void InitTray()
    {
        // The tray is not a second app or background service.
        try
        {
            ApplyTrayIcon(data.Display.TrayIcon);
            SourceInitialized+=(_,_)=>{try{ApplyTrayIcon(data.Display.TrayIcon);}catch{}};
            DpiChanged+=(_,_)=>{try{ApplyTrayIcon(data.Display.TrayIcon);}catch{}};
            if(demo)return;
            trayMenu=new Forms.ContextMenuStrip();
            void Item(string label,Action action)
            {
                var item=trayMenu.Items.Add(label);
                item.Click+=(_,_)=>Dispatcher.BeginInvoke(new Action(()=>{
                    if(closing||!IsEnabled)return;BringDesk();action();
                }));
            }
            Item("表示する",()=>{});
            Item("設定・管理",Menu);
            Item("アイコンを選ぶ",TraySettings);
            Item("アップデート",Updates);
            trayMenu.Items.Add(new Forms.ToolStripSeparator());
            Item("終了",Close);
            tray=new Forms.NotifyIcon{Text="KOMU DESK",ContextMenuStrip=trayMenu};
            ApplyTrayIcon(data.Display.TrayIcon);
            tray.DoubleClick+=(_,_)=>Dispatcher.BeginInvoke(new Action(()=>{if(!closing&&IsEnabled)BringDesk();}));
            tray.Visible=true;
        }
        catch{DisposeTray();/* Widget remains usable even if the Windows shell is unavailable. */}
    }
    private void DisposeTray()
    {
        if(tray!=null){tray.Visible=false;tray.Dispose();tray=null;}
        trayMenu?.Dispose();trayMenu=null;
        nativeTrayIcon?.Dispose();nativeTrayIcon=null;
    }
    internal FrameworkElement BuildTraySettings(Action close)
    {
        int selected=ValidTray(data.Display.TrayIcon);
        var p=new StackPanel{Margin=new Thickness(16)};
        var title=Text("アイコンを選ぶ",22,true);
        var detail=Text("10種類から選択できます。保存するとタスクバーと通知領域に反映します。",13,false,Muted);
        var cards=new UniformGrid{Columns=2,Margin=new Thickness(0,10,0,8)};
        var buttons=new List<Button>();var labels=new List<TextBlock>();
        void Highlight()
        {
            for(int i=0;i<buttons.Count;i++)
            {
                bool active=i+1==selected;
                buttons[i].BorderBrush=active?Ink:Line;
                buttons[i].BorderThickness=new Thickness(active?2:1);
                labels[i].Text=active?"選択中":" ";
            }
        }
        for(int i=1;i<=TrayNames.Length;i++)
        {
            int id=i;
            var stack=new StackPanel{HorizontalAlignment=HorizontalAlignment.Stretch};
            var picture=new Image{Source=TrayPng(id),Width=48,Height=48,Stretch=Stretch.Uniform};
            var name=Text($"{id:00}  {TrayNames[id-1]}",12,true);name.TextAlignment=TextAlignment.Center;
            var samples=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,4,0,2)};
            foreach(var bg in new[]{"#E8E8E8","#242424"})
            {
                var im=new Image{Source=TrayWindowImage(id,16,bg=="#242424"),Width=16,Height=16,Stretch=Stretch.Uniform};
                samples.Children.Add(new Border{Background=BrushOf(bg),CornerRadius=new CornerRadius(3),Padding=new Thickness(4),Margin=new Thickness(2,0,2,0),Child=im});
            }
            var label=Text("",10,true,Muted);label.TextAlignment=TextAlignment.Center;labels.Add(label);
            Add(stack,picture,name,samples,label);
            var b=WidgetButton(stack,TrayNames[id-1],()=>{selected=id;Highlight();});b.Margin=new Thickness(4);b.Padding=new Thickness(6);b.MinHeight=108;b.HorizontalContentAlignment=HorizontalAlignment.Stretch;
            buttons.Add(b);cards.Children.Add(b);
        }
        var footer=Horizontal();
        Add(footer,Btn("保存",()=>{
            var d=data.Clone();d.Display.TrayIcon=selected;
            if(!Commit(d))return;
            try{ApplyTrayIcon(selected);close();}
            catch(Exception ex){Error(ex);}
        }),Btn("取消",close));
        Highlight();Add(p,title,detail,cards,footer);
        return new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
    }
    private void TraySettings()
    {
        var w=Dialog("アイコンを選ぶ",530,780);
        w.Height=Math.Min(780,SystemParameters.WorkArea.Height-40);w.MinWidth=370;
        w.Content=BuildTraySettings(w.Close);w.ShowDialog();
    }
    internal void CheckTrayResources()
    {
        for(int i=1;i<=TrayNames.Length;i++)
        {
            var png=TrayPng(i);if(png.PixelWidth!=64||png.PixelHeight!=64)throw new InvalidDataException("PNG size");
            foreach(bool light in new[]{false,true})
            {
                using var stream=Application.GetResourceStream(TrayUri(i,"ico",light))?.Stream??throw new InvalidDataException("ICO missing");
                var frames=new IconBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames;
                if(!frames.Select(f=>f.PixelWidth).Order().SequenceEqual(IconSizes))throw new InvalidDataException("ICO frames");
                foreach(int size in IconSizes.Where(n=>n<=64))
                {using var icon=TrayNative(i,size,light);if(icon.Width!=size||icon.Height!=size)throw new InvalidDataException("ICO size");}
            }
            ApplyTrayIcon(i);
        }
        ApplyTrayIcon(data.Display.TrayIcon);
    }
}
