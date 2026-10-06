using MyVoice.Core;
using MyVoice.Infrastructure;
using MyVoice.Soundboard;
public static class QualityTests
{
    public static void Run()
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
        var original=Enumerable.Range(0,48000*2).Select(i=>(float)(.1*Math.Sin(2*Math.PI*80*(i/2)/48000))).ToArray();
        var neutral=(float[])original.Clone();new SoundToneProcessor().Process(neutral,neutral.Length,1,0,0);Check(neutral.SequenceEqual(original),"Neutral sound tone leaves PCM unchanged");
        var bass=(float[])original.Clone();new SoundToneProcessor().Process(bass,bass.Length,1,12,0);Check(bass.Skip(24000).Average(x=>x*x)>original.Skip(24000).Average(x=>x*x)*4,"Bass shelf boosts a low frequency");
        var saturated=(float[])original.Clone();new SoundToneProcessor().Process(saturated,saturated.Length,1,0,1);Check(saturated.Max()>.9&&saturated.All(float.IsFinite),"Saturation changes waveform without invalid samples");
        var mute=(float[])original.Clone();new SoundToneProcessor().Process(mute,mute.Length,0,24,1);Check(mute.All(x=>x==0),"Zero volume is silent from the first sample");
        var boosted=(float[])original.Clone();new SoundToneProcessor().Process(boosted,boosted.Length,10,0,0);Check(boosted.Max()>.99,"Numeric volume exceeds the former 200 percent limit");
        var root=Path.Combine(Path.GetTempPath(),"MyVoice-Quality-"+Guid.NewGuid().ToString("N"));var store=new LocalStore(root);store.Save(new AppSettings {SoundboardVolume=12.34,MonitoringVolume=4.5});var settings=store.Load();Check(settings.SoundboardVolume==12.34&&settings.MonitoringVolume==4.5,"High numeric volumes survive settings reload");
        using var sounds=new SoundboardService(root);sounds.Library.Sounds.Add(new SoundItem {Name="Original",Volume=9.5,BassDb=12,Saturation=.4});sounds.Save();sounds.Library.Sounds.Add(new SoundItem {Name="New"});sounds.Save();Check(Directory.GetFiles(Path.Combine(root,"backups","soundboard-history")).Length>0,"Sound catalog history survives successive saves");
        using var reopened=new SoundboardService(root);var item=reopened.Library.Sounds.First(x=>x.Name=="Original");Check(item.Volume==9.5&&item.BassDb==12&&item.Saturation==.4,"Per-sound tone settings survive catalog reload");
        var version1=UserDataBackup.EnsureVersionBackup(root,"2.1.0");var version2=UserDataBackup.EnsureVersionBackup(root,"2.2.0");Check(version1!=version2&&File.Exists(Path.Combine(version2,"complete.json")),"Each version creates a separate complete backup");
    }
}
