using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
namespace MyVoice.App.Services;
public sealed class ApplicationIcon : IValueConverter
{
    private static readonly Dictionary<string,BitmapSource?> cache=new(StringComparer.OrdinalIgnoreCase);
    public object? Convert(object value,Type target,object parameter,CultureInfo culture)
    {
        if(value is not string path||string.IsNullOrEmpty(path))return null;
        if(cache.TryGetValue(path,out var image))return image;
        try
        {
            using var icon=System.Drawing.Icon.ExtractAssociatedIcon(path);
            if(icon!=null){image=Imaging.CreateBitmapSourceFromHIcon(icon.Handle,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(24,24));image.Freeze();}
        }
        catch{image=null;}
        if(cache.Count>=96)cache.Remove(System.Linq.Enumerable.First(cache.Keys));cache[path]=image;return image;
    }
    public object ConvertBack(object value,Type target,object parameter,CultureInfo culture)=>Binding.DoNothing;
}
