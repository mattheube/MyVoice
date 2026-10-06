using NAudio.Wave;
namespace MyVoice.Audio;
/// <summary>Bounded queue: discard oldest samples instead of building latency.</summary>
public sealed class SampleRing : ISampleProvider
{
    private readonly float[] data;
    private int head, count;
    private readonly long[]? timestamps;
    public long LastReadTimestamp {get;private set;}
    public long FirstTimestamp {get{lock(sync)return count>0&&timestamps!=null?timestamps[head]:0;}}
    private readonly object sync = new();
    public WaveFormat WaveFormat
    {
        get;
    }
    public long Dropped
    {
        get; private set;
    }
    public int Count
    {
        get
        {
            lock (sync)
                return count;
        }
    }
    public SampleRing(int capacity, int channels = 1, bool trackTimestamps=false)
    {
        data = new float[capacity];
        if(trackTimestamps)timestamps=new long[capacity];
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, channels);
    }
    public void Write(ReadOnlySpan<float> samples, long firstTimestamp=0)
    {
        lock (sync)
        {
            int index=0;
            foreach (var sample in samples)
            {
                if (count == data.Length)
                {
                    head = (head + 1) % data.Length;
                    count--;
                    Dropped++;
                }
                data[(head + count) % data.Length] = sample;
                if(timestamps!=null)timestamps[(head+count)%data.Length]=firstTimestamp==0?0:firstTimestamp+(long)(index*System.Diagnostics.Stopwatch.Frequency/48000d);
                index++;
                count++;
            }
        }
    }
    public int Take(Span<float> target)
    {
        lock (sync)
        {
            int n = Math.Min(target.Length, count);
            LastReadTimestamp=n>0&&timestamps!=null?timestamps[head]:0;
            for (int i = 0; i < n; i++)
            {
                target[i] = data[head];
                head = (head + 1) % data.Length;
            }
            count -= n;
            target[n..].Clear();
            return n;
        }
    }
    public int Read(float[] buffer, int offset, int count)
    {
        Take(buffer.AsSpan(offset, count));
        return count;
    }
    public void Clear()
    {
        lock (sync)
        {
            head = 0;
            count = 0;
        }
    }
}
