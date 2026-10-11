using MyVoice.Core;
using NAudio.CoreAudioApi;
using NAudio.Wave;
namespace MyVoice.Audio;
/// <summary>Owns Windows devices on a control worker, separate from the audio render loop.</summary>
public sealed class MixerDeviceManager : IDisposable
{
    private sealed record CaptureSlot(string Identity,IDisposable Capture,Func<bool> Failed,string Format);
    private readonly MixerRouter router;
    private readonly CancellationTokenSource stop=new();
    private readonly AutoResetEvent changed=new(false);
    private readonly Thread worker;
    private AudioProfile? requested;
    private MixerEndpoint[] outputs=[];
    private readonly Dictionary<string,CaptureSlot> inputs=[];
    private IReadOnlyList<SourceStatus> statuses=[];
    private string signature="";
    public bool Enabled {get;set;}=true;
    public IReadOnlyList<SourceStatus> Statuses=>Volatile.Read(ref statuses);
    public long Underruns=>Volatile.Read(ref outputs).Sum(x=>x.Underruns);
    public long Dropped=>Volatile.Read(ref outputs).Sum(x=>x.Dropped);
    public MixerDeviceManager(MixerRouter router){this.router=router;worker=new Thread(Run){IsBackground=true,Name="MyVoice device control"};worker.Start();}
    public void Apply(AudioProfile profile,bool enabled=true){Enabled=enabled;Volatile.Write(ref requested,profile);if(!enabled)router.Apply(RoutingSafety.Fix(profile));changed.Set();}
    public static string[] AsioDrivers(){try{return AsioOut.GetDriverNames();}catch{return [];}}
    private void Run(){WaitHandle[] events=[stop.Token.WaitHandle,changed];while(!stop.IsCancellationRequested){try{if(requested is {} profile)Reconcile(profile);}catch(Exception e){Volatile.Write(ref statuses,new[]{new SourceStatus("engine",false,e.Message,"")});}if(WaitHandle.WaitAny(events,1500)==0)break;}}
    private void Reconcile(AudioProfile profile)
    {
        var status=new List<SourceStatus>();var resolvedChannels=new List<AudioChannel>();var resolvedBuses=new List<AudioOutputBus>();
        bool deviceChanged=false;
        void ChangingDevice(){if(!deviceChanged){router.SuspendForDeviceChange();deviceChanged=true;}}
        using var e=new MMDeviceEnumerator();var captureIds=new HashSet<string>();
        if(profile.Channels.FirstOrDefault(c=>c.Id=="mic")?.DeviceId is {} primary)captureIds.Add(primary);
        var activeInputIds=new HashSet<string>();
        var applications=profile.Channels.Any(c=>c.Type=="application")?ApplicationAudioCatalog.List():Array.Empty<ApplicationAudioSource>();
        foreach(var channel in profile.Channels)
        {
            var resolved=channel;
            if(channel.Type is "microphone" or "soundboard"){resolvedChannels.Add(resolved);continue;}
            activeInputIds.Add(channel.Id);
            if(!channel.CaptureEnabled)
            {
                if(inputs.Remove(channel.Id,out var inactive))inactive.Capture.Dispose();
                router.Input(channel.Id).Clear();
                resolvedChannels.Add(channel);
                status.Add(new(channel.Id,false,"Capture arrêtée", ""));
                continue;
            }
            try
            {
                string identity,format="48 000 Hz · stéréo";Func<CaptureSlot> create;
                if(channel.Type=="application")
                {
                    var app=applications.FirstOrDefault(p=>p.Name.Equals(channel.ApplicationName,StringComparison.OrdinalIgnoreCase))??throw new InvalidOperationException("Application indisponible · lancez sa lecture audio");
                    identity="process:"+app.ProcessId;
                    if(!captureIds.Add(identity))throw new InvalidOperationException("Application déjà présente dans le mix");
                    create=()=>{var input=new ProcessAudioCapture(app.ProcessId,router.Input(channel.Id));return new(identity,input,()=>input.Failed,format);};
                }
                else
                {
                    using var device=channel.DeviceMode=="Default"?e.GetDefaultAudioEndpoint(channel.Type=="system"?DataFlow.Render:DataFlow.Capture,Role.Multimedia):e.GetDevice(channel.DeviceId??throw new InvalidOperationException("Choisissez un périphérique"));
                    if(device.State!=DeviceState.Active)throw new InvalidOperationException("Device unavailable · reconnexion automatique");
                    identity=device.ID;if(!captureIds.Add(identity))throw new InvalidOperationException("Périphérique déjà utilisé par un autre canal");
                    resolved=channel with{DeviceId=device.ID,DeviceName=device.FriendlyName};var id=device.ID;
                    create=()=>{var input=new EndpointCapture(id,channel.Type=="system",router.Input(channel.Id));return new(identity,input,()=>input.Failed,input.Format);};
                }
                if(!Enabled){if(inputs.Remove(channel.Id,out var disabled))disabled.Capture.Dispose();router.Input(channel.Id).Clear();status.Add(new(channel.Id,false,"Audio désactivé pour ce test",format));resolvedChannels.Add(resolved);continue;}
                if(!inputs.TryGetValue(channel.Id,out var existing)||existing.Identity!=identity||existing.Failed()){ChangingDevice();existing?.Capture.Dispose();inputs.Remove(channel.Id);router.Input(channel.Id).Clear();inputs[channel.Id]=create();}
                status.Add(new(channel.Id,true,"Connecté",inputs[channel.Id].Format));
            }
            catch(Exception error){if(inputs.Remove(channel.Id,out var previous))previous.Capture.Dispose();router.Input(channel.Id).Clear();status.Add(new(channel.Id,false,error.Message,""));}
            resolvedChannels.Add(resolved);
        }
        foreach(var id in inputs.Keys.Where(id=>!activeInputIds.Contains(id)).ToArray()){inputs[id].Capture.Dispose();inputs.Remove(id);}
        var usedOutputs=new HashSet<string>();var nextOutputs=new List<MixerEndpoint>();var current=outputs;
        foreach(var bus in profile.Outputs)
        {
            if(bus.DeviceMode=="Internal"){resolvedBuses.Add(bus);status.Add(new(bus.Id,true,"Bus interne · disponible pour enregistrer","48 000 Hz · float32 · stéréo"));continue;}
            try
            {
                string id,name;
                if(bus.Backend=="ASIO"){id=bus.DeviceId??"";if(!AsioDrivers().Contains(id))throw new InvalidOperationException("Driver ASIO indisponible");name=id;}
                else if(bus.Backend=="MME"){id=bus.DeviceId??"-1";var number=int.Parse(id);if(number<-1||number>=WaveOut.DeviceCount)throw new InvalidOperationException("Périphérique MME indisponible");name=number<0?"Sortie Windows par défaut":WaveOut.GetCapabilities(number).ProductName;if(number>=0&&bus.DeviceName!=null&&bus.DeviceName!=name)throw new InvalidOperationException("Le périphérique MME a changé de position. Sélectionnez-le à nouveau.");}
                else{using var device=bus.DeviceMode=="Default"?e.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia):e.GetDevice(bus.DeviceId??throw new InvalidOperationException("Choisissez une sortie"));if(device.State!=DeviceState.Active)throw new InvalidOperationException("Device unavailable · reconnexion automatique");id=device.ID;name=device.FriendlyName;}
                var outputKey=bus.Backend+":"+id;
                if(bus.Backend=="WASAPI")outputKey="endpoint:"+id;
                else if(bus.Backend=="MME")
                {
                    if(id=="-1"){using var selected=e.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia);outputKey="endpoint:"+selected.ID;}
                    else{var matches=new List<string>();foreach(var device in e.EnumerateAudioEndPoints(DataFlow.Render,DeviceState.Active))using(device)if(device.FriendlyName.StartsWith(name,StringComparison.OrdinalIgnoreCase))matches.Add(device.ID);if(matches.Count==1)outputKey="endpoint:"+matches[0];if(matches.Count>1)throw new InvalidOperationException("Nom MME ambigu : choisissez WASAPI pour distinguer ces périphériques.");}
                }
                if(!usedOutputs.Add(outputKey))throw new InvalidOperationException("Sortie déjà utilisée : évitez un double monitoring");
                var resolved=bus with{DeviceId=id,DeviceName=name};resolvedBuses.Add(resolved);
                if(Enabled){var fingerprint=$"{bus.Backend}:{id}:{bus.BufferMs}";var endpoint=current.FirstOrDefault(x=>x.Id==bus.Id&&x.Fingerprint==fingerprint&&!x.Failed);if(endpoint==null){ChangingDevice();endpoint=new MixerEndpoint(resolved,id);}nextOutputs.Add(endpoint);status.Add(new(bus.Id,true,name,$"48 000 Hz · {bus.Backend} · tampon ~{endpoint.ActualBufferSamples} samples"));}
                else status.Add(new(bus.Id,false,"Audio désactivé pour ce test",name));
            }
            catch(Exception error){if(!resolvedBuses.Any(b=>b.Id==bus.Id))resolvedBuses.Add(bus);status.Add(new(bus.Id,false,error.Message,""));}
        }
        Volatile.Write(ref outputs,nextOutputs.ToArray());foreach(var previous in current)if(!nextOutputs.Contains(previous))previous.Dispose();
        var resolvedProfile=profile with{Channels=resolvedChannels,Outputs=resolvedBuses};var issues=RoutingSafety.Inspect(resolvedProfile);
        foreach(var issue in issues)status.Add(new("loop:"+issue.ChannelId+":"+issue.BusId,false,"Boucle bloquée : "+issue.Description,""));
        var newSignature=System.Text.Json.JsonSerializer.Serialize(resolvedProfile);
        if(signature!=newSignature||deviceChanged){router.Apply(RoutingSafety.Fix(resolvedProfile));signature=newSignature;}
        Volatile.Write(ref statuses,status);
    }
    public void Submit(){foreach(var endpoint in Volatile.Read(ref outputs)){var samples=router.Bus(endpoint.Id);if(!samples.IsEmpty)endpoint.Write(samples);}}
    public void Dispose(){stop.Cancel();changed.Set();worker.Join(8000);foreach(var capture in inputs.Values)capture.Capture.Dispose();inputs.Clear();foreach(var output in outputs)output.Dispose();outputs=[];changed.Dispose();stop.Dispose();}
}
