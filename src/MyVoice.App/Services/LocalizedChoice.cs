using System.ComponentModel;
namespace MyVoice.App.Services;
public sealed class LocalizedChoice : INotifyPropertyChanged
{
    private readonly Texts texts;
    private readonly string nameKey, descriptionKey;
    public string Id {get;}
    public string Name=>texts[nameKey];
    public string Description=>texts[descriptionKey];
    public event PropertyChangedEventHandler? PropertyChanged;
    public LocalizedChoice(Texts texts,string id,string nameKey,string descriptionKey="")
    {
        this.texts=texts;Id=id;this.nameKey=nameKey;this.descriptionKey=descriptionKey;
        texts.PropertyChanged+=(_,_)=>{PropertyChanged?.Invoke(this,new(nameof(Name)));PropertyChanged?.Invoke(this,new(nameof(Description)));};
    }
    public override string ToString()=>Name;
}
