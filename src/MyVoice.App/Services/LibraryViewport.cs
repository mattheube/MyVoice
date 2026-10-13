using System;
using System.Globalization;
using System.Windows.Data;
namespace MyVoice.App.Services;
public sealed class LibraryViewport:IMultiValueConverter
{
    public object Convert(object[] values,Type targetType,object parameter,CultureInfo culture)=>Math.Max(210,((values[0] is double h?h:900)-220)/(values[1] is double s?s:1)-210);
    public object[] ConvertBack(object value,Type[] targetTypes,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
