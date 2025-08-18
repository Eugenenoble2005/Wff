namespace Wff.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

public class StringEqualConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string sval && parameter is string param)
        {
            return sval == param;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StringNotEqualConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string sval && parameter is string param)
        {
            return sval != param;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class RegionContentConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        string region = values[0] is string v ? v : "";
        bool notEqual = values[1] is bool b ? b : false;

        if (notEqual)
        {
            return $"Select Region {region}";
        }
        else
        {
            return "Select Region";
        }
    }

    public object?[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
