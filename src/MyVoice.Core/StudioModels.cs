namespace MyVoice.Core;
public sealed partial class AppSettings
{
    public string? MonitoringDeviceId
    {
        get; set;
    }
    public string? VirtualDeviceId
    {
        get; set;
    }
    public bool HearMyself
    {
        get; set;
    }
    public bool SendMicrophone { get; set; } = true;
    public double MonitoringVolume { get; set; } = .5;
    public bool AllowMultipleSounds
    {
        get; set;
    }
    public double SoundboardVolume { get; set; } = 1;
    public string VoicePreset { get; set; } = "Clean";
    public bool VoiceEnabled { get; set; } = true;
    public double VoiceIntensity { get; set; } = 1;
    public List<string> FavoriteVoices { get; set; } = new();
    public ProcessingSettings Processing { get; set; } = new();
    public string AiPython { get; set; } = "";
    public string AiRepository { get; set; } = "";
    public string AiDevice { get; set; } = "auto";
    public string AiProfile { get; set; } = "Balanced";
    public string? SelectedAiVoice
    {
        get; set;
    }
    public string UpdateManifestUrl { get; set; } = "";
    public bool CheckUpdatesAtStartup {get;set;} = true;
    public bool Notifications { get; set; } = true;
    public double UiScale { get; set; } = 1;
    public Dictionary<string, HotkeyBinding> Shortcuts { get; set; } = new();
    private void ValidateStudio()
    {
        MonitoringVolume = double.IsFinite(MonitoringVolume) ? Math.Max(0,MonitoringVolume) : .5;
        SoundboardVolume = double.IsFinite(SoundboardVolume) ? Math.Max(0,SoundboardVolume) : 1;
        VoiceIntensity = Math.Clamp(VoiceIntensity, 0, 1);
        UiScale = Math.Clamp(UiScale, .85, 1.3);
        Processing ??= new();
        FavoriteVoices ??= new();
        Shortcuts ??= new();
        AiProfile = AiProfile is "Low latency" or "Quality" ? AiProfile : "Balanced";
        AiModel = AiModel is "fast" or "conversation" or "studio" ? AiModel : "conversation";
        AiFileModel = AiFileModel is "fast" or "conversation" or "studio" ? AiFileModel : "studio";
        AiExpression = AiExpression is "source" or "reference"?AiExpression:"adaptive";
        AiDevice = AiDevice is "cpu" or "cuda" ? AiDevice : "auto";
        AiSimilarity=Math.Clamp(double.IsFinite(AiSimilarity)?AiSimilarity:.7,0,1);
    }
}
public record HotkeyBinding(uint Modifiers, uint Key);
public sealed record ProcessingSettings
{
    public bool Gate { get; init; } = true;
    public double GateThreshold { get; init; } = -48;
    public double GateAttackMs { get; init; } = 5;
    public double GateHoldMs { get; init; } = 100;
    public double GateReleaseMs { get; init; } = 120;
    public int NoiseSuppression { get; init; } = 1;
    public bool AutoGain
    {
        get; init;
    }
    public bool Compressor { get; init; } = true;
    public double CompressorThreshold { get; init; } = -18;
    public double CompressorRatio { get; init; } = 3;
    public double CompressorAttackMs { get; init; } = 8;
    public double CompressorReleaseMs { get; init; } = 150;
    public double MakeupDb
    {
        get; init;
    }
    public double BassDb
    {
        get; init;
    }
    public double MidDb
    {
        get; init;
    }
    public double TrebleDb
    {
        get; init;
    }
}
public sealed class SoundItem
{
    [System.Text.Json.Serialization.JsonExtensionData] public Dictionary<string,System.Text.Json.JsonElement>? AdditionalData {get;set;}
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Sound";
    public string Folder { get; set; } = "My sounds";
    public string File { get; set; } = "";
    public string? Cover
    {
        get; set;
    }
    public double Volume { get; set; } = 1;
    public double BassDb { get; set; }
    public double Saturation { get; set; }
    public bool HearMyself { get; set; } = true;
    public bool SendToMic { get; set; } = true;
    public bool Loop
    {
        get; set;
    }
    public string PlaybackMode { get; set; } = "Play once";
    public HotkeyBinding? Hotkey
    {
        get; set;
    }
}
public sealed class SoundFolder
{
    [System.Text.Json.Serialization.JsonExtensionData] public Dictionary<string,System.Text.Json.JsonElement>? AdditionalData {get;set;}
    public string Name { get; set; } = "My sounds";
    public string? Cover
    {
        get; set;
    }
    public override string ToString() => Name;
}
public sealed class SoundLibrary
{
    [System.Text.Json.Serialization.JsonExtensionData] public Dictionary<string,System.Text.Json.JsonElement>? AdditionalData {get;set;}
    public List<SoundFolder> Folders { get; set; } = [new()];
    public List<SoundItem> Sounds { get; set; } = new();
}
public sealed partial class VoiceReference
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Voice";
    public string ReferenceFile { get; set; } = "";
    public string? Cover
    {
        get; set;
    }
    public double Duration
    {
        get; set;
    }
    public double PeakDb
    {
        get; set;
    }
    public double RmsDb
    {
        get; set;
    }
    public DateTime Created { get; set; } = DateTime.Now;
    public override string ToString() => Name;
}
public delegate void AudioSamplesHandler(ReadOnlySpan<float> samples);
