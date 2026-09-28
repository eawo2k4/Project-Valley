using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Box86_WinUI.Models;
using Box86_WinUI.Services;

namespace Box86_WinUI.ViewModels
{
    /// <summary>One selectable bus channel; unavailable when another drive already sits on it.</summary>
    public sealed record ChannelOption(string Name, bool IsAvailable)
    {
        public string Display => IsAvailable ? Name : Name + " (in use)";
    }

    public class HardDiskEntry : ObservableObject
    {
        private List<ChannelOption> _channelOptions = new();

        /// <summary>The channels of this disk's bus, those used by other drives marked unavailable (kept current by the view model).</summary>
        public List<ChannelOption> ChannelOptions
        {
            get => _channelOptions;
            set => SetProperty(ref _channelOptions, value);
        }

        private int _channel = 0;
        private string _type = "IDE";
        private string _bus = "IDE";
        private string _channelName = "0:0";
        private string _model = "[ATA-5] Quantum Fireball CR8.4A";
        private string _audio = "None";
        private int _cylinders = 2031;
        private int _heads = 16;
        private int _sectors = 63;
        private int _sizeMb = 999;
        private string _imagePath = string.Empty;

        public int Channel
        {
            get => _channel;
            set => SetProperty(ref _channel, value);
        }

        public string Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        public string Bus
        {
            get => _bus;
            set
            {
                if (SetProperty(ref _bus, value))
                    OnPropertyChanged(nameof(BusChannelDisplay));
            }
        }

        public string ChannelName
        {
            get => _channelName;
            set
            {
                // A combo box briefly clears its selection while its item list is swapped; ignore that.
                if (string.IsNullOrEmpty(value)) { OnPropertyChanged(); return; }
                if (SetProperty(ref _channelName, value))
                    OnPropertyChanged(nameof(BusChannelDisplay));
            }
        }

        public string BusChannelDisplay => $"{Bus} {ChannelName}";

        public string Model
        {
            get => _model;
            set => SetProperty(ref _model, value);
        }

        public string Audio
        {
            get => _audio;
            set => SetProperty(ref _audio, value);
        }

        public int Cylinders
        {
            get => _cylinders;
            set
            {
                if (SetProperty(ref _cylinders, value))
                {
                    OnPropertyChanged(nameof(CylindersD));
                    RecalculateSize();
                }
            }
        }
        public double CylindersD
        {
            get => Cylinders;
            set => Cylinders = (int)value;
        }

        public int Heads
        {
            get => _heads;
            set
            {
                if (SetProperty(ref _heads, value))
                {
                    OnPropertyChanged(nameof(HeadsD));
                    RecalculateSize();
                }
            }
        }
        public double HeadsD
        {
            get => Heads;
            set => Heads = (int)value;
        }

        public int Sectors
        {
            get => _sectors;
            set
            {
                if (SetProperty(ref _sectors, value))
                {
                    OnPropertyChanged(nameof(SectorsD));
                    RecalculateSize();
                }
            }
        }
        public double SectorsD
        {
            get => Sectors;
            set => Sectors = (int)value;
        }

        public int SizeMb
        {
            get => _sizeMb;
            set
            {
                if (SetProperty(ref _sizeMb, value))
                {
                    OnPropertyChanged(nameof(SizeMbD));
                    OnPropertyChanged(nameof(GeometryDisplay));
                }
            }
        }
        public double SizeMbD
        {
            get => SizeMb;
            set => SizeMb = (int)value;
        }

        public string ImagePath
        {
            get => _imagePath;
            set => SetProperty(ref _imagePath, value);
        }

        public string GeometryDisplay => $"{Cylinders}, {Heads}, {Sectors} ({SizeMb} MiB)";

        public void RecalculateSize()
        {
            long totalSectors = (long)Cylinders * Heads * Sectors;
            SizeMb = (int)(totalSectors / 2048); // 512 bytes per sector = 2048 sectors per MiB
            OnPropertyChanged(nameof(GeometryDisplay));
        }
    }

    public class FloppyDriveEntry : ObservableObject
    {
        private int _index;
        private string _type = "None";
        private bool _turbo = false;
        private bool _checkBpb = true;
        private string _audio = "None";

        public int Index
        {
            get => _index;
            set => SetProperty(ref _index, value);
        }

        public string DriveLetter => $"Drive {((char)('A' + Index))}:";

        public string Type
        {
            get => _type;
            set
            {
                if (SetProperty(ref _type, value))
                {
                    OnPropertyChanged(nameof(IconPath));
                }
            }
        }

        public bool Turbo
        {
            get => _turbo;
            set
            {
                if (SetProperty(ref _turbo, value))
                    OnPropertyChanged(nameof(TurboDisplay));
            }
        }

        public string TurboDisplay => Turbo ? "On" : "Off";

        public bool CheckBpb
        {
            get => _checkBpb;
            set
            {
                if (SetProperty(ref _checkBpb, value))
                    OnPropertyChanged(nameof(CheckBpbDisplay));
            }
        }

        public string CheckBpbDisplay => CheckBpb ? "On" : "Off";

        public string Audio
        {
            get => _audio;
            set => SetProperty(ref _audio, value);
        }

        public string Glyph => "";
        public double IconOpacity => (Type == "None" || string.IsNullOrEmpty(Type)) ? 0.45 : 1.0;

        public string IconPath => (Type == "None" || string.IsNullOrEmpty(Type))
            ? "ms-appx:///Assets/Icons/disabled.ico"
            : (Type.Contains("5.25") ? "ms-appx:///Assets/Icons/floppy_525.ico" : "ms-appx:///Assets/Icons/floppy_35.ico");
    }

    public class CdromDriveEntry : ObservableObject
    {
        private int _index;
        private string _bus = "Disabled";
        private string _channel = "0:1";
        private string _speed = "8x";
        private string _type = "86cd";
        private bool _useEdcEcc = true;

        public int Index
        {
            get => _index;
            set => SetProperty(ref _index, value);
        }

        public string Bus
        {
            get => _bus;
            set
            {
                if (SetProperty(ref _bus, value))
                {
                    OnPropertyChanged(nameof(BusDisplay));
                    OnPropertyChanged(nameof(Glyph));
                    OnPropertyChanged(nameof(IconPath));
                }
            }
        }

        public string Channel
        {
            get => _channel;
            set
            {
                if (SetProperty(ref _channel, value))
                    OnPropertyChanged(nameof(BusDisplay));
            }
        }

        public string BusDisplay => Bus == "Disabled" ? "Disabled" : $"{Bus} ({Channel})";

        public string Speed
        {
            get => _speed;
            set => SetProperty(ref _speed, value);
        }

        public string Type
        {
            get => _type;
            set
            {
                if (SetProperty(ref _type, value))
                {
                    OnPropertyChanged(nameof(TypeDisplay));
                    OnPropertyChanged(nameof(Glyph));
                    OnPropertyChanged(nameof(IconPath));
                }
            }
        }

        public string TypeDisplay => HardwareCatalog.GetCdromDisplayName(Type);

        public bool UseEdcEcc
        {
            get => _useEdcEcc;
            set => SetProperty(ref _useEdcEcc, value);
        }

        public string Glyph => (Bus == "Disabled" || Type == "none" || Type == "None" || string.IsNullOrEmpty(Type)) ? "\uE711" : "\uE958";
        public double IconOpacity => (Bus == "Disabled" || Type == "none" || Type == "None" || string.IsNullOrEmpty(Type)) ? 0.45 : 1.0;

        public string IconPath => (Bus == "Disabled" || Type == "none" || Type == "None" || string.IsNullOrEmpty(Type))
            ? "ms-appx:///Assets/Icons/disabled.ico"
            : (HardwareCatalog.IsCdromDvd(Type) ? "ms-appx:///Assets/Icons/dvdrom.ico" : "ms-appx:///Assets/Icons/cdrom.ico");
    }

    /// <summary>The per-adapter options of the Network tab and which of them the current mode shows.</summary>
    public partial class NetCardOptions : ObservableObject
    {
        [ObservableProperty] public partial string HostDevice { get; set; } = string.Empty;
        [ObservableProperty] public partial string Secret { get; set; } = string.Empty;
        [ObservableProperty] public partial bool Promiscuous { get; set; }

        private bool _pcap, _local;

        public Microsoft.UI.Xaml.Visibility InterfaceVisibility => Vis(_pcap);
        public Microsoft.UI.Xaml.Visibility SecretVisibility => Vis(_local);
        public Microsoft.UI.Xaml.Visibility HubModeVisibility => Vis(_local);
        public Microsoft.UI.Xaml.Visibility OptionsVisibility => Vis(_pcap || _local);

        private static Microsoft.UI.Xaml.Visibility Vis(bool v) => v ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

        public void Update(string mode, bool hasCard)
        {
            _pcap = hasCard && mode == "PCap";
            _local = hasCard && mode == "Local Switch";
            OnPropertyChanged(nameof(InterfaceVisibility));
            OnPropertyChanged(nameof(SecretVisibility));
            OnPropertyChanged(nameof(HubModeVisibility));
            OnPropertyChanged(nameof(OptionsVisibility));
        }
    }

    public class GenericRemovableDriveEntry : ObservableObject
    {
        private int _index;
        private string _bus = "Disabled";
        private string _channel = "";
        private string _type = "None";
        private string _glyph = "\uE88E";

        public int Index
        {
            get => _index;
            set => SetProperty(ref _index, value);
        }

        public string Bus
        {
            get => _bus;
            set
            {
                if (SetProperty(ref _bus, value))
                {
                    OnPropertyChanged(nameof(BusDisplay));
                    OnPropertyChanged(nameof(TypeDisplay));
                    OnPropertyChanged(nameof(IsChannelEnabled));
                    OnPropertyChanged(nameof(IconOpacity));
                }
            }
        }

        public string Channel
        {
            get => _channel;
            set
            {
                if (SetProperty(ref _channel, value))
                    OnPropertyChanged(nameof(BusDisplay));
            }
        }

        public string BusDisplay => Bus == "Disabled" ? "Disabled" : (string.IsNullOrWhiteSpace(Channel) ? Bus : $"{Bus} {Channel}");

        public string Type
        {
            get => _type;
            set
            {
                if (SetProperty(ref _type, value))
                {
                    OnPropertyChanged(nameof(TypeDisplay));
                }
            }
        }

        public string TypeDisplay => (Bus == "Disabled" || string.IsNullOrWhiteSpace(Type)) ? "None" : Type;

        public string Glyph
        {
            get => _glyph;
            set => SetProperty(ref _glyph, value);
        }

        public double IconOpacity => Bus == "Disabled" ? 0.45 : 1.0;
        public bool IsChannelEnabled => Bus != "Disabled";
    }

    public partial class SettingsViewModel : ObservableObject
    {
        public VirtualMachine VM { get; }
        private readonly MachineConfig _config;
        private readonly Action _closeWindow;

        // ==========================================
        // Category Navigation (11 Authentic Categories + Manager Settings)
        // ==========================================
        [ObservableProperty]
        public partial string CurrentCategory { get; set; } = "Machine";

        public bool IsMachineCategory => CurrentCategory == "Machine";
        public bool IsDisplayCategory => CurrentCategory == "Display";
        public bool IsInputCategory => CurrentCategory == "Input devices";
        public bool IsSoundCategory => CurrentCategory == "Sound";
        public bool IsNetworkCategory => CurrentCategory == "Network";
        public bool IsPortsCategory => CurrentCategory == "Ports (COM & LPT)";
        public bool IsStorageControllersCategory => CurrentCategory == "Storage controllers";
        public bool IsHardDisksCategory => CurrentCategory == "Hard disks";
        public bool IsFloppyCDROMCategory => CurrentCategory == "Floppy & CD-ROM drives";
        public bool IsOtherRemovableCategory => CurrentCategory == "Other removable devices";
        public bool IsOtherPeripheralsCategory => CurrentCategory == "Other peripherals";
        public bool IsAdvancedCategory => CurrentCategory == "Advanced";
        public bool IsManagerSettingsCategory => CurrentCategory == "Manager settings" || CurrentCategory == "Manager Settings" || CurrentCategory == "Manager";

        public string CurrentCategoryIcon => CurrentCategory switch
        {
            "Machine" => "ms-appx:///Assets/Icons/machine.ico",
            "Display" => "ms-appx:///Assets/Icons/display.ico",
            "Input devices" => "ms-appx:///Assets/Icons/input_devices.ico",
            "Sound" => "ms-appx:///Assets/Icons/sound.ico",
            "Network" => "ms-appx:///Assets/Icons/network.ico",
            "Ports (COM & LPT)" => "ms-appx:///Assets/Icons/ports.ico",
            "Storage controllers" => "ms-appx:///Assets/Icons/storage_controllers.ico",
            "Hard disks" => "ms-appx:///Assets/Icons/hard_disk.ico",
            "Floppy & CD-ROM drives" => "ms-appx:///Assets/Icons/floppy_and_cdrom_drives.ico",
            "Other removable devices" => "ms-appx:///Assets/Icons/other_removable_devices.ico",
            "Other peripherals" => "ms-appx:///Assets/Icons/other_peripherals.ico",
            _ => "ms-appx:///Assets/Icons/machine.ico"
        };

