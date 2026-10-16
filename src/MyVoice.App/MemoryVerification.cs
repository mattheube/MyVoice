using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MyVoice.App.Services;
using MyVoice.App.ViewModels;
using MyVoice.Core;
namespace MyVoice.App;
public partial class App
{
    private async Task VerifyMemory(MainWindow window,string folder)
    {
        var rows=new List<object>();
        async Task Sample(string state)
        {
            await Task.Delay(1500);window.UpdateLayout();
            using var p=Process.GetCurrentProcess();p.Refresh();
            rows.Add(new{state,workingSet=p.WorkingSet64,privateBytes=p.PrivateMemorySize64,managed=GC.GetTotalMemory(false),allocated=GC.GetTotalAllocatedBytes(),gen0=GC.CollectionCount(0),gen2=GC.CollectionCount(2),cpu=p.TotalProcessorTime.TotalMilliseconds});
            File.WriteAllText(Path.Combine(folder,"memory.json"),System.Text.Json.JsonSerializer.Serialize(rows,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        }
        vm!.ShowWelcome=false;await Sample("startup");
        var file=vm.SoundCards.First().Item.File;
        foreach(var i in Enumerable.Range(0,600))vm.SoundCards.Add(new SoundCard(new SoundItem{Name="Memory fixture "+i,File=file}));
        vm.ShowLibraryCommand.Execute(null);
        for(int loop=0;loop<4;loop++)foreach(int page in new[]{4,1,0,8,5,2,4}){vm.Page=page;await Sample($"loop{loop}-page{page}");}
        vm.Page=1;
        var menu=LibraryContextMenu.Build(vm,vm.SoundCards.First());menu.PlacementTarget=window;menu.IsOpen=true;await Task.Delay(200);menu.UpdateLayout();
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth),(int)Math.Ceiling(menu.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(menu);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(folder,"context-menu.png")))encoder.Save(stream);menu.IsOpen=false;
        window.Hide();await Sample("tray-start");await Task.Delay(TimeSpan.FromMinutes(2));await Sample("tray-two-minutes");
        window.Reveal();await Sample("restored");
        File.WriteAllText(Path.Combine(folder,"memory-result.txt"),"PASS navigation, 600 metadata-only sounds, tray and restore; no forced GC. Audio disabled in this UI-only benchmark.");
    }
}
