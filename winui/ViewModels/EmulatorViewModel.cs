using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Box86_WinUI.Models;
using Box86_WinUI.Services;

namespace Box86_WinUI.ViewModels
{
    public class EmulatorViewModel : INotifyPropertyChanged
    {
        private static readonly SolidColorBrush InactiveBrush = new(Color.FromArgb(255, 68, 68, 68));
        private static readonly SolidColorBrush FloppyActiveBrush = new(Color.FromArgb(255, 0, 204, 68));
        private static readonly SolidColorBrush CdromActiveBrush = new(Color.FromArgb(255, 255, 185, 0));
        private static readonly SolidColorBrush RemovableActiveBrush = new(Color.FromArgb(255, 0, 183, 195));
        private static readonly SolidColorBrush HddActiveBrush = new(Color.FromArgb(255, 0, 204, 68));
        private static readonly SolidColorBrush NetActiveBrush = new(Color.FromArgb(255, 0, 153, 255));

        public VirtualMachine VM { get; }
        public StatusViewModel Status { get; }

        /// <summary>The machine has ACPI, so a soft power-button shutdown makes sense (engine MACHINE_ACPI flag).</summary>
        public bool CurrentMachineSupportsAcpi { get; private set; }

        /// <summary>
        /// CurrentMachineSupportsAcpi as a Visibility: a Window root cannot resolve XAML converters in x:Bind
        /// (it is not a FrameworkElement), so the conversion lives here.
        /// </summary>
        public Microsoft.UI.Xaml.Visibility AcpiShutdownVisibility
            => CurrentMachineSupportsAcpi ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        /// <summary>Set by the window: sends the ACPI power button press to the running machine.</summary>
        public Action? AcpiShutdownAction { get; set; }

        public System.Windows.Input.ICommand AcpiShutdownCommand { get; }

        /// <summary>Re-reads the machine from 86box.cfg (also call after the settings were saved).</summary>
        public void RefreshMachineCapabilities()
        {
            bool supports = false;
            try
            {
                if (System.IO.File.Exists(VM.ConfigFilePath))
                {
                    string machine = MachineConfig.Load(VM.ConfigFilePath).GetString("Machine", "machine", "");
                    supports = Cfg86Resolver.SupportsAcpi(machine);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmulatorViewModel] ACPI capability check failed: {ex.Message}");
            }

            if (supports != CurrentMachineSupportsAcpi)
            {
                CurrentMachineSupportsAcpi = supports;
                OnPropertyChanged(nameof(CurrentMachineSupportsAcpi));
                OnPropertyChanged(nameof(AcpiShutdownVisibility));
                (AcpiShutdownCommand as CommunityToolkit.Mvvm.Input.RelayCommand)?.NotifyCanExecuteChanged();
            }
        }
        public DeviceTelemetryService Telemetry { get; }
        public VMProcessSupervisor Supervisor { get; }

        private string _speedText = "100.0%";
        private string _speedHeader = "100% (Realtime)";
        private string _mt32Text = "Roland MT-32: READY";
        private int _viewportWidth = 640;
        private int _viewportHeight = 480;

