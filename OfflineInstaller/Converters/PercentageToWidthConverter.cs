using System;
using System.Globalization;
using System.Windows.Data;

namespace OfflineInstaller.Converters;

public class PercentageToWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values != null && values.Length == 3 && values[0] is double percentage && values[2] is double maximumValue)
        {
            return percentage / 100 * maximumValue;
        }

        return 0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
