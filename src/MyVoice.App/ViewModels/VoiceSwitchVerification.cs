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
        Settings.AutoStartAi=true;StartServices();await Task.Delay(6000);
        if(AiReady||AiBusy||ai.Ready)throw new Exception("Default Clean loaded an idle AI engine");
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
            if(AiLive||AiReady||ai.Ready||DisplayVoice!="Default Clean"||Settings.ActiveAiVoice)throw new Exception("Default Clean retained an unused engine or obsolete activation");
            var remembered=Settings.LastAiVoiceId;var selected=Settings.SelectedAiVoice;
            communityTrialVoice=new VoiceReference{Id="trial-fixture",Name="Community trial",ReferenceFile=reference};
            await UseAiVoiceAsync(communityTrialVoice);
            if(!AiLive||DisplayVoice!="Community trial"||Settings.LastAiVoiceId!=remembered||Settings.SelectedAiVoice!=selected||AiVoices.Contains(communityTrialVoice))throw new Exception("Community trial persisted or failed to activate");
            File.WriteAllText(report,"PASS real engine: newest switch wins; browsing preserves resumed voice; Default Clean cancels an in-flight activation; Community trial activates the actual voice without adding it to the library or replacing remembered voices.");
        }
        finally{StopLive();if(liveWorker!=null)await liveWorker;StopAi();}
    }
}
