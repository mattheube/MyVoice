using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace MyVoice.App.Services;

// Native vector artwork, rendered once to the user's thumbnail cache.
internal static class OfficialPackArtwork
{
    public static string Create(string id,string directory)
    {
        Directory.CreateDirectory(directory);var path=Path.Combine(directory,id+"-v1.png");if(File.Exists(path))return path;
        int kind=id switch{"starter"=>0,"prankster"=>1,"memes"=>2,_=>3};
        var ink=new SolidColorBrush(Color.FromRgb(18,22,28));
        var color=(Color)ColorConverter.ConvertFromString(new[]{"#FFAE63","#F697CF","#D1EF70","#79DDE4"}[kind]);
        var accent=new SolidColorBrush(color);var visual=new DrawingVisual();
        using(var dc=visual.RenderOpen()){
            dc.DrawRectangle(ink,null,new Rect(0,0,640,360));
            var grid=new Pen(new SolidColorBrush(Color.FromArgb(30,color.R,color.G,color.B)),1);
            for(int x=0;x<640;x+=32)dc.DrawLine(grid,new Point(x,0),new Point(x,360));
            for(int y=0;y<360;y+=32)dc.DrawLine(grid,new Point(0,y),new Point(640,y));
            dc.PushTransform(new RotateTransform(kind%2==0?-12:12,320,180));
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(7,10,14)),null,new Rect(180,57,320,270),40,40);
            dc.DrawRoundedRectangle(accent,new Pen(new SolidColorBrush(Colors.White){Opacity=.25},2),new Rect(160,37,320,270),40,40);
            var stroke=new Pen(ink,12){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
            if(kind==0){for(int i=0;i<7;i++){double h=40+90*Math.Abs(Math.Sin(i*.8));dc.DrawLine(stroke,new Point(224+i*32,180-h/2),new Point(224+i*32,180+h/2));}}
            if(kind==1){var spring=new StreamGeometry();using(var c=spring.Open()){c.BeginFigure(new Point(215,125),false,false);for(int i=1;i<=36;i++)c.LineTo(new Point(215+i*6,180+55*Math.Sin(i*Math.PI/6)),true,false);}dc.DrawGeometry(null,stroke,spring);}
            if(kind==2){dc.DrawEllipse(ink,null,new Point(270,143),14,24);dc.DrawEllipse(ink,null,new Point(370,143),14,24);var smile=Geometry.Parse("M 246,204 C 280,264 364,264 398,204");dc.DrawGeometry(null,stroke,smile);}
            if(kind==3){var star=Geometry.Parse("M 320,80 L 346,151 426,176 346,201 320,274 294,201 214,176 294,151 Z");dc.DrawGeometry(ink,null,star);dc.DrawEllipse(accent,null,new Point(320,176),14,14);}
            dc.Pop();
            dc.DrawEllipse(null,new Pen(accent,3),new Point(548,75),20,20);dc.DrawLine(new Pen(accent,3),new Point(70,275),new Point(105,275));dc.DrawLine(new Pen(accent,3),new Point(87,258),new Point(87,292));
        }
        var bitmap=new RenderTargetBitmap(640,360,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))encoder.Save(file);return path;
    }
}
