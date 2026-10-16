using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wasapi.CoreAudioApi.Interfaces;
using NAudio.Wave;
namespace MyVoice.Audio;
[ComImport,Guid("94EA2B94-E9CC-49E0-C0FF-EE64CA8F5B90"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] public interface IProcessCaptureAgileObject{}
public sealed record ApplicationAudioSource(int ProcessId,string Name,string Label)
{
    public string? ExecutablePath {get;init;}
    public string? PackageFamily {get;init;}
    public string? AppUserModelId {get;init;}
    public bool HasWindow {get;init;}
    public bool HasAudio {get;init;}
    public long Started {get;init;}
    public bool Running=>ProcessId>0;
    public string Group=>Running?"OPEN NOW":"SAVED APPLICATIONS";
    public string ExecutableName=>Path.GetFileName(ExecutablePath??Name+".exe");
    public string Detail=>ExecutableName+(Running?(HasAudio?" · session audio":" · ouverte") :" · hors ligne");
    public string Identity=>!string.IsNullOrEmpty(AppUserModelId)?"app:"+AppUserModelId:!string.IsNullOrEmpty(PackageFamily)?"package:"+PackageFamily:!string.IsNullOrEmpty(ExecutablePath)?"exe:"+ExecutablePath:"name:"+Name;
}
public static class ApplicationAudioCatalog
{
    public static bool Supported=>OperatingSystem.IsWindowsVersionAtLeast(10,0,20348);
    public static IReadOnlyList<ApplicationAudioSource> List()
    {
        var result=new Dictionary<int,ApplicationAudioSource>();using var devices=new MMDeviceEnumerator();
        foreach(var endpoint in devices.EnumerateAudioEndPoints(DataFlow.Render,DeviceState.Active))using(endpoint)
        {
            var sessions=endpoint.AudioSessionManager.Sessions;
            for(int i=0;i<sessions.Count;i++)try{using var session=sessions[i];int pid=(int)session.GetProcessID;if(pid<=0||pid==Environment.ProcessId)continue;using var process=Process.GetProcessById(pid);if(process.ProcessName.Equals("MyVoice",StringComparison.OrdinalIgnoreCase))continue;result[pid]=new(pid,process.ProcessName,process.ProcessName+" · "+pid);}catch(ArgumentException){}catch(System.ComponentModel.Win32Exception){}
        }
        return result.Values.OrderBy(p=>p.Name).ToArray();
    }
}
/// <summary>Windows process loopback, including child processes; no system-audio fallback.</summary>
internal sealed class ProcessAudioCapture : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct ActivationParams {public int Type;public uint ProcessId;public int Mode;}
    [StructLayout(LayoutKind.Explicit,Size=24)] private struct BlobVariant{[FieldOffset(0)]public ushort Type;[FieldOffset(8)]public int Length;[FieldOffset(16)]public IntPtr Data;}
    [DllImport("Mmdevapi.dll",ExactSpelling=true,CharSet=CharSet.Unicode)]private static extern int ActivateAudioInterfaceAsync(string device,ref Guid iid,IntPtr parameters,IActivateAudioInterfaceCompletionHandler callback,out IActivateAudioInterfaceAsyncOperation operation);
    [ComVisible(true),ClassInterface(ClassInterfaceType.None)] private sealed class Completion : IActivateAudioInterfaceCompletionHandler,IProcessCaptureAgileObject
    {
        public readonly TaskCompletionSource<AudioClient> Result=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IntPtr Parameters,Variant;
        public void Free(){var p=Interlocked.Exchange(ref Parameters,IntPtr.Zero);var v=Interlocked.Exchange(ref Variant,IntPtr.Zero);if(p!=IntPtr.Zero)Marshal.FreeHGlobal(p);if(v!=IntPtr.Zero)Marshal.FreeHGlobal(v);}
        public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation operation){try{operation.GetActivateResult(out int hr,out object unknown);Marshal.ThrowExceptionForHR(hr);Result.TrySetResult(new((IAudioClient)unknown));}catch(Exception e){Result.TrySetException(e);}finally{Free();}}
    }
    private static async Task<AudioClient> Activate(int pid)
    {
        if(!ApplicationAudioCatalog.Supported)throw new PlatformNotSupportedException("La capture par application nécessite Windows build 20348 ou plus récent.");
        var handler=new Completion();handler.Parameters=Marshal.AllocHGlobal(12);handler.Variant=Marshal.AllocHGlobal(24);
        Marshal.StructureToPtr(new ActivationParams{Type=1,ProcessId=(uint)pid,Mode=0},handler.Parameters,false);
        Marshal.StructureToPtr(new BlobVariant{Type=65,Length=12,Data=handler.Parameters},handler.Variant,false);
        var iid=typeof(IAudioClient).GUID;
        bool submitted=false;
        try{Marshal.ThrowExceptionForHR(ActivateAudioInterfaceAsync("VAD\\Process_Loopback",ref iid,handler.Variant,handler,out var operation));submitted=true;var client=await handler.Result.Task.WaitAsync(TimeSpan.FromSeconds(5));GC.KeepAlive(operation);return client;}
        catch{if(!submitted||handler.Result.Task.IsCompleted)handler.Free();else _=handler.Result.Task.ContinueWith(t=>{if(t.IsCompletedSuccessfully)t.Result.Dispose();handler.Free();},TaskScheduler.Default);throw;}
    }
    private readonly AudioClient client;
    private readonly AudioCaptureClient capture;
    private readonly AutoResetEvent ready=new(false);
    private readonly CancellationTokenSource stop=new();
    private readonly Thread worker;
    private readonly SampleRing destination;
    public bool Failed {get;private set;}
    public int ProcessId {get;}
    public ProcessAudioCapture(int pid,SampleRing input)
    {
        ProcessId=pid;destination=input;client=Activate(pid).GetAwaiter().GetResult();
        try{client.Initialize(AudioClientShareMode.Shared,AudioClientStreamFlags.Loopback|AudioClientStreamFlags.EventCallback|AudioClientStreamFlags.AutoConvertPcm,0,0,new WaveFormat(48000,16,2),Guid.Empty);client.SetEventHandle(ready.SafeWaitHandle.DangerousGetHandle());capture=client.AudioCaptureClient;client.Start();worker=new Thread(Run){IsBackground=true,Name="MyVoice application capture"};worker.Start();}catch{client.Dispose();ready.Dispose();stop.Dispose();throw;}
    }
    private void Run()
    {
        var shorts=new short[65536];var floats=new float[65536];WaitHandle[] events=[stop.Token.WaitHandle,ready];
        try{while(!stop.IsCancellationRequested){if(WaitHandle.WaitAny(events,250)==0)break;while(capture.GetNextPacketSize()>0){var pointer=capture.GetBuffer(out int frames,out AudioClientBufferFlags flags);try{int count=frames*2;if(count>shorts.Length)throw new InvalidDataException("Paquet de capture excessif");if(flags.HasFlag(AudioClientBufferFlags.Silent))Array.Clear(floats,0,count);else{Marshal.Copy(pointer,shorts,0,count);for(int i=0;i<count;i++)floats[i]=shorts[i]/32768f;}destination.Write(floats.AsSpan(0,count));}finally{capture.ReleaseBuffer(frames);}}}}catch{Failed=true;}
    }
    public void Dispose(){stop.Cancel();worker.Join(2000);client.Stop();client.Dispose();ready.Dispose();stop.Dispose();}
}
