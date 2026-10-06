using MyVoice.Audio;
using System.Diagnostics;
public static class StreamingQueueTests
{
    public static void Run()
    {
        var queue=new SampleRing(960,trackTimestamps:true);var stamp=Stopwatch.GetTimestamp();queue.Write(new float[720],stamp);queue.Write(new float[720],stamp+(long)(720*Stopwatch.Frequency/48000d));
        if(queue.Count!=960||queue.Dropped!=480)throw new Exception("Unbounded queue");
        var read=new float[480];queue.Take(read);var expected=stamp+(long)(480*Stopwatch.Frequency/48000d);
        if(Math.Abs(queue.LastReadTimestamp-expected)>2)throw new Exception("Timestamp not advanced after dropped input");
        Console.WriteLine("PASS Bounded input drops oldest samples and preserves timestamps");queue.Clear();queue.Take(read);if(queue.LastReadTimestamp!=0||read.Any(x=>x!=0))throw new Exception("Stale timestamp after reset");Console.WriteLine("PASS Empty/reset queue cannot report stale latency");
    }
}