        public EmulatorViewModel(VirtualMachine vm, VMProcessSupervisor? supervisor = null)
        {
            VM = vm;
            Status = new StatusViewModel(vm);
            AcpiShutdownCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => AcpiShutdownAction?.Invoke(), () => CurrentMachineSupportsAcpi);
            RefreshMachineCapabilities();
            Telemetry = new DeviceTelemetryService(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            Supervisor = supervisor ?? new VMProcessSupervisor();

            VM.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName is nameof(VirtualMachine.MountedFloppyPath) or nameof(VirtualMachine.IsFloppyMounted))
                {
                    OnPropertyChanged(nameof(FloppyStatusText));
                    OnPropertyChanged(nameof(IsFloppyMounted));
                    OnPropertyChanged(nameof(FloppyDisplayName));
                }
                else if (e.PropertyName is nameof(VirtualMachine.MountedCdromPath) or nameof(VirtualMachine.IsCdromMounted))
                {
                    OnPropertyChanged(nameof(CdromStatusText));
                    OnPropertyChanged(nameof(IsCdromMounted));
                    OnPropertyChanged(nameof(CdromDisplayName));
                }
                else if (e.PropertyName is nameof(VirtualMachine.MountedRemovablePath) or nameof(VirtualMachine.IsRemovableMounted))
                {
                    OnPropertyChanged(nameof(RemovableStatusText));
                    OnPropertyChanged(nameof(IsRemovableMounted));
                    OnPropertyChanged(nameof(RemovableDisplayName));
                }
                else if (e.PropertyName is nameof(VirtualMachine.IsFloppyWriteProtected))
                {
                    OnPropertyChanged(nameof(IsFloppyWriteProtected));
                }
                else if (e.PropertyName is nameof(VirtualMachine.IsRemovableWriteProtected))
                {
                    OnPropertyChanged(nameof(IsRemovableWriteProtected));
                }
                else if (e.PropertyName is nameof(VirtualMachine.IsFloppyActive))
                {
                    OnPropertyChanged(nameof(FloppyLedBrush));
                }
                else if (e.PropertyName is nameof(VirtualMachine.IsCdromActive))
                {
                    OnPropertyChanged(nameof(CdromLedBrush));
                }
                else if (e.PropertyName is nameof(VirtualMachine.IsRemovableActive))
                {
                    OnPropertyChanged(nameof(RemovableLedBrush));
                }
                else if (e.PropertyName is nameof(VirtualMachine.IsHddActive))
                {
                    OnPropertyChanged(nameof(HddLedBrush));
                }
                else if (e.PropertyName is nameof(VirtualMachine.IsNetActive))
                {
                    OnPropertyChanged(nameof(NetLedBrush));
                }
            };
        }

        public string FloppyStatusText => VM.IsFloppyMounted ? $"Mounted: {VM.MountedFloppyPath}" : "Drive Empty: (Empty)";
        public string FloppyDisplayName => VM.FloppyDisplayName;
        public bool IsFloppyMounted => VM.IsFloppyMounted;
        public bool IsFloppyWriteProtected
        {
            get => VM.IsFloppyWriteProtected;
            set
            {
                if (VM.IsFloppyWriteProtected != value)
                {
                    VM.IsFloppyWriteProtected = value;
                    VM.SaveMediaConfig();
                    OnPropertyChanged();
                }
            }
        }

        public string CdromStatusText => VM.IsCdromMounted ? $"Mounted: {VM.MountedCdromPath}" : "Drive Empty: (Empty)";
        public string CdromDisplayName => VM.CdromDisplayName;
        public bool IsCdromMounted => VM.IsCdromMounted;

        public string RemovableStatusText => VM.IsRemovableMounted ? $"Mounted: {VM.MountedRemovablePath}" : "Drive Empty: (Empty)";
        public string RemovableDisplayName => VM.RemovableDisplayName;
        public bool IsRemovableMounted => VM.IsRemovableMounted;
        public bool IsRemovableWriteProtected
        {
            get => VM.IsRemovableWriteProtected;
            set
            {
                if (VM.IsRemovableWriteProtected != value)
                {
                    VM.IsRemovableWriteProtected = value;
                    VM.SaveMediaConfig();
                    OnPropertyChanged();
                }
            }
        }

        // Live Dynamic LED Brushes
        public Brush FloppyLedBrush => VM.IsFloppyActive ? FloppyActiveBrush : InactiveBrush;
        public Brush CdromLedBrush => VM.IsCdromActive ? CdromActiveBrush : InactiveBrush;
        public Brush RemovableLedBrush => VM.IsRemovableActive ? RemovableActiveBrush : InactiveBrush;
        public Brush HddLedBrush => VM.IsHddActive ? HddActiveBrush : InactiveBrush;
        public Brush NetLedBrush => VM.IsNetActive ? NetActiveBrush : InactiveBrush;

        public string SpeedText
        {
            get => _speedText;
            set => SetField(ref _speedText, value);
        }

        public string SpeedHeader
        {
            get => _speedHeader;
            set => SetField(ref _speedHeader, value);
        }

        public string Mt32Text
        {
            get => _mt32Text;
            set => SetField(ref _mt32Text, value);
        }

        public int ViewportWidth
        {
            get => _viewportWidth;
            set
            {
                if (SetField(ref _viewportWidth, value))
                {
                    OnPropertyChanged(nameof(ViewportSizeDisplay));
                }
            }
        }

        public int ViewportHeight
        {
            get => _viewportHeight;
            set
            {
                if (SetField(ref _viewportHeight, value))
                {
                    OnPropertyChanged(nameof(ViewportSizeDisplay));
                }
            }
        }

        public string ViewportSizeDisplay => $"{ViewportWidth}x{ViewportHeight}";

        public void FlashFloppyActivity(int durationMs = 350)
        {
            VM.IsFloppyActive = true;
            Task.Delay(durationMs).ContinueWith(_ => VM.IsFloppyActive = false);
        }

        public void FlashCdromActivity(int durationMs = 400)
        {
            VM.IsCdromActive = true;
            Task.Delay(durationMs).ContinueWith(_ => VM.IsCdromActive = false);
        }

        public void FlashRemovableActivity(int durationMs = 350)
        {
            VM.IsRemovableActive = true;
            Task.Delay(durationMs).ContinueWith(_ => VM.IsRemovableActive = false);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
