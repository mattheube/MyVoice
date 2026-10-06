using System.Diagnostics;
using MyVoice.Core;
using MyVoice.Soundboard;
using NAudio.CoreAudioApi;
using NAudio.Wave;
namespace MyVoice.Audio;
public sealed class AudioGraph : IDisposable
{
    private readonly SampleRing mic = new(4800,trackTimestamps:true), converted = new(48000*60,trackTimestamps:true);
    private readonly SoundboardService sounds;
    private readonly Thread worker;
    private readonly CancellationTokenSource stop = new();
    private readonly object outputGate = new();
    private Endpoint? monitor, virtualOutput;
    private readonly PeakLimiter monitorLimiter = new(), virtualLimiter = new();
    public VoiceProcessor Processor { get; } = new();
    private VoiceProcessor aiPost { get; } = new();
    public bool HearMyself
    {
        get; set;
    }
    public bool SendMicrophone { get; set; } = true;
    public bool Muted
    {
        get; set;
    }
    public float MonitorVolume { get; set; } = .5f;
    public bool AiLive
    {
        get; set;
    }
    public string? MonitorId
    {
        get; private set;
    }
    public string? VirtualId
    {
        get; private set;
    }
    public double MonitorPeak { get; private set; } = -90;
    public double ProcessedPeak { get; private set; } = -90;
    private int testFrames;
    private double testPhase;
    public void TestVirtualOutput() => Interlocked.Exchange(ref testFrames, 48000);
    public double OutputPeak { get; private set; } = -90;
    public long DroppedFrames => mic.Dropped + converted.Dropped;
    public event Action<string>? Error;
    public event AudioSamplesHandler? AiInput;
    public long AiInputTimestamp {get;private set;}
    public event Action<double>? AiVirtualSubmitted;
    private int convertedDiscontinuity;
    private float lastConvertedSample;
    public AudioGraph(SoundboardService sounds)
    {
        this.sounds = sounds;
        worker = new Thread(Run) { IsBackground = true, Name = "MyVoice mixer", Priority = ThreadPriority.AboveNormal };
        worker.Start();
    }
    public void PushMicrophone(ReadOnlySpan<float> samples) => mic.Write(samples,Stopwatch.GetTimestamp()-(long)(samples.Length*Stopwatch.Frequency/48000d));
    public int ConvertedFrames => converted.Count;
    public void PushConverted(ReadOnlySpan<float> samples,long firstTimestamp=0) => converted.Write(samples,firstTimestamp);
    public void FlushConverted(){converted.Clear();Interlocked.Exchange(ref convertedDiscontinuity,1);}
    public void FlushMicrophone()
    {
        mic.Clear();
        converted.Clear();
    }
    public IReadOnlyList<AudioDevice> Devices()
    {
        using var e = new MMDeviceEnumerator();
        var list = new List<AudioDevice>();
        foreach (var d in e.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        using (d)
            list.Add(new(d.ID, d.FriendlyName));
        try
        {
            using var preferred=e.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);
            return list.OrderByDescending(d=>d.Id==preferred.ID).ToArray();
        }
        catch(System.Runtime.InteropServices.COMException){ return list; }
    }
    public void Configure(string? monitorId, string? virtualId)
    {
        if (monitorId != null && monitorId == virtualId)
            throw new InvalidOperationException("Choose different monitoring and virtual outputs.");
        lock (outputGate)
        {
            if (monitorId != MonitorId || monitor?.Failed == true)
            {
                monitor?.Dispose();
                monitor = null;
                MonitorId = null;
                if (monitorId != null)
                {
                    monitor = new(monitorId, Error);
                    MonitorId = monitorId;
                }
            }
            if (virtualId != VirtualId || virtualOutput?.Failed == true)
            {
                virtualOutput?.Dispose();
                virtualOutput = null;
                VirtualId = null;
                if (virtualId != null)
                {
                    virtualOutput = new(virtualId, Error);
                    VirtualId = virtualId;
                }
            }
        }
    }
    private void Run()
    {
        var voice = new float[480];
        var ai = new float[480];
        var local = new float[960];
        var remote = new float[960];
        var clock = Stopwatch.StartNew();
        long next = 0;
        while (!stop.IsCancellationRequested)
        {
            try
            {
                mic.Take(voice);
                Processor.Process(voice, true, !AiLive, !AiLive);
                if (AiLive)
                {
                    AiInputTimestamp=mic.LastReadTimestamp;
                    AiInput?.Invoke(voice);
                    converted.Take(ai);
                    if(Interlocked.Exchange(ref convertedDiscontinuity,0)==1)for(int j=0;j<ai.Length;j++){float w=j/(float)(ai.Length-1);ai[j]=lastConvertedSample*(1-w)+ai[j]*w;}
                    lastConvertedSample=ai[^1];
                    aiPost.Settings=Processor.Settings;aiPost.Process(ai,false,true,false);
                }
                Array.Clear(local);
                Array.Clear(remote);
                AudioRouting.MixMicrophone(AiLive ? ai : voice, local, remote, Muted, HearMyself, SendMicrophone);
                sounds.Mix(local, remote);
                if (testFrames > 0)
                {
                    for(int i=0;i<480;i++) { var tone=(float)(.025*Math.Sin(testPhase)); testPhase+=2*Math.PI*733/48000; remote[i*2]+=tone; remote[i*2+1]+=tone; }
                    Interlocked.Add(ref testFrames, -480);
                }
                ProcessedPeak = Calibration.Db((AiLive ? ai : voice).Max(x => Math.Abs(x)));
                for (int i = 0; i < local.Length; i++)
                    local[i] *= MonitorVolume;
                monitorLimiter.Process(local);
                virtualLimiter.Process(remote);
                MonitorPeak = Calibration.Db(local.Max(x => Math.Abs(x)));
                OutputPeak = Calibration.Db(remote.Max(x => Math.Abs(x)));
                lock (outputGate)
                {
                    monitor?.Write(local);
                    virtualOutput?.Write(remote);
                    if(virtualOutput!=null&&AiLive&&converted.LastReadTimestamp>0)AiVirtualSubmitted?.Invoke((Stopwatch.GetTimestamp()-converted.LastReadTimestamp)*1000d/Stopwatch.Frequency);
                }
            }
            catch (Exception e) { Error?.Invoke(e.Message); }
            next += 10;
            var delay = next - clock.ElapsedMilliseconds;
            if (delay > 0)
                stop.Token.WaitHandle.WaitOne((int)delay);
            else if (delay < -100)
                next = clock.ElapsedMilliseconds;
        }
    }
    public void Dispose()
    {
        stop.Cancel();
        worker.Join(2000);
        lock (outputGate)
        {
            monitor?.Dispose();
            virtualOutput?.Dispose();
        }
        stop.Dispose();
    }
    private sealed class Endpoint : IDisposable
    {
        private readonly MMDevice device; private readonly WasapiOut player; private readonly SampleRing ring = new(9600, 2);
        public bool Failed { get; private set; }
        public Endpoint(string id, Action<string>? error)
        {
            using var e = new MMDeviceEnumerator();
            device = e.GetDevice(id);
            player = new WasapiOut(device, AudioClientShareMode.Shared, true, 30);
            try
            {
                player.Init(ring);
                player.PlaybackStopped += (_, args) => { if (args.Exception != null) { Failed=true; error?.Invoke(args.Exception.Message); } };
                player.Play();
            }
            catch { player.Dispose(); device.Dispose(); throw; }
        }
        public void Write(ReadOnlySpan<float> samples)
        {
            if (ring.Count > 7680)
                ring.Clear();
            ring.Write(samples);
        }
        public void Dispose()
        {
            player.Stop();
            player.Dispose();
            device.Dispose();
        }
    }
}
