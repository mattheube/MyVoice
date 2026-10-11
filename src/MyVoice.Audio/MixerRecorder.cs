using System.Buffers;
using System.Threading.Channels;
using NAudio.Wave;
namespace MyVoice.Audio;
/// <summary>Bounded, non-blocking producer; the worker owns the WAV file and final header.</summary>
public sealed class MixerRecorder : IDisposable
{
    private sealed class Session
    {
        public readonly Channel<float[]> Queue=Channel.CreateBounded<float[]>(new BoundedChannelOptions(200){FullMode=BoundedChannelFullMode.Wait,SingleReader=true});
        public required string Target,Path;
        public Task? Worker;
        public long Samples;
        public long Dropped;
        public string? Error;
    }
    private Session? current,last;
    private readonly object lifecycle=new();
    private Task finishing=Task.CompletedTask;
    public bool Recording=>Volatile.Read(ref current)!=null;
    public string? Error=>current?.Error??last?.Error;
    public string? FilePath=>current?.Path??last?.Path;
    public double Duration=>(current?.Samples??last?.Samples??0)/96000d;
    public long Bytes=>44+(current?.Samples??last?.Samples??0)*4;
    public void Start(string path,string target)
    {
        lock(lifecycle){
        if(Recording||!finishing.IsCompleted)throw new InvalidOperationException("L’enregistrement précédent doit être terminé avant de recommencer.");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        var writer=new WaveFileWriter(path,WaveFormat.CreateIeeeFloatWaveFormat(48000,2));
        var session=new Session{Target=target,Path=path};last=session;
        Volatile.Write(ref current,session);
        finishing=session.Worker=Task.Run(async()=>{try{await foreach(var block in session.Queue.Reader.ReadAllAsync()){try{if(session.Samples*4+3840>uint.MaxValue-4096)throw new IOException("Limite de 4 Go du format WAV atteinte.");writer.WriteSamples(block,0,960);Interlocked.Add(ref session.Samples,960);}finally{ArrayPool<float>.Shared.Return(block);}}}catch(Exception e){session.Error="Enregistrement arrêté : "+e.Message;Interlocked.CompareExchange(ref current,null,session);session.Queue.Writer.TryComplete();while(session.Queue.Reader.TryRead(out var block))ArrayPool<float>.Shared.Return(block);}finally{try{writer.Dispose();}catch(Exception e){session.Error="Finalisation WAV impossible : "+e.Message;}}});
        }
    }
    public void Offer(string source,ReadOnlySpan<float> samples)
    {
        var session=Volatile.Read(ref current);if(session==null||session.Target!=source||samples.Length!=960)return;
        var block=ArrayPool<float>.Shared.Rent(960);samples.CopyTo(block);
        if(!session.Queue.Writer.TryWrite(block)){ArrayPool<float>.Shared.Return(block);if(Interlocked.CompareExchange(ref current,null,session)!=session)return;Interlocked.Increment(ref session.Dropped);session.Error="Enregistrement arrêté : le disque ne suit pas le débit audio.";session.Queue.Writer.TryComplete();}
    }
    public async Task StopAsync(){Task done;lock(lifecycle){var session=Interlocked.Exchange(ref current,null)??last;if(session==null)return;session.Queue.Writer.TryComplete();done=finishing;}await done;}
    public void Dispose()=>StopAsync().GetAwaiter().GetResult();
}