        partial void OnCurrentCategoryChanged(string value)
        {
            OnPropertyChanged(nameof(CurrentCategoryIcon));
            OnPropertyChanged(nameof(IsMachineCategory));
            OnPropertyChanged(nameof(IsDisplayCategory));
            OnPropertyChanged(nameof(IsInputCategory));
            OnPropertyChanged(nameof(IsSoundCategory));
            OnPropertyChanged(nameof(IsNetworkCategory));
            OnPropertyChanged(nameof(IsPortsCategory));
            OnPropertyChanged(nameof(IsStorageControllersCategory));
            OnPropertyChanged(nameof(IsHardDisksCategory));
            OnPropertyChanged(nameof(IsFloppyCDROMCategory));
            OnPropertyChanged(nameof(IsOtherRemovableCategory));
            OnPropertyChanged(nameof(IsOtherPeripheralsCategory));
            OnPropertyChanged(nameof(IsAdvancedCategory));
            OnPropertyChanged(nameof(IsManagerSettingsCategory));
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [RelayCommand]
        public async Task OpenConfigFileAsync()
        {
            string cfg = VM.ConfigFilePath;
            if (!File.Exists(cfg))
                return;

            try
            {
                var storageFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(cfg);
                await Windows.System.Launcher.LaunchFileAsync(storageFile);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsViewModel] Failed to open config file: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task ExportConfigFileAsync()
        {
            string cfg = VM.ConfigFilePath;
            if (!File.Exists(cfg))
                return;

            try
            {
                var picker = new Windows.Storage.Pickers.FileSavePicker
                {
                    SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
                    SuggestedFileName = $"{VM.Name}.cfg"
                };
                picker.FileTypeChoices.Add("86Box Configuration", new List<string> { ".cfg" });

                IntPtr hwnd = App.MainWindowHandle;
                if (hwnd == IntPtr.Zero)
                {
                    hwnd = GetActiveWindow();
                }

                if (hwnd != IntPtr.Zero)
                {
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                }

                var destination = await picker.PickSaveFileAsync();
                if (destination != null)
                {
                    File.Copy(cfg, destination.Path, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsViewModel] Failed to export config file: {ex.Message}");
            }
        }

        // ==========================================
        // Manager Settings / ROMs Path Management
        // ==========================================
        private readonly Services.ConfigService _configService = new();

        [ObservableProperty]
        public partial string RomDirectory { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsRomPathConfigured { get; set; } = false;

        public bool IsRomPathMissing => !IsRomPathConfigured;
        public bool IsRomPathNotConfigured => !IsRomPathConfigured;

        [ObservableProperty]
        public partial bool IsRomPathValid { get; set; } = false;

        [ObservableProperty]
        public partial string RomValidationMessage { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string RomValidationBadgeText { get; set; } = "Not configured";

        [ObservableProperty]
        public partial string RomValidationBadgeStyleKey { get; set; } = "AttentionDotInfoBadgeStyle";

        public bool IsRomDirectoryPresent => !string.IsNullOrWhiteSpace(RomDirectory) && Directory.Exists(RomDirectory);

        public Microsoft.UI.Xaml.Style? RomValidationBadgeStyle
        {
            get
            {
                if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(RomValidationBadgeStyleKey ?? "AttentionDotInfoBadgeStyle", out var style) && style is Microsoft.UI.Xaml.Style s)
                {
                    return s;
                }
                return null;
            }
        }

        private bool _loadingDriveSounds;

        [ObservableProperty]
        public partial bool EnableDriveSeekSounds { get; set; } = false;

        partial void OnEnableDriveSeekSoundsChanged(bool value)
        {
            if (_loadingDriveSounds) return;
            _configService.SaveDriveSeekSounds(value);
            Services.DriveSoundService.Enabled = value;
        }

        public void LoadRomConfiguration()
        {
            _loadingDriveSounds = true;
            EnableDriveSeekSounds = _configService.GetDriveSeekSounds();
            _loadingDriveSounds = false;
            RomDirectory = _configService.GetRomDirectory();
            ValidateRomDirectory(RomDirectory);

            // The engine only learns the ROM directory when a VM starts (EmuEngineService.StartEmulationAsync).
            // Opening Settings on a machine that has never been started leaves it unset, so every ROM-dependent
            // device (most real video/sound/network cards) silently reports "unavailable" and vanishes from every
            // dropdown here, even though the files are present. Registering it as soon as Settings opens fixes that;
            // the native side ignores a repeat call with the same path, so calling it again when the VM later starts is harmless.
            if (!string.IsNullOrWhiteSpace(RomDirectory))
                Box86_WinUI.Interop.Box86CoreNative.EmuSetRomPath(RomDirectory);
        }

        public void ValidateRomDirectory(string? path)
        {
            var validation = _configService.ValidateRomDirectory(path);
            IsRomPathConfigured = validation.IsConfigured && validation.IsValid;
            IsRomPathValid = validation.IsValid;
            RomValidationMessage = validation.Message;
            RomValidationBadgeText = validation.BadgeText;
            RomValidationBadgeStyleKey = validation.BadgeStyleKey;
            OnPropertyChanged(nameof(RomValidationBadgeStyle));
            OnPropertyChanged(nameof(IsRomDirectoryPresent));
            OnPropertyChanged(nameof(IsRomPathMissing));
            OnPropertyChanged(nameof(IsRomPathNotConfigured));
        }

        public void SetRomDirectory(string path)
        {
            RomDirectory = path;
            _configService.SaveRomDirectory(path);
            ValidateRomDirectory(path);
        }

        [RelayCommand]
        public async Task BrowseRomDirectoryAsync()
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FolderPicker
                {
                    SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder
                };
                picker.FileTypeFilter.Add("*");

                IntPtr hwnd = App.MainWindowHandle;
                if (hwnd != IntPtr.Zero)
                {
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                }

                var folder = await picker.PickSingleFolderAsync();
                if (folder != null)
                {
                    SetRomDirectory(folder.Path);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Error picking ROM folder: {ex.Message}");
            }
        }

        [RelayCommand]
        public void OpenRomDirectoryInExplorer()
        {
            if (IsRomDirectoryPresent)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = RomDirectory,
                    UseShellExecute = true
                });
            }
        }

        // ==========================================
        // 1. Machine Category
        // ==========================================
        [ObservableProperty]
        public partial int MachineSubTabIndex { get; set; } = 0;

        public bool IsMachineSubTab0 => MachineSubTabIndex == 0;
        public bool IsMachineSubTab1 => MachineSubTabIndex == 1;
        public bool IsMachineSubTab2 => MachineSubTabIndex == 2;

        partial void OnMachineSubTabIndexChanged(int value)
        {
            OnPropertyChanged(nameof(IsMachineSubTab0));
            OnPropertyChanged(nameof(IsMachineSubTab1));
            OnPropertyChanged(nameof(IsMachineSubTab2));
        }

        public List<string> MachineTypes => HardwareCatalog.MachineTypes;

        [ObservableProperty]
        public partial string SelectedMachineType { get; set; } = "[1997] Slot 1";

        public List<string> AvailableMachines
        {
            get
            {
                if (HardwareCatalog.MachinesByType.TryGetValue(SelectedMachineType, out var list))
                    return list.Select(m => m.Name).ToList();
                return new List<string> { VM.MachineModel };
            }
        }

        partial void OnSelectedMachineTypeChanged(string value)
        {
            OnPropertyChanged(nameof(AvailableMachines));
            if (!AvailableMachines.Contains(SelectedMachine))
            {
                SelectedMachine = AvailableMachines.FirstOrDefault() ?? value;
            }

            OnPropertyChanged(nameof(AvailableCpus));
            OnPropertyChanged(nameof(CpuTypes));
            if (!AvailableCpus.Contains(SelectedCpuType))
            {
                SelectedCpuType = AvailableCpus.FirstOrDefault() ?? "Intel Pentium II (Deschutes)";
            }
        }

        [ObservableProperty]
        public partial string SelectedMachine { get; set; } = "[i440BX] ASUS P2B-LS";

        [ObservableProperty]
        public partial double MinRamMb { get; set; } = 8;

        [ObservableProperty]
        public partial double MaxRamMb { get; set; } = 1024;

        partial void OnSelectedMachineChanged(string value)
        {
            RefreshDeviceLists();
            if (HardwareCatalog.MachinesByType.TryGetValue(SelectedMachineType, out var list))
            {
                var match = list.FirstOrDefault(m => m.Name == value);
                if (match != null)
                {
                    MinRamMb = Math.Max(1, match.MinRamKb / 1024);
                    MaxRamMb = Math.Max(MinRamMb, match.MaxRamKb / 1024);
                    if (MemoryMb < MinRamMb)
                        MemoryMb = MinRamMb;
                    else if (MemoryMb > MaxRamMb && MaxRamMb > 0)
                        MemoryMb = MaxRamMb;
                }
            }

            OnPropertyChanged(nameof(AvailableCpus));
            OnPropertyChanged(nameof(CpuTypes));
            if (!AvailableCpus.Contains(SelectedCpuType))
            {
                SelectedCpuType = AvailableCpus.FirstOrDefault() ?? "Intel Pentium II (Deschutes)";
            }
            RefreshFrequencies();
        }

        [ObservableProperty]
        public partial double MemoryMb { get; set; } = 128;

        [ObservableProperty]
        public partial string TimeSyncMode { get; set; } = "Local";

        public bool IsTimeSyncDisabled
        {
            get => TimeSyncMode == "Disabled";
            set { if (value) TimeSyncMode = "Disabled"; }
        }

        public bool IsTimeSyncLocal
        {
            get => TimeSyncMode == "Local";
            set { if (value) TimeSyncMode = "Local"; }
        }

        public bool IsTimeSyncUtc
        {
            get => TimeSyncMode == "Utc";
            set { if (value) TimeSyncMode = "Utc"; }
        }

        public IReadOnlyList<string> TimeSyncOptions { get; } = new[]
        {
            "Disabled",
            "Enabled (Local Time)",
            "Enabled (UTC)"
        };

        public string SelectedTimeSyncOption
        {
            get => TimeSyncMode switch
            {
                "Disabled" => "Disabled",
                "Utc" => "Enabled (UTC)",
                _ => "Enabled (Local Time)"
            };
            set
            {
                if (value == "Disabled") TimeSyncMode = "Disabled";
                else if (value == "Enabled (UTC)") TimeSyncMode = "Utc";
                else TimeSyncMode = "Local";
                OnPropertyChanged();
            }
        }

        partial void OnTimeSyncModeChanged(string value)
        {
            OnPropertyChanged(nameof(IsTimeSyncDisabled));
            OnPropertyChanged(nameof(IsTimeSyncLocal));
            OnPropertyChanged(nameof(IsTimeSyncUtc));
            OnPropertyChanged(nameof(SelectedTimeSyncOption));
        }

        // Processor Subtab
        /// <summary>Only the CPU families the engine accepts on the selected board (socket, bus speed, voltage, multiplier).</summary>
        public List<string> AvailableCpus
        {
            get
            {
                var machine = Services.Cfg86Resolver.Machine(SelectedMachine);
                if (machine != null)
                {
                    var families = Services.Cfg86Resolver.EligibleFamilies(machine);
                    if (families.Count > 0) return families.Select(f => f.Label).ToList();
                }
                return HardwareCatalog.GetCompatibleCpus(SelectedMachineType, SelectedMachine).Select(c => c.Name).ToList();
            }
        }

        // Alias for compatibility
        public List<string> CpuTypes => AvailableCpus;

        [ObservableProperty]
        public partial string SelectedCpuType { get; set; } = "Intel Pentium II (Deschutes)";

        // Speed labels -> the exact engine step (cpu_speed in Hz + cpu_multi). No freeform entry is possible.
        private readonly Dictionary<string, Models.CpuStep> _stepByLabel = new();

        public List<string> AvailableFrequencies
        {
            get
            {
                _stepByLabel.Clear();
                var machine = Services.Cfg86Resolver.Machine(SelectedMachine);
                if (machine != null
                    && Services.Cfg86Resolver.Cpu(machine, SelectedCpuType, null, out var famKey, out _, out _))
                {
                    var labels = new List<string>();
                    foreach (var step in Services.Cfg86Resolver.EligibleSteps(machine, famKey))
                    {
                        string label = Services.Cfg86Resolver.StepLabel(step);
                        if (_stepByLabel.TryAdd(label, step)) labels.Add(label);
                    }
                    if (labels.Count > 0) return labels;
                }

                var match = HardwareCatalog.CpuList.FirstOrDefault(c => c.Name == SelectedCpuType);
                return match?.Frequencies ?? new List<string>();
            }
        }

        /// <summary>The catalog step behind the selected speed label (null if none is selected).</summary>
        private Models.CpuStep? SelectedStep => _stepByLabel.TryGetValue(SelectedFrequency ?? string.Empty, out var s) ? s : null;

        [ObservableProperty]
        public partial string SelectedFrequency { get; set; } = "";

        public List<string> AvailableFpus
        {
            get
            {
                string wanted = Services.Cfg86Resolver.Norm(System.Text.RegularExpressions.Regex.Replace(SelectedCpuType ?? string.Empty, @"\s*\([a-z0-9_]+\)$", ""));
                var match = HardwareCatalog.CpuList.FirstOrDefault(c => c.Name == SelectedCpuType)
                            ?? HardwareCatalog.CpuList.FirstOrDefault(c => Services.Cfg86Resolver.Norm(c.Name) == wanted);
                return match?.SupportedFpus ?? new List<string> { "Internal" };
            }
        }

        [ObservableProperty]
        public partial string SelectedFpu { get; set; } = "Internal";

        public List<string> WaitStates => HardwareCatalog.WaitStatesOptions;

        [ObservableProperty]
        public partial string SelectedWaitState { get; set; } = "Default";

        [ObservableProperty]
        public partial bool SoftfloatFpu { get; set; } = false;

        partial void OnSelectedCpuTypeChanged(string value)
        {
            RefreshFrequencies();
            OnPropertyChanged(nameof(AvailableFpus));

            if (!AvailableFpus.Contains(SelectedFpu))
                SelectedFpu = AvailableFpus.FirstOrDefault() ?? "Internal";
        }

        /// <summary>Repopulates the speed list for the current machine + CPU family and keeps a valid selection.</summary>
        private void RefreshFrequencies()
        {
            var list = AvailableFrequencies;
            OnPropertyChanged(nameof(AvailableFrequencies));
            if (!list.Contains(SelectedFrequency))
                SelectedFrequency = list.FirstOrDefault() ?? string.Empty;
        }

        // Performance Subtab
        public List<string> PitModes => HardwareCatalog.PitModes;

