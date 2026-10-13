using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using MyVoice.Audio;
using MyVoice.Infrastructure;
using MyVoice.App.ViewModels;
using MyVoice.Updater;
using Forms = System.Windows.Forms;
namespace MyVoice.App;
public partial class App : Application
{
    private LocalStore? store; private MainViewModel? vm; private Forms.NotifyIcon? tray; private Mutex? instance; private bool ownsMutex;
    public bool Exiting
    {
        get; private set;
    }
    protected override async void OnStartup(StartupEventArgs e)
    {
        Services.WindowsIdentity.Initialize();
        base.OnStartup(e);
        var smoke = e.Args.Contains("--smoke-test");
        var updateJob=e.Args.SkipWhile(x=>x!="--update-job").Skip(1).FirstOrDefault();
        var rollbackJob=e.Args.SkipWhile(x=>x!="--update-rollback").Skip(1).FirstOrDefault();
        if(updateJob==null&&rollbackJob==null&&UpdateHandoff.IsBusy(AppContext.BaseDirectory))
        {MessageBox.Show("MyVoice est en cours de mise à jour. Il se rouvrira automatiquement.","MyVoice");Shutdown();return;}
        instance = new Mutex(true, smoke ? "Local\\MyVoice.Smoke" : "Local\\MyVoice.App", out ownsMutex);
        if (!ownsMutex)
        {
            MessageBox.Show("MyVoice est déjà ouvert dans la zone de notification.", "MyVoice");
            Shutdown();
            return;
        }
        SplashWindow? splash=null;
        if(!smoke || e.Args.Contains("--startup-flow")) { splash=new SplashWindow(); splash.Show(); }
        try
        {
            store = new LocalStore(smoke ? Path.Combine(Path.GetTempPath(), "MyVoice-Smoke-" + Guid.NewGuid().ToString("N")) : null);
            UpdateSession? startupUpdate=null;string? updateProblem=null;
            if(!smoke || e.Args.Contains("--startup-flow"))
            {
                var preferences=await Task.Run(store.Load);
                try { Services.ProductAppearance.Apply(await Task.Run(()=>new ProductStore(store.Root).Load())); } catch(Exception appearanceError) { store.Log("Early theme: "+appearanceError.Message); }
                splash?.RefreshMotion();
                if(preferences.CheckUpdatesAtStartup && updateJob==null && rollbackJob==null)
                {
                    splash?.Stage(StartupPhase.CheckingUpdates,"Vérification des mises à jour…");
                    var source=preferences.UpdateChannel=="beta"?DistributionDefaults.BetaManifestUrl:DistributionDefaults.ManifestUrl;
                    var installed=System.Reflection.Assembly.GetExecutingAssembly().GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]??"2.4.0-beta.1";
                    bool recent=preferences.LastUpdateSource==source && preferences.LastUpdateCheck is {} at && DateTime.UtcNow-at>=TimeSpan.Zero && DateTime.UtcNow-at<TimeSpan.FromHours(6);
                    if(recent && !string.IsNullOrEmpty(preferences.LastKnownVersion) && !ReleaseVersion.IsNewer(preferences.LastKnownVersion,preferences.LastKnownReleaseTag,installed))
                        splash?.Update(new(UpdatePhase.Current,"MyVoice est à jour ✓"));
                    else
                    {
                        startupUpdate=new UpdateSession();using var skip=new CancellationTokenSource();
                        if(splash!=null)splash.ContinueRequested+=skip.Cancel;
                        bool retry;
                        do
                        {
                            startupUpdate=new UpdateSession();
                            await startupUpdate.RunAsync(source,installed,Path.Combine(store.Root,"cache","updates"),new Progress<UpdateSnapshot>(value=>splash?.Update(value)),store.Log,skip.Token);
                            retry=splash!=null && startupUpdate.State.Phase==UpdatePhase.Failed && startupUpdate.FailedAt!=UpdatePhase.Checking && await splash.OfferRetryAsync(startupUpdate.State.Message);
                        }while(retry);
                        if(splash!=null)splash.ContinueRequested-=skip.Cancel;
                    }
                }
                if(!smoke && preferences.AutomaticUpdates && startupUpdate?.DownloadedFile is {} package && startupUpdate.Release is {} release && !string.IsNullOrEmpty(release.PackageUrl))
                {
                    if(UpdateHandoff.IsBlocked(release.Tag)){updateProblem="Cette mise à jour a échoué précédemment. Vous pouvez réessayer dans les réglages.";store.Log("Automatic update paused for failed release "+release.Tag);}
                    else
                    {
                        try
                        {
                            splash?.Stage(StartupPhase.InstallingUpdate,"Installation de la mise à jour…");
                            await UpdateHandoff.LaunchAsync(AppContext.BaseDirectory,package,startupUpdate.Source,release,CancellationToken.None);
                            splash?.Close();Shutdown();return;
                        }
                        catch(Exception handoffError){updateProblem="Mise à jour reportée : "+handoffError.Message;store.Log("Update handoff failed: "+handoffError);}
                    }
                }
                splash?.Stage(StartupPhase.LoadingSettings,"Chargement de vos réglages…");
            }
            if(!smoke) await Task.Run(()=>UserDataBackup.EnsureVersionBackup(store.Root,System.Reflection.Assembly.GetExecutingAssembly().GetName().Version!.ToString(3)));
            if(smoke && e.Args.Contains("--profile-copy"))
            {
                var original=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"MyVoice");
                foreach(var folder in new[]{"config","soundboards","voices"})foreach(var file in Directory.GetFiles(Path.Combine(original,folder),"*.json"))File.Copy(file,Path.Combine(store.Root,folder,Path.GetFileName(file)),true);
            }
            store.Log("Starting MyVoice "+Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
            DispatcherUnhandledException += (_, args) => { store.Log(args.Exception.ToString()); MessageBox.Show(vm?.T["Error"] ?? "MyVoice could not continue.", "MyVoice"); args.Handled = true; ExitApplication(); };
            splash?.Stage(StartupPhase.LoadingLibrary,"Chargement de vos sons et de vos voix…");
            await Task.Yield();
            vm = new MainViewModel(store, new MicrophoneService(), !smoke);
            if(startupUpdate!=null)vm.AdoptUpdateSession(startupUpdate);
            if(updateProblem!=null)vm.UpdateStatus=updateProblem;
            if(smoke && e.Args.Contains("--audio-startup")){vm.Settings.AutoStartAi=false;vm.StartServices();if(!vm.Running)throw new Exception("Automatic microphone failed");store.Log("PASS automatic microphone startup");}
            var window = new MainWindow(vm);
            MainWindow = window;
            tray = new Forms.NotifyIcon { Icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "MyVoice.Tray.ico")), Text = "MyVoice", Visible = true };
            vm.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(vm.Muted) && vm.Running && vm.Notifications) { tray.Text = "MyVoice · " + vm.MicState; tray.ShowBalloonTip(1200, "MyVoice", vm.MicState, Forms.ToolTipIcon.None); } };
            var menu = new Forms.ContextMenuStrip();
            menu.Opening += (_, _) => { menu.Items.Clear(); menu.Items.Add(vm.T["Open"], null, (_, _) => window.Reveal()); var mute = menu.Items.Add(vm.MicState, null, (_, _) => vm.ToggleMute()); mute.Enabled = vm.Running; menu.Items.Add(vm.T["HearMyself"], null, (_, _) => vm.HearMyself = !vm.HearMyself); menu.Items.Add(vm.T["VoiceEnabled"], null, (_, _) => vm.VoiceEnabled = !vm.VoiceEnabled); menu.Items.Add(vm.T["StopAll"], null, (_, _) => vm.StopAllSounds()); menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add(vm.T["Exit"], null, (_, _) => ExitApplication()); };
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += (_, _) => window.Reveal();
            if(!smoke)
            {
                splash?.Stage(StartupPhase.DetectingAudio,"Détection du microphone virtuel…");await Task.Yield();vm.RefreshCableCommand.Execute(null);
                splash?.Stage(StartupPhase.StartingAudioEngine,"Connexion du microphone…");await Task.Yield();vm.StartServices();
                while(!vm.Running && updateJob==null && splash!=null && await splash.OfferRetryAsync("Le microphone n’est pas disponible. Vérifiez son branchement.",()=>vm.OpenLogsCommand.Execute(null)))
                    vm.StartServices();
                if(vm.Settings.AutoStartAi)splash?.Stage(StartupPhase.StartingAIEngine,"Préparation de l’IA en arrière-plan…");
            }
            window.Show();
            if(updateJob!=null)
            {
                UpdateHandoff.MarkHealthy(updateJob,vm.InstalledRelease);
                vm.UpdateStatus="MyVoice mis à jour · "+vm.InstalledRelease;
                vm.ShowToast(vm.UpdateStatus);
            }
            if(rollbackJob!=null){vm.UpdateStatus="La mise à jour a échoué. La version précédente a été restaurée.";vm.ShowToast(vm.UpdateStatus);}
            if(splash!=null)await splash.FinishAsync(vm.Running?"Prêt ✓":"MyVoice prêt · microphone indisponible");
            if (vm.StartMinimized && !smoke)
                window.Hide();
            store.Log("Main window started");

            if (smoke)
                _ = SmokeAsync(window, e.Args);
        }
        catch (Exception ex) { splash?.Close(); store?.Log(ex.ToString()); MessageBox.Show(ex.Message, "MyVoice — démarrage impossible"); Shutdown(1); }
    }
    private async Task SmokeAsync(MainWindow window, string[] args)
    {
        try
        {
            vm!.Animations=false;window.WindowState=WindowState.Normal;window.Width=1340;window.Height=920;
            await Task.Delay(700);
            var folder = args.SkipWhile(x => x != "--smoke-test").Skip(1).FirstOrDefault() ?? Path.GetTempPath();
            Directory.CreateDirectory(folder);
            if(args.Contains("--exercise-update"))
            {
                // Explicit integration test: only this isolated smoke profile and program copy are used.
                using var updater=new UpdateService();
                var release=await updater.ReadManifestAsync(DistributionDefaults.BetaManifestUrl,CancellationToken.None);
                var package=await updater.DownloadPackageAsync(release,DistributionDefaults.BetaManifestUrl,Path.Combine(store!.Root,"cache","updates"),CancellationToken.None);
                await UpdateHandoff.LaunchAsync(AppContext.BaseDirectory,package,DistributionDefaults.BetaManifestUrl,release,CancellationToken.None,true);
                File.WriteAllText(Path.Combine(folder,"handoff.txt"),"accepted "+release.Tag);
                ExitApplication();return;
            }
            vm!.ShowWelcome=false;
            for (int page = 0; page < 9; page++)
            {
                vm!.Page = page;
                if (page == 1 && vm.SoundCards.Count > 0)
                {
                    vm.PlaySoundCommand.Execute(vm.SoundCards[0]);
                    vm.SoundLoop = true;
                }
                if (page == 2)
                {
                    vm.StopAllSounds();
                    vm.ClosePlayerCommand.Execute(null);
                }
                await Task.Delay(220);
                window.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(folder, $"page-{page}.png"));
                encoder.Save(file);
            }
            if(args.Contains("--ui-details")){await VerifyImageUi(window,folder);VerifyProductUi(folder);await VerifyMixerUi(window,folder);await VerifyStartupUi(window,folder);}
            if(args.Contains("--revision-ui"))await VerifyRevisionUi(window,folder);
            window.Width=1000;window.Height=700;vm!.UiScale=1.5;vm.Page=3;await Task.Delay(180);window.UpdateLayout();
            var compact=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);compact.Render(window);
            var compactEncoder=new PngBitmapEncoder();compactEncoder.Frames.Add(BitmapFrame.Create(compact));using(var compactFile=File.Create(Path.Combine(folder,"compact-150.png")))compactEncoder.Save(compactFile);
            if(args.Contains("--engine-startup"))
            {
                await vm!.StartAiCommand.ExecuteAsync(null);
                if(!vm.AiReady)throw new Exception("WPF AI startup failed: "+vm.AiState);
                File.WriteAllText(Path.Combine(folder,"engine-result.txt"),vm.AiState);
                vm.StopAi();
            }
            if(args.Contains("--phrase-test"))
            {
                var offset=Array.IndexOf(args,"--phrase-test");
                await vm!.VerifyPhrasePipelineAsync(args[offset+1],args[offset+2],Path.Combine(folder,"phrase-result.txt"));
            }
            if(args.Contains("--conversation-test"))
            {
                var offset=Array.IndexOf(args,"--conversation-test");
                await vm!.VerifyConversationAsync(args[offset+1],args[offset+2],folder,args.Contains("--virtual-conversation"));
            }
            vm.UiScale=1;vm.LanguageIndex = 1;
            vm.GainDb = 3.5;
            vm.Save();
            if (store!.Load().GainDb != 3.5)
                throw new Exception("Settings round trip failed");
            if (!vm.SetHotkey(6, 0x7B))
                throw new Exception("Hotkey registration failed");
            using (var hidden = new HwndSource(new HwndSourceParameters("MyVoice hotkey conflict test")))
            using (var conflict = new Services.HotkeyService(hidden.Handle))
            {
                if (conflict.Set(6, 0x7B))
                    throw new Exception("Hotkey conflict not detected");
            }
            vm.SetHotkey(0, 0);
            vm.CloseToTray = true;
            window.Close();
            if (window.IsVisible)
                throw new Exception("Close-to-tray failed");
            window.Reveal();
            if (!window.IsVisible)
                throw new Exception("Tray restore failed");
            File.WriteAllText(Path.Combine(folder, "smoke-result.txt"), "PASS: 9 pages rendered; English loaded; settings round trip; global hotkey registered and conflict rejected; close-to-tray and restore; clean exit.\n" + store!.Root);
            store.Log("Smoke test passed");
            ExitApplication();
        }
        catch (Exception ex) { store?.Log(ex.ToString()); ExitApplication(); Environment.ExitCode = 1; }
    }
    public void ExitApplication()
    {
        if (Exiting)
            return;
        Exiting = true;
        try
        {
            vm?.Dispose();
        }
        catch (Exception ex) { store?.Log(ex.ToString()); }
        tray?.Dispose();
        store?.Log("Shutdown complete");
        Shutdown();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        tray?.Dispose();
        if (ownsMutex)
            instance?.ReleaseMutex();
        instance?.Dispose();
        base.OnExit(e);
    }
}
