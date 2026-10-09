using System.IO.Compression;
using System.Text.Json;
using MyVoice.Core;
using MyVoice.Audio;
using MyVoice.Updater;
using MyVoice.Infrastructure;
static class ProductTests
{
 public static void Run()
 {
  var root=Path.Combine(Path.GetTempPath(),"MyVoice-product-tests-"+Guid.NewGuid());Directory.CreateDirectory(Path.Combine(root,"config"));Directory.CreateDirectory(Path.Combine(root,"soundboards"));
  void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
  void Reject(Action action,string name){try{action();throw new Exception("accepted");}catch(InvalidDataException){Console.WriteLine("PASS "+name);}}
  File.WriteAllText(Path.Combine(root,"config","config.json"),"{\"GainDb\":3.5,\"UnknownFutureField\":true}");
  File.WriteAllText(Path.Combine(root,"soundboards","library.json"),"{\"Sounds\":[{\"Name\":\"Existing\"}],\"Folders\":[]}");
  var before=File.ReadAllText(Path.Combine(root,"config","config.json"));var store=new ProductStore(root);var state=store.Load();
  Check(File.Exists(Path.Combine(root,"migration-backup","product-v1","config","config.json")),"Product migration backs up old metadata before activation");
  state.Theme="Deep Ocean";store.Save(state);Check(store.Load().Theme=="Deep Ocean"&&File.ReadAllText(Path.Combine(root,"config","config.json"))==before,"New preferences preserve original audio settings and unknown fields");
  var voice=new DesignedVoice{Name="Custom",Pitch=1.25,Delay=.2};var package=Path.Combine(root,"preset.zip");CreationPackage.ExportPreset(package,voice);var unpack=CreationPackage.Extract(package,Path.Combine(root,"installed"));Check(unpack.Manifest.Preset!.Pitch==1.25,"Voice preset package round trip");
  foreach(var path in new[]{"../escape.json","/root.json","C:/escape.json","files\\escape.json","safe/../../bad.json","CON.wav","trailing./sound.wav"}){
   var file=Path.Combine(root,Guid.NewGuid()+".zip");using(var z=ZipFile.Open(file,ZipArchiveMode.Create)){using var writer=new StreamWriter(z.CreateEntry(path).Open());writer.Write("{}");}Reject(()=>CreationPackage.Inspect(file),"Package rejects unsafe path "+path);
  }
  foreach(var extension in new[]{".exe",".dll",".bat",".ps1",".js"}){var file=Path.Combine(root,Guid.NewGuid()+".zip");using(var z=ZipFile.Open(file,ZipArchiveMode.Create)){z.CreateEntry("payload"+extension);}Reject(()=>CreationPackage.Inspect(file),"Package refuses executable "+extension);}
  var duplicate=Path.Combine(root,"duplicate.zip");using(var z=ZipFile.Open(duplicate,ZipArchiveMode.Create)){z.CreateEntry("cover.png");z.CreateEntry("Cover.png");}Reject(()=>CreationPackage.Inspect(duplicate),"Package rejects Windows case-colliding files");
  Check(AccountRules.Username("SomeName")=="somename","Username canonicalization");Reject(()=>AccountRules.Username("ADMIN"),"Reserved username rejected locally");Check(AccountRules.PasswordScore("password123456")==0&&AccountRules.PasswordScore("Une longue phrase de passe originale")>=2,"Weak password refused and passphrase accepted");
  var vault=new SessionVault(root);vault.Save(new("secret-access-token","secret-refresh-token","sample-user",DateTimeOffset.UtcNow.AddHours(1)));Check(vault.Load()?.RefreshToken=="secret-refresh-token"&&!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(root,"config","community.session"))).Contains("secret-"),"DPAPI session round trip without plaintext tokens");vault.Clear();Check(vault.Load()==null,"Logout clears stored session");
  Check(ReleaseVersion.IsNewer("2.3.0","v2.3.0-beta.2","2.3.0-beta.1"),"Beta update sequence advances within same numeric version");
  Check(ReleaseVersion.IsNewer("2.3.0","v2.3.0","2.3.0-beta.2"),"Stable replaces same-version beta");
  Check(!ReleaseVersion.IsNewer("2.3.0","v2.3.0-beta.3","2.3.0"),"Beta never downgrades same-version stable");
  var beta=new ReleaseManifest("2.3.0", "https://github.com/mattheube/MyVoice/releases/download/v2.3.0-beta.1/MyVoiceSetup.exe",new string('0',64)){Tag="v2.3.0-beta.1",Channel="beta",ReleaseUrl="https://github.com/mattheube/MyVoice/releases/tag/v2.3.0-beta.1"};
  Check(UpdateService.Validate(beta,DistributionDefaults.BetaManifestUrl).Channel=="beta","Beta manifest accepts official matching tag");
  Reject(()=>UpdateService.Validate(beta,DistributionDefaults.ManifestUrl),"Stable source refuses beta manifest");
  var dry=Enumerable.Range(0,480).Select(i=>(float)(Math.Sin(i*.2)*.15)).ToArray();var processor=new VoiceProcessor{Settings=new(){Gate=false,NoiseSuppression=0,Compressor=false},Designed=voice};var output=(float[])dry.Clone();processor.Process(output);Check(output.All(float.IsFinite),"Designed voice processing produces finite audio");processor.Enabled=false;output=(float[])dry.Clone();processor.Process(output);Check(output.Zip(dry,(a,b)=>Math.Abs(a-b)).Max()<.00001,"Voice off bypasses designed effects");
 }
}