        [ObservableProperty]
        public partial string SelectedPitMode { get; set; } = "Auto";

        [ObservableProperty]
        public partial bool Use486InterpreterFor286_386 { get; set; } = false;

        [ObservableProperty]
        public partial bool DynamicRecompiler { get; set; } = true;

        [ObservableProperty]
        public partial bool AccurateFpuEnvironment { get; set; } = true;

        [ObservableProperty]
        public partial string CpuFrameSize { get; set; } = "Smaller";

        public bool IsCpuFrameSizeLarger
        {
            get => CpuFrameSize == "Larger";
            set { if (value) CpuFrameSize = "Larger"; }
        }

        public bool IsCpuFrameSizeSmaller
        {
            get => CpuFrameSize == "Smaller";
            set { if (value) CpuFrameSize = "Smaller"; }
        }

        partial void OnCpuFrameSizeChanged(string value)
        {
            OnPropertyChanged(nameof(IsCpuFrameSizeLarger));
            OnPropertyChanged(nameof(IsCpuFrameSizeSmaller));
        }

        // ==========================================
        // 2. Display Category
        // ==========================================
        [ObservableProperty]
        public partial int DisplaySubTabIndex { get; set; } = 0;

        public bool IsDisplaySubTab0 => DisplaySubTabIndex == 0;
        public bool IsDisplaySubTab1 => DisplaySubTabIndex == 1;

        partial void OnDisplaySubTabIndexChanged(int value)
        {
            OnPropertyChanged(nameof(IsDisplaySubTab0));
            OnPropertyChanged(nameof(IsDisplaySubTab1));
        }

        public List<string> VideoCards { get; private set; } = HardwareCatalog.VideoCards;

        /// <summary>
        /// VideoCards sorted newest-bus-first (AGP, PCI, VLB, ISA, MCA - see VideoBusGrouping in
        /// Models/VideoAdapterGroup.cs) for the "Video:" combo box, each label still carrying its own
        /// "[PCI]"/"[ISA]" bus tag. Purely a presentation view over VideoCards, which stays the source
        /// of truth for Keep()/DeviceLabel() lookups and for what gets written to 86box.cfg.
        ///
        /// This used to be a real grouped ComboBox (CollectionViewSource with IsSourceGrouped="True"
        /// and section headers), but that combination threw two different unrecoverable native
        /// exceptions back to back on this WinAppSDK version - COMException(0x80070490, "Element not
        /// found"), then, after changing the selection binding to work around that, ArgumentException
        /// ("The parameter is incorrect") - both stowed exceptions that bypass App.xaml.cs's
        /// UnhandledException handler entirely. A flat, pre-sorted list with a plain SelectedItem
        /// binding (what every other combo box in this dialog already uses) has none of that risk.
        /// </summary>
        public List<string> VideoAdapterOptions => Models.VideoBusGrouping.Group(VideoCards)
            .SelectMany(g => g.Select(e => e.Label)).ToList();

        // The lists of selectable devices come from the engine, per machine, exactly as the original dialog builds them:
        // ROMs present (available) and valid for the machine's buses, with the "[PCI] Name" labels.
        private bool _refreshingLists;

        private static List<string> Labels(DeviceListKind kind, string? machine, int slot = 0, string? primary = null, IReadOnlyList<string>? fallback = null)
        {
            var list = EngineDeviceLists.Get(kind, machine, slot, primary);
            return list.Count > 0 ? list.Select(x => x.Label).ToList() : (fallback?.ToList() ?? new List<string>());
        }

        /// <summary>Re-reads every device list for the selected machine and keeps a selection that is still allowed.</summary>
        // False while the saved configuration is being loaded; a machine change after that is the user's.
        private bool _settingsLoaded;

        public void RefreshDeviceLists()
        {
            RefreshMemExpList();
            if (_refreshingLists) return;
            _refreshingLists = true;
            try
            {
                string? machine = Services.Cfg86Resolver.Machine(SelectedMachine)?.Info.InternalName;
                if (machine == null) return;

                KeyboardTypes = Labels(DeviceListKind.Keyboard, machine, 0, null, KeyboardTypes);
                MouseTypes = Labels(DeviceListKind.Mouse, machine, 0, null, MouseTypes);
                TabletTypes = Labels(DeviceListKind.Tablet, machine, 0, null, TabletTypes);
                OnPropertyChanged(nameof(KeyboardTypes));
                OnPropertyChanged(nameof(MouseTypes));
                OnPropertyChanged(nameof(TabletTypes));
                SelectedKeyboardType = Keep("input", SelectedKeyboardType, KeyboardTypes);
                SelectedMouseType = Keep("input", SelectedMouseType, MouseTypes);
                SelectedTabletType = Keep("input", SelectedTabletType, TabletTypes);
                VideoCards = Labels(DeviceListKind.Video, machine, 0, null, HardwareCatalog.VideoCards);
                RefreshSecondaryVideo(machine);
                SoundCards = Labels(DeviceListKind.Sound, machine, 0, null, HardwareCatalog.SoundCards);
                SoundCardsExtra = Labels(DeviceListKind.Sound, machine, 1, null, HardwareCatalog.SoundCards);
                NetworkAdapters = Labels(DeviceListKind.Network, machine, 0, null, HardwareCatalog.NetworkAdapters);
                NetworkAdaptersExtra = Labels(DeviceListKind.Network, machine, 1, null, HardwareCatalog.NetworkAdapters);
                HdControllers = Labels(DeviceListKind.HardDiskController, machine, 0, null, HardwareCatalog.HdControllers);
                HdControllersExtra = Labels(DeviceListKind.HardDiskController, machine, 1, null, HardwareCatalog.HdControllers);
                ScsiControllers = Labels(DeviceListKind.Scsi, machine, 0, null, HardwareCatalog.ScsiControllers);
                FloppyControllers = Labels(DeviceListKind.FloppyController, machine, 0, null, HardwareCatalog.FloppyControllers);
                CdromControllers = Labels(DeviceListKind.CdromInterface, machine, 0, null, HardwareCatalog.CdromControllers);

                foreach (var name in new[] { nameof(VideoCards), nameof(VideoAdapterOptions), nameof(SoundCards), nameof(SoundCardsExtra), nameof(NetworkAdapters),
                                             nameof(NetworkAdaptersExtra), nameof(HdControllers), nameof(HdControllersExtra),
                                             nameof(ScsiControllers), nameof(FloppyControllers), nameof(CdromControllers) })
                    OnPropertyChanged(name);

                // Keep what is selected when the new list still offers it (matched by engine identity); otherwise, for
                // the primary card, land on the highest-priority (newest-bus) compatible card this board offers rather
                // than silently falling back to "None" - a Socket 7 board should default to its best PCI/AGP option.
                SelectedVideoCard = Keep("video", SelectedVideoCard, VideoCards, Models.VideoBusGrouping.HighestPriorityCard);
                SelectedSecondaryVideoCard = Keep("video", SelectedSecondaryVideoCard, SecondaryVideoCards);
                SelectedSoundCard1 = Keep("sound", SelectedSoundCard1, SoundCards);
                SelectedSoundCard2 = Keep("sound", SelectedSoundCard2, SoundCardsExtra);
                SelectedSoundCard3 = Keep("sound", SelectedSoundCard3, SoundCardsExtra);
                SelectedSoundCard4 = Keep("sound", SelectedSoundCard4, SoundCardsExtra);
                SelectedNetworkAdapter1 = Keep("network", SelectedNetworkAdapter1, NetworkAdapters);
                SelectedNetworkAdapter2 = Keep("network", SelectedNetworkAdapter2, NetworkAdaptersExtra);
                SelectedNetworkAdapter3 = Keep("network", SelectedNetworkAdapter3, NetworkAdaptersExtra);
                SelectedNetworkAdapter4 = Keep("network", SelectedNetworkAdapter4, NetworkAdaptersExtra);
                SelectedFloppyController = Keep("fdc", SelectedFloppyController, FloppyControllers);
                SelectedCdromController = Keep("cdrom_if", SelectedCdromController, CdromControllers);
                SelectedHdController1 = Keep("hdc", SelectedHdController1, HdControllers);
                SelectedHdController2 = Keep("hdc", SelectedHdController2, HdControllersExtra);
                SelectedHdController3 = Keep("hdc", SelectedHdController3, HdControllersExtra);
                SelectedHdController4 = Keep("hdc", SelectedHdController4, HdControllersExtra);
                SelectedScsiController1 = Keep("scsi", SelectedScsiController1, ScsiControllers);
                SelectedScsiController2 = Keep("scsi", SelectedScsiController2, ScsiControllers);
                SelectedScsiController3 = Keep("scsi", SelectedScsiController3, ScsiControllers);
                SelectedScsiController4 = Keep("scsi", SelectedScsiController4, ScsiControllers);

                // A board picked in the dialog brings its own built-in floppy and IDE controllers, as in the original.
                // Without this a "None" left over from a previous board (one without them) was kept on the new one,
                // which hides the floppy drive ("Invalid boot device") and the hard disks from the guest.
                if (_settingsLoaded)
                {
                    var internalFdc = FloppyControllers.FirstOrDefault(l => l.StartsWith("Internal", StringComparison.OrdinalIgnoreCase));
                    if (internalFdc != null) SelectedFloppyController = internalFdc;
                    var internalHdc = HdControllers.FirstOrDefault(l => l.StartsWith("Internal", StringComparison.OrdinalIgnoreCase));
                    if (internalHdc != null) SelectedHdController1 = internalHdc;
                }
            }
            finally { _refreshingLists = false; }
        }

        private void RefreshSecondaryVideo(string machine)
        {
            string? primary = Services.Cfg86Resolver.Device("video", SelectedVideoCard);
            var list = Labels(DeviceListKind.SecondaryVideo, machine, 0, primary, HardwareCatalog.SecondaryVideoCards);
            list.Insert(0, "None");
            SecondaryVideoCards = list;
            OnPropertyChanged(nameof(SecondaryVideoCards));
        }

        /// <summary>
        /// The list entry for the same engine device as <paramref name="current"/>; otherwise
        /// <paramref name="fallback"/>'s pick if it returns one, else "None" / the first entry.
        /// </summary>
        private static string Keep(string kind, string current, List<string> list, Func<List<string>, string?>? fallback = null)
        {
            if (list.Count == 0) return current;
            if (list.Contains(current)) return current;

            string? internalName = Services.Cfg86Resolver.Device(kind, current);
            if (internalName != null)
                foreach (var item in list)
                    if (Services.Cfg86Resolver.Device(kind, item) == internalName) return item;

            return fallback?.Invoke(list) ?? list.FirstOrDefault(i => i == "None") ?? list[0];
        }

        [ObservableProperty]
        public partial string SelectedVideoCard { get; set; } = "S3 Trio64V+ (PCI)";

        partial void OnSelectedVideoCardChanged(string value)
        {
            if (_refreshingLists) return;
            string? machine = Services.Cfg86Resolver.Machine(SelectedMachine)?.Info.InternalName;
            if (machine == null) return;
            RefreshSecondaryVideo(machine);
            SelectedSecondaryVideoCard = Keep("video", SelectedSecondaryVideoCard, SecondaryVideoCards);
        }

        public List<string> SecondaryVideoCards { get; private set; } = HardwareCatalog.SecondaryVideoCards;

        [ObservableProperty]
        public partial string SelectedSecondaryVideoCard { get; set; } = "None";

        [ObservableProperty]
        public partial bool EnableVoodoo { get; set; } = false;

        [ObservableProperty]
        public partial bool Enable8514 { get; set; } = false;

        [ObservableProperty]
        public partial bool EnableXga { get; set; } = false;

        [ObservableProperty]
        public partial bool EnableDa2 { get; set; } = false;

        // CRT post-process filter for the viewport itself - a WinUI rendering preference, not an
        // authentic 86box.cfg key, so it is stored in the manager ini (see ConfigService) and
        // applies to every machine the same way rather than being written into this VM's config.
        private static readonly List<string> DisplayFilterOptionsList = new()
        {
            "None (Sharp)", "Scanlines (Flat)", "Authentic CRT (Curved Glass)"
        };
        public List<string> DisplayFilterOptions => DisplayFilterOptionsList;

        [ObservableProperty]
        public partial string SelectedDisplayFilter { get; set; } = DisplayFilterOptionsList[0];

        partial void OnSelectedDisplayFilterChanged(string value)
        {
            if (_loadingDisplayFilter) return;
            int mode = Math.Max(0, DisplayFilterOptionsList.IndexOf(value));
            _configService.SaveCrtFilterMode(mode);
            Box86_WinUI.Interop.Box86CoreNative.EmuSetCrtFilterMode(mode);
        }

        /// <summary>0 .. 15 (%). Authentic CRT mode only, but always adjustable/persisted.</summary>
        [ObservableProperty]
        public partial double ScreenCurvaturePercent { get; set; } = 6.0;

        partial void OnScreenCurvaturePercentChanged(double value)
        {
            if (_loadingDisplayFilter) return;
            _configService.SaveScreenCurvature(value / 100.0);
            Box86_WinUI.Interop.Box86CoreNative.EmuSetCrtCurvature((float) (value / 100.0));
        }

        /// <summary>0 .. 100 (%). Authentic CRT mode only, but always adjustable/persisted.</summary>
        [ObservableProperty]
        public partial double PhosphorBloomPercent { get; set; } = 35.0;

        partial void OnPhosphorBloomPercentChanged(double value)
        {
            if (_loadingDisplayFilter) return;
            _configService.SavePhosphorBloom(value / 100.0);
            Box86_WinUI.Interop.Box86CoreNative.EmuSetCrtBloom((float) (value / 100.0));
        }

        private bool _loadingDisplayFilter;

