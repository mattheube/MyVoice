using System.Windows;
using System.Windows.Media;
namespace MyVoice.App.Services;
public sealed class NavIcon : FrameworkElement
{
    public string Kind {get;set;}="home";
    protected override void OnRender(DrawingContext dc)
    {
        var path=Kind switch
        {
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
        dc.PushTransform(new ScaleTransform(ActualWidth/24,ActualHeight/24));
        var brush=(Brush)GetValue(System.Windows.Documents.TextElement.ForegroundProperty);
        dc.DrawGeometry(null,new Pen(brush,1.65){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round},Geometry.Parse(path));dc.Pop();
    }
}
