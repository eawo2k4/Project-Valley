using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Windowing;
using Windows.Storage.Pickers;
using Windows.UI;
using Box86_WinUI.Models;
using Box86_WinUI.Services;
using Box86_WinUI.ViewModels;

namespace Box86_WinUI.Views
{
    public sealed partial class EmulatorWindow : Window
    {
        public VirtualMachine VM { get; }
        public EmulatorViewModel ViewModel { get; }

        private readonly DisplayRendererService _rendererService = new();
        private readonly VMProcessSupervisor _supervisor;
        private readonly DispatcherTimer _ledTimer;
        private readonly Random _random = new();

        private bool _isPaused = false;
        private bool _isFullscreen = false;

        // Hardware activity icons: each FontIcon is the indicator itself. Idle is the dim
        // neutral text brush; while I/O is active it transitions (150ms ColorAnimation on the
        // icon's own brush) to the device's accent brush, then decays back via the LED timer.

        private readonly EmuEngineService _engineService;

        public EmulatorWindow(VirtualMachine vm, VMProcessSupervisor? supervisor = null)
        {
            this.InitializeComponent();
            VM = vm;
            _supervisor = supervisor ?? new VMProcessSupervisor();
            ViewModel = new EmulatorViewModel(vm, _supervisor);

            TitleTextBlock.Text = vm.Name;
            this.Title = vm.Name;
            ViewModel.Telemetry.ChannelChanged += OnDeviceChannelChanged;
            DriveSoundService.SoundPlayed += () => DispatcherQueue.TryEnqueue(() => ViewModel.Telemetry.SignalActivity(DeviceChannel.Snd));
            ViewModel.AcpiShutdownAction = () => _engineService?.AcpiShutdown();

            // Extends into TitleBar for a sleek modern look
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            WindowIconHelper.ApplyAppIcon(this);

            // Synchronize position and sizing of native owned window with WinUI window
            this.AppWindow.Changed += (s, args) =>
            {
                if (args.DidPositionChange || args.DidSizeChange || args.DidVisibilityChange)
                {
                    DispatcherQueue.TryEnqueue(UpdateViewportLayout);
                }
            };

            this.Activated += (s, e) =>
            {
                if (e.WindowActivationState == WindowActivationState.Deactivated)
                {
                    ReleaseMouse();
                    ReleaseAllKeys();
                }
                if (e.WindowActivationState != WindowActivationState.Deactivated)
                {
                    DispatcherQueue.TryEnqueue(UpdateViewportLayout);
                }
            };

            // Listen for window HWND and status updates
            VM.PropertyChanged += OnVmPropertyChanged;

            // Live hardware LED and performance telemetry timer
            _ledTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _ledTimer.Tick += OnLedTimerTick;
            _ledTimer.Start();
            UpdateMediaState();

            Debug.WriteLine($"[EmulatorWindow] ctor: creating EmuEngineService for VM '{vm.Name}'");
            _engineService = new EmuEngineService(DispatcherQueue);
            _engineService.TelemetryUpdated += OnTelemetryUpdated;
            _engineService.MessageReceived += OnEngineMessageReceived;
            _supervisor.RegisterEngine(VM, _engineService);

            this.Closed += OnWindowClosed;
            RestoreSystemCursors(); // clears a blank cursor left behind by an earlier crash
            AppDomain.CurrentDomain.ProcessExit += (_, _) => RestoreSystemCursors();
            VM.ConfigSaveFailed += msg => DispatcherQueue.TryEnqueue(() =>
            {
                ViewportInfoBar.Title = "Configuration not saved";
                ViewportInfoBar.Message = msg;
                ViewportInfoBar.Severity = InfoBarSeverity.Error;
                ViewportInfoBar.IsOpen = true;
            });

            if (!_engineService.IsNativeAvailable)
            {
                Debug.WriteLine("[EmulatorWindow] ctor: native engine unavailable, VM will not auto-start");
                ViewportInfoBar.Title = "Emulation engine unavailable";
                ViewportInfoBar.Message = "86box_core.dll could not be loaded. Check that it is built and deployed next to the application.";
                ViewportInfoBar.Severity = InfoBarSeverity.Error;
                ViewportInfoBar.IsOpen = true;
            }
            else
            {
                // Auto-start in-process VM engine directly via 86box_core.dll
                _ = StartInProcessVmAsync();
            }
        }

