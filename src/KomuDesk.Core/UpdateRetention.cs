using System.Text.RegularExpressions;
using System.Reflection;

namespace KomuDesk.Core;

// Only the updater's exact per-version directories are eligible. Never accept
// arbitrary paths, never follow a junction/symlink, never remove a newer build.
public static class UpdateRetention
{
    public const string ManifestName=".komu-package-files";
    private static readonly Regex ManagedName=new(@"^(v[0-9]{1,5}\.[0-9]{1,5}\.[0-9]{1,5}-preview)-[a-f0-9]{32}$",RegexOptions.CultureInvariant);
    public static Version? VersionOf(string name)
    {
        var m=ManagedName.Match(name);return m.Success?UpdatePackage.ParseTag(m.Groups[1].Value):null;
    }
    private static bool Linked(string path)=>(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0;
    private static bool CleanAncestors(string path)
    {
        for(DirectoryInfo? p=new(Path.GetFullPath(path));p!=null;p=p.Parent)
            if(!p.Exists||Linked(p.FullName))return false;
        return true;
    }
    // Bounded enumeration without recursive traversal through links.
    private static HashSet<string>? Files(string dir)
    {
        var set=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending=new Stack<string>();pending.Push(dir);int count=0;
        while(pending.Count>0)
        {
            var p=pending.Pop();if(Linked(p))return null;
            foreach(var item in Directory.EnumerateFileSystemEntries(p))
            {
                if(++count>5000||Linked(item))return null;
                if(Directory.Exists(item))pending.Push(item);
                else if(File.Exists(item))set.Add(Path.GetRelativePath(dir,item));
                else return null;
            }
        }
        return set;
    }
    public static void RecordPackage(string dir)
    {
        if(!CleanAncestors(dir))throw new IOException("Unsafe update directory.");
        var files=Files(dir)??throw new IOException("Unsafe package files.");
        if(files.Contains(ManifestName)||files.Any(f=>f.Contains('\n')||f.Contains('\r')))throw new IOException("Unexpected package manifest.");
        File.WriteAllLines(Path.Combine(dir,ManifestName),files.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase));
    }
    private static bool IsVerifiedPackage(string dir,Version version,HashSet<string> files,HashSet<string> currentFiles)
    {
        if(!files.Contains("KOMU-DESK.exe")||!files.Contains("KOMU-DESK.dll")||!files.Contains("KOMU-DESK.runtimeconfig.json"))return false;
        var asm=AssemblyName.GetAssemblyName(Path.Combine(dir,"KOMU-DESK.dll"));
        if(asm.Name!="KOMU-DESK"||asm.Version is not Version v||v.Major!=version.Major||v.Minor!=version.Minor||v.Build!=version.Build)return false;
        var manifest=Path.Combine(dir,ManifestName);
        if(files.Contains(ManifestName))
        {
            if(new FileInfo(manifest).Length>500_000)return false;
            var expected=File.ReadAllLines(manifest).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return expected.Count>0&&files.Where(f=>f!=ManifestName).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(expected);
        }
        // Legacy versions have no manifest. Only accept the exact known runtime
        // layout (subset of the current package), never extra user files.
        return files.IsSubsetOf(currentFiles);
    }
    public static string[] Plan(IEnumerable<string> names,string active,Version current,int keep)
    {
        if(keep<0||keep>2||VersionOf(active)!=current)return [];
        var older=names.Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(n=>(Name:n,Version:VersionOf(n))).Where(x=>x.Version!=null&&x.Version<current)
            .OrderByDescending(x=>x.Version).ThenBy(x=>x.Name,StringComparer.Ordinal).ToArray();
        // Retain all directories of the newest N distinct older versions; duplicates
        // for the same current version are deliberately not removed.
        var retain=older.Select(x=>x.Version).Distinct().Take(keep).ToHashSet();
        return older.Where(x=>!retain.Contains(x.Version)).Select(x=>x.Name).ToArray();
    }
    public static int Clean(string root,string currentDir,Version current,int keep)
    {
        if(keep<0||keep>2||!Directory.Exists(root))return 0;
        root=Path.GetFullPath(root);currentDir=Path.GetFullPath(currentDir).TrimEnd(Path.DirectorySeparatorChar);
        if(!string.Equals(Path.GetDirectoryName(currentDir),root,StringComparison.OrdinalIgnoreCase)||!CleanAncestors(root)||!CleanAncestors(currentDir))return 0;
        string active=Path.GetFileName(currentDir),pointer=Path.Combine(root,"active.txt");
        if(VersionOf(active)!=current||!File.Exists(pointer)||Linked(pointer)||new FileInfo(pointer).Length>128||File.ReadAllText(pointer).Trim()!=active)return 0;
        var currentFiles=Files(currentDir);if(currentFiles==null)return 0;
        var candidates=Directory.EnumerateDirectories(root).Select(Path.GetFileName).OfType<string>().ToArray();
        var planned=Plan(candidates,active,current,keep);int removed=0;
        foreach(var name in planned)
        {
            try
            {
                var dir=Path.Combine(root,name);var v=VersionOf(name);
                if(v==null||!CleanAncestors(dir)||Linked(pointer)||File.ReadAllText(pointer).Trim()!=active)continue;
                var files=Files(dir);
                if(files==null||!IsVerifiedPackage(dir,v,files,currentFiles))continue;
                // Recheck immediately before deletion. Unknown or modified layouts
                // are skipped, including any file added by the user.
                if(!CleanAncestors(dir)||Files(dir) is not HashSet<string> again||!again.SetEquals(files))continue;
                Directory.Delete(dir,true);removed++;
            }
            catch(IOException){}catch(UnauthorizedAccessException){}catch(BadImageFormatException){}catch(System.Security.SecurityException){}
        }
        return removed;
    }
}
