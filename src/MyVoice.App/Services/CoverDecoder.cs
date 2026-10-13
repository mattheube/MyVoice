using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;
namespace MyVoice.App.Services;
public sealed record CoverFrames(BitmapSource[] Frames,int[] Delays);
public static class CoverDecoder
{
    public static CoverFrames Read(string path,int size=256,bool animate=true)
    {
        if(new FileInfo(path).Length>20_000_000)throw new InvalidDataException("Image limitée à 20 Mo.");
        using var stream=File.OpenRead(path);using var codec=SKCodec.Create(stream)??throw new InvalidDataException("Image non reconnue.");
        if((long)codec.Info.Width*codec.Info.Height>16_000_000)throw new InvalidDataException("Image limitée à 16 mégapixels.");
        int count=animate?Math.Max(1,codec.FrameCount):1;
        if(count>120)throw new InvalidDataException("Animation limitée à 120 images.");
        var result=new List<BitmapSource>();var delays=new List<int>();
        using var decoded=new SKBitmap(new SKImageInfo(codec.Info.Width,codec.Info.Height,SKColorType.Bgra8888,SKAlphaType.Premul));
        var factor=Math.Min(1d,(double)size/Math.Max(decoded.Width,decoded.Height));
        var width=Math.Max(1,(int)(decoded.Width*factor));var height=Math.Max(1,(int)(decoded.Height*factor));
        for(int i=0;i<count;i++)
        {
            var status=codec.GetPixels(decoded.Info,decoded.GetPixels(),new SKCodecOptions(i));
            if(status!=SKCodecResult.Success)throw new InvalidDataException("Image incomplète.");
            using var scaled=new SKBitmap(new SKImageInfo(width,height,SKColorType.Bgra8888,SKAlphaType.Premul));
            using(var canvas=new SKCanvas(scaled)){canvas.Clear(SKColors.Transparent);canvas.DrawBitmap(decoded,new SKRect(0,0,width,height),new SKSamplingOptions(SKFilterMode.Linear));}
            var bitmap=BitmapSource.Create(width,height,96,96,PixelFormats.Pbgra32,null,scaled.GetPixels(),scaled.ByteCount,scaled.RowBytes);bitmap.Freeze();result.Add(bitmap);
            delays.Add(codec.FrameCount>0?Math.Clamp(codec.FrameInfo[i].Duration,20,10000):100);
        }
        return new(result.ToArray(),delays.ToArray());
    }
}
