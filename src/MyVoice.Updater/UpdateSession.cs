namespace MyVoice.Updater;
public enum UpdatePhase { Checking, Downloading, Verifying, Current, Ready, Failed, Cancelled }
public sealed record UpdateSnapshot(UpdatePhase Phase, string Message, double? Percent=null);
public sealed class UpdateSession
{
    public ReleaseManifest? Release {get;private set;}
    public string Source {get;private set;}="";
    public string? DownloadedFile {get;private set;}
    public DateTime? CheckedAt {get;private set;}
    public UpdatePhase? FailedAt {get;private set;}
    public UpdateSnapshot State {get;private set;}=new(UpdatePhase.Checking,"Vérification des mises à jour…");
    public async Task RunAsync(string source,string installed,string cache,IProgress<UpdateSnapshot> progress,Action<string> log,CancellationToken token)
    {
        Source=source;
        void Report(UpdateSnapshot value){State=value;progress.Report(value);}
        try
        {
            log("Update installed="+installed+" source="+source);Report(State);
            using var service=new UpdateService();Release=await service.ReadManifestAsync(source,token);CheckedAt=DateTime.UtcNow;
            log("Update release="+Release.Tag+" version="+Release.Version);
            if(!ReleaseVersion.IsNewer(Release.Version,Release.Tag,installed)){Report(new(UpdatePhase.Current,"MyVoice est à jour ✓"));return;}
            var numeric=installed.Split('-')[0];
            if(Version.Parse(numeric)<Version.Parse(Release.MinimumVersion))throw new InvalidDataException("Version intermédiaire requise : "+Release.MinimumVersion);
            Report(new(UpdatePhase.Downloading,"Téléchargement de MyVoice "+Release.Tag));
            if(!string.IsNullOrEmpty(Release.PackageUrl))
                DownloadedFile=await service.DownloadPackageAsync(Release,source,cache,token,new InlineProgress<DownloadProgress>(p=>Report(new(UpdatePhase.Downloading,$"MyVoice {Release.Tag} · {p.Received/1048576d:0.0} Mo"+(p.Total>0?$" / {p.Total/1048576d:0.0} Mo":""),p.Total>0?p.Received*100d/p.Total:null))),()=>{log("Update verifying package SHA-256");Report(new(UpdatePhase.Verifying,"Vérification du téléchargement…"));});
            else DownloadedFile=await service.DownloadAsync(Release,source,cache,token,null,
                new InlineProgress<DownloadProgress>(p=>Report(new(UpdatePhase.Downloading,$"MyVoice {Release.Tag} · {p.Received/1048576d:0.0} Mo"+(p.Total>0?$" / {p.Total/1048576d:0.0} Mo":""),p.Total>0?p.Received*100d/p.Total:null))),
                ()=>{log("Update verifying SHA-256");Report(new(UpdatePhase.Verifying,"Vérification du téléchargement…"));});
            log("Update SHA-256 verified; staged="+DownloadedFile);
            Report(new(UpdatePhase.Ready,"Mise à jour vérifiée · prête à installer"));
        }
        catch(OperationCanceledException){log("Update cancelled or timed out");Report(new(UpdatePhase.Cancelled,"Vérification interrompue · démarrage de la version actuelle"));}
        catch(Exception e){FailedAt=State.Phase;log("Update failed: "+e.Message);Report(new(UpdatePhase.Failed,"Mise à jour indisponible : "+e.Message));}
    }
    private sealed class InlineProgress<T>(Action<T> action):IProgress<T>{public void Report(T value)=>action(value);}
}
