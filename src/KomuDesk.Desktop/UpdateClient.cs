using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using KomuDesk.Core;

namespace KomuDesk.Desktop;
internal sealed record UpdateOffer(string Tag,Version Version,long Size,string Digest);
internal sealed class UpdateClient : IDisposable
{
    private const string Base="https://github.com/yama-books/komu-desk/releases/download/";
    private readonly HttpClient http=new(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false}){Timeout=TimeSpan.FromMinutes(3)};
    public UpdateClient(){http.DefaultRequestHeaders.UserAgent.ParseAdd("KOMU-DESK-Update/1.0");}
    public async Task<UpdateOffer?> Check(Version current,CancellationToken ct)
    {
        using var response=await http.GetAsync("https://api.github.com/repos/yama-books/komu-desk/releases?per_page=30",ct);
        response.EnsureSuccessStatusCode();
        using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var offers=new List<UpdateOffer>();
        foreach(var release in doc.RootElement.EnumerateArray())
        {
            if(release.GetProperty("draft").GetBoolean()||!release.GetProperty("prerelease").GetBoolean())continue;
            string tag=release.GetProperty("tag_name").GetString()??"";
            var version=UpdatePackage.ParseTag(tag);if(version==null||version<=current)continue;
            var assets=release.GetProperty("assets").EnumerateArray().ToArray();
            var a=assets.Where(x=>x.GetProperty("name").GetString()==UpdatePackage.AssetName).ToArray();
            if(a.Length!=1||assets.Count(x=>x.GetProperty("name").GetString()=="SHA256SUMS.txt")!=1)continue;
            long size=a[0].GetProperty("size").GetInt64();
            if(size<=0||size>200_000_000)continue;
            string digest=a[0].TryGetProperty("digest",out var d)&&d.ValueKind==JsonValueKind.String?d.GetString()??"":"";
            offers.Add(new(tag,version,size,digest));
        }
        return offers.OrderByDescending(x=>x.Version).FirstOrDefault();
    }
    private async Task Download(string tag,string name,string path,long max,CancellationToken ct,IProgress<long>? progress=null)
    {
        if(UpdatePackage.ParseTag(tag)==null||name!=UpdatePackage.AssetName&&name!="SHA256SUMS.txt")throw new InvalidOperationException();
        var uri=new Uri(Base+tag+"/"+name);
        for(int i=0;i<5;i++)
        {
            if(uri.Scheme!="https"||!uri.IsDefaultPort||uri.UserInfo!=""||!new[]{"github.com","release-assets.githubusercontent.com","objects.githubusercontent.com"}.Contains(uri.Host))throw new InvalidDataException("許可されていない更新先です。");
            using var response=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,ct);
            if((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {uri=new Uri(uri,response.Headers.Location??throw new InvalidDataException("更新先がありません。"));continue;}
            response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentLength>max)throw new InvalidDataException("更新サイズの上限を超えています。");
            await using var input=await response.Content.ReadAsStreamAsync(ct);
            await using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
            byte[] buffer=new byte[65536];long total=0;int n;
            while((n=await input.ReadAsync(buffer,ct))!=0)
            {total+=n;if(total>max)throw new InvalidDataException("更新サイズの上限を超えています。");await output.WriteAsync(buffer.AsMemory(0,n),ct);progress?.Report(total);}
            return;
        }
        throw new InvalidDataException("更新先の転送回数が多すぎます。");
    }
    public async Task<string> Stage(UpdateOffer offer,CancellationToken ct,IProgress<long> progress)
    {
        var root=UpdateInstall.Root;Directory.CreateDirectory(root);
        var work=Path.Combine(root,"download-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
        try
        {
            var sha=Path.Combine(work,"SHA256SUMS.txt");var zip=Path.Combine(work,"package.zip");
            await Download(offer.Tag,"SHA256SUMS.txt",sha,4096,ct);
            var expected=UpdatePackage.ParseChecksum(await File.ReadAllTextAsync(sha,ct));
            if(offer.Digest!=""&&!offer.Digest.Equals("sha256:"+expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("GitHubのハッシュと配布チェックサムが一致しません。");
            await Download(offer.Tag,UpdatePackage.AssetName,zip,offer.Size,ct,progress);
            if(new FileInfo(zip).Length!=offer.Size)throw new InvalidDataException("更新ファイルのサイズが一致しません。");
            await Task.Run(()=>{UpdatePackage.Verify(zip,expected);UpdatePackage.Extract(zip,Path.Combine(work,"app"));},ct);
            var exe=Path.Combine(work,"app","KOMU-DESK.exe");
            var info=System.Reflection.AssemblyName.GetAssemblyName(Path.Combine(work,"app","KOMU-DESK.dll")).Version;
            if(info==null||info.Major!=offer.Version.Major||info.Minor!=offer.Version.Minor||info.Build!=offer.Version.Build)throw new InvalidDataException("実行ファイルの版が一致しません。");
            ct.ThrowIfCancellationRequested();
            // A verified package must also start its isolated fictional smoke mode before switching.
            using(var probe=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(exe)!,ArgumentList={"--smoke"}})??throw new InvalidOperationException("更新版を試験起動できません。"))
            {
                using var limit=CancellationTokenSource.CreateLinkedTokenSource(ct);limit.CancelAfter(TimeSpan.FromSeconds(30));
                try{await probe.WaitForExitAsync(limit.Token);}
                catch{if(!probe.HasExited)probe.Kill();throw;}
                if(probe.ExitCode!=0)throw new InvalidDataException("更新版の試験起動に失敗しました。");
            }
            string dir=Path.Combine(root,offer.Tag+"-"+Guid.NewGuid().ToString("N"));
            Directory.Move(Path.Combine(work,"app"),dir);
            return dir;
        }
        finally{try{Directory.Delete(work,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
    }
    public void Dispose()=>http.Dispose();
}
