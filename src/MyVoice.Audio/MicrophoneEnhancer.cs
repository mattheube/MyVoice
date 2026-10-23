using System.Runtime.InteropServices;

namespace MyVoice.Audio;

public enum MicrophoneEnhanceMode { Off, Light, Balanced, Strong }

// RNNoise denoises a 10 ms / 48 kHz mono frame. The dry contribution is delayed
// by one frame to match RNNoise's analysis/synthesis latency and avoid comb filtering.
public sealed class MicrophoneEnhancer : IDisposable
{
    private readonly float[] input=new float[480],wet=new float[480],dry=new float[480];
    private IntPtr state;
    private int mode;
    private float blend,lastSample;
    private bool wasActive,failed;
    public MicrophoneEnhanceMode Mode {get=>(MicrophoneEnhanceMode)Volatile.Read(ref mode);set=>Volatile.Write(ref mode,Enum.IsDefined(value)?(int)value:0);}
    public bool Failed=>failed;
    public static bool Available{get;}=Probe();
    private static bool Probe(){try{return FrameSize()==480;}catch(Exception e) when(e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException){return false;}}
    public void Process(float[] samples)
    {
        if(samples.Length!=480)throw new ArgumentException("Microphone enhancement requires a 480-sample frame.");
        var selected=Mode;
        if(selected==MicrophoneEnhanceMode.Off||!Available||failed)
        {
            if(wasActive)
            {
                for(int i=0;i<64;i++){float w=(i+1)/64f;samples[i]=lastSample*(1-w)+samples[i]*w;}
                Reset();
            }
            return;
        }
        if(state==IntPtr.Zero){state=Create(IntPtr.Zero);if(state==IntPtr.Zero){failed=true;return;}}
        for(int i=0;i<480;i++)input[i]=(float.IsFinite(samples[i])?Math.Clamp(samples[i],-1f,1f):0)*32768f;
        ProcessFrame(state,wet,input);
        float target=selected switch{MicrophoneEnhanceMode.Light=>.35f,MicrophoneEnhanceMode.Balanced=>.65f,_=>1f};
        for(int i=0;i<480;i++)
        {
            float amount=blend+(target-blend)*(i+1)/480f;
            float cleaned=float.IsFinite(wet[i])?wet[i]/32768f:0;
            samples[i]=Math.Clamp(dry[i]*(1-amount)+cleaned*amount,-1f,1f);
            dry[i]=input[i]/32768f;
        }
        blend=target;wasActive=true;lastSample=samples[^1];
    }
    private void Reset(){if(state!=IntPtr.Zero){Destroy(state);state=IntPtr.Zero;}Array.Clear(dry);blend=0;wasActive=false;lastSample=0;}
    public void Dispose()=>Reset();
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
    [DllImport("myvoice_rnnoise.dll",EntryPoint="rnnoise_get_frame_size",CallingConvention=CallingConvention.Cdecl)]private static extern int FrameSize();
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
    [DllImport("myvoice_rnnoise.dll",EntryPoint="rnnoise_create",CallingConvention=CallingConvention.Cdecl)]private static extern IntPtr Create(IntPtr model);
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
    [DllImport("myvoice_rnnoise.dll",EntryPoint="rnnoise_destroy",CallingConvention=CallingConvention.Cdecl)]private static extern void Destroy(IntPtr state);
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
    [DllImport("myvoice_rnnoise.dll",EntryPoint="rnnoise_process_frame",CallingConvention=CallingConvention.Cdecl)]private static extern float ProcessFrame(IntPtr state,[Out] float[] output,[In] float[] input);
}
