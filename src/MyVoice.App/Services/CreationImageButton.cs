using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace MyVoice.App.Services;

/// <summary>The visible image shape is also the pointer hit target.</summary>
public sealed class CreationImageButton : Button
{
    public CreationImageButton()=>SizeChanged+=(_,_)=>UpdateClip();
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if(e.Property==TagProperty)UpdateClip();
    }
    private void UpdateClip()
    {
        var bounds=new Rect(0,0,ActualWidth,ActualHeight);
        Clip=Tag as string=="circle"?new EllipseGeometry(bounds):new RectangleGeometry(bounds,12,12);
    }
}
