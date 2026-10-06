using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using MyVoice.Audio;
using MyVoice.Core;
using MyVoice.App.Services;
using NAudio.Wave;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private bool referenceRecording;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string referenceRecordingStatus = "";
    private TaskCompletionSource<bool>? referenceCompletion;
    [RelayCommand] private void FinishReference() => referenceCompletion?.TrySetResult(true);
    [RelayCommand] private void CancelReference() => referenceCompletion?.TrySetResult(false);
    [RelayCommand]
    private async Task RecordReferenceAsync()
    {
        if (AiBusy || Muted || mic is not MicrophoneService source)
        {
            Status = T["AiLiveRequirements"];
            return;
        }
        var name = Dialogs.Prompt(T["VoiceName"]);
        if (name == null)
            return;
        bool started = !Running;
        var samples = new List<float>(48000 * 60);
        var captureLock = new object();
        void Capture(ReadOnlySpan<float> data)
        {
            lock (captureLock)
            {
                foreach (var value in data)
                {
                    if (samples.Count >= 48000 * 120)
                        break;
                    samples.Add(value);
                }
            }
        }
        AiBusy = true;
        Status = T["Recording"];
        try
        {
            if (started)
                ToggleCapture();
            if (!Running)
                throw new InvalidOperationException(T["NoInput"]);
            source.Samples += Capture;
            ReferenceRecording=true;
            referenceCompletion=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var began=DateTime.UtcNow;
            while (!referenceCompletion.Task.IsCompleted && DateTime.UtcNow-began < TimeSpan.FromMinutes(2))
            {
                ReferenceRecordingStatus=$"Enregistrement · {(DateTime.UtcNow-began).TotalSeconds:0} s / 120 s — terminez quand vous le souhaitez";
                await Task.WhenAny(referenceCompletion.Task,Task.Delay(200,lifetime.Token));
                lifetime.Token.ThrowIfCancellationRequested();
            }
            if(referenceCompletion.Task.IsCompleted && !await referenceCompletion.Task) { ReferenceRecordingStatus="Enregistrement annulé";return; }
            ReferenceRecordingStatus="Préparation de la référence…";
            source.Samples -= Capture;
            float[] data;
            lock (captureLock)
                data = samples.ToArray();
            if (data.Length < 48000 || data.Max(x => Math.Abs(x)) < .004)
                throw new InvalidDataException(T["ReferenceTooShort"]);
            var id = Guid.NewGuid().ToString("N");
            var path = Path.Combine(store.Root, "voices", id + ".wav");
            using (var writer = new WaveFileWriter(path, WaveFormat.CreateIeeeFloatWaveFormat(48000, 1)))
                writer.WriteSamples(data, 0, data.Length);
            var voice = new VoiceReference { Id = id, Name = name, ReferenceFile = path, Duration = data.Length / 48000d, PeakDb = Calibration.Db(data.Max(x => Math.Abs(x))), RmsDb = Calibration.Db(Math.Sqrt(data.Average(x => (double)x * x))) };
            AiVoices.Add(voice);
            SelectedAiVoice = voice;
            SaveVoices();
            Status = T["VoiceCreated"];
            ReferenceRecordingStatus=$"Référence enregistrée · {voice.Duration:0.#} s";
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { ReferenceRecordingStatus=""; StudioError(e.Message); }
        finally { ReferenceRecording=false;referenceCompletion=null;source.Samples -= Capture; if (started && Running) ToggleCapture(); AiBusy = false; }
    }
}
