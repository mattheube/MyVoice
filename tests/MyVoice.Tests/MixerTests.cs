using MyVoice.Core;
using MyVoice.Audio;
using MyVoice.Infrastructure;
using NAudio.Wave;
using System.Diagnostics;
static class MixerTests
{
    public static async Task Run()
    {
        void Check(bool yes,string name){if(!yes)throw new Exception(name);Console.WriteLine("PASS "+name);}
        var settings=new AppSettings{HearMyself=false,SendMicrophone=true,MonitoringVolume=.5,SoundboardVolume=1.4};
        var original=MixerState.Recommended(settings);MixerStore.Validate(original);
        Check(!original.Routes.Single(r=>r.ChannelId=="mic"&&r.BusId=="monitor").Enabled&&original.Channels.Single(c=>c.Id=="soundboard").Volume==1.4,"Mixer migration preserves monitoring and soundboard gain");
        var p=original with{Channels=original.Channels.Append(new AudioChannel{Id="music",Name="Music",Type="capture",CaptureEnabled=true}).ToList(),Routes=original.Routes.Append(new("music","stream")).Append(new("music","monitor")).ToList()};
        var mixer=new MixerRouter();mixer.Apply(p);
        var voice=Enumerable.Repeat(.1f,480).ToArray();var audio=new float[960];for(int i=0;i<480;i++){audio[i*2]=.2f;audio[i*2+1]=.1f;}var silence=new float[960];
        void Render(int n=40){for(int i=0;i<n;i++){mixer.Input("music").Write(audio);mixer.Render(voice,silence,silence,silence);}}
        Render();Check(Math.Abs(mixer.Bus("virtual")[400]-.1)<.002,"Mic goes to Virtual independently of Music");Check(Math.Abs(mixer.Bus("stream")[400]-.3)<.003,"Multiple sources sum into Stream");Check(Math.Abs(mixer.Bus("monitor")[400]-.1)<.002,"Music monitor gain and mic exclusion");
        p=p with{Channels=p.Channels.Select(c=>c.Id=="music"?c with{Pan=1}:c).ToList()};mixer.Apply(p);Render();Check(Math.Abs(mixer.Bus("monitor")[400])<.001&&mixer.Bus("monitor")[401]>.045,"Stereo balance fully right without channel bleed");
        p=p with{Channels=p.Channels.Select(c=>c.Id=="music"?c with{Solo=true,Pan=0}:c).ToList()};mixer.Apply(p);Render();Check(Math.Abs(mixer.Bus("virtual")[400]-.1)<.002&&mixer.Bus("monitor")[400]>.09,"Solo preserves virtual microphone while isolating monitoring");
        p=p with{Channels=p.Channels.Select(c=>c.Id=="music"?c with{Mute=true}:c).ToList()};mixer.Apply(p);Render();Check(Math.Abs(mixer.Bus("stream")[400]-.1)<.002,"Mute overrides solo without muting other stream sources");
        p=p with{Channels=p.Channels.Select(c=>c.Id=="music"?c with{Mute=false,Volume=100}:c).ToList()};mixer.Apply(p);Render();Check(mixer.Bus("monitor").ToArray().Max(Math.Abs)<=.98&&mixer.BusMeter("monitor").Clipping,"Output limiter protects large numeric gain and reports clipping");
        var isolated=p with{Channels=p.Channels.Select(c=>c.Id=="music"?c with{Volume=1,Mono=true,NeverSendVirtual=true}:c).ToList(),Routes=p.Routes.Append(new("music","virtual")).ToList()};
        mixer.Apply(isolated);Render();Check(Math.Abs(mixer.Bus("virtual")[400]-.1)<.002,"Persistent exclusion overrides enabled virtual route");
        Check(Math.Abs(mixer.Channel("music")[400]-mixer.Channel("music")[401])<.001,"Mono downmix preserves balanced stereo output");
        mixer.Apply(p);Render();
        var unsafeProfile=p with{Channels=p.Channels.Append(new AudioChannel{Id="loop",Name="System",Type="system",DeviceId="same"}).ToList(),Outputs=p.Outputs.Select(b=>b.Id=="monitor"?b with{DeviceMode="Fixed",DeviceId="same"}:b).ToList(),Routes=p.Routes.Append(new("loop","monitor")).ToList()};Check(RoutingSafety.Inspect(unsafeProfile).Count==1&&!RoutingSafety.Fix(unsafeProfile).Routes.Single(r=>r.ChannelId=="loop").Enabled,"Loopback output recursion detected and removed");
        Check(RoutingSafety.CableKey("CABLE-A Output (VB-Audio Cable A)")==RoutingSafety.CableKey("CABLE-A Input (VB-Audio Cable A)"),"Virtual cable input/output pair detection");
        var cycle=original with{Channels=original.Channels.Concat(new[]{new AudioChannel{Id="a",Name="Cable A",DeviceName="CABLE-A Output"},new AudioChannel{Id="b",Name="Cable B",DeviceName="CABLE-B Output"}}).ToList(),Outputs=original.Outputs.Concat(new[]{new AudioOutputBus{Id="out-a",Name="A",DeviceMode="Fixed",DeviceName="CABLE-A Input"},new AudioOutputBus{Id="out-b",Name="B",DeviceMode="Fixed",DeviceName="CABLE-B Input"}}).ToList(),Routes=[new("a","out-b"),new("b","out-a")]};
        Check(RoutingSafety.Fix(cycle).Routes.All(r=>!r.Enabled),"Feedback spanning two virtual cables blocked");
        using(var midi=new MixerMidi()){for(int i=0;i<500;i++)midi.Enqueue(new("Test","CC",1,7,i%128));int count=0;while(midi.Read(out var signal)){if(signal?.Number!=7)throw new Exception("MIDI queue corruption");count++;}Check(count==256,"MIDI callback queue is bounded and preserves control messages");}
        mixer.SuspendForDeviceChange();mixer.Render(voice,silence,silence,silence);Check(mixer.Bus("monitor").ToArray().All(x=>x==0),"Device changes suspend old routes before opening endpoints");
        mixer.Apply(p);Render();Check(mixer.Bus("monitor").ToArray().Any(x=>x!=0),"Validated new plan resumes after device change");
        mixer.Apply(p with{Channels=p.Channels.Select(c=>c.Id=="music"?c with{CaptureEnabled=false,Effects=new()}:c).ToList()});Render();Check(mixer.Bus("monitor").ToArray().Max(Math.Abs)<.001,"Stopped capture cannot leak queued audio");
        var root=Path.Combine(Path.GetTempPath(),"MyVoice-MixerTests-"+Guid.NewGuid());Directory.CreateDirectory(root);var store=new MixerStore(root);var state=store.Load(settings);state.Current=p;state.Profiles.Add(p with{Name="Gaming"});store.Save(state);Check(store.Load(settings).Profiles[0].Name=="Gaming","Audio profiles persist locally");
        var file=Path.Combine(root,"profile.json");MixerStore.Export(file,unsafeProfile);var portable=File.ReadAllText(file);var imported=MixerStore.Import(file);Check(!portable.Contains("same")&&imported.Outputs.All(b=>b.DeviceMode=="Internal")&&imported.Channels.All(c=>c.DeviceId==null),"Profile export strips machine endpoints and imports disconnected");
        File.WriteAllText(file,"{\"Name\":\"invalid\",\"Channels\":[]}");try{MixerStore.Import(file);throw new Exception("Unsafe profile accepted");}catch(InvalidDataException){Console.WriteLine("PASS invalid profile rejected without changing current routing");}
        File.WriteAllText(file,"{\"Name\":\"invalid\",\"Channels\":null}");try{MixerStore.Import(file);throw new Exception("Null profile accepted");}catch(InvalidDataException){Console.WriteLine("PASS null profile rejected safely");}
        var ring=new SampleRing(100000,2);var resampler=new StereoNormalizer(44100,1);resampler.Convert(Enumerable.Repeat(.2f,22050).ToArray(),ring);Check(Math.Abs(ring.Count-48000)<3,"44.1k mono normalized to 48k stereo with stable phase");var chunk=new float[960];ring.Take(chunk);Check(chunk.Where((x,i)=>i%2==0).Zip(chunk.Where((x,i)=>i%2==1),(a,b)=>a==b).All(x=>x),"Mono duplicated to both stereo channels");
        var surroundRing=new SampleRing(4800,2);new StereoNormalizer(48000,6).Convert([0,0,.3f,0,0,0],surroundRing);float[] stereo=new float[2];surroundRing.Take(stereo);Check(stereo[0]>.05&&stereo[0]==stereo[1],"5.1 center dialogue is retained in the stereo mix");
        using(var recorder=new MixerRecorder()){var path=Path.Combine(root,"recording.wav");recorder.Start(path,"stream");for(int i=0;i<100;i++){recorder.Offer("monitor",audio);recorder.Offer("stream",audio);}await recorder.StopAsync();using var wave=new WaveFileReader(path);Check(wave.WaveFormat.SampleRate==48000&&wave.WaveFormat.BitsPerSample==32&&Math.Abs(wave.TotalTime.TotalSeconds-1)<.001,"WAV recording selects its bus and finalizes an exact one-second float32 file");}
        using(var recorder=new MixerRecorder()){for(int n=0;n<5;n++){recorder.Start(Path.Combine(root,$"tail-{n}.wav"),"stream");for(int i=0;i<15;i++)recorder.Offer("stream",audio);await recorder.StopAsync();Check(recorder.Error==null&&Math.Abs(recorder.Duration-.15)<.001,"Recording stop drains its tail before the next file "+n);}}
        var separated=new MixerRouter();separated.Apply(original);var music=Enumerable.Repeat(.1f,960).ToArray();for(int i=0;i<40;i++)separated.Render(new float[480],music,silence,music);Check(separated.Bus("monitor").ToArray().All(x=>x==0)&&separated.Bus("virtual").ToArray().Max(Math.Abs)>.1,"Soundboard per-item Hear OFF / Send ON survives new routing");
        var dynamics=new MixerRouter();var dynamicsProfile=original with{Channels=original.Channels.Select(c=>c.Id=="soundboard"?c with{Volume=1,Effects=new(){MakeupDb=6}}:c).ToList()};dynamics.Apply(dynamicsProfile);
        for(int i=0;i<40;i++)dynamics.Render(new float[480],music,music,music);
        Check(Math.Abs(dynamics.Channel("soundboard")[500]-.1*Math.Pow(10,6d/20))<.002,"Mixer makeup gain reaches the real audio processor");
        var roundtrip=System.Text.Json.JsonSerializer.Deserialize<AudioProfile>(System.Text.Json.JsonSerializer.Serialize(dynamicsProfile));
        Check(roundtrip!.Channels.Single(c=>c.Id=="soundboard").Effects.MakeupDb==6,"Channel dynamics survive profile serialization");
        try{MixerStore.Validate(original with{Channels=original.Channels.Select(c=>c with{Effects=new(){GateReleaseMs=double.NaN}}).ToList()});throw new Exception("Non-finite dynamics accepted");}catch(InvalidDataException){Console.WriteLine("PASS malformed channel dynamics rejected");}
        var eq=new MixerRouter();eq.Apply(original with{Outputs=original.Outputs.Select(b=>b.Id=="stream"?b with{Equalizer=[new(1000,-12,2)]}:b).ToList()});
        var sine=Enumerable.Range(0,480).Select(i=>(float)(.1*Math.Sin(2*Math.PI*1000*i/48000))).ToArray();for(int i=0;i<50;i++)eq.Render(sine,silence,silence,silence);
        Check(eq.BusMeter("stream").RmsDb<eq.BusMeter("virtual").RmsDb-10,"Output parametric EQ cuts its selected frequency without changing another bus");
        try{MixerStore.Validate(original with{Outputs=original.Outputs.Select(b=>b with{Equalizer=[new(25000)]}).ToList()});throw new Exception("Invalid EQ accepted");}catch(InvalidDataException){Console.WriteLine("PASS out-of-range output EQ rejected");}
        var perf=new MixerRouter();perf.Apply(original);var clock=Stopwatch.StartNew();for(int i=0;i<1000;i++)perf.Render(voice,silence,silence,silence);clock.Stop();Check(clock.Elapsed.TotalMilliseconds/1000<8,"Mixer block processing below 10ms deadline");Console.WriteLine($"Mixer mean test block: {clock.Elapsed.TotalMilliseconds/1000:F3} ms");
    }
}
