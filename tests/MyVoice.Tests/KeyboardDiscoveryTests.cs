using MyVoice.Audio;
using MyVoice.Core;
using MyVoice.Infrastructure;
internal static class KeyboardDiscoveryTests
{
    private static void Check(bool ok){if(!ok)throw new Exception("Keyboard/discovery assertion failed");}
    internal static void Run(Action<string,Action> test)
    {
        var dir=new DirectoryInfo(AppContext.BaseDirectory);while(dir!=null&&!Directory.Exists(Path.Combine(dir.FullName,"assets","KeyboardPacks")))dir=dir.Parent;
        var root=Path.Combine(dir?.FullName??throw new Exception("Keyboard test packs missing"),"assets","KeyboardPacks");
        test("Discovery merges silent windows, multiprocess audio and saved identities",()=>
        {
            ApplicationAudioSource[] data=[new(40,"chrome","Chrome"){ExecutablePath=@"C:\Apps\Chrome.exe",HasWindow=true},new(42,"chrome","Chrome"){ExecutablePath=@"c:\apps\chrome.exe",HasAudio=true},new(0,"chrome","Chrome"){ExecutablePath=@"C:\Apps\Chrome.exe"},new(51,"Discord","Discord"){ExecutablePath=@"C:\Apps\Discord.exe",HasWindow=true},new(0,"Spotify","Spotify"){ExecutablePath=@"C:\Apps\Spotify.exe"}];
            var merged=ApplicationDiscoveryService.Merge(data);Check(merged.Length==3&&merged[0].Running&&merged[^1].Name=="Spotify"&&merged.Single(a=>a.Name=="chrome").ProcessId==40);
            Check(ApplicationDiscoveryService.Matches(merged[0],new(){ApplicationPath=merged[0].ExecutablePath}));
            Check(!ApplicationDiscoveryService.Matches(merged[0],new(){ApplicationPath=@"C:\Other\Chrome.exe",ApplicationName="chrome"}));
        });
        test("Packaged application identity survives executable update paths",()=>
        {
            var app=new ApplicationAudioSource(1,"Spotify","Spotify"){AppUserModelId="Spotify_family!App",ExecutablePath=@"C:\v2\Spotify.exe"};
            Check(ApplicationDiscoveryService.Matches(app,new(){ApplicationId="Spotify_family!App",ApplicationPath=@"C:\v1\Spotify.exe"}));
        });
        test("Saved silent executable is detected on launch and returns offline after exit",()=>
        {
            string temporary=Path.Combine(Path.GetTempPath(),"MyVoice-discovery-fixture-"+Guid.NewGuid());Directory.CreateDirectory(temporary);foreach(var file in Directory.GetFiles(AppContext.BaseDirectory))File.Copy(file,Path.Combine(temporary,Path.GetFileName(file)));
            string executable=Path.Combine(temporary,"MyVoice.Tests.exe");var selected=ApplicationDiscoveryService.FromFile(executable);
            var rule=new AudioChannel{Name="Silent test fixture",Type="application",ApplicationName=selected.Name,ApplicationPath=selected.ExecutablePath,CaptureEnabled=true};
            using var child=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(executable,"--discovery-child"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true})!;
            try{var discovered=ApplicationDiscoveryService.Shared.Discover([rule],true);Check(discovered.Any(a=>a.ProcessId==child.Id&&ApplicationDiscoveryService.Matches(a,rule)));}
            finally{child.StandardInput.WriteLine();child.WaitForExit(5000);}
            var offline=ApplicationDiscoveryService.Shared.Discover([rule],true);
            Check(offline.Any(a=>!a.Running&&ApplicationDiscoveryService.Matches(a,rule)));
        });
        test("Five recorded keyboard packs have bounded PCM, down/up and differentiated output",()=>
        {
            var catalog=KeyboardSoundService.Catalog(root);Check(catalog.Length==5);var signatures=new HashSet<string>();using var engine=new KeyboardSoundService();
            foreach(var pack in catalog)
            {
                engine.Load(root,new(){Enabled=true,Pack=pack.Id});Check(engine.LoadedBytes is >0 and <8*1024*1024);
                Check(pack.Samples.Values.All(v=>v.Length>=4));
                var audio=new float[960];engine.Trigger(KeyboardKeyKind.Space,false);engine.Render(audio);Check(audio.Any(x=>x!=0)&&audio.All(x=>float.IsFinite(x)&&Math.Abs(x)<=.9));signatures.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(audio.AsSpan()))));
                engine.Disable();Check(engine.LoadedBytes==0&&engine.ActiveVoices==0);engine.Render(audio);Check(audio.All(x=>x==0));
            }
            Check(signatures.Count==5);
        });
        test("Keyboard rapid typing and held chords keep polyphony and allocations bounded",()=>
        {
            using var engine=new KeyboardSoundService();engine.Load(root,new(){Enabled=true,Pack="blue",Volume=2});var audio=new float[960];long start=GC.GetAllocatedBytesForCurrentThread();var watch=System.Diagnostics.Stopwatch.StartNew();
            for(int block=0;block<6000;block++){if(block%10==0)for(int k=0;k<4;k++)engine.Trigger((KeyboardKeyKind)(k%7),false);if(block%10==5)for(int k=0;k<4;k++)engine.Trigger((KeyboardKeyKind)(k%7),true);engine.Render(audio);Check(engine.ActiveVoices<=32&&audio.All(x=>float.IsFinite(x)&&Math.Abs(x)<=.9));}
            Check(GC.GetAllocatedBytesForCurrentThread()-start<10*1024*1024);Check(watch.Elapsed.TotalSeconds<10);
            for(int i=0;i<60;i++)engine.Render(audio);Check(engine.ActiveVoices==0);
        });
        test("Keyboard monitor default isolates virtual microphone; explicit route sends both",()=>
        {
            var profile=MixerState.Recommended(new());profile=profile with{Channels=profile.Channels.Append(new(){Id="keyboard",Type="keyboard",Name="Keyboard Sound"}).ToList(),Routes=profile.Routes.Append(new("keyboard","monitor")).ToList()};MixerStore.Validate(profile);
            var router=new MixerRouter();router.Apply(profile);var zero=new float[960];var mic=new float[480];var key=Enumerable.Repeat(.1f,960).ToArray();
            for(int i=0;i<20;i++)router.Render(mic,zero,zero,zero,keyboard:key);Check(router.Bus("monitor").ToArray().Any(x=>x!=0)&&router.Bus("virtual").ToArray().All(x=>x==0));
            router.Apply(profile with{Routes=profile.Routes.Append(new("keyboard","virtual")).ToList()});for(int i=0;i<20;i++)router.Render(mic,zero,zero,zero,keyboard:key);Check(router.Bus("monitor").ToArray().Any(x=>x!=0)&&router.Bus("virtual").ToArray().Any(x=>x!=0));
        });
        test("Application identity and keyboard profile round-trip without capture activation on export",()=>
        {
            var temp=Path.Combine(Path.GetTempPath(),"MyVoice-keyboard-tests-"+Guid.NewGuid());Directory.CreateDirectory(temp);
            var profile=MixerState.Recommended(new()) with{Keyboard=new(){Enabled=true,Pack="blue",Pitch=2}};
            profile=profile with{Channels=profile.Channels.Append(new(){Name="Closed App",Type="application",ApplicationName="closed",ApplicationPath=@"C:\Apps\closed.exe",ApplicationId="Family!App",CaptureEnabled=true}).ToList()};
            var store=new MixerStore(temp);store.Save(new(){Current=profile});var loaded=store.Load(new());Check(loaded.Current.Keyboard.Pack=="blue"&&loaded.Current.Keyboard.Enabled&&loaded.Current.Channels.Last().ApplicationPath==@"C:\Apps\closed.exe");
            string file=Path.Combine(temp,"profile.json");MixerStore.Export(file,profile);var imported=MixerStore.Import(file);Check(!imported.Keyboard.Enabled&&imported.Channels.Last().ApplicationPath==null&&imported.Channels.Last().ApplicationId==null&&!imported.Channels.Last().CaptureEnabled);
        });
    }
}
