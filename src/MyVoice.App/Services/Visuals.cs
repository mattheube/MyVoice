using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
namespace MyVoice.App.Services;
public sealed class PageConverter : IValueConverter
{
    public object Convert(object value, Type type, object parameter, CultureInfo culture) => value.ToString() == parameter.ToString(); public object ConvertBack(object value, Type type, object parameter, CultureInfo culture) => Binding.DoNothing;
}
public sealed class SignalHistory : FrameworkElement
{
    public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(nameof(Level), typeof(double), typeof(SignalHistory), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender, OnLevel));
    public double Level
    {
        get => (double)GetValue(LevelProperty); set => SetValue(LevelProperty, value);
    }
    private readonly double[] levels = new double[70];
    public SignalHistory()
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) => { if(!IsVisible)return; Array.Copy(levels, 1, levels, 0, 69); levels[69] = Math.Clamp(Level / 100, 0, 1); InvalidateVisual(); };
        Loaded += (_, _) => timer.Start();
        Unloaded += (_, _) => timer.Stop();
    }
    private static void OnLevel(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double center = ActualHeight / 2;
        var pen = new Pen((Brush)(TryFindResource("AccentBrush")??Brushes.MediumPurple), 2);
        var grid = new Pen(new SolidColorBrush(Color.FromRgb(54, 54, 56)), 1);
        dc.DrawLine(grid, new(0, center), new(ActualWidth, center));
        for (int i = 0; i < 70; i++)
        {
            double x = (i + .5) * ActualWidth / 70, h = Math.Max(1, levels[i] * (center - 4));
            dc.DrawLine(pen, new(x, center - h), new(x, center + h));
        }
    }
}
