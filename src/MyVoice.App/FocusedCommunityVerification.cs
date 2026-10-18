using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MyVoice.Core;
namespace MyVoice.App;
public partial class App {
 private async Task VerifyFocusedCommunity(MainWindow window,string folder){
  vm!.ShowWelcome=false;vm.Page=5;window.UpdateLayout();await Task.Delay(200);
  var page=Walk(window).OfType<Views.CommunityView>().Single();
  if(Walk(page).OfType<Views.PublicationView>().Any()||Walk(page).OfType<Button>().Any(b=>b.Content?.ToString()?.Contains("Installer un package")==true))throw new Exception("Personal management remains in Community");
  vm.CommunityItems.Clear();vm.CommunityItems.Add(new(){Id=Guid.NewGuid().ToString(),Title="Fixture — Michou",Kind="soundboard",Username="fixture_creator",Description="UI test only",Cover=vm.SoundCards.FirstOrDefault()?.Cover});window.UpdateLayout();Shot("community-focused.png");
  vm.Page=7;window.UpdateLayout();await Task.Delay(100);var profile=Walk(window).OfType<Views.ProfileView>().Single();
  if(!Walk(profile).OfType<TextBlock>().Any(t=>t.IsVisible&&t.Text.StartsWith("Créez un compte MyVoice"))||Walk(profile).OfType<TabControl>().Any(t=>t.IsVisible))throw new Exception("Guest sees social profile");Shot("profile-guest.png");
  var ai=new VoiceReference{Name="Original AI fixture",ReferenceFile=Path.Combine(folder,"test-reference.wav"),Duration=.1};
  using(var writer=new NAudio.Wave.WaveFileWriter(ai.ReferenceFile,new NAudio.Wave.WaveFormat(48000,1))){var pcm=Enumerable.Range(0,4800).Select(i=>(float)(.1*Math.Sin(i*.1))).ToArray();writer.WriteSamples(pcm,0,pcm.Length);}
  bool HasPublish(object item)=>Services.LibraryContextMenu.Build(vm,item).Items.OfType<MenuItem>().Any(m=>m.Header?.ToString()=="Publish & Share");
  if(!HasPublish(ai)||HasPublish(new VoiceReference{SourceContentId="remote"})||!HasPublish(new DesignedVoice())||HasPublish(new DesignedVoice{SourceContentId="remote"})||HasPublish(vm.Presets.First()))throw new Exception("Publication ownership menu mismatch");
  await vm.PrepareAiPublication(ai);window.UpdateLayout();await Task.Delay(150);if(!vm.ShowPublication||!vm.HasPublicationPackage||vm.Product.PublicationKind!="ai-voice")throw new Exception("AI publication modal missing");Shot("publish-ai-modal.png");
  vm.ClosePublicationCommand.Execute(null);if(vm.ShowPublication||!vm.HasPublicationPackage)throw new Exception("Closing modal discarded draft");
  // Render a disconnected social-profile fixture: no real user or server mutation.
  var social=new Views.ProfileView{DataContext=new ProfileFixture{SignedIn=true,ProfileName="Profil de test",ProfileUsername="fixture",ProfileBio="Aperçu de la mise en page publique",ProfileMetrics="12 abonnés · 8 abonnements · 42 likes reçus",ProfileVerified=true,ProfilePublicationKind="all",ProfilePublications=vm.CommunityItems,CommunityConfigured=false}};
  var host=new Window{Content=social,Width=950,Height=850,ShowInTaskbar=false,WindowStyle=WindowStyle.None};host.SetResourceReference(Window.BackgroundProperty,"CanvasBrush");host.SetResourceReference(Window.ForegroundProperty,"TextBrush");host.Show();host.UpdateLayout();await Task.Delay(100);Save(host,"profile-public-fixture.png");host.Close();
  File.WriteAllText(Path.Combine(folder,"focused-community-result.txt"),"PASS discovery excludes publication UI; guest profile; owned AI/preset publication menus; downloaded/classic publication denied; AI draft/modal/close preservation; social profile fixture rendered. No real account or publication created.");
  void Shot(string name)=>Save(window,name);
  void Save(Window target,string name){var bitmap=new RenderTargetBitmap((int)target.ActualWidth,(int)target.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(target);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(folder,name));png.Save(file);}
 }
}

internal sealed class ProfileFixture {
 public bool SignedIn{get;set;} public bool CommunityConfigured{get;set;} public bool ProfileVerified{get;set;} public bool ProfilePrivate{get;set;}
 public string ProfileName{get;set;}="";public string ProfileUsername{get;set;}="";public string ProfileBio{get;set;}="";public string ProfileMetrics{get;set;}="";public string ProfilePublicationKind{get;set;}="all";
 public object? ProfilePublications{get;set;}
}
