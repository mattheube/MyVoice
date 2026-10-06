using System.Text.Json;
using MyVoice.AI;
using NAudio.Wave;
public static class AiIntegrationTests
{
    public static async Task Run(string device = "auto")
    {
        var root = Directory.GetCurrentDirectory();
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyVoice", "AI", "runtime.json")));
        using var backend = new SeedBackend();
        backend.Log += line => File.AppendAllText(Path.Combine(root, "work", "ai-csharp-errors.txt"), line + Environment.NewLine);
        await backend.StartAsync(config.RootElement.GetProperty("Python").GetString()!, config.RootElement.GetProperty("Repository").GetString()!, Path.Combine(root, "outputs", "MyVoice", "voice-engine", "worker.py"), device, CancellationToken.None);
        if (!backend.Ready)
            throw new Exception("AI not ready");
        Console.WriteLine("PASS C# starts AI: " + backend.Device);
        await backend.LoadAsync(Path.Combine(root, "work", "ai-reference.wav"), CancellationToken.None);
        Console.WriteLine("PASS C# loads reference and warms model");
        var output = Path.Combine(root, "work", "ai-csharp-" + device + ".wav");
        await backend.ConvertFileAsync(Path.Combine(root, "work", "ai-source.wav"), output, 6, CancellationToken.None);
        using (var reader = new AudioFileReader(output))
        {
            if (reader.TotalTime.TotalSeconds < 1)
                throw new Exception("Empty conversion");
            Console.WriteLine($"PASS C# offline conversion: {reader.TotalTime.TotalSeconds:0.00}s in {backend.LastSeconds:0.00}s");
        }
        var input = Enumerable.Range(0, 38400).Select(i => (float)(.02 * Math.Sin(2 * Math.PI * 220 * i / 48000))).ToArray();
        var samples = await backend.ConvertChunkAsync(input, 6, CancellationToken.None);
        if (samples.Length != input.Length || samples.Any(x => !float.IsFinite(x)))
            throw new Exception("Invalid AI response");
        Console.WriteLine($"PASS C# live chunk: {samples.Length} frames in {backend.LastSeconds:0.000}s; RTF={backend.LastSeconds / .8:0.00}");
        if(backend.Device!="CPU")
        {
            var silence=await backend.ConvertChunkAsync(new float[38400],6,CancellationToken.None);
            if(silence.Skip(1920).Any(x=>Math.Abs(x)>.0001))throw new Exception("Non-silent AI pause");
            Console.WriteLine("PASS AI silence remains silent after overlap tail");
            backend.Similarity=0;
            var dry=await backend.ConvertChunkAsync(input,6,CancellationToken.None);
            if(dry.Skip(1920).Zip(input.Skip(960), (a,b)=>Math.Abs(a-b)).Max()>.001)throw new Exception("Similarity zero not dry");
            Console.WriteLine("PASS similarity zero preserves original timbre");
        }
        backend.Stop();
        if (backend.Ready)
            throw new Exception("AI not stopped");
        Console.WriteLine("PASS AI stop releases child process");
        var failing=Path.Combine(root,"work","ai-startup-failure.py");
        File.WriteAllText(failing,"import sys\nprint('MYVOICE_STARTUP_DIAGNOSTIC', file=sys.stderr)\nsys.exit(4)\n");
        try
        {
            await backend.StartAsync(config.RootElement.GetProperty("Python").GetString()!,config.RootElement.GetProperty("Repository").GetString()!,failing,device,CancellationToken.None);
            throw new Exception("Failed worker was treated as ready");
        }
        catch(IOException ex) when(ex.Message.Contains("MYVOICE_STARTUP_DIAGNOSTIC"))
        { Console.WriteLine("PASS startup failure displays the Python diagnostic"); }

    }
}
