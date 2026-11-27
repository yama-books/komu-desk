using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KomuDesk.Calendar;

public sealed record DesktopCredential(string ClientId,string ClientSecret)
{
    public static DesktopCredential Read(string path)
    {
        using var stream=File.OpenRead(path);return Read(stream);
    }
    public static DesktopCredential Read(Stream stream)
    {
        if(!stream.CanSeek||stream.Length>65536)throw new InvalidDataException("OAuth設定ファイルが大きすぎます。");
        using var doc=JsonDocument.Parse(stream);
        var root=doc.RootElement.GetProperty("installed");
        var id=root.GetProperty("client_id").GetString()??"";
        var secret=root.GetProperty("client_secret").GetString()??"";
        if(!id.EndsWith(".apps.googleusercontent.com",StringComparison.Ordinal)||id.Length>512||string.IsNullOrWhiteSpace(secret)||secret.Length>512)
            throw new InvalidDataException("Google Cloudのデスクトップアプリ用OAuth設定を選んでください。");
        return new DesktopCredential(id,secret);
    }
}
public sealed record CalendarChoice(string Id,string Name);
public sealed record GoogleEvent(string CalendarId,string Id,string Title,string Start,string End,bool AllDay)
{
    public bool OnDate(DateTime date)
    {
        if(AllDay)
            return DateOnly.ParseExact(Start,"yyyy-MM-dd",CultureInfo.InvariantCulture)<=DateOnly.FromDateTime(date)
                &&DateOnly.ParseExact(End,"yyyy-MM-dd",CultureInfo.InvariantCulture)>DateOnly.FromDateTime(date);
        var start=DateTimeOffset.Parse(Start,CultureInfo.InvariantCulture).LocalDateTime;
        var end=DateTimeOffset.Parse(End,CultureInfo.InvariantCulture).LocalDateTime;
        return start<date.Date.AddDays(1)&&end>date.Date;
    }
    public string DisplayTime(DateTime date)
    {
        if(AllDay)return "終日";
        var start=DateTimeOffset.Parse(Start,CultureInfo.InvariantCulture).LocalDateTime;
        return start.Date<date.Date?"継続":start.ToString("HH:mm",CultureInfo.InvariantCulture);
    }
}
public sealed record CalendarSnapshot(string From,string To,DateTimeOffset LastSuccessUtc,List<GoogleEvent> Events);
public sealed record CalendarSession(DesktopCredential Credential,string RefreshToken,List<CalendarChoice> Calendars,List<string> SelectedIds);
public sealed class GoogleCalendarClient : IDisposable
{
    private const string AuthEndpoint="https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint="https://oauth2.googleapis.com/token";
    private const string Api="https://www.googleapis.com/calendar/v3/";
    public const string CalendarListScope="https://www.googleapis.com/auth/calendar.calendarlist.readonly";
    public const string EventsScope="https://www.googleapis.com/auth/calendar.events.readonly";
    private readonly HttpClient http;
    public GoogleCalendarClient(HttpMessageHandler? testHandler=null)
    {
        http=new HttpClient(testHandler??new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false})
        { Timeout=TimeSpan.FromSeconds(30),MaxResponseContentBufferSize=8_000_000 };
    }
    private static string Query(IEnumerable<KeyValuePair<string,string>> pairs)=>string.Join("&",pairs.Select(p=>Uri.EscapeDataString(p.Key)+"="+Uri.EscapeDataString(p.Value)));
    private static string B64(byte[] bytes)=>Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');
    private static string Value(JsonElement obj,string name)=>obj.TryGetProperty(name,out var p)&&p.ValueKind==JsonValueKind.String?p.GetString()??"":"";
    private static void CheckScopes(JsonElement obj)
    {
        var scope=Value(obj,"scope");
        if(scope!=""&&!scope.Split(' ',StringSplitOptions.RemoveEmptyEntries).ToHashSet().SetEquals(new[]{CalendarListScope,EventsScope}))
            throw new InvalidOperationException("必要な読み取り専用権限と一致しません。Google側の許可設定を確認してください。");
    }
    private async Task<JsonDocument> Token(Dictionary<string,string> form,CancellationToken ct)
    {
        using var content=new FormUrlEncodedContent(form);
        using var response=await http.PostAsync(TokenEndpoint,content,ct);
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Google認証を完了できませんでした。設定または再認証を確認してください。");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }
    public async Task<CalendarSession> LoginAsync(DesktopCredential credential,Action<string> openBrowser,CancellationToken token)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromMinutes(3));
        var ct=timeout.Token;
        string verifier=B64(RandomNumberGenerator.GetBytes(48));
        string challenge=B64(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        string state=B64(RandomNumberGenerator.GetBytes(32));
        var listener=new TcpListener(IPAddress.Loopback,0);
        listener.Start();
        try
        {
            int port=((IPEndPoint)listener.LocalEndpoint).Port;
            string redirect=$"http://127.0.0.1:{port}/";
            var url=AuthEndpoint+"?"+Query(new Dictionary<string,string>{
                ["client_id"]=credential.ClientId,["redirect_uri"]=redirect,["response_type"]="code",
                ["scope"]=CalendarListScope+" "+EventsScope,["state"]=state,["code_challenge"]=challenge,
                ["code_challenge_method"]="S256",["access_type"]="offline",["prompt"]="consent"});
            openBrowser(url);
            string code=await Receive(listener,state,ct);
            using var doc=await Token(new Dictionary<string,string>{
                ["client_id"]=credential.ClientId,["client_secret"]=credential.ClientSecret,["code"]=code,
                ["code_verifier"]=verifier,["redirect_uri"]=redirect,["grant_type"]="authorization_code"},ct);
            CheckScopes(doc.RootElement);
            var refresh=Value(doc.RootElement,"refresh_token");var access=Value(doc.RootElement,"access_token");
            if(refresh==""||access=="")throw new InvalidOperationException("更新トークンを取得できませんでした。再認証してください。");
            var calendars=await ListAsync(access,ct);
            return new CalendarSession(credential,refresh,calendars,new List<string>());
        }
        finally{listener.Stop();}
    }
    private static async Task<string> Receive(TcpListener listener,string state,CancellationToken ct)
    {
        // Only loopback; bounded request header, no request body or redirect to arbitrary URLs.
        while(true)
        {
            using var socket=await listener.AcceptTcpClientAsync(ct);
            using var stream=socket.GetStream();
            byte[] bytes=new byte[8192];int used=0;
            using var readTimeout=CancellationTokenSource.CreateLinkedTokenSource(ct);readTimeout.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                while(used<bytes.Length)
                {
                    int n=await stream.ReadAsync(bytes.AsMemory(used),readTimeout.Token);if(n==0)break;used+=n;
                    if(Encoding.ASCII.GetString(bytes,0,used).Contains("\r\n\r\n"))break;
                }
            }
            catch(OperationCanceledException) when(!ct.IsCancellationRequested){continue;}
            string request=Encoding.ASCII.GetString(bytes,0,used);
            string line=request.Split("\r\n")[0];
            var parts=line.Split(' ');
            var args=new Dictionary<string,string>(StringComparer.Ordinal);
            if(parts.Length==3&&parts[0]=="GET"&&parts[1].StartsWith("/?",StringComparison.Ordinal)&&request.Contains("\r\n\r\n"))
            {
                foreach(var param in parts[1][2..].Split('&'))
                {
                    var kv=param.Split('=',2);if(kv.Length==2)args.TryAdd(WebUtility.UrlDecode(kv[0]),WebUtility.UrlDecode(kv[1]));
                }
            }
            bool matches=args.TryGetValue("state",out var returned)&&returned==state;
            bool accepted=matches&&args.TryGetValue("code",out var code)&&!string.IsNullOrWhiteSpace(code)&&code.Length<4096;
            string html=accepted?"Authorization received. Return to KOMU DESK.":"Authorization not completed. Return to KOMU DESK.";
            byte[] message=Encoding.UTF8.GetBytes(html);
            byte[] header=Encoding.ASCII.GetBytes($"HTTP/1.1 {(accepted?"200 OK":"400 Bad Request")}\r\nContent-Type: text/plain; charset=utf-8\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nContent-Length: {message.Length}\r\nConnection: close\r\n\r\n");
            try{await stream.WriteAsync(header,ct);await stream.WriteAsync(message,ct);}catch(IOException){}
            if(accepted)return args["code"];
            if(matches&&args.ContainsKey("error"))throw new InvalidOperationException("Google認証が取り消されました。");
        }
    }
    private async Task<string> Access(CalendarSession session,CancellationToken ct)
    {
        using var doc=await Token(new Dictionary<string,string>{
            ["client_id"]=session.Credential.ClientId,["client_secret"]=session.Credential.ClientSecret,
            ["refresh_token"]=session.RefreshToken,["grant_type"]="refresh_token"},ct);
        CheckScopes(doc.RootElement);
        var access=Value(doc.RootElement,"access_token");if(access=="")throw new InvalidOperationException("Google認証を更新できませんでした。");
        return access;
    }
    private async Task<JsonDocument> Get(string relative,string access,Dictionary<string,string> query,CancellationToken ct)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,Api+relative+"?"+Query(query));
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",access);
        using var response=await http.SendAsync(request,ct);
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Googleカレンダーを取得できませんでした。前回の予定を維持します。");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }
    private async Task<List<CalendarChoice>> ListAsync(string access,CancellationToken ct)
    {
        var result=new List<CalendarChoice>();string page="";
        for(int i=0;i<20;i++)
        {
            var query=new Dictionary<string,string>{{"maxResults","250"},{"fields","nextPageToken,items(id,summary,summaryOverride,accessRole)"},{"minAccessRole","reader"}};
            if(page!="")query["pageToken"]=page;
            using var doc=await Get("users/me/calendarList",access,query,ct);
            if(doc.RootElement.TryGetProperty("items",out var items))foreach(var item in items.EnumerateArray())
            {
                string id=Value(item,"id");string title=Value(item,"summaryOverride");if(title=="")title=Value(item,"summary");
                if(id!="")result.Add(new CalendarChoice(id,title==""?"名称なし":title));
            }
            page=Value(doc.RootElement,"nextPageToken");if(page=="")return result.DistinctBy(x=>x.Id).ToList();
        }
        throw new InvalidOperationException("カレンダー一覧の取得上限を超えました。");
    }
    public async Task<CalendarSession> RefreshListAsync(CalendarSession session,CancellationToken ct)
    {
        var calendars=await ListAsync(await Access(session,ct),ct);
        return session with {Calendars=calendars,SelectedIds=session.SelectedIds.Where(id=>calendars.Any(c=>c.Id==id)).ToList()};
    }
    public async Task<CalendarSnapshot> SyncAsync(CalendarSession session,DateTime from,DateTime to,CancellationToken ct)
    {
        if(from.Date>to.Date||(to.Date-from.Date).TotalDays>366)throw new ArgumentException("同期期間は1年度以内にしてください。");
        if(session.SelectedIds.Count==0||session.SelectedIds.Count>20||session.SelectedIds.Any(id=>!session.Calendars.Any(c=>c.Id==id)))throw new ArgumentException("同期するカレンダーを1〜20件選択してください。");
        string access=await Access(session,ct);
        string min=new DateTimeOffset(from.Date).ToString("o",CultureInfo.InvariantCulture);
        string max=new DateTimeOffset(to.Date.AddDays(1)).ToString("o",CultureInfo.InvariantCulture);
        var result=new List<GoogleEvent>();
        foreach(string id in session.SelectedIds.Distinct())
        {
            string page="";
            for(int i=0;i<20;i++)
            {
                var query=new Dictionary<string,string>{{"timeMin",min},{"timeMax",max},{"singleEvents","true"},{"showDeleted","false"},{"maxResults","2500"},{"fields","nextPageToken,items(id,summary,start,end,status)"}};
                if(page!="")query["pageToken"]=page;
                using var doc=await Get("calendars/"+Uri.EscapeDataString(id)+"/events",access,query,ct);
                if(doc.RootElement.TryGetProperty("items",out var items))
                    foreach(var item in items.EnumerateArray())
                    {
                        var e=ParseEvent(id,item);if(e!=null)result.Add(e);
                        if(result.Count>20000)throw new InvalidOperationException("予定の取得上限を超えました。");
                    }
                page=Value(doc.RootElement,"nextPageToken");if(page=="")break;
                if(i==19)throw new InvalidOperationException("予定の取得ページ上限を超えました。");
            }
        }
        return new CalendarSnapshot(from.ToString("yyyy-MM-dd"),to.ToString("yyyy-MM-dd"),DateTimeOffset.UtcNow,result.DistinctBy(x=>(x.CalendarId,x.Id)).ToList());
    }
    public static GoogleEvent? ParseEvent(string calendarId,JsonElement item)
    {
        if(Value(item,"status")=="cancelled")return null;
        string id=Value(item,"id");if(id=="")throw new InvalidDataException("予定IDがありません。");
        string title=Value(item,"summary");if(title=="")title="名称なし";
        var start=item.GetProperty("start");var end=item.GetProperty("end");
        string a=Value(start,"date"),b=Value(end,"date");
        if(a!=""&&b!="")
        {
            if(DateOnly.ParseExact(a,"yyyy-MM-dd",CultureInfo.InvariantCulture)>=DateOnly.ParseExact(b,"yyyy-MM-dd",CultureInfo.InvariantCulture))throw new InvalidDataException("終日予定の期間が不正です。");
            return new GoogleEvent(calendarId,id,title,a,b,true);
        }
        a=Value(start,"dateTime");b=Value(end,"dateTime");
        if(!DateTimeOffset.TryParse(a,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var x)||!DateTimeOffset.TryParse(b,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var y)||x>=y)
            throw new InvalidDataException("予定の時刻を読み取れません。");
        return new GoogleEvent(calendarId,id,title,x.ToString("o"),y.ToString("o"),false);
    }
    public void Dispose()=>http.Dispose();
}
