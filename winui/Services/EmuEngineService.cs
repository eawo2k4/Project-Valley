using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Box86_WinUI.Interop;

namespace Box86_WinUI.Services
{
    public class EmuMessageEventArgs : EventArgs
    {
        public int Level { get; }
        public string Header { get; }
        public string Message { get; }

        public EmuMessageEventArgs(int level, string header, string message)
        {
            Level = level;
            Header = header;
            Message = message;
        }
    }

    /// <summary>
    /// In-process service managing the 86box_core.dll lifecycle, Direct3D 11 DXGI
    /// SwapChain presentation, input injection, and marshaling native telemetry
    /// callbacks onto the UI thread via DispatcherQueue.
    /// </summary>
    public sealed class EmuEngineService : INotifyPropertyChanged, IDisposable
    {
        private readonly DispatcherQueue _dispatcherQueue;
        private EmuTelemetryNativeCallback? _nativeTelemetryCallback;
        private EmuLogNativeCallback? _nativeLogCallback;
        private EmuMessageNativeCallback? _nativeMessageCallback;

        private double _speedPercentage = 100.0;
        private uint _fps = 0;
        private bool _fddActivity = false;
        private bool _hddActivity = false;
        private bool _cdActivity = false;
        private bool _isRunning = false;
        private bool _isPaused = false;
        private bool _isMouseCaptured = false;
        private string _statusString = "Ready";

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<EmuTelemetryData>? TelemetryUpdated;
        public event EventHandler<string>? LogReceived;
        public event EventHandler<EmuMessageEventArgs>? MessageReceived;

        /// <summary>
        /// False when 86box_core.dll (or one of its exports) could not be loaded, e.g. the
        /// native binary is missing next to the WinUI executable. Every lifecycle method
        /// checks this before crossing the P/Invoke boundary so a missing/mismatched native
        /// bridge degrades to a logged no-op instead of crashing the process.
        /// </summary>
        public bool IsNativeAvailable { get; private set; } = true;

        public EmuEngineService(DispatcherQueue? dispatcherQueue = null)
        {
            _dispatcherQueue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread();

            // Retain delegate references to prevent GC collection during native callbacks
            _nativeTelemetryCallback = OnNativeTelemetry;
            _nativeLogCallback = OnNativeLog;
            _nativeMessageCallback = OnNativeMessage;

            Debug.WriteLine("[EmuEngineService] ctor: registering native telemetry/log/message callbacks");
            try
            {
                Box86CoreNative.EmuSetTelemetryCallback(_nativeTelemetryCallback, IntPtr.Zero);
                Box86CoreNative.EmuSetLogCallback(_nativeLogCallback, IntPtr.Zero);
                Box86CoreNative.EmuSetMessageCallback(_nativeMessageCallback, IntPtr.Zero);
                Debug.WriteLine("[EmuEngineService] ctor: native callbacks registered successfully");
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or SEHException)
            {
                IsNativeAvailable = false;
                StatusString = "Native engine unavailable";
                Debug.WriteLine($"[EmuEngineService] ctor: FAILED to bind 86box_core.dll ({ex.GetType().Name}): {ex.Message}");
            }
        }

        #region Observable Telemetry Properties

        public double SpeedPercentage
        {
            get => _speedPercentage;
            private set => SetField(ref _speedPercentage, value);
        }

        public uint Fps
        {
            get => _fps;
            private set => SetField(ref _fps, value);
        }

        public bool FddActivity
        {
            get => _fddActivity;
            private set => SetField(ref _fddActivity, value);
        }

        public bool HddActivity
        {
            get => _hddActivity;
            private set => SetField(ref _hddActivity, value);
        }

        public bool CdActivity
        {
            get => _cdActivity;
            private set => SetField(ref _cdActivity, value);
        }

        public bool IsRunning
        {
            get => _isRunning;
            private set => SetField(ref _isRunning, value);
        }

        public bool IsPaused
        {
            get => _isPaused;
            private set => SetField(ref _isPaused, value);
        }

        public bool IsMouseCaptured
        {
            get => _isMouseCaptured;
            private set => SetField(ref _isMouseCaptured, value);
        }

