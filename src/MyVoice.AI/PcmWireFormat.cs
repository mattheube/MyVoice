using System.Runtime.InteropServices;
namespace MyVoice.AI;

// The local worker protocol uses little-endian float32. Encode/decode directly
// against float storage instead of allocating an additional full byte buffer.
public static class PcmWireFormat
{
    public static string Encode(float[] samples)=>Convert.ToBase64String(MemoryMarshal.AsBytes(samples.AsSpan()));
    public static float[] Decode(string encoded)
    {
        const int maximumBytes=48000*4*60;
        if(encoded.Length%4!=0||encoded.Length>((maximumBytes+2)/3)*4)throw new InvalidDataException("Invalid AI audio response");
        int length=encoded.Length/4*3;
        if(encoded.EndsWith("==",StringComparison.Ordinal))length-=2;
        else if(encoded.EndsWith('=') )length--;
        if(length<0||length>maximumBytes||length%4!=0)throw new InvalidDataException("Invalid AI audio response");
        var result=new float[length/4];
        if(!Convert.TryFromBase64String(encoded,MemoryMarshal.AsBytes(result.AsSpan()),out int written)||written!=length)throw new InvalidDataException("Invalid AI audio response");
        return result;
    }
}
