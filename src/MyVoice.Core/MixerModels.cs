namespace MyVoice.Core;

public sealed record ChannelEffects
{
    public double HighPassHz { get; init; }
    public double BassDb { get; init; }
    public double MidDb { get; init; }
    public double TrebleDb { get; init; }
    public bool Gate { get; init; }
    public double GateThreshold { get; init; } = -48;
    public int NoiseReduction { get; init; }
    public bool Compressor { get; init; }
    public double CompressorThreshold { get; init; } = -18;
    public double CompressorRatio { get; init; } = 3;
    public double Delay { get; init; }
    public double Reverb { get; init; }
}
public sealed record AudioChannel
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "Source";
    public string Type { get; init; } = "capture";
    public string DeviceMode { get; init; } = "Fixed";
    public string? DeviceId { get; init; }
    public string? DeviceName { get; init; }
    public string? ApplicationName { get; init; }
    public bool CaptureEnabled { get; init; }
    public double Volume { get; init; } = 1;
    public double GainDb { get; init; }
    public double Pan { get; init; }
    public bool Mute { get; init; }
    public bool Solo { get; init; }
    public ChannelEffects Effects { get; init; } = new();
}
public sealed record AudioOutputBus
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "Output";
    public string DeviceMode { get; init; } = "Internal";
    public string? DeviceId { get; init; }
    public string? DeviceName { get; init; }
    public string Backend { get; init; } = "WASAPI";
    public int BufferMs { get; init; } = 30;
    public double Volume { get; init; } = 1;
    public bool Mute { get; init; }
}
public sealed record AudioRoute(string ChannelId,string BusId,bool Enabled=true);
public sealed record MidiMapping(string Device,string Kind,int Channel,int Number,string Action,string Target);
public sealed record AudioProfile
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "Mon mix";
    public List<AudioChannel> Channels { get; init; } = [];
    public List<AudioOutputBus> Outputs { get; init; } = [];
    public List<AudioRoute> Routes { get; init; } = [];
}
public sealed class MixerState
{
    public int SchemaVersion { get; set; } = 1;
    public AudioProfile Current { get; set; } = new();
    public List<AudioProfile> Profiles { get; set; } = [];
    public List<MidiMapping> MidiMappings { get; set; } = [];
    public string? MidiDevice { get; set; }
    public string? RecordingFolder { get; set; }
    public string RecordingSource { get; set; } = "recording";
    public bool Advanced { get; set; }
    public static AudioProfile Recommended(AppSettings settings)
    {
        var channels=new List<AudioChannel>{new(){Id="mic",Name="Microphone",Type="microphone",Mute=settings.Muted},new(){Id="soundboard",Name="Soundboard",Type="soundboard",Volume=settings.SoundboardVolume}};
        var buses=new List<AudioOutputBus>{new(){Id="monitor",Name="Headphones",DeviceMode=string.IsNullOrEmpty(settings.MonitoringDeviceId)?"Default":"Fixed",DeviceId=settings.MonitoringDeviceId,Volume=settings.MonitoringVolume},new(){Id="virtual",Name="Virtual Mic",DeviceMode="Fixed",DeviceId=settings.VirtualDeviceId},new(){Id="stream",Name="Stream Mix"},new(){Id="recording",Name="Recording Mix"},new(){Id="master",Name="Master Mix"}};
        var routes=new List<AudioRoute>();foreach(var c in channels)foreach(var b in buses)routes.Add(new(c.Id,b.Id,c.Id!="mic"||(b.Id=="monitor"?settings.HearMyself:b.Id=="virtual"?settings.SendMicrophone:true)));
        return new(){Name="Mon mix",Channels=channels,Outputs=buses,Routes=routes};
    }
}
public sealed record MixerMeter(double PeakDb,double RmsDb,bool Clipping,long Samples)
{
    public static readonly MixerMeter Silent=new(-90,-90,false,0);
}
public sealed record MixerDiagnostics(double AverageBlockMs,double MaximumBlockMs,long LateBlocks,long DroppedSamples,long Underruns,long RenderedBlocks);
public sealed record SourceStatus(string Id,bool Available,string Message,string Format);
