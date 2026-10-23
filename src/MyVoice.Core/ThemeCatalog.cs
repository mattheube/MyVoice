namespace MyVoice.Core;

public sealed record ThemePalette(string Id,string Name,string Canvas,string Surface,string Raised,string Line,string Accent);
public static class ThemeCatalog
{
    // Persisted IDs from earlier releases are retained.
    public static IReadOnlyList<ThemePalette> All { get; } = new ThemePalette[]
    {
        new("Midnight","Midnight Violet","#101117","#191B25","#242735","#353948","#B7A5FF"),
        new("Graphite","Graphite","#191C20","#23272D","#2D333B","#3A414B","#BDCAD5"),
        new("Deep Ocean","Ocean","#081820","#102732","#193542","#2B4854","#6ACDFA"),
        new("Crimson","Crimson","#1D1119","#2D1B28","#3F2836","#60404E","#FF899B"),
        new("Emerald","Emerald","#0C1B18","#152D26","#213D34","#365C4F","#65DDB2"),
        new("Amber","Amber","#1B1710","#2A2418","#3C3220","#5C4C2E","#F3C36A"),
        new("Rose","Rose","#20151D","#30202C","#422D3B","#614759","#F2A5D1"),
        new("Ice","Ice","#111D24","#1B2D36","#29414B","#42616D","#A2E9ED"),
        new("Deep Blue","Deep Blue","#0E1428","#18223B","#243352","#3A4C73","#83ACFF"),
        new("Warm Dark","Warm Dark","#1C1615","#2A2220","#372C28","#514038","#E9BB88")
    };
    public static ThemePalette Find(string id)=>All.FirstOrDefault(p=>p.Id==id)??All[0];
}