        private void LoadDisplayFilterConfiguration()
        {
            _loadingDisplayFilter = true;
            int mode = _configService.GetCrtFilterMode();
            SelectedDisplayFilter = mode >= 0 && mode < DisplayFilterOptionsList.Count ? DisplayFilterOptionsList[mode] : DisplayFilterOptionsList[0];
            ScreenCurvaturePercent = _configService.GetScreenCurvature() * 100.0;
            PhosphorBloomPercent = _configService.GetPhosphorBloom() * 100.0;
            _loadingDisplayFilter = false;
        }

        public List<string> ScreenTypes => HardwareCatalog.ScreenTypes;
        [ObservableProperty]
        public partial string SelectedScreenType { get; set; } = "RGB Color";

        public List<string> GrayscaleConversions => HardwareCatalog.GrayscaleConversions;
        [ObservableProperty]
        public partial string SelectedGrayscaleConversion { get; set; } = "BT601 (NTSC/PAL)";

        [ObservableProperty]
        public partial string MonitorEdidMode { get; set; } = "Default";

        public bool IsMonitorEdidDefault
        {
            get => MonitorEdidMode == "Default";
            set { if (value) MonitorEdidMode = "Default"; }
        }

        public bool IsMonitorEdidCustom
        {
            get => MonitorEdidMode == "Custom";
            set { if (value) MonitorEdidMode = "Custom"; }
        }

        partial void OnMonitorEdidModeChanged(string value)
        {
            OnPropertyChanged(nameof(IsMonitorEdidDefault));
            OnPropertyChanged(nameof(IsMonitorEdidCustom));
        }

        [ObservableProperty]
        public partial string CustomEdidPath { get; set; } = "";

        /// <summary>Standard display profile chosen in the EDID dialog (null = default or a user file).</summary>
        public string? EdidProfileId { get; set; }

        /// <summary>Device settings by "section/key"; edited by the device configuration dialogs.</summary>
        public Dictionary<string, string> DeviceSettings { get; } = new();

        /// <summary>Stored value of a device option in 86box.cfg (null when the key is absent).</summary>
        public string? ReadDeviceSetting(string section, string key)
            => _config.HasKey(section, key) ? _config.GetString(section, key, string.Empty) : null;

        /// <summary>
        /// Which engine device a Configure button belongs to: the device currently chosen for that slot, the name the
        /// engine uses for its config section, and that section (with " #N" for a second identical device).
        /// </summary>
        public (string DeviceName, string Section)? ResolveConfigDevice(string tag)
        {
            string? kind = null, label = null;
            int priorSame = 0;

            switch (tag)
            {
                case "Primary Video": kind = "video"; label = SelectedVideoCard; break;
                case "Secondary Video": kind = "video"; label = SelectedSecondaryVideoCard; break;
                case "3dfx Voodoo Graphics": return ("3Dfx Voodoo Graphics", "3Dfx Voodoo Graphics");
                case "Mouse": kind = "input"; label = SelectedMouseType; break;
                case "Keyboard": kind = "input"; label = SelectedKeyboardType; break;
                case "MIDI Out": kind = "midi_out"; label = SelectedMidiOutDevice; break;
                case "MIDI In": kind = "midi_in"; label = SelectedMidiInDevice; break;
                case "MPU-401": return ("Roland MPU-IPC-T", "Roland MPU-IPC-T");
                case "ISA RTC": kind = "isartc"; label = SelectedIsaRtc; break;
                case "Unit Tester": return ("86Box Unit Tester", "86Box Unit Tester");
                default:
                    var slotMatch = System.Text.RegularExpressions.Regex.Match(tag, @"^(Memory Expansion|ISA ROM) Card (\d)$");
                    if (slotMatch.Success)
                    {
                        int slot = int.Parse(slotMatch.Groups[2].Value) - 1;
                        bool rom = slotMatch.Groups[1].Value == "ISA ROM";
                        var slotList = rom ? new[] { SelectedIsaRom1, SelectedIsaRom2, SelectedIsaRom3, SelectedIsaRom4 }
                                           : new[] { SelectedMemExp1, SelectedMemExp2, SelectedMemExp3, SelectedMemExp4 };
                        kind = rom ? "isarom" : (MemExpIsMca ? "mcamem" : "isamem");
                        label = slotList[slot];
                        for (int i = 0; i < slot; i++)
                            if (Services.Cfg86Resolver.Device(kind, slotList[i]) == Services.Cfg86Resolver.Device(kind, label)) priorSame++;
                        break;
                    }
                    var sound = new[] { SelectedSoundCard1, SelectedSoundCard2, SelectedSoundCard3, SelectedSoundCard4 };
                    var net = new[] { SelectedNetworkAdapter1, SelectedNetworkAdapter2, SelectedNetworkAdapter3, SelectedNetworkAdapter4 };
                    var m = System.Text.RegularExpressions.Regex.Match(tag, @"^(Sound Card|Network Adapter) (\d)$");
                    if (m.Success)
                    {
                        int n = int.Parse(m.Groups[2].Value) - 1;
                        var list = m.Groups[1].Value == "Sound Card" ? sound : net;
                        kind = m.Groups[1].Value == "Sound Card" ? "sound" : "network";
                        label = list[n];
                        for (int i = 0; i < n; i++)
                            if (Services.Cfg86Resolver.Device(kind, list[i]) == Services.Cfg86Resolver.Device(kind, label)) priorSame++;
                    }
                    break;
            }

            if (kind == null || label == null) return null;
            string? internalName = Services.Cfg86Resolver.Device(kind, label);
            if (string.IsNullOrEmpty(internalName) || internalName == "none") return null;
            if (!AuthenticCatalog.Devices.TryGetValue(kind, out var devices)) return null;

            var dev = devices.FirstOrDefault(d => d.Internal == internalName);
            if (dev.Internal == null) return null;

            return (dev.Name, priorSame > 0 ? $"{dev.Name} #{priorSame + 1}" : dev.Name);
        }

        [ObservableProperty]
        public partial bool EnableOverscan { get; set; } = false;

        [ObservableProperty]
        public partial bool InvertDisplay { get; set; } = false;

        [ObservableProperty]
        public partial bool MonochromeContrast { get; set; } = false;

        public List<string> RendererBackends { get; } = new() { "Direct3D 11", "OpenGL 3.3 Core", "Vulkan", "Software" };

        [ObservableProperty]
        public partial string SelectedRendererBackend { get; set; } = "Direct3D 11";

        [ObservableProperty]
        public partial bool VsyncEnabled { get; set; } = true;

        [ObservableProperty]
        public partial bool AspectRatio43 { get; set; } = true;

        // ==========================================
        // 3. Input Devices Category
        // ==========================================
        public List<string> KeyboardTypes { get; private set; } = new() { "Standard 101-key Enhanced Keyboard", "Standard 104-key Windows Keyboard", "Standard 83-key XT Keyboard", "Standard 84-key AT Keyboard", "Standard PS/2 Keyboard" };

        [ObservableProperty]
        public partial string SelectedKeyboardType { get; set; } = "Standard 101-key Enhanced Keyboard";

        public List<string> MouseTypes { get; private set; } = HardwareCatalog.MouseTypes;

        [ObservableProperty]
        public partial string SelectedMouseType { get; set; } = "Standard PS/2 Mouse";

        public List<string> TabletTypes { get; private set; } = new() { "None", "Wacom Intuos / ArtZ II", "Genius EasyPen", "Kurta IS/ONE" };

        [ObservableProperty]
        public partial string SelectedTabletType { get; set; } = "None";

        public List<string> KeyboardLayouts { get; } = new() { "Standard US (101/104-key)", "UK English (102/105-key)", "German (102/105-key)", "French (AZERTY)", "Japanese (106-key)" };

        [ObservableProperty]
        public partial string SelectedKeyboardLayout { get; set; } = "Standard US (101/104-key)";

        public List<string> Joysticks { get; } = new() { "None", "2-axis, 2-button joystick", "2-axis, 4-button joystick", "4-axis, 4-button joystick", "CH Flightstick Pro", "Thrustmaster FCS" };

        [ObservableProperty]
        public partial string SelectedJoystick1 { get; set; } = "None";

        [ObservableProperty]
        public partial string SelectedJoystick2 { get; set; } = "None";

        // ==========================================
        // 4. Sound Category
        // ==========================================
        [ObservableProperty]
        public partial int SoundSubTabIndex { get; set; } = 0;

        public bool IsSoundSubTab0 => SoundSubTabIndex == 0;
        public bool IsSoundSubTab1 => SoundSubTabIndex == 1;

        partial void OnSoundSubTabIndexChanged(int value)
        {
            OnPropertyChanged(nameof(IsSoundSubTab0));
            OnPropertyChanged(nameof(IsSoundSubTab1));
        }

        public List<string> SoundCards { get; private set; } = HardwareCatalog.SoundCards;
        /// <summary>Sound slots 2-4: the same cards without the "internal" entry.</summary>
        public List<string> SoundCardsExtra { get; private set; } = HardwareCatalog.SoundCards;

        [ObservableProperty]
        public partial string SelectedSoundCard1 { get; set; } = "Sound Blaster 16 (CT1740/CT2230)";

        [ObservableProperty]
        public partial string SelectedSoundCard2 { get; set; } = "None";

        [ObservableProperty]
        public partial string SelectedSoundCard3 { get; set; } = "None";

        [ObservableProperty]
        public partial string SelectedSoundCard4 { get; set; } = "None";

        public List<string> FmDrivers => HardwareCatalog.FmDrivers;
        [ObservableProperty]
        public partial string SelectedFmDriver { get; set; } = "Nuked (more accurate)";

        [ObservableProperty]
        public partial bool UseFloat32Sound { get; set; } = false;

        public List<string> AudioOutputDevices => HardwareCatalog.AudioOutputDevices;
        [ObservableProperty]
        public partial string SelectedAudioOutputDevice { get; set; } = "Default Audio Endpoint";

        public List<string> AudioSampleRates => HardwareCatalog.AudioSampleRates;
        [ObservableProperty]
        public partial string SelectedAudioSampleRate { get; set; } = "44100 Hz";

        /// <summary>MIDI output devices, straight from the engine build's midi_out_devices[] (midi.c): only what was compiled in and is available.</summary>
        public List<string> MidiOutDevices { get; private set; } = MidiLabels(DeviceListKind.MidiOut);

        /// <summary>MIDI input devices, from the engine's midi_in_devices[] (None and the host MIDI input).</summary>
        public List<string> MidiInDevices { get; private set; } = MidiLabels(DeviceListKind.MidiIn);

        private static List<string> MidiLabels(DeviceListKind kind)
        {
            // The MIDI tables do not depend on the machine; EngineDeviceLists only needs a valid one to answer.
            var list = EngineDeviceLists.Get(kind, "ibmpc").Select(x => x.Label).ToList();
            return list.Count > 0 ? list : new List<string> { "None" };
        }

        [ObservableProperty]
        public partial string SelectedMidiDevice { get; set; } = "None";

        [ObservableProperty]
        public partial string SelectedMidiOutDevice { get; set; } = "None";

        [ObservableProperty]
        public partial string SelectedMidiInDevice { get; set; } = "None";

        [ObservableProperty]
        public partial bool StandaloneMpu401 { get; set; } = false;

        // ==========================================
        // 5. Network Category
        // ==========================================
        [ObservableProperty]
        public partial int NetworkSubTabIndex { get; set; } = 0;

        public bool IsNetworkSubTab0 => NetworkSubTabIndex == 0;
        public bool IsNetworkSubTab1 => NetworkSubTabIndex == 1;
        public bool IsNetworkSubTab2 => NetworkSubTabIndex == 2;
        public bool IsNetworkSubTab3 => NetworkSubTabIndex == 3;

        partial void OnNetworkSubTabIndexChanged(int value)
        {
            OnPropertyChanged(nameof(IsNetworkSubTab0));
            OnPropertyChanged(nameof(IsNetworkSubTab1));
            OnPropertyChanged(nameof(IsNetworkSubTab2));
            OnPropertyChanged(nameof(IsNetworkSubTab3));
        }

        public List<string> NetworkAdapters { get; private set; } = HardwareCatalog.NetworkAdapters;
        public List<string> NetworkAdaptersExtra { get; private set; } = HardwareCatalog.NetworkAdapters;

        [ObservableProperty]
        public partial string SelectedNetworkAdapter1 { get; set; } = "Realtek RTL8029AS (PCI 10Mbps)";

        [ObservableProperty]
        public partial string SelectedNetworkAdapter2 { get; set; } = "None";

        [ObservableProperty]
        public partial string SelectedNetworkAdapter3 { get; set; } = "None";

        [ObservableProperty]
        public partial string SelectedNetworkAdapter4 { get; set; } = "None";


        [ObservableProperty]
        public partial string SelectedNetworkMode1 { get; set; } = "SLiRP";

        [ObservableProperty]
        public partial string SelectedNetworkMode2 { get; set; } = "Null Driver";

        [ObservableProperty]
        public partial string SelectedNetworkMode3 { get; set; } = "Null Driver";

        [ObservableProperty]
        public partial string SelectedNetworkMode4 { get; set; } = "Null Driver";

        [ObservableProperty]
        public partial string Net1Mac { get; set; } = "52:54:00:12:34:56";

        [ObservableProperty]
        public partial string Net2Mac { get; set; } = "52:54:00:12:34:57";

        [ObservableProperty]
        public partial string Net3Mac { get; set; } = "52:54:00:12:34:58";

        [ObservableProperty]
        public partial string Net4Mac { get; set; } = "52:54:00:12:34:59";

        /// <summary>Host adapters for the PCap mode, as the engine reports them (descriptions).</summary>
        public List<string> NetworkInterfaces { get; private set; } = new();

        // description <-> device name of the host adapters (net_NN_host_device stores the device name)
        private List<(string Device, string Description)> _hostDevices = new();

        /// <summary>"Null Driver", "SLiRP", "PCap" (only when the host has a capture library) and "Local Switch", like the original.</summary>
        public List<string> NetworkModes { get; private set; } = new() { "Null Driver", "SLiRP", "Local Switch" };

