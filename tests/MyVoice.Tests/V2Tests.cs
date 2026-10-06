using MyVoice.Core;
using MyVoice.AI;
using MyVoice.Infrastructure;
using MyVoice.Audio;
using System.Security.Cryptography;
using System.Text.Json;
using NAudio.Wave;
public static class V2Tests
{
    public static void Run()
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS V2 "+name);}
        var physical=new AudioDevice("mic","Razer Microphone");var virtualMic=new AudioDevice("vm","Microphone (Voicemod)");var cable=new AudioDevice("c","CABLE Input (VB-Audio)");
        Check(DeviceSelection.Physical([virtualMic,physical],null)==physical,"physical default avoids virtual feedback");
        Check(DeviceSelection.Physical([new("","—"),physical],"")==physical,"empty legacy output selects physical default");
        Check(DeviceSelection.Physical([physical],"missing")==physical,"temporary input fallback");
        Check(DeviceSelection.Virtual([cable],"missing")==cable,"single cable auto selected");
        Check(DeviceSelection.Virtual([cable,new("c2","CABLE-B Input")],null)==null,"ambiguous cable requires selection");
        Check(DeviceSelection.Virtual([cable,new("c2","CABLE-B Input")],"c")==cable,"endpoint ID persists across ambiguity");
        var root=Path.Combine(Path.GetTempPath(),"MyVoice-V2-"+Guid.NewGuid());var store=new LocalStore(root);
        var path=Path.Combine(root,"config","config.json");File.WriteAllText(path,"{\"ConfigVersion\":1,\"GainDb\":4,\"FutureSetting\":{\"Keep\":true},\"InputDeviceId\":\"missing\",\"Shortcuts\":{\"sound:x\":{\"Modifiers\":2,\"Key\":65}}}");
        var backup=UserDataBackup.EnsureV2Backup(root);var settings=store.Load();store.Save(settings);
        Check(File.ReadAllText(path).Contains("FutureSetting")&&settings.Shortcuts.Count==1,"unknown fields and shortcuts preserved");
        Check(File.Exists(Path.Combine(backup,"config","config.json")),"pre-upgrade backup exists");
        var before=File.ReadAllText(Path.Combine(backup,"config","config.json"));UserDataBackup.EnsureV2Backup(root);
        Check(before==File.ReadAllText(Path.Combine(backup,"config","config.json")),"backup is not overwritten on relaunch");
        var voice=new VoiceReference{Name="Test"};
        for(int k=0;k<10;k++)
        {
            var file=Path.Combine(root,$"source{k}.wav");using(var writer=new WaveFileWriter(file,WaveFormat.CreateIeeeFloatWaveFormat(44100,2))){var data=Enumerable.Range(0,44100*3*2).Select(i=>(float)(.08*Math.Sin(2*Math.PI*240*(i/2)/44100))).ToArray();writer.WriteSamples(data,0,data.Length);}
            voice.Samples.Add(DatasetService.Import(root,voice.Id,file,CancellationToken.None));
        }
        Check(voice.Samples.Count==10&&voice.DatasetDuration>29.9,"all ten files and 30 seconds analyzed");
        Check(voice.Samples.All(s=>s.Segments.Count>0&&File.Exists(s.OriginalFile)),"all original samples retained with segments");
        DatasetService.BuildReference(root,voice);var old=voice.ReferenceFile;var hash=SHA256.HashData(File.ReadAllBytes(old));
        voice.Samples[0].Excluded=true;DatasetService.BuildReference(root,voice);
        Check(voice.ReferenceFile!=old&&hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(old)))&&voice.Revisions.Count==2,"prepare creates new version without overwriting previous");
        var json=JsonSerializer.Serialize(voice);var copy=JsonSerializer.Deserialize<VoiceReference>(json)!;
        Check(copy.Samples.Count==10&&copy.Samples[0].Excluded&&copy.Revisions.Count==2,"dataset exclusions and revisions persist");
        var p=new VoiceProcessor{Settings=new ProcessingSettings{Gate=false,NoiseSuppression=0,Compressor=false,BassDb=12},Preset="Robot"};
        var samples=Enumerable.Range(0,480).Select(i=>(float)(.05*Math.Sin(i*.04))).ToArray();var original=samples.ToArray();p.Process(samples,true,false,false);
        Check(samples.SequenceEqual(original),"AI preprocessing excludes EQ and character effect");p.Process(samples,false,true,false);
        Check(!samples.SequenceEqual(original),"post processing applies EQ");
    }
}
