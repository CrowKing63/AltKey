using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AltKey.Themes;

/// T-5.2: 체류 진행도(0.0~1.0) → StrokeDashOffset 변환
/// StrokeDashArray="100" 기준 → progress=0 시 offset=100(비어있음), progress=1 시 offset=0(가득 참)
[ValueConversion(typeof(double), typeof(double))]
public class ProgressToOffsetConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double progress = value is double d ? d : 0.0;
        return 100.0 * (1.0 - Math.Clamp(progress, 0.0, 1.0));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// bool → Visibility 변환 (App.xaml에 이미 있지만 테마 파일에서도 사용 가능하도록 제공)
[ValueConversion(typeof(bool), typeof(Visibility))]
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

/// T-9.4: bool 반전 후 Visibility 변환 (false → Visible)
[ValueConversion(typeof(bool), typeof(Visibility))]
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Collapsed;
}

/// T-9.4: EditWidth(double) → 픽셀 폭(double) 변환. 기준 단위 = 50px
[ValueConversion(typeof(double), typeof(double))]
public class WidthToPixelConverter : IValueConverter
{
    private const double Unit = 50.0;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double w ? Math.Max(Unit * w, 30.0) : 50.0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// Gap 값(double) → 우측 여백 Thickness(0,0,gap*Unit,0) 변환
[ValueConversion(typeof(double), typeof(Thickness))]
public class GapToRightMarginConverter : IValueConverter
{
    private const double Unit = 50.0;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => new Thickness(0, 0, (value is double g ? g : 0.0) * Unit, 0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// null 또는 빈 문자열 → Collapsed, 값 있으면 Visible
[ValueConversion(typeof(object), typeof(Visibility))]
public class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrEmpty(s) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// bool → 별표(★/☆) 문자 변환 (즐겨찾기 표시용)
[ValueConversion(typeof(bool), typeof(string))]
public class BoolToStarConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "★" : "☆";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && s == "★";
}
