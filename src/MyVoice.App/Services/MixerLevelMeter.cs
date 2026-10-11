using System;
using System.Windows;
using System.Windows.Media;
namespace MyVoice.App.Services;
public sealed class MixerLevelMeter:FrameworkElement
{
    public static readonly DependencyProperty LevelProperty=DependencyProperty.Register(nameof(Level),typeof(double),typeof(MixerLevelMeter),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender));
    public double Level{get=>(double)GetValue(LevelProperty);set=>SetValue(LevelProperty,value);}
    public static readonly DependencyProperty ClippingProperty=DependencyProperty.Register(nameof(Clipping),typeof(bool),typeof(MixerLevelMeter),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsRender));
    public bool Clipping{get=>(bool)GetValue(ClippingProperty);set=>SetValue(ClippingProperty,value);}
    protected override void OnRender(DrawingContext dc)
    {
        int lit=(int)Math.Ceiling(Math.Clamp(Level,0,100)/100*26);double step=ActualHeight/26;
        var off=(Brush)(TryFindResource("LineBrush")??Brushes.Gray);var on=(Brush)(TryFindResource("AccentBrush")??Brushes.MediumPurple);
        for(int i=0;i<26;i++)dc.DrawRoundedRectangle(i>=lit?off:i>=24&&Clipping?Brushes.Coral:i>=22?Brushes.Goldenrod:on,null,new Rect(0,ActualHeight-(i+1)*step,ActualWidth,Math.Max(1,step-2)),1,1);
    }
}
