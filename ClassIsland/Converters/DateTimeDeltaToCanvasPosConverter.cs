﻿using System;
using System.Globalization;
using System.Windows.Data;

namespace ClassIsland.Converters;

public class DateTimeDeltaToCanvasPosConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var v1 = (DateTime)values[0];
        var v2 = (DateTime)values[1];
        var s = values[2] as double? ?? 1.0;
        return Math.Max((v2 - v1).Ticks / 1000000000.0 * s, 0.0);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        return new object[] { };
    }
}