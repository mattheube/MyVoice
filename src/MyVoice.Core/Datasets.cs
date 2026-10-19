using System.Text.Json;
using System.Text.Json.Serialization;
namespace MyVoice.Core;
public sealed partial class VoiceReference
{
    public string? SourceContentId {get;set;}
    [JsonExtensionData] public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    public string Backend { get; set; } = "Seed-VC";
    public string BackendVersion { get; set; } = "tiny-xlsr / 51383efd";
    public List<string>? PreparedReferenceFiles {get;set;}
    public void EnsurePreparedReferences(){PreparedReferenceFiles ??= Samples.Where(s=>!s.Excluded).SelectMany(s=>s.Segments.OrderByDescending(x=>x.Score).Take(3)).Select(s=>s.File).Distinct().ToList();}
    [JsonIgnore] public string[] ActiveReferenceFiles {get{EnsurePreparedReferences();var files=PreparedReferenceFiles!.Where(File.Exists).ToArray();return files.Length>0?files:[ReferenceFile];}}
    public List<DatasetSample> Samples { get; set; } = new();
    public List<VoiceRevision> Revisions { get; set; } = new();
    public int Revision { get; set; } = 1;
    [JsonIgnore] public double DatasetDuration => Samples.Count == 0 ? Duration : Samples.Sum(s=>s.Duration);
    [JsonIgnore] public double UsableDuration => Samples.Count == 0 ? Duration : Samples.Where(s=>!s.Excluded).Sum(s=>s.UsableSeconds);
    [JsonIgnore] public string Summary => $"{(Samples.Count==0 ? "Legacy reference" : Samples.Count+" files")} · {TimeSpan.FromSeconds(DatasetDuration):hh\\:mm\\:ss} · v{Revision}";
    [JsonIgnore] public bool Ready => File.Exists(ReferenceFile);
}
public sealed class DatasetSample
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string OriginalFile { get; set; } = "";
    public double Duration { get; set; }
    public double UsableSeconds { get; set; }
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public double PeakDb { get; set; }
    public double RmsDb { get; set; }
    public double ClippingRatio { get; set; }
    public string Quality { get; set; } = "";
    public string Notes { get; set; } = "";
    public bool Excluded { get; set; }
    public List<DatasetSegment> Segments { get; set; } = new();
    [JsonIgnore] public string Summary => $"{Duration:0}s · {UsableSeconds:0}s active · {Quality}";
}
public sealed record DatasetSegment(string File, double Seconds, double Score);
public sealed record VoiceRevision(int Number, string ReferenceFile, DateTime Created,string[]? References=null);
