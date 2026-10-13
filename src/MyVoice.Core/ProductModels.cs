namespace MyVoice.Core;

public sealed class ProductState
{
    public int SchemaVersion { get; set; } = 1;
    public bool OnboardingCompleted { get; set; }
    public string DisplayName { get; set; } = "";
    public string FirstName {get;set;}="";
    public string LastName {get;set;}="";
    public List<ProductNotification> Notifications {get;set;}=[];
    public string Bio { get; set; } = "";
    public string? Avatar { get; set; }
    public string PublicationTitle {get;set;}="";
    public string PublicationKind {get;set;}="";
    public string PublicationDescription {get;set;}="";
    public string PublicationVisibility {get;set;}="private";
    public string PublicationCategory {get;set;}="Other";
    public string PublicationTags {get;set;}="";
    public string Theme { get; set; } = "Midnight";
    public string Accent { get; set; } = "#B7A5FF";
    public string Motion { get; set; } = "Full";
    public string Background { get; set; } = "Balanced";
    public string Density { get; set; } = "Comfortable";
    public List<Guid> FavoriteSounds { get; set; } = [];
    public List<string> FavoriteDesignedVoices {get;set;}=[];
    public List<LibraryCollection> Collections {get;set;}=[];
    public List<string> FavoriteAiVoices { get; set; } = [];
    public List<DesignedVoice> Presets { get; set; } = [];
    public List<InstalledCreation> Installed { get; set; } = [];
}
public sealed record DesignedVoice
{
    public string? SourceContentId {get;init;}
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
public sealed record LibraryEntry(string Kind,string Id);
public sealed class ProductNotification
{
    public string Text {get;set;}="";
    public string Category {get;set;}="application";
    public DateTime CreatedAt {get;set;}=DateTime.UtcNow;
    public bool Read {get;set;}
}
public sealed class LibraryCollection
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string Name {get;set;}="Collection";
    public List<LibraryEntry> Items {get;set;}=[];
}
