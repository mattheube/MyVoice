using CommunityToolkit.Mvvm.Input;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    [RelayCommand] private void ApplyMicPreset(string name)
    {
        ResetProcessing();
        switch(name)
        {
            case "Natural":Gate=false;NoiseSuppression=0;Compressor=false;break;
            case "Clear Voice":NoiseSuppression=1;Bass=-1;Treble=2;CompressorRatio=2;break;
            case "Broadcast":Bass=2;Mid=1;Treble=1;CompressorThreshold=-22;CompressorRatio=3;break;
            case "Distant Microphone":GainDb=6;NoiseSuppression=1;GateThreshold=-58;CompressorThreshold=-26;CompressorRatio=2;MakeupGain=2;break;
            case "Quiet Room":Gate=false;NoiseSuppression=0;CompressorRatio=2;break;
            case "Noisy Room":NoiseSuppression=2;GateThreshold=-40;GateHold=140;CompressorRatio=2;break;
        }
        Save();ShowToast("Réglage appliqué · "+name);
    }
    [RelayCommand] private void ResetMicSection(string name)
    {
        switch(name)
        {
            case "Cleanup":Gate=true;GateThreshold=-48;GateAttack=5;GateHold=100;GateRelease=120;NoiseSuppression=1;break;
            case "Tone":ResetEq();break;
            case "Dynamics":Compressor=true;CompressorThreshold=-18;CompressorRatio=3;CompressorAttack=8;CompressorRelease=150;MakeupGain=0;AutoGain=false;break;
            case "Monitoring":HearMyself=false;MonitoringVolume=50;break;
        }
        Save();
    }
}
