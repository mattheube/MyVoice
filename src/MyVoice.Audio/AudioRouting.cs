namespace MyVoice.Audio;
public static class AudioRouting
{
    public static void MixMicrophone(ReadOnlySpan<float> mono, Span<float> local, Span<float> remote, bool muted, bool hear, bool send)
    {
        for (int i = 0; i < mono.Length; i++)
        {
            float x = muted ? 0 : mono[i];
            if (hear)
                local[2 * i] = local[2 * i + 1] = x;
            if (send)
                remote[2 * i] = remote[2 * i + 1] = x;
        }
    }
}
