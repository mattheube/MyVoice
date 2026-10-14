using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MyVoice.Core;
namespace MyVoice.App.ViewModels;
public sealed class MixerChannelCard : ObservableObject
{
    private readonly Action changed;
    public AudioChannel Value {get;private set;}
    public MixerChannelCard(AudioChannel value,Action changed){Value=value;this.changed=changed;}
    private void Set(AudioChannel value,string property){Value=value;OnPropertyChanged(property);changed();}
    public string Id=>Value.Id;
    public bool CanRemove=>Id is not("mic" or "soundboard");
    public bool HasInputGain=>Id!="mic";
    public bool IsMic=>Id=="mic";
    public bool IsApplication=>Value.Type=="application";
    public bool Mono{get=>Value.Mono;set=>Set(Value with{Mono=value},nameof(Mono));}
    public bool NeverSendVirtual{get=>Value.NeverSendVirtual;set=>Set(Value with{NeverSendVirtual=value},nameof(NeverSendVirtual));}
    public bool CaptureEnabled {get=>Value.CaptureEnabled;set=>Set(Value with{CaptureEnabled=value},nameof(CaptureEnabled));}
    public bool IsMicrophoneInput=>Value.Type=="capture";
    public string Kind=>Value.Type switch{"microphone"=>"MIC / VOICE","soundboard"=>"SOUNDBOARD BUS","capture"=>"INPUT","system"=>"SYSTEM AUDIO",_=>"APPLICATION"};
    public string Name{get=>Value.Name;set{if(!string.IsNullOrWhiteSpace(value))Set(Value with{Name=value[..Math.Min(value.Length,80)]},nameof(Name));}}
    public double Volume{get=>Value.Volume*100;set{if(double.IsFinite(value)){Set(Value with{Volume=Math.Clamp(value/100,0,1e6)},nameof(Volume));OnPropertyChanged(nameof(VolumeSlider));}}}
    public double VolumeSlider{get=>Math.Min(200,Volume);set{if(Math.Abs(value-Math.Min(200,Volume))>.01)Volume=value;}}
    public double Gain{get=>Value.GainDb;set=>Set(Value with{GainDb=Math.Clamp(value,-60,30)},nameof(Gain));}
    public double Pan{get=>Value.Pan*100;set=>Set(Value with{Pan=Math.Clamp(value/100,-1,1)},nameof(Pan));}
    public bool Mute{get=>Value.Mute;set=>Set(Value with{Mute=value},nameof(Mute));}
    public bool Solo{get=>Value.Solo;set=>Set(Value with{Solo=value},nameof(Solo));}
    private void Effect(ChannelEffects e,string name){Set(Value with{Effects=e},name);OnPropertyChanged(nameof(Chain));}
    public double Bass{get=>Value.Effects.BassDb;set=>Effect(Value.Effects with{BassDb=Math.Clamp(value,-18,18)},nameof(Bass));}
    public double Mid{get=>Value.Effects.MidDb;set=>Effect(Value.Effects with{MidDb=Math.Clamp(value,-18,18)},nameof(Mid));}
    public double Treble{get=>Value.Effects.TrebleDb;set=>Effect(Value.Effects with{TrebleDb=Math.Clamp(value,-18,18)},nameof(Treble));}
    public double HighPass{get=>Value.Effects.HighPassHz;set=>Effect(Value.Effects with{HighPassHz=Math.Clamp(value,0,1000)},nameof(HighPass));}
    public double Delay{get=>Value.Effects.Delay*100;set=>Effect(Value.Effects with{Delay=Math.Clamp(value/100,0,1)},nameof(Delay));}
    public double Reverb{get=>Value.Effects.Reverb*100;set=>Effect(Value.Effects with{Reverb=Math.Clamp(value/100,0,1)},nameof(Reverb));}
    public bool Gate{get=>Value.Effects.Gate;set=>Effect(Value.Effects with{Gate=value},nameof(Gate));}
    public int NoiseReduction{get=>Value.Effects.NoiseReduction;set=>Effect(Value.Effects with{NoiseReduction=Math.Clamp(value,0,3)},nameof(NoiseReduction));}
    public bool Compressor{get=>Value.Effects.Compressor;set=>Effect(Value.Effects with{Compressor=value},nameof(Compressor));}
    public double Threshold{get=>Value.Effects.CompressorThreshold;set=>Effect(Value.Effects with{CompressorThreshold=Math.Clamp(value,-60,0)},nameof(Threshold));}
    public double Ratio{get=>Value.Effects.CompressorRatio;set=>Effect(Value.Effects with{CompressorRatio=Math.Clamp(value,1,20)},nameof(Ratio));}
    public string Chain=>IsMic?"Traitement Mic Setup → voix active → niveau → bus → limiteur":"Gain → filtre → "+(Gate?"gate → ":"")+(NoiseReduction>0?"réduction du bruit → ":"")+"EQ → "+(Compressor?"compression → ":"")+"delay / reverb → niveau → bus → limiteur";
    private double level;public double Level{get=>level;private set=>SetProperty(ref level,value);}
    private string meter="−90 dB";public string Meter{get=>meter;private set=>SetProperty(ref meter,value);}
    private bool clipping;public bool Clipping{get=>clipping;private set=>SetProperty(ref clipping,value);}
    private string state="";public string State{get=>state;set=>SetProperty(ref state,value);}
    public void RefreshMeter(MixerMeter value){Level=Math.Clamp((value.PeakDb+60)*100/60,0,100);Meter=$"Peak {value.PeakDb:0.0} · RMS {value.RmsDb:0.0} dB";Clipping=value.Clipping;}
}
public sealed class MixerOutputCard : ObservableObject
{
    private readonly Action changed;
    public AudioOutputBus Value {get;private set;}
    public ObservableCollection<AudioDevice> Choices {get;}=[];
    private bool refreshingChoices;
    public void SetChoices(IEnumerable<AudioDevice> choices)
    {
        refreshingChoices=true;
        try{MyVoice.App.Services.CollectionSync.ByKey(Choices,choices,d=>d.Id);}
        finally{refreshingChoices=false;RefreshDevice();}
    }
    public void ReplaceValue(AudioOutputBus value){if(Value==value)return;Value=value;OnPropertyChanged(string.Empty);}
    public MixerOutputCard(AudioOutputBus value,Action changed){Value=value;this.changed=changed;}
    private void Set(AudioOutputBus value,string name){if(Value==value)return;Value=value;OnPropertyChanged(name);changed();}
    public string Id=>Value.Id;
    public bool CanRemove=>Id is not("monitor" or "virtual" or "stream" or "recording" or "master");
    public string Name{get=>Value.Name;set{if(!string.IsNullOrWhiteSpace(value))Set(Value with{Name=value[..Math.Min(value.Length,80)]},nameof(Name));}}
    public double Volume{get=>Value.Volume*100;set{if(double.IsFinite(value)){Set(Value with{Volume=Math.Clamp(value/100,0,1e6)},nameof(Volume));OnPropertyChanged(nameof(VolumeSlider));}}}
    public double VolumeSlider{get=>Math.Min(150,Volume);set{if(Math.Abs(value-Math.Min(150,Volume))>.01)Volume=value;}}
    public bool Mute{get=>Value.Mute;set=>Set(Value with{Mute=value},nameof(Mute));}
    public string Device{get=>Value.DeviceMode=="Internal"?"@internal":Value.DeviceMode=="Default"?"@default":Value.DeviceId??"@missing";set{if(value==null||refreshingChoices)return;Set(Value with{DeviceMode=value=="@internal"?"Internal":value=="@default"?"Default":"Fixed",DeviceId=value.StartsWith('@')?null:value,DeviceName=Choices.FirstOrDefault(d=>d.Id==value)?.Name},nameof(Device));}}
    public void RefreshDevice()=>OnPropertyChanged(nameof(Device));
    public string Backend{get=>Value.Backend;set{if(value==null||Value.Backend==value)return;Set(Value with{Backend=value,DeviceId=null,DeviceMode="Internal"},nameof(Backend));OnPropertyChanged(nameof(Device));}}
    public int BufferMs{get=>Value.BufferMs;set=>Set(Value with{BufferMs=Math.Clamp(value,10,200)},nameof(BufferMs));}
    private double level;public double Level{get=>level;private set=>SetProperty(ref level,value);}
    private string meter="−90 dB";public string Meter{get=>meter;private set=>SetProperty(ref meter,value);}
    private bool clipping;public bool Clipping{get=>clipping;private set=>SetProperty(ref clipping,value);}
    private string state="";public string State{get=>state;set=>SetProperty(ref state,value);}
    public void RefreshMeter(MixerMeter value){Level=Math.Clamp((value.PeakDb+60)*100/60,0,100);Meter=$"Peak {value.PeakDb:0.0} · RMS {value.RmsDb:0.0} dB";Clipping=value.Clipping;}
}
public sealed class MixerRouteCell : ObservableObject
{
    private readonly Action changed;private bool enabled;
    public string ChannelId{get;}public string BusId{get;}public string Label{get;}
    public MixerRouteCell(string channelId,string busId,string label,bool enabled,Action changed){ChannelId=channelId;BusId=busId;Label=label;this.enabled=enabled;this.changed=changed;}
    private bool blocked;
    public bool Blocked{get=>blocked;set{if(SetProperty(ref blocked,value)){OnPropertyChanged(nameof(CanEdit));OnPropertyChanged(nameof(Enabled));}}}
    public bool CanEdit=>!Blocked;
    public bool Enabled{get=>enabled&&!Blocked;set{if(!Blocked&&SetProperty(ref enabled,value))changed();}}
}
public sealed record MixerRouteRow(string Name,List<MixerRouteCell> Cells);
