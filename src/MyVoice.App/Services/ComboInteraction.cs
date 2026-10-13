using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace MyVoice.App.Services;
public static class ComboInteraction
{
    public static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(ComboInteraction),new PropertyMetadata(false,Changed));
    public static bool GetEnabled(DependencyObject value)=>(bool)value.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject value,bool enabled)=>value.SetValue(EnabledProperty,enabled);
    private static void Changed(DependencyObject value,DependencyPropertyChangedEventArgs e)
    {
        if(value is not ComboBox combo)return;
        combo.PreviewMouseWheel-=Wheel;combo.Unloaded-=Unloaded;
        if((bool)e.NewValue){combo.PreviewMouseWheel+=Wheel;combo.Unloaded+=Unloaded;}
    }
    private static void Unloaded(object sender,RoutedEventArgs e)=>((ComboBox)sender).SetCurrentValue(ComboBox.IsDropDownOpenProperty,false);
    private static void Wheel(object sender,MouseWheelEventArgs e)
    {
        if(((ComboBox)sender).IsDropDownOpen)return;
        // Scrolling past a closed audio selector must not switch devices or reset its backend.
        e.Handled=true;
        for(DependencyObject? parent=VisualTreeHelper.GetParent((ComboBox)sender);parent!=null;parent=VisualTreeHelper.GetParent(parent))
            if(parent is ScrollViewer scroll){scroll.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice,e.Timestamp,e.Delta){RoutedEvent=UIElement.MouseWheelEvent,Source=scroll});break;}
    }
}
