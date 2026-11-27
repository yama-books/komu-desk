using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using KomuDesk.Core;

namespace KomuDesk.Desktop;
internal static class UpdateInstall
{
    internal static Version Current=>typeof(UpdateInstall).Assembly.GetName().Version is Version v?new Version(v.Major,v.Minor,v.Build):new Version(0,1,2);
    internal static string Root=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KOMU DESK","versions");
    private static string Pointer=>Path.Combine(Root,"active.txt");
    internal static bool IsManaged(string dir,out Version? version)
    {
        version=null;
        var full=Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar);
        if(!string.Equals(Path.GetDirectoryName(full),Path.GetFullPath(Root),StringComparison.OrdinalIgnoreCase))return false;
        var match=Regex.Match(Path.GetFileName(full),@"^(v[0-9]+\.[0-9]+\.[0-9]+-preview)-[a-f0-9]{32}$");
        if(!match.Success)return false;
        version=UpdatePackage.ParseTag(match.Groups[1].Value);
        return version!=null&&Directory.Exists(full)&&(File.GetAttributes(full)&FileAttributes.ReparsePoint)==0&&File.Exists(Path.Combine(full,"KOMU-DESK.exe"));
    }
    internal static void Activate()
    {
        var dir=AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        if(!IsManaged(dir,out var v)||v!=Current)return;
        Directory.CreateDirectory(Root);
        var temp=Pointer+"."+Guid.NewGuid().ToString("N");
        try{File.WriteAllText(temp,Path.GetFileName(dir));File.Move(temp,Pointer,true);}
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
    internal static void ClearPointer(){if(File.Exists(Pointer))File.Delete(Pointer);}
    internal static bool Redirect(string[] args)
    {
        if(args.Length!=0||!File.Exists(Pointer))return false;
        try
        {
            if(new FileInfo(Pointer).Length>128)return false;
            string name=File.ReadAllText(Pointer).Trim();
            if(name!=Path.GetFileName(name))return false;
            string dir=Path.Combine(Root,name);
            if(!IsManaged(dir,out var v)||v<=Current)return false;
            Launch(dir);return true;
        }
        catch{return false;}
    }
    internal static void Launch(string dir,int? waitPid=null)
    {
        if(!IsManaged(dir,out _))throw new InvalidDataException("更新先が不正です。");
        var p=new ProcessStartInfo(Path.Combine(dir,"KOMU-DESK.exe")){UseShellExecute=false,WorkingDirectory=dir};
        if(waitPid.HasValue){p.ArgumentList.Add("--update-wait");p.ArgumentList.Add(waitPid.Value.ToString());}
        using var started=Process.Start(p)??throw new InvalidOperationException("更新版を起動できません。");
    }
    internal static void WaitForOld(string[] args)
    {
        if(args.Length!=2||args[0]!="--update-wait"||!int.TryParse(args[1],out var pid)||pid<=0||pid==Environment.ProcessId||!IsManaged(AppContext.BaseDirectory,out var v)||v!=Current)throw new InvalidOperationException("更新起動条件が不正です。");
        try{using var old=Process.GetProcessById(pid);if(!old.WaitForExit(20000))throw new InvalidOperationException("旧版が終了していません。");}
        catch(ArgumentException){}
    }
}
