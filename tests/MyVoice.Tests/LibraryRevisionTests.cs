using MyVoice.Core;
using MyVoice.Infrastructure;
using MyVoice.Soundboard;
using NAudio.Wave;
using System.Security.Cryptography;

public static class LibraryRevisionTests
{
    public static async Task Run()
    {
        void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
        var root=Path.Combine(Path.GetTempPath(),"MyVoice-LibraryRevision-"+Guid.NewGuid().ToString("N"));
        new LocalStore(root);
        var input=Path.Combine(root,"source.wav");
        using(var writer=new WaveFileWriter(input,WaveFormat.CreateIeeeFloatWaveFormat(48000,1)))
        {var samples=Enumerable.Repeat(.2f,48000).ToArray();writer.WriteSamples(samples,0,samples.Length);}
        var original=SHA256.HashData(File.ReadAllBytes(input));
        var output=Path.Combine(root,"edited.wav");
        await SoundEditor.RenderAsync(input,output,new(.2,.8,.05,.05,true),CancellationToken.None);
        using(var reader=new AudioFileReader(output))
        {
            var samples=new float[28800];var count=reader.Read(samples,0,samples.Length);
            Check(count==28800&&Math.Abs(reader.TotalTime.TotalSeconds-.6)<.001,"Sound trim keeps exactly the selected duration");
            Check(Math.Abs(samples[0])<.0001&&Math.Abs(samples[^1])<.0001&&Math.Abs(samples.Max()-.95)<.001,"Normalization and both fades affect rendered audio");
        }
        Check(original.SequenceEqual(SHA256.HashData(File.ReadAllBytes(input))),"Sound editing preserves the original bytes");
        try{await SoundEditor.RenderAsync(input,input,new(0,.5),CancellationToken.None);throw new Exception("Overwrite accepted");}
        catch(InvalidDataException){Check(original.SequenceEqual(SHA256.HashData(File.ReadAllBytes(input))),"Editor rejects overwriting the source");}
        using(var sounds=new SoundboardService(root))
        {
            var id=await sounds.ImportAsync(input,CancellationToken.None);var item=sounds.Library.Sounds.Single(s=>s.Id==id);
            item.HearMyself=false;item.SendToMic=true;
            sounds.Preview(id);float[] monitor=new float[960],remote=new float[960],all=new float[960];
            sounds.MixChannels(monitor,remote,all);
            Check(monitor.Any(v=>v!=0)&&remote.All(v=>v==0)&&all.All(v=>v==0),"Preview reaches headphones only, never virtual or other buses");
            Check(!item.HearMyself&&item.SendToMic,"Preview preserves saved routing preferences");
            sounds.Stop(id);sounds.Play(id);Array.Clear(monitor);sounds.MixChannels(monitor,remote,all);
            Check(monitor.All(v=>v==0)&&remote.Any(v=>v!=0),"Normal playback restores the configured routing after preview");
        }
        var store=new ProductStore(root);var state=new ProductState();var entry=new LibraryEntry("sound",Guid.NewGuid().ToString());
        state.Collections.Add(new(){Name="Mixed",Items=[entry,new("voice-preset","Clean")]});state.FavoriteSounds.Add(Guid.Parse(entry.Id));store.Save(state);
        var loaded=store.Load();loaded.Collections[0].Items.Remove(entry);store.Save(loaded);
        Check(store.Load().FavoriteSounds.Contains(Guid.Parse(entry.Id))&&store.Load().Collections[0].Items.Count==1,"Removing a collection reference leaves independent likes intact");
        var imported=Path.Combine(root,"imported.myvoice");CreationPackage.ExportBoard(imported,new(){Name="Imported"},[new(){File=input,Name="Imported",SourceContentId="community-123",Category="SFX",Description="Metadata retained",Tags=["test"]}]);
        var manifest=CreationPackage.Inspect(imported);
        Check(manifest.Sounds[0].SourceContentId=="community-123"&&manifest.Sounds[0].Description=="Metadata retained"&&manifest.Sounds[0].Category=="SFX","Package export preserves provenance and sound metadata");
        try{await new PublicationDraftStore(root).PrepareAsync(imported);throw new Exception("Imported publication accepted");}
        catch(InvalidDataException){Check(true,"Draft preparation refuses republishing imported content");}
    }
}