        public NetCardOptions NetOptions1 { get; } = new();
        public NetCardOptions NetOptions2 { get; } = new();
        public NetCardOptions NetOptions3 { get; } = new();
        public NetCardOptions NetOptions4 { get; } = new();

        private NetCardOptions[] NetOptionsAll => new[] { NetOptions1, NetOptions2, NetOptions3, NetOptions4 };

        private void LoadNetworkHostDevices()
        {
            _hostDevices = Services.EngineDeviceLists.NetworkHostDevices();
            NetworkInterfaces = _hostDevices.Select(d => d.Description).ToList();

            var modes = new List<string> { "Null Driver", "SLiRP" };
            if (_hostDevices.Count > 1) modes.Add("PCap");
            modes.Add("Local Switch");
            NetworkModes = modes;
            OnPropertyChanged(nameof(NetworkInterfaces));
            OnPropertyChanged(nameof(NetworkModes));
        }

        private string HostDeviceFor(string description)
            => _hostDevices.FirstOrDefault(d => d.Description == description).Device ?? string.Empty;

        private string HostDescriptionFor(string device)
            => _hostDevices.FirstOrDefault(d => d.Device == device).Description ?? (NetworkInterfaces.FirstOrDefault() ?? string.Empty);

        /// <summary>Shows only the option rows the chosen mode needs (and none without a card), as the original does.</summary>
        private void UpdateNetOptions()
        {
            var modes = new[] { SelectedNetworkMode1, SelectedNetworkMode2, SelectedNetworkMode3, SelectedNetworkMode4 };
            var cards = new[] { SelectedNetworkAdapter1, SelectedNetworkAdapter2, SelectedNetworkAdapter3, SelectedNetworkAdapter4 };
            var opts = NetOptionsAll;
            for (int i = 0; i < 4; i++)
                opts[i].Update(modes[i], !string.IsNullOrEmpty(cards[i]) && cards[i] != "None");
        }

        partial void OnSelectedNetworkAdapter1Changed(string value) => UpdateNetOptions();
        partial void OnSelectedNetworkAdapter2Changed(string value) => UpdateNetOptions();
        partial void OnSelectedNetworkAdapter3Changed(string value) => UpdateNetOptions();
        partial void OnSelectedNetworkAdapter4Changed(string value) => UpdateNetOptions();
        partial void OnSelectedNetworkMode2Changed(string value) => UpdateNetOptions();
        partial void OnSelectedNetworkMode3Changed(string value) => UpdateNetOptions();
        partial void OnSelectedNetworkMode4Changed(string value) => UpdateNetOptions();
        [ObservableProperty]
        public partial string SelectedNet1Interface { get; set; } = "Default / Any";
        [ObservableProperty]
        public partial string SelectedNet2Interface { get; set; } = "Default / Any";
        [ObservableProperty]
        public partial string SelectedNet3Interface { get; set; } = "Default / Any";
        [ObservableProperty]
        public partial string SelectedNet4Interface { get; set; } = "Default / Any";

        [ObservableProperty]
        public partial string LocalSwitchSecret { get; set; } = "";

        [ObservableProperty]
        public partial bool LocalSwitchPromiscuous { get; set; } = false;

        public bool IsLocalSwitchSelected => SelectedNetworkMode1 == "Local Switch";

        partial void OnSelectedNetworkMode1Changed(string value)
        {
            UpdateNetOptions();
            OnPropertyChanged(nameof(IsLocalSwitchSelected));
        }

        // ==========================================
        // 6. Ports (COM & LPT) Category
        // ==========================================
        [ObservableProperty]
        public partial int PortsSubTabIndex { get; set; } = 0;

        public bool IsPortsSubTab0 => PortsSubTabIndex == 0;
        public bool IsPortsSubTab1 => PortsSubTabIndex == 1;

        partial void OnPortsSubTabIndexChanged(int value)
        {
            OnPropertyChanged(nameof(IsPortsSubTab0));
            OnPropertyChanged(nameof(IsPortsSubTab1));
        }

        public List<string> LptEcpDmaOptions { get; } = new() { "None", "1", "3", "DMA 1", "DMA 3" };

        [ObservableProperty]
        public partial string SelectedLptEcpDma { get; set; } = "None";

        public List<string> LptDevices { get; } = DeviceNames("char_lpt");
        public List<string> ComDevices { get; } = DeviceNames("char_com");

        [ObservableProperty] public partial bool EnableLpt1 { get; set; } = true;
        [ObservableProperty] public partial bool EnableLpt2 { get; set; } = false;
        [ObservableProperty] public partial bool EnableLpt3 { get; set; } = false;
        [ObservableProperty] public partial bool EnableLpt4 { get; set; } = false;

        [ObservableProperty] public partial string SelectedLpt1Device { get; set; } = "None";
        [ObservableProperty] public partial string SelectedLpt2Device { get; set; } = "None";
        [ObservableProperty] public partial string SelectedLpt3Device { get; set; } = "None";
        [ObservableProperty] public partial string SelectedLpt4Device { get; set; } = "None";

        [ObservableProperty] public partial bool EnableCom1 { get; set; } = true;
        [ObservableProperty] public partial bool EnableCom2 { get; set; } = true;
        [ObservableProperty] public partial bool EnableCom3 { get; set; } = false;
        [ObservableProperty] public partial bool EnableCom4 { get; set; } = false;

        [ObservableProperty] public partial string SelectedCom1Device { get; set; } = "None";
        [ObservableProperty] public partial string SelectedCom2Device { get; set; } = "None";
        [ObservableProperty] public partial string SelectedCom3Device { get; set; } = "None";
        [ObservableProperty] public partial string SelectedCom4Device { get; set; } = "None";

        // ==========================================
        // 7. Storage Controllers Category
        // ==========================================
        [ObservableProperty]
        public partial int StorageSubTabIndex { get; set; } = 0;

        public bool IsStorageSubTab0 => StorageSubTabIndex == 0;
        public bool IsStorageSubTab1 => StorageSubTabIndex == 1;

        partial void OnStorageSubTabIndexChanged(int value)
        {
            OnPropertyChanged(nameof(IsStorageSubTab0));
            OnPropertyChanged(nameof(IsStorageSubTab1));
        }

        public List<string> HdControllers { get; private set; } = HardwareCatalog.HdControllers;
        public List<string> HdControllersExtra { get; private set; } = HardwareCatalog.HdControllers;
        public List<string> ScsiControllers { get; private set; } = HardwareCatalog.ScsiControllers;
        public List<string> CdromControllers { get; private set; } = HardwareCatalog.CdromControllers;
        public List<string> FloppyControllers { get; private set; } = HardwareCatalog.FloppyControllers;

        // General Subtab
        [ObservableProperty]
        public partial string SelectedFloppyController { get; set; } = "Internal controller";

        [ObservableProperty]
        public partial string SelectedCdromController { get; set; } = "None";

        [ObservableProperty]
        public partial string SelectedHdController1 { get; set; } = "Internal controller";

        [ObservableProperty]
        public partial string SelectedHdController2 { get; set; } = "None";

        [ObservableProperty]
        public partial string SelectedHdController3 { get; set; } = "None";

        [ObservableProperty]
        public partial string SelectedHdController4 { get; set; } = "None";

        public string SelectedHdController
        {
            get => SelectedHdController1;
            set => SelectedHdController1 = value;
        }

        [ObservableProperty]
        public partial bool EnableCassette { get; set; } = false;

        // SCSI Controllers Subtab
        [ObservableProperty]
        public partial string SelectedScsiController1 { get; set; } = "None";

        [ObservableProperty]
        public partial string SelectedScsiController2 { get; set; } = "None";

        [ObservableProperty]
        public partial string SelectedScsiController3 { get; set; } = "None";

        [ObservableProperty]
        public partial string SelectedScsiController4 { get; set; } = "None";

        // ==========================================
        // 8. Hard Disks Category
        // ==========================================
        public ObservableCollection<HardDiskEntry> HardDisks { get; } = new();

        [ObservableProperty]
        public partial HardDiskEntry? SelectedHardDisk { get; set; }

        public List<string> HddBuses => HardwareCatalog.HddBuses;
        public List<string> HddChannels => HardwareCatalog.HddChannels;
        public List<string> HddModels => HardwareCatalog.HddModels;
        public List<string> HddAudioOptions => HardwareCatalog.HddAudioOptions;

        private static bool IsIdeClass(string? bus) => (bus ?? string.Empty).ToUpperInvariant() is "IDE" or "ATAPI";

        /// <summary>True when a drive on bus <paramref name="a"/> competes for the same channel numbers as bus <paramref name="b"/> (IDE and ATAPI share one set).</summary>
        private static bool SharesChannels(string? a, string? b)
        {
            if (IsIdeClass(a) && IsIdeClass(b)) return true;
            string ua = (a ?? string.Empty).ToUpperInvariant(), ub = (b ?? string.Empty).ToUpperInvariant();
            return ua == ub && ua is "SCSI" or "ESDI" or "MFM";
        }

        /// <summary>The channels of <paramref name="bus"/>, those another drive (any hard disk, CD-ROM, MO, removable or tape) already uses marked unavailable.</summary>
        public List<ChannelOption> GetChannelOptions(string? bus, object? except)
        {
            string b = (bus ?? "IDE").ToUpperInvariant();
            IEnumerable<string> channels = b == "SCSI" ? Enumerable.Range(0, 16).Select(i => $"0:{i}")
                                         : b is "ESDI" or "MFM" ? new[] { "0:0", "0:1" }
                                         : HardwareCatalog.HddChannels;

            var taken = new HashSet<string>();
            foreach (var d in HardDisks)
                if (!ReferenceEquals(d, except) && SharesChannels(d.Bus, b)) taken.Add(d.ChannelName);
            foreach (var c in CdromDrives)
                if (!ReferenceEquals(c, except) && SharesChannels(c.Bus, b)) taken.Add(c.Channel);
            foreach (var m in MoDrives.Concat(RemovableDrives).Concat(TapeDrives))
                if (!ReferenceEquals(m, except) && SharesChannels(m.Bus, b)) taken.Add(m.Channel);

            return channels.Select(ch => new ChannelOption(ch, !taken.Contains(ch))).ToList();
        }

        private bool _refreshingChannels;

        /// <summary>Recomputes every hard disk's channel choices and moves a disk that sits on a taken channel to the first free one.</summary>
        public void RefreshChannelOptions()
        {
            if (_refreshingChannels) return;
            _refreshingChannels = true;
            try
            {
                for (int pass = 0; pass < 2; pass++)
                    foreach (var d in HardDisks)
                    {
                        var opts = GetChannelOptions(d.Bus, d);
                        var current = opts.FirstOrDefault(o => o.Name == d.ChannelName);
                        if (current == null || !current.IsAvailable)
                        {
                            var free = opts.FirstOrDefault(o => o.IsAvailable);
                            if (free != null)
                            {
                                d.ChannelName = free.Name;
                                opts = GetChannelOptions(d.Bus, d);
                            }
                        }
                        if (!opts.SequenceEqual(d.ChannelOptions))
                            d.ChannelOptions = opts;
                    }
            }
            finally { _refreshingChannels = false; }
        }

