using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MyVoice.Core;
namespace MyVoice.App.Services;

public static class ProductAppearance
{
    public static bool Animate {get;private set;}=true;
    public static void Apply(ProductState state)
    {
        Animate=state.Motion=="Full" && SystemParameters.ClientAreaAnimation;
        var palette=state.Theme switch
        {
            "Graphite"=>new[]{"#191C20","#23272D","#2D333B","#3A414B"},
            "Deep Ocean"=>new[]{"#081820","#102732","#193542","#2B4854"},
            "Crimson"=>new[]{"#1D1119","#2D1B28","#3F2836","#60404E"},
            "Emerald"=>new[]{"#0C1B18","#152D26","#213D34","#365C4F"},
            "Warm Dark"=>new[]{"#1C1615","#2A2220","#372C28","#514038"},
            _=>new[]{"#101117","#191B25","#242735","#353948"}
        };
        var resources=Application.Current.Resources;
        string[] keys={"CanvasBrush","SurfaceBrush","RaisedBrush","LineBrush"};
        for(int i=0;i<keys.Length;i++)resources[keys[i]]=Brush(palette[i]);
        Color accent;try{accent=(Color)ColorConverter.ConvertFromString(state.Accent);}catch{accent=(Color)ColorConverter.ConvertFromString("#B7A5FF");}
        resources["AccentBrush"]=new SolidColorBrush(accent);
        resources["Accent"]=new SolidColorBrush(accent);
        resources["AccentColor"]=accent;
        resources["AccentHoverBrush"]=new SolidColorBrush(Color.FromRgb((byte)(accent.R*.85+38),(byte)(accent.G*.85+38),(byte)(accent.B*.85+38)));
        resources["AccentPressedBrush"]=new SolidColorBrush(Color.FromRgb((byte)(accent.R*.8),(byte)(accent.G*.8),(byte)(accent.B*.8)));
        resources["AccentGlowBrush"]=new SolidColorBrush(Color.FromArgb(80,accent.R,accent.G,accent.B));
        var canvas=(Color)ColorConverter.ConvertFromString(palette[0]);
        resources["OverlayBrush"]=new SolidColorBrush(Color.FromArgb(235,canvas.R,canvas.G,canvas.B));
        resources["CardPadding"]=new Thickness(state.Density=="Compact"?12:18);
        resources["NavPadding"]=new Thickness(14,state.Density=="Compact"?9:13,14,state.Density=="Compact"?9:13);
        resources["AccentSoftBrush"]=new SolidColorBrush(Color.FromArgb(32,accent.R,accent.G,accent.B));
        var brightness=.2126*accent.R+.7152*accent.G+.0722*accent.B;
        resources["AccentInkBrush"]=Brush(brightness>145?"#101117":"#FFFFFF");
        resources["AmbientOpacity"]=state.Background=="Minimal"?0d:state.Background=="Rich"?.15d:.07d;
    }
    private static SolidColorBrush Brush(string value)=>new((Color)ColorConverter.ConvertFromString(value));
}

public sealed class BrandMark : FrameworkElement
{
    private static readonly Geometry Mark=Geometry.Parse("F0 M 7,18 L 22,9 L 32,28 L 42,9 L 57,18 L 38,55 L 26,55 Z M 20,24 L 32,47 L 44,24 L 38,28 L 32,39 L 26,28 Z");
    protected override void OnRender(DrawingContext dc)
    {
        var scale=System.Math.Min(ActualWidth,ActualHeight)/64;
        dc.PushTransform(new TranslateTransform((ActualWidth-64*scale)/2,(ActualHeight-64*scale)/2));
        dc.PushTransform(new ScaleTransform(scale,scale));
        dc.DrawGeometry((Brush)(TryFindResource("AccentBrush")??Brushes.MediumPurple),null,Mark);dc.Pop();dc.Pop();
    }
}
public sealed class BrandWordmark : FrameworkElement
{
    private static readonly Geometry Mark=Geometry.Parse("M 3,35 V 9 L 15,24 L 27,9 V 35 M 39,19 L 47,35 L 55,19 M 47,35 L 42,45 M 68,9 L 80,35 L 92,9 M 113,18 C 99,18 99,35 113,35 C 127,35 127,18 113,18 M 137,19 V 35 M 137,10 L 137,10.5 M 166,21 C 149,10 147,43 166,32 M 180,27 H 198 C 198,12 177,17 178,27 C 178,39 193,35 198,32");
    protected override void OnRender(DrawingContext dc)
    {
        dc.PushTransform(new ScaleTransform(ActualWidth/204,ActualHeight/50));
        var pen=new Pen((Brush)(TryFindResource("TextBrush")??Brushes.WhiteSmoke),3.8){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
        dc.DrawGeometry(null,pen,Mark);dc.Pop();
    }
}

public static class Motion
{
    public static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(Motion),new PropertyMetadata(false,Changed));
    public static bool GetEnabled(DependencyObject o)=>(bool)o.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject o,bool v)=>o.SetValue(EnabledProperty,v);
    private static void Changed(DependencyObject o,DependencyPropertyChangedEventArgs e)
    {
        if(o is not Button button||!(bool)e.NewValue)return;
        button.MouseEnter+=(_,_)=>{if(ProductAppearance.Animate)button.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(.82,1,TimeSpan.FromMilliseconds(160)));};
    }
}
