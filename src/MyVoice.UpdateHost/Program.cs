using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MyVoice.Updater;
namespace MyVoice.UpdateHost;
internal static class Program
{
    [STAThread]public static void Main(string[] args)
    {
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        var window=new Window{Title="MyVoice · Mise à jour",Width=480,Height=240,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=new SolidColorBrush(Color.FromRgb(16,17,23))};
        var panel=new StackPanel{Margin=new Thickness(30)};var status=new TextBlock{Text="Préparation de la mise à jour…",Foreground=Brushes.WhiteSmoke,FontSize=18,TextWrapping=TextWrapping.Wrap};var progress=new ProgressBar{Height=6,Margin=new Thickness(0,24,0,0),IsIndeterminate=true};panel.Children.Add(status);panel.Children.Add(progress);window.Content=panel;
        bool finished=false;
        app.Startup+=async(_,_)=>
        {
            window.Show();
            try{if(args.Length!=1)throw new ArgumentException("Transaction requise");await Run(args[0],p=>app.Dispatcher.Invoke(()=>{status.Text=p.Stage;progress.IsIndeterminate=p.Total<=0;if(p.Total>0)progress.Value=p.Completed*100d/p.Total;}));}
            catch(Exception e){status.Text="La mise à jour n’a pas pu aboutir : "+e.Message;await Task.Delay(1800);}
            finished=true;app.Shutdown();
        };
        window.Closing+=(_,e)=>{if(!finished)e.Cancel=true;};app.Run();
    }
    private static async Task Run(string jobPath,Action<InstallProgress> report)
    {
        var directory=UpdateHandoff.ValidateJobPath(jobPath);var job=JsonSerializer.Deserialize<UpdateJob>(File.ReadAllText(jobPath))??throw new InvalidDataException("Transaction vide");
        UpdateTransaction? transaction=null;Process? child=null;bool accepted=false;
        using var installationLock=new Mutex(false,UpdateHandoff.MutexName(job.Target));
        bool ownsLock=false;
        void Log(string text){try{File.AppendAllText(Path.Combine(directory,"update.log"),DateTime.UtcNow.ToString("O")+" "+text+Environment.NewLine);}catch{ /* Diagnostics must never prevent recovery. */ }}
        try
        {
            try{ownsLock=installationLock.WaitOne(0);}catch(AbandonedMutexException){ownsLock=true;}
            if(!ownsLock)throw new IOException("Une mise à jour est déjà en cours.");
            if(job.Source!=DistributionDefaults.ManifestUrl&&job.Source!=DistributionDefaults.BetaManifestUrl)throw new InvalidDataException("Source non officielle");
            UpdateService.Validate(job.Release,job.Source);
            using var parent=Process.GetProcessById(job.ParentId);
            if(parent.StartTime.ToUniversalTime().Ticks!=job.ParentStarted||!string.Equals(parent.MainModule?.FileName,Path.Combine(job.Target,"MyVoice.exe"),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("L’application d’origine ne correspond pas à cette transaction");
            // Re-fetch the immutable release manifest; a mutable local job cannot authorize arbitrary binaries.
            using var service=new UpdateService();var published=await service.ReadManifestAsync("https://github.com/"+DistributionDefaults.Repository+"/releases/download/"+job.Release.Tag+"/manifest.json",CancellationToken.None);
            UpdateService.Validate(published,job.Source);
            if(published.PackageSha256!=job.Release.PackageSha256||published.PackageUrl!=job.Release.PackageUrl||published.Tag!=job.Release.Tag)throw new InvalidDataException("Le package ne correspond plus à la release officielle");
            await UpdateService.VerifyPackageAsync(job.Package,published.PackageSha256,CancellationToken.None);
            if(File.Exists(Path.Combine(directory,"cancelled")))return;
            File.WriteAllText(Path.Combine(directory,"accepted"),job.Token);accepted=true;Log("Accepted "+published.Tag);
            report(new("Fermeture de MyVoice…",0,0));using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60)))await parent.WaitForExitAsync(timeout.Token);
            if(File.Exists(Path.Combine(directory,"cancelled")))return;
            transaction=new UpdateTransaction(job.Target,directory);
            await Task.Run(()=>transaction.PrepareAsync(job.Package,published.PackageSha256,report,CancellationToken.None));
            var assembly=System.Reflection.AssemblyName.GetAssemblyName(Path.Combine(transaction.Stage,"MyVoice.dll"));
            if(assembly.Version?.ToString(3)!=published.Version)throw new InvalidDataException("Version des fichiers programme incorrecte");
            Log("Package prepared");transaction.Commit();Log("Program files replaced");report(new("Redémarrage de MyVoice…",0,0));
            child=StartApp(job,jobPath,false);
            var until=DateTime.UtcNow.AddSeconds(120);bool healthy=false;
            while(DateTime.UtcNow<until)
            {
                if(File.Exists(Path.Combine(directory,"healthy"))&&File.ReadAllText(Path.Combine(directory,"healthy"))==job.Token){healthy=true;break;}
                if(child.HasExited)break;
                await Task.Delay(200);
            }
            if(!healthy)throw new IOException("La nouvelle version n’a pas confirmé son démarrage.");
            transaction.Complete();UpdateHandoff.WriteReceipt(directory,new("completed",published.Tag,"MyVoice a été mis à jour.",transaction.Backup));Log("Startup confirmed; backup preserved");
            if(!job.SmokeTest)try{File.WriteAllText(Path.Combine(UpdateHandoff.Root,"failed-release.txt"),"");}catch(Exception markerError){Log(markerError.Message);}
        }
        catch(Exception e)
        {
            Log("Failure: "+e);
            bool recovered=true;
            try
            {
                if(child is {HasExited:false}){child.Kill();using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(15));await child.WaitForExitAsync(stop.Token);}
                if(transaction!=null){transaction.Rollback();Log("Previous program restored");}
            }
            catch(Exception recoveryError){recovered=false;Log("Recovery failed; previous files preserved at "+transaction?.Backup+": "+recoveryError);}
            try{if(!job.SmokeTest&&transaction!=null){Directory.CreateDirectory(UpdateHandoff.Root);File.WriteAllText(Path.Combine(UpdateHandoff.Root,"failed-release.txt"),job.Release.Tag);}}catch(Exception markerError){Log(markerError.Message);}
            try{UpdateHandoff.WriteReceipt(directory,new("failed",job.Release.Tag,e.Message+(recovered?"":" · Restauration incomplète ; sauvegarde : "+transaction?.Backup),transaction?.Backup??""));}catch(Exception receiptError){Log(receiptError.Message);}
            if(accepted&&recovered)
            {
                // The parent may have timed out while saving. Never start a second copy in that case.
                bool parentAlive;try{using var parent=Process.GetProcessById(job.ParentId);parentAlive=!parent.HasExited&&parent.StartTime.ToUniversalTime().Ticks==job.ParentStarted;}catch{parentAlive=false;}
                if(!parentAlive&&File.Exists(Path.Combine(job.Target,"MyVoice.exe")))StartApp(job,jobPath,true).Dispose();
            }
            throw;
        }
        finally{child?.Dispose();if(ownsLock)installationLock.ReleaseMutex();}
    }
    private static Process StartApp(UpdateJob job,string jobPath,bool rollback)
    {
        var start=new ProcessStartInfo(Path.Combine(job.Target,"MyVoice.exe")){WorkingDirectory=job.Target,UseShellExecute=false};
        start.ArgumentList.Add(rollback?"--update-rollback":"--update-job");start.ArgumentList.Add(jobPath);
        if(job.SmokeTest){start.ArgumentList.Add("--smoke-test");start.ArgumentList.Add(Path.Combine(Path.GetDirectoryName(jobPath)!,"smoke"));}
        return Process.Start(start)??throw new IOException("Impossible de relancer MyVoice");
    }
}
