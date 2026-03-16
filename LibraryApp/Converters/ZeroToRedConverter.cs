using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace LibraryApp.Converters;

public class ZeroToRedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int i && i == 0)
            return new SolidColorBrush(Color.Parse("#C0392B"));
        return new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
