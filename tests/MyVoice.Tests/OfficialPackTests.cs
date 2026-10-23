using MyVoice.Core;
using MyVoice.Soundboard;
using NAudio.Wave;
static class OfficialPackTests
{
    public static void Run(Action<string,Action> test)
    {
        test("Official packs contain distinct bounded original PCM audio",()=>{
            string source=Path.Combine(AppContext.BaseDirectory,"OfficialPacks");var packs=OfficialPacks.Load(source);
            if(packs.Count!=4||packs.Sum(p=>p.Count)!=37)throw new Exception("Official catalog incomplete");
            var hashes=new HashSet<string>();
            foreach(var sound in packs.SelectMany(p=>p.Sounds)){
                if(!hashes.Add(sound.Sha256))throw new Exception("Duplicate sound");using var reader=new AudioFileReader(Path.Combine(source,sound.File));
                if(reader.TotalTime.TotalSeconds is <=0 or >3||reader.WaveFormat.SampleRate!=48000)throw new Exception("Unexpected sound format");
                var frame=new float[4096];double peak=0;int count;while((count=reader.Read(frame,0,frame.Length))>0)for(int i=0;i<count;i++){if(!float.IsFinite(frame[i]))throw new Exception("Invalid PCM");peak=Math.Max(peak,Math.Abs(frame[i]));}
                if(peak is <.001 or >.61)throw new Exception("Clipped or empty sound");
            }
        });
        test("Installing official pack preserves personal sounds and renamed pack",()=>{
            string source=Path.Combine(AppContext.BaseDirectory,"OfficialPacks"),root=Path.Combine(Path.GetTempPath(),"MyVoice-Originals-"+Guid.NewGuid());
            try{
                var pack=OfficialPacks.Load(source)[0];var personal=new SoundItem{Name="Personal",Volume=4};var library=new SoundLibrary();library.Sounds.Add(personal);library.Folders.Add(new(){Name=pack.Title});
                var folder=OfficialPacks.Install(pack,source,root,library);if(folder.Name==pack.Title||library.Sounds[0]!=personal||personal.Volume!=4)throw new Exception("Personal catalog changed");
                folder.Name="Renamed by user";var again=OfficialPacks.Install(pack,source,root,library);
                if(again!=folder||library.Sounds.Count!=11)throw new Exception("Duplicate pack installation");
                if(library.Sounds.Skip(1).Any(s=>!File.Exists(s.File)||!s.File.StartsWith(root)))throw new Exception("Assets not retained in user library");
            }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
        });
    }
}
