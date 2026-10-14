namespace MyVoice.Core;

public sealed record VirtualAudioConnection(string Backend, AudioDevice? Playback, AudioDevice? Capture)
{
    public bool Detected => Playback != null && Capture != null;
}
public interface IVirtualAudioBackend
{
    string Name { get; }
    VirtualAudioConnection Resolve(IReadOnlyList<AudioDevice> playback, IReadOnlyList<AudioDevice> capture, string? preferred);
}
/// <summary>Only the standard stereo cable pair is supported. Never route to speakers or a 16-channel cable by accident.</summary>
public sealed class VBCableBackend : IVirtualAudioBackend
{
    public string Name => "VB-Audio Virtual Cable";
    public static bool IsPlayback(AudioDevice d) =>
        !d.Name.Replace(" ", "").Contains("16ch", StringComparison.OrdinalIgnoreCase)
        && (Matches(d,"CABLE Input") || System.Text.RegularExpressions.Regex.IsMatch(d.Name,
            @"\((?:\d+-\s*)?VB-Audio Virtual Cable\)$",System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            && !d.Name.StartsWith("CABLE Output",StringComparison.OrdinalIgnoreCase));
    private static bool Matches(AudioDevice d, string endpoint) =>
        (d.Name.Equals(endpoint, StringComparison.OrdinalIgnoreCase) || d.Name.StartsWith(endpoint + " (", StringComparison.OrdinalIgnoreCase))
        && !d.Name.Contains("16ch", StringComparison.OrdinalIgnoreCase);
    public VirtualAudioConnection Resolve(IReadOnlyList<AudioDevice> playback, IReadOnlyList<AudioDevice> capture, string? preferred)
    {
        var outputs = playback.Where(IsPlayback).ToArray();
        var inputs = capture.Where(d => Matches(d, "CABLE Output")).ToArray();
        var selected = outputs.FirstOrDefault(d => d.Id == preferred) ?? (outputs.Length == 1 ? outputs[0] : null);
        return new(Name, selected, inputs.Length == 1 ? inputs[0] : null);
    }
}
