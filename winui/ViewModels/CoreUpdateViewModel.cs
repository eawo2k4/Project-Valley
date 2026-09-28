using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Box86_WinUI.Services;

namespace Box86_WinUI.ViewModels
{
    public partial class CoreUpdateViewModel : ObservableObject
    {
        private readonly CoreUpdateService _updateService;
        private readonly DispatcherQueue _dispatcherQueue;

        public IReadOnlyList<string> ChannelOptions { get; } = new[] { "Release", "Experimental" };
        public IReadOnlyList<string> RecompilerOptions { get; } = new[] { "NDR", "ODR" };

        [ObservableProperty]
        public partial string SelectedChannel { get; set; }

        [ObservableProperty]
        public partial string SelectedRecompiler { get; set; }

        /// <summary>
        /// NDR (New Dynamic Recompiler) isn't part of the stable Release channel yet, so the
        /// combo greys it out there and falls back to ODR instead of letting Release track a
        /// recompiler build that channel doesn't actually ship.
        /// </summary>
        [ObservableProperty]
        public partial bool IsNdrEnabled { get; set; }

        [ObservableProperty]
        public partial string LastCheckedText { get; set; } = "Never";

        [ObservableProperty]
        public partial bool IsChecking { get; set; }

        [ObservableProperty]
        public partial bool IsDownloading { get; set; }

        [ObservableProperty]
        public partial double DownloadProgress { get; set; }

        [ObservableProperty]
        public partial string DownloadProgressText { get; set; } = "0%";

        [ObservableProperty]
        public partial bool IsUpdateReadyToRestart { get; set; }

        [ObservableProperty]
        public partial string StatusMessage { get; set; } = string.Empty;

        [ObservableProperty]
        public partial CoreManifest? AvailableUpdate { get; set; }

        public bool IsUpdating => IsChecking || IsDownloading;
        public Microsoft.UI.Xaml.Visibility ProgressVisibility => IsDownloading ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        public Microsoft.UI.Xaml.Visibility RestartButtonVisibility => IsUpdateReadyToRestart ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        public Microsoft.UI.Xaml.Visibility UpdateAvailableBadgeVisibility => AvailableUpdate != null && !IsUpdateReadyToRestart ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public CoreUpdateViewModel()
        {
            _updateService = CoreUpdateService.Instance;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread() ?? DispatcherQueue.GetForCurrentThread();

            SelectedChannel = _updateService.Channel.ToString();
            SelectedRecompiler = _updateService.Recompiler.ToString();
            IsNdrEnabled = _updateService.Channel != UpdateChannel.Release;
            if (!IsNdrEnabled && SelectedRecompiler == "NDR")
            {
                SelectedRecompiler = "ODR";
                _updateService.Recompiler = UpdateRecompiler.ODR;
                _updateService.SaveSettings();
            }
            AvailableUpdate = _updateService.AvailableUpdate;
            IsUpdateReadyToRestart = _updateService.IsUpdateReadyToRestart;
            UpdateLastCheckedText();

            _updateService.UpdateAvailable += OnUpdateAvailable;
            _updateService.UpToDate += OnUpToDate;
            _updateService.UpdateCheckFailed += OnUpdateCheckFailed;
            _updateService.DownloadProgressChanged += OnDownloadProgressChanged;
            _updateService.UpdateInstalled += OnUpdateInstalled;
        }

        partial void OnSelectedChannelChanged(string value)
        {
            if (Enum.TryParse<UpdateChannel>(value, true, out var channel))
            {
                _updateService.Channel = channel;
                IsNdrEnabled = channel != UpdateChannel.Release;
                if (!IsNdrEnabled && SelectedRecompiler == "NDR")
                {
                    SelectedRecompiler = "ODR"; // OnSelectedRecompilerChanged persists this
                }
                _updateService.SaveSettings();
            }
        }

        partial void OnSelectedRecompilerChanged(string value)
        {
            if (Enum.TryParse<UpdateRecompiler>(value, true, out var recompiler))
            {
                _updateService.Recompiler = recompiler;
                _updateService.SaveSettings();
            }
        }

