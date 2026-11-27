using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using KomuDesk.Core;
namespace KomuDesk.Desktop;
internal static class RosterVault
{
    internal static string PathName=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KOMU DESK","roster.bin");
    internal static List<RosterStudent> Load(string path)
    {
        if(!File.Exists(path))return new();
        if(new FileInfo(path).Length>16_000_000)throw new InvalidDataException("名列ファイルが大きすぎます。");
        var plain=ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser);
        try{var rows=JsonSerializer.Deserialize<List<RosterStudent>>(plain)??throw new InvalidDataException("名列の読み込みに失敗しました。");RosterImport.Validate(rows);return rows;}
        finally{CryptographicOperations.ZeroMemory(plain);}
    }
    internal static void Save(string path,List<RosterStudent> rows)
    {
        RosterImport.Validate(rows);var plain=JsonSerializer.SerializeToUtf8Bytes(rows);
        try
        {
            var encrypted=ProtectedData.Protect(plain,null,DataProtectionScope.CurrentUser);if(encrypted.Length>16_000_000)throw new InvalidDataException("名列ファイルが大きすぎます。");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllBytes(temp,encrypted);File.Move(temp,path,true);}
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
        finally{CryptographicOperations.ZeroMemory(plain);}
    }
}
