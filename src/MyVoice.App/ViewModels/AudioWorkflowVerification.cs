using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyVoice.Core;
using NAudio.Wave;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    internal async Task VerifyAudioWorkflow(string folder)
    {
        var before=SelectedAiVoice?.ReferenceFile;
        var recording=RecordConversionAudioCommand.ExecuteAsync(null);
        await Task.Delay(1800);FinishConversionRecordingCommand.Execute(null);await recording;
        if(!File.Exists(ConversionInput)||ConversionRecording||AiBusy)throw new Exception("Audio recording did not finish: "+WorkflowStatus);
        using(var reader=new AudioFileReader(ConversionInput))if(reader.TotalTime.TotalSeconds<1)throw new Exception("Recording too short");
        if(SelectedAiVoice?.ReferenceFile!=before)throw new Exception("Recording changed the selected voice");
        var profile=graph.Mixer.Configuration;
        graph.ApplyMixer(profile with{Outputs=profile.Outputs.Select(b=>b.Id=="virtual"?b with{DeviceId="@test"}:b).ToList(),Routes=profile.Routes.Select(r=>r.ChannelId=="mic"?r with{Enabled=r.BusId=="virtual"}:r).ToList()},false);
        lastConvertedFile=Path.Combine(folder,"format-source.wav");
        var playback=PlayAudioResultToVoiceCommand.ExecuteAsync(null);double peak=-90,monitor=-90;
        while(!playback.IsCompleted){peak=Math.Max(peak,graph.OutputPeak);monitor=Math.Max(monitor,graph.MonitorPeak);await Task.Delay(10);}
        await playback;if(peak< -35||monitor> -70||graph.VoicePlaybackFrames!=0||AiBusy)throw new Exception($"Result routing failed: virtual={peak}, monitor={monitor}");
        var cancel=PlayAudioResultToVoiceCommand.ExecuteAsync(null);await Task.Delay(120);CancelStudioCommand.Execute(null);await cancel;
        if(StudioWorking||AiBusy||graph.VoicePlaybackFrames!=0)throw new Exception("Playback cancellation retained audio");
        graph.ApplyMixer(profile,false);
        File.WriteAllText(Path.Combine(folder,"audio-workflow-result.txt"),"PASS physical microphone recording in isolated profile; voice reference unchanged; result reaches My Voice mixer bus; headphones excluded; playback cancellation clears queued audio. No virtual-device audio sent during this test.");
    }
}
