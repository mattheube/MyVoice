using System;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyVoice.Audio;
using MyVoice.Core;
using MyVoice.App.Services;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    private GlobalKeyListener? keyboardListener;
    private readonly SemaphoreSlim keyboardLoadGate=new(1,1);
    private int keyboardRevision;
    private string KeyboardRoot=>Path.Combine(AppContext.BaseDirectory,"KeyboardPacks");
    public KeyboardPack[] KeyboardPacks {get;private set;}=[];
    [ObservableProperty] private string keyboardStatus="Désactivé · aucun texte enregistré";
    public string SelectedKeyboardPack{get=>mixerState.Current.Keyboard.Pack;set{if(value!=SelectedKeyboardPack&&KeyboardPacks.Any(p=>p.Id==value))SetKeyboard(mixerState.Current.Keyboard with{Pack=value});}}
    public double KeyboardDownVolume{get=>mixerState.Current.Keyboard.KeyDownVolume*100;set{if(double.IsFinite(value))SetKeyboard(mixerState.Current.Keyboard with{KeyDownVolume=Math.Clamp(value/100,0,2)});}}
    public double KeyboardUpVolume{get=>mixerState.Current.Keyboard.KeyUpVolume*100;set{if(double.IsFinite(value))SetKeyboard(mixerState.Current.Keyboard with{KeyUpVolume=Math.Clamp(value/100,0,2)});}}
    public double KeyboardEnterVolume{get=>mixerState.Current.Keyboard.EnterVolume*100;set{if(double.IsFinite(value))SetKeyboard(mixerState.Current.Keyboard with{EnterVolume=Math.Clamp(value/100,0,2)});}}
    public double KeyboardVariation{get=>mixerState.Current.Keyboard.Variation*100;set{if(double.IsFinite(value))SetKeyboard(mixerState.Current.Keyboard with{Variation=Math.Clamp(value/100,0,1)});}}
    public double KeyboardStereoWidth{get=>mixerState.Current.Keyboard.StereoWidth*100;set{if(double.IsFinite(value))SetKeyboard(mixerState.Current.Keyboard with{StereoWidth=Math.Clamp(value/100,0,1)});}}
    public string SelectedKeyboardName=>KeyboardPacks.FirstOrDefault(p=>p.Id==SelectedKeyboardPack)?.Name??SelectedKeyboardPack;
    public bool KeyboardEnabled {get=>mixerState.Current.Keyboard.Enabled;set=>SetKeyboard(mixerState.Current.Keyboard with{Enabled=value});}
    public double KeyboardVolume {get=>mixerState.Current.Keyboard.Volume*100;set{if(double.IsFinite(value))SetKeyboard(mixerState.Current.Keyboard with{Volume=Math.Clamp(value/100,0,2)});}}
    public double KeyboardPitch {get=>mixerState.Current.Keyboard.Pitch;set{if(double.IsFinite(value))SetKeyboard(mixerState.Current.Keyboard with{Pitch=Math.Clamp(value,-6,6)});}}
    public double KeyboardIntensity {get=>mixerState.Current.Keyboard.Intensity*100;set{if(double.IsFinite(value))SetKeyboard(mixerState.Current.Keyboard with{Intensity=Math.Clamp(value/100,0,1)});}}
    public double KeyboardSpaceVolume {get=>mixerState.Current.Keyboard.SpacebarVolume*100;set{if(double.IsFinite(value))SetKeyboard(mixerState.Current.Keyboard with{SpacebarVolume=Math.Clamp(value/100,0,2)});}}
    public bool KeyboardRepeat {get=>mixerState.Current.Keyboard.Repeat;set=>SetKeyboard(mixerState.Current.Keyboard with{Repeat=value});}
    public bool KeyboardExcludeModifiers {get=>mixerState.Current.Keyboard.ExcludeModifiers;set=>SetKeyboard(mixerState.Current.Keyboard with{ExcludeModifiers=value});}
    public bool KeyboardExcludeFunctions {get=>mixerState.Current.Keyboard.ExcludeFunctionKeys;set=>SetKeyboard(mixerState.Current.Keyboard with{ExcludeFunctionKeys=value});}
    public bool KeyboardMonitor {get=>KeyboardRoute("monitor");set=>SetKeyboardRoute("monitor",value);}
    public bool KeyboardVirtual {get=>KeyboardRoute("virtual");set=>SetKeyboardRoute("virtual",value);}
    public bool KeyboardStream {get=>KeyboardRoute("stream");set=>SetKeyboardRoute("stream",value);}
    public bool KeyboardRecording {get=>KeyboardRoute("recording");set=>SetKeyboardRoute("recording",value);}
    private bool KeyboardRoute(string bus)=>mixerState.Current.Routes.Any(r=>r.ChannelId=="keyboard"&&r.BusId==bus&&r.Enabled);
    private void SetKeyboardRoute(string bus,bool value){if(!EnsureKeyboardChannel())return;foreach(var c in MixerRows.SelectMany(r=>r.Cells).Where(c=>c.ChannelId=="keyboard"&&c.BusId==bus))c.Enabled=value;NotifyKeyboard();}
    private bool EnsureKeyboardChannel()
    {
        if(MixerChannels.Any(c=>c.Id=="keyboard"))return true;
        if(MixerChannels.Count>=24){KeyboardStatus=T["Runtime51"];return false;}
        var profile=mixerState.Current with{Channels=mixerState.Current.Channels.Append(new AudioChannel{Id="keyboard",Name="Keyboard Sound",Type="keyboard"}).ToList(),Routes=mixerState.Current.Routes.Concat(MixerOutputs.Select(b=>new AudioRoute("keyboard",b.Id,b.Id=="monitor"))).ToList()};
        mixerState.Current=profile;LoadMixerCards(profile);ApplyMixer();return true;
    }
    private void InitializeKeyboard(){KeyboardPacks=KeyboardSoundService.Catalog(KeyboardRoot);if(!KeyboardPacks.Any(p=>p.Id==SelectedKeyboardPack)){mixerState.Current=mixerState.Current with{Keyboard=mixerState.Current.Keyboard with{Pack="red"}};ScheduleSave();}OnPropertyChanged(nameof(KeyboardPacks));if(KeyboardEnabled)_=ApplyKeyboardAsync();}
    private void SetKeyboard(KeyboardSettings settings)
    {
        if(settings.Enabled&&!EnsureKeyboardChannel()){NotifyKeyboard();return;}
        mixerState.Current=mixerState.Current with{Keyboard=settings};NotifyKeyboard();ScheduleSave();_=ApplyKeyboardAsync();
    }
    private void NotifyKeyboard(){foreach(var p in new[]{nameof(KeyboardDownVolume),nameof(KeyboardUpVolume),nameof(KeyboardEnterVolume),nameof(KeyboardVariation),nameof(KeyboardStereoWidth),nameof(KeyboardEnabled),nameof(KeyboardVolume),nameof(KeyboardPitch),nameof(KeyboardIntensity),nameof(KeyboardSpaceVolume),nameof(KeyboardRepeat),nameof(KeyboardExcludeModifiers),nameof(KeyboardExcludeFunctions),nameof(SelectedKeyboardPack),nameof(SelectedKeyboardName),nameof(KeyboardMonitor),nameof(KeyboardVirtual),nameof(KeyboardStream),nameof(KeyboardRecording)})OnPropertyChanged(p);}
    private async Task ApplyKeyboardAsync()
    {
        int revision=++keyboardRevision;var settings=mixerState.Current.Keyboard;
        if(!settings.Enabled){keyboardListener?.Dispose();keyboardListener=null;graph.Keyboard.Disable();KeyboardStatus=T["Runtime43"];}
        await keyboardLoadGate.WaitAsync();
        try
        {
            if(revision!=keyboardRevision||lifetime.IsCancellationRequested)return;
            if(settings.Enabled){if(!EnsureKeyboardChannel())return;await Task.Run(()=>graph.Keyboard.Load(KeyboardRoot,settings));}
            if(revision!=keyboardRevision||lifetime.IsCancellationRequested){graph.Keyboard.Disable();return;}
            if(settings.Enabled)
            {
                if(enableOutputs)keyboardListener??=new GlobalKeyListener(graph.Keyboard.Trigger);
                if(keyboardListener!=null)keyboardListener.Settings=settings;
                KeyboardStatus=$"{SelectedKeyboardName} · actif en arrière-plan · {graph.Keyboard.LoadedBytes/1048576d:0.0} Mo de samples";
            }
        }
        catch(Exception e){keyboardListener?.Dispose();keyboardListener=null;graph.Keyboard.Disable();mixerState.Current=mixerState.Current with{Keyboard=settings with{Enabled=false}};NotifyKeyboard();KeyboardStatus="Keyboard Sound : "+e.Message;ScheduleSave();}
        finally{keyboardLoadGate.Release();}
    }
    [RelayCommand] private async Task SelectKeyboardPackAsync(KeyboardPack? pack)
    {
        if(pack==null)return;SetKeyboard(mixerState.Current.Keyboard with{Pack=pack.Id});await PreviewKeyboardAsync();
    }
    [RelayCommand] private async Task PreviewKeyboardAsync()
    {
        if(!EnsureKeyboardChannel())return;
        await keyboardLoadGate.WaitAsync();
        try
        {
            if(lifetime.IsCancellationRequested)return;
            var settings=mixerState.Current.Keyboard;await Task.Run(()=>graph.Keyboard.Load(KeyboardRoot,settings with{Enabled=true}));
            foreach(var kind in new[]{KeyboardKeyKind.Normal,KeyboardKeyKind.Normal,KeyboardKeyKind.Space,KeyboardKeyKind.Enter})
            {graph.Keyboard.Trigger(kind,false);await Task.Delay(65,lifetime.Token);graph.Keyboard.Trigger(kind,true);await Task.Delay(95,lifetime.Token);}
            await Task.Delay(250,lifetime.Token);
        }
        catch(OperationCanceledException){}
        catch(Exception e){KeyboardStatus=e.Message;}
        finally{if(!KeyboardEnabled||lifetime.IsCancellationRequested)graph.Keyboard.Disable();keyboardLoadGate.Release();}
    }
    private void DisposeKeyboard(){keyboardRevision++;keyboardListener?.Dispose();keyboardListener=null;graph.Keyboard.Disable();}
}
