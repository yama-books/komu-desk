using System.Net;
using System.Text;
using System.Text.Json;
using KomuDesk.Calendar;

internal static class CalendarCases
{
    public static async Task<int> Run()
    {
        int passed=0;
        void Check(bool b,string label){if(!b)throw new Exception(label);Console.WriteLine("PASS "+label);passed++;}
        using var doc=JsonDocument.Parse("""{"id":"test","summary":"fictional","start":{"date":"2026-04-06"},"end":{"date":"2026-04-08"}}""");
        var e=GoogleCalendarClient.ParseEvent("fictional",doc.RootElement)!;
        Check(e.OnDate(new DateTime(2026,4,6))&&e.OnDate(new DateTime(2026,4,7))&&!e.OnDate(new DateTime(2026,4,8)),"calendar all-day exclusive end");
        using var cancelled=JsonDocument.Parse("""{"status":"cancelled","id":"x"}""");
        Check(GoogleCalendarClient.ParseEvent("fictional",cancelled.RootElement)==null,"cancelled event ignored");
        using var timed=JsonDocument.Parse("""{"id":"t","start":{"dateTime":"2026-04-06T23:00:00+09:00"},"end":{"dateTime":"2026-04-07T01:00:00+09:00"}}""");
        Check(GoogleCalendarClient.ParseEvent("fictional",timed.RootElement) is {AllDay:false,Title:"名称なし"},"time offset and missing title");
        var handler=new Fake();
        using var client=new GoogleCalendarClient(handler);
        var session=new CalendarSession(new DesktopCredential("fictional.apps.googleusercontent.com","fictional"),"fictional",
            new(){new("cal@example.invalid","架空カレンダー")},new(){"cal@example.invalid"});
        var snapshot=await client.SyncAsync(session,new DateTime(2026,4,1),new DateTime(2026,4,30),CancellationToken.None);
        Check(snapshot.Events.Count==2,"calendar pagination and dedup");
        Check(handler.Calls.Count==3,"only token and two event queries");
        Check(handler.Calls[0].Method=="POST"&&handler.Calls[0].Host=="oauth2.googleapis.com"&&handler.Calls.Skip(1).All(x=>x.Method=="GET"&&x.Host=="www.googleapis.com"),"calendar fixed read-only endpoint");
        Check(handler.Calls.Skip(1).All(x=>x.Query.Contains("singleEvents=true")&&x.Query.Contains("timeMin=")&&x.Query.Contains("timeMax=")),"calendar bounded recurrence expansion");
        var refreshed=await client.RefreshListAsync(session,CancellationToken.None);
        Check(refreshed.Calendars.Count==1&&refreshed.SelectedIds.Count==1,"calendar list selection retained");
        try{await client.SyncAsync(session with {SelectedIds=new(){"not-listed"}},new DateTime(2026,4,1),new DateTime(2026,4,30),CancellationToken.None);throw new Exception("unknown calendar accepted");}
        catch(ArgumentException){Check(true,"unknown calendar rejected");}
        return passed;
    }
    private sealed class Fake:HttpMessageHandler
    {
        public List<(string Method,string Host,string Query)> Calls {get;}=new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct)
        {
            var u=r.RequestUri!;Calls.Add((r.Method.Method,u.Host,u.Query));
            string content;
            if(u.Host=="oauth2.googleapis.com"&&u.AbsolutePath=="/token"&&r.Method==HttpMethod.Post)
                content="""{"access_token":"fictional-token"}""";
            else if(u.Host=="www.googleapis.com"&&r.Method==HttpMethod.Get&&u.AbsolutePath.EndsWith("/calendarList"))
                content="""{"items":[{"id":"cal@example.invalid","summary":"架空カレンダー"}]}""";
            else if(u.Host=="www.googleapis.com"&&r.Method==HttpMethod.Get&&u.AbsolutePath.EndsWith("/events"))
                content=u.Query.Contains("pageToken=")?
                    """{"items":[{"id":"e1","summary":"架空行事","start":{"date":"2026-04-06"},"end":{"date":"2026-04-07"}},{"id":"e2","summary":"架空行事2","start":{"date":"2026-04-07"},"end":{"date":"2026-04-08"}}]}""":
                    """{"nextPageToken":"p2","items":[{"id":"e1","summary":"架空行事","start":{"date":"2026-04-06"},"end":{"date":"2026-04-07"}}]}""";
            else throw new InvalidOperationException("unexpected HTTP request");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(content,Encoding.UTF8,"application/json")});
        }
    }
}
