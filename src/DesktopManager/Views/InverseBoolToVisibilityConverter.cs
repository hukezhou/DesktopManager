using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DesktopManager.Views;

/// <summary>true 隐藏、false 显示。</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
