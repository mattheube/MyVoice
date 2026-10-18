using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MyVoice.Audio;
namespace MyVoice.App;
public partial class App
{
    private async Task VerifyKeyboardDiscovery(MainWindow window,string folder)
    {
        await vm!.ForceRefreshApplicationsCommand.ExecuteAsync(null);
        if(vm.MixerApplications.Count==0||vm.MixerApplications.Select(a=>a.Identity.ToUpperInvariant()).Distinct().Count()!=vm.MixerApplications.Count)throw new Exception("Application discovery empty or duplicated");
        File.WriteAllLines(Path.Combine(folder,"discovered-applications.txt"),vm.MixerApplications.Select(a=>a.Label+" | "+a.Detail));
        vm.Page=8;vm.MixerSourceType="Application";window.UpdateLayout();
        foreach(var expander in Walk(window).OfType<Expander>().Where(e=>e.Header?.ToString()=="+ Ajouter une source"))expander.IsExpanded=true;
        vm.ApplicationSearch="discord";window.UpdateLayout();
        if(vm.ApplicationView.Cast<ApplicationAudioSource>().Any(a=>!(a.Label+" "+a.Name+" "+a.ExecutablePath).Contains("discord",StringComparison.OrdinalIgnoreCase)))throw new Exception("Application search failed");
        vm.ApplicationSearch="";await Task.Delay(100);Shot("application-discovery.png");
        var app=vm.MixerApplications.First();vm.MixerApplication=app;vm.AddMixerSourceCommand.Execute(null);
        if(!vm.MixerChannels.Any(c=>ApplicationDiscoveryService.Matches(app,c.Value)))throw new Exception("Application identity not saved");
        await vm.RefreshApplicationsCommand.ExecuteAsync(null);if(vm.MixerApplication?.Identity!=app.Identity)throw new Exception("Application selection lost");
        vm.Page=1;window.UpdateLayout();
        var section=Walk(window).OfType<Expander>().First(e=>e.Header?.ToString()?.StartsWith("Keyboard Sound")==true);section.IsExpanded=true;window.UpdateLayout();section.BringIntoView();
        if(vm.KeyboardPacks.Length!=5||vm.KeyboardEnabled)throw new Exception("Keyboard default or catalog invalid");
        await vm.SelectKeyboardPackCommand.ExecuteAsync(vm.KeyboardPacks.First(p=>p.Id=="blue"));
        await vm.VerifyKeyboardStateAsync();window.UpdateLayout();section.BringIntoView();await Task.Delay(150);Shot("keyboard-sound.png");
        // Exercise native hook lifetime without collecting or injecting any user's keystrokes.
        using(var listener=new Services.GlobalKeyListener((_,_)=>{}))await Task.Delay(100);
        File.WriteAllText(Path.Combine(folder,"keyboard-discovery-result.txt"),"PASS merged live discovery, unique identities, search, saved application, selection across refresh, five recorded keyboard profiles, preview, OFF memory release, hidden audio, profile persistence, native global hook install/dispose. Physical typing in Notepad and subjective realism are not verified by this automated test.");
        void Shot(string name){var bmp=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using var f=File.Create(Path.Combine(folder,name));encoder.Save(f);}
    }
    private static System.Collections.Generic.IEnumerable<DependencyObject> Walk(DependencyObject parent){for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var descendant in Walk(child))yield return descendant;}}
}
