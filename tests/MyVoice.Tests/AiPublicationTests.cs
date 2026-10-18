using System.IO.Compression;
using MyVoice.Core;
using MyVoice.Infrastructure;
using NAudio.Wave;
internal static class AiPublicationTests {
 internal static void Run(Action<string,Action> test){
 test("AI package contains usable audio without local paths or excluded originals",()=>{
 var root=Path.Combine(Path.GetTempPath(),"MyVoice-publication-"+Guid.NewGuid());Directory.CreateDirectory(root);
 try{var reference=Path.Combine(root,"reference.wav");using(var writer=new WaveFileWriter(reference,new WaveFormat(48000,1))){writer.WriteSamples(Enumerable.Range(0,4800).Select(i=>(float)(.1*Math.Sin(i*.1))).ToArray(),0,4800);}
 var voice=new VoiceReference{Name="My original",ReferenceFile=reference,Duration=.1,Samples=[new(){OriginalFile=@"C:\private\excluded.wav",Excluded=true},new(){OriginalFile=@"C:\private\original.wav",Segments=[new(reference,.1,1)]}]};
 var file=Path.Combine(root,"voice.myvoice");CreationPackage.ExportAiVoice(file,voice);var manifest=CreationPackage.Inspect(file);using var zip=ZipFile.OpenRead(file);using var reader=new StreamReader(zip.GetEntry("manifest.json")!.Open());var json=reader.ReadToEnd();if(manifest.Kind!="ai-voice"||manifest.AiVoice!.Samples.Count!=1||json.Contains("private")||json.Contains(root)||manifest.AiVoice.Revisions.Count!=0)throw new Exception("Private paths escaped");
 var drafts=new PublicationDraftStore(root);drafts.PrepareAsync(file).GetAwaiter().GetResult();var metadata=new PublicationDraftMetadata("Saved title","Saved description","unlisted","Other","tag");drafts.PrepareAsync(file,metadata).GetAwaiter().GetResult();var archived=Directory.GetFiles(Path.Combine(root,"drafts","history"),"*.myvoice").Single();if(CreationPackage.Inspect(archived).Title!=manifest.Title||System.Text.Json.JsonSerializer.Deserialize<PublicationDraftMetadata>(File.ReadAllText(archived+".json"))!=metadata)throw new Exception("Draft archive lost content or metadata");
 var extracted=CreationPackage.Extract(file,root);if(!File.Exists(Path.Combine(extracted.Directory,manifest.AiVoice.ReferenceFile)))throw new Exception("Missing audio");
 }finally{Directory.Delete(root,true);}});
 test("Imported AI voice cannot be exported as original",()=>{try{CreationPackage.ExportAiVoice("unused.myvoice",new(){SourceContentId="community-id"});throw new Exception("Not rejected");}catch(InvalidDataException){}});
 }
}
