using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Box86_WinUI.Models
{
    public class VMScreenshot : ObservableObject
    {
        private string _filePath = string.Empty;
        private BitmapImage? _imageSource;

        public string FilePath
        {
            get => _filePath;
            set
            {
                if (SetProperty(ref _filePath, value))
                {
                    OnPropertyChanged(nameof(FileName));
                    _imageSource = null;
                    OnPropertyChanged(nameof(ImageSource));
                }
            }
        }

        public string FileName => Path.GetFileName(FilePath);

        public DateTime Timestamp { get; set; } = DateTime.Now;

        public int Width { get; set; } = 640;

        public int Height { get; set; } = 480;

        public string ColorDepth { get; set; } = "256 Colors";

        public string DisplayMode => $"{Width} \u00D7 {Height} \u2022 {ColorDepth}";

        public string FormattedTimestamp => Timestamp.ToString("MMM d, yyyy h:mm tt");

        public BitmapImage? ImageSource
        {
            get
            {
                if (_imageSource == null && !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath))
                {
                    try
                    {
                        _imageSource = new BitmapImage(new Uri(FilePath));
                    }
                    catch
                    {
                        _imageSource = null;
                    }
                }
                return _imageSource;
            }
        }
    }
}
