using System.IO;
using System.Threading;
using System.Windows;
using KomuDesk.Core;

namespace KomuDesk.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var mutex = new Mutex(true, @"Local\KomuDesk.Desktop", out bool first);
        if (!first) { MessageBox.Show("KOMU DESK は既に起動しています。", "KOMU DESK"); return; }
        try
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            bool demo = args.Contains("--demo");
            var location = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KOMU DESK", "data.json");
            var store = new LocalStore(location);
            var data = demo ? DemoData.Create(DateTime.Today) : store.Load(DateTime.Today);
            app.Run(new DeskWindow(store, data, demo));
        }
        catch (Exception ex)
        {
            MessageBox.Show("起動できませんでした。データは上書きしていません。\n" + ex.GetType().Name, "KOMU DESK");
        }
    }
}
