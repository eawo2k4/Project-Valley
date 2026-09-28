using System;
using System.IO;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Box86_WinUI.Services
{
    /// <summary>
    /// Sets a window's title-bar/taskbar icon via its Win32 handle instead of the higher-level
    /// Window.AppWindow shortcut, and resolves Assets/AppIcon.ico against AppContext.BaseDirectory
    /// so it still finds the file regardless of the process's current working directory (a bare
    /// relative "Assets/AppIcon.ico" only resolves when the CWD happens to be the output folder,
    /// which isn't guaranteed for every launch path - e.g. a shortcut with a different "Start in").
    /// </summary>
    public static class WindowIconHelper
    {
        public static void ApplyAppIcon(Window window)
        {
            try
            {
                nint hWnd = WindowNative.GetWindowHandle(window);
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
                AppWindow? appWindow = AppWindow.GetFromWindowId(windowId);
                string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
                if (appWindow != null && File.Exists(iconPath))
                {
                    appWindow.SetIcon(iconPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WindowIconHelper] Failed to set app icon: {ex.Message}");
            }
        }
    }
}
