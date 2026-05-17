using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace GraceKeeper.UI.Converters;

public sealed class HealthToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value as string ?? "Healthy";
        var key = status switch
        {
            "Paused" => "Semantic.Warn",
            "Failing" => "Semantic.Bad",
            _ => "Semantic.Good"
        };
        return (SolidColorBrush)Application.Current.FindResource(key);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
