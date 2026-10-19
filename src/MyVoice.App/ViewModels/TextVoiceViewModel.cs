using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyVoice.AI;
namespace MyVoice.App.ViewModels;
public sealed record TextVoiceLanguage(string Id,string Name);
public partial class MainViewModel
{
    [ObservableProperty] private string voiceText="";
    [ObservableProperty] private string textVoiceLanguage="fr";
    [ObservableProperty] private double textVoiceExpression=.5;
    [ObservableProperty] private string textVoiceStatus="";
    public bool TextVoiceInstalled=>TextVoiceBackend.Installed;
    public TextVoiceLanguage[] TextVoiceLanguages {get;}=[new("fr","Français"),new("en","English"),new("pt","Português"),new("es","Español"),new("de","Deutsch"),new("it","Italiano")];
    private void TextVoiceProgress(string message)
    {
        store.Log("Text Voice: "+message);
        if(message.StartsWith("Text to Voice ")&&message.Contains('/'))System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(()=>TextVoiceStatus=T["TextVoiceGenerating"]+" "+message[14..]));
    }
    [RelayCommand] private async Task InstallTextVoiceAsync()
    {
        if(AiBusy)return;AiBusy=true;BeginStudio();TextVoiceStatus=T["TextVoiceInstalling"];
        try{await TextVoiceBackend.InstallAsync(AiPython,TextVoiceProgress,StudioToken);OnPropertyChanged(nameof(TextVoiceInstalled));TextVoiceStatus=T["TextVoiceReady"];}
        catch(OperationCanceledException){TextVoiceStatus=T["WorkflowCancelled"];}catch(Exception e){TextVoiceStatus=e.Message;}finally{EndStudio();AiBusy=false;}
    }
    [RelayCommand] private async Task GenerateVoiceTextAsync()
    {
        if(AiBusy)return;if(SelectedAiVoice==null){TextVoiceStatus=T["SelectVoiceFirst"];return;}if(string.IsNullOrWhiteSpace(VoiceText)){TextVoiceStatus=T["EnterVoiceText"];return;}
        if(!TextVoiceInstalled){TextVoiceStatus=T["TextVoiceInstallFirst"];return;}
        var reference=SelectedAiVoice.ReferenceFile;var text=VoiceText;StopAi();if(liveWorker!=null)await liveWorker;
        AiBusy=true;BeginStudio();TextVoiceStatus=T["TextVoiceGenerating"];
        try{var output=WorkflowFile("text.wav");await TextVoiceBackend.GenerateAsync(Path.Combine(AppContext.BaseDirectory,"voice-engine","text_voice.py"),text,TextVoiceLanguage,reference,output,AiDevice,AiCpuThreads,TextVoiceExpression,TextVoiceProgress,StudioToken);lastConvertedFile=output;OnPropertyChanged(nameof(HasAudioResult));TextVoiceStatus=T["AudioResultReady"];}
        catch(OperationCanceledException){TextVoiceStatus=T["WorkflowCancelled"];}catch(Exception e){TextVoiceStatus=e.Message;}finally{EndStudio();AiBusy=false;}
    }
}
