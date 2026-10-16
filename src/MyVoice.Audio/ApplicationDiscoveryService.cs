using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using MyVoice.Core;
namespace MyVoice.Audio;

/// <summary>Lightweight snapshots, shared by the picker and capture reconciliation. No Process handles retained.</summary>
public sealed class ApplicationDiscoveryService
{
    public static ApplicationDiscoveryService Shared {get;}=new();
    private readonly object gate=new();
    private DateTime nextScan;
    private ApplicationAudioSource[] snapshot=[];
    private delegate bool WindowCallback(IntPtr window,IntPtr state);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback,IntPtr state);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out int pid);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool QueryFullProcessImageName(IntPtr process,int flags,StringBuilder path,ref int length);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern int GetPackageFamilyName(IntPtr process,ref uint length,StringBuilder? family);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern int GetApplicationUserModelId(IntPtr process,ref uint length,StringBuilder? id);
    public IReadOnlyList<ApplicationAudioSource> Discover(IEnumerable<AudioChannel>? saved=null,bool force=false)
    {
        lock(gate)
        {
            if(force||DateTime.UtcNow>=nextScan){snapshot=Scan(saved?.ToArray()??[]);nextScan=DateTime.UtcNow.AddSeconds(5);}
            var result=snapshot.ToList();
            foreach(var rule in saved??[])
            {
                if(rule.Type!="application")continue;
                if(result.Any(a=>Matches(a,rule)))continue;
                var name=rule.ApplicationName??Path.GetFileNameWithoutExtension(rule.ApplicationPath??"");
                result.Add(new(0,name,rule.Name){ExecutablePath=rule.ApplicationPath,PackageFamily=rule.ApplicationPackage,AppUserModelId=rule.ApplicationId});
            }
            return Merge(result);
        }
    }
    public static bool Matches(ApplicationAudioSource app,AudioChannel rule)=>
        !string.IsNullOrEmpty(rule.ApplicationId)?string.Equals(rule.ApplicationId,app.AppUserModelId,StringComparison.OrdinalIgnoreCase):
        !string.IsNullOrEmpty(rule.ApplicationPackage)?string.Equals(rule.ApplicationPackage,app.PackageFamily,StringComparison.OrdinalIgnoreCase):
        !string.IsNullOrEmpty(rule.ApplicationPath)?string.Equals(rule.ApplicationPath,app.ExecutablePath,StringComparison.OrdinalIgnoreCase):
        string.Equals(rule.ApplicationName,app.Name,StringComparison.OrdinalIgnoreCase);
    public static ApplicationAudioSource[] Merge(IEnumerable<ApplicationAudioSource> sources)=>sources
        .GroupBy(a=>a.Identity,StringComparer.OrdinalIgnoreCase)
        .Select(g=>g.OrderByDescending(a=>a.Running).ThenByDescending(a=>a.HasWindow).ThenBy(a=>a.Started).ThenBy(a=>a.ProcessId).First())
        .OrderByDescending(a=>a.Running).ThenBy(a=>a.Label,StringComparer.OrdinalIgnoreCase).ToArray();
    public static ApplicationAudioSource FromFile(string path)
    {
        path=Path.GetFullPath(path);
        if(!File.Exists(path)||!Path.GetExtension(path).Equals(".exe",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Choisissez un programme .exe existant.");
        string name=Path.GetFileNameWithoutExtension(path),label=name;
        try{label=FileVersionInfo.GetVersionInfo(path).FileDescription??name;}catch{}
        return new(0,name,string.IsNullOrWhiteSpace(label)?name:label){ExecutablePath=path};
    }
    private static ApplicationAudioSource[] Scan(AudioChannel[] saved)
    {
        var windows=new HashSet<int>();EnumWindows((w,_)=>{if(IsWindowVisible(w)){GetWindowThreadProcessId(w,out var pid);windows.Add(pid);}return true;},IntPtr.Zero);
        ApplicationAudioSource[] audioSources=[];try{audioSources=ApplicationAudioCatalog.List().ToArray();}catch(System.Runtime.InteropServices.COMException){}
        var audio=audioSources.Select(a=>a.ProcessId).ToHashSet();
        using var self=Process.GetCurrentProcess();int session=self.SessionId;
        var list=new List<ApplicationAudioSource>();string windowsFolder=Environment.GetFolderPath(Environment.SpecialFolder.Windows)+Path.DirectorySeparatorChar;
        foreach(var process in Process.GetProcesses())using(process)
        {
            try
            {
                if(process.Id==Environment.ProcessId||process.SessionId!=session||process.ProcessName.Equals("MyVoice",StringComparison.OrdinalIgnoreCase))continue;
                var handle=OpenProcess(0x1000,false,process.Id);if(handle==IntPtr.Zero)continue;
                try
                {
                    int length=32768;var path=new StringBuilder(length);if(!QueryFullProcessImageName(handle,0,path,ref length))continue;
                    string exe=path.ToString(),name=process.ProcessName;bool visible=windows.Contains(process.Id),hasAudio=audio.Contains(process.Id);
                    bool helper=new[]{"crash","update","helper","service","renderer","msedgewebview","conhost","dllhost","runtimebroker"}.Any(x=>name.Contains(x,StringComparison.OrdinalIgnoreCase));
                    bool configured=saved.Any(c=>c.Type=="application"&&(string.Equals(c.ApplicationPath,exe,StringComparison.OrdinalIgnoreCase)||string.Equals(c.ApplicationName,name,StringComparison.OrdinalIgnoreCase)));
                    bool knownTray=new[]{"discord","spotify","voicemod","chrome","msedge","firefox","obs64","steam","epicgameslauncher"}.Contains(name,StringComparer.OrdinalIgnoreCase);
                    bool systemHost=name is "TextInputHost" or "ShellExperienceHost" or "StartMenuExperienceHost" or "SearchHost";
                    if(!hasAudio&&!configured&&(systemHost||helper||(!visible&&!knownTray)))continue;
                    uint size=0;string? family=null,id=null;
                    if(GetPackageFamilyName(handle,ref size,null)==122&&size<1024){var text=new StringBuilder((int)size);if(GetPackageFamilyName(handle,ref size,text)==0)family=text.ToString();}
                    size=0;if(GetApplicationUserModelId(handle,ref size,null)==122&&size<2048){var text=new StringBuilder((int)size);if(GetApplicationUserModelId(handle,ref size,text)==0)id=text.ToString();}
                    string label=name;try{label=FileVersionInfo.GetVersionInfo(exe).FileDescription??name;}catch{}
                    long started=long.MaxValue;try{started=process.StartTime.Ticks;}catch{}
                    list.Add(new(process.Id,name,string.IsNullOrWhiteSpace(label)?name:label){ExecutablePath=exe,PackageFamily=family,AppUserModelId=id,HasWindow=visible,HasAudio=hasAudio,Started=started});
                }
                finally{CloseHandle(handle);}
            }
            catch(Exception e)when(e is InvalidOperationException or System.ComponentModel.Win32Exception or ArgumentException){}
        }
        foreach(var source in audioSources)if(!list.Any(a=>a.ProcessId==source.ProcessId||a.Name.Equals(source.Name,StringComparison.OrdinalIgnoreCase)))list.Add(source with{Label=source.Name,HasAudio=true});
        return Merge(list);
    }
}
