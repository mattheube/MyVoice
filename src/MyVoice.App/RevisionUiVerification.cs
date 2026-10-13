using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MyVoice.Core;
using MyVoice.App.ViewModels;
namespace MyVoice.App;
public partial class App
{
    private async Task VerifyRevisionUi(MainWindow window,string folder)
    {
        await vm!.RefreshMixerDevicesCommand.ExecuteAsync(null);vm.Page=8;vm.MixerAdvanced=true;
        var chosen=vm.MixerInputs.Last();vm.MixerInput=chosen;
        await vm.RefreshMixerDevicesCommand.ExecuteAsync(null);
        if(vm.MixerInput?.Id!=chosen.Id)throw new Exception("Device refresh lost source selection");
        vm.AddMixerOutputCommand.Execute(null);var card=vm.SelectedMixerOutput!;
        var device=card.Choices.First(d=>d.Id=="@default");card.Device=device.Id;
        await Task.Delay(200);window.UpdateLayout();
        System.Collections.Generic.IEnumerable<DependencyObject> Children(DependencyObject p){for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++){var c=VisualTreeHelper.GetChild(p,i);yield return c;foreach(var x in Children(c))yield return x;}}
        var combo=Children(window).OfType<ComboBox>().First(c=>ReferenceEquals(c.DataContext,card)&&c.SelectedValuePath=="Id");
        combo.BringIntoView();await Task.Delay(100);combo.IsDropDownOpen=true;await Task.Delay(100);
        var popup=(Popup)combo.Template.FindName("PART_Popup",combo);
        if(!popup.IsOpen||popup.Child is not FrameworkElement {ActualWidth:>0,ActualHeight:>0})throw new Exception("Dropdown did not render outside the mixer");
        await vm.RefreshMixerDevicesCommand.ExecuteAsync(null);
        if(!combo.IsDropDownOpen||!ReferenceEquals(card,vm.SelectedMixerOutput)||combo.SelectedValue?.ToString()!=device.Id)throw new Exception("Refreshing devices closed the dropdown or changed selection");
        combo.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window),0,Key.Escape){RoutedEvent=Keyboard.KeyDownEvent});
        if(combo.IsDropDownOpen)throw new Exception("Escape did not close dropdown");
        combo.Focus();combo.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,-120){RoutedEvent=Mouse.PreviewMouseWheelEvent});
        if(combo.SelectedValue?.ToString()!=device.Id)throw new Exception("Closed selector changed device on mouse wheel");
        var selectedCard=vm.SelectedMixerOutput;var selectedSource=vm.MixerInput;
        vm.RefreshCableCommand.Execute(null);
        if(!ReferenceEquals(selectedCard,vm.SelectedMixerOutput))throw new Exception("Cable refresh rebuilt unrelated mixer controls");
        vm.RemoveMixerOutputCommand.Execute(null);
        var draft=Path.Combine(folder,"draft-fixture.myvoice");MyVoice.Infrastructure.CreationPackage.ExportPreset(draft,new DesignedVoice{Name="Draft fixture"});
        await vm.PreparePublicationAsync(draft);vm.PublishDescription="Description conservée";vm.PublishVisibility="friends";vm.SavePublicationDraftCommand.Execute(null);
        var product=new MyVoice.Infrastructure.ProductStore(store!.Root).Load();
        if(product.PublicationTitle!="Draft fixture"||product.PublicationDescription!=vm.PublishDescription||product.PublicationVisibility!="friends"||vm.CanPublishDraft)throw new Exception("Offline publication draft persistence failed");
        var good=new MyVoice.Infrastructure.PublicationDraftStore(store.Root).PackagePath;var hash=System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(good));
        var invalid=Path.Combine(folder,"invalid.myvoice");File.WriteAllText(invalid,"invalid");await vm.PreparePublicationAsync(invalid);
        if(!hash.SequenceEqual(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(good))))throw new Exception("Invalid package replaced the valid draft");
        vm.Page=1;vm.ShowLibraryCommand.Execute(null);window.UpdateLayout();
        var sound=vm.SoundCards.First();var entry=new LibraryEntry("sound",sound.Item.Id.ToString());
        var collection=new LibraryCollection{Name="Verification",Items=[entry,new("classic-voice","Clean")]};vm.Product.Collections.Add(collection);vm.Collections.Add(collection);
        if(!vm.EntryLiked(entry))vm.ToggleEntryLike(entry);
        vm.SelectedCollection=collection;window.UpdateLayout();
        if(vm.SoundView.Cast<object>().Count()!=1||vm.CollectionVoices.Count!=1)throw new Exception("Mixed collection filter lost its sound or voice");
        vm.ToggleCollectionEntry(collection,entry);
        if(!vm.EntryLiked(entry)||vm.SoundView.Cast<object>().Any())throw new Exception("Collection removal changed the independent like");
        vm.ShowLibraryCommand.Execute(null);
        var context=MyVoice.App.Services.LibraryContextMenu.Build(vm,sound);context.PlacementTarget=window;context.IsOpen=true;await Task.Delay(100);
        if(context.Items.Count<6||context.ActualWidth<=0)throw new Exception("Sound context menu did not render");context.IsOpen=false;
        if(!vm.FindDestinations(sound.Name).Any(r=>r.Title==sound.Name))throw new Exception("Global search missed a sound");
        vm.ShowToast("Verification notification");if(vm.UnreadNotifications==0)throw new Exception("Notification not retained");vm.MarkNotificationsRead();if(vm.UnreadNotifications!=0)throw new Exception("Notification read state failed");
        var gif=Path.Combine(folder,"animated.gif");var encoder=new System.Windows.Media.Imaging.GifBitmapEncoder();
        foreach(byte color in new byte[]{20,220}){var pixels=Enumerable.Repeat(color,64*32*4).ToArray();var bitmap=System.Windows.Media.Imaging.BitmapSource.Create(64,32,96,96,PixelFormats.Bgra32,null,pixels,256);var metadata=new System.Windows.Media.Imaging.BitmapMetadata("gif");metadata.SetQuery("/grctlext/Delay",(ushort)12);encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap,null,metadata,null));}
        Services.GifEncoding.Save(gif,encoder,[120,120]);
        var frames=Services.CoverDecoder.Read(gif);if(frames.Frames.Length!=2||frames.Delays.Any(d=>d!=120))throw new Exception("GIF frame decoding failed");
        var crop=new ImageCropWindow(gif,false);var cropped=crop.SaveExport(Path.Combine(folder,"cropped-animation"));crop.Close();
        var croppedFrames=Services.CoverDecoder.Read(cropped);if(croppedFrames.Frames.Length!=2||croppedFrames.Frames.Any(f=>f.PixelWidth!=f.PixelHeight))throw new Exception("Animated crop did not preserve square frames");
        using(var bitmap=new SkiaSharp.SKBitmap(64,32)){bitmap.Erase(SkiaSharp.SKColors.Red);using var data=bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Webp,90);using var file=File.Create(Path.Combine(folder,"test.webp"));data.SaveTo(file);}
        if(Services.CoverDecoder.Read(Path.Combine(folder,"test.webp")).Frames[0].PixelWidth!=64)throw new Exception("WebP decoding failed");
        var added=Enumerable.Range(0,600).Select(i=>new MyVoice.App.ViewModels.SoundCard(new SoundItem{Name="Virtualized "+i,File=sound.Item.File})).ToArray();
        foreach(var item in added)vm.SoundCards.Add(item);vm.ShowLibraryCommand.Execute(null);window.UpdateLayout();await Task.Delay(150);
        var soundList=Children(window).OfType<ListBox>().First(l=>ReferenceEquals(l.ItemsSource,vm.SoundRows));
        int realized=Enumerable.Range(0,soundList.Items.Count).Count(i=>soundList.ItemContainerGenerator.ContainerFromIndex(i)!=null);
        if(realized>=soundList.Items.Count||realized>30)throw new Exception("Sound rows were not virtualized: "+realized);
        foreach(var item in added)vm.SoundCards.Remove(item);
        typeof(MainWindow).GetMethod("ShowSearch",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(window,null);await Task.Delay(100);
        var search=Application.Current.Windows.OfType<Window>().First(w=>w.Owner==window&&w.Title==vm.T["SearchEverywhere"]);
        if(!search.IsVisible)throw new Exception("Search window failed to open");search.Close();
        int language=vm.LanguageIndex;vm.LanguageIndex=1;if(vm.T["RevisionLabel16"]!="Sound editor"||vm.AccountLabel!="Guest")throw new Exception("English revision labels failed");vm.LanguageIndex=language;
        File.WriteAllText(Path.Combine(folder,"revision-result.txt"),"PASS dropdown render/open during refresh; Escape; wheel protection; stable device selection; cable refresh retains mixer cards; offline draft metadata persists; invalid package preserves previous draft; mixed collection filters; independent likes; context menu render; global search; notification read state.");
    }
}
