using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Box86_WinUI.Models
{
    public enum VMStatus
    {
        Stopped,
        Running,
        Paused,
        Starting,
        Stopping
    }

    public class VirtualMachine : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _name = "New Virtual Machine";
        private string _path = string.Empty;
        private string _machineFamily = "IBM PC / XT";
        private string _machineModel = "IBM PC (1981)";
        private string _rawMachine = "ibmpc";
        private string _cpu = "Intel 8088 (4.77 MHz)";
        private string _rawCpu = "i8088";
        private int _memoryKb = 640;
        private int _memoryMb = 1;
        private string _videoCard = "IBM CGA";
        private string _rawVideo = "cga";
        private string _soundCard = "PC Speaker";
        private string _rawSound = "speaker";
        private string _hardDisksSummary = "None";
        private string _floppySummary = "1.44M 3.5\"";
        private string _cdRomSummary = "None";
        private VMStatus _status = VMStatus.Stopped;
        private int _processId = 0;
        private IntPtr _windowHwnd = IntPtr.Zero;
        private string? _screenshotPath;
        private double _speedPercent = 100.0;
        private string? _notes;
        private DateTime _lastRun = DateTime.MinValue;

        // Removable Media State
        private string? _mountedFloppyPath;
        private bool _isFloppyWriteProtected = false;
        private ObservableCollection<string> _recentFloppyImages = new();

        private string? _mountedCdromPath;
        private ObservableCollection<string> _recentCdromImages = new();

        private string? _mountedRemovablePath;
        private bool _isRemovableWriteProtected = false;
        private ObservableCollection<string> _recentRemovableImages = new();

        // Emulation Activity Indicators
        private bool _isFloppyActive = false;
        private bool _isCdromActive = false;
        private bool _isRemovableActive = false;
        private bool _isHddActive = false;
        private bool _isNetActive = false;

        public string Id
        {
            get => _id;
            set => SetField(ref _id, value);
        }

        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        public string Path
        {
            get => _path;
            set => SetField(ref _path, value);
        }

        public string VmPath => Path;

        public string ConfigFilePath => System.IO.Path.Combine(Path, "86box.cfg");

        public string MachineFamily
        {
            get => _machineFamily;
            set
            {
                if (SetField(ref _machineFamily, value))
                {
                    OnPropertyChanged(nameof(PlatformName));
                    OnPropertyChanged(nameof(SocketType));
                }
            }
        }

        public string MachineModel
        {
            get => _machineModel;
            set
            {
                if (SetField(ref _machineModel, value))
                {
                    OnPropertyChanged(nameof(SpecsSummary));
                    OnPropertyChanged(nameof(PlatformName));
                    OnPropertyChanged(nameof(SocketType));
                }
            }
        }

        public string RawMachine
        {
            get => _rawMachine;
            set
            {
                if (SetField(ref _rawMachine, value))
                {
                    OnPropertyChanged(nameof(PlatformName));
                    OnPropertyChanged(nameof(SocketType));
                }
            }
        }

        public string Cpu
        {
            get => _cpu;
            set
            {
                if (SetField(ref _cpu, value))
                {
                    OnPropertyChanged(nameof(SpecsSummary));
                    OnPropertyChanged(nameof(ArchitectureSummary));
                }
            }
        }

        public string RawCpu
        {
            get => _rawCpu;
            set => SetField(ref _rawCpu, value);
        }

        public int MemoryKb
        {
            get => _memoryKb;
            set
            {
                if (SetField(ref _memoryKb, value))
                {
                    OnPropertyChanged(nameof(MemoryDisplay));
                    OnPropertyChanged(nameof(SpecsSummary));
                    OnPropertyChanged(nameof(ArchitectureSummary));
                }
            }
        }

        public int MemoryMb
        {
            get => _memoryMb;
            set
            {
                if (SetField(ref _memoryMb, value))
                {
                    OnPropertyChanged(nameof(MemoryDisplay));
                    OnPropertyChanged(nameof(SpecsSummary));
                    OnPropertyChanged(nameof(ArchitectureSummary));
                }
            }
        }

        public string MemoryDisplay => HardwareCatalog.FormatMemoryDisplay(MemoryKb > 0 ? MemoryKb : MemoryMb * 1024);

        public string VideoCard
        {
            get => _videoCard;
            set => SetField(ref _videoCard, value);
        }

        public string RawVideo
        {
            get => _rawVideo;
            set => SetField(ref _rawVideo, value);
        }

        public string SoundCard
        {
            get => _soundCard;
            set => SetField(ref _soundCard, value);
        }

        public string RawSound
        {
            get => _rawSound;
            set => SetField(ref _rawSound, value);
        }

        public string HardDisksSummary
        {
            get => _hardDisksSummary;
            set => SetField(ref _hardDisksSummary, value);
        }

        public string FloppySummary
        {
            get => _floppySummary;
            set => SetField(ref _floppySummary, value);
        }

        public string CdRomSummary
        {
            get => _cdRomSummary;
            set => SetField(ref _cdRomSummary, value);
        }

        public string ArchitectureSummary => $"{Cpu} • {MemoryDisplay} • {VideoCard}";

        public string SpecsSummary
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(MachineModel))
                    parts.Add(MachineModel);
                if (!string.IsNullOrWhiteSpace(Cpu))
                    parts.Add(Cpu);
                if (!string.IsNullOrWhiteSpace(MemoryDisplay))
                    parts.Add($"{MemoryDisplay} RAM");

                return parts.Count > 0 ? string.Join(" · ", parts) : "No hardware configured";
            }
        }

        public string PlatformName
        {
            get
            {
                var (platform, _) = HardwareCatalog.FindMachine(RawMachine);
                if (string.IsNullOrEmpty(platform))
                    (platform, _) = HardwareCatalog.FindMachine(MachineModel);
                if (!string.IsNullOrEmpty(platform))
                    return platform;
                return !string.IsNullOrEmpty(MachineFamily) && MachineFamily != "IBM PC / XT"
                    ? MachineFamily
                    : "IBM PC Compatible";
            }
        }

        public string SocketType
        {
            get
            {
                var (platform, info) = HardwareCatalog.FindMachine(RawMachine);
                if (info == null)
                    (platform, info) = HardwareCatalog.FindMachine(MachineModel);
                if (info != null && !string.IsNullOrEmpty(info.CpuPackage))
                    return HardwareCatalog.FormatSocketPackage(info.CpuPackage);
                return !string.IsNullOrEmpty(PlatformName) ? PlatformName : "Socket / Slot";
            }
        }

        public string PlatformGlyph
        {
            get
            {
                string text = $"{MachineFamily} {MachineModel}".ToLowerInvariant();
                if (text.Contains("laptop") || text.Contains("notebook") || text.Contains("thinkpad"))
                    return "\uE7F8"; // Laptop
                if (text.Contains("server"))
                    return "\uE83B"; // Server
                return "\uE7F4";     // Desktop / Workstation PC
            }
        }

        public VMStatus Status
        {
            get => _status;
            set
            {
                if (SetField(ref _status, value))
                {
                    OnPropertyChanged(nameof(IsRunning));
                    OnPropertyChanged(nameof(IsStopped));
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(ProcessStatusString));
                    OnPropertyChanged(nameof(DetailStatusText));
                }
            }
        }

        public bool IsRunning => Status == VMStatus.Running || Status == VMStatus.Paused;
        public bool IsStopped => Status == VMStatus.Stopped;

        public string ProcessStatusString => Status switch
        {
            VMStatus.Running => "Running",
            VMStatus.Paused => "Paused",
            VMStatus.Starting => "Starting",
            VMStatus.Stopping => "Stopping",
            _ => "Stopped"
        };

        public string DetailStatusText => Status switch
        {
            VMStatus.Running => ProcessId > 0 ? $"Running: PID {ProcessId}" : "Running",
            VMStatus.Paused => "Paused",
            _ => "Not running"
        };

        public string StatusText => DetailStatusText;

        public string StatusColor => Status switch
        {
            VMStatus.Running => "#107C41", // Fluent Green
            VMStatus.Paused => "#FFB900",  // Fluent Amber
            VMStatus.Starting => "#0078D4",// Fluent Blue
            VMStatus.Stopping => "#D83B01",// Fluent Orange
            _ => "#8A8886"                 // Fluent Gray
        };

        public int ProcessId
        {
            get => _processId;
            set
            {
                if (SetField(ref _processId, value))
                {
                    OnPropertyChanged(nameof(DetailStatusText));
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public IntPtr WindowHwnd
        {
            get => _windowHwnd;
            set => SetField(ref _windowHwnd, value);
        }

        public string? ScreenshotPath
        {
            get => _screenshotPath;
            set => SetField(ref _screenshotPath, value);
        }

        public double SpeedPercent
        {
            get => _speedPercent;
            set => SetField(ref _speedPercent, value);
        }

        public string? Notes
        {
            get => _notes;
            set
            {
                if (SetField(ref _notes, value))
                {
                    OnPropertyChanged(nameof(Description));
                    SaveNotesToConfig();
                }
            }
        }

        public void SetNotesWithoutSaving(string? notes)
        {
            _notes = notes;
            OnPropertyChanged(nameof(Notes));
            OnPropertyChanged(nameof(Description));
        }

        public void SaveNotesToConfig()
        {
            if (string.IsNullOrEmpty(Path) || !System.IO.File.Exists(ConfigFilePath)) return;
            try
            {
                var config = MachineConfig.Load(ConfigFilePath);
                string encodedNotes = (_notes ?? string.Empty)
                    .Replace("\r\n", "\\n")
                    .Replace("\r", "\\n")
                    .Replace("\n", "\\n");
                config.SetString("General", "notes", encodedNotes);
                config.Save(ConfigFilePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualMachine] Error saving notes to config: {ex.Message}");
            }
        }

        public string? Description
        {
            get => _notes;
            set => Notes = value;
        }

        public DateTime LastRun
        {
            get => _lastRun;
            set => SetField(ref _lastRun, value);
        }

        #region Removable Media Properties & Operations

        public string? MountedFloppyPath
        {
            get => _mountedFloppyPath;
            set
            {
                if (SetField(ref _mountedFloppyPath, value))
                {
                    OnPropertyChanged(nameof(IsFloppyMounted));
                    OnPropertyChanged(nameof(FloppyDisplayName));
                }
            }
        }

        public bool IsFloppyMounted => !string.IsNullOrEmpty(MountedFloppyPath);
        public string FloppyDisplayName => (IsFloppyMounted && MountedFloppyPath != null ? System.IO.Path.GetFileName(MountedFloppyPath) : null) ?? "(Empty)";

        public bool IsFloppyWriteProtected
        {
            get => _isFloppyWriteProtected;
            set => SetField(ref _isFloppyWriteProtected, value);
        }

        public ObservableCollection<string> RecentFloppyImages
        {
            get => _recentFloppyImages;
            set => SetField(ref _recentFloppyImages, value);
        }

        public string? MountedCdromPath
        {
            get => _mountedCdromPath;
            set
            {
                if (SetField(ref _mountedCdromPath, value))
                {
                    OnPropertyChanged(nameof(IsCdromMounted));
                    OnPropertyChanged(nameof(CdromDisplayName));
                }
            }
        }

        public bool IsCdromMounted => !string.IsNullOrEmpty(MountedCdromPath);
        public string CdromDisplayName => (IsCdromMounted && MountedCdromPath != null ? System.IO.Path.GetFileName(MountedCdromPath) : null) ?? "(Empty)";

        public ObservableCollection<string> RecentCdromImages
        {
            get => _recentCdromImages;
            set => SetField(ref _recentCdromImages, value);
        }

        public string? MountedRemovablePath
        {
            get => _mountedRemovablePath;
            set
            {
                if (SetField(ref _mountedRemovablePath, value))
                {
                    OnPropertyChanged(nameof(IsRemovableMounted));
                    OnPropertyChanged(nameof(RemovableDisplayName));
                }
            }
        }

        public bool IsRemovableMounted => !string.IsNullOrEmpty(MountedRemovablePath);
        public string RemovableDisplayName => (IsRemovableMounted && MountedRemovablePath != null ? System.IO.Path.GetFileName(MountedRemovablePath) : null) ?? "(Empty)";

        public bool IsRemovableWriteProtected
        {
            get => _isRemovableWriteProtected;
            set => SetField(ref _isRemovableWriteProtected, value);
        }

        public ObservableCollection<string> RecentRemovableImages
        {
            get => _recentRemovableImages;
            set => SetField(ref _recentRemovableImages, value);
        }

        // Live Emulation LED States
        public bool IsFloppyActive
        {
            get => _isFloppyActive;
            set => SetField(ref _isFloppyActive, value);
        }

        public bool IsCdromActive
        {
            get => _isCdromActive;
            set => SetField(ref _isCdromActive, value);
        }

        public bool IsRemovableActive
        {
            get => _isRemovableActive;
            set => SetField(ref _isRemovableActive, value);
        }

        public bool IsHddActive
        {
            get => _isHddActive;
            set => SetField(ref _isHddActive, value);
        }

        public bool IsNetActive
        {
            get => _isNetActive;
            set => SetField(ref _isNetActive, value);
        }

        // Per-drive media. Drive 0 is backed by the original single-drive properties.
        private readonly string?[] _extraFloppyPaths = new string?[4];
        private readonly string?[] _extraCdromPaths = new string?[8];

        public string? GetFloppyPath(int drive) => drive == 0 ? MountedFloppyPath : _extraFloppyPaths[drive];
        public string? GetCdromPath(int drive) => drive == 0 ? MountedCdromPath : _extraCdromPaths[drive];

        public void SetFloppyPath(int drive, string? path)
        {
            if (drive == 0)
            {
                if (string.IsNullOrEmpty(path)) EjectFloppy(); else MountFloppy(path);
                return;
            }
            _extraFloppyPaths[drive] = string.IsNullOrEmpty(path) ? null : path;
            if (!string.IsNullOrEmpty(path)) AddRecentImage(RecentFloppyImages, path);
            SaveMediaConfig();
        }

        public void SetCdromPath(int drive, string? path)
        {
            if (drive == 0)
            {
                if (string.IsNullOrEmpty(path)) EjectCdrom(); else MountCdrom(path);
                return;
            }
            _extraCdromPaths[drive] = string.IsNullOrEmpty(path) ? null : path;
            if (!string.IsNullOrEmpty(path)) AddRecentImage(RecentCdromImages, path);
            SaveMediaConfig();
        }

        /// <summary>Reads the media of drives 2+ from the config (drive 1 is loaded with the rest of the VM).</summary>
        public void LoadExtraMedia()
        {
            if (string.IsNullOrEmpty(Path) || !File.Exists(ConfigFilePath)) return;
            var config = MachineConfig.Load(ConfigFilePath);
            for (int i = 1; i < _extraFloppyPaths.Length; i++)
            {
                string v = config.GetString("Floppy and CD-ROM drives", $"fdd_{i + 1:D2}_fn", string.Empty);
                _extraFloppyPaths[i] = v.Length == 0 ? null : v;
            }
            for (int i = 1; i < _extraCdromPaths.Length; i++)
            {
                string v = config.GetString("Floppy and CD-ROM drives", $"cdrom_{i + 1:D2}_image_path", string.Empty);
                _extraCdromPaths[i] = v.Length == 0 ? null : v;
            }
        }

        public void ClearFloppyHistory() { RecentFloppyImages.Clear(); SaveMediaConfig(); }
        public void ClearCdromHistory() { RecentCdromImages.Clear(); SaveMediaConfig(); }

        public void MountFloppy(string path, bool? writeProtect = null)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            MountedFloppyPath = path;
            if (writeProtect.HasValue)
            {
                IsFloppyWriteProtected = writeProtect.Value;
            }

            AddRecentImage(RecentFloppyImages, path);
            SaveMediaConfig();
        }

        public void EjectFloppy()
        {
            MountedFloppyPath = null;
            SaveMediaConfig();
        }

        public void ToggleFloppyWriteProtect()
        {
            IsFloppyWriteProtected = !IsFloppyWriteProtected;
            SaveMediaConfig();
        }

        public void MountCdrom(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            MountedCdromPath = path;
            AddRecentImage(RecentCdromImages, path);
            SaveMediaConfig();
        }

        public void EjectCdrom()
        {
            MountedCdromPath = null;
            SaveMediaConfig();
        }

        public void MountRemovable(string path, bool? writeProtect = null)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            MountedRemovablePath = path;
            if (writeProtect.HasValue)
            {
                IsRemovableWriteProtected = writeProtect.Value;
            }

            AddRecentImage(RecentRemovableImages, path);
            SaveMediaConfig();
        }

        public void EjectRemovable()
        {
            MountedRemovablePath = null;
            SaveMediaConfig();
        }

        public void ToggleRemovableWriteProtect()
        {
            IsRemovableWriteProtected = !IsRemovableWriteProtected;
            SaveMediaConfig();
        }

        private static void AddRecentImage(ObservableCollection<string> list, string path)
        {
            if (list.Contains(path))
            {
                list.Remove(path);
            }
            list.Insert(0, path);
            while (list.Count > 10)
            {
                list.RemoveAt(list.Count - 1);
            }
        }

        /// <summary>Raised when writing 86box.cfg fails so the UI can tell the user.</summary>
        public event Action<string>? ConfigSaveFailed;

        public void SaveMediaConfig()
        {
            if (string.IsNullOrEmpty(Path) || !File.Exists(ConfigFilePath)) return;
            try
            {
                var config = MachineConfig.Load(ConfigFilePath);

                // Floppy section
                // Authentic keys only; an empty/unmounted slot has no key at all.
                if (string.IsNullOrEmpty(MountedFloppyPath)) config.RemoveKey("Floppy and CD-ROM drives", "fdd_01_fn");
                else config.SetString("Floppy and CD-ROM drives", "fdd_01_fn", MountedFloppyPath);
                config.RemoveKey("Floppy and CD-ROM drives", "fdd_01_wp");
                for (int i = 0; i < Math.Min(10, RecentFloppyImages.Count); i++)
                {
                    config.SetString("Floppy and CD-ROM drives", $"fdd_01_image_history_{i + 1:D2}", RecentFloppyImages[i]);
                }
                for (int i = Math.Min(10, RecentFloppyImages.Count); i < 10; i++)
                    config.RemoveKey("Floppy and CD-ROM drives", $"fdd_01_image_history_{i + 1:D2}");

                // CD-ROM section
                if (string.IsNullOrEmpty(MountedCdromPath)) config.RemoveKey("Floppy and CD-ROM drives", "cdrom_01_image_path");
                else config.SetString("Floppy and CD-ROM drives", "cdrom_01_image_path", MountedCdromPath);
                config.RemoveKey("Floppy and CD-ROM drives", "cdrom_01_fn");
                for (int i = 0; i < Math.Min(10, RecentCdromImages.Count); i++)
                {
                    config.SetString("Floppy and CD-ROM drives", $"cdrom_01_image_history_{i + 1:D2}", RecentCdromImages[i]);
                }
                for (int i = Math.Min(10, RecentCdromImages.Count); i < 10; i++)
                    config.RemoveKey("Floppy and CD-ROM drives", $"cdrom_01_image_history_{i + 1:D2}");

                for (int i = 1; i < _extraFloppyPaths.Length; i++)
                {
                    string key = $"fdd_{i + 1:D2}_fn";
                    if (string.IsNullOrEmpty(_extraFloppyPaths[i])) config.RemoveKey("Floppy and CD-ROM drives", key);
                    else config.SetString("Floppy and CD-ROM drives", key, _extraFloppyPaths[i]!);
                }
                for (int i = 1; i < _extraCdromPaths.Length; i++)
                {
                    string key = $"cdrom_{i + 1:D2}_image_path";
                    if (string.IsNullOrEmpty(_extraCdromPaths[i])) config.RemoveKey("Floppy and CD-ROM drives", key);
                    else config.SetString("Floppy and CD-ROM drives", key, _extraCdromPaths[i]!);
                }

                // Removable section
                if (string.IsNullOrEmpty(MountedRemovablePath)) config.RemoveKey("Other removable devices", "rdisk_01_image_path");
                else config.SetString("Other removable devices", "rdisk_01_image_path", MountedRemovablePath);
                config.RemoveKey("Other removable devices", "rdisk_01_fn");
                config.RemoveKey("Other removable devices", "rdisk_01_wp");
                for (int i = 0; i < Math.Min(10, RecentRemovableImages.Count); i++)
                {
                    config.SetString("Other removable devices", $"rdisk_01_image_history_{i + 1:D2}", RecentRemovableImages[i]);
                }

                config.Save(ConfigFilePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VirtualMachine] Error saving media config: {ex}");
                ConfigSaveFailed?.Invoke($"Could not save media changes to '{ConfigFilePath}': {ex.Message}");
            }
        }

        #endregion

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
