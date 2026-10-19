using MyVoice.Core;
internal static class MediaValidationTests {
 internal static void Run(Action<string,Action> test){
 test("Preparing a voice activates reviewed samples and preserves the previous bank",()=>{
 var root=Path.Combine(Path.GetTempPath(),"MyVoice-review-"+Guid.NewGuid());Directory.CreateDirectory(root);
 try{var original=Path.Combine(root,"original.wav");var sample=Path.Combine(root,"sample.wav");
 foreach(var file in new[]{original,sample})using(var writer=new NAudio.Wave.WaveFileWriter(file,NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(48000,1))){var data=Enumerable.Range(0,96000).Select(i=>(float)(.12*Math.Sin(i*.1))).ToArray();writer.WriteSamples(data,0,data.Length);}
 var voice=new VoiceReference{Name="Reviewed voice",ReferenceFile=original,Duration=2};voice.EnsurePreparedReferences();voice.Samples.Add(new(){Segments=[new(sample,2,1)]});
 var package=Path.Combine(root,"pending.myvoice");MyVoice.Infrastructure.CreationPackage.ExportAiVoice(package,voice);if(MyVoice.Infrastructure.CreationPackage.Inspect(package).AiVoice!.Samples.Count!=0)throw new Exception("Pending samples published");
 MyVoice.AI.DatasetService.BuildReference(root,voice);
 if(voice.Revision!=2||voice.ActiveReferenceFiles.Single()!=sample||voice.Revisions.First().References!.Single()!=original||!File.Exists(original))throw new Exception("Revision did not preserve previous reference");
 var saved=System.Text.Json.JsonSerializer.Deserialize<VoiceReference>(System.Text.Json.JsonSerializer.Serialize(voice))!;if(saved.ActiveReferenceFiles.Single()!=sample)throw new Exception("Approved bank lost on restart");
 }finally{Directory.Delete(root,true);}});
 test("Dataset normalization preserves transients without adding clipping",()=>{
 var root=Path.Combine(Path.GetTempPath(),"MyVoice-normalize-"+Guid.NewGuid());Directory.CreateDirectory(root);
 try{var input=Path.Combine(root,"input.wav");var data=Enumerable.Range(0,96000).Select(i=>(float)(.02*Math.Sin(i*.1))).ToArray();data[48000]=.8f;using(var writer=new NAudio.Wave.WaveFileWriter(input,NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(48000,1)))writer.WriteSamples(data,0,data.Length);
 var sample=MyVoice.AI.DatasetService.Import(root,"voice",input,default);using var reader=new NAudio.Wave.AudioFileReader(sample.Segments.Single().File);var output=new float[(int)reader.Length/4];var count=reader.Read(output,0,output.Length);if(output.Take(count).Count(x=>Math.Abs(x)>=.949)>1||output.Take(count).Max(Math.Abs)>.951)throw new Exception("Transient clipped by normalization");
 }finally{Directory.Delete(root,true);}});
 test("Pending samples do not change the active reference bank",()=>{var file=Path.GetTempFileName();try{var voice=new VoiceReference{ReferenceFile=file};voice.EnsurePreparedReferences();voice.Samples.Add(new(){Segments=[new(file,2,1)]});if(voice.PreparedReferenceFiles!.Count!=0||voice.ActiveReferenceFiles.Single()!=file)throw new Exception("Unreviewed sample became active");var saved=System.Text.Json.JsonSerializer.Serialize(voice);var restored=System.Text.Json.JsonSerializer.Deserialize<VoiceReference>(saved)!;if(restored.PreparedReferenceFiles!.Count!=0)throw new Exception("Restart activated pending sample");}finally{File.Delete(file);}});

 test("Image and executable masquerading as audio rejected before decoding",()=>{var folder=Path.Combine(Path.GetTempPath(),"MyVoice-media-"+Guid.NewGuid());Directory.CreateDirectory(folder);try{foreach(var bytes in new[]{new byte[]{137,80,78,71,13,10,26,10},new byte[]{77,90,0,0,0,0,0,0}}){var path=Path.Combine(folder,"false.mp3");File.WriteAllBytes(path,bytes);try{MediaValidation.RequireAudio(path);throw new Exception("Accepted non audio");}catch(InvalidDataException){}}}finally{Directory.Delete(folder,true);}});
 test("Wrong image extension and audio content rejected from image zone",()=>{var file=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".png");try{File.WriteAllBytes(file,System.Text.Encoding.ASCII.GetBytes("RIFFxxxxWAVEfmt "));try{MediaValidation.RequireImage(file);throw new Exception("Accepted audio image");}catch(InvalidDataException){}}finally{File.Delete(file);}});
 }
}
