namespace MyVoice.Core;

public static class MixerReset
{
    /// <summary>Reset standard sources/buses, retaining user captures, outputs and their routes.</summary>
    public static AudioProfile Essentials(AudioProfile current,AppSettings preferences)
    {
        var baseline=MixerState.Recommended(new AppSettings
        {
            MonitoringDeviceId=preferences.MonitoringDeviceId,
            VirtualDeviceId=preferences.VirtualDeviceId
        });
        var channels=baseline.Channels.Select(c=>c.Id).ToHashSet();
        var buses=baseline.Outputs.Select(b=>b.Id).ToHashSet();
        return current with
        {
            Channels=baseline.Channels.Concat(current.Channels.Where(c=>!channels.Contains(c.Id))).ToList(),
            Outputs=baseline.Outputs.Concat(current.Outputs.Where(b=>!buses.Contains(b.Id))).ToList(),
            Routes=baseline.Routes.Concat(current.Routes.Where(r=>!channels.Contains(r.ChannelId)||!buses.Contains(r.BusId))).ToList()
        };
    }
}