        public string StatusString
        {
            get => _statusString;
            private set => SetField(ref _statusString, value);
        }

        #endregion

        #region Lifecycle Controls

        /// <summary>
        /// Initializes the 86box_core engine in-process with the configuration directory or file path.
        /// </summary>
        public Task<int> InitializeAsync(string configPath)
        {
            return Task.Run(() =>
            {
                if (!IsNativeAvailable)
                {
                    Debug.WriteLine("[EmuEngineService] InitializeAsync: skipped, native bridge unavailable");
                    return -1;
                }

                Debug.WriteLine($"[EmuEngineService] InitializeAsync: begin (configPath='{configPath}')");
                int result = CallNative(() => Box86CoreNative.EmuInitialize(configPath), nameof(InitializeAsync), -1);
                Debug.WriteLine($"[EmuEngineService] InitializeAsync: end (result={result})");

                _dispatcherQueue.TryEnqueue(() =>
                {
                    StatusString = result == 0 ? "Engine Initialized" : $"Init Error {result}";
                });
                return result;
            });
        }

        /// <summary>
        /// Starts the dedicated emulation worker thread inside 86box_core.dll.
        /// </summary>
        public Task<int> StartAsync()
        {
            return Task.Run(() =>
            {
                if (!IsNativeAvailable)
                {
                    Debug.WriteLine("[EmuEngineService] StartAsync: skipped, native bridge unavailable");
                    return -1;
                }

                Debug.WriteLine("[EmuEngineService] StartAsync: begin");
                int res = CallNative(() => Box86CoreNative.EmuStart(), nameof(StartAsync), -1);
                Debug.WriteLine($"[EmuEngineService] StartAsync: end (result={res})");

                _dispatcherQueue.TryEnqueue(() =>
                {
                    IsRunning = res == 0;
                    IsPaused = false;
                });
                return res;
            });
        }

        /// <summary>
        /// Binds the native swap chain target surface and launches the emulation engine in-process.
        /// </summary>
        public Task<int> StartEmulationAsync(string configPath, IntPtr targetSurface)
        {
            return Task.Run(() =>
            {
                if (!IsNativeAvailable)
                {
                    Debug.WriteLine("[EmuEngineService] StartEmulationAsync: skipped, native bridge unavailable");
                    _dispatcherQueue.TryEnqueue(() => StatusString = "Native engine unavailable");
                    return -1;
                }

                if (targetSurface == IntPtr.Zero)
                {
                    Debug.WriteLine("[EmuEngineService] StartEmulationAsync: WARNING - target surface (SwapChain/HWND) is zero; starting without a bound render target");
                }

                Debug.WriteLine($"[EmuEngineService] StartEmulationAsync: begin (configPath='{configPath}', targetSurface=0x{targetSurface.ToInt64():X})");
                // Two drives on one IDE channel / SCSI ID corrupt the engine's heap when the machine stops or resets;
                // repair a config that has that (older files, hand edits) before the engine reads it.
                foreach (var change in Cfg86ChannelFixer.FixFile(System.IO.Path.Combine(configPath, "86box.cfg")))
                    Debug.WriteLine($"[EmuEngineService] cfg repaired: {change}");

                var missingDisks = Cfg86ChannelFixer.MissingHardDiskImages(System.IO.Path.Combine(configPath, "86box.cfg"));
                if (missingDisks.Count > 0)
                {
                    EngineLogDiagnostics.StartDetail = string.Join(Environment.NewLine, missingDisks);
                    _dispatcherQueue.TryEnqueue(() => StatusString = "Hard disk image missing");
                    return 30;
                }
                string romDir = new ConfigService().GetRomDirectory();
                Debug.WriteLine($"[EmuEngineService] StartEmulationAsync: ROM path = '{romDir}'");
                CallNative(() => { Box86CoreNative.EmuSetRomPath(romDir ?? string.Empty); return 0; }, nameof(StartEmulationAsync), -1);

                // The CRT post-process filter is a WinUI viewport preference (not part of 86box.cfg),
                // stored once for every VM; apply it here so it is already in effect for the very
                // first frame, the same reason ROM path is set here rather than only from Settings.
                var displayConfig = new ConfigService();
                CallNative(() => { Box86CoreNative.EmuSetCrtFilterMode(displayConfig.GetCrtFilterMode()); return 0; }, nameof(StartEmulationAsync), -1);
                CallNative(() => { Box86CoreNative.EmuSetCrtCurvature((float) displayConfig.GetScreenCurvature()); return 0; }, nameof(StartEmulationAsync), -1);
                CallNative(() => { Box86CoreNative.EmuSetCrtBloom((float) displayConfig.GetPhosphorBloom()); return 0; }, nameof(StartEmulationAsync), -1);
                int res = CallNative(() => Box86CoreNative.EmuStartEmulation(configPath, targetSurface), nameof(StartEmulationAsync), -1);
                Debug.WriteLine($"[EmuEngineService] StartEmulationAsync: end (result={res})");

                _dispatcherQueue.TryEnqueue(() =>
                {
                    IsRunning = res == 0;
                    IsPaused = false;
                    StatusString = res == 0 ? "Running" : $"Start Error {res}";
                });
                return res;
            });
        }

