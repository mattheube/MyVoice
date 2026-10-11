using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using MyVoice.Core;
namespace MyVoice.Audio;

/// <summary>Streaming linear rate conversion; carries phase across packets and preserves stereo.</summary>
public sealed class StereoNormalizer
{
    private readonly int rate,channels;
    private double next;
    private long position;
    private float previousLeft,previousRight;
    private readonly float[] output=new float[8192];
    public StereoNormalizer(int sampleRate,int channelCount){rate=sampleRate;channels=channelCount;if(rate<8000||channels<1||channels>32)throw new ArgumentException("Unsupported capture format");}
    private static float At(ReadOnlySpan<float> input,int index)=>float.IsFinite(input[index])?input[index]:0;
    public void Convert(ReadOnlySpan<float> input,SampleRing destination)
    {
        int written=0;
        for(int i=0;i+channels<=input.Length;i+=channels)
        {
            float left=float.IsFinite(input[i])?input[i]:0,right=channels==1?left:float.IsFinite(input[i+1])?input[i+1]:0;
            // Standard Windows surround layouts: fold center and surrounds into stereo.

            if(channels==3){left=(left+At(input,i+2)*.707f)/1.707f;right=(right+At(input,i+2)*.707f)/1.707f;}
            else if(channels==4){left=(left+At(input,i+2)*.707f)/1.707f;right=(right+At(input,i+3)*.707f)/1.707f;}
            else if(channels==5){left=(left+(At(input,i+2)+At(input,i+3))*.707f)/2.414f;right=(right+(At(input,i+2)+At(input,i+4))*.707f)/2.414f;}
            else if(channels is 6 or 8){left+=At(input,i+2)*.707f+At(input,i+3)*.5f+At(input,i+4)*.707f;right+=At(input,i+2)*.707f+At(input,i+3)*.5f+At(input,i+5)*.707f;if(channels==8){left+=At(input,i+6)*.707f;right+=At(input,i+7)*.707f;}var scale=channels==8?3.621f:2.914f;left/=scale;right/=scale;}
            while(next<=position){float fraction=(float)Math.Clamp(next-(position-1),0,1);output[written++]=previousLeft+(left-previousLeft)*fraction;output[written++]=previousRight+(right-previousRight)*fraction;if(written==output.Length){destination.Write(output);written=0;}next+=rate/48000d;}
            previousLeft=left;previousRight=right;position++;
        }
        if(written>0)destination.Write(output.AsSpan(0,written));
    }
}
internal sealed class EndpointCapture : IDisposable
{
    private readonly MMDevice device;
    private readonly WasapiCapture capture;
    private readonly SampleRing destination;
    private readonly StereoNormalizer normalizer;
    public bool Failed {get;private set;}
    public string Format=>capture.WaveFormat.SampleRate+" Hz · "+capture.WaveFormat.Channels+" canaux";
    public EndpointCapture(string id,bool loopback,SampleRing input)
    {
        destination=input;using var e=new MMDeviceEnumerator();device=e.GetDevice(id);
        capture=loopback?new WasapiLoopbackCapture(device):new WasapiCapture(device,false,20);
        var mix=device.AudioClient.MixFormat;capture.WaveFormat=WaveFormat.CreateIeeeFloatWaveFormat(mix.SampleRate,mix.Channels);
        normalizer=new(capture.WaveFormat.SampleRate,capture.WaveFormat.Channels);
        capture.DataAvailable+=Data;capture.RecordingStopped+=Stopped;
        try{capture.StartRecording();}catch{capture.Dispose();device.Dispose();throw;}
    }
    private void Data(object? sender,WaveInEventArgs e){if(e.BytesRecorded>0)normalizer.Convert(MemoryMarshal.Cast<byte,float>(e.Buffer.AsSpan(0,e.BytesRecorded)),destination);}
    private void Stopped(object? sender,StoppedEventArgs e){if(e.Exception!=null)Failed=true;}
    public void Dispose(){capture.DataAvailable-=Data;capture.RecordingStopped-=Stopped;capture.StopRecording();capture.Dispose();device.Dispose();}
}
internal sealed class MixerEndpoint : IDisposable
{
    private readonly MMDevice? device;
    private readonly IWavePlayer player;
    private readonly SampleRing ring=new(48000,2);
    private bool started;
    public string Id {get;}
    public string Fingerprint {get;}
    public bool Failed {get;private set;}
    public long Underruns=>ring.Underruns;
    public long Dropped=>ring.Dropped;
    public int ActualBufferSamples {get;}
    public MixerEndpoint(AudioOutputBus output,string resolvedId)
    {
        Id=output.Id;Fingerprint=$"{output.Backend}:{resolvedId}:{output.BufferMs}";
        if(output.Backend=="MME"){player=new WaveOutEvent{DeviceNumber=int.Parse(resolvedId),DesiredLatency=output.BufferMs,NumberOfBuffers=3};ActualBufferSamples=output.BufferMs*48;}
        else if(output.Backend=="ASIO"){var asio=new AsioOut(resolvedId);player=asio;ActualBufferSamples=asio.FramesPerBuffer;}
        else{using var e=new MMDeviceEnumerator();device=e.GetDevice(resolvedId);player=new WasapiOut(device,AudioClientShareMode.Shared,true,output.BufferMs);ActualBufferSamples=output.BufferMs*48;}
        try{player.Init(ring.ToWaveProvider());if(player is AsioOut initializedAsio)ActualBufferSamples=initializedAsio.FramesPerBuffer;player.PlaybackStopped+=(_,e)=>{if(e.Exception!=null)Failed=true;};}
        catch{player.Dispose();device?.Dispose();throw;}
    }
    public void Write(ReadOnlySpan<float> samples)
    {
        if(Failed)return;
        if(ring.Count>48000/4)ring.Clear();ring.Write(samples);
        if(!started&&ring.Count>=2880){started=true;ThreadPool.QueueUserWorkItem(_=>{try{player.Play();}catch{Failed=true;}});}
    }
    public void Dispose(){Failed=true;player.Stop();player.Dispose();device?.Dispose();}
}
