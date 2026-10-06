using MyVoice.Audio;
using MyVoice.Core;
using MyVoice.Soundboard;
using MyVoice.Infrastructure;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Runtime.InteropServices;
public static class VirtualRoutingTests
{
    public static async Task Run()
    {
        using var devices=new MMDeviceEnumerator();
        var root=Path.Combine(Path.GetTempPath(),"MyVoice-RoutingV2-"+Guid.NewGuid());new LocalStore(root);
        using var sounds=new SoundboardService(root);using var graph=new AudioGraph(sounds);
        var available=graph.Devices();foreach(var d in available)Console.WriteLine("OUTPUT "+d.Name);
        var cable=DeviceSelection.Virtual(available,null);
        var target=cable??available.FirstOrDefault(d=>d.Name.Contains("Razer"));
        if(target==null){Console.WriteLine("SKIP physical/virtual endpoint unavailable");return;}
        var fixture=Path.Combine(root,"probe.wav");
        using(var writer=new WaveFileWriter(fixture,WaveFormat.CreateIeeeFloatWaveFormat(44100,1)))
        {var data=Enumerable.Range(0,44100*2).Select(i=>(float)(.025*Math.Sin(2*Math.PI*733*i/44100))).ToArray();writer.WriteSamples(data,0,data.Length);}
        var id=await sounds.ImportAsync(fixture,CancellationToken.None);var item=sounds.Library.Sounds.First(x=>x.Id==id);item.HearMyself=false;item.SendToMic=true;
        using var output=devices.GetDevice(target.Id);using var loopback=new WasapiLoopbackCapture(output);
        double peak=0;long bytes=0;loopback.DataAvailable+=(_,args)=>{bytes+=args.BytesRecorded;if(loopback.WaveFormat.BitsPerSample==32)foreach(var x in MemoryMarshal.Cast<byte,float>(args.Buffer.AsSpan(0,args.BytesRecorded)))if(float.IsFinite(x))peak=Math.Max(peak,Math.Abs(x));};
        var receiver=devices.EnumerateAudioEndPoints(DataFlow.Capture,DeviceState.Active).FirstOrDefault(d=>d.FriendlyName.Contains("CABLE Output",StringComparison.OrdinalIgnoreCase));
        using var receiverDevice=receiver;
        using var capture=receiver==null?null:new WasapiCapture(receiver);
        double receivedPeak=0,toneSin=0,toneCos=0;long receivedBytes=0,toneFrames=0;
        if(capture!=null){capture.WaveFormat=WaveFormat.CreateIeeeFloatWaveFormat(receiver!.AudioClient.MixFormat.SampleRate,receiver.AudioClient.MixFormat.Channels);capture.DataAvailable+=(_,a)=>{receivedBytes+=a.BytesRecorded;var floats=MemoryMarshal.Cast<byte,float>(a.Buffer.AsSpan(0,a.BytesRecorded));for(int i=0;i<floats.Length;i+=capture.WaveFormat.Channels){var x=floats[i];if(float.IsFinite(x)){receivedPeak=Math.Max(receivedPeak,Math.Abs(x));var phase=2*Math.PI*733*toneFrames/capture.WaveFormat.SampleRate;toneSin+=x*Math.Sin(phase);toneCos+=x*Math.Cos(phase);toneFrames++;}}};capture.StartRecording();}
        loopback.StartRecording();graph.Configure(null,target.Id);sounds.Play(id);await Task.Delay(1200);
        capture?.StopRecording();
        if(cable!=null&&capture!=null){if(receivedBytes==0||receivedPeak<.001||2*Math.Sqrt(toneSin*toneSin+toneCos*toneCos)/Math.Max(1,toneFrames)<.001)throw new Exception("CABLE capture side received no signal");Console.WriteLine($"PASS actual CABLE Input → CABLE Output capture: {receivedBytes} bytes, {Calibration.Db(receivedPeak):0.0} dB; Hear OFF / Send ON");}
        var meter=graph.OutputPeak;loopback.StopRecording();sounds.StopAll();graph.Configure(null,null);
        if(bytes==0||peak<.001||meter < -65)throw new Exception($"Virtual branch failed: {bytes}/{peak}/{meter}");
        Console.WriteLine($"PASS Soundboard 44.1k mono → 48k stereo virtual branch, Hear OFF / Send ON: meter={meter:0.0}dB, loopback={Calibration.Db(peak):0.0}dB");
        if(cable==null)Console.WriteLine("LIMIT: no compatible cable found; virtual render branch tested on headphones, Discord end-to-end unverified.");
        else Console.WriteLine("PASS selected virtual rendering endpoint: "+target.Name+". Discord microphone selection still requires application verification.");
    }
}
