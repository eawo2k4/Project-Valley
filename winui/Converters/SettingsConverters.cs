using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Box86_WinUI.Converters
{
    public class CategoryToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var category = value as string ?? string.Empty;
            return category switch
            {
                "Machine" => "ms-appx:///Assets/Icons/machine.ico",
                "Display" => "ms-appx:///Assets/Icons/display.ico",
                "Input devices" => "ms-appx:///Assets/Icons/input_devices.ico",
                "Sound" => "ms-appx:///Assets/Icons/sound.ico",
                "Network" => "ms-appx:///Assets/Icons/network.ico",
                "Ports (COM & LPT)" => "ms-appx:///Assets/Icons/ports.ico",
                "Storage controllers" => "ms-appx:///Assets/Icons/storage_controllers.ico",
                "Hard disks" => "ms-appx:///Assets/Icons/hard_disk.ico",
                "Floppy & CD-ROM drives" => "ms-appx:///Assets/Icons/floppy_and_cdrom_drives.ico",
                "Other removable devices" => "ms-appx:///Assets/Icons/other_removable_devices.ico",
                "Other peripherals" => "ms-appx:///Assets/Icons/other_peripherals.ico",
                _ => "ms-appx:///Assets/Icons/machine.ico"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToTabBgConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isSelected = value is bool b && b;
            if (isSelected)
            {
                if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out var brush))
                    return (Brush)brush;
            }
            return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToTabBorderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isSelected = value is bool b && b;
            if (isSelected)
            {
                if (Application.Current.Resources.TryGetValue("CardStrokeColorDefaultBrush", out var brush))
                    return (Brush)brush;
            }
            return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class NotNullToBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object parameter, string language)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class InvertedBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is bool b && !b;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is bool b && !b;
        }
    }
}
