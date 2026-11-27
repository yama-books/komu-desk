using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace KomuDesk.Core;

public static class UpdatePackage
{
    public const string AssetName="komu-desk-windows-x64-preview.zip";
    public static Version? ParseTag(string tag)
    {
        if(!Regex.IsMatch(tag,@"^v[0-9]{1,5}\.[0-9]{1,5}\.[0-9]{1,5}-preview$"))return null;
        return Version.TryParse(tag[1..^8],out var v)?v:null;
    }
    public static string Tag(Version v)=>$"v{v.Major}.{v.Minor}.{v.Build}-preview";
    public static string ParseChecksum(string text)
    {
        var match=Regex.Match(text.Trim(),@"\A([a-fA-F0-9]{64})[ \t]+\*?komu-desk-windows-x64-preview\.zip\z");
        if(!match.Success)throw new InvalidDataException("更新用チェックサムが不正です。");
        return match.Groups[1].Value.ToUpperInvariant();
    }
    public static void Verify(string path,string expected)
    {
        if(!Regex.IsMatch(expected,@"\A[A-Fa-f0-9]{64}\z"))throw new InvalidDataException("ハッシュ形式が不正です。");
        using var f=File.OpenRead(path);
        if(!CryptographicOperations.FixedTimeEquals(SHA256.HashData(f),Convert.FromHexString(expected)))throw new InvalidDataException("更新ファイルのハッシュが一致しません。");
    }
    public static void Extract(string zip,string target,CancellationToken ct=default)
    {
        if(Directory.Exists(target)||File.Exists(target))throw new IOException("更新先は新しいフォルダである必要があります。");
        using var archive=ZipFile.OpenRead(zip);
        if(archive.Entries.Count>4000||archive.Entries.Sum(e=>e.Length)>600_000_000)throw new InvalidDataException("展開サイズの上限を超えています。");
        var root=Path.GetFullPath(target)+Path.DirectorySeparatorChar;
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Directory.CreateDirectory(target);
        try
        {
            foreach(var e in archive.Entries)
            {
                ct.ThrowIfCancellationRequested();
                string name=e.FullName.Replace('\\','/');
                var parts=name.TrimEnd('/').Split('/');
                if(name.StartsWith('/')||parts.Any(p=>p.Length==0||p=="."||p==".."||p.EndsWith('.')||p.EndsWith(' ')||p.Contains(':')||p.IndexOfAny(Path.GetInvalidFileNameChars())>=0||Regex.IsMatch(p,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)",RegexOptions.IgnoreCase)))throw new InvalidDataException("安全でないZIPパスです。");
                if(((e.ExternalAttributes>>16)&0xF000)==0xA000||(e.ExternalAttributes&0x400)!=0)throw new InvalidDataException("リンクを含むZIPは使用できません。");
                var full=Path.GetFullPath(Path.Combine(root,Path.Combine(parts)));
                if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!seen.Add(full))throw new InvalidDataException("ZIP内に重複または不正なパスがあります。");
                if(name.EndsWith('/')){Directory.CreateDirectory(full);continue;}
                var ext=Path.GetExtension(full).ToLowerInvariant();
                if(ext is ".xlsx" or ".xls" or ".csv" or ".db" or ".sqlite" or ".sqlite3" or ".bin" or ".ps1" or ".bat" or ".cmd" || Path.GetFileName(full).Equals("data.json",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("更新に不要なファイルが含まれます。");
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                using var source=e.Open();using var destination=new FileStream(full,FileMode.CreateNew,FileAccess.Write,FileShare.None);
                var buffer=new byte[65536];int n;
                while((n=source.Read(buffer,0,buffer.Length))>0){ct.ThrowIfCancellationRequested();destination.Write(buffer,0,n);}
            }
            foreach(var required in new[]{"KOMU-DESK.exe","KOMU-DESK.dll","KOMU-DESK.runtimeconfig.json"})
                if(!File.Exists(Path.Combine(target,required)))throw new InvalidDataException("更新パッケージが不足しています。");
        }
        catch{try{Directory.Delete(target,true);}catch(IOException){}catch(UnauthorizedAccessException){}throw;}
    }
}
