using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AltKey.Converters;

/// <summary>bool 값을 Visibility로 변환하되, Inverse 파라미터를 지원</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            var useInverse = parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true;
            var finalValue = useInverse ? !boolValue : boolValue;
            return finalValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            var isVisible = visibility == Visibility.Visible;
            var useInverse = parameter?.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true;
            return useInverse ? !isVisible : isVisible;
        }
        return false;
    }
}
