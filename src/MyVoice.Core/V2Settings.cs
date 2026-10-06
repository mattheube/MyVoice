using System.Text.Json;
using System.Text.Json.Serialization;
namespace MyVoice.Core;
public sealed partial class AppSettings
{
    public bool LastVoiceIsAi {get;set;}
    public string? LastAiVoiceId {get;set;}
    public string AiModel { get; set; } = "conversation";
    public string AiFileModel {get;set;} = "studio";
    public bool DeveloperMode {get;set;}
    public DateTime? LastUpdateCheck {get;set;}
    public string LastKnownVersion {get;set;} = "";
    public string LastUpdateSource {get;set;} = "";
    public string UpdateChannel {get;set;} = "stable";
    public string AiExpression { get; set; } = "adaptive";
    [JsonExtensionData] public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    public double WindowWidth {get;set;} = 1340;
    public double WindowHeight {get;set;} = 920;
    public bool WindowMaximized {get;set;}
    public string LastModifiedVoice { get; set; } = "Deep";
    public Guid? LastSoundId { get; set; }
    public bool ActiveAiVoice { get; set; }
    public bool AutoStartAi { get; set; } = true;
    public double AiSimilarity { get; set; } = .7;
    public bool AiPreserveDynamics { get; set; } = true;
    public bool AiReduceArtifacts { get; set; } = true;
    public int AiCpuThreads { get; set; } = Math.Clamp(Environment.ProcessorCount / 2, 1, 8);
}
