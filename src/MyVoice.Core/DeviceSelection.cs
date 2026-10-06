namespace MyVoice.Core;
public static class DeviceSelection
{
    public static bool IsVirtual(AudioDevice d) => new[] { "cable", "voicemeeter", "voicemod", "virtual", "streaming" }.Any(s => d.Name.Contains(s, StringComparison.OrdinalIgnoreCase));
    public static bool IsCableOutput(AudioDevice d) => d.Name.Contains("CABLE", StringComparison.OrdinalIgnoreCase) && d.Name.Contains("Input", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("Voicemeeter Input", StringComparison.OrdinalIgnoreCase);
    public static AudioDevice? Physical(IReadOnlyList<AudioDevice> devices, string? preferred) => devices.FirstOrDefault(d => d.Id.Length > 0 && d.Id == preferred) ?? devices.FirstOrDefault(d => !IsVirtual(d) && d.Id.Length > 0);
    public static AudioDevice? Virtual(IReadOnlyList<AudioDevice> devices, string? preferred)
    {
        var prior = devices.FirstOrDefault(d => d.Id == preferred && d.Id.Length > 0);
        if (prior != null) return prior;
        var candidates = devices.Where(IsCableOutput).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }
}
