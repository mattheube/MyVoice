using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;
namespace MyVoice.App.Services;
public sealed record CoverFrames(BitmapSource[] Frames,int[] Delays,Color? Accent=null);
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
        return new(result.ToArray(),delays.ToArray(),Accent(result[0]));
    }
    private static Color? Accent(BitmapSource image)
    {
        int stride=image.PixelWidth*4;var pixels=new byte[stride*image.PixelHeight];image.CopyPixels(pixels,stride,0);
        var weights=new double[12];var red=new double[12];var green=new double[12];var blue=new double[12];
        for(int y=0;y<image.PixelHeight;y+=Math.Max(1,image.PixelHeight/24))for(int x=0;x<image.PixelWidth;x+=Math.Max(1,image.PixelWidth/24)){
            int i=y*stride+x*4;if(pixels[i+3]<180)continue;
            double r=pixels[i+2],g=pixels[i+1],b=pixels[i],max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),delta=max-min;
            if(max<45||delta<25)continue;double hue=max==r?(g-b)/delta:max==g?2+(b-r)/delta:4+(r-g)/delta;if(hue<0)hue+=6;
            int bin=Math.Min(11,(int)(hue*2));double weight=delta/max;weights[bin]+=weight;red[bin]+=r*weight;green[bin]+=g*weight;blue[bin]+=b*weight;
        }
        int best=0;for(int i=1;i<12;i++)if(weights[i]>weights[best])best=i;if(weights[best]<1)return null;
        return Color.FromRgb((byte)(red[best]/weights[best]),(byte)(green[best]/weights[best]),(byte)(blue[best]/weights[best]));
    }
}
