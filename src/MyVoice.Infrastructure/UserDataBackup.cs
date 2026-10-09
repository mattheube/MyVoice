using System.Security.Cryptography;
using System.Text.Json;
namespace MyVoice.Infrastructure;
public static class UserDataBackup
{
    public static string EnsureV2Backup(string root) => EnsureVersionBackup(root,"v2");
    public static string EnsureVersionBackup(string root,string version)
    {
        if(version.Any(c=>!char.IsLetterOrDigit(c)&&c!='.'&&c!='-'))throw new ArgumentException("Invalid version");
        var destination = Path.Combine(root, "backups", "before-"+version);
        var complete = Path.Combine(destination, "complete.json");
        if (File.Exists(complete)) return destination;
        Directory.CreateDirectory(destination);
        var hashes = new Dictionary<string, string>();
        foreach (var folder in new[] { "config", "soundboards", "sounds", "images", "voices", "voice-models", "datasets", "packages" })
        {
            var source = Path.Combine(root, folder);
            if (!Directory.Exists(source)) continue;
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file);
                var copy = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
                File.Copy(file, copy, true);
                using var stream = File.OpenRead(copy);
                hashes[relative] = Convert.ToHexString(SHA256.HashData(stream));
            }
        }
        File.WriteAllText(complete, JsonSerializer.Serialize(hashes, new JsonSerializerOptions { WriteIndented = true }));
        return destination;
    }
}
