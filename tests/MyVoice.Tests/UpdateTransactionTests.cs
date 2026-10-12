using System.IO.Compression;
using System.Security.Cryptography;
using MyVoice.Updater;
public static class UpdateTransactionTests
{
    public static async Task Run()
    {
        var root=Path.Combine(Path.GetTempPath(),"MyVoice-transaction-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
        string Target(string name){var p=Path.Combine(root,name);Directory.CreateDirectory(p);File.WriteAllText(Path.Combine(p,"MyVoice.exe"),"old executable");File.WriteAllText(Path.Combine(p,"MyVoice.dll"),"old dll");File.WriteAllText(Path.Combine(p,"personal.txt"),"keep me");return p;}
        string Package(string name,Action<ZipArchive>? extra=null)
        {
            var p=Path.Combine(root,name+".zip");using(var zip=ZipFile.Open(p,ZipArchiveMode.Create))
            {
                foreach(var file in new[]{"MyVoice.exe","MyVoice.dll","MyVoice.runtimeconfig.json","new-file.dll"}){using var writer=new StreamWriter(zip.CreateEntry(file).Open());writer.Write("new "+file);}
                extra?.Invoke(zip);
            }return p;
        }
        string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        async Task Reject(string package,string digest,string label)
        {
            var target=Target(Guid.NewGuid().ToString("N"));var t=new UpdateTransaction(target,Path.Combine(root,Guid.NewGuid().ToString("N")));
            try{await t.PrepareAsync(package,digest,null,CancellationToken.None);throw new Exception("Accepted invalid package");}
            catch(InvalidDataException){Check(File.ReadAllText(Path.Combine(target,"MyVoice.exe"))=="old executable",label);}
        }
        var good=Package("good");var target=Target("installed");var appdata=Path.Combine(root,"AppData");Directory.CreateDirectory(appdata);File.WriteAllText(Path.Combine(appdata,"voices.json"),"user voices");
        var tx=new UpdateTransaction(target,Path.Combine(root,"job"));var steps=new List<InstallProgress>();await tx.PrepareAsync(good,Hash(good),steps.Add,CancellationToken.None);
        Check(File.ReadAllText(Path.Combine(target,"MyVoice.exe"))=="old executable","Preparation never mutates current installation");
        tx.Commit();Check(File.ReadAllText(Path.Combine(target,"MyVoice.exe"))=="new MyVoice.exe"&&File.ReadAllText(Path.Combine(tx.Backup,"MyVoice.exe"))=="old executable","Commit installs new files and retains exact previous executable");
        Check(File.ReadAllText(Path.Combine(target,"personal.txt"))=="keep me"&&File.ReadAllText(Path.Combine(appdata,"voices.json"))=="user voices","Update preserves extra local files and external user data");
        tx.Rollback();Check(File.ReadAllText(Path.Combine(target,"MyVoice.exe"))=="old executable"&&!File.Exists(Path.Combine(target,"new-file.dll")),"Failed startup rollback restores previous files and removes new-only binaries");
        Check(steps.All(x=>x.Completed>0&&x.Completed<=x.Total),"Install progress counts real copied and extracted entries");
        await Reject(good,new string('0',64),"Wrong package hash leaves installed version intact");
        var traversal=Package("traversal",z=>z.CreateEntry("../outside.txt"));await Reject(traversal,Hash(traversal),"ZIP traversal rejected before installation");Check(!File.Exists(Path.Combine(root,"outside.txt")),"Archive cannot write outside staging directory");
        var duplicate=Package("duplicate",z=>z.CreateEntry("MYVOICE.EXE"));await Reject(duplicate,Hash(duplicate),"Case-insensitive duplicate package entry rejected");
        var link=Package("link",z=>{var e=z.CreateEntry("link");e.ExternalAttributes=unchecked((int)0xA1FF0000);});await Reject(link,Hash(link),"Symbolic link package entry rejected");
        var ads=Package("ads",z=>z.CreateEntry("payload:stream"));await Reject(ads,Hash(ads),"NTFS alternate stream entry rejected");
        var lockedTarget=Target("locked");var locked=new UpdateTransaction(lockedTarget,Path.Combine(root,"locked-job"));
        using(var held=new FileStream(Path.Combine(lockedTarget,"MyVoice.exe"),FileMode.Open,FileAccess.Read,FileShare.None))
        {try{await locked.PrepareAsync(good,Hash(good),null,CancellationToken.None);throw new Exception("Locked file copied");}catch(IOException){Console.WriteLine("PASS File lock aborts preparation without replacing program");}}
        Check(File.ReadAllText(Path.Combine(lockedTarget,"MyVoice.exe"))=="old executable","File-lock failure leaves previous executable intact");
        var completeTarget=Target("complete");var complete=new UpdateTransaction(completeTarget,Path.Combine(root,"complete-job"));await complete.PrepareAsync(good,Hash(good),null,CancellationToken.None);complete.Commit();complete.Complete();
        Check(File.ReadAllText(complete.Journal).Contains("completed")&&Directory.Exists(complete.Backup),"Successful update journals completion and preserves backup");
        var official=new ReleaseManifest("2.6.0","",""+new string('A',64)){Tag="v2.6.0-beta.1",Channel="beta",DownloadUrl="https://github.com/mattheube/MyVoice/releases/download/v2.6.0-beta.1/MyVoiceSetup.exe",ReleaseUrl="https://github.com/mattheube/MyVoice/releases/tag/v2.6.0-beta.1",PackageUrl="https://github.com/other/repo/releases/download/v2.6.0-beta.1/package.zip",PackageSha256=new string('B',64)};
        try{UpdateService.Validate(official,DistributionDefaults.BetaManifestUrl);throw new Exception("Accepted nonofficial package");}catch(InvalidDataException){Console.WriteLine("PASS Automatic packages must belong to the official release");}
    }
}
