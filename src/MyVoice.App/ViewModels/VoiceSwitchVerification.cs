using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyVoice.Core;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    internal async Task VerifyVoiceSwitchAsync(string reference,string report)
    {
        if(enableOutputs)throw new InvalidOperationException("Requires isolated audio outputs");
        StopAi();Settings.ActiveAiVoice=false;AiModel="fast";Muted=true;
        var first=new VoiceReference{Name="Switch A",ReferenceFile=reference};var last=new VoiceReference{Name="Switch B",ReferenceFile=reference};AiVoices.Add(first);AiVoices.Add(last);
        try
        {
            await StartAiCommand.ExecuteAsync(null);if(!AiReady)throw new Exception(AiState);
            var a=UseAiVoiceAsync(first);await Task.Delay(10);var b=UseAiVoiceAsync(last);await Task.WhenAll(a,b);
            if(!AiLive||activeAiVoiceId!=last.Id||DisplayVoice!="Switch B"||Settings.LastAiVoiceId!=last.Id)throw new Exception("Latest requested voice was not committed");
            SelectedAiVoice=first;VoiceEnabled=false;VoiceEnabled=true;
            if(UseAiVoiceCommand.ExecutionTask is {} resume)await resume;
            if(!AiLive||activeAiVoiceId!=last.Id||DisplayVoice!="Switch B")throw new Exception("Browsing changed the voice resumed by the dock");
            var pending=UseAiVoiceAsync(first);await Task.Delay(10);SelectPreset(Presets.First(p=>p.Name=="Clean"));await pending;
            if(AiLive||DisplayVoice!="Default Clean"||Settings.ActiveAiVoice)throw new Exception("Obsolete voice load overrode Default Clean");
            File.WriteAllText(report,"PASS real engine: newest switch wins; browsing preserves resumed voice; Default Clean cancels an in-flight activation.");
        }
        finally{StopLive();if(liveWorker!=null)await liveWorker;StopAi();}
    }
}
