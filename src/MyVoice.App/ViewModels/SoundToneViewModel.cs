using System;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    public double SoundVolumeSlider { get=>Math.Min(400,SoundVolume);set=>SoundVolume=value; }
    public double SoundboardVolumeSlider { get=>Math.Min(400,SoundboardVolume);set=>SoundboardVolume=value; }
    public double MonitoringVolumeSlider { get=>Math.Min(200,MonitoringVolume);set=>MonitoringVolume=value; }
    public double SoundBass { get=>SelectedSound?.Item.BassDb??0;set{if(SelectedSound==null)return;SelectedSound.Item.BassDb=Math.Clamp(double.IsFinite(value)?value:0,-24,24);SoundChanged(nameof(SoundBass));} }
    public double SoundSaturation { get=>(SelectedSound?.Item.Saturation??0)*100;set{if(SelectedSound==null)return;SelectedSound.Item.Saturation=Math.Clamp(double.IsFinite(value)?value/100:0,0,1);SoundChanged(nameof(SoundSaturation));} }
}
