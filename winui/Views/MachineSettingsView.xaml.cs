using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Box86_WinUI.ViewModels;

namespace Box86_WinUI.Views
{
    public sealed partial class MachineSettingsView : UserControl
    {
        public SettingsViewModel? ViewModel => DataContext as SettingsViewModel;

        public MachineSettingsView()
        {
            this.InitializeComponent();
        }

        private async void OnOpenConfigFileClicked(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                await ViewModel.OpenConfigFileAsync();
            }
        }
    }
}
