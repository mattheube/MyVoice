using System.Text;
namespace MyVoice.AI;

public static class ManagedRuntime
{
    // uv's redirector can depend on a junction Windows refuses in GUI child processes.
    // Use CPython's own venv launcher, keeping site-packages and every downloaded model.
    public static bool RepairLauncher(string python)
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyVoice", "AI");
        var expected = Path.Combine(root, "venv", "Scripts", "python.exe");
        if (!Path.GetFullPath(python).Equals(expected, StringComparison.OrdinalIgnoreCase) || !File.Exists(python)) return false;
        if (!Encoding.ASCII.GetString(File.ReadAllBytes(python)).Contains("uv trampoline")) return false;
        var home = Path.Combine(root, "python", "cpython-3.11.15-windows-x86_64-none");
        var launcher = Path.Combine(home, "Lib", "venv", "scripts", "nt", "python.exe");
        if (!File.Exists(launcher) || !File.Exists(Path.Combine(home, "python.exe")))
            throw new IOException("Python est incomplet. Utilisez Réparer le moteur dans Voice Lab ; vos voix seront conservées.");
        var cfg = Path.Combine(root, "venv", "pyvenv.cfg");
        if (!File.Exists(python + ".uv-backup")) File.Copy(python, python + ".uv-backup");
        if (File.Exists(cfg) && !File.Exists(cfg + ".backup")) File.Copy(cfg, cfg + ".backup");
        var lines = File.Exists(cfg) ? File.ReadAllLines(cfg).Where(x => !x.StartsWith("home =", StringComparison.OrdinalIgnoreCase)).ToList() : new List<string> { "include-system-site-packages = false" };
        lines.Insert(0, "home = " + home);
        File.WriteAllLines(cfg, lines, new UTF8Encoding(false));
        File.Copy(launcher, python, true);
        return true;
    }
}
