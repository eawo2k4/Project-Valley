using System;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using CommunityToolkit.Mvvm.Input;
using Box86_WinUI.ViewModels;
using Box86_WinUI.Services;

namespace Box86_WinUI.Views
{
    public sealed partial class ManagerSettingsView : UserControl
    {
        public CoreUpdateViewModel UpdateViewModel { get; } = new();

        public ICommand RestartWelcomeTourCommand { get; }

        /// <summary>
        /// This control is always hosted inside ManagerSettingsDialog, itself a ContentDialog - and
        /// only one ContentDialog can be open at a time, so the tour can't just ShowAsync() a second
        /// one from in here. The host dialog handles closing itself first; see ManagerSettingsDialog.
        /// </summary>
        public event EventHandler? RestartTourRequested;

        public ManagerSettingsView()
        {
            // Assigned before InitializeComponent(): the XAML's x:Bind to this property is OneTime
            // by default, evaluated during InitializeComponent() itself - assigning it afterward
            // meant the button's Command was permanently bound to null and clicking did nothing.
            RestartWelcomeTourCommand = new RelayCommand(() => RestartTourRequested?.Invoke(this, EventArgs.Empty));
            this.InitializeComponent();

            bool isRelease = EngineInfoService.IsReleaseBuild;
            RunningChannelBadgeText.Text = EngineInfoService.ChannelLabel;
            RunningChannelBadge.Background = Brush(isRelease ? "SystemFillColorSuccessBrush" : "SystemFillColorCautionBrush");
            RunningChannelBadgeText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
            RunningVersionText.Text = EngineInfoService.VersionString;
            RunningEngineText.Text = $"Engine: {EngineInfoService.RecompilerLabel}";

            var update = CoreUpdateService.Instance;
            if (update.AvailableUpdate != null)
            {
                RunningUpdateStatusText.Text = "Update available";
                RunningUpdateStatusText.Foreground = Brush("SystemFillColorCautionBrush");
            }
            else if (update.LastCheckedTime.HasValue)
            {
                RunningUpdateStatusText.Text = "Latest release (Up to date)";
                RunningUpdateStatusText.Foreground = Brush("SystemFillColorSuccessBrush");
            }
            else
            {
                RunningUpdateStatusText.Text = "Not checked yet";
                RunningUpdateStatusText.Foreground = Brush("TextFillColorSecondaryBrush");
            }
        }

        private static Brush Brush(string resourceKey) =>
            Application.Current.Resources.TryGetValue(resourceKey, out var value) && value is Brush b
                ? b
                : new SolidColorBrush(Microsoft.UI.Colors.Gray);
    }
}
