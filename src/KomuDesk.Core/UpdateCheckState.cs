using System.Globalization;

namespace KomuDesk.Core;

// Only metadata about update checks, never school records or account credentials.
public sealed record UpdateCheckState(DateTimeOffset? Attempt,DateTimeOffset? Success,string LatestTag="")
{
    public static UpdateCheckState Empty=>new(null,null);
    public bool IsDue(DateTimeOffset now)
    {
        if(Attempt==null||Attempt.Value>now)return true;
        var interval=Success==Attempt?TimeSpan.FromHours(12):TimeSpan.FromHours(1);
        return now-Attempt.Value>=interval;
    }
    public bool HasNewer(Version current)=>UpdatePackage.ParseTag(LatestTag) is Version v&&v>current;
    public UpdateCheckState Failed(DateTimeOffset now)=>this with {Attempt=now};
    public UpdateCheckState Succeeded(DateTimeOffset now,string tag)=>new(now,now,tag);
    public string ToStorage()=>string.Join("\n",Attempt?.ToString("O",CultureInfo.InvariantCulture)??"",Success?.ToString("O",CultureInfo.InvariantCulture)??"",LatestTag);
    public static UpdateCheckState Read(string value)
    {
        var parts=value.Split('\n');
        if(parts.Length!=3||parts.Any(p=>p.Length>128))return Empty;
        DateTimeOffset? ReadDate(string s)=>DateTimeOffset.TryParseExact(s,"O",CultureInfo.InvariantCulture,DateTimeStyles.None,out var time)?time:null;
        return new(ReadDate(parts[0]),ReadDate(parts[1]),UpdatePackage.ParseTag(parts[2])!=null?parts[2]:"");
    }
}
