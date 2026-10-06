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
    [RelayCommand] private void ResetEq(){Bass=0;Mid=0;Treble=0;}
}
