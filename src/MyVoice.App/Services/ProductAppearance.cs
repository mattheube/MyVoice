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
            "Warm Dark"=>new[]{"#1C1615","#2A2220","#372C28","#514038"},
            _=>new[]{"#101117","#191B25","#242735","#353948"}
        };
        var resources=Application.Current.Resources;
        string[] keys={"CanvasBrush","SurfaceBrush","RaisedBrush","LineBrush"};
        for(int i=0;i<keys.Length;i++)resources[keys[i]]=Brush(palette[i]);
        Color accent;try{accent=(Color)ColorConverter.ConvertFromString(state.Accent);}catch{accent=(Color)ColorConverter.ConvertFromString("#B7A5FF");}
        resources["AccentBrush"]=new SolidColorBrush(accent);
        resources["AccentSoftBrush"]=new SolidColorBrush(Color.FromArgb(32,accent.R,accent.G,accent.B));
        var brightness=.2126*accent.R+.7152*accent.G+.0722*accent.B;
        resources["AccentInkBrush"]=Brush(brightness>145?"#101117":"#FFFFFF");
        resources["AmbientOpacity"]=state.Background=="Minimal"?0d:state.Background=="Rich"?.15d:.07d;
    }
    private static SolidColorBrush Brush(string value)=>new((Color)ColorConverter.ConvertFromString(value));
}

public sealed class BrandMark : FrameworkElement
{
    private static readonly Geometry Mark=Geometry.Parse("M 5,29 L 5,7 15,22 25,7 25,29 M 31,8 L 39,29 47,8");
    protected override void OnRender(DrawingContext dc)
    {
        dc.PushTransform(new ScaleTransform(ActualWidth/52,ActualHeight/36));
        var pen=new Pen((Brush)(TryFindResource("AccentBrush")??Brushes.MediumPurple),3.7){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
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
