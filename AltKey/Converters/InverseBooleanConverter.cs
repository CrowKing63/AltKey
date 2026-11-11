using System;
using System.Globalization;
using System.Windows.Data;

namespace AltKey.Converters;

/// <summary>
/// [역할] 부울 값을 반전(true→false, false→true)해 주는 변환기입니다.
/// AI 백엔드 라디오 버튼에서 "외부 API"가 IsLocalAi=false일 때 체크되도록 쓰입니다.
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : false;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}
