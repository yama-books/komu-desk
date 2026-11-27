using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KomuDesk.Core;

namespace KomuDesk.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        bool smoke=args.Contains("--smoke");
        bool updating=args.Length>0&&args[0]=="--update-wait";
        try{if(updating)UpdateInstall.WaitForOld(args);else if(UpdateInstall.Redirect(args))return 0;}catch{MessageBox.Show("更新版の起動に失敗しました。旧版を起動してください。","KOMU DESK");return 1;}
        using var mutex = new Mutex(true, smoke?@"Local\KomuDesk.Smoke":@"Local\KomuDesk.Desktop", out bool first);
        if (!first) { if(!smoke)MessageBox.Show("KOMU DESK は既に起動しています。", "KOMU DESK"); return 1; }
        try
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            bool demo = smoke || args.Contains("--demo");
            var location = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KOMU DESK", "data.json");
            var store = new LocalStore(location);
            var data = demo ? DemoData.Create(DateTime.Today) : store.Load(DateTime.Today);
            var window=new DeskWindow(store,data,demo);
            if(smoke&&args.Contains("--menu-preview")){window.Width=600;window.Height=780;window.Content=window.BuildSettingsContent(_=>{},()=>{});}
            if(smoke&&args.Contains("--tray-preview")){window.CheckTrayResources();window.Width=530;window.Height=780;window.Content=window.BuildTraySettings(()=>{});}
            if(smoke)
            {
                app.DispatcherUnhandledException+=(_,e)=>{e.Handled=true;app.Shutdown(1);};
                window.Loaded+=(_,_)=>window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>
                {
                    try
                    {
                        window.UpdateLayout();
                        if(!window.IsLoaded||window.ActualWidth<200||window.ActualHeight<150)throw new InvalidOperationException();
                        int i=Array.IndexOf(args,"--smoke");
                        if(i+1<args.Length)
                        {
                            var content=(FrameworkElement)window.Content;
                            int width=(int)Math.Ceiling(content.ActualWidth),height=(int)Math.Ceiling(content.ActualHeight);
                            if(width<1||height<1)throw new InvalidOperationException();
                            var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(content);
                            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                            using var file=File.Create(args[i+1]);encoder.Save(file);
                        }
                        app.Shutdown(0);
                    }
                    catch{app.Shutdown(1);}
                }));
            }
            if(!demo)window.ContentRendered+=(_,_)=>{try{if(args.Contains("--no-update-redirect"))UpdateInstall.ClearPointer();else UpdateInstall.Activate();}catch{}};
            int result=app.Run(window);
            if(!demo&&result==0&&window.SavedOnClose&&window.PendingUpdate==null&&!args.Contains("--no-update-redirect"))
            {
                try{UpdateRetention.Clean(UpdateInstall.Root,AppContext.BaseDirectory,UpdateInstall.Current,window.KeepPrevious);}catch{}
            }
            if(window.PendingUpdate!=null)UpdateInstall.Launch(window.PendingUpdate,Environment.ProcessId);
            return result;
        }
        catch (Exception ex)
        {
            if(!smoke)MessageBox.Show("起動できませんでした。データは上書きしていません。\n" + ex.GetType().Name, "KOMU DESK");
            return 1;
        }
    }
}
