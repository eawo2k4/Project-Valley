using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Box86_WinUI.Converters
{
    /// <summary>
    /// Device icon foreground: neutral gray while idle, and a per-device illumination colour while active.
    /// ConverterParameter selects the device: FDD (amber), CD (green), HDD (amber), NET (accent blue).
    /// </summary>
    public sealed class DeviceActivityBrushConverter : IValueConverter
    {
        private static Brush Theme(string key, Color fallback)
            => Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush
                ? brush
                : new SolidColorBrush(fallback);

        public object Convert(object? value, Type targetType, object? parameter, string language)
        {
            if (value is not true)
                return Theme("TextFillColorSecondaryBrush", Color.FromArgb(255, 160, 160, 160));

            return (parameter as string)?.ToUpperInvariant() switch
            {
                "FDD" => Theme("SystemFillColorCautionBrush", Color.FromArgb(255, 255, 185, 0)),
                "CD" => Theme("AccentFillColorDefaultBrush", Color.FromArgb(255, 0, 120, 212)),
                "HDD" => Theme("SystemFillColorCautionBrush", Color.FromArgb(255, 255, 140, 0)),
                "NET" => Theme("AccentFillColorDefaultBrush", Color.FromArgb(255, 0, 120, 212)),
                "SND" => Theme("AccentFillColorDefaultBrush", Color.FromArgb(255, 0, 120, 212)),
                _ => Theme("AccentFillColorDefaultBrush", Color.FromArgb(255, 0, 120, 212))
            };
        }

        public object ConvertBack(object value, Type targetType, object? parameter, string language)
            => throw new NotSupportedException();
    }
}
