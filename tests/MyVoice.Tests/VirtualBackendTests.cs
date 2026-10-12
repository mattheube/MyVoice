using MyVoice.Core;
public static class VirtualBackendTests
{
    public static void Run()
    {
        var backend=new VBCableBackend();var cable=new AudioDevice("cable","CABLE Input (VB-Audio Virtual Cable)");var receiver=new AudioDevice("receiver","CABLE Output (VB-Audio Virtual Cable)");
        AudioDevice[] devices=[new("old","Haut-parleurs"),new("wide","CABLE In 16ch (VB-Audio Virtual Cable)"),cable];
        void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
        Check(backend.Resolve(devices,[receiver],"old").Playback==cable,"Virtual backend repairs stale speaker route");
        Check(backend.Resolve(devices,[receiver],"cable").Detected,"Virtual backend restores known cable pair");
        Check(!backend.Resolve(devices,[],"cable").Detected,"Virtual backend needs capture endpoint too");
        Check(!backend.Resolve([devices[0],devices[1]],[receiver],"old").Detected,"Virtual backend never uses speakers or 16ch");
        Check(!backend.Resolve([cable,new("duplicate",cable.Name)],[receiver],null).Detected,"Ambiguous cables do not report connected");
        Check(backend.Resolve([cable,new("duplicate",cable.Name)],[receiver],"cable").Detected,"Explicit compatible cable resolves ambiguity");
    }
}
