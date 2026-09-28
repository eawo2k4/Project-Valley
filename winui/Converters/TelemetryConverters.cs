using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Box86_WinUI.Converters
{
    /// <summary>
    /// Converts a boolean drive activity state to the system accent brush (active) or subtle tertiary brush (idle).
    /// </summary>
    public class ActivityToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object parameter, string language)
        {
            bool isActive = value is true;

            if (isActive)
            {
                if (Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out var accent) && accent is Brush accentBrush)
                {
                    return accentBrush;
                }
                return new SolidColorBrush(Color.FromArgb(255, 0, 120, 212));
            }
            else
            {
                if (Application.Current.Resources.TryGetValue("TextFillColorTertiaryBrush", out var tertiary) && tertiary is Brush tertiaryBrush)
                {
                    return tertiaryBrush;
                }
                return new SolidColorBrush(Color.FromArgb(120, 160, 160, 160));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts an emulation speed degradation state or percentage into an appropriate foreground status brush.
    /// </summary>
    public class SpeedToForegroundBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object parameter, string language)
        {
            bool isDegraded = false;
            if (value is bool boolVal)
            {
                isDegraded = boolVal;
            }
            else if (value is double dblVal)
            {
                isDegraded = dblVal > 0 && dblVal < 95.0;
            }

            if (isDegraded)
            {
                if (Application.Current.Resources.TryGetValue("SystemFillColorCautionBrush", out var caution) && caution is Brush cautionBrush)
                {
                    return cautionBrush;
                }
                return new SolidColorBrush(Color.FromArgb(255, 255, 185, 0));
            }
            else
            {
                if (Application.Current.Resources.TryGetValue("SystemFillColorSuccessBrush", out var success) && success is Brush successBrush)
                {
                    return successBrush;
                }
                return new SolidColorBrush(Color.FromArgb(255, 16, 124, 65));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts an emulation speed degradation state or percentage into an appropriate pill background brush.
    /// </summary>
    public class SpeedToBackgroundBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object parameter, string language)
        {
            bool isDegraded = false;
            if (value is bool boolVal)
            {
                isDegraded = boolVal;
            }
            else if (value is double dblVal)
            {
                isDegraded = dblVal > 0 && dblVal < 95.0;
            }

            if (isDegraded)
            {
                return new SolidColorBrush(Color.FromArgb(38, 255, 185, 0)); // 15% opacity caution
            }
            else
            {
                if (Application.Current.Resources.TryGetValue("SubtleFillColorSecondaryBrush", out var subtle) && subtle is Brush subtleBrush)
                {
                    return subtleBrush;
                }
                return new SolidColorBrush(Color.FromArgb(25, 16, 124, 65)); // 10% opacity success
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
