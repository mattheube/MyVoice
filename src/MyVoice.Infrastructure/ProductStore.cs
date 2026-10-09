using System.Text.Json;
using MyVoice.Core;
namespace MyVoice.Infrastructure;

public sealed class ProductStore(string root)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private string PathName => Path.Combine(root,"config","product.json");
    public ProductState Load()
    {
        if(File.Exists(PathName))
        {
            var state=JsonSerializer.Deserialize<ProductState>(File.ReadAllText(PathName))??throw new InvalidDataException("Profil produit invalide");
            if(state.SchemaVersion!=1)throw new InvalidDataException("Version du profil produit non prise en charge");
            return state;
        }
        var backup=Path.Combine(root,"migration-backup","product-v1");
        Directory.CreateDirectory(backup);
        foreach(var relative in new[]{"config/config.json","soundboards/library.json","voices/voices.json"})
        {
            var source=Path.Combine(root,relative);
            if(!File.Exists(source))continue;
            using var parsed=JsonDocument.Parse(File.ReadAllText(source));
            var target=Path.Combine(backup,relative);Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if(!File.Exists(target))File.Copy(source,target);
        }
        var fresh=new ProductState();Save(fresh);return fresh;
    }
    public void Save(ProductState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);
        var json=JsonSerializer.Serialize(state,Json);
        _=JsonSerializer.Deserialize<ProductState>(json)??throw new InvalidDataException("Profil produit invalide");
        var temp=PathName+".tmp";
        File.WriteAllText(temp,json);
        if(File.Exists(PathName))File.Copy(PathName,PathName+".backup",true);
        File.Move(temp,PathName,true);
    }
}