        public void Pause(bool pause)
        {
            if (!IsNativeAvailable) return;

            Debug.WriteLine($"[EmuEngineService] Pause({pause}): begin");
            CallNative(() => Box86CoreNative.EmuPause(pause ? 1 : 0), nameof(Pause), -1);
            IsPaused = pause;
            Debug.WriteLine($"[EmuEngineService] Pause({pause}): end");
        }

        public void TogglePause()
        {
            Pause(!IsPaused);
        }

        public void Reset(bool hardReset = false)
        {
            if (!IsNativeAvailable) return;

            Debug.WriteLine($"[EmuEngineService] Reset(hardReset={hardReset}): begin");
            CallNative(() => Box86CoreNative.EmuReset(hardReset ? 1 : 0), nameof(Reset), -1);
            Debug.WriteLine($"[EmuEngineService] Reset(hardReset={hardReset}): end");
        }

        public void SetAspectMode(bool stretch)
        {
            if (!IsNativeAvailable) return;
            CallNative(() => { Box86CoreNative.EmuSetAspectMode(stretch ? 1 : 0); return 0; }, nameof(SetAspectMode), -1);
        }

        public void MountFloppy(int drive, string path, bool writeProtect = false)
        {
            if (!IsNativeAvailable) return;
            CallNative(() => Box86CoreNative.EmuFloppyMount(drive, path, writeProtect ? 1 : 0), nameof(MountFloppy), -1);
        }

        public void EjectFloppy(int drive)
        {
            if (!IsNativeAvailable) return;
            CallNative(() => Box86CoreNative.EmuFloppyEject(drive), nameof(EjectFloppy), -1);
        }

        public void MountCdrom(int drive, string path)
        {
            if (!IsNativeAvailable) return;
            CallNative(() => Box86CoreNative.EmuCdromMount(drive, path), nameof(MountCdrom), -1);
        }

        public void EjectCdrom(int drive)
        {
            if (!IsNativeAvailable) return;
            CallNative(() => Box86CoreNative.EmuCdromEject(drive), nameof(EjectCdrom), -1);
        }

        public void SetFastForward(bool enable)
        {
            if (!IsNativeAvailable) return;
            CallNative(() => { Box86CoreNative.EmuSetFastForward(enable ? 1 : 0); return 0; }, nameof(SetFastForward), -1);
        }

        public void NetworkConnect(int slot, bool connect)
        {
            if (!IsNativeAvailable) return;
            CallNative(() => Box86CoreNative.EmuNetworkConnect(slot, connect ? 1 : 0), nameof(NetworkConnect), -1);
        }

        /// <summary>Live link state of a card slot; null when the engine cannot be asked.</summary>
        public bool? NetworkIsConnected(int slot)
        {
            if (!IsNativeAvailable || !IsRunning) return null;
            int r = CallNative(() => Box86CoreNative.EmuNetworkIsConnected(slot), nameof(NetworkIsConnected), -1);
            return r < 0 ? null : r == 1;
        }

