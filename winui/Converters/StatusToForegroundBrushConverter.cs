using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Box86_WinUI.Models;

namespace Box86_WinUI.Converters
{
    /// <summary>
    /// Converts a VMStatus (or boolean running state) into the appropriate Fluent foreground brush:
    /// Success brush (SystemFillColorSuccessBrush) when Running,
    /// Caution brush (SystemFillColorCautionBrush) when Paused,
    /// and SecondaryText brush (TextFillColorSecondaryBrush) when Stopped or idle.
    /// </summary>
    public sealed class StatusToForegroundBrushConverter : IValueConverter
    {
        private static Brush Theme(string key, Color fallback)
            => Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush
                ? brush
                : new SolidColorBrush(fallback);

        public object Convert(object? value, Type targetType, object? parameter, string language)
        {
            if (value is VMStatus status)
            {
                return status switch
                {
                    VMStatus.Running => Theme("SystemFillColorSuccessBrush", Color.FromArgb(255, 16, 124, 65)),
                    VMStatus.Paused => Theme("SystemFillColorCautionBrush", Color.FromArgb(255, 255, 185, 0)),
                    _ => Theme("TextFillColorSecondaryBrush", Color.FromArgb(255, 160, 160, 160))
                };
            }

            if (value is bool isRunning && isRunning)
            {
                return Theme("SystemFillColorSuccessBrush", Color.FromArgb(255, 16, 124, 65));
            }

            return Theme("TextFillColorSecondaryBrush", Color.FromArgb(255, 160, 160, 160));
        }

        public object ConvertBack(object value, Type targetType, object? parameter, string language)
            => throw new NotSupportedException();
    }
}
