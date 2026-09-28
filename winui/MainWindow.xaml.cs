using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Box86_WinUI.Models;
using Box86_WinUI.Services;
using Box86_WinUI.Views;

namespace Box86_WinUI;

/// <summary>
/// The application window. This hosts a Frame that displays pages.
/// </summary>
public sealed partial class MainWindow : Window
{
    private DispatcherTimer? _autoDismissTimer;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        WindowIconHelper.ApplyAppIcon(this);

        HookUpdateEvents();

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));

        // Loaded (not Activated) so Content.XamlRoot is guaranteed to exist before ShowAsync runs.
        RootFrame.Loaded += OnRootFrameLoadedShowWelcomeTour;

        // Silent background poll: OnUpToDate/OnUpdateCheckFailed below only surface an InfoBar for
        // a manual "Check for updates" click, so this never bothers the user unless a real update
        // is actually found (currently never, while GitHubRepo is an unset placeholder).
        _ = CoreUpdateService.Instance.CheckForUpdatesAsync(manualCheck: false);
    }

    private static readonly string ManagerConfigPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "86Box", "86box.manager.ini");

    private async void OnRootFrameLoadedShowWelcomeTour(object sender, RoutedEventArgs args)
    {
        RootFrame.Loaded -= OnRootFrameLoadedShowWelcomeTour;

        try
        {
            var config = System.IO.File.Exists(ManagerConfigPath) ? MachineConfig.Load(ManagerConfigPath) : new MachineConfig();
            if (config.GetBool("UI", "has_seen_welcome_tour", false))
                return;

            var dialog = new WelcomeDialog { XamlRoot = Content.XamlRoot };
            await dialog.ShowAsync();

            config.SetBool("UI", "has_seen_welcome_tour", true);
            config.Save(ManagerConfigPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Welcome tour failed: {ex.Message}");
        }
    }

    private void HookUpdateEvents()
    {
        var updater = CoreUpdateService.Instance;
        updater.UpdateAvailable += OnUpdateAvailable;
        updater.UpToDate += OnUpToDate;
        updater.UpdateInstalled += OnUpdateInstalled;
        updater.UpdateCheckFailed += OnUpdateCheckFailed;
    }

    private void OnUpdateAvailable(object? sender, UpdateAvailableEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _autoDismissTimer?.Stop();

            UpdateNotificationInfoBar.Severity = InfoBarSeverity.Informational;
            UpdateNotificationInfoBar.Title = "Core Update Available";
            UpdateNotificationInfoBar.Message = $"86Box {e.Update.Version} ({e.Update.Channel}, {e.Update.Recompiler}) is ready to install.";

            var updateButton = new Button
            {
                Content = "Update now",
                Style = Application.Current.Resources.TryGetValue("AccentButtonStyle", out var s) && s is Style style ? style : null
            };

            updateButton.Click += async (s, args) =>
            {
                updateButton.IsEnabled = false;
                UpdateNotificationInfoBar.Severity = InfoBarSeverity.Informational;
                UpdateNotificationInfoBar.Title = "Downloading Core Update";
                UpdateNotificationInfoBar.Message = $"Downloading 86Box {e.Update.Version}...";
                UpdateNotificationInfoBar.ActionButton = null;

                try
                {
                    var progress = new Progress<double>(p =>
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            UpdateNotificationInfoBar.Message = $"Downloading 86Box {e.Update.Version}... {p:F0}%";
                        });
                    });

                    bool ok = await CoreUpdateService.Instance.DownloadAndApplyUpdateAsync(e.Update, progress);
                    if (ok)
                    {
                        ShowRestartPrompt(e.Update);
                    }
                }
                catch (Exception ex)
                {
                    UpdateNotificationInfoBar.Severity = InfoBarSeverity.Error;
                    UpdateNotificationInfoBar.Title = "Update Failed";
                    UpdateNotificationInfoBar.Message = ex.Message;
                    UpdateNotificationInfoBar.ActionButton = null;
                }
            };

            UpdateNotificationInfoBar.ActionButton = updateButton;
            UpdateNotificationInfoBar.IsOpen = true;
        });
    }

    private void OnUpToDate(object? sender, UpdateUpToDateEventArgs e)
    {
        if (!e.IsManualCheck) return; // silent poll found nothing new - stay quiet

        DispatcherQueue.TryEnqueue(() =>
        {
            _autoDismissTimer?.Stop();

            UpdateNotificationInfoBar.Severity = InfoBarSeverity.Success;
            UpdateNotificationInfoBar.Title = "Up to date";
            UpdateNotificationInfoBar.Message = "86Box core is up to date.";
            UpdateNotificationInfoBar.ActionButton = null;
            UpdateNotificationInfoBar.IsOpen = true;

            // Auto-dismiss transient Success InfoBar after 4 seconds
            _autoDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _autoDismissTimer.Tick += (s, args) =>
            {
                _autoDismissTimer.Stop();
                UpdateNotificationInfoBar.IsOpen = false;
            };
            _autoDismissTimer.Start();
        });
    }

    private void OnUpdateInstalled(object? sender, CoreManifest manifest)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ShowRestartPrompt(manifest);
        });
    }

    private void ShowRestartPrompt(CoreManifest manifest)
    {
        _autoDismissTimer?.Stop();

        UpdateNotificationInfoBar.Severity = InfoBarSeverity.Success;
        UpdateNotificationInfoBar.Title = "Update Ready to Apply";
        UpdateNotificationInfoBar.Message = $"86Box core build {manifest.BuildNumber} has been installed. Restart to apply.";

        var restartButton = new Button
        {
            Content = "Restart to apply",
            Style = Application.Current.Resources.TryGetValue("AccentButtonStyle", out var s) && s is Style style ? style : null
        };

        restartButton.Click += (s, args) =>
        {
            try
            {
                // Hands off to an external script that copies the downloaded DLLs in after this
                // process actually exits (they're locked while loaded) and relaunches the app.
                if (CoreUpdateService.Instance.TryApplyPendingUpdate())
                {
                    Environment.Exit(0);
                    return;
                }

                Microsoft.Windows.AppLifecycle.AppInstance.Restart("");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Restart failed: {ex.Message}");
            }
        };

        UpdateNotificationInfoBar.ActionButton = restartButton;
        UpdateNotificationInfoBar.IsOpen = true;
    }

    private void OnUpdateCheckFailed(object? sender, UpdateCheckFailedEventArgs e)
    {
        if (!e.IsManualCheck) return; // silent poll failing (e.g. no release repo configured yet) shouldn't nag on every launch

        DispatcherQueue.TryEnqueue(() =>
        {
            _autoDismissTimer?.Stop();

            UpdateNotificationInfoBar.Severity = InfoBarSeverity.Warning;
            UpdateNotificationInfoBar.Title = "Update Check Failed";
            UpdateNotificationInfoBar.Message = e.Message;
            UpdateNotificationInfoBar.ActionButton = null;
            UpdateNotificationInfoBar.IsOpen = true;

            _autoDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            _autoDismissTimer.Tick += (s, args) =>
            {
                _autoDismissTimer.Stop();
                UpdateNotificationInfoBar.IsOpen = false;
            };
            _autoDismissTimer.Start();
        });
    }
}
