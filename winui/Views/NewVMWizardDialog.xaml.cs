using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Box86_WinUI.Models;
using Box86_WinUI.ViewModels;

namespace Box86_WinUI.Views
{
    public sealed partial class NewVMWizardDialog : ContentDialog
    {
        public NewVMWizardViewModel ViewModel { get; } = new();
        public VirtualMachine? CreatedVM { get; private set; }

        public NewVMWizardDialog()
        {
            this.InitializeComponent();
        }

        private void OnCustomConfigCardClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.IsNewConfiguration = true;
            ViewModel.IsExistingConfiguration = false;
        }

        private void OnImportExistingCardClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.IsExistingConfiguration = true;
            ViewModel.IsNewConfiguration = false;
        }

        private async void OnBrowseExistingConfigClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker
                {
                    SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder
                };
                picker.FileTypeFilter.Add(".cfg");
                picker.FileTypeFilter.Add("*");

                IntPtr hwnd = App.MainWindowHandle;
                if (hwnd != IntPtr.Zero)
                {
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                }

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    ViewModel.ExistingConfigPath = file.Path;
                    if (string.IsNullOrWhiteSpace(ViewModel.SystemName))
                    {
                        var dirName = Path.GetFileName(Path.GetDirectoryName(file.Path));
                        if (!string.IsNullOrEmpty(dirName))
                        {
                            ViewModel.SystemName = dirName;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NewVMWizardDialog] Error browsing config: {ex.Message}");
            }
        }

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(ViewModel.SystemName))
            {
                args.Cancel = true;
                ViewModel.TriggerSystemNameValidation();
                return;
            }

            CreatedVM = ViewModel.FinalizeCreation();
            if (CreatedVM == null)
            {
                args.Cancel = true;
            }
        }

        private void OnCloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            CreatedVM = null;
        }
    }
}
