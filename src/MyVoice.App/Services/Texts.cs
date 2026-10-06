using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
namespace MyVoice.App.Services;
public sealed class Texts : INotifyPropertyChanged
{
    private Dictionary<string, string> values = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    public string this[string key] => values.TryGetValue(key, out var value) ? value : key;
    public void Load(string language)
    {
        using var stream = typeof(Texts).Assembly.GetManifestResourceStream($"MyVoice.Resources.{language}.json") ?? throw new FileNotFoundException(language);
        values = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
        PropertyChanged?.Invoke(this, new("Item[]"));
    }
}