        #region Direct3D 11 Viewport Hosting & Sizing

        private void OnSwapChainPanelLoaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[EmulatorWindow] OnSwapChainPanelLoaded: begin");
            try
            {
                // Real native WinRT object pointer (GetIUnknownForObject would only wrap the managed projection).
                var unknown = WinRT.MarshalInspectable<object>.FromManaged(DisplaySwapChainPanel);
                try
                {
                    int width = (int)Math.Max(640, ViewportContainer.ActualWidth);
                    int height = (int)Math.Max(480, ViewportContainer.ActualHeight);

                    _rendererService.InitializeRenderer(unknown, width, height);
                }
                finally
                {
                    Marshal.Release(unknown);
                }

                if (_rendererService.SwapChainHandle == IntPtr.Zero)
                {
                    Debug.WriteLine("[EmulatorWindow] OnSwapChainPanelLoaded: renderer did not produce a swap chain handle, skipping native bind");
                }
                else
                {
                    _engineService.SetDxgiSwapChain(_rendererService.SwapChainHandle);
                }

                UpdateViewportLayout();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EmulatorWindow] OnSwapChainPanelLoaded: FAILED - {ex}");
            }
            Debug.WriteLine("[EmulatorWindow] OnSwapChainPanelLoaded: end");
        }

        private async Task StartInProcessVmAsync()
        {
            Debug.WriteLine($"[EmulatorWindow] StartInProcessVmAsync: begin (path='{VM.Path}')");
            try
            {
                // Direct native library invocation: bind SwapChain and launch emulation thread
                int res = await _engineService.StartEmulationAsync(VM.Path, _rendererService.SwapChainHandle);
                if (res == 0)
                {
                    ViewportStandbyOverlay.Visibility = Visibility.Collapsed;
                    DisplaySwapChainPanel.Visibility = Visibility.Visible;

                    // Started, but the engine may still have complained about a device or ROM: surface it.
                    var warnings = EngineLogDiagnostics.Scan(VM.Path);
                    if (warnings.Count > 0)
                    {
                        ViewportInfoBar.Title = "Engine reported problems";
                        ViewportInfoBar.Message = string.Join(Environment.NewLine, warnings);
                        ViewportInfoBar.Severity = InfoBarSeverity.Warning;
                        ViewportInfoBar.IsOpen = true;
                    }
                }
                else
                {
                    _supervisor.UnregisterEngine(VM);
                    var problems = EngineLogDiagnostics.Scan(VM.Path);
                    ViewportInfoBar.Title = "Failed to start emulation";
                    ViewportInfoBar.Message = EngineLogDiagnostics.DescribeStartError(res)
                        + (problems.Count > 0 ? Environment.NewLine + string.Join(Environment.NewLine, problems) : string.Empty);
                    ViewportInfoBar.Severity = InfoBarSeverity.Error;
                    ViewportInfoBar.IsOpen = true;
                }
                Debug.WriteLine($"[EmulatorWindow] StartInProcessVmAsync: end (result={res})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EmulatorWindow] StartInProcessVmAsync: FAILED - {ex}");
                _supervisor.UnregisterEngine(VM);
                ViewportInfoBar.Title = "Failed to start emulation";
                ViewportInfoBar.Message = ex.Message;
                ViewportInfoBar.Severity = InfoBarSeverity.Error;
                ViewportInfoBar.IsOpen = true;
            }
        }

        private void OnEngineMessageReceived(object? sender, EmuMessageEventArgs e)
        {
            ViewportInfoBar.Title = string.IsNullOrWhiteSpace(e.Header) ? "Notification" : e.Header;
            ViewportInfoBar.Message = e.Message;
            ViewportInfoBar.Severity = (e.Level & 0x40) != 0 ? InfoBarSeverity.Error :
                                      (e.Level & 0x02) != 0 ? InfoBarSeverity.Error :
                                      (e.Level & 0x20) != 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Informational;
            ViewportInfoBar.IsOpen = true;
        }

        private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateViewportLayout();
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(VirtualMachine.MountedFloppyPath) or nameof(VirtualMachine.MountedCdromPath)
                or nameof(VirtualMachine.IsFloppyMounted) or nameof(VirtualMachine.IsCdromMounted))
            {
                DispatcherQueue.TryEnqueue(UpdateMediaState);
            }

            if (e.PropertyName is nameof(VirtualMachine.Status))
            {
                if (VM.Status != VMStatus.Running) ViewModel.Telemetry.Reset();
                DispatcherQueue.TryEnqueue(ViewModel.Status.Refresh);
                DispatcherQueue.TryEnqueue(ViewModel.RefreshMachineCapabilities);
                DispatcherQueue.TryEnqueue(UpdateViewportLayout);
            }
        }

        private void UpdateViewportLayout()
        {
            if (ViewportContainer == null) return;

            int width = (int)Math.Max(640, ViewportContainer.ActualWidth);
            int height = (int)Math.Max(480, ViewportContainer.ActualHeight);

            ViewModel.ViewportWidth = width;
            ViewModel.ViewportHeight = height;

            if (_rendererService.IsInitialized)
            {
                _rendererService.ResizeBuffers(width, height);
            }

            _engineService.ResizeViewport(width, height);
        }

        private void OnTelemetryUpdated(object? sender, Box86_WinUI.Interop.EmuTelemetryData data)
        {
            SpeedValueText.Text = $"{data.SpeedPercentage:F1}% ({data.Fps} FPS)";
            UpdateMouseCaptureBadge();
            var tel = ViewModel.Telemetry;
            if (data.FddActive != 0) { tel.SignalActivity(DeviceChannel.Fdd); DriveSoundService.Seek(DriveSoundKind.Floppy); }
            if (data.HddActive != 0) { tel.SignalActivity(DeviceChannel.Hdd); DriveSoundService.Seek(DriveSoundKind.HardDisk); }
            if (data.CdActive != 0) tel.SignalActivity(DeviceChannel.Cd);
            if (data.NetActive != 0) tel.SignalActivity(DeviceChannel.Net);
            _isPaused = data.IsPaused != 0;
            SyncPauseUi();
        }

        #endregion

        #region Removable Media Quick-Mount Flyouts

        // --- Floppy Drive (FDD) Flyout: one submenu per configured drive ---
        private const string DriveSection = "Floppy and CD-ROM drives";

        private void OnFloppyFlyoutOpening(object? sender, object e)
        {
            VM.LoadExtraMedia();
            FloppyMenuFlyout.Items.Clear();

            var cfg = MachineConfig.Load(VM.ConfigFilePath);
            int shown = 0;
            for (int i = 0; i < 4; i++)
            {
                string type = Cfg86Reader.FloppyLabel(cfg.GetString(DriveSection, $"fdd_{i + 1:D2}_type", ""), i);
                if (type == "None") continue;
                shown++;

                int drive = i;
                string? mounted = VM.GetFloppyPath(drive);
                var sub = new MenuFlyoutSubItem
                {
                    Text = $"Floppy {drive + 1} ({type}): {(mounted == null ? "Empty" : Path.GetFileName(mounted))}",
                    Icon = new FontIcon { Glyph = "\uE74E" }
                };

                var insert = new MenuFlyoutItem { Text = "Mount Image…", Icon = new FontIcon { Glyph = "\uED25" } };
                insert.Click += async (s, a) => await InsertFloppyAsync(drive);
                sub.Items.Add(insert);

                var eject = new MenuFlyoutItem { Text = "Eject", Icon = new FontIcon { Glyph = "\uF847" }, IsEnabled = mounted != null };
                eject.Click += (s, a) => EjectFloppyDrive(drive);
                sub.Items.Add(eject);

                var history = new MenuFlyoutSubItem { Text = "Recent Images", Icon = new FontIcon { Glyph = "\uE81C" } };
                PopulateRecentSubItem(history, VM.RecentFloppyImages, path => MountFloppyImage(drive, path), VM.ClearFloppyHistory);
                sub.Items.Add(history);

                FloppyMenuFlyout.Items.Add(sub);
            }

            if (shown == 0)
                FloppyMenuFlyout.Items.Add(new MenuFlyoutItem { Text = "No floppy drives configured", IsEnabled = false });
        }

        private void MountFloppyImage(int drive, string path)
        {
            _engineService.MountFloppy(drive, path);
            VM.SetFloppyPath(drive, path);
        }

        private void EjectFloppyDrive(int drive)
        {
            _engineService.EjectFloppy(drive);
            VM.SetFloppyPath(drive, null);
            ViewModel.Telemetry.Reset(DeviceChannel.Fdd);
        }

        private async Task InsertFloppyAsync(int drive)
        {
            var picker = new FileOpenPicker();
            foreach (var ext in new[] { ".img", ".ima", ".86f", ".dsk", ".flp", ".bin" })
                picker.FileTypeFilter.Add(ext);
            picker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));

            var file = await picker.PickSingleFileAsync();
            if (file != null) MountFloppyImage(drive, file.Path);
        }

        // CommandBar "Media" flyout entries operate on drive 1.
        private async void OnInsertFloppyClicked(object sender, RoutedEventArgs e) => await InsertFloppyAsync(0);

        private void OnToggleFloppyWriteProtectClicked(object sender, RoutedEventArgs e)
        {
            VM.ToggleFloppyWriteProtect();
            if (VM.MountedFloppyPath is { } p)
                _engineService.MountFloppy(0, p, VM.IsFloppyWriteProtected);
        }

        private void OnEjectFloppyClicked(object sender, RoutedEventArgs e) => EjectFloppyDrive(0);

        // --- Network flyout: one Connect / Disconnect entry per configured card ---
        private void OnNetFlyoutOpening(object? sender, object e)
        {
            ViewModel.Status.Refresh();
            ViewModel.Status.SyncLinkState(slot => _engineService.NetworkIsConnected(slot));
            NetMenuFlyout.Items.Clear();

            if (ViewModel.Status.NetCards.Count == 0)
            {
                NetMenuFlyout.Items.Add(new MenuFlyoutItem { Text = "No network adapter configured", IsEnabled = false });
                return;
            }

            foreach (var card in ViewModel.Status.NetCards)
            {
                int slot = card.Slot;
                bool up = card.LinkUp;
                var item = new MenuFlyoutItem
                {
                    Text = $"{(up ? "Disconnect" : "Connect")} {card.Name}",
                    Icon = new FontIcon { Glyph = up ? "\uF384" : "\uE839" },
                    IsEnabled = card.Mode.Length > 0 // the null driver has no host network to connect to
                };
                item.Click += (s, a) => SetNetworkLink(slot, !up);
                NetMenuFlyout.Items.Add(item);
            }
        }

        private void SetNetworkLink(int slot, bool up)
        {
            try
            {
                _engineService.NetworkConnect(slot, up);   // the running machine
                ViewModel.Status.SaveLink(slot, up);        // 86box.cfg, so the next start agrees
                ViewModel.Status.SetLink(slot, up);         // this indicator
            }
            catch (Exception ex)
            {
                ViewportInfoBar.Title = "Network link not changed";
                ViewportInfoBar.Message = ex.Message;
                ViewportInfoBar.Severity = InfoBarSeverity.Error;
                ViewportInfoBar.IsOpen = true;
            }
        }

        // --- CD-ROM Drive (CD) Flyout: one submenu per configured drive ---
        private void OnCdromFlyoutOpening(object? sender, object e)
        {
            VM.LoadExtraMedia();
            CdromMenuFlyout.Items.Clear();

            var cfg = MachineConfig.Load(VM.ConfigFilePath);
            int shown = 0;
            for (int i = 0; i < 8; i++)
            {
                string parameters = cfg.GetString(DriveSection, $"cdrom_{i + 1:D2}_parameters", "");
                string bus = parameters.Contains(',') ? parameters.Split(',')[1].Trim().ToLowerInvariant() : "none";
                if (bus == "none") continue;
                shown++;

                int drive = i;
                string? mounted = VM.GetCdromPath(drive);
                var sub = new MenuFlyoutSubItem
                {
                    Text = $"CD-ROM {drive + 1} ({Cfg86Reader.BusLabel(bus)}): {(mounted == null ? "Empty" : Path.GetFileName(mounted.TrimEnd('\\', '/')))}",
                    Icon = new FontIcon { Glyph = "\uE958" }
                };

                var insert = new MenuFlyoutItem { Text = "Mount ISO / CUE / CHD…", Icon = new FontIcon { Glyph = "\uED25" } };
                insert.Click += async (s, a) => await InsertCdromAsync(drive);
                sub.Items.Add(insert);

                var folder = new MenuFlyoutItem { Text = "Mount Host Directory as CD…", Icon = new FontIcon { Glyph = "\uE8B7" } };
                folder.Click += async (s, a) => await MountCdromFolderAsync(drive);
                sub.Items.Add(folder);

                var eject = new MenuFlyoutItem { Text = "Eject", Icon = new FontIcon { Glyph = "\uF847" }, IsEnabled = mounted != null };
                eject.Click += (s, a) => EjectCdromDrive(drive);
                sub.Items.Add(eject);

                var history = new MenuFlyoutSubItem { Text = "Recent Images", Icon = new FontIcon { Glyph = "\uE81C" } };
                PopulateRecentSubItem(history, VM.RecentCdromImages, path => MountCdromImage(drive, path), VM.ClearCdromHistory);
                sub.Items.Add(history);

                CdromMenuFlyout.Items.Add(sub);
            }

            if (shown == 0)
                CdromMenuFlyout.Items.Add(new MenuFlyoutItem { Text = "No CD-ROM drives configured", IsEnabled = false });
        }

        private void MountCdromImage(int drive, string path)
        {
            _engineService.MountCdrom(drive, path);
            VM.SetCdromPath(drive, path);
        }

        private void EjectCdromDrive(int drive)
        {
            _engineService.EjectCdrom(drive);
            VM.SetCdromPath(drive, null);
            ViewModel.Telemetry.Reset(DeviceChannel.Cd);
        }

        private async Task InsertCdromAsync(int drive)
        {
            var picker = new FileOpenPicker();
            foreach (var ext in new[] { ".iso", ".cue", ".chd", ".toc", ".ccd", ".mds", ".mdx", ".bin", ".img" })
                picker.FileTypeFilter.Add(ext);
            picker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));

            var file = await picker.PickSingleFileAsync();
            if (file != null) MountCdromImage(drive, file.Path);
        }

        private async Task MountCdromFolderAsync(int drive)
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));

            var folder = await picker.PickSingleFolderAsync();
            // The engine treats a path ending in a separator as a host directory.
            if (folder != null) MountCdromImage(drive, folder.Path.TrimEnd('\\') + "\\");
        }

        private async void OnInsertCdromClicked(object sender, RoutedEventArgs e) => await InsertCdromAsync(0);

        private void OnEjectCdromClicked(object sender, RoutedEventArgs e) => EjectCdromDrive(0);

        private static void PopulateRecentSubItem(MenuFlyoutSubItem subItem, IEnumerable<string> recentImages, Action<string> onSelect, Action? onClear = null)
        {
            subItem.Items.Clear();
            var list = recentImages.Where(p => !string.IsNullOrWhiteSpace(p)).Take(10).ToList();
            if (list.Count == 0)
            {
                var emptyItem = new MenuFlyoutItem { Text = "(No recent images)", IsEnabled = false };
                subItem.Items.Add(emptyItem);
                return;
            }

            if (onClear != null)
            {
                var clear = new MenuFlyoutItem { Text = "Clear History", Icon = new FontIcon { Glyph = "\uE74D" } };
                clear.Click += (s, e) => onClear();
                subItem.Items.Add(clear);
                subItem.Items.Add(new MenuFlyoutSeparator());
            }

            foreach (var path in list)
            {
                var item = new MenuFlyoutItem
                {
                    Text = Path.GetFileName(path)
                };
                ToolTipService.SetToolTip(item, path);
                string targetPath = path;
                item.Click += (s, e) => onSelect(targetPath);
                subItem.Items.Add(item);
            }
        }

        #endregion

        #region Live Telemetry & Emulation LED Activity

        /// <summary>
        /// Loaded vs. empty drive: muted icon and "(empty)" label without media, full icon and the mounted
        /// filename in the tooltip with media. An active drive always shows at full strength.
        /// </summary>
        private void UpdateMediaState()
        {
            bool fdMounted = VM.IsFloppyMounted && !string.IsNullOrEmpty(VM.MountedFloppyPath);
            bool cdMounted = VM.IsCdromMounted && !string.IsNullOrEmpty(VM.MountedCdromPath);

            // Presence only changes the neutral brush (primary text with media, secondary when empty);
            // the accent/amber highlight belongs to the read/write callback alone.
            ShowMedia(FloppyIconMounted, FloppyLabel, FloppyFileLabel, FloppyButton, fdMounted,
                      fdMounted ? Path.GetFileName(VM.MountedFloppyPath) : string.Empty,
                      VM.MountedFloppyPath, "Floppy Drive: (Empty) - Click to manage media");

            string cdPath = (VM.MountedCdromPath ?? string.Empty).TrimEnd('\\', '/');
            ShowMedia(CdromIconMounted, CdromLabel, CdromFileLabel, CdromButton, cdMounted,
                      cdMounted ? Path.GetFileName(cdPath) : string.Empty,
                      VM.MountedCdromPath, "CD-ROM Drive: (Empty) - Click to manage media");
        }

        private static void ShowMedia(FontIcon mountedIcon, TextBlock emptyLabel, TextBlock fileLabel, DropDownButton button,
                                      bool mounted, string fileName, string? fullPath, string emptyTip)
        {
            mountedIcon.Opacity = mounted ? 1.0 : 0.0;
            emptyLabel.Visibility = mounted ? Visibility.Collapsed : Visibility.Visible;
            fileLabel.Visibility = mounted ? Visibility.Visible : Visibility.Collapsed;
            fileLabel.Text = fileName;
            ToolTipService.SetToolTip(button, mounted ? fullPath : emptyTip);
        }

        /// <summary>Crossfades the icon's active-colour twin over the idle (secondary text) icon in 180 ms.</summary>
        private static void FadeIcon(FontIcon activeIcon, bool active)
        {
            var animation = new DoubleAnimation
            {
                To = active ? 1.0 : 0.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(180)),
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(animation, activeIcon);
            Storyboard.SetTargetProperty(animation, "Opacity");
            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            storyboard.Begin();
        }

        /// <summary>Mirrors the telemetry channels into the VM flags and the icon strength.</summary>
        private void OnDeviceChannelChanged(DeviceChannel channel, bool active)
        {
            switch (channel)
            {
                case DeviceChannel.Fdd: FadeIcon(FloppyIconActive, active); break;
                case DeviceChannel.Cd: FadeIcon(CdromIconActive, active); break;
                case DeviceChannel.Hdd: FadeIcon(HddIconActive, active); break;
                case DeviceChannel.Net: FadeIcon(NetIconActive, active); break;
                case DeviceChannel.Snd: FadeIcon(SoundIconActive, active); break;
            }
            switch (channel)
            {
                case DeviceChannel.Fdd: VM.IsFloppyActive = active; break;
                case DeviceChannel.Cd: VM.IsCdromActive = active; break;
                case DeviceChannel.Hdd: VM.IsHddActive = active; break;
                case DeviceChannel.Net: VM.IsNetActive = active; break;
                case DeviceChannel.Snd: break;
            }
            UpdateMediaState();
        }

        private void OnLedTimerTick(object? sender, object e)
        {
            if (VM.Status == VMStatus.Running)
            {
                // Dynamic speed jitter (99.0% - 100.0%)
                double speed = 99.1 + (_random.NextDouble() * 0.9);
                SpeedValueText.Text = $"{speed:F1}% (60 FPS)";
            }
            else if (VM.Status == VMStatus.Paused)
            {
                SpeedValueText.Text = "0.0% (0 FPS)";
                PauseBadge.Visibility = Visibility.Visible;
            }

            if (VM.Status == VMStatus.Running)
                ViewModel.Status.SyncLinkState(slot => _engineService.NetworkIsConnected(slot));
        }

        #endregion

        #region Toolbar & Window Command Handlers

        private void OnPauseClicked(object sender, RoutedEventArgs e)
        {
            _engineService.TogglePause();
            _isPaused = _engineService.IsPaused;
            SyncPauseUi();
            if (VM.Status is VMStatus.Running or VMStatus.Paused)
                VM.Status = _isPaused ? VMStatus.Paused : VMStatus.Running;
        }

        /// <summary>Keeps the toggle button, its icon and the title-bar badge in step with the engine's pause state.</summary>
        private bool _pauseUiShown;

        private void SyncPauseUi()
        {
            if (_pauseUiShown == _isPaused && PauseToggle.IsChecked == _isPaused) return;
            _pauseUiShown = _isPaused;
            PauseBadge.Visibility = _isPaused ? Visibility.Visible : Visibility.Collapsed;
            PauseToggle.IsChecked = _isPaused;
            PauseToggle.Icon = new FontIcon { Glyph = _isPaused ? "\uE768" : "\uE769" };
            PauseToggle.Label = _isPaused ? "Resume" : "Pause";
        }

        private void OnAcpiShutdownClicked(object sender, RoutedEventArgs e)
        {
            _engineService.AcpiShutdown();
        }

        private void OnResetClicked(object sender, RoutedEventArgs e)
        {
            _engineService.Reset(hardReset: true);
            Mt32LcdText.Text = "Roland MT-32: RESET";
        }

        private void OnCadClicked(object sender, RoutedEventArgs e)
        {
            _engineService.Reset(hardReset: false);
        }

        private async void OnCaeClicked(object sender, RoutedEventArgs e)
        {
            // Set-1 scancodes: Ctrl 0x1D, Alt 0x38, Esc 0x01
            foreach (var code in new uint[] { 0x1D, 0x38, 0x01 }) _engineService.SendKeyboardInput(code, true);
            await Task.Delay(60);
            foreach (var code in new uint[] { 0x01, 0x38, 0x1D }) _engineService.SendKeyboardInput(code, false);
        }

        private void OnOpenScreenshotsFolderClicked(object sender, RoutedEventArgs e)
        {
            string dir = Path.Combine(VM.Path, "screenshots");
            Directory.CreateDirectory(dir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }

        private void OnDisplayOptionChanged(object sender, RoutedEventArgs e)
        {
            bool lock43 = Lock43Item.IsChecked;
            _engineService.SetAspectMode(stretch: !lock43);
            _engineService.SetDisplayOptions(IntegerScalingItem.IsChecked, CrtShaderItem.IsChecked);
        }

        private void UpdateMouseCaptureBadge()
        {
            bool captured = _mouseCaptured;
            MouseCaptureToggle.IsChecked = captured;
            MouseCaptureToggle.Label = captured ? "Mouse captured" : "Mouse free";
        }

        private void OnNmiClicked(object sender, RoutedEventArgs e) => _engineService.TriggerNmi();

        private async void OnSettingsClicked(object sender, RoutedEventArgs e)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var dialog = new MachineSettingsDialog(VM, hwnd, this.Content.XamlRoot, ViewportContainer.ActualHeight + 200);
            await dialog.ShowAsync();

            // The settings are already on disk; a running machine only picks them up after a restart.
            if (dialog.RestartRequired)
            {
                var choice = await new ContentDialog
                {
                    Title = "Settings saved",
                    Content = "The new configuration is saved. A running machine only picks up hardware changes when it is started again - a Reset does not reload it. Restart the machine now?",
                    PrimaryButtonText = "Restart now",
                    CloseButtonText = "Later",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.Content.XamlRoot
                }.ShowAsync();

                if (choice == ContentDialogResult.Primary)
                {
                    ViewportStandbyOverlay.Visibility = Visibility.Visible;
                    await _engineService.ShutdownAsync();
                    await StartInProcessVmAsync();
                }
            }
            ViewModel.Status.Refresh();
            ViewModel.RefreshMachineCapabilities();
        }

        private void OnFullscreenClicked(object sender, RoutedEventArgs e)
        {
            var appWindow = AppWindow;
            if (appWindow != null)
            {
                _isFullscreen = !_isFullscreen;
                appWindow.SetPresenter(_isFullscreen ? AppWindowPresenterKind.FullScreen : AppWindowPresenterKind.Default);
            }
        }

        private void OnMountFloppyClicked(object sender, RoutedEventArgs e)
        {
            OnInsertFloppyClicked(sender, e);
        }

        private void OnMountCdromClicked(object sender, RoutedEventArgs e)
        {
            OnInsertCdromClicked(sender, e);
        }

        private void OnScreenshotClicked(object sender, RoutedEventArgs e)
        {
            _engineService.TakeScreenshot();
            ViewportInfoBar.Severity = InfoBarSeverity.Success;
            ViewportInfoBar.Title = "Screenshot captured";
            ViewportInfoBar.Message = "Saved to the screenshots folder of this machine.";
            ViewportInfoBar.IsOpen = true;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, args) =>
            {
                ViewportInfoBar.IsOpen = false;
                timer.Stop();
            };
            timer.Start();
        }

        private async void OnCopyScreenshotClicked(object sender, RoutedEventArgs e)
            => await CopyFrameToClipboardAsync(aspectCorrect: Lock43Item.IsChecked);

        private async void OnCopyRawFramebufferClicked(object sender, RoutedEventArgs e)
            => await CopyFrameToClipboardAsync(aspectCorrect: false);

        /// <summary>Puts the current guest frame on the clipboard as a PNG bitmap (raw = the guest's own resolution).</summary>
        private async Task CopyFrameToClipboardAsync(bool aspectCorrect)
        {
            try
            {
                var frame = _engineService.CopyFrame();
                if (frame is not { } f)
                {
                    ShowNotice("Nothing to copy", "The guest has not drawn a frame yet.", InfoBarSeverity.Warning);
                    return;
                }

                var bytes = new byte[f.Pixels.Length * 4];
                Buffer.BlockCopy(f.Pixels, 0, bytes, 0, bytes.Length);

                using var bitmap = Windows.Graphics.Imaging.SoftwareBitmap.CreateCopyFromBuffer(
                    System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.AsBuffer(bytes),
                    Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, f.Width, f.Height, Windows.Graphics.Imaging.BitmapAlphaMode.Ignore);

                var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
                encoder.SetSoftwareBitmap(bitmap);
                if (aspectCorrect)
                {
                    encoder.BitmapTransform.ScaledWidth = (uint)Math.Round(f.Height * 4.0 / 3.0);
                    encoder.BitmapTransform.ScaledHeight = (uint)f.Height;
                    encoder.BitmapTransform.InterpolationMode = Windows.Graphics.Imaging.BitmapInterpolationMode.NearestNeighbor;
                }
                await encoder.FlushAsync();

                var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                package.SetBitmap(Windows.Storage.Streams.RandomAccessStreamReference.CreateFromStream(stream));
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                Windows.ApplicationModel.DataTransfer.Clipboard.Flush();

                ShowNotice(aspectCorrect ? "Screenshot copied" : "Framebuffer copied",
                           $"{f.Width}x{f.Height} image copied to the clipboard.", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowNotice("Could not copy the image", ex.Message, InfoBarSeverity.Error);
            }
        }

        private void ShowNotice(string title, string message, InfoBarSeverity severity)
        {
            ViewportInfoBar.Title = title;
            ViewportInfoBar.Message = message;
            ViewportInfoBar.Severity = severity;
            ViewportInfoBar.IsOpen = true;
        }

        private void OnOpenPrinterTrayClicked(object sender, RoutedEventArgs e)
        {
            string dir = Path.Combine(VM.Path, "printer");
            Directory.CreateDirectory(dir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }

        private async void OnForcePowerOffClicked(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Force power off?",
                Content = "The machine is switched off immediately, like pulling the plug. Unsaved data inside the guest is lost.",
                PrimaryButtonText = "Power Off",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.Content.XamlRoot
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                this.Close(); // OnWindowClosed shuts the engine down
        }

        private void OnDocumentationClicked(object sender, RoutedEventArgs e)
            => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://86box.readthedocs.io/", UseShellExecute = true });

        private void OnVolumeChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (VolumeText == null) return;
            VolumeText.Text = $"{(int)e.NewValue}%";
            ApplySound();
        }

        private void OnMuteToggled(object sender, RoutedEventArgs e) => ApplySound();

        /// <summary>Master volume as dB for the engine's sound_gain (100 % = 0 dB), plus the mute flag.</summary>
        private void ApplySound()
        {
            bool muted = MuteSwitch.IsOn || VolumeSlider.Value <= 0;
            int gainDb = VolumeSlider.Value <= 0 ? -60 : (int)Math.Round(20.0 * Math.Log10(VolumeSlider.Value / 100.0));
            _engineService.SetSound(gainDb, muted);
            SoundIcon.Glyph = muted ? "\uE74F" : "\uE767";
            ViewModel.Telemetry.SignalActivity(DeviceChannel.Snd);
        }

        private async void OnAboutClicked(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "About 86Box",
                Content = $"{VM.Name}\nCPU: {VM.Cpu}\nRAM: {VM.MemoryMb} MB\nVideo: {VM.VideoCard}\nAudio: {VM.SoundCard}\n\nWinUI 3 Modernized Frontend for 86Box Hypervisor.",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private void OnExitClicked(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            ReleaseMouse();
            ReleaseAllKeys();
            RemoveCursorHooks();
            _ledTimer.Stop();
            VM.PropertyChanged -= OnVmPropertyChanged;
            _supervisor.UnregisterEngine(VM);
            _ = _engineService.ShutdownAsync();
            _engineService.Dispose();
            _rendererService.Cleanup();
        }

        #endregion
    }
}
