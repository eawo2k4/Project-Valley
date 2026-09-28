using System;
using Microsoft.UI.Xaml.Controls;
using Box86_WinUI.ViewModels;
using Box86_WinUI.Services;

namespace Box86_WinUI.Views
{
    public sealed partial class ManagerSettingsDialog : ContentDialog
    {
        public VMManagerViewModel ViewModel { get; }
        public string DefaultVmDirectory { get; }

        public ManagerSettingsDialog(VMManagerViewModel viewModel)
        {
            ViewModel = viewModel;
            var configService = new ConfigService();
            DefaultVmDirectory = configService.GetDefaultVmDirectory();

            this.InitializeComponent();
        }

        // Only one ContentDialog can be open at a time, so this dialog must fully close before the
        // tour (also a ContentDialog) can open - showing it while still open threw
        // "Only a single ContentDialog can be open at any time" and crashed the app.
        private async void OnRestartTourRequested(object? sender, EventArgs e)
        {
            Hide();
            var dialog = new WelcomeDialog { XamlRoot = App.MainWindowInstance?.Content?.XamlRoot ?? XamlRoot };
            await dialog.ShowAsync();
        }
    }
}
