using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Box86_WinUI.Services;

namespace Box86_WinUI.Views
{
    public sealed partial class AboutDialog : ContentDialog
    {
        public AboutDialog()
        {
            InitializeComponent();

            var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
            string arch = RuntimeInformation.ProcessArchitecture.ToString().ToUpperInvariant();
            VersionText.Text = $"Version {version.Major}.{version.Minor}.{version.Build} • {arch}";

            // Real, compile-time facts about the loaded 86box_core.dll (see EngineInfoService) - not
            // the user's update-channel/recompiler preference, which only describes a future download.
            bool isRelease = EngineInfoService.IsReleaseBuild;
            ChannelBadgeText.Text = EngineInfoService.ChannelLabel;
            ChannelBadge.Background = Brush(isRelease ? "SystemFillColorSuccessBrush" : "SystemFillColorCautionBrush");
            ChannelBadgeText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);

            EngineText.Text = $"Engine: {EngineInfoService.RecompilerLabel} — core {EngineInfoService.VersionString}";

            var update = CoreUpdateService.Instance;
            if (update.AvailableUpdate != null)
            {
                UpdateStatusText.Text = "Update available";
                UpdateStatusText.Foreground = Brush("SystemFillColorCautionBrush");
            }
            else if (update.LastCheckedTime.HasValue)
            {
                UpdateStatusText.Text = "Latest release (Up to date)";
                UpdateStatusText.Foreground = Brush("SystemFillColorSuccessBrush");
            }
            else
            {
                UpdateStatusText.Text = "Not checked yet";
                UpdateStatusText.Foreground = Brush("TextFillColorSecondaryBrush");
            }
        }

        private static Brush Brush(string resourceKey) =>
            Application.Current.Resources.TryGetValue(resourceKey, out var value) && value is Brush b
                ? b
                : new SolidColorBrush(Microsoft.UI.Colors.Gray);

        private static void OpenUrl(string url) => Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });

        private void OnGitHubClicked(object sender, RoutedEventArgs e) => OpenUrl("https://github.com/86Box/86Box");

        private void OnDocumentationClicked(object sender, RoutedEventArgs e) => OpenUrl("https://86box.readthedocs.io");

        private void OnDiscordClicked(object sender, RoutedEventArgs e) => OpenUrl("https://discord.gg/QXK9XTv");
    }
}
