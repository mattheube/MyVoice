using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MyVoice.Audio;
using MyVoice.Core;
using MyVoice.Infrastructure;
namespace MyVoice.App.ViewModels;

public sealed record MidiAssignment(MidiMapping Mapping,string Label);
public partial class MainViewModel
{
    private readonly MixerMidi mixerMidi=new();
    private bool midiLearning;
    private readonly Dictionary<string,bool> midiPressed=[];
    public ObservableCollection<string> MidiDevices {get;}=[];
    public ObservableCollection<MidiAssignment> MidiAssignments {get;}=[];
    public string[] MidiActions {get;}=["Volume canal","Mute canal","Solo canal","Volume sortie","Mute sortie","Charger le profil"];
    [ObservableProperty] private string? selectedMidiDevice;
    [ObservableProperty] private string midiAction="Volume canal";
    [ObservableProperty] private string midiStatus="Connectez un contrôleur MIDI, puis choisissez une commande à apprendre.";
    [ObservableProperty] private MidiAssignment? selectedMidiAssignment;

    [RelayCommand] private void DeleteAudioProfile()
    {
        if(SelectedAudioProfile is not {} p)return;
        if(MessageBox.Show("Supprimer le profil « "+p.Name+" » ? Le mix actuel reste en place.","Audio Profile",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        mixerState.Profiles.Remove(p);AudioProfiles.Remove(p);SelectedAudioProfile=AudioProfiles.FirstOrDefault();ScheduleSave();
    }
    [RelayCommand] private void ExportAudioProfile()
    {
        var dialog=new SaveFileDialog{Title="Exporter le mix actuel",Filter="Profil MyVoice|*.json",FileName="MyVoice-audio-profile.json",OverwritePrompt=true};
        if(dialog.ShowDialog()!=true)return;
        try{MixerStore.Export(dialog.FileName,mixerState.Current);MixerStatus="Profil exporté sans les identifiants des périphériques de ce PC.";}catch(Exception e){MixerStatus=e.Message;}
    }
    [RelayCommand] private void ImportAudioProfile()
    {
        if(AudioProfiles.Count>=50){MixerStatus="50 profils maximum. Retirez un profil avant d’en importer un.";return;}
        var dialog=new OpenFileDialog{Title="Importer un profil audio",Filter="Profil MyVoice|*.json"};if(dialog.ShowDialog()!=true)return;
        try{var p=MixerStore.Import(dialog.FileName);mixerState.Profiles.Add(p);AudioProfiles.Add(p);SelectedAudioProfile=p;ScheduleSave();MixerStatus="Profil importé. Chargez-le puis choisissez vos périphériques ; ses captures sont arrêtées.";}catch(Exception e){MixerStatus="Import impossible : "+e.Message;}
    }
    [RelayCommand] private void ResetMixerRouting()
    {
        if(MessageBox.Show("Rétablir le routage recommandé ? Les niveaux, les sources et les profils enregistrés sont conservés.","Routing Matrix",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        var p=mixerState.Current;
        SetMixerProfile(p with{Routes=p.Channels.SelectMany(c=>p.Outputs.Select(b=>new AudioRoute(c.Id,b.Id,b.Id is "master" or "recording" or "stream"||b.Id=="monitor"&&c.Id=="soundboard"||b.Id=="virtual"&&c.Id is "mic" or "soundboard"))).ToList()});
    }
    [RelayCommand] private void RemoveMixerOutput()
    {if(SelectedMixerOutput is {CanRemove:true} b)SetMixerProfile(mixerState.Current with{Outputs=mixerState.Current.Outputs.Where(x=>x.Id!=b.Id).ToList(),Routes=mixerState.Current.Routes.Where(r=>r.BusId!=b.Id).ToList()});}
    private void InitializeMixerControls()
    {
        RefreshMidiDevices();SelectedMidiDevice=mixerState.MidiDevice;RefreshMidiAssignments();
        if(enableOutputs&&!string.IsNullOrEmpty(SelectedMidiDevice))ConnectMidi();
    }
    [RelayCommand] private void RefreshMidiDevices()
    {try{var chosen=SelectedMidiDevice;MidiDevices.Clear();foreach(var d in MixerMidi.Devices())MidiDevices.Add(d);SelectedMidiDevice=chosen;}catch(Exception e){MidiStatus=e.Message;}}
    [RelayCommand] private void ConnectMidi()
    {try{mixerMidi.Open(SelectedMidiDevice);mixerState.MidiDevice=SelectedMidiDevice;MidiStatus=string.IsNullOrEmpty(SelectedMidiDevice)?"MIDI déconnecté.":"Connecté · "+SelectedMidiDevice;ScheduleSave();}catch(Exception e){MidiStatus=e.Message;}}
    [RelayCommand] private void DisconnectMidi(){mixerMidi.Dispose();mixerState.MidiDevice=null;midiLearning=false;MidiStatus="MIDI déconnecté.";ScheduleSave();}
    [RelayCommand] private void LearnMidi()
    {
        if(string.IsNullOrEmpty(mixerState.MidiDevice)){MidiStatus="Connectez d’abord un contrôleur MIDI.";return;}
        if(mixerState.MidiMappings.Count>=200){MidiStatus="200 commandes maximum.";return;}
        midiLearning=true;MidiStatus="Apprentissage… bougez un fader ou appuyez sur une touche. Le canal / la sortie sélectionnés seront associés.";
    }
    [RelayCommand] private void CancelMidiLearn(){midiLearning=false;MidiStatus="Apprentissage annulé.";}
    [RelayCommand] private void RemoveMidiAssignment()
    {if(SelectedMidiAssignment is {} a){mixerState.MidiMappings.Remove(a.Mapping);RefreshMidiAssignments();ScheduleSave();}}
    private void RefreshMidiAssignments()
    {MidiAssignments.Clear();foreach(var m in mixerState.MidiMappings){var name=MixerChannels.FirstOrDefault(c=>c.Id==m.Target)?.Name??MixerOutputs.FirstOrDefault(b=>b.Id==m.Target)?.Name??AudioProfiles.FirstOrDefault(p=>p.Id==m.Target)?.Name??"Cible indisponible";MidiAssignments.Add(new(m,$"{m.Device} · {m.Kind} {m.Number} / ch {m.Channel} → {m.Action} · {name}"));}}
    private void TickMixerMidi()
    {
        while(mixerMidi.Read(out var signal))
        {
            if(signal==null||signal.Device!=mixerState.MidiDevice)continue;
            if(midiLearning)
            {
                string? target=MidiAction.Contains("sortie")?SelectedMixerOutput?.Id:MidiAction=="Charger le profil"?SelectedAudioProfile?.Id:SelectedMixerChannel?.Id;
                if(target==null){MidiStatus="Sélectionnez d’abord le canal, la sortie ou le profil à contrôler.";midiLearning=false;continue;}
                if(MidiAction.StartsWith("Volume")&&signal.Kind!="CC"){MidiStatus="Pour un volume, utilisez un fader ou bouton rotatif MIDI CC.";continue;}
                mixerState.MidiMappings.RemoveAll(m=>m.Device==signal.Device&&m.Kind==signal.Kind&&m.Channel==signal.Channel&&m.Number==signal.Number);
                mixerState.MidiMappings.Add(new(signal.Device,signal.Kind,signal.Channel,signal.Number,MidiAction,target));midiLearning=false;RefreshMidiAssignments();ScheduleSave();MidiStatus="Commande mémorisée.";continue;
            }
            var key=$"{signal.Device}:{signal.Kind}:{signal.Channel}:{signal.Number}";
            bool pressed=signal.Value>=64,rising=signal.Kind=="Note"||pressed&&!midiPressed.GetValueOrDefault(key);midiPressed[key]=pressed;
            foreach(var m in mixerState.MidiMappings.Where(m=>m.Device==signal.Device&&m.Kind==signal.Kind&&m.Channel==signal.Channel&&m.Number==signal.Number))
            {
                var c=MixerChannels.FirstOrDefault(c=>c.Id==m.Target);var b=MixerOutputs.FirstOrDefault(b=>b.Id==m.Target);
                switch(m.Action)
                {
                    case "Volume canal":if(c!=null)c.Volume=signal.Value/127d*200;break;
                    case "Volume sortie":if(b!=null)b.Volume=signal.Value/127d*150;break;
                    case "Mute canal":if(rising&&c!=null)c.Mute=!c.Mute;break;
                    case "Solo canal":if(rising&&c!=null)c.Solo=!c.Solo;break;
                    case "Mute sortie":if(rising&&b!=null)b.Mute=!b.Mute;break;
                    case "Charger le profil":if(rising&&AudioProfiles.FirstOrDefault(p=>p.Id==m.Target) is {} p)SetMixerProfile(p);break;
                }
            }
        }
    }
    private void OnMixerHotkey(string action)
    {
        var parts=action.Split(':');if(parts.Length<2)return;var target=parts.Length>2?parts[2]:"";
        var c=MixerChannels.FirstOrDefault(c=>c.Id==target);var b=MixerOutputs.FirstOrDefault(b=>b.Id==target);
        switch(parts[1])
        {
            case "mute":if(c!=null)c.Mute=!c.Mute;break;
            case "solo":if(c!=null)c.Solo=!c.Solo;break;
            case "up":if(c!=null)c.Volume+=5;break;
            case "down":if(c!=null)c.Volume=Math.Max(0,c.Volume-5);break;
            case "output-mute":if(b!=null)b.Mute=!b.Mute;break;
            case "profile":if(AudioProfiles.Count>0){SelectedAudioProfile=AudioProfiles[(AudioProfiles.IndexOf(SelectedAudioProfile!)+1)%AudioProfiles.Count];LoadAudioProfile();}break;
            case "record":if(!IsRecording)RequestMixerRecording();else StopMixerRecordingCommand.Execute(null);break;
        }
    }
}
