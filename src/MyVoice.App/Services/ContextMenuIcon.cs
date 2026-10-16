using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
namespace MyVoice.App.Services;
public sealed class ContextMenuIcon : NavIcon
{
    public ContextMenuIcon()
    {
        Width=16;Height=16;IsHitTestVisible=false;SnapsToDevicePixels=true;
        SetBinding(TextElement.ForegroundProperty,new Binding(nameof(MenuItem.Foreground)){RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(MenuItem),1)});
    }
}
