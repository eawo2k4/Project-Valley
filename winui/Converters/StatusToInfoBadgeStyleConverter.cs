using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Box86_WinUI.Models;

namespace Box86_WinUI.Converters
{
    public class StatusToInfoBadgeStyleConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object parameter, string language)
        {
            if (value is VMStatus status)
            {
                string styleKey = status switch
                {
                    VMStatus.Running => "SuccessDotInfoBadgeStyle",
                    VMStatus.Paused => "AttentionIconInfoBadgeStyle",
                    VMStatus.Starting or VMStatus.Stopping => "InformationalDotInfoBadgeStyle",
                    _ => "NeutralDotInfoBadgeStyle"
                };

                if (Application.Current.Resources.TryGetValue(styleKey, out var style))
                {
                    return style;
                }
            }

            if (value is bool isRunning)
            {
                string styleKey = isRunning ? "SuccessDotInfoBadgeStyle" : "NeutralDotInfoBadgeStyle";
                if (Application.Current.Resources.TryGetValue(styleKey, out var style))
                {
                    return style;
                }
            }

            if (Application.Current.Resources.TryGetValue("NeutralDotInfoBadgeStyle", out var defaultStyle))
            {
                return defaultStyle;
            }

            if (Application.Current.Resources.TryGetValue("InformationalDotInfoBadgeStyle", out var fallbackStyle))
            {
                return fallbackStyle;
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
