using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using Box86_WinUI.ViewModels;

namespace Box86_WinUI.Views
{
    public sealed partial class ScreenshotGalleryView : UserControl
    {
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(
                nameof(ViewModel),
                typeof(VMManagerViewModel),
                typeof(ScreenshotGalleryView),
                new PropertyMetadata(null));

        public VMManagerViewModel? ViewModel
        {
            get => (VMManagerViewModel?)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public ScreenshotGalleryView()
        {
            this.InitializeComponent();
        }

        private async void OnExportClicked(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.SelectedScreenshot == null || !File.Exists(ViewModel.SelectedScreenshot.FilePath)) return;

            try
            {
                var picker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                    SuggestedFileName = ViewModel.SelectedScreenshot.FileName
                };
                picker.FileTypeChoices.Add("PNG Image", new[] { ".png" });

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    File.Copy(ViewModel.SelectedScreenshot.FilePath, file.Path, overwrite: true);
                    ViewModel.StatusMessage = $"Exported screenshot to {file.Name}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error exporting screenshot: {ex.Message}");
            }
        }

        private void OnDeleteClicked(object sender, RoutedEventArgs e)
        {
            ViewModel?.DeleteScreenshot(ViewModel.SelectedScreenshot);
        }
    }
}
