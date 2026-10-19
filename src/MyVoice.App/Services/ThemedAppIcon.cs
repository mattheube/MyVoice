using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MyVoice.App.Services;

/// <summary>Uses the same vector mark and accent as the in-app brand.</summary>
internal static class ThemedAppIcon
{
    private static Color? previous;
    public static void Refresh(System.Windows.Forms.NotifyIcon? tray)
    {
        if(Application.Current.Resources["AccentColor"] is not Color color)return;
        if(previous==color && tray?.Icon!=null)return;
        previous=color;
        var mark=new BrandMark();
        mark.SetValue(BrandMark.AccentProperty,new SolidColorBrush(color));
        mark.Measure(new Size(64,64));mark.Arrange(new Rect(0,0,64,64));
        var bitmap=new RenderTargetBitmap(64,64,96,96,PixelFormats.Pbgra32);
        bitmap.Render(mark);bitmap.Freeze();
        foreach(Window window in Application.Current.Windows)window.Icon=bitmap;
        if(tray==null)return;
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var png=new MemoryStream();encoder.Save(png);
        using var ico=new MemoryStream();
        using(var writer=new BinaryWriter(ico,System.Text.Encoding.UTF8,true))
        {
            writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)1);
            writer.Write((byte)64);writer.Write((byte)64);writer.Write((byte)0);writer.Write((byte)0);
            writer.Write((ushort)1);writer.Write((ushort)32);writer.Write((uint)png.Length);writer.Write((uint)22);writer.Write(png.ToArray());
        }
        ico.Position=0;using var loaded=new System.Drawing.Icon(ico);
        var old=tray.Icon;tray.Icon=(System.Drawing.Icon)loaded.Clone();old?.Dispose();
    }
}
