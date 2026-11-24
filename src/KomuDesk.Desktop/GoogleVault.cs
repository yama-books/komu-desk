using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using KomuDesk.Calendar;

namespace KomuDesk.Desktop;

internal sealed record GoogleState(CalendarSession Session,CalendarSnapshot? Snapshot);
internal sealed class GoogleVault
{
    private readonly string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KOMU DESK","calendar-state.bin");
    public GoogleState? Load()
    {
        if(!File.Exists(path))return null;
        if(new FileInfo(path).Length>60_000_000)throw new InvalidDataException("カレンダー保存量の上限を超えています。");
        var bytes=ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser);
        try{return JsonSerializer.Deserialize<GoogleState>(bytes)??throw new InvalidDataException("カレンダー設定を読み込めません。");}
        finally{CryptographicOperations.ZeroMemory(bytes);}
    }
    public void Save(GoogleState value)
    {
        var plain=JsonSerializer.SerializeToUtf8Bytes(value);
        try
        {
            var encrypted=ProtectedData.Protect(plain,null,DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(encrypted);stream.Flush(true);}
                File.Move(temp,path,true);
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
        finally{CryptographicOperations.ZeroMemory(plain);}
    }
    public void Clear(){if(File.Exists(path))File.Delete(path);}
}
