using NAudio.Wave;
namespace MyVoice.Soundboard;
public sealed record SoundEdit(double Start,double End,double FadeIn=0,double FadeOut=0,bool Normalize=false);
public static class SoundEditor
{
    public static Task RenderAsync(string input,string output,SoundEdit edit,CancellationToken token)=>Task.Run(()=>
    {
        using var reader=new AudioFileReader(input);var duration=reader.TotalTime.TotalSeconds;
        if(!double.IsFinite(edit.Start)||!double.IsFinite(edit.End)||!double.IsFinite(edit.FadeIn)||!double.IsFinite(edit.FadeOut)||edit.Start<0||edit.End>duration+.001||edit.End<=edit.Start||edit.FadeIn<0||edit.FadeOut<0)throw new InvalidDataException("La sélection et les fondus doivent être dans la durée du son.");
        if(Path.GetFullPath(input).Equals(Path.GetFullPath(output),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("L’original doit être conservé.");
        var rate=reader.WaveFormat.SampleRate;var channels=reader.WaveFormat.Channels;
        long frames=(long)Math.Round((edit.End-edit.Start)*rate);var buffer=new float[4096*channels];float peak=0;
        if(edit.Normalize)
        {
            reader.CurrentTime=TimeSpan.FromSeconds(edit.Start);long remaining=frames*channels;
            while(remaining>0){token.ThrowIfCancellationRequested();int count=reader.Read(buffer,0,(int)Math.Min(buffer.Length,remaining));if(count==0)break;for(int i=0;i<count;i++)peak=Math.Max(peak,Math.Abs(buffer[i]));remaining-=count;}
        }
        double gain=peak>0?.95/peak:1;reader.CurrentTime=TimeSpan.FromSeconds(edit.Start);var temporary=output+".part";
        try
        {
            using(var writer=new WaveFileWriter(temporary,WaveFormat.CreateIeeeFloatWaveFormat(rate,channels)))
            {
                long written=0;while(written<frames*channels)
                {
                    token.ThrowIfCancellationRequested();int count=reader.Read(buffer,0,(int)Math.Min(buffer.Length,frames*channels-written));if(count==0)break;
                    for(int i=0;i<count;i++){double frame=(written+i)/channels;double fade=1;if(edit.FadeIn>0)fade=Math.Min(fade,frame/(rate*edit.FadeIn));if(edit.FadeOut>0)fade=Math.Min(fade,(frames-1-frame)/(rate*edit.FadeOut));buffer[i]=(float)(buffer[i]*gain*Math.Clamp(fade,0,1));}
                    writer.WriteSamples(buffer,0,count);written+=count;
                }
            }
            File.Move(temporary,output,false);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    },token);
}
