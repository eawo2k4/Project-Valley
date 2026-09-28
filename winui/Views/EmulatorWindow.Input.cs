using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Box86_WinUI.Views
{
    /// <summary>
    /// Keyboard and mouse forwarding from the viewport to the 86Box core: raw scancodes for keys, and
    /// relative deltas (cursor pinned to the viewport centre) plus button state for the mouse.
    /// </summary>
    public sealed partial class EmulatorWindow
    {
        #region Win32

        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] private static extern bool ClipCursor(ref RECT r);
        [DllImport("user32.dll", EntryPoint = "ClipCursor")] private static extern bool ClipCursorRelease(IntPtr r);
        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd, ref POINT p);
        [DllImport("user32.dll")] private static extern int ShowCursor(bool show);
        [DllImport("user32.dll")] private static extern IntPtr SetCursor(IntPtr hCursor);

        // Window subclassing: WinUI answers WM_SETCURSOR from its child content windows, so the hook goes on the
        // top-level window and on every descendant. While the mouse is captured it blanks the cursor and reports
        // the message as handled so Windows cannot put the arrow back.
        private delegate IntPtr SubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data);
        private delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc proc, UIntPtr id, UIntPtr data);
        [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc proc, UIntPtr id);
        [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumChildProc proc, IntPtr lParam);

        // Last resort that works regardless of which window/thread owns the pointer: while captured, every
        // standard system cursor is replaced by a fully transparent one, and the user's scheme is reloaded on release.
        [DllImport("user32.dll")] private static extern IntPtr CreateCursor(IntPtr hInst, int xHotSpot, int yHotSpot, int width, int height, byte[] andPlane, byte[] xorPlane);
        [DllImport("user32.dll")] private static extern bool SetSystemCursor(IntPtr hcur, uint id);
        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")] private static extern bool SystemParametersInfo(uint action, uint param, IntPtr pvParam, uint winIni);

        private const uint SPI_SETCURSORS = 0x0057;
        private static readonly uint[] SystemCursorIds =
        {
            32512, // OCR_NORMAL (arrow)
            32513, // OCR_IBEAM
            32514, // OCR_WAIT
            32515, // OCR_CROSS
            32649, // OCR_HAND
            32650, // OCR_APPSTARTING
        };

        private static bool _systemCursorBlanked;

        private static void BlankSystemCursors()
        {
            if (_systemCursorBlanked) return;
            _systemCursorBlanked = true;

            // 32x32 monochrome cursor: AND plane all 1s (keep background), XOR plane all 0s = invisible.
            var and = new byte[128]; Array.Fill(and, (byte)0xFF);
            var xor = new byte[128];
            foreach (var id in SystemCursorIds)
            {
                var blank = CreateCursor(IntPtr.Zero, 0, 0, 32, 32, and, xor); // SetSystemCursor takes ownership of the handle
                if (blank != IntPtr.Zero) SetSystemCursor(blank, id);
            }
        }

        /// <summary>Reloads the user's real cursor scheme. Safe to call at any time, including when nothing was blanked.</summary>
        private static void RestoreSystemCursors()
        {
            SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
            _systemCursorBlanked = false;
        }

        private const uint WM_SETCURSOR = 0x0020;
        private const uint WM_NCDESTROY = 0x0082;
        private static readonly UIntPtr CursorHookId = new(0x86B0C);

        private SubclassProc? _cursorHook;
        private readonly HashSet<IntPtr> _hookedWindows = new();

        private IntPtr CursorHookProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data)
        {
            if (msg == WM_SETCURSOR && _mouseCaptured)
            {
                SetCursor(IntPtr.Zero);
                return (IntPtr)1; // handled: Windows must not restore the arrow
            }

            if (msg == WM_NCDESTROY)
            {
                RemoveWindowSubclass(hWnd, _cursorHook!, CursorHookId);
                _hookedWindows.Remove(hWnd);
            }

            return DefSubclassProc(hWnd, msg, wParam, lParam);
        }

        private void InstallCursorHooks()
        {
            _cursorHook ??= CursorHookProc; // the delegate must stay alive as long as the window is subclassed
            var top = WinRT.Interop.WindowNative.GetWindowHandle(this);

            void Hook(IntPtr h)
            {
                if (_hookedWindows.Add(h))
                    SetWindowSubclass(h, _cursorHook!, CursorHookId, UIntPtr.Zero);
            }

            Hook(top);
            EnumChildWindows(top, (child, _) => { Hook(child); return true; }, IntPtr.Zero);
        }

        private void RemoveCursorHooks()
        {
            if (_cursorHook == null) return;
            foreach (var h in _hookedWindows.ToArray())
                RemoveWindowSubclass(h, _cursorHook, CursorHookId);
            _hookedWindows.Clear();
        }

        #endregion

        private const uint ButtonLeft = 1, ButtonRight = 2, ButtonMiddle = 4;

        private readonly HashSet<uint> _pressedScancodes = new();
        private bool _mouseCaptured;
        private bool _cursorHidden;
        private uint _mouseButtons;
        private POINT _captureCenter;

        // -------------------------------------------------------------- Fast forward

        private void OnFastForwardClicked(object sender, RoutedEventArgs e)
        {
            bool on = FastForwardToggle.IsChecked == true;
            _engineService.SetFastForward(on);
            FastForwardToggle.Label = on ? "Fast Forward (on)" : "Fast Forward";
        }

        // -------------------------------------------------------------- Keyboard

        /// <summary>Set-1 scancode as 86Box expects it: extended keys carry the 0x100 flag.</summary>
        private static uint ToScancode(KeyRoutedEventArgs e)
        {
            uint code = e.KeyStatus.ScanCode & 0xFF;
            return e.KeyStatus.IsExtendedKey ? code | 0x100u : code;
        }

        // Host shortcuts that must reach the CommandBar accelerators instead of the guest:
        // F11 / Pause, Ctrl+F10/F11/F12, Ctrl+Shift+P/F/F12.
        private static bool IsHostHotkey(KeyRoutedEventArgs e)
        {
            bool ctrl = IsDown(VirtualKey.Control), shift = IsDown(VirtualKey.Shift);
            if (e.Key is VirtualKey.F11 or VirtualKey.Pause) return true;
            if (ctrl && !shift && e.Key is VirtualKey.F10 or VirtualKey.F11 or VirtualKey.F12) return true;
            if (ctrl && shift && e.Key is VirtualKey.P or VirtualKey.F or VirtualKey.F12) return true;
            return false;
        }

        private static bool IsDown(VirtualKey key)
            => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        private void OnViewportKeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Release hotkeys for a captured mouse: Ctrl+Alt or Ctrl+End (middle click is handled on the pointer).
            bool ctrlAlt = IsDown(VirtualKey.Control) && IsDown(VirtualKey.Menu);
            if (_mouseCaptured && ((e.Key == VirtualKey.End && IsDown(VirtualKey.Control)) || ctrlAlt))
            {
                ReleaseMouse();
                ReleaseAllKeys();
                e.Handled = true;
                return;
            }

            if (IsHostHotkey(e)) return; // let the CommandBar accelerators (F11, F12, Pause, Ctrl+Shift+F) run

            uint code = ToScancode(e);
            if (code == 0 || e.KeyStatus.WasKeyDown) { e.Handled = true; return; } // the guest does its own typematic

            _pressedScancodes.Add(code);
            _engineService.SendKeyboardInput(code, true);
            e.Handled = true; // keep Tab / arrows / Alt from navigating the host UI
        }

        private void OnViewportKeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (IsHostHotkey(e)) return;

            uint code = ToScancode(e);
            if (code == 0) { e.Handled = true; return; }

            if (_pressedScancodes.Remove(code))
                _engineService.SendKeyboardInput(code, false);
            e.Handled = true;
        }

        private void ReleaseAllKeys()
        {
            foreach (var code in _pressedScancodes) _engineService.SendKeyboardInput(code, false);
            _pressedScancodes.Clear();
        }

        private void OnViewportLostFocus(object sender, RoutedEventArgs e)
        {
            // Never leave a key stuck down in the guest when focus moves away.
            ReleaseAllKeys();
        }

        // -------------------------------------------------------------- Mouse

        private void OnViewportPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            InputSurface.Focus(FocusState.Programmatic);

            var props = e.GetCurrentPoint(InputSurface).Properties;

            if (!_mouseCaptured)
            {
                // First click only captures; it is not delivered to the guest.
                if (props.IsLeftButtonPressed) CaptureMouse(e);
                e.Handled = true;
                return;
            }

            if (props.IsMiddleButtonPressed)
            {
                ReleaseMouse(); // middle click releases
                e.Handled = true;
                return;
            }

            _mouseButtons = (props.IsLeftButtonPressed ? ButtonLeft : 0)
                          | (props.IsRightButtonPressed ? ButtonRight : 0);
            _engineService.SendMouseInput(0, 0, _mouseButtons);
            e.Handled = true;
        }

        private void OnViewportPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_mouseCaptured) return;

            var props = e.GetCurrentPoint(InputSurface).Properties;
            _mouseButtons = (props.IsLeftButtonPressed ? ButtonLeft : 0)
                          | (props.IsRightButtonPressed ? ButtonRight : 0);
            _engineService.SendMouseInput(0, 0, _mouseButtons);
            e.Handled = true;
        }

        private void OnViewportPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_mouseCaptured) return;

            // Relative motion: distance from the pinned centre, then re-pin.
            if (!GetCursorPos(out var pos)) return;
            int dx = pos.X - _captureCenter.X;
            int dy = pos.Y - _captureCenter.Y;
            if (dx == 0 && dy == 0) return;

            SetCursorPos(_captureCenter.X, _captureCenter.Y);
            SetCursor(IntPtr.Zero);
            _engineService.SendMouseInput(dx, dy, _mouseButtons);
            e.Handled = true;
        }

        /// <summary>Viewport rectangle in screen pixels.</summary>
        private bool TryGetViewportScreenRect(out RECT rect)
        {
            rect = default;
            if (InputSurface.XamlRoot == null || this.Content is not UIElement root) return false;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var origin = new POINT { X = 0, Y = 0 };
            if (!ClientToScreen(hwnd, ref origin)) return false;

            double scale = InputSurface.XamlRoot.RasterizationScale;
            var topLeft = InputSurface.TransformToVisual(root).TransformPoint(new Windows.Foundation.Point(0, 0));
            rect = new RECT
            {
                Left = origin.X + (int)Math.Round(topLeft.X * scale),
                Top = origin.Y + (int)Math.Round(topLeft.Y * scale),
            };
            rect.Right = rect.Left + (int)Math.Round(InputSurface.ActualWidth * scale);
            rect.Bottom = rect.Top + (int)Math.Round(InputSurface.ActualHeight * scale);
            return rect.Right > rect.Left && rect.Bottom > rect.Top;
        }

        // UIElement.ProtectedCursor is protected; a custom control subclass would break the XAML compiler here,
        // so the property is set through reflection on the input surface.
        private static readonly System.Reflection.PropertyInfo? ProtectedCursorProperty =
            typeof(UIElement).GetProperty("ProtectedCursor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

        private void SetHostCursor(InputSystemCursorShape shape)
        {
            try
            {
                var cursor = InputSystemCursor.Create(shape);
                ProtectedCursorProperty?.SetValue(InputSurface, cursor);
                if (this.Content is UIElement root) ProtectedCursorProperty?.SetValue(root, cursor);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[EmulatorWindow] could not set the host cursor: {ex.Message}"); }
        }

        private void CaptureMouse(PointerRoutedEventArgs? e)
        {
            if (!TryGetViewportScreenRect(out var rect)) return;

            if (e != null) InputSurface.CapturePointer(e.Pointer);

            ClipCursor(ref rect);
            _captureCenter = new POINT { X = (rect.Left + rect.Right) / 2, Y = (rect.Top + rect.Bottom) / 2 };
            SetCursorPos(_captureCenter.X, _captureCenter.Y);

            InstallCursorHooks();
            BlankSystemCursors();
            SetCursor(IntPtr.Zero); // InputSystemCursorShape has no "None"; WM_SETCURSOR is answered by the hook above
            if (!_cursorHidden) { while (ShowCursor(false) >= 0) { } _cursorHidden = true; } // drain the OS show-counter

            _mouseCaptured = true;
            _mouseButtons = 0;
            _engineService.SetMouseCapture(true);
            UpdateMouseCaptureBadge();
        }

        private void OnMouseCaptureToggleClicked(object sender, RoutedEventArgs e)
        {
            if (_mouseCaptured) ReleaseMouse();
            else
            {
                InputSurface.Focus(FocusState.Programmatic);
                CaptureMouse(null);
            }
            UpdateMouseCaptureBadge();
        }

        private void ReleaseMouse()
        {
            if (!_mouseCaptured) return;

            _mouseCaptured = false;
            if (_mouseButtons != 0) _engineService.SendMouseInput(0, 0, 0);
            _mouseButtons = 0;
            _engineService.SetMouseCapture(false);

            RestoreSystemCursors();
            ClipCursorRelease(IntPtr.Zero);
            if (_cursorHidden) { while (ShowCursor(true) < 0) { } _cursorHidden = false; }
            SetHostCursor(Microsoft.UI.Input.InputSystemCursorShape.Arrow);
            InputSurface.ReleasePointerCaptures();
            UpdateMouseCaptureBadge();
        }
    }
}
