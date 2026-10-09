namespace MyVoice.Core;

public sealed class ProductState
{
    public int SchemaVersion { get; set; } = 1;
    public bool OnboardingCompleted { get; set; }
    public string DisplayName { get; set; } = "";
    public string Bio { get; set; } = "";
    public string? Avatar { get; set; }
    public string Theme { get; set; } = "Midnight";
    public string Accent { get; set; } = "#B7A5FF";
    public string Motion { get; set; } = "Full";
    public string Background { get; set; } = "Balanced";
    public List<Guid> FavoriteSounds { get; set; } = [];
    public List<string> FavoriteAiVoices { get; set; } = [];
    public List<DesignedVoice> Presets { get; set; } = [];
    public List<InstalledCreation> Installed { get; set; } = [];
}
public sealed record DesignedVoice
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "Ma voix";
    public string Base { get; init; } = "Clean";
    public double Pitch { get; init; } = 1;
    public double Bass { get; init; }
    public double Mid { get; init; }
    public double Treble { get; init; }
    public double Distortion { get; init; }
    public double Delay { get; init; }
    public double Mix { get; init; } = 1;
}
public sealed record InstalledCreation(string ContentId, string Version, string Kind, string Title, DateTime InstalledAt);