        public void SetDisplayOptions(bool integerScaling, bool crtScanlines)
        {
            if (!IsNativeAvailable) return;
            CallNative(() => { Box86CoreNative.EmuSetDisplayOptions(integerScaling ? 1 : 0, crtScanlines ? 1 : 0); return 0; }, nameof(SetDisplayOptions), -1);
        }

        /// <summary>0 = None (Sharp), 1 = Scanlines (Flat), 2 = Authentic CRT (Curved Glass).</summary>
        public void SetCrtFilterMode(int mode)
        {
            if (!IsNativeAvailable) return;
            CallNative(() => { Box86CoreNative.EmuSetCrtFilterMode(mode); return 0; }, nameof(SetCrtFilterMode), -1);
        }

        /// <summary>0.0 .. 0.15 (0% - 15% screen curvature).</summary>
        public void SetCrtCurvature(float amount)
        {
            if (!IsNativeAvailable) return;
            CallNative(() => { Box86CoreNative.EmuSetCrtCurvature(amount); return 0; }, nameof(SetCrtCurvature), -1);
        }

        /// <summary>0.0 .. 1.0 phosphor bloom intensity.</summary>
        public void SetCrtBloom(float amount)
        {
            if (!IsNativeAvailable) return;
            CallNative(() => { Box86CoreNative.EmuSetCrtBloom(amount); return 0; }, nameof(SetCrtBloom), -1);
        }

        public void TriggerNmi()
        {
            if (!IsNativeAvailable) return;
            CallNative(() => Box86CoreNative.EmuTriggerNmi(), nameof(TriggerNmi), -1);
        }

        public void SetSound(int gainDb, bool muted)
        {
            if (!IsNativeAvailable) return;
            CallNative(() => { Box86CoreNative.EmuSetSound(gainDb, muted ? 1 : 0); return 0; }, nameof(SetSound), -1);
        }

        /// <summary>The current guest frame as BGRA pixels, or null when nothing has been drawn yet.</summary>
        public (uint[] Pixels, int Width, int Height)? CopyFrame()
        {
            if (!IsNativeAvailable) return null;
            int w = 0, h = 0;
            Box86CoreNative.EmuCopyFrame(null, 0, out w, out h);
            if (w <= 0 || h <= 0) return null;

            var buf = new uint[w * h];
            int n = Box86CoreNative.EmuCopyFrame(buf, buf.Length, out w, out h);
            return n > 0 ? (buf, w, h) : null;
        }

        public void AcpiShutdown()
        {
            if (!IsNativeAvailable) return;
            CallNative(() => Box86CoreNative.EmuAcpiPowerButton(), nameof(AcpiShutdown), -1);
        }

        public void TakeScreenshot()
        {
            if (!IsNativeAvailable) return;
            CallNative(() => Box86CoreNative.EmuTakeScreenshot(), nameof(TakeScreenshot), -1);
        }

        /// <summary>
        /// Requests graceful shutdown of the native emulation thread and releases core resources.
        /// </summary>
        public Task ShutdownAsync()
        {
            return Task.Run(() =>
            {
                if (!IsNativeAvailable)
                {
                    Debug.WriteLine("[EmuEngineService] ShutdownAsync: skipped, native bridge unavailable");
                    return;
                }

                Debug.WriteLine("[EmuEngineService] ShutdownAsync: begin");
                CallNative(() => { Box86CoreNative.EmuShutdown(); return 0; }, nameof(ShutdownAsync), -1);
                Debug.WriteLine("[EmuEngineService] ShutdownAsync: end");

                _dispatcherQueue.TryEnqueue(() =>
                {
                    IsRunning = false;
                    IsPaused = false;
                    IsMouseCaptured = false;
                    StatusString = "Stopped";
                });
            });
        }

        #endregion

        #region Direct3D 11 DXGI SwapChain Presentation

