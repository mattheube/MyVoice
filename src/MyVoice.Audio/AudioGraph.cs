using System.Diagnostics;
using MyVoice.Core;
using MyVoice.Soundboard;
using NAudio.CoreAudioApi;
namespace MyVoice.Audio;
public sealed class AudioGraph : IDisposable
{
    // Capacity absorbs short Windows scheduling/GC stalls; it does not prefill or delay playback.
    // 100 ms overflowed during a real Conversation run (20 ms lost at the input).
    private readonly SampleRing mic=new(24000,trackTimestamps:true),converted=new(48000*60,trackTimestamps:true),soundQueue=new(2880*8,2);
    private readonly SoundboardService sounds;
    private readonly Thread worker,soundWorker;
    private readonly CancellationTokenSource stop=new();
    private readonly object configurationGate=new();
    private readonly MixerDeviceManager devices;
    private bool devicesEnabled;
    private AudioProfile? requestedProfile;
    public MixerRouter Mixer {get;}=new();
    public MixerRecorder Recorder {get;}=new();
    public IReadOnlyList<SourceStatus> DeviceStatuses=>devices.Statuses;
    public VoiceProcessor Processor {get;}=new();
    private VoiceProcessor aiPost {get;}=new();
    public bool HearMyself {get=>Route("mic","monitor");set=>SetRoute("mic","monitor",value);}
    public bool SendMicrophone {get=>Route("mic","virtual");set=>SetRoute("mic","virtual",value);}
    public bool Muted {get=>Mixer.Configuration.Channels.First(c=>c.Id=="mic").Mute;set=>Update(p=>p with{Channels=p.Channels.Select(c=>c.Id=="mic"?c with{Mute=value}:c).ToList()});}
    public float MonitorVolume {get=>(float)Mixer.Configuration.Outputs.First(b=>b.Id=="monitor").Volume;set=>Update(p=>p with{Outputs=p.Outputs.Select(b=>b.Id=="monitor"?b with{Volume=value}:b).ToList()});}
    public bool AiLive {get;set;}
    public string? MonitorId=>Mixer.Configuration.Outputs.FirstOrDefault(b=>b.Id=="monitor")?.DeviceId;
    public string? VirtualId=>Mixer.Configuration.Outputs.FirstOrDefault(b=>b.Id=="virtual")?.DeviceId;
    public double MonitorPeak=>Mixer.BusMeter("monitor").PeakDb;
    public double OutputPeak=>Mixer.BusMeter("virtual").PeakDb;
    public double ProcessedPeak {get;private set;}=-90;
    private int testFrames;
    private double testPhase;
    public void TestVirtualOutput()=>Interlocked.Exchange(ref testFrames,48000);
    public long DroppedFrames=>mic.Dropped+converted.Dropped+Mixer.DroppedSamples;
    public event Action<string>? Error;
    public event AudioSamplesHandler? AiInput;
    public long AiInputTimestamp {get;private set;}
    public event Action<double>? AiVirtualSubmitted;
    private int convertedDiscontinuity;
    private float lastConvertedSample;
    private double averageBlockMs,maxBlockMs;
    private long lateBlocks,renderedBlocks;
    public MixerDiagnostics Diagnostics=>new(averageBlockMs,maxBlockMs,Interlocked.Read(ref lateBlocks),DroppedFrames+devices.Dropped,devices.Underruns,Interlocked.Read(ref renderedBlocks));
    public AudioGraph(SoundboardService sounds)
    {
        this.sounds=sounds;devices=new(Mixer);soundQueue.Write(new float[5760]);
        soundWorker=new Thread(RenderSoundboard){IsBackground=true,Name="MyVoice sound decoder"};soundWorker.Start();
        worker=new Thread(Run){IsBackground=true,Name="MyVoice mixer",Priority=ThreadPriority.AboveNormal};worker.Start();
    }
    public void ApplyMixer(AudioProfile profile,bool enableDevices=true){lock(configurationGate){requestedProfile=profile;devicesEnabled=enableDevices;devices.Apply(profile,enableDevices);}}
    private bool Route(string c,string b)=>Mixer.Configuration.Routes.Any(r=>r.ChannelId==c&&r.BusId==b&&r.Enabled);
    private void Update(Func<AudioProfile,AudioProfile> update){lock(configurationGate)ApplyMixer(update(requestedProfile??Mixer.Configuration),devicesEnabled);}
    private void SetRoute(string c,string b,bool value)=>Update(p=>p with{Routes=p.Routes.Where(r=>r.ChannelId!=c||r.BusId!=b).Append(new(c,b,value)).ToList()});
    public void PushMicrophone(ReadOnlySpan<float> samples)=>mic.Write(samples,Stopwatch.GetTimestamp()-(long)(samples.Length*Stopwatch.Frequency/48000d));
    public int ConvertedFrames=>converted.Count;
    public void PushConverted(ReadOnlySpan<float> samples,long firstTimestamp=0)=>converted.Write(samples,firstTimestamp);
    public void FlushConverted(){converted.Clear();Interlocked.Exchange(ref convertedDiscontinuity,1);}
    public void FlushMicrophone(){mic.Clear();converted.Clear();}
    public IReadOnlyList<AudioDevice> Devices()
    {
        using var e=new MMDeviceEnumerator();var list=new List<AudioDevice>();foreach(var d in e.EnumerateAudioEndPoints(DataFlow.Render,DeviceState.Active))using(d)list.Add(new(d.ID,d.FriendlyName));
        try{using var preferred=e.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);return list.OrderByDescending(d=>d.Id==preferred.ID).ToArray();}catch(System.Runtime.InteropServices.COMException){return list;}
    }
    public void Configure(string? monitorId,string? virtualId)
    {
        if(monitorId!=null&&monitorId==virtualId)throw new InvalidOperationException("Choose different monitoring and virtual outputs.");
        var available=Devices();var p=requestedProfile??Mixer.Configuration;
        var outputs=p.Outputs.Select(b=>b.Id=="monitor"?b with{DeviceId=monitorId,DeviceName=available.FirstOrDefault(d=>d.Id==monitorId)?.Name,DeviceMode=monitorId==null?"Internal":"Fixed"}:b.Id=="virtual"?b with{DeviceId=virtualId,DeviceName=available.FirstOrDefault(d=>d.Id==virtualId)?.Name,DeviceMode=virtualId==null?"Internal":"Fixed"}:b).ToList();
        ApplyMixer(p with{Outputs=outputs});
    }
    private void RenderSoundboard()
    {
        var data=new float[2880];var clock=Stopwatch.StartNew();long next=0;
        while(!stop.IsCancellationRequested)
        {
            Array.Clear(data);try{sounds.MixChannels(data.AsSpan(960,960),data.AsSpan(1920,960),data.AsSpan(0,960));soundQueue.Write(data);}catch(Exception e){Error?.Invoke(e.Message);}
            next+=10;long delay=next-clock.ElapsedMilliseconds;if(delay>0)stop.Token.WaitHandle.WaitOne((int)delay);else if(delay < -100)next=clock.ElapsedMilliseconds;
        }
    }
    private void Run()
    {
        var voice=new float[480];var ai=new float[480];var raw=new float[960];var sb=new float[2880];var test=new float[960];
        var clock=Stopwatch.StartNew();long next=0;
        while(!stop.IsCancellationRequested)
        {
            long started=Stopwatch.GetTimestamp();
            try
            {
                mic.Take(voice);for(int i=0;i<480;i++)raw[i*2]=raw[i*2+1]=voice[i];Recorder.Offer("raw-mic",raw);
                Processor.Process(voice,true,!AiLive,!AiLive);
                if(AiLive){AiInputTimestamp=mic.LastReadTimestamp;AiInput?.Invoke(voice);converted.Take(ai);if(Interlocked.Exchange(ref convertedDiscontinuity,0)==1)for(int j=0;j<ai.Length;j++){float w=j/(float)(ai.Length-1);ai[j]=lastConvertedSample*(1-w)+ai[j]*w;}lastConvertedSample=ai[^1];aiPost.Settings=Processor.Settings;aiPost.Process(ai,false,true,false);}
                var processed=AiLive?ai:voice;ProcessedPeak=MixerRouter.Measure(processed).PeakDb;
                soundQueue.Take(sb);Array.Clear(test);
                if(testFrames>0){for(int i=0;i<480;i++){var tone=(float)(.025*Math.Sin(testPhase));testPhase+=2*Math.PI*733/48000;test[i*2]=test[i*2+1]=tone;}Interlocked.Add(ref testFrames,-480);}
                Mixer.Render(processed,sb.AsSpan(0,960),sb.AsSpan(960,960),sb.AsSpan(1920,960),test);
                foreach(var bus in Mixer.Configuration.Outputs)Recorder.Offer(bus.Id,Mixer.Bus(bus.Id));
                foreach(var channel in Mixer.Configuration.Channels)Recorder.Offer("channel:"+channel.Id,Mixer.Channel(channel.Id));
                for(int i=0;i<480;i++)raw[i*2]=raw[i*2+1]=processed[i];Recorder.Offer("processed-mic",raw);
                devices.Submit();
                if(VirtualId!=null&&AiLive&&converted.LastReadTimestamp>0)AiVirtualSubmitted?.Invoke((Stopwatch.GetTimestamp()-converted.LastReadTimestamp)*1000d/Stopwatch.Frequency);
            }
            catch(Exception e){Error?.Invoke(e.Message);}
            var elapsed=(Stopwatch.GetTimestamp()-started)*1000d/Stopwatch.Frequency;averageBlockMs=averageBlockMs*.98+elapsed*.02;maxBlockMs=Math.Max(maxBlockMs,elapsed);Interlocked.Increment(ref renderedBlocks);
            next+=10;var delay=next-clock.ElapsedMilliseconds;if(delay>0)stop.Token.WaitHandle.WaitOne((int)delay);else{Interlocked.Increment(ref lateBlocks);if(delay < -100)next=clock.ElapsedMilliseconds;}
        }
    }
    public void Dispose(){stop.Cancel();worker.Join(3000);soundWorker.Join(3000);Recorder.Dispose();devices.Dispose();stop.Dispose();}
}
