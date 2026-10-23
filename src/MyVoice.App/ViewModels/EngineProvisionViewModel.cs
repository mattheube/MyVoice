using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyVoice.AI;

namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    [ObservableProperty] private string engineProvisionStatus="";
    private Task? engineProvisionTask;
    [RelayCommand] private async Task RepairSupportedEnginesAsync()
    {
        if(AiBusy)return;
        var resume=Settings.ActiveAiVoice&&VoiceEnabled;
        StopAi();AiBusy=true;
        bool repaired=false;
        try{await PrepareSupportedEnginesAsync(true);repaired=true;}
        catch(OperationCanceledException){}
        catch(Exception e){EngineProvisionStatus=T["EnginesRepairNeeded"]+" "+e.Message;store.Log(e.ToString());}
        finally{AiBusy=false;}
        if(repaired&&resume&&!lifetime.IsCancellationRequested)await UseAiVoiceAsync(ResumeAiVoice);
    }
    public Task PrepareEnginesInBackgroundAsync()=>engineProvisionTask??=PrepareEnginesAtStartupAsync();
    private async Task PrepareEnginesAtStartupAsync()
    {
        try
        {
            if(ModelProvisioner.Complete){EngineProvisionStatus=T["EnginesOnDisk"];return;}
            while(AiBusy)await Task.Delay(500,lifetime.Token);
            AiBusy=true;
            try{await PrepareSupportedEnginesAsync(false); }
            finally{AiBusy=false;}
        }
        catch(OperationCanceledException){}
        catch(Exception e){EngineProvisionStatus=T["EnginesRepairNeeded"]+" "+e.Message;store.Log("Automatic engine preparation: "+e);}
    }
    private async Task PrepareSupportedEnginesAsync(bool verify)
    {
        EngineProvisionStatus=T["EnginesPreparing"];
        void Progress(string line)
        {
            EngineRepairLog(line);
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(()=>EngineProvisionStatus=line));
        }
        var runtime=await EngineBootstrapper.EnsureRuntimeAsync(AiPython,AiRepository,
            Path.Combine(AppContext.BaseDirectory,"voice-engine","SetupEngine.ps1"),Progress,lifetime.Token);
        AiPython=runtime.Python;AiRepository=runtime.Repository;
        await EngineBootstrapper.EnsureConversationAsync(AiPython,Progress,lifetime.Token);
        await TextVoiceBackend.EnsureAsync(AiPython,Progress,lifetime.Token);
        await ModelProvisioner.EnsureAsync(AiPython,AiRepository,"all",Progress,lifetime.Token,verify);
        EngineProvisionStatus=T["EnginesOnDisk"];OnPropertyChanged(nameof(TextVoiceInstalled));Save();
    }
}
