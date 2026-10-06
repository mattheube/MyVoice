using MyVoice.Core;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
namespace MyVoice.AI;
/// <summary>Preserves every original; streaming analysis uses bounded eight-second windows.</summary>
public static class DatasetService
{
    public static DatasetSample Import(string root, string voiceId, string file, CancellationToken token)
    {
        var sample = new DatasetSample { Name=Path.GetFileName(file) };
        var folder=Path.Combine(root,"datasets",voiceId,sample.Id); Directory.CreateDirectory(folder);
        sample.OriginalFile=Path.Combine(folder,"original"+Path.GetExtension(file));
        File.Copy(file,sample.OriginalFile,false);
        try
        {
            using var reader=new AudioFileReader(sample.OriginalFile);
            sample.SampleRate=reader.WaveFormat.SampleRate; sample.Channels=reader.WaveFormat.Channels; sample.Duration=reader.TotalTime.TotalSeconds;
            ISampleProvider source=reader;
            if(source.WaveFormat.Channels==2) source=new StereoToMonoSampleProvider(source);
            if(source.WaveFormat.Channels!=1) throw new InvalidDataException("Mono/stereo required");
            if(source.WaveFormat.SampleRate!=48000) source=new WdlResamplingSampleProvider(source,48000);
            var block=new float[48000*8]; long total=0,clipped=0,active=0; double sum=0,peak=0; int index=0;
            while(true)
            {
                token.ThrowIfCancellationRequested(); int count=0,n;
                while(count<block.Length && (n=source.Read(block,count,block.Length-count))>0) count+=n;
                if(count==0) break;
                double windowSum=0; long windowActive=0,windowClipped=0;
                for(int i=0;i<count;i++) { var x=float.IsFinite(block[i])?block[i]:0; block[i]=x; peak=Math.Max(peak,Math.Abs(x)); sum+=x*x; windowSum+=x*x; if(Math.Abs(x)>=.995) {clipped++;windowClipped++;} }
                for(int j=0;j<count;j+=960) { double power=0; var size=Math.Min(960,count-j);for(int i=0;i<size;i++)power+=block[j+i]*block[j+i];if(Math.Sqrt(power/size)>.006)windowActive+=size; }
                active+=windowActive;total+=count;
                if(windowActive>=48000)
                {
                    var start=0;var end=count-1;while(start<end&&Math.Abs(block[start])<.004)start++;while(end>start&&Math.Abs(block[end])<.004)end--;
                    start=Math.Max(0,start-2400);end=Math.Min(count-1,end+2400);
                    var length=end-start+1; var rms=Math.Sqrt(windowSum/count);
                    var gain=Math.Min(3,.12/Math.Max(.001,rms));
                    var data=block.AsSpan(start,length).ToArray();for(int i=0;i<data.Length;i++)data[i]=(float)Math.Clamp(data[i]*gain,-.95,.95);
                    var path=Path.Combine(folder,$"segment-{index++:D4}.wav");
                    using(var writer=new WaveFileWriter(path,WaveFormat.CreateIeeeFloatWaveFormat(48000,1)))writer.WriteSamples(data,0,data.Length);
                    var score=windowActive/(double)count - 5*windowClipped/(double)count - Math.Abs(Calibration.Db(rms)+20)/100;
                    sample.Segments.Add(new(path,length/48000d,score));
                }
            }
            sample.UsableSeconds=active/48000d; sample.ClippingRatio=clipped/(double)Math.Max(1,total);sample.PeakDb=Calibration.Db(peak);sample.RmsDb=Calibration.Db(Math.Sqrt(sum/Math.Max(1,total)));
            sample.Excluded=sample.Segments.Count==0 || sample.ClippingRatio>.03;
            sample.Quality=sample.Excluded?"Poor":sample.ClippingRatio>.001||sample.UsableSeconds<sample.Duration*.4?"Usable":"Good";
            sample.Notes=sample.ClippingRatio>.001?"Clipping detected":sample.UsableSeconds<sample.Duration*.4?"Long silence / low level":"Signal levels suitable";
            sample.Notes+=" · Activity is an energy estimate; music / speakers / reverb not classified.";
        }
        catch(OperationCanceledException){throw;}
        catch(Exception e){sample.Excluded=true;sample.Quality="Unreadable";sample.Notes=e.Message;}
        return sample;
    }
    public static void BuildReference(string root, VoiceReference voice)
    {
        // The dataset is unlimited; this backend has a short conditioning window, not a training stage.
        var candidates=voice.Samples.Where(s=>!s.Excluded).SelectMany(s=>s.Segments.OrderByDescending(x=>x.Score).Take(3)).OrderByDescending(s=>s.Score).ToList();
        if(candidates.Count==0) throw new InvalidDataException("No usable segments. Keep a clean sample before preparing the voice.");
        var data=new List<float>(576000);
        foreach(var segment in candidates)
        {
            using var reader=new AudioFileReader(segment.File);var block=new float[Math.Min(48000*6,576000-data.Count)];var n=reader.Read(block,0,block.Length);
            for(int i=0;i<n;i++) { float fade=Math.Min(1,Math.Min(i/960f,(n-1-i)/960f));data.Add(block[i]*fade); }
            if(data.Count>=576000)break;
        }
        if(data.Count<48000)throw new InvalidDataException("At least one second of usable speech required.");
        var version=voice.Revisions.Count==0 ? voice.Revision+1 : Math.Max(voice.Revision,voice.Revisions.Max(r=>r.Number))+1;
        var folder=Path.Combine(root,"voice-models",voice.Id);Directory.CreateDirectory(folder);
        var path=Path.Combine(folder,$"reference-v{version}-{Guid.NewGuid():N}.wav");
        using(var writer=new WaveFileWriter(path,WaveFormat.CreateIeeeFloatWaveFormat(48000,1)))writer.WriteSamples(data.ToArray(),0,data.Count);
        if(File.Exists(voice.ReferenceFile)&&!voice.Revisions.Any(r=>r.ReferenceFile==voice.ReferenceFile))voice.Revisions.Add(new(voice.Revision,voice.ReferenceFile,DateTime.UtcNow));
        voice.Revisions.Add(new(version,path,DateTime.UtcNow));voice.Revision=version;voice.ReferenceFile=path;voice.Duration=data.Count/48000d;
    }
}
