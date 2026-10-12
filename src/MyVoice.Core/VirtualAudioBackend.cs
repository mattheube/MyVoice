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
    private static bool Matches(AudioDevice d, string endpoint) =>
        (d.Name.Equals(endpoint, StringComparison.OrdinalIgnoreCase) || d.Name.StartsWith(endpoint + " (", StringComparison.OrdinalIgnoreCase))
        && !d.Name.Contains("16ch", StringComparison.OrdinalIgnoreCase);
    public VirtualAudioConnection Resolve(IReadOnlyList<AudioDevice> playback, IReadOnlyList<AudioDevice> capture, string? preferred)
    {
        var outputs = playback.Where(d => Matches(d, "CABLE Input")).ToArray();
        var inputs = capture.Where(d => Matches(d, "CABLE Output")).ToArray();
        var selected = outputs.FirstOrDefault(d => d.Id == preferred) ?? (outputs.Length == 1 ? outputs[0] : null);
        return new(Name, selected, inputs.Length == 1 ? inputs[0] : null);
    }
}
