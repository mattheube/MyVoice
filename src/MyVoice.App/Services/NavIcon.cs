using System.Windows;
using System.Windows.Media;
namespace MyVoice.App.Services;
public class NavIcon : FrameworkElement
{
    public string Kind {get;set;}="home";
    private static readonly System.Collections.Generic.Dictionary<string,Geometry> geometries=new();
    private static Geometry GetGeometry(string kind)
    {
        if(geometries.TryGetValue(kind,out var cached))return cached;
        var path=kind switch
        {
            "headphones"=>"M 3,14 V 11 A 9,9 0 0 1 21,11 V 14 M 3,12 H 7 V 21 H 3 Z M 17,12 H 21 V 21 H 17 Z",
            "inspector"=>"M 2,3 H 22 V 21 H 2 Z M 15,3 V 21 M 5,7 H 11 M 5,11 H 11",
            "collection"=>"M 2,6 V 21 H 22 V 6 H 12 L 9,3 H 2 Z M 8,14 H 16 M 12,10 V 18",
            "collection-remove"=>"M 2,6 V 21 H 22 V 6 H 12 L 9,3 H 2 Z M 8,14 H 16",
            "library-remove"=>"M 3,3 H 7 V 21 H 3 Z M 10,3 H 14 V 21 H 10 Z M 17,12 H 23",
            "play"=>"M 6,3 L 21,12 6,21 Z",
            "keyboard"=>"M 2,5 H 22 V 19 H 2 Z M 5,9 H 7 M 10,9 H 12 M 15,9 H 17 M 5,13 H 7 M 10,13 H 12 M 15,13 H 19 M 7,16 H 17",
            "edit"=>"M 4,16 L 16,4 20,8 8,20 3,21 Z M 13,7 L 17,11",
            "upload"=>"M 12,16 V 2 M 6,8 L 12,2 18,8 M 3,15 V 22 H 21 V 15",
            "heart" or "heart-filled"=>"M 12,21 L 3,12 C -3,3 8,-1 12,6 C 16,-1 27,3 21,12 Z",
            "trash"=>"M 3,6 H 21 M 7,6 V 3 H 17 V 6 M 5,6 L 6,22 H 18 L 19,6 M 10,10 V 18 M 14,10 V 18",
            "search"=>"M 3,10 A 7,7 0 1 0 17,10 A 7,7 0 1 0 3,10 M 15,15 L 22,22",
            "bell"=>"M 5,17 H 19 L 17,14 V 9 C 17,2 7,2 7,9 V 14 Z M 9,20 Q 12,24 15,20",
            "home"=>"M 3,10 L 12,3 L 21,10 M 5,9 V 21 H 19 V 9 M 10,21 V 14 H 14 V 21",
            "mic"=>"M 9,5 C 9,1 15,1 15,5 V 12 C 15,16 9,16 9,12 Z M 5,11 V 13 C 5,22 19,22 19,13 V 11 M 12,20 V 23",
            "profile"=>"M 8,7 A 4,4 0 1 0 16,7 A 4,4 0 1 0 8,7 M 3,22 C 3,11 21,11 21,22",
            "voice"=>"M 3,10 V 14 M 7,5 V 19 M 12,2 V 22 M 17,7 V 17 M 21,10 V 14",
            "soundboard"=>"M 3,3 H 10 V 10 H 3 Z M 14,3 H 21 V 10 H 14 Z M 3,14 H 10 V 21 H 3 Z M 14,14 H 21 V 21 H 14 Z",
            "mixer"=>"M 5,2 V 7 M 5,13 V 22 M 2,7 H 8 V 13 H 2 Z M 18,2 V 13 M 18,19 V 22 M 15,13 H 21 V 19 H 15 Z",
            "community"=>"M 4,9 A 3,3 0 1 0 10,9 A 3,3 0 1 0 4,9 M 14,6 A 3,3 0 1 0 20,6 A 3,3 0 1 0 14,6 M 1,21 C 1,12 13,12 13,21 M 13,13 C 18,11 23,14 23,20",
            "settings"=>"M 3,6 H 8 M 14,6 H 21 M 3,18 H 14 M 20,18 H 21 M 8,6 A 3,3 0 1 0 14,6 A 3,3 0 1 0 8,6 M 14,18 A 3,3 0 1 0 20,18 A 3,3 0 1 0 14,18",
            _=>"M 8,2 H 16 M 10,2 V 10 L 3,20 Q 2,22 5,22 H 19 Q 22,22 21,20 L 14,10 V 2 M 7,15 H 17"
        };
        var geometry=Geometry.Parse(path);geometry.Freeze();geometries[kind]=geometry;return geometry;
    }
    protected override void OnRender(DrawingContext dc)
    {
        dc.PushTransform(new ScaleTransform(ActualWidth/24,ActualHeight/24));
        var brush=(Brush)GetValue(System.Windows.Documents.TextElement.ForegroundProperty);
        dc.DrawGeometry(Kind=="heart-filled"?brush:null,new Pen(brush,1.65){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round},GetGeometry(Kind));dc.Pop();
    }
}
