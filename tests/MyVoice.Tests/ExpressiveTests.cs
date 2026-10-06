using System.Text.Json;
using MyVoice.AI;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
public static class ExpressiveTests
{
    public static async Task Run(bool cpu,bool styleOnly=false)
    {
        var root=Directory.GetCurrentDirectory();using var cfg=JsonDocument.Parse(File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MyVoice","AI","runtime.json")));
        using var engine=new SeedBackend {Model="studio",Expression="source",CpuThreads=8,Similarity=.85};
        engine.Log+=line=>File.AppendAllText(Path.Combine(root,"work","expressive-errors.log"),line+Environment.NewLine);
        var reference=Path.Combine(root,"work","ai-reference.wav");engine.ReferenceFiles=[reference];
        await engine.StartAsync(cfg.RootElement.GetProperty("Python").GetString()!,cfg.RootElement.GetProperty("Repository").GetString()!,Path.Combine(root,"outputs/MyVoice/voice-engine/worker.py"),cpu?"cpu":"auto",CancellationToken.None);
        await engine.LoadAsync(reference,CancellationToken.None);Console.WriteLine("PASS V2 loaded on "+engine.Device+"; references="+engine.ReferenceCount);
        using var reader=new AudioFileReader(Path.Combine(root,"work","ai-source.wav"));var data=new float[48000*(cpu&&!styleOnly?1:3)];var source=new WdlResamplingSampleProvider(reader,48000);var count=source.Read(data,0,data.Length);Array.Resize(ref data,count);
        foreach(var expression in cpu?new[]{styleOnly?"adaptive":"source"}:new[]{"source","reference","adaptive"})
        {
            engine.Expression=expression;var output=await engine.ConvertChunkAsync(data,30,CancellationToken.None);
            Console.WriteLine($"V2 output frames={output.Length}; peak={output.Select(Math.Abs).DefaultIfEmpty().Max()}");
            if(output.Length<4800||output.Any(x=>!float.IsFinite(x))||output.All(x=>Math.Abs(x)<.0001))throw new Exception("V2 output invalid");
            using var writer=new WaveFileWriter(Path.Combine(root,"work",$"expressive-{(cpu?"cpu":"gpu")}-{expression}.wav"),WaveFormat.CreateIeeeFloatWaveFormat(48000,1));writer.WriteSamples(output,0,output.Length);
            Console.WriteLine($"PASS V2 {expression}: {output.Length/48000d:0.00}s output; {engine.LastSeconds:0.00}s computation");
        }
        engine.Stop();Console.WriteLine("PASS expressive engine stops cleanly");
    }
}
