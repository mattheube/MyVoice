using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyVoice.Core;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    internal async Task VerifyConversationAsync(string sourceFile,string referenceFile,string folder,bool virtualOutput)
    {
        if(enableOutputs)throw new InvalidOperationException("Use an isolated smoke profile");
        StopAi();Settings.ActiveAiVoice=false;AiModel="conversation";AiProfile="Balanced";AiExpression="adaptive";Muted=false;
        SelectedAiVoice=new VoiceReference{Name="Synthetic test",ReferenceFile=referenceFile};
        await StartAiCommand.ExecuteAsync(null);if(!AiReady)throw new Exception(AiState);
        ai.ReferenceFiles=[referenceFile];await ai.LoadAsync(referenceFile,lifetime.Token);
        if(!ai.ConversationRecommended)throw new Exception($"Conversation did not pass load benchmark: RTF {ai.BenchmarkRtf}");
        if(virtualOutput)
        {
            var cables=graph.Devices().Where(d=>d.Name.Contains("CABLE Input",StringComparison.OrdinalIgnoreCase)).ToArray();
            if(cables.Length!=1)throw new Exception("Expected one CABLE Input test endpoint");
            graph.Configure(null,cables[0].Id);graph.SendMicrophone=true;
        }
        using var reader=new AudioFileReader(sourceFile);var source=new WdlResamplingSampleProvider(reader,48000);var samples=new List<float>();var scratch=new float[48000];int n;
        while((n=source.Read(scratch,0,scratch.Length))>0)samples.AddRange(scratch.Take(n));
        samples.AddRange(new float[96000]);var cycle=samples.ToArray();var latencies=new List<double>();var sync=new object();
        void Latency(double ms){lock(sync)latencies.Add(ms);}
        graph.AiVirtualSubmitted+=Latency;graph.FlushMicrophone();conversationInput.Clear();AiLive=true;graph.AiLive=true;
        liveCancel=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);liveWorker=Task.Run(()=>ConversationLoop(liveCancel.Token));
        long beforeDrops=graph.DroppedFrames;var peaks=new List<double>();
        try
        {
            var watch=Stopwatch.StartNew();
            await Task.Run(async()=>
            {
                var block=new float[480];
                for(int frame=0;frame<18000;frame++)
                {
                    var delay=frame*10-watch.ElapsedMilliseconds;if(delay>0)await Task.Delay((int)delay,lifetime.Token);
                    for(int j=0;j<480;j++)block[j]=cycle[(frame*480+j)%cycle.Length];
                    graph.PushMicrophone(block);
                    if(frame%100==0){lock(sync)peaks.Add(graph.ProcessedPeak);}
                }
            });
            await Task.Delay(2500,lifetime.Token);
            if(!AiLive)throw new Exception("Conversation stopped: "+AiState);
            if(conversationDrops!=0||graph.DroppedFrames!=beforeDrops)throw new Exception($"Dropped audio: {conversationDrops}/{graph.DroppedFrames-beforeDrops}");
            if(!peaks.Any(x=>x>-60))throw new Exception("No converted PCM reached mixer");
            double[] values;lock(sync)values=latencies.ToArray();
            if(virtualOutput && values.Length<1000)throw new Exception("Virtual submission timestamps missing");
            if(values.Length>0)
            {
                var first=values.Take(values.Length/3).Order().ElementAt(values.Length/6);var last=values.TakeLast(values.Length/3).Order().ElementAt(values.Length/6);
                if(last-first>250||values.Max()>2500)throw new Exception("Latency accumulated beyond budget");
                File.WriteAllText(Path.Combine(folder,"conversation-result.txt"),$"PASS 180 s end-to-end streaming, no drops, nonzero converted PCM, virtual WASAPI submitted timestamps={values.Length}; first median={first:0}ms; last median={last:0}ms; max={values.Max():0}ms. Device and Discord/network latency excluded.\n");
            }
            else File.WriteAllText(Path.Combine(folder,"conversation-result.txt"),"PASS 180 s isolated streaming, no drops, converted PCM; virtual output not connected.\n");
        }
        finally{graph.AiVirtualSubmitted-=Latency;StopLive();if(liveWorker!=null)await liveWorker;StopAi();graph.Configure(null,null);}
        await RunComparisons(sourceFile);
        if(VoiceComparisons.Count!=3)throw new Exception("Comparisons incomplete: "+ComparisonStatus);
        foreach(var result in VoiceComparisons)File.Copy(result.File,Path.Combine(folder,result.Mode+".wav"),true);
        File.AppendAllText(Path.Combine(folder,"conversation-result.txt"),"PASS three modes convert identical source and reference; outputs saved for perceptual comparison.\n");
    }
}
