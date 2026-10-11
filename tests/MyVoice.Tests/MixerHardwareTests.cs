using System.Diagnostics;
using MyVoice.Audio;
using MyVoice.Core;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
public static class MixerHardwareTests
{
    public static async Task PlayProbe(string[] args)
    {
        using var e=new MMDeviceEnumerator();using var d=e.GetDevice(args[1]);using var player=new WasapiOut(d,AudioClientShareMode.Shared,true,30);
        player.Init(new SignalGenerator(48000,2){Type=SignalGeneratorType.Sin,Frequency=double.Parse(args[2],System.Globalization.CultureInfo.InvariantCulture),Gain=.008}.ToWaveProvider());
        player.Play();await Task.Delay(12000);player.Stop();
    }
    private static Process Probe(string endpoint,int frequency)
    {
        var exe=Environment.ProcessPath!;var start=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true};
        if(Path.GetFileNameWithoutExtension(exe).Equals("dotnet",StringComparison.OrdinalIgnoreCase))start.ArgumentList.Add(typeof(MixerHardwareTests).Assembly.Location);
        start.ArgumentList.Add("--mixer-probe");start.ArgumentList.Add(endpoint);start.ArgumentList.Add(frequency.ToString());return Process.Start(start)!;
    }
    public static async Task Run()
    {
        using var e=new MMDeviceEnumerator();using var cable=e.EnumerateAudioEndPoints(DataFlow.Render,DeviceState.Active).FirstOrDefault(d=>d.FriendlyName.Contains("CABLE Input",StringComparison.OrdinalIgnoreCase));
        if(cable==null){Console.WriteLine("SKIP Mixer hardware: no VB-CABLE render endpoint");return;}
        using var wanted=Probe(cable.ID,733);using var other=Probe(cable.ID,1231);
        try
        {
            await Task.Delay(700);var ring=new SampleRing(48000*10,2);
            using(var capture=new ProcessAudioCapture(wanted.Id,ring))
            {
                await Task.Delay(1600);if(capture.Failed||ring.Count<4800)throw new Exception("Process capture returned no frames");
                var samples=new float[ring.Count];ring.Take(samples);double Tone(int f){double a=0,b=0;for(int i=0;i<samples.Length/2;i++){double phase=2*Math.PI*f*i/48000;a+=samples[i*2]*Math.Sin(phase);b+=samples[i*2]*Math.Cos(phase);}return 4*Math.Sqrt(a*a+b*b)/samples.Length;}
                var target=Tone(733);var unrelated=Tone(1231);if(target<.002||unrelated>target*.1)throw new Exception($"Process isolation failed: wanted={target}, other={unrelated}");
                Console.WriteLine($"PASS real per-process WASAPI capture: target {target:F4}, unrelated {unrelated:F6}, {samples.Length/2} frames");
            }
            var profile=MixerState.Recommended(new()) with{Channels=[new(){Id="mic",Name="Mic",Type="microphone"},new(){Id="soundboard",Name="Soundboard",Type="soundboard"},new(){Id="loop",Name="System",Type="system",CaptureEnabled=true,DeviceId=cable.ID,DeviceName=cable.FriendlyName}],Outputs=[new(){Id="monitor",Name="Monitor",DeviceMode="Internal"},new(){Id="virtual",Name="Virtual",DeviceMode="Internal"},new(){Id="recording",Name="Recording"}],Routes=[new("loop","recording")]};
            var mixer=new MixerRouter();using var manager=new MixerDeviceManager(mixer);manager.Apply(profile);await Task.Delay(500);var zero=new float[960];
            for(int i=0;i<50;i++){mixer.Render(zero.AsSpan(0,480),zero,zero,zero);await Task.Delay(10);}
            if(mixer.BusMeter("recording").PeakDb < -55)throw new Exception("System audio capture failed");Console.WriteLine("PASS System Audio capture feeds only selected internal recording bus");
            manager.Apply(profile with{Channels=profile.Channels.Select(c=>c.Id=="loop"?c with{DeviceId="missing-endpoint"}:c).ToList()});await Task.Delay(300);
            if(manager.Statuses.Any(s=>s.Id=="loop"&&s.Available))throw new Exception("Missing endpoint silently substituted");Console.WriteLine("PASS missing fixed device stays unavailable without fallback");
            manager.Apply(profile);await Task.Delay(400);if(!manager.Statuses.Any(s=>s.Id=="loop"&&s.Available))throw new Exception("Device did not reconnect");Console.WriteLine("PASS saved endpoint reconnects after restoring selection");
            manager.Apply(profile,false);await Task.Delay(200);if(manager.Statuses.Any(s=>s.Id=="loop"&&s.Available))throw new Exception("Capture continued while disabled");Console.WriteLine("PASS disabling Mixer closes active captures");
        }
        finally{foreach(var child in new[]{wanted,other})if(!child.HasExited)child.Kill();}
    }
}
