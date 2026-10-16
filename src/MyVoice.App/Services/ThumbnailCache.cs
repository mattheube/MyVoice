using System.Collections.Generic;
using System.Linq;
namespace MyVoice.App.Services;
// Accessed on the UI dispatcher; decoded frames are frozen before insertion.
internal static class ThumbnailCache
{
    private const long Limit=64L*1024*1024;
    private static readonly Dictionary<string,LinkedListNode<(string Key,CoverFrames Value,long Bytes)>> entries=new();
    private static readonly LinkedList<(string Key,CoverFrames Value,long Bytes)> lru=new();
    private static long bytes;
    public static bool TryGetValue(string key,out CoverFrames value)
    {
        if(entries.TryGetValue(key,out var node)){lru.Remove(node);lru.AddFirst(node);value=node.Value.Value;return true;}
        value=null!;return false;
    }
    public static void Add(string key,CoverFrames value)
    {
        long size=value.Frames.Sum(f=>(long)f.PixelWidth*f.PixelHeight*4);
        if(size>Limit)return;
        if(entries.ContainsKey(key))return;
        while(lru.Last is {} old&&(bytes+size>Limit||entries.Count>=96)){bytes-=old.Value.Bytes;entries.Remove(old.Value.Key);lru.RemoveLast();}
        entries[key]=lru.AddFirst((key,value,size));bytes+=size;
    }
    public static void Clear(){entries.Clear();lru.Clear();bytes=0;}
}
