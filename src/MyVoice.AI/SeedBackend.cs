using System.Diagnostics;
using System.Text.Json;
using System.Runtime.InteropServices;
using MyVoice.Core;
namespace MyVoice.AI;
public sealed class SeedBackend : IDisposable
{
    private Process? process;
    private Task? errorPump;
    private string lastError = "";
    public int ReferenceCount {get;private set;} = 1;
    public string Model {get;set;} = "fast";
    public string Profile {get;set;} = "Balanced";
    public double ChunkSeconds {get;private set;} = .8;
    public double ContextSeconds {get;private set;} = .8;
    public double OverlapSeconds {get;private set;} = .16;
    public double LookaheadSeconds {get;private set;} = .12;
    public double BenchmarkRtf {get;private set;}
    public bool ConversationRecommended {get;private set;}
    public int CurrentSteps {get;private set;}
    public string Expression {get;set;} = "adaptive";
    public string[] ReferenceFiles {get;set;} = [];
    public double Similarity { get; set; } = .7;
    public bool PreserveDynamics { get; set; } = true;
    public bool ReduceArtifacts { get; set; } = true;
    public int CpuThreads { get; set; } = 4;
    public double VramMb { get; private set; }
    public double BenchmarkSeconds { get; private set; }
    public event Action<double>? Progress;
    private readonly SemaphoreSlim serial = new(1, 1);
    public bool Ready
    {
        get; private set;
    }
    public string Device { get; private set; } = "";
    public double LastSeconds
    {
        get; private set;
    }
    public event Action<string>? Log;
    public async Task StartAsync(string python, string repository, string script, string device, CancellationToken token)
    {
        Stop();
        if (ManagedRuntime.RepairLauncher(python)) Log?.Invoke("Python : lanceur local réparé, bibliothèques conservées.");
        if (!File.Exists(python) || !Directory.Exists(repository))
            throw new FileNotFoundException("Configure the local AI runtime first.");
        var start = new ProcessStartInfo(python) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = repository };
        foreach (var arg in new[] { "-u", script, "--repository", repository, "--device", device, "--threads", CpuThreads.ToString(), "--model", Model })
            start.ArgumentList.Add(arg);
        start.Environment["HF_HUB_DISABLE_TELEMETRY"] = "1";
        start.Environment.Remove("PYTHONHOME");
        start.Environment.Remove("PYTHONPATH");
        start.Environment.Remove("__PYVENV_LAUNCHER__");
        process = new Process { StartInfo = start, EnableRaisingEvents = true };
        var launched = process;
        process.Exited += (_, _) => { if (ReferenceEquals(process, launched)) Ready = false; };
        lastError = "";
        Log?.Invoke("Python local : " + python);
        process.Start();
        var child = process;
        errorPump = Task.Run(async () => { try { while (await child.StandardError.ReadLineAsync() is { } line) { lastError=(lastError + " " + line).Trim(); if(lastError.Length>1200)lastError=lastError[^1200..]; Log?.Invoke(line); } } catch (ObjectDisposedException) { } catch (InvalidOperationException) { } });
        try
        {
            using var response = await ReadAsync(TimeSpan.FromMinutes(15), token);
            Ensure(response);
            Ready = response.RootElement.TryGetProperty("state", out var state) && state.GetString() == "ready";
            Device = response.RootElement.GetProperty("gpu").GetString() ?? "CPU";
        }
        catch { Stop(); throw; }
    }
    public async Task LoadAsync(string path, CancellationToken token)
    {
        using var result = await RequestAsync(new
        {
            command = "load",
            path, references = ReferenceFiles, profile=Profile
        }, TimeSpan.FromMinutes(10), token);
        Ensure(result);
        if(result.RootElement.TryGetProperty("hop",out var hop))ChunkSeconds=hop.GetDouble();
        if(result.RootElement.TryGetProperty("context",out var context))ContextSeconds=context.GetDouble();
        if(result.RootElement.TryGetProperty("overlap",out var overlap))OverlapSeconds=overlap.GetDouble();
        if(result.RootElement.TryGetProperty("lookahead",out var look))LookaheadSeconds=look.GetDouble();
        if(result.RootElement.TryGetProperty("rtf",out var rtf))BenchmarkRtf=rtf.GetDouble();
        if(result.RootElement.TryGetProperty("recommended",out var recommended))ConversationRecommended=recommended.GetBoolean();
        if(result.RootElement.TryGetProperty("steps",out var steps))CurrentSteps=steps.GetInt32();
        if(result.RootElement.TryGetProperty("references",out var refs))ReferenceCount=refs.GetInt32();
        if(result.RootElement.TryGetProperty("vram_mb",out var memory)) VramMb=memory.GetDouble();
        if(result.RootElement.TryGetProperty("seconds",out var elapsed)) BenchmarkSeconds=elapsed.GetDouble();
    }
    public async Task ConvertFileAsync(string input, string output, int steps, CancellationToken token)
    {
        using var result = await RequestAsync(new
        {
            command = "file",
            input,
            output,
            steps, expression = Expression, similarity = Similarity, preserve = PreserveDynamics, artifacts = ReduceArtifacts
        }, TimeSpan.FromMinutes(20), token);
        Ensure(result);
        LastSeconds = result.RootElement.GetProperty("seconds").GetDouble();
    }
    public async Task<float[]> ConvertChunkAsync(float[] samples, int steps, CancellationToken token, bool reset=false)
    {
        var bytes = new byte[samples.Length * 4];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        using var result = await RequestAsync(new
        {
            command = "chunk", reset,
            audio = Convert.ToBase64String(bytes),
            steps, expression = Expression, similarity = Similarity, preserve = PreserveDynamics, artifacts = ReduceArtifacts
        }, TimeSpan.FromMinutes(Device == "CPU" ? 15 : 3), token);
        Ensure(result);
        LastSeconds = result.RootElement.GetProperty("seconds").GetDouble();
        if(result.RootElement.TryGetProperty("steps",out var actualSteps))CurrentSteps=actualSteps.GetInt32();
        var output = Convert.FromBase64String(result.RootElement.GetProperty("audio").GetString()!);
        if (output.Length > 48000 * 4 * 60 || output.Length % 4 != 0)
            throw new InvalidDataException("Invalid AI audio response");
        var floats = new float[output.Length / 4];
        Buffer.BlockCopy(output, 0, floats, 0, output.Length);
        return floats;
    }
    private async Task<JsonDocument> RequestAsync(object request, TimeSpan timeout, CancellationToken token)
    {
        await serial.WaitAsync(token);
        try
        {
            if (!Ready || process == null)
                throw new InvalidOperationException("AI engine offline");
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request));
            await process.StandardInput.FlushAsync(token);
            return await ReadAsync(timeout, token);
        }
        catch { Stop(); throw; }
        finally { serial.Release(); }
    }
    private async Task<JsonDocument> ReadAsync(TimeSpan timeout, CancellationToken token)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(token);
        limit.CancelAfter(timeout);
        while(true)
        {
            var line = await process!.StandardOutput.ReadLineAsync(limit.Token);
            if(line==null)
            {
                if(errorPump!=null) { try { await errorPump.WaitAsync(TimeSpan.FromSeconds(2),token); } catch(TimeoutException) { } }
                throw new IOException(string.IsNullOrWhiteSpace(lastError) ? "Le moteur s’est arrêté avant de répondre. Consultez le journal dans Voice Lab." : "Le moteur s’est arrêté : " + lastError);
            }
            var document = JsonDocument.Parse(line);
            if(document.RootElement.TryGetProperty("progress",out var progress)) { Progress?.Invoke(progress.GetDouble()); document.Dispose(); continue; }
            return document;
        }
    }
    private static void Ensure(JsonDocument response)
    {
        if (response.RootElement.TryGetProperty("error", out var error))
            throw new InvalidOperationException(error.GetString());
    }
    public void Stop()
    {
        Ready = false;
        var p = process;
        process = null;
        if (p == null)
            return;
        try
        {
            if (!p.HasExited)
            {
                p.Kill(true);
                p.WaitForExit(3000);
            }
        }
        catch (InvalidOperationException) { }
        finally { p.Dispose(); }
    }
    public void Dispose() => Stop();
}
