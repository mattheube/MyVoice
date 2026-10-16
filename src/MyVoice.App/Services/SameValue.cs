using System;
using System.Globalization;
using System.Windows.Data;
namespace MyVoice.App.Services;
public sealed class SameValue : IMultiValueConverter
{
    public object Convert(object[] values,Type type,object parameter,CultureInfo culture)=>values.Length==2&&Equals(values[0],values[1]);
    public object[] ConvertBack(object value,Type[] types,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
