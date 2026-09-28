using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Box86_WinUI.Models;

namespace Box86_WinUI.Services
{
    public class ConfigService
    {
        private static readonly string DefaultVmRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
            "86Box VMs");

        public string GetDefaultVmDirectory()
        {
            if (!Directory.Exists(DefaultVmRoot))
                Directory.CreateDirectory(DefaultVmRoot);
            return DefaultVmRoot;
        }

        public List<VirtualMachine> ScanForVirtualMachines(string? rootPath = null)
        {
            rootPath ??= GetDefaultVmDirectory();
            var list = new List<VirtualMachine>();

            if (!Directory.Exists(rootPath))
                return list;

            try
            {
                // Each subdirectory with 86box.cfg is a VM
                foreach (var dir in Directory.GetDirectories(rootPath))
                {
                    string cfgPath = Path.Combine(dir, "86box.cfg");
                    if (File.Exists(cfgPath))
                    {
                        var vm = LoadVirtualMachine(dir);
                        if (vm != null) list.Add(vm);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Error scanning: {ex.Message}");
            }

            return list;
        }

        public VirtualMachine LoadVirtualMachine(string vmDir)
        {
            string dirName = Path.GetFileName(vmDir);
            string cfgPath = Path.Combine(vmDir, "86box.cfg");
            var config = MachineConfig.Load(cfgPath);

            string rawMachine = config.GetString("Machine", "machine", "ibm5150");
            string rawCpu = config.GetString("Machine", "cpu_family", "8088");
            int cpuSpeedHz = config.GetInt("Machine", "cpu_speed", 0);
            string rawFpu = config.GetString("Machine", "fpu_type", "");
            int memSizeKb = config.GetInt("Machine", "mem_size", 640);
            string rawVideo = config.GetString("Video", "gfxcard", "cga");
            string rawSound = config.GetString("Sound", "sndcard", "none");

            string configuredFamily = config.GetString("Machine", "machine_family", "");
            string machineFamily = !string.IsNullOrWhiteSpace(configuredFamily) && configuredFamily != "IBM PC / XT"
                ? configuredFamily
                : HardwareCatalog.ResolveMachinePlatform(rawMachine);

            var vm = new VirtualMachine
            {
                Path = vmDir,
                Name = dirName,
                MachineFamily = machineFamily,
                MachineModel = HardwareCatalog.ResolveMachineName(rawMachine),
                RawMachine = rawMachine,
                Cpu = HardwareCatalog.ResolveCpuName(rawCpu, cpuSpeedHz, rawFpu),
                RawCpu = rawCpu,
                MemoryKb = memSizeKb,
                MemoryMb = HardwareCatalog.ResolveMemoryMb(memSizeKb),
                VideoCard = HardwareCatalog.ResolveVideoName(rawVideo),
                RawVideo = rawVideo,
                SoundCard = HardwareCatalog.ResolveSoundName(rawSound),
                RawSound = rawSound
            };

            // Floppy drive resolution
            string fdd1 = config.GetString("Floppy and CD-ROM drives", "fdd_01_type", "");
            if (!string.IsNullOrEmpty(fdd1))
            {
                vm.FloppySummary = fdd1 switch
                {
                    "35_2hd" => "1.44M 3.5\"",
                    "35_2dd" => "720K 3.5\"",
                    "35_2ed" => "2.88M 3.5\"",
                    "525_2hd" => "1.2M 5.25\"",
                    "525_2dd" => "360K 5.25\"",
                    "none" => "None",
                    _ => fdd1
                };
            }

            // Hard disk drive resolution
            string hdd1 = config.GetString("Hard disks", "hdd_01_parameters", "");
            if (!string.IsNullOrEmpty(hdd1))
            {
                var parts = hdd1.Split(',');
                if (parts.Length >= 4 &&
                    int.TryParse(parts[0].Trim(), out int spt) &&
                    int.TryParse(parts[1].Trim(), out int hds) &&
                    int.TryParse(parts[2].Trim(), out int cyl))
                {
                    long totalSectors = (long)cyl * hds * spt;
                    long sizeMb = totalSectors / 2048;
                    string bus = parts[parts.Length - 1].Trim().ToUpperInvariant();
                    vm.HardDisksSummary = sizeMb >= 1000 ? $"{sizeMb / 1000.0:0.#} GB ({bus})" : $"{sizeMb} MB ({bus})";
                }
            }

            // CD-ROM drive resolution
            string cdBus = config.GetString("Floppy and CD-ROM drives", "cdrom_01_bus", "");
            string cdChannel = config.GetString("Floppy and CD-ROM drives", "cdrom_01_channel", "");
            string cdType = config.GetString("Floppy and CD-ROM drives", "cdrom_01_type", "");
            if (!string.IsNullOrEmpty(cdBus) && !cdBus.Equals("Disabled", StringComparison.OrdinalIgnoreCase) && !cdBus.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                string cdName = HardwareCatalog.GetCdromDisplayName(cdType);
                vm.CdRomSummary = !string.IsNullOrEmpty(cdChannel) ? $"{cdBus} {cdChannel}" : cdBus;
                if (!string.IsNullOrEmpty(cdName) && cdName != "None")
                {
                    vm.CdRomSummary += $" ({cdName})";
                }
            }
            else
            {
                vm.CdRomSummary = "None";
            }

            // Removable media quick-mount state & history resolution
            string floppyFn = config.GetString("Floppy and CD-ROM drives", "fdd_01_fn", "");
            bool floppyWp = config.GetBool("Floppy and CD-ROM drives", "fdd_01_wp", false);
            vm.MountedFloppyPath = string.IsNullOrWhiteSpace(floppyFn) ? null : floppyFn;
            vm.IsFloppyWriteProtected = floppyWp;
            for (int i = 1; i <= 10; i++)
            {
                string hist = config.GetString("Floppy and CD-ROM drives", $"fdd_01_image_history_{i:D2}", "");
                if (!string.IsNullOrWhiteSpace(hist) && !vm.RecentFloppyImages.Contains(hist))
                {
                    vm.RecentFloppyImages.Add(hist);
                }
            }

            string cdFn = config.GetString("Floppy and CD-ROM drives", "cdrom_01_image_path", config.GetString("Floppy and CD-ROM drives", "cdrom_01_fn", ""));
            vm.MountedCdromPath = string.IsNullOrWhiteSpace(cdFn) ? null : cdFn;
            for (int i = 1; i <= 10; i++)
            {
                string hist = config.GetString("Floppy and CD-ROM drives", $"cdrom_01_image_history_{i:D2}", "");
                if (!string.IsNullOrWhiteSpace(hist) && !vm.RecentCdromImages.Contains(hist))
                {
                    vm.RecentCdromImages.Add(hist);
                }
            }

            string rdiskFn = config.GetString("Other removable devices", "rdisk_01_fn", "");
            bool rdiskWp = config.GetBool("Other removable devices", "rdisk_01_wp", false);
            vm.MountedRemovablePath = string.IsNullOrWhiteSpace(rdiskFn) ? null : rdiskFn;
            vm.IsRemovableWriteProtected = rdiskWp;
            for (int i = 1; i <= 10; i++)
            {
                string hist = config.GetString("Other removable devices", $"rdisk_01_image_history_{i:D2}", "");
                if (!string.IsNullOrWhiteSpace(hist) && !vm.RecentRemovableImages.Contains(hist))
                {
                    vm.RecentRemovableImages.Add(hist);
                }
            }

            string rawNotes = config.GetString("General", "notes", config.GetString("Machine", "notes", ""));
            if (!string.IsNullOrEmpty(rawNotes))
            {
                vm.SetNotesWithoutSaving(rawNotes.Replace("\\n", Environment.NewLine));
            }

            // Check for screenshots in screenshots/ folder
            string screenshotDir = Path.Combine(vmDir, "screenshots");
            if (Directory.Exists(screenshotDir))
            {
                var screenFile = Directory.GetFiles(screenshotDir, "*.png")
                    .OrderByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();
                if (screenFile != null)
                    vm.ScreenshotPath = screenFile;
            }

            return vm;
        }

        /// <summary>
        /// Creates a VM folder with an authentic 86box.cfg. Returns null (and removes the folder)
        /// when the config would not be bootable, e.g. the machine's ROM set is missing.
        /// </summary>
        public VirtualMachine? TryCreateVirtualMachine(string name, MachinePreset preset, out CfgWriteResult result, string? targetRoot = null)
        {
            targetRoot ??= GetDefaultVmDirectory();
            string cleanName = SanitizeFolderName(name);
            string vmDir = Path.Combine(targetRoot, cleanName);

            int counter = 1;
            while (Directory.Exists(vmDir))
            {
                vmDir = Path.Combine(targetRoot, $"{cleanName} ({counter++})");
            }

            var settings = BuildSettingsFromPreset(preset);
            string cfgPath = Path.Combine(vmDir, "86box.cfg");

            Directory.CreateDirectory(vmDir);
            foreach (var hd in settings.HardDisks)
            {
                // A raw image the engine can open; NTFS allocates it lazily.
                string imagePath = Path.Combine(vmDir, hd.ImagePath);
                using var fs = new FileStream(imagePath, FileMode.CreateNew, FileAccess.Write);
                fs.SetLength(hd.SizeMb * 1024L * 1024L);
            }

            string romDir = GetRomDirectory();
            WriteGlobalConfig(romDir);
            result = new Cfg86Writer().Write(cfgPath, settings, romDir);
            foreach (var issue in result.Issues.Where(i => !i.IsError))
                System.Diagnostics.Debug.WriteLine($"[ConfigService] cfg warning [{issue.Section}] {issue.Key}: {issue.Message}");

            if (result.HasErrors || !File.Exists(cfgPath))
            {
                try { Directory.Delete(vmDir, true); } catch { }
                return null;
            }

            return LoadVirtualMachine(vmDir);
        }

        public VirtualMachine? CreateVirtualMachine(string name, MachinePreset preset, string? targetRoot = null)
        {
            return TryCreateVirtualMachine(name, preset, out _, targetRoot);
        }

        // Preset fields hold display labels; MemoryMb on a preset is actually KILOBYTES (mem_size).
        private static Cfg86Settings BuildSettingsFromPreset(MachinePreset preset)
        {
            var s = new Cfg86Settings
            {
                Machine = preset.MachineModel,
                CpuFamily = preset.Cpu,
                MemoryKb = preset.MemoryMb,
                VideoCard = preset.VideoCard,
                KeyboardType = string.Empty,
                MouseType = "none",
                DynamicRecompiler = true,
            };
            s.SoundCards[0] = preset.SoundCard;
            s.Floppies.Add(new FloppySetting { Type = preset.FloppyType });

            // Speed: take a number from the preset title ("Pentium 133", "Pentium II 400") that is a real
            // step of the resolved family; otherwise the family's first step (the engine's own default).
            var machine = Cfg86Resolver.Machine(preset.MachineModel);
            if (machine != null)
            {
                foreach (Match m in Regex.Matches(preset.Name, @"\d+(\.\d+)?"))
                {
                    if (Cfg86Resolver.Cpu(machine, preset.Cpu, m.Value, out _, out var step, out _) && step != null)
                    {
                        s.CpuSpeed = m.Value;
                        break;
                    }
                }
                // Otherwise leave the speed empty: the engine uses the family's first eligible CPU.
            }

            if (preset.HardDiskMb > 0)
            {
                s.HardDisks.Add(new HardDiskSetting { Bus = "ide", Channel = "0:0", SizeMb = preset.HardDiskMb, ImagePath = "drive_c.img" });
            }

            return s;
        }

        /// <summary>
        /// Permanently deletes a machine folder. The only requirement is that it is a direct child of the VM
        /// root (e.g. C:\Users\name\86Box VMs\HP): broken, empty or half-configured folders (only a log, nvr\)
        /// can be cleaned up too. Path traversal, the root itself, other locations and links are refused.
        /// A folder that is already gone counts as success.
        /// </summary>
        public void DeleteVirtualMachine(VirtualMachine vm, bool deleteFiles = true)
        {
            if (!deleteFiles) return;

            string raw = vm.Path ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
                throw new InvalidOperationException("The machine has no folder path.");

            // Path.GetFullPath would silently trim a trailing space/dot ("HP " -> "HP") and hit the wrong folder,
            // so the raw string is validated and then used verbatim through the \\?\ prefix.
            string path = raw.TrimEnd('\\', '/');
            if (path.Split('\\', '/').Any(seg => seg == ".." || seg == "."))
                throw new InvalidOperationException($"Refusing to delete '{raw}': path traversal is not allowed.");

            string root = DefaultVmRoot.TrimEnd('\\', '/');
            string prefix = root + System.IO.Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Refusing to delete '{raw}': it is not inside the VM folder '{root}'.");

            string relative = path.Substring(prefix.Length);
            if (relative.Length == 0 || relative.IndexOfAny(new[] { '\\', '/' }) >= 0)
                throw new InvalidOperationException($"Refusing to delete '{raw}': it must be a machine folder directly inside '{root}'.");

            string extended = @"\\?\" + path;
            if (!Directory.Exists(extended))
                return; // already removed from disk

            if ((new DirectoryInfo(extended).Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException($"Refusing to delete '{raw}': it is a link, not a machine folder.");

            // A file that was just written (the NVR save at shutdown) or is being scanned can stay open for a moment
            // after the machine stops, so a sharing violation is retried for a few seconds before it is reported.
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    ClearReadOnlyAttributes(extended);
                    Directory.Delete(extended, recursive: true);
                    break;
                }
                catch (IOException) when (attempt < 10)
                {
                    System.Threading.Thread.Sleep(400);
                }
                catch (UnauthorizedAccessException) when (attempt < 10)
                {
                    System.Threading.Thread.Sleep(400);
                }
            }

            if (Directory.Exists(extended))
                throw new IOException($"'{raw}' could not be fully deleted; a file may still be in use.");
        }

        private static void ClearReadOnlyAttributes(string dir)
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                var attrs = File.GetAttributes(f);
                if ((attrs & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(f, attrs & ~FileAttributes.ReadOnly);
            }
        }

        /// <summary>
        /// A name that is safe as a Windows folder: invalid characters, control characters and leading/trailing
        /// whitespace or dots removed (a trailing space produces a second, near-identical folder in Explorer),
        /// reserved device names avoided.
        /// </summary>
        public static string SanitizeFolderName(string? name)
        {
            string s = string.Join("_", (name ?? string.Empty).Split(System.IO.Path.GetInvalidFileNameChars()));
            s = new string(s.Where(c => !char.IsControl(c)).ToArray());
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim().TrimEnd('.', ' ').TrimStart('.', ' ');

            var reserved = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                                   "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
            if (s.Length == 0 || reserved.Contains(s.Split('.')[0], StringComparer.OrdinalIgnoreCase))
                s = "VM" + (s.Length > 0 ? "_" + s : string.Empty);
            return s;
        }

        private static readonly string ManagerConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "86Box");

        private static readonly string ManagerConfigPath = Path.Combine(ManagerConfigDir, "86box.manager.ini");

        /// <summary>Global (per-user, not per-VM) config next to the manager settings.</summary>
        public static string GlobalConfigPath => Path.Combine(ManagerConfigDir, "86box_global.cfg");

        /// <summary>
        /// Records the ROM directory under [General] rom_path of 86box_global.cfg so machines created without
        /// their own ROM setting inherit the configured BIOS directory.
        /// </summary>
        public void WriteGlobalConfig(string romPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(romPath)) return;
                Directory.CreateDirectory(ManagerConfigDir);
                var g = File.Exists(GlobalConfigPath) ? MachineConfig.Load(GlobalConfigPath) : new MachineConfig();
                g.SetString("General", "rom_path", romPath);
                g.Save(GlobalConfigPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] WriteGlobalConfig failed: {ex.Message}");
            }
        }

        public string GetRomDirectory()
        {
            try
            {
                if (File.Exists(ManagerConfigPath))
                {
                    var config = MachineConfig.Load(ManagerConfigPath);
                    string romPath = config.GetString("General", "rom_path", "");
                    if (!string.IsNullOrWhiteSpace(romPath) && Directory.Exists(romPath))
                        return romPath;
                }

                if (File.Exists(GlobalConfigPath))
                {
                    string globalRom = MachineConfig.Load(GlobalConfigPath).GetString("General", "rom_path", "");
                    if (!string.IsNullOrWhiteSpace(globalRom) && Directory.Exists(globalRom))
                        return globalRom;
                }

                // Check default and installed locations
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                var candidates = new List<string>
                {
                    @"C:\86Box-Windows-64-b9001\roms",
                    @"C:\86box-build-mingw2\roms",
                    @"C:\86Box\roms",
                    Path.Combine(appDir, "roms"),
                    Path.Combine(appDir, "..", "..", "..", "..", "roms"),
                    Path.Combine(ManagerConfigDir, "roms")
                };

                // Search C:\ for 86Box directories
                try
                {
                    if (Directory.Exists(@"C:\"))
                    {
                        foreach (var dir in Directory.GetDirectories(@"C:\", "*86Box*"))
                        {
                            string romsSub = Path.Combine(dir, "roms");
                            if (!candidates.Contains(romsSub))
                                candidates.Add(romsSub);
                        }
                    }
                }
                catch { }

                foreach (var candidate in candidates)
                {
                    if (Directory.Exists(candidate))
                    {
                        var validation = ValidateRomDirectory(candidate);
                        if (validation.IsValid)
                        {
                            return Path.GetFullPath(candidate);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Error reading ROM directory: {ex.Message}");
            }

            return string.Empty;
        }

        public bool GetDriveSeekSounds()
        {
            try
            {
                if (File.Exists(ManagerConfigPath))
                    return MachineConfig.Load(ManagerConfigPath).GetBool("General", "drive_seek_sounds", false);
            }
            catch { }
            return false;
        }

        public void SaveDriveSeekSounds(bool enabled)
        {
            try
            {
                if (!Directory.Exists(ManagerConfigDir))
                    Directory.CreateDirectory(ManagerConfigDir);

                var config = File.Exists(ManagerConfigPath)
                    ? MachineConfig.Load(ManagerConfigPath)
                    : new MachineConfig();

                config.SetString("General", "drive_seek_sounds", enabled ? "1" : "0");
                config.Save(ManagerConfigPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Error saving drive sound setting: {ex.Message}");
            }
        }

        // CRT post-process filter (WinUI viewport rendering only - not an authentic 86box.cfg key,
        // so it lives in the manager ini alongside drive_seek_sounds, applied to every VM the same
        // way). 0 = None (Sharp), 1 = Scanlines (Flat), 2 = Authentic CRT (Curved Glass).
        public int GetCrtFilterMode()
        {
            try
            {
                if (File.Exists(ManagerConfigPath))
                    return Math.Clamp(MachineConfig.Load(ManagerConfigPath).GetInt("Display", "crt_filter_mode", 0), 0, 2);
            }
            catch { }
            return 0;
        }

        public void SaveCrtFilterMode(int mode)
        {
            try
            {
                if (!Directory.Exists(ManagerConfigDir))
                    Directory.CreateDirectory(ManagerConfigDir);

                var config = File.Exists(ManagerConfigPath) ? MachineConfig.Load(ManagerConfigPath) : new MachineConfig();
                config.SetString("Display", "crt_filter_mode", Math.Clamp(mode, 0, 2).ToString());
                config.Save(ManagerConfigPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Error saving CRT filter mode: {ex.Message}");
            }
        }

        /// <summary>0.0 .. 0.15 (0% - 15% screen curvature).</summary>
        public double GetScreenCurvature()
        {
            try
            {
                if (File.Exists(ManagerConfigPath))
                    return Math.Clamp(MachineConfig.Load(ManagerConfigPath).GetDouble("Display", "crt_curvature", 0.06), 0.0, 0.15);
            }
            catch { }
            return 0.06;
        }

        public void SaveScreenCurvature(double amount)
        {
            try
            {
                if (!Directory.Exists(ManagerConfigDir))
                    Directory.CreateDirectory(ManagerConfigDir);

                var config = File.Exists(ManagerConfigPath) ? MachineConfig.Load(ManagerConfigPath) : new MachineConfig();
                config.SetString("Display", "crt_curvature", Math.Clamp(amount, 0.0, 0.15).ToString(System.Globalization.CultureInfo.InvariantCulture));
                config.Save(ManagerConfigPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Error saving screen curvature: {ex.Message}");
            }
        }

        /// <summary>0.0 .. 1.0 phosphor bloom intensity.</summary>
        public double GetPhosphorBloom()
        {
            try
            {
                if (File.Exists(ManagerConfigPath))
                    return Math.Clamp(MachineConfig.Load(ManagerConfigPath).GetDouble("Display", "crt_bloom", 0.35), 0.0, 1.0);
            }
            catch { }
            return 0.35;
        }

        public void SavePhosphorBloom(double amount)
        {
            try
            {
                if (!Directory.Exists(ManagerConfigDir))
                    Directory.CreateDirectory(ManagerConfigDir);

                var config = File.Exists(ManagerConfigPath) ? MachineConfig.Load(ManagerConfigPath) : new MachineConfig();
                config.SetString("Display", "crt_bloom", Math.Clamp(amount, 0.0, 1.0).ToString(System.Globalization.CultureInfo.InvariantCulture));
                config.Save(ManagerConfigPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Error saving phosphor bloom: {ex.Message}");
            }
        }

        public void SaveRomDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(ManagerConfigDir))
                {
                    Directory.CreateDirectory(ManagerConfigDir);
                }

                var config = File.Exists(ManagerConfigPath)
                    ? MachineConfig.Load(ManagerConfigPath)
                    : new MachineConfig();

                config.SetString("General", "rom_path", path);
                config.Save(ManagerConfigPath);
                WriteGlobalConfig(path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Error saving ROM directory: {ex.Message}");
            }
        }

        public RomValidationResult ValidateRomDirectory(string? path)
        {
            var result = new RomValidationResult();

            if (string.IsNullOrWhiteSpace(path))
            {
                result.IsValid = false;
                result.IsConfigured = false;
                result.Message = "No system firmware directory configured. Select the folder containing 86Box BIOS and ROM files.";
                result.BadgeText = "Not configured";
                result.BadgeStyleKey = "AttentionDotInfoBadgeStyle";
                return result;
            }

            result.IsConfigured = true;

            if (!Directory.Exists(path))
            {
                result.IsValid = false;
                result.Message = $"The specified directory does not exist: {path}";
                result.BadgeText = "Missing folder";
                result.BadgeStyleKey = "CriticalDotInfoBadgeStyle";
                return result;
            }

            // Detect whether user picked root containing a 'roms' folder or the 'roms' folder itself
            string checkRoot = path;
            if (Directory.Exists(Path.Combine(path, "roms")))
            {
                checkRoot = Path.Combine(path, "roms");
            }

            bool hasMachines = Directory.Exists(Path.Combine(checkRoot, "machines"));
            bool hasVideo = Directory.Exists(Path.Combine(checkRoot, "video"));
            bool hasSound = Directory.Exists(Path.Combine(checkRoot, "sound"));
            bool hasNetwork = Directory.Exists(Path.Combine(checkRoot, "network"));
            bool hasScsi = Directory.Exists(Path.Combine(checkRoot, "scsi"));
            bool hasFloppy = Directory.Exists(Path.Combine(checkRoot, "floppy"));

            if (hasMachines) result.DetectedComponents.Add("Machines / BIOS");
            if (hasVideo) result.DetectedComponents.Add("Video cards");
            if (hasSound) result.DetectedComponents.Add("Sound cards");
            if (hasNetwork) result.DetectedComponents.Add("Network adapters");
            if (hasScsi) result.DetectedComponents.Add("SCSI controllers");
            if (hasFloppy) result.DetectedComponents.Add("Floppy / Disks");

            if (hasMachines || hasVideo)
            {
                result.IsValid = true;
                result.BadgeText = "Valid";
                result.BadgeStyleKey = "SuccessDotInfoBadgeStyle";
                result.Message = $"Valid 86Box ROM structure detected ({string.Join(", ", result.DetectedComponents)}).";
            }
            else
            {
                result.IsValid = false;
                result.BadgeText = "Invalid structure";
                result.BadgeStyleKey = "CriticalDotInfoBadgeStyle";
                result.Message = "Missing standard 86Box firmware folders. Expected 'machines' or 'video' subdirectories.";
            }

            return result;
        }
    }
}
