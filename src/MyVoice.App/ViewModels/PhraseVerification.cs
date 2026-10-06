using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    // Integration harness: only available to the isolated smoke profile, with no audio endpoints.
    internal async Task VerifyPhrasePipelineAsync(string sourceFile,string referenceFile,string report)
    {
        if(enableOutputs)throw new InvalidOperationException("Phrase verification requires isolated outputs");
        StopAi();Settings.ActiveAiVoice=false;AiModel="studio";AiExpression="adaptive";AiProfile="Balanced";Muted=false;
        await StartAiCommand.ExecuteAsync(null);
        if(!AiReady)throw new Exception(AiState);
        ai.ReferenceFiles=[referenceFile];await ai.LoadAsync(referenceFile,lifetime.Token);
        using var reader=new AudioFileReader(sourceFile);
        var source=new WdlResamplingSampleProvider(reader,48000);var input=new float[144000];
        var read=source.Read(input,0,input.Length);Array.Resize(ref input,read);
        aiInput.Clear();graph.FlushMicrophone();AiLive=true;graph.AiLive=true;
        liveCancel=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        liveWorker=Task.Run(()=>LiveLoop(liveCancel.Token));
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(4));
        async Task Until(Func<bool> predicate){while(!predicate()){timeout.Token.ThrowIfCancellationRequested();if(!AiLive)throw new Exception(AiState);await Task.Delay(25);}}
        try
        {
            await Until(()=>AiState.StartsWith("À vous"));
            for(var cycle=0;cycle<2;cycle++)
            {
                FeedAi(input);if(cycle==1)FinishAiPhrase();
                await Until(()=>AiState.StartsWith("Conversion"));
                if(aiAcceptInput)throw new Exception("Input was not paused during conversion");
                FeedAi(input);if(aiInput.Count!=0)throw new Exception("Input accumulated during conversion");
                await Until(()=>AiState.StartsWith("Lecture"));
                await Until(()=>graph.ProcessedPeak>-65);
                await Until(()=>AiState.StartsWith("À vous"));
                if(!aiAcceptInput||aiInput.Count!=0||graph.ConvertedFrames!=0)throw new Exception("Phrase cycle did not drain/reset");
                File.AppendAllText(report,$"PASS {(cycle==0?"pause":"manual finish")}: conversion, paused capture, audible PCM in mixer, playback drained, capture resumed; {ai.LastSeconds:0.00}s computation\n");
            }
        }
        finally{StopLive();if(liveWorker!=null)await liveWorker;StopAi();}
    }
}
