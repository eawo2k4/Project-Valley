using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;
using Windows.System;
using Windows.UI;
using Box86_WinUI.Interop;
using Box86_WinUI.Services;

namespace Box86_WinUI.Controls
{
    public sealed partial class ViewportHostControl : UserControl
    {
        private readonly DisplayRendererService _rendererService = new();
        private EmuEngineService? _engineService;
        private Window? _parentWindow;

        private bool _isMouseLocked = false;
        private Point _lastPointerPosition;
        private uint _currentMouseButtons = 0;

        private static readonly SolidColorBrush InactiveLedBrush = new(Color.FromArgb(255, 50, 50, 55));
        private static readonly SolidColorBrush FloppyLedBrush = new(Color.FromArgb(255, 0, 220, 80));
        private static readonly SolidColorBrush HddLedBrush = new(Color.FromArgb(255, 255, 170, 0));
        private static readonly SolidColorBrush CdLedBrush = new(Color.FromArgb(255, 0, 160, 255));


        public ViewportHostControl()
        {
            this.InitializeComponent();

            this.SizeChanged += OnViewportSizeChanged;
            this.PointerPressed += OnViewportPointerPressed;
            this.PointerReleased += OnViewportPointerReleased;
            this.PointerMoved += OnViewportPointerMoved;
            this.PointerWheelChanged += OnViewportPointerWheelChanged;
            this.KeyDown += OnViewportKeyDown;
            this.KeyUp += OnViewportKeyUp;
        }

        #region Engine Service Attachment

        public EmuEngineService? EngineService
        {
            get => _engineService;
            set
            {
                if (_engineService != null)
                {
                    _engineService.TelemetryUpdated -= OnTelemetryUpdated;
                    _engineService.MessageReceived -= OnEngineMessageReceived;
                }

                _engineService = value;

                if (_engineService != null)
                {
                    _engineService.TelemetryUpdated += OnTelemetryUpdated;
                    _engineService.MessageReceived += OnEngineMessageReceived;
                    BindRendererToEngine();
                }
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

        public void AttachParentWindow(Window window)
        {
            _parentWindow = window;
        }

        #endregion

        #region Direct3D 11 Initialization & Resize Handling

        private void OnSwapChainPanelLoaded(object sender, RoutedEventArgs e)
        {
            int width = Math.Max(640, (int)ActualWidth);
            int height = Math.Max(480, (int)ActualHeight);

            var unknown = Marshal.GetIUnknownForObject(ViewportSwapChainPanel);
            bool success = _rendererService.InitializeRenderer(unknown, width, height);
            Marshal.Release(unknown);

            if (success)
            {
                BindRendererToEngine();
            }
        }

        private void BindRendererToEngine()
        {
            if (_engineService == null || !_rendererService.IsInitialized) return;

            // Pass the native DXGI SwapChain1 pointer to the 86box_core engine
            _engineService.SetDxgiSwapChain(_rendererService.SwapChainHandle);

            int pixelW = (int)Math.Round(ActualWidth * (XamlRoot?.RasterizationScale ?? 1.0));
            int pixelH = (int)Math.Round(ActualHeight * (XamlRoot?.RasterizationScale ?? 1.0));
            if (pixelW > 0 && pixelH > 0)
            {
                _engineService.ResizeViewport(pixelW, pixelH);
            }
        }

        private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width <= 0 || e.NewSize.Height <= 0) return;

            double scale = XamlRoot?.RasterizationScale ?? 1.0;
            int pixelW = (int)Math.Round(e.NewSize.Width * scale);
            int pixelH = (int)Math.Round(e.NewSize.Height * scale);

            if (_rendererService.IsInitialized)
            {
                _rendererService.ResizeBuffers(pixelW, pixelH);
            }

            _engineService?.ResizeViewport(pixelW, pixelH);
        }

        #endregion
        #region Mouse Capture, Cursor Lock & Input Coordination

        public void ToggleMouseLock()
        {
            SetMouseLock(!_isMouseLocked);
        }

        public void SetMouseLock(bool locked)
        {
            if (_isMouseLocked == locked) return;

            _isMouseLocked = locked;
            _engineService?.SetMouseCapture(locked);

            if (_isMouseLocked)
            {
                // Lock and hide/change host pointer for immersive relative mouse control
                ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Cross);
                ShowCaptureBanner(true);
                this.Focus(FocusState.Programmatic);
            }
            else
            {
                // Release host pointer
                ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
                ShowCaptureBanner(false);
            }
        }

