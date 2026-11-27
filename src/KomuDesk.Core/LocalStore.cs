using System.Text.Json;

namespace KomuDesk.Core;

public sealed class LocalStore
{
    private readonly string path;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public LocalStore(string path) { this.path = Path.GetFullPath(path); }
    public string PathOnDisk => path;
    public SchoolData Load(DateTime today)
    {
        if (!File.Exists(path)) return SchoolData.Empty(today);
        if (new FileInfo(path).Length > 20_000_000) throw new InvalidDataException("データファイルが大きすぎます。");
        var d = JsonSerializer.Deserialize<SchoolData>(File.ReadAllText(path), Options) ?? throw new InvalidDataException("データを読み込めません。");
        d.Validate();
        return d;
    }
    public void Save(SchoolData data)
    {
        if(data.BellProfiles.Count>0||data.ScheduleRules.Count>0||data.Days.Any(d=>d.MaxPeriod.HasValue||d.ProfileId!=""))data.Schema=2;
        data.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, data, Options);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temp, path, path + ".previous", true);
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public void Backup(SchoolData data, string destination)
    {
        var full = Path.GetFullPath(destination);
        if (StringComparer.OrdinalIgnoreCase.Equals(full, path) || StringComparer.OrdinalIgnoreCase.Equals(full, path + ".previous")) throw new InvalidOperationException("稼働データとは別の保存先を選んでください。");
        data.Validate();
        using var stream = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, data, Options);
        stream.Flush(true);
    }
}
