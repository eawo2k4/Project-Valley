using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Box86_WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    public static Window? MainWindowInstance { get; private set; }
    public static IntPtr MainWindowHandle => MainWindowInstance != null ? WinRT.Interop.WindowNative.GetWindowHandle(MainWindowInstance) : IntPtr.Zero;

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();

        // Global exception catching: log and swallow so the app never silently
        // disappears (e.g. a native P/Invoke failure from EmuBridge on VM start).
        this.UnhandledException += OnAppUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    /// <summary>Unhandled errors are swallowed to keep the app alive, so they are also written where they can be read.</summary>
    private static void LogToFile(string source, Exception? ex)
    {
        try
        {
            System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "box86_startup.log"),
                $"{DateTime.Now:HH:mm:ss.fff} {source}: {ex}{Environment.NewLine}");
        }
        catch (Exception logEx)
        {
            Debug.WriteLine($"[App] could not write the error log: {logEx.Message}");
        }
    }

    private void OnAppUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Debug.WriteLine($"[App] UnhandledException: {e.Exception}");
        LogToFile("UnhandledException", e.Exception);
        // Prevent the process from terminating; the offending operation failed,
        // but the rest of the UI should stay alive.
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        Debug.WriteLine($"[App] AppDomain.UnhandledException (IsTerminating={e.IsTerminating}): {e.ExceptionObject}");
        LogToFile("AppDomain.UnhandledException", e.ExceptionObject as Exception);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Debug.WriteLine($"[App] UnobservedTaskException: {e.Exception}");
        LogToFile("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        MainWindowInstance = _window;
        _window.Activate();
    }
}
