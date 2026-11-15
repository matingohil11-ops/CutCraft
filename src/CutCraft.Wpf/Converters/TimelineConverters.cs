using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using CutCraft.Core.Models;

namespace CutCraft.Wpf.Converters;

public class TimeToPixelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeSpan time)
        {
            double scale = 50.0;
            if (parameter != null && double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedScale))
            {
                scale = parsedScale;
            }
            return time.TotalSeconds * scale;
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double pixels)
        {
            double scale = 50.0;
            if (parameter != null && double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedScale))
            {
                scale = parsedScale;
            }
            return TimeSpan.FromSeconds(pixels / scale);
        }
        return TimeSpan.Zero;
    }
}

public class DurationToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeSpan time)
        {
            double scale = 50.0;
            if (parameter != null && double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedScale))
            {
                scale = parsedScale;
            }
            return Math.Max(60.0, time.TotalSeconds * scale);
        }
        return 60.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class AssetTypeToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush VideoBrush = new(Color.FromRgb(0x2D, 0x7D, 0x9A));
    private static readonly SolidColorBrush AudioBrush = new(Color.FromRgb(0x2E, 0x8B, 0x57));
    private static readonly SolidColorBrush ImageBrush = new(Color.FromRgb(0x7B, 0x68, 0xEE));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is AssetType type)
        {
            return type switch
            {
                AssetType.Audio => AudioBrush,
                AssetType.Image => ImageBrush,
                _ => VideoBrush
            };
        }
        return VideoBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class AssetTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is AssetType type)
        {
            return type switch
            {
                AssetType.Audio => "🎵",
                AssetType.Image => "🖼",
                _ => "🎬"
            };
        }
        return "🎬";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
