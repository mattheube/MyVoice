using NAudio.Dsp;
namespace MyVoice.Soundboard;
/// <summary>Independent stereo tone stage per playing sound. Gain ramps avoid clicks.</summary>
public sealed class SoundToneProcessor
{
    private BiQuadFilter? left,right;
    private double previousBass=double.NaN;
    private double gain=double.NaN;
    public void Process(float[] samples,int count,double volume,double bassDb,double saturation)
    {
        bassDb=Math.Clamp(double.IsFinite(bassDb)?bassDb:0,-24,24);
        saturation=Math.Clamp(double.IsFinite(saturation)?saturation:0,0,1);
        if(bassDb!=previousBass){left=BiQuadFilter.LowShelf(48000,140,1,(float)bassDb);right=BiQuadFilter.LowShelf(48000,140,1,(float)bassDb);previousBass=bassDb;}
        double target=double.IsFinite(volume)?Math.Clamp(volume,0,1e6):0;
        if(double.IsNaN(gain))gain=target;
        var drive=Math.Pow(10,saturation*2);
        for(int i=0;i<count;i+=2)
        {
            gain+=(target-gain)*.004;
            for(int channel=0;channel<2&&i+channel<count;channel++)
            {
                var value=samples[i+channel];if(!float.IsFinite(value))value=0;
                if(bassDb!=0)value=(channel==0?left!:right!).Transform(value);
                if(saturation>0)value=(float)(Math.Tanh(value*drive)/Math.Tanh(drive));
                samples[i+channel]=(float)Math.Clamp(value*gain,-1e6,1e6);
            }
        }
    }
}
