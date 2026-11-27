using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using KomuDesk.Calendar;

namespace KomuDesk.Desktop;
internal static class GoogleClientConfig
{
    private static string PathName=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KOMU DESK","oauth-client.bin");
    // Installed-app credentials identify a public OAuth client, not a user account.
    // Never persist access/refresh tokens here, or copy these settings to school-data exports.
    internal static DesktopCredential? Resolve(CalendarSession? session)
    {
        if(File.Exists(PathName))
        {
            if(new FileInfo(PathName).Length>65536)throw new InvalidDataException("OAuth設定が大きすぎます。");
            var bytes=ProtectedData.Unprotect(File.ReadAllBytes(PathName),null,DataProtectionScope.CurrentUser);
            try
            {
                using var stream=new MemoryStream(bytes,false);
                return DesktopCredential.Read(stream);
            }
            finally{CryptographicOperations.ZeroMemory(bytes);}
        }
        if(session!=null)return session.Credential;
        using var resource=typeof(GoogleClientConfig).Assembly.GetManifestResourceStream("KomuDesk.GoogleDesktopOAuth");
        return resource==null?null:DesktopCredential.Read(resource);
    }
    internal static void Save(DesktopCredential credential)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(new{installed=new{client_id=credential.ClientId,client_secret=credential.ClientSecret}});
        try
        {
            var encrypted=ProtectedData.Protect(bytes,null,DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);
            var temp=PathName+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllBytes(temp,encrypted);File.Move(temp,PathName,true);}
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
        finally{CryptographicOperations.ZeroMemory(bytes);}
    }
    internal static void Clear(){if(File.Exists(PathName))File.Delete(PathName);}
}
