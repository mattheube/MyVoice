using MyVoice.Core;
using NAudio.Dsp;
namespace MyVoice.Audio;

/// <summary>Per-channel processing; reuses the existing microphone DSP, independently for left/right.</summary>
internal sealed class StereoChannelProcessor
{
    private readonly VoiceProcessor left=new(){Enabled=false},right=new(){Enabled=false};
    private readonly float[] l=new float[480],r=new float[480],delay=new float[48000*2],reverb=new float[4096*2];
    private int delayPosition,reverbPosition;
    private ChannelEffects? applied;
    private BiQuadFilter? highLeft,highRight;
    public void Process(float[] data,AudioChannel channel,bool microphone=false)
    {
        var fx=channel.Effects;
        if(microphone)return; // Primary mic processing already lives in Mic Setup; never apply it twice.
        if(applied!=fx){var s=new ProcessingSettings{Gate=fx.Gate,GateThreshold=fx.GateThreshold,NoiseSuppression=fx.NoiseReduction,Compressor=fx.Compressor,CompressorThreshold=fx.CompressorThreshold,CompressorRatio=fx.CompressorRatio,BassDb=fx.BassDb,MidDb=fx.MidDb,TrebleDb=fx.TrebleDb};left.Settings=s;right.Settings=s;highLeft=fx.HighPassHz>0?BiQuadFilter.HighPassFilter(48000,(float)fx.HighPassHz,.707f):null;highRight=fx.HighPassHz>0?BiQuadFilter.HighPassFilter(48000,(float)fx.HighPassHz,.707f):null;applied=fx;}
        var gain=(float)Math.Pow(10,Math.Clamp(channel.GainDb,-60,30)/20);
        for(int i=0;i<480;i++){var a=data[i*2]*gain;var b=data[i*2+1]*gain;l[i]=highLeft?.Transform(a)??a;r[i]=highRight?.Transform(b)??b;}
        left.Process(l);right.Process(r);
        for(int i=0;i<480;i++)for(int c=0;c<2;c++){float dry=c==0?l[i]:r[i];int di=(delayPosition+c)%delay.Length,ri=(reverbPosition+c)%reverb.Length;var echo=delay[di];var room=reverb[ri];delay[di]=dry+echo*.22f;reverb[ri]=dry+room*.4f;data[i*2+c]=dry+echo*(float)fx.Delay*.65f+room*(float)fx.Reverb*.35f;if(c==1){delayPosition=(delayPosition+2)%delay.Length;reverbPosition=(reverbPosition+2)%reverb.Length;}}
    }
}

