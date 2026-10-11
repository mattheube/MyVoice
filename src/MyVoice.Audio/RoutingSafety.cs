using MyVoice.Core;
namespace MyVoice.Audio;
public sealed record RoutingIssue(string ChannelId,string BusId,string Description);
public static class RoutingSafety
{
    public static IReadOnlyList<RoutingIssue> Inspect(AudioProfile profile)
    {
        var result=new List<RoutingIssue>();
        foreach(var route in profile.Routes.Where(r=>r.Enabled))
        {
            var source=profile.Channels.FirstOrDefault(c=>c.Id==route.ChannelId);var bus=profile.Outputs.FirstOrDefault(b=>b.Id==route.BusId);
            if(source==null||bus==null||bus.DeviceMode=="Internal")continue;
            if(source.Type=="system"&&(source.DeviceMode=="Default"&&bus.DeviceMode=="Default"||!string.IsNullOrEmpty(source.DeviceId)&&source.DeviceId==bus.DeviceId))result.Add(new(source.Id,bus.Id,$"{source.Name} → {bus.Name} → {source.Name}"));
            var input=CableKey(source.DeviceName);var output=CableKey(bus.DeviceName);
            if(input!=null&&input==output)result.Add(new(source.Id,bus.Id,$"{source.Name} → {bus.Name} → {source.Name}"));
        }
        // A bus may feed a different cable which eventually feeds the first source again.
        var edges=new List<(string From,string To,AudioRoute Route)>();
        foreach(var route in profile.Routes.Where(r=>r.Enabled))
        {
            var bus=profile.Outputs.FirstOrDefault(b=>b.Id==route.BusId);if(bus==null||bus.DeviceMode=="Internal")continue;
            foreach(var capture in profile.Channels)
            {
                bool sameCable=CableKey(capture.DeviceName) is {} cable&&cable==CableKey(bus.DeviceName);
                bool sameSystem=capture.Type=="system"&&(capture.DeviceId!=null&&capture.DeviceId==bus.DeviceId||capture.DeviceMode=="Default"&&bus.DeviceMode=="Default");
                if(sameCable||sameSystem)edges.Add((route.ChannelId,capture.Id,route));
            }
        }
        bool Reaches(string from,string target,HashSet<string> seen)
        {if(from==target)return true;if(!seen.Add(from))return false;return edges.Where(e=>e.From==from).Any(e=>Reaches(e.To,target,seen));}
        foreach(var edge in edges)if(Reaches(edge.To,edge.From,[])&&!result.Any(r=>r.ChannelId==edge.Route.ChannelId&&r.BusId==edge.Route.BusId))result.Add(new(edge.Route.ChannelId,edge.Route.BusId,"Circuit fermé entre plusieurs sources et sorties virtuelles"));
        return result;
    }
    public static AudioProfile Fix(AudioProfile profile)
    {
        var blocked=Inspect(profile).Select(x=>(x.ChannelId,x.BusId)).ToHashSet();
        return profile with{Routes=profile.Routes.Select(r=>blocked.Contains((r.ChannelId,r.BusId))?r with{Enabled=false}:r).ToList()};
    }
    public static string? CableKey(string? name)
    {
        if(string.IsNullOrWhiteSpace(name)||!name.Contains("CABLE",StringComparison.OrdinalIgnoreCase))return null;
        return System.Text.RegularExpressions.Regex.Replace(name.ToUpperInvariant(),"INPUT|OUTPUT|\\([^)]*\\)|\\s","");
    }
}

