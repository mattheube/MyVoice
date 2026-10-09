using MyVoice.Core;
using NAudio.Dsp;
namespace MyVoice.Audio;
public sealed class VoiceProcessor
{
    public ProcessingSettings Settings { get; set; } = new();
    public DesignedVoice? Designed {get;set;}
    private DesignedVoice? lastDesigned;
    private BiQuadFilter? designedBass,designedMid,designedTreble;
    private readonly SmbPitchShifter designedShifter=new();
    private readonly float[] designedDelay=new float[48000];
    private int designedPosition;
    public string Preset { get; set; } = "Clean";
    public bool Enabled { get; set; } = true;
    public double Intensity { get; set; } = 1;
    private ProcessingSettings? applied;
    private BiQuadFilter bass = BiQuadFilter.LowShelf(48000, 180, 1, 0), mid = BiQuadFilter.PeakingEQ(48000, 1400, .7f, 0), treble = BiQuadFilter.HighShelf(48000, 5000, 1, 0);
    private readonly BiQuadFilter radioHi = BiQuadFilter.HighPassFilter(48000, 350, .7f), radioLo = BiQuadFilter.LowPassFilter(48000, 3200, .7f);
    private readonly SmbPitchShifter shifter = new();
    private readonly SpectralDenoiser denoiser = new();
    private readonly float[] dry = new float[480], delay = new float[48000];
    private int delayIndex, hold;
    private double envelope, gateGain = 1, compressionGain = 1, autoGain = 1, phase;
    public void Process(float[] samples, bool pre = true, bool post = true, bool effect = true)
    {
        var s = Settings;
        if (applied != s)
        {
            bass = BiQuadFilter.LowShelf(48000, 180, 1, (float)s.BassDb);
            mid = BiQuadFilter.PeakingEQ(48000, 1400, .7f, (float)s.MidDb);
            treble = BiQuadFilter.HighShelf(48000, 5000, 1, (float)s.TrebleDb);
            applied = s;
        }
        double attack = Math.Exp(-1 / (48000 * Math.Max(.001, s.GateAttackMs / 1000))), release = Math.Exp(-1 / (48000 * Math.Max(.001, s.GateReleaseMs / 1000)));
        for (int i = 0; i < samples.Length; i++)
        {
            double x = samples[i];
            if(pre)
            {
            envelope = Math.Max(Math.Abs(x), envelope * .999);
            if (Calibration.Db(envelope) > s.GateThreshold)
                hold = (int)(s.GateHoldMs * 48);
            else if (hold > 0)
                hold--;
            double target = !s.Gate || hold > 0 ? 1 : 0;
            gateGain = target + (gateGain - target) * (target > gateGain ? attack : release);
            x *= gateGain;
            if (s.NoiseSuppression > 0)
                x = denoiser.Process((float)x, s.NoiseSuppression);
            if (s.AutoGain && envelope > .008)
            {
                double desired = Math.Clamp(.1 / envelope, .5, 4);
                autoGain += (desired - autoGain) * .000005;
                x *= autoGain;
            }
            }
            if(post)
            {
            x = treble.Transform(mid.Transform(bass.Transform((float)x)));
            var db = Calibration.Db(Math.Abs(x));
            var reduction = s.Compressor && db > s.CompressorThreshold ? (s.CompressorThreshold + (db - s.CompressorThreshold) / Math.Max(1, s.CompressorRatio) - db) : 0;
            var goal = Math.Pow(10, reduction / 20);
            var coefficient = Math.Exp(-1 / (48 * Math.Max(1, goal < compressionGain ? s.CompressorAttackMs : s.CompressorReleaseMs)));
            compressionGain = goal + (compressionGain - goal) * coefficient;
            x *= compressionGain * Math.Pow(10, s.MakeupDb / 20);
            }
            samples[i] = (float)x;
            dry[i] = (float)x;
        }
        if (!effect || !Enabled)return;
        if(Preset=="Clean"){ApplyDesigned(samples);return;}
        var pitch = Preset switch
        {
            "Deep" => .75f,
            "High" => 1.35f,
            "Demon" => .62f,
            "Tiny" => 1.6f,
            _ => 1f
        };
        if (pitch != 1)
            shifter.PitchShift(pitch, samples.Length, 1024, 4, 48000, samples);
        for (int i = 0; i < samples.Length; i++)
        {
            float x = samples[i];
            phase += 2 * Math.PI * 75 / 48000;
            if (phase > 2 * Math.PI)
                phase -= 2 * Math.PI;
            if (Preset == "Robot")
                x *= (float)Math.Sin(phase);
            if (Preset is "Radio" or "Walkie Talkie" or "Megaphone")
                x = radioLo.Transform(radioHi.Transform(x));
            if (Preset is "Walkie Talkie" or "Megaphone" or "Demon")
                x = (float)Math.Tanh(x * (Preset == "Demon" ? 4 : 7)) * .5f;
            int tap = (delayIndex + delay.Length - 14400) % delay.Length;
            var echo = delay[tap];
            delay[delayIndex] = x + echo * .25f;
            delayIndex = (delayIndex + 1) % delay.Length;
            if (Preset is "Echo" or "Demon")
                x += echo * (Preset == "Echo" ? .45f : .18f);
            samples[i] = (float)(dry[i] * (1 - Intensity) + x * Intensity);
        }
        ApplyDesigned(samples);
    }
    private void ApplyDesigned(float[] samples)
    {
        var d=Designed;if(d==null)return;
        if(lastDesigned!=d){designedBass=BiQuadFilter.LowShelf(48000,180,1,(float)Math.Clamp(d.Bass,-12,12));designedMid=BiQuadFilter.PeakingEQ(48000,1400,.7f,(float)Math.Clamp(d.Mid,-12,12));designedTreble=BiQuadFilter.HighShelf(48000,5000,1,(float)Math.Clamp(d.Treble,-12,12));Array.Clear(designedDelay);lastDesigned=d;}
        Array.Copy(samples,dry,samples.Length);
        if(d.Pitch!=1)designedShifter.PitchShift((float)Math.Clamp(d.Pitch,.5,2),samples.Length,1024,4,48000,samples);
        for(int i=0;i<samples.Length;i++){float x=designedTreble!.Transform(designedMid!.Transform(designedBass!.Transform(samples[i])));if(d.Distortion>0)x=(float)(Math.Tanh(x*(1+Math.Clamp(d.Distortion,0,1)*10))/(1+Math.Clamp(d.Distortion,0,1)*2));var echo=designedDelay[(designedPosition+33600)%48000];designedDelay[designedPosition]=x+echo*.2f;designedPosition=(designedPosition+1)%48000;x+=echo*(float)Math.Clamp(d.Delay,0,1);samples[i]=(float)(dry[i]*(1-Math.Clamp(d.Mix,0,1))+x*Math.Clamp(d.Mix,0,1));}
    }
}
/// <summary>Local spectral subtraction, 1024-point STFT / 256-sample hop. No model or cloud.</summary>
public sealed class SpectralDenoiser
{
    private const int N = 1024, H = 256;
    private readonly float[] input = new float[N], overlap = new float[N], output = new float[H], noise = new float[N], window = new float[N];
    private readonly Complex[] fft = new Complex[N];
    private int position;
    private bool primed;
    public SpectralDenoiser()
    {
        for (int i = 0; i < N; i++)
            window[i] = (float)(.5 - .5 * Math.Cos(2 * Math.PI * i / N));
    }
    public float Process(float x, int strength)
    {
        float result = output[position];
        input[N - H + position] = x;
        if (++position < H)
            return result;
        position = 0;
        for (int i = 0; i < N; i++)
        {
            fft[i].X = input[i] * window[i];
            fft[i].Y = 0;
        }
        FastFourierTransform.FFT(true, 10, fft);
        for (int i = 0; i < N; i++)
        {
            float power = fft[i].X * fft[i].X + fft[i].Y * fft[i].Y;
            if (!primed)
                noise[i] = power * .5f;
            else
                noise[i] = power < noise[i] ? noise[i] * .85f + power * .15f : noise[i] * .995f + power * .005f;
            float gain = Math.Max(.12f, 1 - (.7f + strength * .6f) * noise[i] / Math.Max(power, 1e-12f));
            fft[i].X *= gain;
            fft[i].Y *= gain;
        }
        primed = true;
        FastFourierTransform.FFT(false, 10, fft);
        for (int i = 0; i < N; i++)
            overlap[i] += fft[i].X * window[i] / 1.5f;
        Array.Copy(overlap, output, H);
        Array.Copy(overlap, H, overlap, 0, N - H);
        Array.Clear(overlap, N - H, H);
        Array.Copy(input, H, input, 0, N - H);
        return result;
    }
}
public sealed class PeakLimiter
{
    private float gain = 1;
    public void Process(Span<float> samples)
    {
        float peak = 0;
        foreach (var x in samples)
            peak = Math.Max(peak, Math.Abs(x));
        float target = peak > .92f ? .92f / peak : 1;
        gain = target < gain ? target : Math.Min(target, gain + .005f);
        for (int i = 0; i < samples.Length; i++)
            samples[i] = float.IsFinite(samples[i]) ? Math.Clamp(samples[i] * gain, -.98f, .98f) : 0;
    }
}
