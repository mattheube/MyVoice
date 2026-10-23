using System.Diagnostics;
using MyVoice.Audio;

static class MicrophoneEnhanceTests
{
    public static void Run(Action<string,Action> test)
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        test("Bundled neural microphone model loads without external runtime",()=>Check(MicrophoneEnhancer.Available,"RNNoise DLL missing or incompatible"));
        test("Microphone Off preserves samples exactly",()=>{
            using var effect=new MicrophoneEnhancer();var random=new Random(82);
            var input=Enumerable.Range(0,480).Select(_=>(float)(random.NextDouble()*2-1)).ToArray();var expected=input.ToArray();
            effect.Process(input);Check(input.SequenceEqual(expected),"Off changed microphone samples");
        });
        test("Neural microphone modes preserve silence and sanitize invalid input",()=>{
            foreach(var mode in new[]{MicrophoneEnhanceMode.Light,MicrophoneEnhanceMode.Balanced,MicrophoneEnhanceMode.Strong}){
                using var effect=new MicrophoneEnhancer{Mode=mode};var frame=new float[480];
                for(int i=0;i<100;i++){Array.Clear(frame);effect.Process(frame);Check(frame.All(x=>float.IsFinite(x)&&Math.Abs(x)<.00001),"Noise generated from silence");}
                frame[2]=float.NaN;frame[9]=float.PositiveInfinity;effect.Process(frame);Check(frame.All(x=>float.IsFinite(x)&&Math.Abs(x)<=1),"Invalid output");
                effect.Mode=MicrophoneEnhanceMode.Off;effect.Process(frame);Array.Fill(frame,.1f);effect.Process(frame);Check(frame.All(x=>x==.1f),"Off did not release enhancement");
            }
        });
        test("Neural microphone suppresses stationary noise within realtime budget",()=>{
            using var effect=new MicrophoneEnhancer{Mode=MicrophoneEnhanceMode.Strong};var random=new Random(438);var frame=new float[480];
            double before=0,after=0;long ticks=0;
            for(int f=0;f<600;f++){
                for(int i=0;i<frame.Length;i++)frame[i]=(float)((random.NextDouble()*2-1)*.04);
                if(f>=100)before+=frame.Sum(x=>(double)x*x);
                long start=Stopwatch.GetTimestamp();effect.Process(frame);if(f>=100){ticks+=Stopwatch.GetTimestamp()-start;after+=frame.Sum(x=>(double)x*x);}
            }
            double ms=ticks*1000d/Stopwatch.Frequency/500,ratio=after/before;
            Console.WriteLine($"RNNoise: mean {ms:F3} ms per 10 ms frame; stationary noise {10*Math.Log10(ratio):F1} dB");
            // Broadband hiss varies with the noise realization; it must not be amplified.
            Check(ratio<.9,"Broadband noise not reduced");Check(ms<10,"Neural enhancer cannot keep up with real time on this machine");
            double fan=0;before=0;after=0;
            for(int f=0;f<600;f++){
                for(int i=0;i<480;i++){fan=.95*fan+.05*(random.NextDouble()*2-1)*.2;frame[i]=(float)fan;}
                if(f>=100)before+=frame.Sum(x=>(double)x*x);effect.Process(frame);if(f>=100)after+=frame.Sum(x=>(double)x*x);
            }
            Console.WriteLine($"RNNoise: low-frequency fan noise {10*Math.Log10(after/before):F1} dB");
            Check(after/before<.25,"Fan noise not sufficiently reduced");
        });
    }
}
