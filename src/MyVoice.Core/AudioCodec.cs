using System.Diagnostics;
namespace MyVoice.Core;
public static class AudioCodec
{
    public static string Executable => Path.Combine(AppContext.BaseDirectory,"media","ffmpeg.exe");
    public static bool Available => File.Exists(Executable);
    public static void Decode(string source,string destination,CancellationToken token,int channels=1)
    {
        if(channels is not(1 or 2))throw new ArgumentOutOfRangeException(nameof(channels));
        MediaValidation.RequireAudio(source);
        Run(["-protocol_whitelist","file,pipe","-xerror","-i",Path.GetFullPath(source),"-map","0:a:0","-vn","-ar","48000","-ac",channels.ToString(),"-c:a","pcm_f32le",destination],token);
    }
    public static void Export(string source,string destination,string format,int rate,CancellationToken token)
    {
        if(rate is not(44100 or 48000))throw new ArgumentOutOfRangeException(nameof(rate));
        string[] codec=format switch {"wav"=>["-c:a","pcm_s24le"],"flac"=>["-c:a","flac"],"mp3"=>["-c:a","libmp3lame","-b:a","320k"],"ogg"=>["-c:a","libvorbis","-q:a","8"],"opus"=>["-c:a","libopus","-b:a","192k"],_=>throw new InvalidDataException("Unsupported export format")};
        Run(["-i",source,"-vn","-ar",rate.ToString(),..codec,destination],token);
    }
    private static void Run(string[] arguments,CancellationToken token)
    {
        if(!Available)throw new FileNotFoundException("Audio codecs are missing. Repair the MyVoice installation.");
        var info=new ProcessStartInfo(Executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};
        foreach(var a in new[]{"-nostdin","-hide_banner","-v","error","-y"}.Concat(arguments))info.ArgumentList.Add(a);
        using var child=Process.Start(info)??throw new IOException("Cannot start audio decoder.");
        var errors=child.StandardError.ReadToEndAsync();
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromMinutes(5));
        try {child.WaitForExitAsync(timeout.Token).GetAwaiter().GetResult();var error=errors.GetAwaiter().GetResult();if(child.ExitCode!=0)throw new InvalidDataException("Audio conversion failed: "+error[..Math.Min(error.Length,500)]);}
        catch {if(!child.HasExited)child.Kill(true);throw;}
    }
}
