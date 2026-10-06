using System.Text.Json;
using MyVoice.Core;
namespace MyVoice.Infrastructure;
public sealed class LocalStore
{
    public string Root
    {
        get;
    }
    public string? RecoveryMessage
    {
        get; private set;
    }
    private readonly object gate = new();
    public LocalStore(string? root = null)
    {
        Root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyVoice");
        foreach (var d in new[] { "config", "soundboards", "sounds", "images", "voices", "voice-models", "datasets", "logs", "cache", "temp" })
            Directory.CreateDirectory(Path.Combine(Root, d));
    }
    public void Log(string message)
    {
        try
        {
            lock (gate)
            {
                var folder = Path.Combine(Root, "logs");
                File.AppendAllText(Path.Combine(folder, $"{DateTime.Today:yyyy-MM-dd}.log"), $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
                foreach (var old in Directory.GetFiles(folder, "*.log").OrderDescending().Skip(14))
                    File.Delete(old);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
    public AppSettings Load()
    {
        foreach (var name in new[] { "config.json", "config.backup.json" })
        {
            var path = Path.Combine(Root, "config", name);
            if (!File.Exists(path))
                continue;
            try
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? throw new InvalidDataException();
                settings.Validate();
                if (name.Contains("backup"))
                    RecoveryMessage = "backup";
                return settings;
            }
            catch (Exception e) when (e is JsonException or IOException or InvalidDataException or UnauthorizedAccessException) { Log($"Configuration rejected: {name}: {e.Message}"); RecoveryMessage = "defaults"; }
        }
        return new();
    }
    public void Save(AppSettings settings)
    {
        lock (gate)
        {
            settings.Validate();
            var path = Path.Combine(Root, "config", "config.json");
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            if (File.Exists(path))
            {
                try
                {
                    var old = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
                    old?.Validate();
                    if (old != null)
                        File.Copy(path, Path.Combine(Root, "config", "config.backup.json"), true);
                }
                catch (Exception e) when (e is JsonException or InvalidDataException) { Log("Invalid previous configuration preserved outside backup"); }
            }
            File.Move(temp, path, true);
            if (!File.Exists(Path.Combine(Root, "config", "config.backup.json")))
                File.Copy(path, Path.Combine(Root, "config", "config.backup.json"));
        }
    }
}
