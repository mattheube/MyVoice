using System;
using System.IO;
using CommunityToolkit.Mvvm.Input;
using NAudio.CoreAudioApi;
using NAudio.Wave;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    private string? lastConvertedFile;
    private WasapiOut? previewOutput;
    private AudioFileReader? previewReader;
    private MMDevice? previewDevice;
    private void StopPreview() { previewOutput?.Dispose();previewOutput=null;previewReader?.Dispose();previewReader=null;previewDevice?.Dispose();previewDevice=null; }
    [RelayCommand] private void PreviewResult()
    {
        if(lastConvertedFile==null||!File.Exists(lastConvertedFile)){Status=T["NoPreview"];return;}
        if(string.IsNullOrEmpty(MonitoringDevice?.Id)){Status=T["ChooseOutput"];return;}
        try
        {
            StopPreview();using var devices=new MMDeviceEnumerator();previewDevice=devices.GetDevice(MonitoringDevice.Id);
            previewReader=new AudioFileReader(lastConvertedFile){Volume=(float)(MonitoringVolume/100)};
            previewOutput=new WasapiOut(previewDevice,AudioClientShareMode.Shared,true,80);previewOutput.Init(previewReader);previewOutput.Play();
        }
        catch(Exception e){StopPreview();StudioError(e.Message);}
    }
    public void PreviewLocalFile(string path){
        if(!File.Exists(path)){ShowToast("Aucun extrait disponible.");return;}
        if(string.IsNullOrEmpty(graph.MonitorId)){ShowToast("Choisissez votre casque dans le mixeur.");return;}
        try{StopPreview();using var devices=new MMDeviceEnumerator();previewDevice=devices.GetDevice(graph.MonitorId);if(MyVoice.Core.DeviceSelection.IsVirtual(new(previewDevice.ID,previewDevice.FriendlyName)))throw new InvalidOperationException("Choisissez un casque physique pour la préécoute Community.");previewReader=new AudioFileReader(path){Volume=(float)Math.Clamp(MonitoringVolume/100,0,1)};previewOutput=new WasapiOut(previewDevice,AudioClientShareMode.Shared,true,80);previewOutput.Init(previewReader);previewOutput.Play();}catch(Exception e){StopPreview();ShowToast(e.Message);}
    }
    [RelayCommand] private void ResetEq(){Bass=0;Mid=0;Treble=0;}
}
