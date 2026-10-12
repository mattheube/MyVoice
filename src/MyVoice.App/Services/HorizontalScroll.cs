using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace MyVoice.App.Services;
public static class HorizontalScroll
{
    public static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(HorizontalScroll),new PropertyMetadata(false,Changed));
    public static bool GetEnabled(DependencyObject o)=>(bool)o.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject o,bool value)=>o.SetValue(EnabledProperty,value);
    private static void Changed(DependencyObject o,DependencyPropertyChangedEventArgs e)
    {if(o is ScrollViewer v){v.PreviewMouseWheel-=Wheel;if((bool)e.NewValue)v.PreviewMouseWheel+=Wheel;}}
    private static void Wheel(object sender,MouseWheelEventArgs e)
    {
        var view=(ScrollViewer)sender;
        for(var node=e.OriginalSource as DependencyObject;node!=null&&node!=view;node=Parent(node))
            if(node is ComboBox { IsDropDownOpen:true } || node is System.Windows.Controls.Primitives.Popup)return;
        if(view.ScrollableWidth<=0)return;
        double next=System.Math.Clamp(view.HorizontalOffset-e.Delta,0,view.ScrollableWidth);
        if(next==view.HorizontalOffset)return; // parent can keep scrolling at the edge
        view.ScrollToHorizontalOffset(next);e.Handled=true;
    }
    private static DependencyObject? Parent(DependencyObject o)=>o is Visual or System.Windows.Media.Media3D.Visual3D?VisualTreeHelper.GetParent(o):LogicalTreeHelper.GetParent(o);
    internal static bool HorizontalWheel(int delta)
    {
        for(var node=Mouse.DirectlyOver as DependencyObject;node!=null;node=Parent(node))
        {
            if(node is ComboBox {IsDropDownOpen:true})return false;
            if(node is ScrollViewer view&&GetEnabled(view)&&view.ScrollableWidth>0)
            {view.ScrollToHorizontalOffset(view.HorizontalOffset+delta);return true;}
        }
        return false;
    }
}
