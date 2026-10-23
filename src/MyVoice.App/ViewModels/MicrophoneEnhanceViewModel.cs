using System;
using MyVoice.Audio;

namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    public string MicEnhanceMode
    {
        get=>Settings.MicEnhanceMode;
        set
        {
            if(!Enum.TryParse<MicrophoneEnhanceMode>(value,out var mode)||!Enum.IsDefined(mode))return;
            Settings.MicEnhanceMode=value;graph.MicEnhance.Mode=mode;Changed(nameof(MicEnhanceMode));OnPropertyChanged(nameof(MicEnhanceStatus));OnPropertyChanged(nameof(ClassicNoiseAvailable));
        }
    }
    public bool MicEnhanceAvailable=>MicrophoneEnhancer.Available;
    public bool ClassicNoiseAvailable=>!MicEnhanceAvailable||MicEnhanceMode=="Off";
    public string MicEnhanceStatus=>T[!MicEnhanceAvailable?"MicEnhanceMissing":MicEnhanceMode=="Off"?"MicEnhanceOff":"MicEnhanceReady"];
}