/// <summary>One render plan, swapped between 10 ms blocks. UI/device management never runs here.</summary>
public sealed class MixerRouter
{
    public const int Frames=480,Samples=960;
    private sealed class ChannelRuntime
    {
        public readonly float[] Data=new float[Samples],Monitor=new float[Samples],Virtual=new float[Samples];
        public readonly StereoChannelProcessor Fx=new(),MonitorFx=new(),VirtualFx=new();
        public readonly SampleRing Input=new(48000,2);
        public MixerMeter Meter=MixerMeter.Silent;
        public float Gain=0,Pan;
        public long Frames;
    }
    private sealed class BusRuntime
    {
        public readonly float[] Data=new float[Samples];
        public readonly PeakLimiter Limiter=new();
        public MixerMeter Meter=MixerMeter.Silent;
        public float Gain;
        public long Frames;
    }
    private sealed class RouteRuntime { public float Gain; }
    private sealed record ChannelPlan(AudioChannel Settings,ChannelRuntime Runtime);
    private sealed record BusPlan(AudioOutputBus Settings,BusRuntime Runtime);
    private sealed record RoutePlan(int Channel,int Bus,bool Enabled,RouteRuntime Runtime);
    private sealed record Plan(AudioProfile Profile,ChannelPlan[] Channels,BusPlan[] Buses,RoutePlan[] Routes,bool Solo,int DeviceGeneration);
    private int deviceGeneration;
    public void SuspendForDeviceChange()=>Interlocked.Increment(ref deviceGeneration);
    private readonly object control=new();
    private readonly Dictionary<string,ChannelRuntime> channelCache=[];
    private readonly Dictionary<string,BusRuntime> busCache=[];
    private readonly Dictionary<(string,string),RouteRuntime> routeCache=[];
    private Plan plan;
    private Plan? pending;
    public MixerRouter(){plan=Prepare(MixerState.Recommended(new()));}
    private Plan Prepare(AudioProfile profile)
    {
        var channels=profile.Channels.Select(c=>new ChannelPlan(c,Get(channelCache,c.Id,()=>new ChannelRuntime()))).ToArray();
        var buses=profile.Outputs.Select(b=>new BusPlan(b,Get(busCache,b.Id,()=>new BusRuntime()))).ToArray();
        var routes=new List<RoutePlan>();
        for(int c=0;c<channels.Length;c++)for(int b=0;b<buses.Length;b++)routes.Add(new(c,b,profile.Routes.Any(r=>r.ChannelId==channels[c].Settings.Id&&r.BusId==buses[b].Settings.Id&&r.Enabled),Get(routeCache,(channels[c].Settings.Id,buses[b].Settings.Id),()=>new RouteRuntime())));
        var channelIds=profile.Channels.Select(c=>c.Id).ToHashSet();var busIds=profile.Outputs.Select(b=>b.Id).ToHashSet();
        foreach(var id in channelCache.Keys.Where(id=>!channelIds.Contains(id)).ToArray())channelCache.Remove(id);
        foreach(var id in busCache.Keys.Where(id=>!busIds.Contains(id)).ToArray())busCache.Remove(id);
        foreach(var key in routeCache.Keys.Where(k=>!channelIds.Contains(k.Item1)||!busIds.Contains(k.Item2)).ToArray())routeCache.Remove(key);
        return new(profile,channels,buses,routes.ToArray(),channels.Any(c=>c.Settings.Solo),Volatile.Read(ref deviceGeneration));
    }
    private static T Get<K,T>(Dictionary<K,T> dictionary,K key,Func<T> create)where K:notnull{if(!dictionary.TryGetValue(key,out var value))dictionary[key]=value=create();return value;}
    public void Apply(AudioProfile profile){lock(control)Volatile.Write(ref pending,Prepare(profile));}
    public AudioProfile Configuration=>Volatile.Read(ref pending)?.Profile??Volatile.Read(ref plan).Profile;
    public SampleRing Input(string channelId){lock(control)return Get(channelCache,channelId,()=>new ChannelRuntime()).Input;}
    public MixerMeter ChannelMeter(string id){lock(control)return channelCache.TryGetValue(id,out var channel)?Volatile.Read(ref channel.Meter):MixerMeter.Silent;}
    public MixerMeter BusMeter(string id){lock(control)return busCache.TryGetValue(id,out var bus)?Volatile.Read(ref bus.Meter):MixerMeter.Silent;}
    public long DroppedSamples{get{lock(control)return channelCache.Values.Sum(c=>c.Input.Dropped);}}
    public ReadOnlySpan<float> Bus(string id){var p=plan;foreach(var b in p.Buses)if(b.Settings.Id==id)return b.Runtime.Data;return ReadOnlySpan<float>.Empty;}
    public ReadOnlySpan<float> Channel(string id){var p=plan;foreach(var c in p.Channels)if(c.Settings.Id==id)return c.Runtime.Data;return ReadOnlySpan<float>.Empty;}
    public void Render(ReadOnlySpan<float> microphone,ReadOnlySpan<float> soundboard,ReadOnlySpan<float> soundMonitor,ReadOnlySpan<float> soundVirtual,ReadOnlySpan<float> virtualTest=default)
    {
        var next=Interlocked.Exchange(ref pending,null);if(next!=null)Volatile.Write(ref plan,next);
        var p=plan;
        foreach(var b in p.Buses)Array.Clear(b.Runtime.Data);
        if(p.DeviceGeneration!=Volatile.Read(ref deviceGeneration)){foreach(var b in p.Buses)Volatile.Write(ref b.Runtime.Meter,MixerMeter.Silent);return;}
        foreach(var c in p.Channels)
        {
            var rt=c.Runtime;var data=rt.Data;
            if(c.Settings.Type=="microphone"){for(int i=0;i<Frames;i++)data[i*2]=data[i*2+1]=microphone[i];}
            else if(c.Settings.Type=="soundboard"){soundboard.CopyTo(data);soundMonitor.CopyTo(rt.Monitor);soundVirtual.CopyTo(rt.Virtual);}
            else if(c.Settings.CaptureEnabled)rt.Input.Take(data);else Array.Clear(data);
            rt.Fx.Process(data,c.Settings,c.Settings.Type=="microphone");
            if(c.Settings.Type=="soundboard"){rt.MonitorFx.Process(rt.Monitor,c.Settings);rt.VirtualFx.Process(rt.Virtual,c.Settings);}
            float target=c.Settings.Mute||(p.Solo&&!c.Settings.Solo)?0:(float)Math.Clamp(c.Settings.Volume,0,1e6);
            float pan=(float)Math.Clamp(c.Settings.Pan,-1,1);
            for(int i=0;i<Frames;i++)
            {
                rt.Gain+=(target-rt.Gain)*.004f;rt.Pan+=(pan-rt.Pan)*.004f;
                float gl=rt.Gain*(1-Math.Max(0,rt.Pan)),gr=rt.Gain*(1+Math.Min(0,rt.Pan));
                data[i*2]*=gl;data[i*2+1]*=gr;
                if(c.Settings.Type=="soundboard"){rt.Monitor[i*2]*=gl;rt.Monitor[i*2+1]*=gr;rt.Virtual[i*2]*=gl;rt.Virtual[i*2+1]*=gr;}
            }
            rt.Frames+=Frames;Volatile.Write(ref rt.Meter,Measure(data,rt.Frames));
        }
        foreach(var route in p.Routes)
        {
            var c=p.Channels[route.Channel];var b=p.Buses[route.Bus];var src=c.Settings.Type=="soundboard"?(b.Settings.Id=="monitor"?c.Runtime.Monitor:b.Settings.Id=="virtual"?c.Runtime.Virtual:c.Runtime.Data):c.Runtime.Data;
            var output=b.Runtime.Data;float target=route.Enabled?1:0;
            for(int i=0;i<Frames;i++){route.Runtime.Gain+=(target-route.Runtime.Gain)*.004f;var gain=route.Runtime.Gain;output[i*2]+=src[i*2]*gain;output[i*2+1]+=src[i*2+1]*gain;}
        }
        foreach(var b in p.Buses)
        {
            var rt=b.Runtime;float target=b.Settings.Mute?0:(float)Math.Clamp(b.Settings.Volume,0,1e6);
            for(int i=0;i<Frames;i++){rt.Gain+=(target-rt.Gain)*.002f;rt.Data[i*2]*=rt.Gain;rt.Data[i*2+1]*=rt.Gain;}
            if(b.Settings.Id=="virtual"&&!virtualTest.IsEmpty)for(int i=0;i<Samples;i++)rt.Data[i]+=virtualTest[i];
            bool clipping=false;foreach(var v in rt.Data)if(Math.Abs(v)>=.98f)clipping=true;
            rt.Limiter.Process(rt.Data);rt.Frames+=Frames;var meter=Measure(rt.Data,rt.Frames);Volatile.Write(ref rt.Meter,meter with{Clipping=clipping});
        }
    }
    public static MixerMeter Measure(ReadOnlySpan<float> data,long samples=0){double sum=0,peak=0;foreach(var sample in data){var v=float.IsFinite(sample)?sample:0;sum+=(double)v*v;peak=Math.Max(peak,Math.Abs(v));}return new(Calibration.Db(peak),Calibration.Db(Math.Sqrt(sum/Math.Max(1,data.Length))),peak>=.98,samples);}
}
