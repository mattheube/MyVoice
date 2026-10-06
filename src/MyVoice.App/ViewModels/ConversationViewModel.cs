using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyVoice.Audio;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    private readonly SampleRing conversationInput=new(96000,trackTimestamps:true);
    private double conversationLatency,conversationQueue;
    private long conversationDrops;
    [ObservableProperty] private string conversationStatus="";
    [ObservableProperty] private bool conversationNeedsDirect;
    public string ConversationMetrics=>$"Bloc {ai.ChunkSeconds*1000:0} ms · contexte {ai.ContextSeconds*1000:0} ms · recouvrement {ai.OverlapSeconds*1000:0} ms · anticipation {ai.LookaheadSeconds*1000:0} ms\nCalcul {ai.LastSeconds*1000:0} ms · RTF {ai.LastSeconds/Math.Max(.01,ai.ChunkSeconds):0.00} · file {conversationQueue:0} ms · blocs écartés {conversationDrops}\nSortie virtuelle, capture → soumission WASAPI : {(conversationLatency>0?conversationLatency.ToString("0")+" ms":"non mesurée")} · étapes {ai.CurrentSteps}";
    [RelayCommand] private void SwitchConversationToDirect(){AiModel="fast";ConversationStatus="Direct sélectionné.";}
    private async Task ConversationLoop(CancellationToken token)
    {
        int count=(int)Math.Round(ai.ChunkSeconds*48000);long seenDrops=conversationInput.Dropped;bool reset=false;int slow=0;
        conversationDrops=0;conversationLatency=0;
        await Application.Current.Dispatcher.InvokeAsync(()=>{ConversationNeedsDirect=!ai.ConversationRecommended;ConversationStatus=ai.ConversationRecommended?"Conversation recommandée par le test de cette voix.":"Conversation ne tient pas le débit avec ces réglages. Direct est recommandé.";AiState="Conversation · écoute continue";});
        try
        {
            while(!token.IsCancellationRequested)
            {
                while(conversationInput.Count<count)await Task.Delay(10,token);
                if(conversationInput.Dropped!=seenDrops){conversationDrops+=(conversationInput.Dropped-seenDrops+count-1)/count;seenDrops=conversationInput.Dropped;reset=true;}
                while(conversationInput.Count>count*2){conversationInput.Take(new float[count]);conversationDrops++;reset=true;}
                var input=new float[count];conversationInput.Take(input);long stamp=conversationInput.LastReadTimestamp;
                conversationQueue=conversationInput.Count/48d;var generation=Volatile.Read(ref aiGeneration);
                var output=await ai.ConvertChunkAsync(input,ai.CurrentSteps,token,reset);token.ThrowIfCancellationRequested();
                if(Muted||generation!=Volatile.Read(ref aiGeneration))Array.Clear(output);
                var age=stamp>0?(Stopwatch.GetTimestamp()-stamp)/(double)Stopwatch.Frequency:0;
                if(age>2.5){conversationDrops++;reset=true;slow++;}
                else
                {
                    if(graph.ConvertedFrames>count*2){graph.FlushConverted();conversationDrops++;reset=true;}
                    if(reset){graph.FlushConverted();for(int i=0;i<Math.Min(960,output.Length);i++)output[i]*=i/960f;}
                    long origin=stamp>0?stamp-(long)((ai.LookaheadSeconds+ai.OverlapSeconds)*Stopwatch.Frequency):0;
                    graph.PushConverted(output,origin);reset=false;slow=ai.LastSeconds>ai.ChunkSeconds?slow+1:Math.Max(0,slow-1);
                }
                if(slow>=3)await Application.Current.Dispatcher.InvokeAsync(()=>{ConversationNeedsDirect=true;ConversationStatus="Conversation ne maintient pas le temps réel. Les données anciennes sont écartées ; vous pouvez passer en Direct.";});
            }
        }
        catch(OperationCanceledException){}
        catch(Exception e){StudioError(e.Message);await Application.Current.Dispatcher.InvokeAsync(()=>{StopLive();ai.Stop();AiReady=false;AiState=e.Message;ConversationNeedsDirect=true;});}
    }
}