        private void HookChannelWatch(System.ComponentModel.INotifyPropertyChanged item)
        {
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is "Bus" or "Channel" or "ChannelName") RefreshChannelOptions();
            };
        }

        private void StartChannelWatch()
        {
            void Watch<T>(ObservableCollection<T> list) where T : System.ComponentModel.INotifyPropertyChanged
            {
                foreach (var item in list) HookChannelWatch(item);
                list.CollectionChanged += (_, e) =>
                {
                    if (e.NewItems != null)
                        foreach (System.ComponentModel.INotifyPropertyChanged item in e.NewItems) HookChannelWatch(item);
                    RefreshChannelOptions();
                };
            }
            Watch(HardDisks);
            Watch(CdromDrives);
            Watch(MoDrives);
            Watch(RemovableDrives);
            Watch(TapeDrives);
            RefreshChannelOptions();
        }

        /// <summary>
        /// First free channel for a new disk: IDE 0:0, 0:1, 1:0 ... skipping channels used by other hard disks and by
        /// ATAPI CD-ROM drives; SCSI ids likewise. Falls back to SCSI when IDE is full.
        /// </summary>
        public (string Bus, string Channel) NextFreeBusChannel(string? preferredBus = null)
        {
            string pref = (preferredBus ?? "IDE").ToUpperInvariant();
            var buses = pref == "SCSI" ? new[] { "SCSI", "IDE" }
                      : pref is "ESDI" or "MFM" ? new[] { pref }
                      : new[] { "IDE", "SCSI" };

            // Channels are taken by every drive on the bus - hard disks, CD-ROMs, MO, removable disks and tapes.
            foreach (var bus in buses)
            {
                var free = GetChannelOptions(bus, null).FirstOrDefault(o => o.IsAvailable);
                if (free != null) return (bus, free.Name);
            }

            return ("IDE", "0:0");
        }

        /// <summary>Adds a disk that already has its final geometry (created by the dialog or read from an attached image).</summary>
        public HardDiskEntry AddHardDiskEntry(string path, Services.DiskGeometry geometry, string bus, string channel)
        {
            var entry = new HardDiskEntry
            {
                Channel = HardDisks.Count,
                Bus = bus,
                Type = bus,
                ChannelName = channel,
                Model = HardwareCatalog.DefaultHddModel(bus),
                Audio = "None",
                Cylinders = geometry.Cylinders,
                Heads = geometry.Heads,
                Sectors = geometry.Sectors,
                SizeMb = Math.Max(1, geometry.SizeMb),
                ImagePath = path
            };
            HardDisks.Add(entry);
            SelectedHardDisk = entry;
            return entry;
        }

        [RelayCommand]
        public async Task BrowseDiskImageAsync()
        {
            if (SelectedHardDisk == null) return;

            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.FileTypeFilter.Add(".vhd");
                picker.FileTypeFilter.Add(".img");
                picker.FileTypeFilter.Add(".hdi");
                picker.FileTypeFilter.Add(".hdx");
                picker.FileTypeFilter.Add("*");

                if (App.MainWindowHandle != IntPtr.Zero)
                {
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, App.MainWindowHandle);
                }

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    SelectedHardDisk.ImagePath = file.Path;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] BrowseDiskImage failed: {ex.Message}");
            }
        }

        [RelayCommand]
        public void RemoveHardDisk()
        {
            if (SelectedHardDisk != null)
            {
                HardDisks.Remove(SelectedHardDisk);
                SelectedHardDisk = HardDisks.FirstOrDefault();
            }
        }

        // ==========================================
        // 9. Floppy & CD-ROM Drives Category
        // ==========================================
        [ObservableProperty]
        public partial int FloppyCDSubTabIndex { get; set; } = 0;

        public bool IsFloppyCDSubTab0 => FloppyCDSubTabIndex == 0;
        public bool IsFloppyCDSubTab1 => FloppyCDSubTabIndex == 1;

        partial void OnFloppyCDSubTabIndexChanged(int value)
        {
            OnPropertyChanged(nameof(IsFloppyCDSubTab0));
            OnPropertyChanged(nameof(IsFloppyCDSubTab1));
        }

        public List<string> FloppyTypes { get; } = new() { "None", "3.5\" 1.44M", "3.5\" 2.88M", "3.5\" 720k", "5.25\" 1.2M", "5.25\" 360k" };
        public List<string> FloppyAudioOptions { get; } = new() { "None", "Internal PC Speaker", "Sound Blaster" };

        public ObservableCollection<FloppyDriveEntry> FloppyDrives { get; } = new();

        [ObservableProperty]
        public partial FloppyDriveEntry? SelectedFloppyDrive { get; set; }

        [ObservableProperty]
        public partial string SelectedFloppyA { get; set; } = "3.5\" 1.44M";

        [ObservableProperty]
        public partial string SelectedFloppyB { get; set; } = "None";

        public ObservableCollection<CdromDriveEntry> CdromDrives { get; } = new();

        [ObservableProperty]
        public partial CdromDriveEntry? SelectedCdromDrive { get; set; }

        public List<string> CdromBusOptions { get; } = new() { "ATAPI", "SCSI", "Disabled" };
        public List<string> CdromChannelOptions { get; } = new() { "0:0", "0:1", "1:0", "1:1" };
        public List<string> CdromSpeeds { get; } = new() { "None", "1x", "2x", "3x", "4x", "6x", "8x", "10x", "12x", "16x", "20x", "24x", "32x", "36x", "40x", "48x", "50x", "52x", "56x", "72x" };
        public List<OpticalDriveDevice> OpticalDriveModels => HardwareCatalog.OpticalDrives;
        public List<string> CdromTypes => HardwareCatalog.OpticalDrives.Select(d => d.DisplayName).ToList();

        [ObservableProperty]
        public partial string SelectedCdrom0Speed { get; set; } = "48x";

        [ObservableProperty]
        public partial string SelectedCdrom0Bus { get; set; } = "ATAPI 0:1 (Secondary Master)";

        // ==========================================
        // 10. Other Removable Devices Category
        // ==========================================
        [ObservableProperty]
        public partial int OtherRemovableSubTabIndex { get; set; } = 0;

        public bool IsOtherRemovableSubTab0 => OtherRemovableSubTabIndex == 0;
        public bool IsOtherRemovableSubTab1 => OtherRemovableSubTabIndex == 1;
        public bool IsOtherRemovableSubTab2 => OtherRemovableSubTabIndex == 2;

        partial void OnOtherRemovableSubTabIndexChanged(int value)
        {
            OnPropertyChanged(nameof(IsOtherRemovableSubTab0));
            OnPropertyChanged(nameof(IsOtherRemovableSubTab1));
            OnPropertyChanged(nameof(IsOtherRemovableSubTab2));
        }

        /// <summary>"None" followed by every engine device of a table-driven kind (names as the engine lists them).</summary>
        private static List<string> DeviceNames(string kind)
        {
            var list = new List<string> { "None" };
            if (Models.AuthenticCatalog.Devices.TryGetValue(kind, out var devs))
                list.AddRange(devs.Where(d => d.Internal != "none").Select(d => d.Name));
            return list;
        }

        private static List<string> DriveLabels((string Label, string Bus)[] table)
            => new List<string> { "None" }.Concat(table.Select(t => t.Label)).ToList();
        public List<string> RemovableBusOptions { get; } = new() { "Disabled", "SCSI", "ATAPI" };
        public List<string> TapeBusOptions { get; } = new() { "Disabled", "SCSI", "Floppy", "LPT" };
        public List<string> RemovableChannelOptions { get; } = new() { "", "0:0", "0:1", "0:2", "0:3", "0:4", "0:5", "0:6", "1:0", "1:1" };

        // MO drives (Screenshot 3)
        public ObservableCollection<GenericRemovableDriveEntry> MoDrives { get; } = new();
        [ObservableProperty] public partial GenericRemovableDriveEntry? SelectedMoDriveEntry { get; set; }
        public List<string> MoDriveTypes { get; } = DriveLabels(Models.AuthenticCatalog.MoDriveTypes);

        // Removable disk drives (Screenshot 2)
        public ObservableCollection<GenericRemovableDriveEntry> RemovableDrives { get; } = new();
        [ObservableProperty] public partial GenericRemovableDriveEntry? SelectedRemovableDriveEntry { get; set; }
        public List<string> RemovableDriveTypes { get; } = DriveLabels(Models.AuthenticCatalog.RdiskDriveTypes);

        // Tape drives (Screenshot 1)
        public ObservableCollection<GenericRemovableDriveEntry> TapeDrives { get; } = new();
        [ObservableProperty] public partial GenericRemovableDriveEntry? SelectedTapeDriveEntry { get; set; }
        public List<string> TapeDriveTypes { get; } = DriveLabels(Models.AuthenticCatalog.TapeDriveTypes);

        // ==========================================
        // 11. Other Peripherals Category
        // ==========================================
        [ObservableProperty]
        public partial int OtherPeripheralsSubTabIndex { get; set; } = 0;

        public bool IsOtherPeripheralsSubTab0 => OtherPeripheralsSubTabIndex == 0;
        public bool IsOtherPeripheralsSubTab1 => OtherPeripheralsSubTabIndex == 1;
        public bool IsOtherPeripheralsSubTab2 => OtherPeripheralsSubTabIndex == 2;

        partial void OnOtherPeripheralsSubTabIndexChanged(int value)
        {
            OnPropertyChanged(nameof(IsOtherPeripheralsSubTab0));
            OnPropertyChanged(nameof(IsOtherPeripheralsSubTab1));
            OnPropertyChanged(nameof(IsOtherPeripheralsSubTab2));
        }

        public List<string> IsaRtcOptions { get; } = DeviceNames("isartc");
        [ObservableProperty]
        public partial string SelectedIsaRtc { get; set; } = "None";

        [ObservableProperty] public partial bool EnableIsaBugger { get; set; } = false;
        [ObservableProperty] public partial bool EnablePostCard { get; set; } = true;
        [ObservableProperty] public partial bool EnableUnitTester { get; set; } = false;
        [ObservableProperty] public partial bool EnableNovellKeyCard { get; set; } = false;

        /// <summary>Memory expansion boards for this machine: MCA boards on an MCA machine, ISA boards otherwise.</summary>
        public List<string> MemExpOptions { get; private set; } = DeviceNames("isamem");
        public bool MemExpIsMca { get; private set; }
        public List<string> IsaRomOptions { get; } = DeviceNames("isarom");

        [ObservableProperty] public partial string SelectedMemExp1 { get; set; } = "None";
        [ObservableProperty] public partial string SelectedMemExp2 { get; set; } = "None";
        [ObservableProperty] public partial string SelectedMemExp3 { get; set; } = "None";
        [ObservableProperty] public partial string SelectedMemExp4 { get; set; } = "None";
        [ObservableProperty] public partial string SelectedIsaRom1 { get; set; } = "None";
        [ObservableProperty] public partial string SelectedIsaRom2 { get; set; } = "None";
        [ObservableProperty] public partial string SelectedIsaRom3 { get; set; } = "None";
        [ObservableProperty] public partial string SelectedIsaRom4 { get; set; } = "None";
        [ObservableProperty] public partial bool EnableSoftPower { get; set; } = false;

        private void RefreshMemExpList()
        {
            string? machine = Services.Cfg86Resolver.Machine(SelectedMachine)?.Info.InternalName;
            bool mca = false;
            try { mca = machine != null && Box86_WinUI.Interop.Box86CoreNative.EmuMachineHasMca(machine) == 1; }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException) { }

            MemExpIsMca = mca;
            MemExpOptions = DeviceNames(mca ? "mcamem" : "isamem");
            OnPropertyChanged(nameof(MemExpOptions));
            OnPropertyChanged(nameof(MemExpIsMca));
        }

        // ==========================================
        // Constructor & Initialization
        // ==========================================
        public SettingsViewModel(VirtualMachine vm, Action closeWindow)
        {
            VM = vm;
            _closeWindow = closeWindow;
            // Older configs may have two drives on one IDE channel / SCSI ID; repair them before they are shown.
            Services.Cfg86ChannelFixer.FixFile(vm.ConfigFilePath);
            _config = MachineConfig.Load(vm.ConfigFilePath);

            LoadRomConfiguration();
            LoadDisplayFilterConfiguration();
            LoadSettingsFromConfig();
            StartChannelWatch();
            _settingsLoaded = true;
        }

        private void LoadSettingsFromConfig()
        {
            // Machine (authentic 86box.cfg: internal identifiers, mem_size in KB, cpu_speed in Hz)
            var cfgMachine = Services.Cfg86Resolver.Machine(_config.GetString("Machine", "machine", ""));
            if (cfgMachine != null)
            {
                SelectedMachineType = cfgMachine.TypeKey;
                SelectedMachine = cfgMachine.Info.Name;
            }
            else
            {
                SelectedMachineType = MachineTypes.FirstOrDefault() ?? "[1997] Slot 1";
                SelectedMachine = "[i440BX] ASUS P2B-LS";
            }

            int memKb = _config.GetInt("Machine", "mem_size", VM.MemoryMb > 0 ? VM.MemoryMb * 1024 : 131072);
            MemoryMb = Math.Max(1, memKb / 1024.0);
            TimeSyncMode = Services.Cfg86Reader.TimeSyncLabel(_config.GetString("Machine", "time_sync", ""));

            // Processor
            string cfgFamilyRaw = _config.GetString("Machine", "cpu_family", "");
            string? cfgFamily = null;
            if (cfgMachine != null && Services.Cfg86Resolver.Cpu(cfgMachine, cfgFamilyRaw, null, out var famKey, out _, out _))
                cfgFamily = famKey;

            SelectedCpuType = Services.Cfg86Reader.CpuFamilyLabel(cfgFamily, AvailableCpus, AvailableCpus.FirstOrDefault() ?? "Intel Pentium II (Deschutes)");
            if (!AvailableCpus.Contains(SelectedCpuType))
                SelectedCpuType = AvailableCpus.FirstOrDefault() ?? SelectedCpuType;

            long cfgSpeed = long.TryParse(_config.GetString("Machine", "cpu_speed", "0"), out var hz) ? hz : 0;
            double cfgMulti = double.TryParse(_config.GetString("Machine", "cpu_multi", "0"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var mu) ? mu : 0;
            RefreshFrequencies();
            string? storedStep = Services.Cfg86Reader.CpuStepLabel(cfgFamily, cfgSpeed, cfgMulti);
            var storedLabel = _stepByLabel.FirstOrDefault(kv => kv.Value.Name == storedStep).Key;
            SelectedFrequency = storedLabel ?? AvailableFrequencies.FirstOrDefault() ?? string.Empty;
            SelectedFpu = Services.Cfg86Reader.FpuLabel(_config.GetString("Machine", "fpu_type", ""));
            if (!AvailableFpus.Contains(SelectedFpu))
                SelectedFpu = AvailableFpus.FirstOrDefault() ?? SelectedFpu;
            int waitStates = _config.GetInt("Machine", "cpu_waitstates", 0);
            SelectedWaitState = HardwareCatalog.WaitStatesOptions[Math.Clamp(waitStates, 0, HardwareCatalog.WaitStatesOptions.Count - 1)];
            SoftfloatFpu = _config.GetBool("Machine", "fpu_softfloat", false);

            CpuFrameSize = _config.GetBool("General", "force_10ms", false) ? "Larger" : "Smaller";
            // Performance
            SelectedPitMode = Services.Cfg86Reader.PitModeLabel(_config.GetInt("Machine", "pit_mode", -1));
            Use486InterpreterFor286_386 = _config.GetBool("Machine", "cpu_override_interpreter", false);
            DynamicRecompiler = _config.GetBool("Machine", "cpu_use_dynarec", false);
            AccurateFpuEnvironment = _config.GetBool("Machine", "cpu_dyn_accurate_fpu_env", false);

            // Display
            static string Internal(string kind, string raw) => Services.Cfg86Resolver.Device(kind, raw) ?? raw;

            RefreshDeviceLists();
            SelectedVideoCard = Services.Cfg86Reader.DeviceLabel("video", Internal("video", _config.GetString("Video", "gfxcard", "cga")), VideoCards);
            SelectedSecondaryVideoCard = Services.Cfg86Reader.DeviceLabel("video", Internal("video", _config.GetString("Video", "gfxcard_2", "")), SecondaryVideoCards);
            EnableVoodoo = _config.GetBool("Video", "voodoo", false);
            Enable8514 = _config.GetBool("Video", "8514a", false);
            EnableXga = _config.GetBool("Video", "xga", false);
            EnableDa2 = _config.GetBool("Video", "da2", false);

            // Input
            SelectedMouseType = Services.Cfg86Reader.DeviceLabel("input", Internal("input", _config.GetString("Input devices", "mouse_type", "")), MouseTypes);
            SelectedKeyboardType = Services.Cfg86Reader.DeviceLabel("input", Internal("input", _config.GetString("Input devices", "keyboard_type", "")), KeyboardTypes, KeyboardTypes.FirstOrDefault() ?? "None");

            // Sound
            SelectedSoundCard1 = Services.Cfg86Reader.DeviceLabel("sound", Internal("sound", _config.GetString("Sound", "sndcard", "")), SoundCards);
            SelectedSoundCard2 = Services.Cfg86Reader.DeviceLabel("sound", Internal("sound", _config.GetString("Sound", "sndcard2", "")), SoundCardsExtra);
            SelectedSoundCard3 = Services.Cfg86Reader.DeviceLabel("sound", Internal("sound", _config.GetString("Sound", "sndcard3", "")), SoundCardsExtra);
            SelectedSoundCard4 = Services.Cfg86Reader.DeviceLabel("sound", Internal("sound", _config.GetString("Sound", "sndcard4", "")), SoundCardsExtra);
            SelectedMidiOutDevice = Services.Cfg86Reader.DeviceLabel("midi_out", Internal("midi_out", _config.GetString("Sound", "midi_device", "")), MidiOutDevices);
            SelectedMidiInDevice = Services.Cfg86Reader.DeviceLabel("midi_in", Internal("midi_in", _config.GetString("Sound", "midi_in_device", "")), MidiInDevices);
            SelectedMidiDevice = SelectedMidiOutDevice;

            // Monitor EDID (monitor_edid / monitor_edid_path) and device options such as [voodoo]
            string edidPath = _config.GetString("Video", "monitor_edid_path", "");
            if (_config.GetInt("Video", "monitor_edid", 0) != 0 && edidPath.Length > 0)
            {
                MonitorEdidMode = "Custom";
                CustomEdidPath = edidPath;
                string fileName = System.IO.Path.GetFileName(edidPath);
                EdidProfileId = Services.EdidBuilder.Profiles.FirstOrDefault(p => fileName.Equals($"edid_{p.Id}.bin", StringComparison.OrdinalIgnoreCase))?.Id;
            }
            else
            {
                MonitorEdidMode = "Default";
                EdidProfileId = null;
            }

            DeviceSettings.Clear(); // option values are read from the file when a device dialog opens

            // Network: net_NN_card / net_NN_net_type (engine writes up to four cards)
            const string netSec = "Network";
            var netCards = new (string Card, string Type)[4];
            for (int i = 0; i < 4; i++)
            {
                string k = $"net_{i + 1:D2}_";
                netCards[i] = (
                    Services.Cfg86Reader.DeviceLabel("network", _config.GetString(netSec, k + "card", ""), i == 0 ? NetworkAdapters : NetworkAdaptersExtra),
                    Services.Cfg86Reader.NetModeLabel(_config.GetString(netSec, k + "net_type", "")));
            }
            LoadNetworkHostDevices();
            for (int i = 0; i < 4; i++)
            {
                string k = $"net_{i + 1:D2}_";
                var o = NetOptionsAll[i];
                o.HostDevice = HostDescriptionFor(_config.GetString(netSec, k + "host_device", ""));
                o.Secret = _config.GetString(netSec, k + "secret", "");
                o.Promiscuous = _config.GetBool(netSec, k + "promisc", false);
            }
            SelectedNetworkAdapter1 = netCards[0].Card; SelectedNetworkMode1 = netCards[0].Type;
            SelectedNetworkAdapter2 = netCards[1].Card; SelectedNetworkMode2 = netCards[1].Type;
            SelectedNetworkAdapter3 = netCards[2].Card; SelectedNetworkMode3 = netCards[2].Type;
            SelectedNetworkAdapter4 = netCards[3].Card; SelectedNetworkMode4 = netCards[3].Type;
            UpdateNetOptions();

            // Ports (COM & LPT): serialN_enabled defaults on for COM1-2, lptN_enabled on for LPT1; devices omitted = none
            const string portSec = "Ports (COM & LPT)";
            EnableCom1 = _config.GetBool(portSec, "serial1_enabled", true);
            EnableCom2 = _config.GetBool(portSec, "serial2_enabled", true);
            EnableCom3 = _config.GetBool(portSec, "serial3_enabled", false);
            EnableCom4 = _config.GetBool(portSec, "serial4_enabled", false);
            SelectedCom1Device = Services.Cfg86Reader.DeviceLabel("char_com", _config.GetString(portSec, "serial1_device", ""), ComDevices);
            SelectedCom2Device = Services.Cfg86Reader.DeviceLabel("char_com", _config.GetString(portSec, "serial2_device", ""), ComDevices);
            SelectedCom3Device = Services.Cfg86Reader.DeviceLabel("char_com", _config.GetString(portSec, "serial3_device", ""), ComDevices);
            SelectedCom4Device = Services.Cfg86Reader.DeviceLabel("char_com", _config.GetString(portSec, "serial4_device", ""), ComDevices);

            EnableLpt1 = _config.GetBool(portSec, "lpt1_enabled", true);
            EnableLpt2 = _config.GetBool(portSec, "lpt2_enabled", false);
            EnableLpt3 = _config.GetBool(portSec, "lpt3_enabled", false);
            EnableLpt4 = _config.GetBool(portSec, "lpt4_enabled", false);
            SelectedLpt1Device = Services.Cfg86Reader.DeviceLabel("char_lpt", _config.GetString(portSec, "lpt1_device", ""), LptDevices);
            SelectedLpt2Device = Services.Cfg86Reader.DeviceLabel("char_lpt", _config.GetString(portSec, "lpt2_device", ""), LptDevices);
            SelectedLpt3Device = Services.Cfg86Reader.DeviceLabel("char_lpt", _config.GetString(portSec, "lpt3_device", ""), LptDevices);
            SelectedLpt4Device = Services.Cfg86Reader.DeviceLabel("char_lpt", _config.GetString(portSec, "lpt4_device", ""), LptDevices);

            // Storage Controllers
            SelectedCdromController = Services.Cfg86Reader.DeviceLabel("cdrom_if", _config.GetString("Storage controllers", "cdrom_interface", ""), CdromControllers);
            SelectedFloppyController = Services.Cfg86Reader.DeviceLabel("fdc", Internal("fdc", _config.GetString("Storage controllers", "fdc", "internal")), FloppyControllers);
            SelectedHdController1 = Services.Cfg86Reader.DeviceLabel("hdc", Internal("hdc", _config.GetString("Storage controllers", "hdc_1", "internal")), HdControllers);
            SelectedHdController2 = Services.Cfg86Reader.DeviceLabel("hdc", Internal("hdc", _config.GetString("Storage controllers", "hdc_2", "")), HdControllersExtra);
            SelectedHdController3 = Services.Cfg86Reader.DeviceLabel("hdc", Internal("hdc", _config.GetString("Storage controllers", "hdc_3", "")), HdControllersExtra);
            SelectedHdController4 = Services.Cfg86Reader.DeviceLabel("hdc", Internal("hdc", _config.GetString("Storage controllers", "hdc_4", "")), HdControllersExtra);
            EnableCassette = _config.GetBool("Storage controllers", "cassette_enabled", false);
            SelectedScsiController1 = Services.Cfg86Reader.DeviceLabel("scsi", Internal("scsi", _config.GetString("Storage controllers", "scsicard_1", "")), ScsiControllers);
            SelectedScsiController2 = Services.Cfg86Reader.DeviceLabel("scsi", Internal("scsi", _config.GetString("Storage controllers", "scsicard_2", "")), ScsiControllers);
            SelectedScsiController3 = Services.Cfg86Reader.DeviceLabel("scsi", Internal("scsi", _config.GetString("Storage controllers", "scsicard_3", "")), ScsiControllers);
            SelectedScsiController4 = Services.Cfg86Reader.DeviceLabel("scsi", Internal("scsi", _config.GetString("Storage controllers", "scsicard_4", "")), ScsiControllers);

            // A stored device this machine no longer offers (missing ROM, wrong bus) would leave a blank combo box:
            // settle every selection on an entry the lists really contain, as the original dialog does.
            RefreshDeviceLists();

            // Hard Disks: hdd_NN_parameters = "spt, hpc, tracks, wp, bus" (+ per-bus channel key, fn, speed preset)
            HardDisks.Clear();
            for (int n = 1; n <= 30; n++)
            {
                string k = $"hdd_{n:D2}_";
                if (!Services.Cfg86Reader.TryParseHdd(_config.GetString("Hard disks", k + "parameters", ""), out int spt, out int hpc, out int tracks, out string hddBus))
                    continue;

                string hddChannel = hddBus switch
                {
                    "ide" or "atapi" => Services.Cfg86Reader.ChannelLabel(_config.GetString("Hard disks", k + "ide_channel", "0:0")),
                    "scsi" => Services.Cfg86Reader.ChannelLabel(_config.GetString("Hard disks", k + "scsi_location", "0:0")),
                    "mfm" => "0:" + _config.GetInt("Hard disks", k + "mfm_channel", 0),
                    "esdi" => "0:" + _config.GetInt("Hard disks", k + "esdi_channel", 0),
                    _ => "0:0"
                };

                string presetInternal = _config.GetString("Hard disks", k + "speed", "");
                string model = HardwareCatalog.HddModels.FirstOrDefault(m => Services.Cfg86Resolver.HddPreset(m) == presetInternal)
                               ?? HardwareCatalog.DefaultHddModel(hddBus);

                HardDisks.Add(new HardDiskEntry
                {
                    Channel = HardDisks.Count,
                    Type = Services.Cfg86Reader.BusLabel(hddBus),
                    Bus = Services.Cfg86Reader.BusLabel(hddBus),
                    ChannelName = hddChannel,
                    Model = model,
                    Audio = "None",
                    Cylinders = tracks,
                    Heads = hpc,
                    Sectors = spt,
                    SizeMb = (int)((long)tracks * hpc * spt * 512L / (1024 * 1024)),
                    ImagePath = _config.GetString("Hard disks", k + "fn", string.Empty)
                });
            }
            SelectedHardDisk = HardDisks.FirstOrDefault();

            // Floppy Drives: fdd_NN_type is omitted at the engine default (5.25" 360k for the first two, none after)
            FloppyDrives.Clear();
            for (int i = 0; i < 4; i++)
            {
                string k = $"fdd_{i + 1:D2}_";
                FloppyDrives.Add(new FloppyDriveEntry
                {
                    Index = i,
                    Type = Services.Cfg86Reader.FloppyLabel(_config.GetString("Floppy and CD-ROM drives", k + "type", ""), i),
                    Turbo = _config.GetBool("Floppy and CD-ROM drives", k + "turbo", false),
                    CheckBpb = _config.GetBool("Floppy and CD-ROM drives", k + "check_bpb", true)
                });
            }
            SelectedFloppyDrive = FloppyDrives.FirstOrDefault();

            // CD-ROM Drives: cdrom_NN_parameters = "sound, bus" (absent = drive disabled)
            CdromDrives.Clear();
            for (int i = 0; i < 8; i++)
            {
                string k = $"cdrom_{i + 1:D2}_";
                string cdParams = _config.GetString("Floppy and CD-ROM drives", k + "parameters", "");
                string cdBusInternal = cdParams.Contains(',') ? cdParams.Split(',')[1].Trim().ToLowerInvariant() : "none";
                string cdBus = Services.Cfg86Reader.BusLabel(cdBusInternal);

                string cdChannel = cdBusInternal switch
                {
                    "atapi" => Services.Cfg86Reader.ChannelLabel(_config.GetString("Floppy and CD-ROM drives", k + "ide_channel", "0:1")),
                    "scsi" => Services.Cfg86Reader.ChannelLabel(_config.GetString("Floppy and CD-ROM drives", k + "scsi_location", "0:0")),
                    "mke" => "0:" + _config.GetInt("Floppy and CD-ROM drives", k + "mke_channel", 0),
                    _ => "0:0"
                };

                int cdSpeed = _config.GetInt("Floppy and CD-ROM drives", k + "speed", 8);
                string cdType = _config.GetString("Floppy and CD-ROM drives", k + "type", "86cd");

                CdromDrives.Add(new CdromDriveEntry
                {
                    Index = i,
                    Bus = cdBus,
                    Channel = cdChannel,
                    Speed = $"{cdSpeed}x",
                    Type = HardwareCatalog.GetCdromInternalName(cdType),
                    UseEdcEcc = true
                });
            }
            SelectedCdromDrive = CdromDrives.FirstOrDefault();

            // Other removable devices: mo|rdisk|tape_NN_parameters = "typeIndex, bus"
            MoDrives.Clear();
            RemovableDrives.Clear();
            TapeDrives.Clear();
            LoadRemovable(MoDrives, "mo", Models.AuthenticCatalog.MoDriveTypes, "\uE93C");
            LoadRemovable(RemovableDrives, "rdisk", Models.AuthenticCatalog.RdiskDriveTypes, "\uE88E");
            LoadRemovable(TapeDrives, "tape", Models.AuthenticCatalog.TapeDriveTypes, "\uE77C");
            SelectedMoDriveEntry = MoDrives.FirstOrDefault();
            SelectedRemovableDriveEntry = RemovableDrives.FirstOrDefault();
            SelectedTapeDriveEntry = TapeDrives.FirstOrDefault();

            // Other peripherals (engine keys: <name>_enabled, <board>N_type)
            const string periph = "Other peripherals";
            EnableIsaBugger = _config.GetBool(periph, "bugger_enabled", false);
            EnablePostCard = _config.GetBool(periph, "postcard_enabled", false);
            EnableUnitTester = _config.GetBool(periph, "unittester_enabled", false);
            EnableNovellKeyCard = _config.GetBool(periph, "novell_keycard_enabled", false);
            SelectedIsaRtc = Services.Cfg86Reader.DeviceLabel("isartc", _config.GetString(periph, "isartc_type", ""), IsaRtcOptions);
            RefreshMemExpList();
            string memKind = MemExpIsMca ? "mcamem" : "isamem";
            string memKey = MemExpIsMca ? "mcamem" : "isamem";
            SelectedMemExp1 = Services.Cfg86Reader.DeviceLabel(memKind, _config.GetString(periph, memKey + "0_type", ""), MemExpOptions);
            SelectedMemExp2 = Services.Cfg86Reader.DeviceLabel(memKind, _config.GetString(periph, memKey + "1_type", ""), MemExpOptions);
            SelectedMemExp3 = Services.Cfg86Reader.DeviceLabel(memKind, _config.GetString(periph, memKey + "2_type", ""), MemExpOptions);
            SelectedMemExp4 = Services.Cfg86Reader.DeviceLabel(memKind, _config.GetString(periph, memKey + "3_type", ""), MemExpOptions);
            SelectedIsaRom1 = Services.Cfg86Reader.DeviceLabel("isarom", _config.GetString(periph, "isarom0_type", ""), IsaRomOptions);
            SelectedIsaRom2 = Services.Cfg86Reader.DeviceLabel("isarom", _config.GetString(periph, "isarom1_type", ""), IsaRomOptions);
            SelectedIsaRom3 = Services.Cfg86Reader.DeviceLabel("isarom", _config.GetString(periph, "isarom2_type", ""), IsaRomOptions);
            SelectedIsaRom4 = Services.Cfg86Reader.DeviceLabel("isarom", _config.GetString(periph, "isarom3_type", ""), IsaRomOptions);
            EnableSoftPower = _config.GetBool(periph, "softpower_enabled", false);
        }

        private void LoadRemovable(ObservableCollection<GenericRemovableDriveEntry> into, string prefix, (string Label, string Bus)[] table, string glyph)
        {
            const string sec = "Other removable devices";
            for (int i = 0; i < 4; i++)
            {
                string k = $"{prefix}_{i + 1:D2}_";
                bool ok = Services.Cfg86Reader.TryParseRemovable(table, _config.GetString(sec, k + "parameters", ""), out string label, out string bus);

                string channel = !ok ? "" : bus switch
                {
                    "atapi" => Services.Cfg86Reader.ChannelLabel(_config.GetString(sec, k + "ide_channel", "1:0")),
                    "scsi" => Services.Cfg86Reader.ChannelLabel(_config.GetString(sec, k + "scsi_location", "0:0")),
                    "fdc" => "0:" + _config.GetInt(sec, k + "fdd_unit", 0),
                    "lpt" => "0:" + _config.GetInt(sec, k + "lpt_port", 0),
                    _ => ""
                };

                into.Add(new GenericRemovableDriveEntry
                {
                    Index = i,
                    Bus = ok ? Services.Cfg86Reader.BusLabel(bus) : "Disabled",
                    Channel = channel,
                    Type = ok ? label : "None",
                    Glyph = glyph
                });
            }
        }

        [ObservableProperty]
        public partial string SaveErrorMessage { get; set; } = string.Empty;

        public bool HasSaveError => !string.IsNullOrEmpty(SaveErrorMessage);

        partial void OnSaveErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasSaveError));

        /// <summary>Snapshot of the in-scope settings for the authentic 86box.cfg writer.</summary>
        private Services.Cfg86Settings BuildCfgSettings()
        {
            var cfg = new Services.Cfg86Settings
            {
                Machine = SelectedMachine,
                CpuFamily = SelectedCpuType,
                CpuSpeed = SelectedStep?.Name ?? string.Empty,
                MemoryKb = (int)(MemoryMb * 1024),
                Fpu = SelectedFpu,
                DynamicRecompiler = DynamicRecompiler,
                SoftFloat = SoftfloatFpu,
                TimeSync = TimeSyncMode,
                PitMode = SelectedPitMode,
                WaitStates = Math.Max(0, HardwareCatalog.WaitStatesOptions.IndexOf(SelectedWaitState)),
                LargerFrames = CpuFrameSize == "Larger",
                OverrideInterpreter = Use486InterpreterFor286_386,
                AccurateFpuEnvironment = AccurateFpuEnvironment,

                VideoCard = SelectedVideoCard,
                SecondaryVideoCard = SelectedSecondaryVideoCard,
                Voodoo = EnableVoodoo,
                Ibm8514 = Enable8514,
                Xga = EnableXga,
                Da2 = EnableDa2,

                KeyboardType = SelectedKeyboardType,
                MouseType = SelectedMouseType,

                SoundCards = new[] { SelectedSoundCard1, SelectedSoundCard2, SelectedSoundCard3, SelectedSoundCard4 },
                MidiDevice = SelectedMidiOutDevice,
                MidiInDevice = SelectedMidiInDevice,

                FloppyController = SelectedFloppyController,
                CdromInterface = SelectedCdromController,
                HdControllers = new[] { SelectedHdController1, SelectedHdController2, SelectedHdController3, SelectedHdController4 },
                ScsiControllers = new[] { SelectedScsiController1, SelectedScsiController2, SelectedScsiController3, SelectedScsiController4 },
                Cassette = EnableCassette,
            };

            for (int i = 0; i < FloppyDrives.Count; i++)
            {
                var d = FloppyDrives[i];
                cfg.Floppies.Add(new Services.FloppySetting
                {
                    Type = d.Type,
                    Turbo = d.Turbo,
                    CheckBpb = d.CheckBpb,
                    ImagePath = i == 0 ? (VM.MountedFloppyPath ?? string.Empty) : string.Empty
                });
            }

            for (int i = 0; i < CdromDrives.Count; i++)
            {
                var d = CdromDrives[i];
                _ = int.TryParse(new string(d.Speed.Where(char.IsDigit).ToArray()), out int speed);
                cfg.Cdroms.Add(new Services.CdromSetting
                {
                    Bus = d.Bus,
                    Channel = d.Channel,
                    Speed = speed > 0 ? speed : 8,
                    Type = d.Type,
                    ImagePath = i == 0 ? (VM.MountedCdromPath ?? string.Empty) : string.Empty
                });
            }

            foreach (var hd in HardDisks)
            {
                cfg.HardDisks.Add(new Services.HardDiskSetting
                {
                    Bus = hd.Bus,
                    Channel = hd.ChannelName,
                    Cylinders = hd.Cylinders,
                    Heads = hd.Heads,
                    Sectors = hd.Sectors,
                    SizeMb = hd.SizeMb,
                    ImagePath = hd.ImagePath,
                    Model = hd.Model
                });
            }

            if (MonitorEdidMode == "Custom")
            {
                var profile = Services.EdidBuilder.Find(EdidProfileId);
                if (profile != null)
                {
                    try { cfg.MonitorEdidPath = Services.EdidBuilder.WriteProfile(profile, VM.Path); }
                    catch (Exception ex) { SaveErrorMessage = $"Could not write the EDID profile: {ex.Message}"; }
                }
                else if (!string.IsNullOrWhiteSpace(CustomEdidPath))
                {
                    cfg.MonitorEdidPath = CustomEdidPath;
                }
            }

            foreach (var kv in DeviceSettings)
                cfg.DeviceSettings[kv.Key] = kv.Value;

            var nets = new[]
            {
                (SelectedNetworkAdapter1, SelectedNetworkMode1),
                (SelectedNetworkAdapter2, SelectedNetworkMode2),
                (SelectedNetworkAdapter3, SelectedNetworkMode3),
                (SelectedNetworkAdapter4, SelectedNetworkMode4),
            };
            for (int i = 0; i < nets.Length; i++)
            {
                var o = NetOptionsAll[i];
                string mode = nets[i].Item2;
                bool pcap = mode == "PCap", local = mode == "Local Switch";
                cfg.NetworkCards.Add(new Services.NetworkCardSetting
                {
                    Card = nets[i].Item1,
                    NetType = mode,
                    HostDevice = pcap ? HostDeviceFor(o.HostDevice) : string.Empty,
                    Secret = local ? o.Secret : string.Empty,
                    Promiscuous = local && o.Promiscuous
                });
            }

            cfg.ComEnabled = new[] { EnableCom1, EnableCom2, EnableCom3, EnableCom4 };
            cfg.ComDevices = new[] { SelectedCom1Device, SelectedCom2Device, SelectedCom3Device, SelectedCom4Device };
            cfg.LptEnabled = new[] { EnableLpt1, EnableLpt2, EnableLpt3, EnableLpt4 };
            cfg.LptDevices = new[] { SelectedLpt1Device, SelectedLpt2Device, SelectedLpt3Device, SelectedLpt4Device };

            static void AddRemovable(List<Services.RemovableSetting> into, IEnumerable<GenericRemovableDriveEntry> from)
            {
                foreach (var d in from)
                    into.Add(new Services.RemovableSetting { Bus = d.Bus, Channel = d.Channel, Type = d.Type });
            }
            AddRemovable(cfg.MoDrives, MoDrives);
            AddRemovable(cfg.RemovableDisks, RemovableDrives);
            AddRemovable(cfg.TapeDrives, TapeDrives);

            cfg.IsaBugger = EnableIsaBugger;
            cfg.PostCard = EnablePostCard;
            cfg.UnitTester = EnableUnitTester;
            cfg.NovellKeycard = EnableNovellKeyCard;
            cfg.IsaRtc = SelectedIsaRtc;
            cfg.MemExpIsMca = MemExpIsMca;
            cfg.MemExpCards = new[] { SelectedMemExp1, SelectedMemExp2, SelectedMemExp3, SelectedMemExp4 };
            cfg.IsaRomCards = new[] { SelectedIsaRom1, SelectedIsaRom2, SelectedIsaRom3, SelectedIsaRom4 };
            cfg.SoftPower = EnableSoftPower;
            return cfg;
        }

        /// <summary>Raised after a successful save while the VM is running: the running machine keeps its old hardware.</summary>
        public event Action? RestartRequired;

        [RelayCommand]
        public void Save()
        {
            SaveErrorMessage = string.Empty;

            try
            {
                string cfgPath = VM.ConfigFilePath;
                string expectedDir = System.IO.Path.GetFullPath(VM.Path).TrimEnd('\\');
                string actualDir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(cfgPath))?.TrimEnd('\\') ?? string.Empty;
                if (string.IsNullOrWhiteSpace(VM.Path) || !string.Equals(expectedDir, actualDir, StringComparison.OrdinalIgnoreCase))
                {
                    SaveErrorMessage = $"Refusing to save: the configuration path '{cfgPath}' is not inside the machine folder '{VM.Path}'.";
                    return;
                }

                // Every section is serialized by the authentic 86box.cfg writer (internal identifiers only, ROMs verified).
                var settings = BuildCfgSettings();
                if (!string.IsNullOrEmpty(SaveErrorMessage)) return; // e.g. the EDID profile file could not be written
                var result = new Services.Cfg86Writer().Write(cfgPath, settings, _configService.GetRomDirectory());
                foreach (var issue in result.Issues.Where(i => !i.IsError))
                    System.Diagnostics.Debug.WriteLine($"[Cfg86Writer] warning [{issue.Section}] {issue.Key}: {issue.Message}");

                if (result.HasErrors || !result.Written)
                {
                    SaveErrorMessage = result.HasErrors
                        ? string.Join(Environment.NewLine, result.Issues.Where(i => i.IsError).Select(i => i.Message))
                        : "The configuration could not be written.";
                    return; // keep the window open; nothing was written
                }

                // Read it back: proves the bytes are on disk and parse as the settings that were just chosen.
                var check = MachineConfig.Load(cfgPath);
                try
                {
                    System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "box86_startup.log"),
                        $"{DateTime.Now:HH:mm:ss.fff} settings saved '{VM.Name}': machine={check.GetString("Machine", "machine", "")} gfx={check.GetString("Video", "gfxcard", "")} " +
                        $"fdc={check.GetString("Storage controllers", "fdc", "(internal)")} hdc_1={check.GetString("Storage controllers", "hdc_1", "(internal)")} running={VM.IsRunning}{Environment.NewLine}");
                }
                catch (Exception) { /* logging only */ }
                if (!string.Equals(check.GetString("Machine", "machine", ""), Services.Cfg86Resolver.Machine(SelectedMachine)?.Info.InternalName, StringComparison.OrdinalIgnoreCase))
                {
                    SaveErrorMessage = $"The file '{cfgPath}' was written but does not contain the selected machine when read back.";
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Save failed: {ex}");
                try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "box86_startup.log"), $"{DateTime.Now:HH:mm:ss.fff} settings save failed: {ex}{Environment.NewLine}"); }
                catch (Exception logEx) { System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] could not log save failure: {logEx.Message}"); }
                SaveErrorMessage = $"Could not save the configuration to '{VM.ConfigFilePath}': {ex.Message}";
                return;
            }

            if (FloppyDrives.Count > 0) SelectedFloppyA = FloppyDrives[0].Type;

            // Update VM runtime properties
            VM.MachineFamily = SelectedMachineType;
            VM.MachineModel = SelectedMachine;
            VM.Cpu = $"{SelectedCpuType} ({SelectedStep?.Name ?? SelectedFrequency} MHz)";
            VM.MemoryMb = (int)MemoryMb;
            VM.VideoCard = SelectedVideoCard;
            VM.SoundCard = SelectedSoundCard1;
            VM.FloppySummary = SelectedFloppyA;
            VM.HardDisksSummary = HardDisks.Count > 0 ? $"{HardDisks[0].SizeMb} MB" : "None";

            // The file is already on disk, so the next launch reads it; a running machine needs a restart.
            if ((VM.Status is VMStatus.Running or VMStatus.Paused) && RestartRequired != null)
                RestartRequired.Invoke();
            else
                _closeWindow();
        }

        [RelayCommand]
        public void Cancel()
        {
            _closeWindow();
        }
    }
}
