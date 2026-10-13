using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace MyVoice.App.Services;
public static class NestedScroll
{
    public static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(NestedScroll),new PropertyMetadata(false,Changed));
    public static bool GetEnabled(DependencyObject value)=>(bool)value.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject value,bool enabled)=>value.SetValue(EnabledProperty,enabled);
    private static void Changed(DependencyObject value,DependencyPropertyChangedEventArgs e)
    {if(value is ScrollViewer scroll){scroll.PreviewMouseWheel-=Wheel;if((bool)e.NewValue)scroll.PreviewMouseWheel+=Wheel;}}
    private static DependencyObject? Parent(DependencyObject value)=>value is Visual?VisualTreeHelper.GetParent(value):LogicalTreeHelper.GetParent(value);
    private static void Wheel(object sender,MouseWheelEventArgs e)
    {
        var scroll=(ScrollViewer)sender;
        // Only the innermost viewport should consume or forward this input.
        for(var source=e.OriginalSource as DependencyObject;source!=null&&source!=scroll;source=Parent(source))if(source is ScrollViewer)return;
        if(Keyboard.Modifiers==ModifierKeys.Shift&&scroll.ScrollableWidth>0){scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset-e.Delta);e.Handled=true;return;}
        if(e.Delta>0&&scroll.VerticalOffset>0||e.Delta<0&&scroll.VerticalOffset<scroll.ScrollableHeight)return;
        for(var parent=Parent(scroll);parent!=null;parent=Parent(parent))
            if(parent is ScrollViewer outer){e.Handled=true;outer.ScrollToVerticalOffset(outer.VerticalOffset-e.Delta);return;}
    }
}