        private void OnViewportPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            this.Focus(FocusState.Pointer);
            var pt = e.GetCurrentPoint(this);
            var props = pt.Properties;

            // Toggle mouse capture on Middle Click
            if (props.IsMiddleButtonPressed)
            {
                ToggleMouseLock();
                e.Handled = true;
                return;
            }

            if (!_isMouseLocked)
            {
                // Click to capture prompt behavior
                return;
            }

            CapturePointer(e.Pointer);
            _lastPointerPosition = pt.Position;

            uint buttons = 0;
            if (props.IsLeftButtonPressed) buttons |= 0x01;
            if (props.IsRightButtonPressed) buttons |= 0x02;
            if (props.IsMiddleButtonPressed) buttons |= 0x04;
            _currentMouseButtons = buttons;

            _engineService?.SendMouseInput(0, 0, buttons);
            e.Handled = true;
        }

        private void OnViewportPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_isMouseLocked) return;

            var pt = e.GetCurrentPoint(this);
            var props = pt.Properties;

            uint buttons = 0;
            if (props.IsLeftButtonPressed) buttons |= 0x01;
            if (props.IsRightButtonPressed) buttons |= 0x02;
            if (props.IsMiddleButtonPressed) buttons |= 0x04;
            _currentMouseButtons = buttons;

            _engineService?.SendMouseInput(0, 0, buttons);

            if (buttons == 0)
            {
                ReleasePointerCapture(e.Pointer);
            }
            e.Handled = true;
        }

        private void OnViewportPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isMouseLocked) return;

            var pt = e.GetCurrentPoint(this);
            int dx = (int)Math.Round(pt.Position.X - _lastPointerPosition.X);
            int dy = (int)Math.Round(pt.Position.Y - _lastPointerPosition.Y);
            _lastPointerPosition = pt.Position;

            if (dx != 0 || dy != 0)
            {
                _engineService?.SendMouseInput(dx, dy, _currentMouseButtons);
            }
            e.Handled = true;
        }

        private void OnViewportPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (!_isMouseLocked) return;

            int delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;
            // 86Box mouse wheel translation if supported
            e.Handled = true;
        }

        #endregion

        #region Keyboard Input Forwarding & Unlock Shortcuts

        private void OnViewportKeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Escape or Ctrl+Alt unlocks mouse capture
            if (_isMouseLocked)
            {
                var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
                var altState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu);

                bool isCtrl = (ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
                bool isAlt = (altState & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

                if (e.Key == VirtualKey.Escape || (isCtrl && isAlt))
                {
                    SetMouseLock(false);
                    e.Handled = true;
                    return;
                }
            }

            uint scancode = e.KeyStatus.ScanCode;
            if (scancode != 0)
            {
                _engineService?.SendKeyboardInput(scancode, isDown: true);
                e.Handled = true;
            }
        }

        private void OnViewportKeyUp(object sender, KeyRoutedEventArgs e)
        {
            uint scancode = e.KeyStatus.ScanCode;
            if (scancode != 0)
            {
                _engineService?.SendKeyboardInput(scancode, isDown: false);
                e.Handled = true;
            }
        }

        #endregion

        #region Telemetry & HUD Presentation

        private void OnTelemetryUpdated(object? sender, EmuTelemetryData data)
        {
            // Telemetry arrives pre-marshaled to UI thread by EmuEngineService
            SpeedFpsTextBlock.Text = $"{Math.Round(data.SpeedPercentage)}% ({data.Fps} FPS)";
            SpeedFpsTextBlock.Foreground = data.SpeedPercentage >= 95.0
                ? new SolidColorBrush(Color.FromArgb(255, 160, 224, 160))
                : new SolidColorBrush(Color.FromArgb(255, 255, 200, 100));

            FloppyLed.Fill = (data.FddActive != 0) ? FloppyLedBrush : InactiveLedBrush;
            HddLed.Fill = (data.HddActive != 0) ? HddLedBrush : InactiveLedBrush;
            CdLed.Fill = (data.CdActive != 0) ? CdLedBrush : InactiveLedBrush;

            PauseOverlay.Visibility = (data.IsPaused != 0) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowCaptureBanner(bool show)
        {
            if (show)
            {
                CaptureBanner.Opacity = 1.0;
                CaptureBanner.Translation = new System.Numerics.Vector3(0, 0, 0);
            }
            else
            {
                CaptureBanner.Opacity = 0.0;
                CaptureBanner.Translation = new System.Numerics.Vector3(0, -20, 0);
            }
        }

        #endregion
    }
}