        partial void OnIsCheckingChanged(bool value)
        {
            OnPropertyChanged(nameof(IsUpdating));
        }

        partial void OnIsDownloadingChanged(bool value)
        {
            OnPropertyChanged(nameof(IsUpdating));
            OnPropertyChanged(nameof(ProgressVisibility));
        }

        partial void OnIsUpdateReadyToRestartChanged(bool value)
        {
            OnPropertyChanged(nameof(RestartButtonVisibility));
            OnPropertyChanged(nameof(UpdateAvailableBadgeVisibility));
        }

        partial void OnAvailableUpdateChanged(CoreManifest? value)
        {
            OnPropertyChanged(nameof(UpdateAvailableBadgeVisibility));
        }

        private void UpdateLastCheckedText()
        {
            if (_updateService.LastCheckedTime.HasValue)
            {
                var dt = _updateService.LastCheckedTime.Value;
                if (dt.Date == DateTime.Today)
                {
                    LastCheckedText = $"Today at {dt:t}";
                }
                else
                {
                    LastCheckedText = $"{dt:g}";
                }
            }
            else
            {
                LastCheckedText = "Never";
            }
        }

        [RelayCommand]
        public async Task CheckForUpdatesAsync()
        {
            if (IsUpdating) return;

            IsChecking = true;
            StatusMessage = "Checking for updates...";
            try
            {
                var update = await _updateService.CheckForUpdatesAsync(manualCheck: true);
                AvailableUpdate = update;
                UpdateLastCheckedText();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Update check failed: {ex.Message}";
            }
            finally
            {
                IsChecking = false;
            }
        }

        [RelayCommand]
        public async Task DownloadAndApplyUpdateAsync()
        {
            if (AvailableUpdate == null || IsDownloading) return;

            IsDownloading = true;
            DownloadProgress = 0;
            DownloadProgressText = "0%";
            StatusMessage = $"Downloading 86Box {AvailableUpdate.Version}...";

            try
            {
                var progress = new Progress<double>(p =>
                {
                    DownloadProgress = p;
                    DownloadProgressText = $"{p:F0}%";
                });

                bool success = await _updateService.DownloadAndApplyUpdateAsync(AvailableUpdate, progress);
                if (success)
                {
                    IsUpdateReadyToRestart = true;
                    StatusMessage = "Update downloaded. Restart required to apply.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to download update: {ex.Message}";
            }
            finally
            {
                IsDownloading = false;
            }
        }

        [RelayCommand]
        public void RestartToApply()
        {
            try
            {
                // Hands off to an external script that copies the downloaded DLLs in after this
                // process actually exits (they're locked while loaded) and relaunches the app.
                if (_updateService.TryApplyPendingUpdate())
                {
                    Environment.Exit(0);
                    return;
                }

                Microsoft.Windows.AppLifecycle.AppInstance.Restart("");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CoreUpdateViewModel] Restart failed: {ex.Message}");
            }
        }

        private void OnUpdateAvailable(object? sender, UpdateAvailableEventArgs e)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                AvailableUpdate = e.Update;
                UpdateLastCheckedText();
                StatusMessage = $"Version {e.Update.Version} ({e.Update.Channel}, {e.Update.Recompiler}) is available.";
            });
        }

        private void OnUpToDate(object? sender, UpdateUpToDateEventArgs e)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                AvailableUpdate = null;
                UpdateLastCheckedText();
                StatusMessage = "86Box core is up to date.";
            });
        }

        private void OnUpdateCheckFailed(object? sender, UpdateCheckFailedEventArgs e)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                UpdateLastCheckedText();
                StatusMessage = e.Message;
            });
        }

        private void OnDownloadProgressChanged(object? sender, double progress)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                DownloadProgress = progress;
                DownloadProgressText = $"{progress:F0}%";
            });
        }

        private void OnUpdateInstalled(object? sender, CoreManifest manifest)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                IsUpdateReadyToRestart = true;
                StatusMessage = "Update ready to apply. Restart 86Box Manager.";
            });
        }
    }
}
