using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    private CancellationTokenSource? engineStartCancellation;
    private void EngineRepairLog(string line)
    {
        store.Log("AI health: "+line);
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(()=>{AiLog+=line+Environment.NewLine;if(AiLog.Length>12000)AiLog=AiLog[^12000..];if(line.StartsWith("CHECKING")||line.StartsWith("REPAIRING")||line.StartsWith("VERIFIED"))AiState=line;}));
    }
    [RelayCommand] private async Task InstallAiRuntimeAsync()
    {
        if(AiBusy)return;StopLive();ai.Stop();AiReady=false;AiBusy=true;AiLog="";
        using var operation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);engineStartCancellation=operation;bool verified=false;
        try
        {
            var runtime=await MyVoice.AI.EngineBootstrapper.EnsureRuntimeAsync(AiPython,AiRepository,Path.Combine(AppContext.BaseDirectory,"voice-engine","SetupEngine.ps1"),EngineRepairLog,operation.Token,true);
            AiPython=runtime.Python;AiRepository=runtime.Repository;
            await MyVoice.AI.EngineBootstrapper.EnsureConversationAsync(AiPython,EngineRepairLog,operation.Token);
            operation.Token.ThrowIfCancellationRequested();Save();verified=true;
        }
        catch(OperationCanceledException){AiState=T["Runtime94"];}
        catch(Exception e){StudioError(e.Message);AiState="ERROR · "+e.Message;}
        finally{if(ReferenceEquals(engineStartCancellation,operation))engineStartCancellation=null;AiBusy=false;}
        if(verified)await StartAiAsync();
    }
}
