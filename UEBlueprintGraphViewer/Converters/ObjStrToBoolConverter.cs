using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace UEBlueprintGraphViewer.Converters;

internal class ObjStrToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value!.ToString() == parameter?.ToString();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}