        public int SetDxgiSwapChain(IntPtr swapChainHandle)
        {
            if (!IsNativeAvailable) return -1;

            if (swapChainHandle == IntPtr.Zero)
            {
                Debug.WriteLine("[EmuEngineService] SetDxgiSwapChain: WARNING - swap chain handle is zero, skipping bind");
                return -1;
            }

            Debug.WriteLine($"[EmuEngineService] SetDxgiSwapChain: begin (handle=0x{swapChainHandle.ToInt64():X})");
            int result = CallNative(() => Box86CoreNative.EmuSetDxgiSwapChain(swapChainHandle), nameof(SetDxgiSwapChain), -1);
            Debug.WriteLine($"[EmuEngineService] SetDxgiSwapChain: end (result={result})");
            return result;
        }

        public void ResizeViewport(int width, int height)
        {
            if (!IsNativeAvailable) return;
            CallNative(() => { Box86CoreNative.EmuResizeViewport(width, height); return 0; }, nameof(ResizeViewport), -1);
        }

        public void RenderFrame()
        {
            if (!IsNativeAvailable) return;
            CallNative(() => { Box86CoreNative.EmuRenderFrame(); return 0; }, nameof(RenderFrame), -1);
        }

        #endregion

        #region Input Forwarding

        public void SendKeyboardInput(uint scancode, bool isDown)
        {
            if (!IsNativeAvailable) return;
            CallNative(() => { Box86CoreNative.EmuSendInputKeyboard(scancode, isDown ? 1 : 0); return 0; }, nameof(SendKeyboardInput), -1);
        }

        public void SendMouseInput(int dx, int dy, uint buttons)
        {
            if (!IsNativeAvailable) return;
            CallNative(() => { Box86CoreNative.EmuSendInputMouse(dx, dy, buttons); return 0; }, nameof(SendMouseInput), -1);
        }

        public void SetMouseCapture(bool capture)
        {
            if (!IsNativeAvailable) return;
            CallNative(() => { Box86CoreNative.EmuSetMouseCapture(capture ? 1 : 0); return 0; }, nameof(SetMouseCapture), -1);
            IsMouseCaptured = capture;
        }

        public bool GetMouseCapture()
        {
            if (!IsNativeAvailable) return false;
            return CallNative(() => Box86CoreNative.EmuGetMouseCapture(), nameof(GetMouseCapture), 0) != 0;
        }

        #endregion

        /// <summary>
        /// Invokes a single native P/Invoke call, catching binding/marshaling failures
        /// (missing DLL, missing export, ABI mismatch, or an SEH exception raised across
        /// the native boundary) so a bad EmuBridge build degrades gracefully instead of
        /// taking the whole process down.
        /// </summary>
        private static int CallNative(Func<int> nativeCall, string callerName, int errorResult)
        {
            try
            {
                return nativeCall();
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or SEHException or AccessViolationException)
            {
                Debug.WriteLine($"[EmuEngineService] {callerName}: native call failed ({ex.GetType().Name}): {ex.Message}");
                return errorResult;
            }
        }

        #region Native Telemetry Marshaling

        private void OnNativeTelemetry(in EmuTelemetryData telemetry, IntPtr userContext)
        {
            var data = telemetry;
            string status = data.StatusString;

            // Marshal directly to UI Thread via Microsoft.UI.Dispatching.DispatcherQueue
            _dispatcherQueue.TryEnqueue(() =>
            {
                SpeedPercentage = data.SpeedPercentage;
                Fps = data.Fps;
                FddActivity = data.FddActive != 0;
                HddActivity = data.HddActive != 0;
                CdActivity = data.CdActive != 0;
                IsPaused = data.IsPaused != 0;
                IsRunning = data.IsRunning != 0;
                IsMouseCaptured = data.MouseCaptured != 0;
                StatusString = status;

                TelemetryUpdated?.Invoke(this, data);
            });
        }

        private void OnNativeLog(int level, string message, IntPtr userContext)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                LogReceived?.Invoke(this, $"[{level}] {message}");
            });
        }

        private void OnNativeMessage(int level, string header, string message, IntPtr userContext)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                MessageReceived?.Invoke(this, new EmuMessageEventArgs(level, header, message));
            });
        }

        #endregion

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (!Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public void Dispose()
        {
            _nativeTelemetryCallback = null;
            _nativeLogCallback = null;
            _nativeMessageCallback = null;
        }
    }
}
