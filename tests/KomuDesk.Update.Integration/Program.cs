using System.Diagnostics;
using KomuDesk.Core;
using KomuDesk.Desktop;

if(!OperatingSystem.IsWindows()||Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||args.Length!=1||args[0]!="--isolated-ci")throw new InvalidOperationException("Hosted Windows CI only.");
var home=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KOMU DESK");
if(Directory.Exists(home))throw new InvalidOperationException("Refuse to use any existing application data.");
Process? app=null;
try
{
    Directory.CreateDirectory(home);
    // Use a fictional, valid first-run dataset with one local task.
    var store=new LocalStore(Path.Combine(home,"data.json"));
    var data=SchoolData.Empty(DateTime.Today);
    data.Tasks.Add(new Todo{Title="CI fictional task"});
    store.Save(data);
    using var ct=new CancellationTokenSource(TimeSpan.FromMinutes(8));
    using var client=new UpdateClient();
    var offer=await client.Check(UpdateInstall.Current,ct.Token)??throw new Exception("No update found");
    if(offer.Version!=new Version(0,1,2))throw new Exception("Unexpected release "+offer.Tag);
    Console.WriteLine("PASS real public release discovery");
    var staged=await client.Stage(offer,ct.Token,new Progress<long>());
    if(!UpdateInstall.IsManaged(staged,out var version)||version!=offer.Version)throw new Exception("Staging invalid");
    Console.WriteLine("PASS real download/hash/extraction/fictional WPF preflight");
    var pointer=Path.Combine(UpdateInstall.Root,"active.txt");
    if(File.Exists(pointer))throw new Exception("Preflight unexpectedly activated");
    // Simulate a still-running old process and release it by writing a signal.
    var signal=Path.Combine(home,"old-exit.txt");
    using var old=Process.Start(new ProcessStartInfo("powershell.exe"){UseShellExecute=false,CreateNoWindow=true,ArgumentList={"-NoProfile","-Command",$"while (!(Test-Path -LiteralPath '{signal.Replace("'","''")}')) {{ Start-Sleep -Milliseconds 100 }}"}})??throw new Exception("Cannot start old process");
    try
    {
        var start=new ProcessStartInfo(Path.Combine(staged,"KOMU-DESK.exe")){UseShellExecute=false,WorkingDirectory=staged,ArgumentList={"--update-wait",old.Id.ToString()}};
        app=Process.Start(start)??throw new Exception("Cannot start new process");
        await Task.Delay(1500,ct.Token);
        if(File.Exists(pointer)||app.HasExited)throw new Exception("Activated before old process exited");
        File.WriteAllText(signal,"exit");
        await old.WaitForExitAsync(ct.Token);
        var deadline=DateTime.UtcNow.AddSeconds(30);
        while(!File.Exists(pointer)&&DateTime.UtcNow<deadline&&!app.HasExited)await Task.Delay(200,ct.Token);
        if(!File.Exists(pointer)||File.ReadAllText(pointer)!=Path.GetFileName(staged)||app.HasExited)throw new Exception("New version activation failed");
        if(store.Load(DateTime.Today).Tasks.Single().Title!="CI fictional task")throw new Exception("Local data changed");
        Console.WriteLine("PASS old-process wait, actual WPF activation, local task preservation");
        deadline=DateTime.UtcNow.AddSeconds(10);
        while(app.MainWindowHandle==IntPtr.Zero&&DateTime.UtcNow<deadline){await Task.Delay(200,ct.Token);app.Refresh();}
        if(!app.CloseMainWindow())throw new Exception("Cannot close new application normally");
        using var closeTimeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));await app.WaitForExitAsync(closeTimeout.Token);
        if(app.ExitCode!=0||store.Load(DateTime.Today).Tasks.Single().Title!="CI fictional task")throw new Exception("Updated application did not close safely");
        Console.WriteLine("PASS updated application normal shutdown");
        UpdateInstall.ClearPointer();
        if(File.Exists(pointer))throw new Exception("Rollback pointer remains");
        Console.WriteLine("PASS clear active pointer; staged version remains");
    }
    finally{if(!old.HasExited)old.Kill();}
}
finally
{
    if(app!=null){if(!app.HasExited){app.Kill();await app.WaitForExitAsync();}app.Dispose();}
    if(Directory.Exists(home))Directory.Delete(home,true);
}
