using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Box86_WinUI.Models;

namespace Box86_WinUI.Services
{
    public sealed record CfgIssue(string Section, string Key, string Message, bool IsError);

    public sealed class CfgWriteResult
    {
        public List<CfgIssue> Issues { get; } = new();
        public bool Written { get; set; }
        public bool HasErrors => Issues.Any(i => i.IsError);
        public string Summary => string.Join(Environment.NewLine, Issues.Select(i => $"[{i.Section}] {i.Key}: {i.Message}"));
    }

    public sealed class FloppySetting
    {
        public string Type { get; set; } = "none";
        public string ImagePath { get; set; } = string.Empty;
        public bool Turbo { get; set; }
        public bool CheckBpb { get; set; } = true;
    }

    public sealed class CdromSetting
    {
        public string Bus { get; set; } = "none";
        public string Channel { get; set; } = "0:0";
        public int Speed { get; set; } = 8;
        public string Type { get; set; } = "86cd";
        public bool SoundOn { get; set; } = true;
        public string ImagePath { get; set; } = string.Empty;
    }

    public sealed class HardDiskSetting
    {
        public string Bus { get; set; } = "ide";
        public string Channel { get; set; } = "0:0";
        public int Cylinders { get; set; }
        public int Heads { get; set; }
        public int Sectors { get; set; }
        public int SizeMb { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
    }

    /// <summary>
    /// Everything the writer needs. Any identifier-valued field may hold either the engine's
    /// internal name or a UI display label; the writer resolves it through the generated
    /// authentic catalog and refuses to emit anything it cannot resolve.
    /// </summary>
    public sealed partial class Cfg86Settings
    {
        // [Machine]
        public string Machine { get; set; } = string.Empty;
        public string CpuFamily { get; set; } = string.Empty;
        public string CpuSpeed { get; set; } = string.Empty;
        /// <summary>86box.cfg mem_size is in KILOBYTES.</summary>
        public int MemoryKb { get; set; } = 64;
        public string Fpu { get; set; } = "none";
        public bool DynamicRecompiler { get; set; }
        public bool SoftFloat { get; set; }
        public string TimeSync { get; set; } = "local";
        public string PitMode { get; set; } = "auto";
        public int WaitStates { get; set; }
        public bool OverrideInterpreter { get; set; }
        public bool AccurateFpuEnvironment { get; set; }
        /// <summary>"Larger frames (less smooth)": force_10ms = 1 under [General].</summary>
        public bool LargerFrames { get; set; }

        // [Video]
        public string VideoCard { get; set; } = string.Empty;
        public string SecondaryVideoCard { get; set; } = "none";
        public bool Voodoo { get; set; }
        public bool Ibm8514 { get; set; }
        public bool Xga { get; set; }
        public bool Da2 { get; set; }

        // [Input devices]
        public string KeyboardType { get; set; } = string.Empty;
        public string MouseType { get; set; } = "none";
        public string TabletType { get; set; } = "none";

        // [Sound]
        public string[] SoundCards { get; set; } = { "none", "none", "none", "none" };
        public string MidiDevice { get; set; } = "none";
        /// <summary>The MIDI input device (midi_in_device under [Sound]); "none" = key absent.</summary>
        public string MidiInDevice { get; set; } = "none";

        // [Storage controllers]
        public string FloppyController { get; set; } = "internal";
        /// <summary>The CD-ROM interface card (cdrom_interface under [Storage controllers]); "none" = key absent.</summary>
        public string CdromInterface { get; set; } = "none";
        public string[] HdControllers { get; set; } = { string.Empty, "none", "none", "none" };
        public string[] ScsiControllers { get; set; } = { "none", "none", "none", "none" };
        public bool Cassette { get; set; }

        // [Floppy and CD-ROM drives] / [Hard disks]
        public List<FloppySetting> Floppies { get; } = new();
        public List<CdromSetting> Cdroms { get; } = new();
        public List<HardDiskSetting> HardDisks { get; } = new();
    }

    /// <summary>Answer from the engine's own machine_available() for a ROM set.</summary>
    public enum RomStatus { Available, Missing, UnknownMachine, Unverifiable }

    public interface IRomAvailability
    {
        RomStatus Check(string romPath, string machineInternalName);
    }

    /// <summary>Asks 86box_core.dll (the real machine table + ROM search) whether a machine's ROMs exist.</summary>
    public sealed class NativeRomAvailability : IRomAvailability
    {
        public RomStatus Check(string romPath, string machineInternalName)
        {
            try
            {
                int r = Interop.Box86CoreNative.EmuCheckMachineRoms(romPath, machineInternalName);
                return r switch
                {
                    1 => RomStatus.Available,
                    0 => RomStatus.Missing,
                    -1 => RomStatus.UnknownMachine,
                    _ => RomStatus.Unverifiable // -2: engine busy with a running VM
                };
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                return RomStatus.Unverifiable;
            }
        }
    }

    /// <summary>
    /// Serializes a VM to an authentic 86box.cfg: only keys the engine's config.c reads, only
    /// internal identifiers (machine, cpu_family, gfxcard, sndcard, ...) and only the value
    /// formats its own save_*() functions produce. Existing keys the app does not manage (window
    /// state, printer settings, ...) are preserved.
    /// </summary>
    public sealed partial class Cfg86Writer
    {
        // Keys earlier versions of this app invented; the engine never reads them.
        private static readonly (string Section, string Key)[] LegacyInvented =
        {
            ("Machine", "machine_type"), ("Machine", "wait_states"), ("Machine", "override_interpreter"),
            ("Machine", "accurate_fpu"), ("Machine", "cpu_frame_size"),
            ("Video", "secondary_gfxcard"), ("Video", "ibm_8514"), ("Video", "renderer"), ("Video", "vsync"),
            ("Video", "aspect_ratio"), ("Video", "gfxcard_2_legacy"),
            ("Storage controllers", "cdrom"), ("Storage controllers", "hdc_0"),
            ("Hard disks", "count"),
            ("Floppy and CD-ROM drives", "fdd_01_wp"), ("Floppy and CD-ROM drives", "cdrom_01_fn"),
        };

        // "voodoo": an earlier version of this app wrote the Voodoo options there; the engine uses [3Dfx Voodoo Graphics].
        private static readonly string[] LegacySections = { "Mouse", "Input", "Ports", "voodoo" };

        // Relative image paths are resolved by the engine against the VM folder.
        [ThreadStatic] private static string? _vmDir;

        private static string Absolute(string path) =>
            Path.IsPathRooted(path) || _vmDir == null ? path : Path.Combine(_vmDir, path);

        private readonly IRomAvailability _roms;

        public Cfg86Writer(IRomAvailability? roms = null)
        {
            _roms = roms ?? new NativeRomAvailability();
        }

        public CfgWriteResult Write(string cfgPath, Cfg86Settings s, string? romPath, bool validateRoms = true)
        {
            var result = new CfgWriteResult();
            _vmDir = Path.GetDirectoryName(Path.GetFullPath(cfgPath));
            var config = File.Exists(cfgPath) ? MachineConfig.Load(cfgPath) : new MachineConfig();

            // ---- Resolve the machine first: everything else depends on it.
            var machine = Cfg86Resolver.Machine(s.Machine);
            if (machine == null)
            {
                result.Issues.Add(new("Machine", "machine", $"'{s.Machine}' is not a machine in the 86Box catalog.", true));
                return result;
            }

            // ---- ROM validation before touching the file.
            if (validateRoms)
            {
                if (string.IsNullOrWhiteSpace(romPath) || !Directory.Exists(romPath))
                {
                    result.Issues.Add(new("General", "rom_path", "No valid ROM directory is configured (Manager Settings > System ROMs Path); machine ROMs cannot be verified.", true));
                    return result;
                }

                switch (_roms.Check(romPath, machine.Info.InternalName))
                {
                    case RomStatus.Missing:
                        result.Issues.Add(new("Machine", "machine", $"The ROM set for '{machine.Info.Name}' ({machine.Info.InternalName}) was not found in '{romPath}'. The VM would fail to boot.", true));
                        return result;
                    case RomStatus.UnknownMachine:
                        result.Issues.Add(new("Machine", "machine", $"The engine does not know machine '{machine.Info.InternalName}'.", true));
                        return result;
                    case RomStatus.Unverifiable:
                        result.Issues.Add(new("Machine", "machine", "ROM availability could not be verified (engine unavailable or busy); writing anyway.", false));
                        break;
                }
            }

            foreach (var (section, key) in LegacyInvented)
                config.RemoveKey(section, key);
            foreach (var section in LegacySections)
                config.RemoveSection(section);

            // [General]: the engine owns rendering/window keys; only stamp the config version.
            config.SetString("General", "version", "7");
            OptionalInt(config, "General", "force_10ms", s.LargerFrames ? 1 : 0, 0);

            WriteMachine(config, s, machine, result);
            WriteVideo(config, s, machine, result, romPath);
            WriteMonitorAndDeviceSettings(config, s, result);
            WriteInput(config, s, result);
            WriteSound(config, s, result);
            WriteStorageControllers(config, s, machine, result);
            WriteFloppyAndCdrom(config, s, result);
            WriteHardDisks(config, s, result);
            WriteNetwork(config, s, result);
            WritePorts(config, s, result);
            WriteRemovableDevices(config, s, result);
            WriteOtherPeripherals(config, s, result);

            if (result.HasErrors)
                return result;

            // Standard section order, then anything else the engine had saved.
            // The engine's own config_save() order.
            string[] order = { "General", "Machine", "Video", "Input devices", "Sound", "Network", "Ports (COM & LPT)", "Storage controllers", "Hard disks", "Floppy and CD-ROM drives", "Other removable devices", "Other peripherals" };
            var ordered = new MachineConfig();
            foreach (var name in order)
                if (config.Sections.TryGetValue(name, out var sec) && sec.Count > 0)
                    foreach (var kv in sec.OrderBy(k => k.Key, StringComparer.Ordinal))
                        ordered.SetString(name, kv.Key, kv.Value);
            foreach (var kv in config.Sections.Where(k => !order.Contains(k.Key, StringComparer.OrdinalIgnoreCase) && k.Value.Count > 0))
                foreach (var item in kv.Value)
                    ordered.SetString(kv.Key, item.Key, item.Value);

            // Two drives on one IDE channel / SCSI ID corrupt the engine's heap on stop or reset; never write that.
            foreach (var change in Cfg86ChannelFixer.Fix(ordered))
                result.Issues.Add(new("Drives", "channel", change + ".", false));

            ordered.Save(cfgPath);
            result.Written = true;
            return result;
        }

        // ------------------------------------------------------------------ [Machine]
        private static void WriteMachine(MachineConfig c, Cfg86Settings s, ResolvedMachine machine, CfgWriteResult r)
        {
            const string sec = "Machine";
            c.SetString(sec, "machine", machine.Info.InternalName);

            if (Cfg86Resolver.Cpu(machine, s.CpuFamily, s.CpuSpeed, out var family, out var step, out var cpuError))
            {
                c.SetString(sec, "cpu_family", family);
                if (step is { } st)
                {
                    if (Cfg86Resolver.StepEligible(machine, family, st) == false)
                    {
                        double fsb = st.Multi > 0 ? st.RSpeedHz / st.Multi / 1e6 : 0;
                        r.Issues.Add(new(sec, "cpu_speed", $"{st.Name} MHz ({st.Multi:0.0#}x, {fsb:0.##} MHz FSB) is not supported by '{machine.Info.Name}': the engine rejects that bus speed / multiplier / voltage combination.", true));
                    }
                    c.SetString(sec, "cpu_speed", st.RSpeedHz.ToString(CultureInfo.InvariantCulture));
                    c.SetString(sec, "cpu_multi", st.Multi.ToString("0.0###", CultureInfo.InvariantCulture));
                }
                else
                {
                    // No speed chosen: config.c then uses the family's first eligible CPU.
                    c.RemoveKey(sec, "cpu_speed");
                    c.RemoveKey(sec, "cpu_multi");
                }
            }
            else
            {
                // config.c falls back to the first eligible CPU, so a bad CPU is not fatal, but say so.
                r.Issues.Add(new(sec, "cpu_family", cpuError, false));
                c.RemoveKey(sec, "cpu_family");
                c.RemoveKey(sec, "cpu_speed");
                c.RemoveKey(sec, "cpu_multi");
            }

            // mem_size is in KILOBYTES in 86box.cfg.
            long memKb = Math.Max(1, (long)s.MemoryKb);
            long maxKb = machine.Info.MaxRamKb > 0 ? machine.Info.MaxRamKb : (long)machine.Info.MaxRamMb * 1024;
            if (maxKb > 0 && memKb > maxKb)
            {
                r.Issues.Add(new(sec, "mem_size", $"{memKb} KB exceeds this machine's maximum; clamped to {maxKb} KB.", false));
                memKb = maxKb;
            }
            c.SetString(sec, "mem_size", memKb.ToString(CultureInfo.InvariantCulture));

            c.SetBool(sec, "cpu_use_dynarec", s.DynamicRecompiler);

            string? fpu = Cfg86Resolver.Fpu(s.Fpu);
            if (fpu == null) c.RemoveKey(sec, "fpu_type"); else c.SetString(sec, "fpu_type", fpu);

            OptionalInt(c, sec, "fpu_softfloat", s.SoftFloat ? 1 : 0, 0);
            OptionalInt(c, sec, "cpu_waitstates", s.WaitStates, 0);
            OptionalInt(c, sec, "cpu_override_interpreter", s.OverrideInterpreter ? 1 : 0, 0);
            OptionalInt(c, sec, "cpu_dyn_accurate_fpu_env", s.AccurateFpuEnvironment ? 1 : 0, 0);

            // time_sync: only "disabled" and "utc" are stored; "local" is the default (absent).
            string ts = Cfg86Resolver.Norm(s.TimeSync);
            if (ts.StartsWith("disab") || ts == "off") c.SetString(sec, "time_sync", "disabled");
            else if (ts == "utc" || ts == "gmt") c.SetString(sec, "time_sync", "utc");
            else c.RemoveKey(sec, "time_sync");

            // pit_mode: -1 (auto) is stored as absence; 0 = standard, 1 = fast.
            string pm = Cfg86Resolver.Norm(s.PitMode);
            if (pm.StartsWith("slow") || pm.StartsWith("standard") || pm == "0") c.SetInt(sec, "pit_mode", 0);
            else if (pm.StartsWith("fast") || pm == "1") c.SetInt(sec, "pit_mode", 1);
            else c.RemoveKey(sec, "pit_mode");
        }

        // ------------------------------------------------------------------ [Video]
        private static void WriteVideo(MachineConfig c, Cfg86Settings s, ResolvedMachine machine, CfgWriteResult r, string? romPath = null)
        {
            const string sec = "Video";
            string? gfx = Cfg86Resolver.Device("video", s.VideoCard);
            if (gfx == null)
            {
                r.Issues.Add(new(sec, "gfxcard", $"'{s.VideoCard}' is not a video card known to the engine; leaving the machine default.", false));
                c.RemoveKey(sec, "gfxcard");
            }
            else
            {
                c.SetString(sec, "gfxcard", gfx);

                // A card whose BIOS ROM is missing is replaced by the engine at start and the screen stays black.
                if (gfx != "none" && !string.IsNullOrWhiteSpace(romPath) && System.IO.Directory.Exists(romPath))
                {
                    int status;
                    try { status = Box86_WinUI.Interop.Box86CoreNative.EmuCheckVideoCard(romPath, gfx); }
                    catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException) { status = -2; }

                    if (status == 0)
                        r.Issues.Add(new(sec, "gfxcard", $"The video card '{s.VideoCard}' ({gfx}) cannot be used: its BIOS ROM is missing from '{romPath}\\video'. The machine would start with a black screen. Choose another card or add the ROM.", true));
                    else if (status == -1 && gfx != "internal")
                        r.Issues.Add(new(sec, "gfxcard", $"'{s.VideoCard}' ({gfx}) is not a video card this 86Box build offers (it is a chip or on-board part, not an add-in card). The machine would start with a black screen. Choose another card.", true));
                }
            }

            string? gfx2 = Cfg86Resolver.Device("video", s.SecondaryVideoCard);
            if (gfx2 == null || gfx2 == "none") c.RemoveKey(sec, "gfxcard_2"); else c.SetString(sec, "gfxcard_2", gfx2);

            OptionalInt(c, sec, "voodoo", s.Voodoo ? 1 : 0, 0);
            OptionalInt(c, sec, "8514a", s.Ibm8514 ? 1 : 0, 0);
            OptionalInt(c, sec, "xga", s.Xga ? 1 : 0, 0);
            OptionalInt(c, sec, "da2", s.Da2 ? 1 : 0, 0);
        }

        // ------------------------------------------------------------------ [Input devices]
        private static void WriteInput(MachineConfig c, Cfg86Settings s, CfgWriteResult r)
        {
            const string sec = "Input devices";
            // An unspecified keyboard is left to the engine's per-machine default (never "none").
            if (string.IsNullOrWhiteSpace(s.KeyboardType)) c.RemoveKey(sec, "keyboard_type");
            else Required(c, r, sec, "keyboard_type", "input", s.KeyboardType);
            Required(c, r, sec, "mouse_type", "input", s.MouseType);
            Required(c, r, sec, "tablet_type", "input", s.TabletType);
        }

        // ------------------------------------------------------------------ [Sound]
        private static void WriteSound(MachineConfig c, Cfg86Settings s, CfgWriteResult r)
        {
            const string sec = "Sound";
            string[] keys = { "sndcard", "sndcard2", "sndcard3", "sndcard4" };
            for (int i = 0; i < keys.Length; i++)
            {
                string raw = i < s.SoundCards.Length ? s.SoundCards[i] : "none";
                string? dev = Cfg86Resolver.Device("sound", raw);
                if (dev == null)
                    r.Issues.Add(new(sec, keys[i], $"'{raw}' is not a sound card known to the engine; ignored.", false));
                if (dev == null || dev == "none") c.RemoveKey(sec, keys[i]); else c.SetString(sec, keys[i], dev);
            }

            string? midi = Cfg86Resolver.Device("midi_out", s.MidiDevice);
            if (midi == null || midi == "none") c.RemoveKey(sec, "midi_device"); else c.SetString(sec, "midi_device", midi);

            string? midiIn = Cfg86Resolver.Device("midi_in", s.MidiInDevice);
            if (midiIn == null || midiIn == "none") c.RemoveKey(sec, "midi_in_device"); else c.SetString(sec, "midi_in_device", midiIn);
        }

        // ------------------------------------------------------------------ [Storage controllers]
        private static void WriteStorageControllers(MachineConfig c, Cfg86Settings s, ResolvedMachine machine, CfgWriteResult r)
        {
            const string sec = "Storage controllers";

            string? fdc = Cfg86Resolver.Device("fdc", s.FloppyController);
            if (fdc == null) r.Issues.Add(new(sec, "fdc", $"'{s.FloppyController}' is not a floppy controller known to the engine; ignored.", false));
            if (fdc == null || fdc == "internal") c.RemoveKey(sec, "fdc"); else c.SetString(sec, "fdc", fdc);

            string? cdif = Cfg86Resolver.Device("cdrom_if", s.CdromInterface);
            if (cdif == null && !Cfg86Resolver.IsNoneLabel(s.CdromInterface))
                r.Issues.Add(new(sec, "cdrom_interface", $"'{s.CdromInterface}' is not a CD-ROM interface known to the engine; ignored.", false));
            if (cdif == null || cdif == "none") c.RemoveKey(sec, "cdrom_interface"); else c.SetString(sec, "cdrom_interface", cdif);

            for (int i = 0; i < 4; i++)
            {
                string key = $"hdc_{i + 1}";
                string raw = i < s.HdControllers.Length ? s.HdControllers[i] : "none";
                string? dev = string.IsNullOrWhiteSpace(raw) ? null : Cfg86Resolver.Device("hdc", raw);
                if (!string.IsNullOrWhiteSpace(raw) && dev == null)
                    r.Issues.Add(new(sec, key, $"'{raw}' is not a hard disk controller known to the engine; ignored.", false));

                // config.c drops the key when it equals the default (internal for slot 1 on
                // machines with a built-in controller, none otherwise).
                string def = i == 0 ? "internal" : "none";
                if (dev == null || dev == def) c.RemoveKey(sec, key); else c.SetString(sec, key, dev);
            }

            for (int i = 0; i < 4; i++)
            {
                string key = $"scsicard_{i + 1}";
                string raw = i < s.ScsiControllers.Length ? s.ScsiControllers[i] : "none";
                string? dev = Cfg86Resolver.Device("scsi", raw);
                if (dev == null)
                    r.Issues.Add(new(sec, key, $"'{raw}' is not a SCSI controller known to the engine; ignored.", false));
                if (dev == null || dev == "none") c.RemoveKey(sec, key); else c.SetString(sec, key, dev);
            }

            OptionalInt(c, sec, "cassette_enabled", s.Cassette ? 1 : 0, 0);
        }

        // ------------------------------------------------------------------ [Floppy and CD-ROM drives]
        private static void WriteFloppyAndCdrom(MachineConfig c, Cfg86Settings s, CfgWriteResult r)
        {
            const string sec = "Floppy and CD-ROM drives";

            for (int i = 0; i < s.Floppies.Count && i < 4; i++)
            {
                var f = s.Floppies[i];
                string n = $"fdd_{i + 1:D2}_";
                string? type = Cfg86Resolver.FloppyType(f.Type);
                if (type == null)
                {
                    r.Issues.Add(new(sec, n + "type", $"'{f.Type}' is not a floppy drive type known to the engine; ignored.", false));
                    c.RemoveKey(sec, n + "type");
                }
                else
                {
                    string def = i < 2 ? "525_2dd" : "none";
                    if (type == def) c.RemoveKey(sec, n + "type"); else c.SetString(sec, n + "type", type);
                }

                // Image paths stay empty unless explicitly assigned; host drive mappings are never written.
                AssignedImage(c, r, sec, n + "fn", f.ImagePath);
                c.RemoveKey(sec, n + "writeprot");
                c.RemoveKey(sec, n + "host_device");
                OptionalInt(c, sec, n + "turbo", f.Turbo ? 1 : 0, 0);
                OptionalInt(c, sec, n + "check_bpb", f.CheckBpb ? 1 : 0, 1);
            }

            for (int i = 0; i < s.Cdroms.Count && i < 8; i++)
            {
                var cd = s.Cdroms[i];
                string n = $"cdrom_{i + 1:D2}_";
                string bus = Cfg86Resolver.CdromBus(cd.Bus);

                c.RemoveKey(sec, n + "host_drive");
                c.RemoveKey(sec, n + "scsi_id");

                if (bus == "none")
                {
                    foreach (var suffix in new[] { "parameters", "speed", "type", "ide_channel", "scsi_location", "mke_channel", "image_path", "no_check" })
                        c.RemoveKey(sec, n + suffix);
                    continue;
                }

                c.SetString(sec, n + "parameters", $"{(cd.SoundOn ? 1 : 0)}, {bus}");
                if (cd.Speed == 8 || cd.Speed <= 0) c.RemoveKey(sec, n + "speed"); else c.SetInt(sec, n + "speed", cd.Speed);

                string? type = Cfg86Resolver.CdromType(cd.Type);
                if (type == null || type == "86cd" || bus == "mitsumi") c.RemoveKey(sec, n + "type"); else c.SetString(sec, n + "type", type);

                c.RemoveKey(sec, n + "ide_channel");
                c.RemoveKey(sec, n + "scsi_location");
                c.RemoveKey(sec, n + "mke_channel");
                if (bus == "atapi") c.SetString(sec, n + "ide_channel", Cfg86Resolver.IdeChannel(cd.Channel));
                else if (bus == "scsi") c.SetString(sec, n + "scsi_location", Cfg86Resolver.ScsiLocation(cd.Channel));
                else if (bus == "mke") c.SetInt(sec, n + "mke_channel", Cfg86Resolver.SecondNumber(cd.Channel));

                AssignedImage(c, r, sec, n + "image_path", cd.ImagePath);
            }
        }

        // ------------------------------------------------------------------ [Hard disks]
        private static void WriteHardDisks(MachineConfig c, Cfg86Settings s, CfgWriteResult r)
        {
            const string sec = "Hard disks";
            for (int i = 0; i < s.HardDisks.Count && i < 30; i++)
            {
                var d = s.HardDisks[i];
                string n = $"hdd_{i + 1:D2}_";
                string bus = Cfg86Resolver.HddBus(d.Bus);
                if (bus == "none")
                {
                    r.Issues.Add(new(sec, n + "parameters", $"Unsupported bus '{d.Bus}'; hard disk {i + 1} skipped.", false));
                    continue;
                }

                int spt = d.Sectors, hpc = d.Heads, tracks = d.Cylinders;
                if (spt <= 0 || hpc <= 0 || tracks <= 0)
                {
                    if (d.SizeMb <= 0)
                    {
                        r.Issues.Add(new(sec, n + "parameters", $"Hard disk {i + 1} has no geometry and no size; skipped.", false));
                        continue;
                    }
                    spt = 63; hpc = 16;
                    tracks = (int)Math.Clamp(d.SizeMb * 1024L * 1024L / 512 / (spt * hpc), 1, 16383);
                }

                // "spt, hpc, tracks, write-protect, bus"
                c.SetString(sec, n + "parameters", $"{spt}, {hpc}, {tracks}, 0, {bus}");

                foreach (var suffix in new[] { "mfm_channel", "xta_channel", "esdi_channel", "ide_channel", "scsi_location", "scsi_id" })
                    c.RemoveKey(sec, n + suffix);
                switch (bus)
                {
                    case "ide":
                    case "atapi": c.SetString(sec, n + "ide_channel", Cfg86Resolver.IdeChannel(d.Channel)); break;
                    case "scsi": c.SetString(sec, n + "scsi_location", Cfg86Resolver.ScsiLocation(d.Channel)); break;
                    case "mfm": c.SetInt(sec, n + "mfm_channel", Cfg86Resolver.SecondNumber(d.Channel)); break;
                    case "xta": c.SetInt(sec, n + "xta_channel", Cfg86Resolver.SecondNumber(d.Channel)); break;
                    case "esdi": c.SetInt(sec, n + "esdi_channel", Cfg86Resolver.SecondNumber(d.Channel)); break;
                }

                if (string.IsNullOrWhiteSpace(d.ImagePath))
                {
                    c.RemoveKey(sec, n + "fn");
                }
                else
                {
                    if (!File.Exists(Absolute(d.ImagePath)))
                        r.Issues.Add(new(sec, n + "fn", $"Disk image '{d.ImagePath}' does not exist (yet).", false));
                    c.SetString(sec, n + "fn", d.ImagePath);
                }

                string? preset = Cfg86Resolver.HddPreset(d.Model);
                if (preset == null) c.RemoveKey(sec, n + "speed"); else c.SetString(sec, n + "speed", preset);
            }
        }

        // ------------------------------------------------------------------ helpers
        private static void Required(MachineConfig c, CfgWriteResult r, string section, string key, string kind, string raw)
        {
            string? dev = Cfg86Resolver.Device(kind, raw);
            if (dev == null)
            {
                r.Issues.Add(new(section, key, $"'{raw}' is not a known {kind} device; leaving the engine default.", false));
                c.RemoveKey(section, key);
            }
            else
            {
                c.SetString(section, key, dev);
            }
        }

        private static void OptionalInt(MachineConfig c, string section, string key, int value, int defaultValue)
        {
            if (value == defaultValue) c.RemoveKey(section, key); else c.SetInt(section, key, value);
        }

        /// <summary>Optical/floppy image paths are written only when explicitly assigned to a real file.</summary>
        private static void AssignedImage(MachineConfig c, CfgWriteResult r, string section, string key, string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                c.RemoveKey(section, key);
                return;
            }

            if (path.StartsWith("ioctl://", StringComparison.OrdinalIgnoreCase))
            {
                r.Issues.Add(new(section, key, "Host drive mappings (ioctl://) are not written.", false));
                c.RemoveKey(section, key);
                return;
            }

            if (!File.Exists(Absolute(path)))
            {
                r.Issues.Add(new(section, key, $"Image '{path}' does not exist; left unassigned.", false));
                c.RemoveKey(section, key);
                return;
            }

            c.SetString(section, key, path);
        }
    }

    internal sealed record ResolvedMachine(string TypeKey, MachineInfo Info);

    /// <summary>Maps UI labels and internal names to the engine's internal identifiers.</summary>
    internal static class Cfg86Resolver
    {
        private static readonly Regex Tag = new(@"^\[[^\]]+\]\s*", RegexOptions.Compiled);

        public static string Norm(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var sb = new StringBuilder();
            foreach (char ch in Tag.Replace(s.Trim(), ""))
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            return sb.ToString();
        }

        private static bool IsNone(string? s)
        {
            var n = Norm(s);
            return n.Length == 0 || n == "none";
        }

        public static ResolvedMachine? Machine(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string norm = Norm(raw);
            foreach (var (type, list) in HardwareCatalog.MachinesByType)
            {
                foreach (var m in list)
                {
                    if (m.InternalName.Equals(raw.Trim(), StringComparison.OrdinalIgnoreCase))
                        return new ResolvedMachine(type, m);
                }
            }

            foreach (var (type, list) in HardwareCatalog.MachinesByType)
            {
                foreach (var m in list)
                {
                    if (m.Name.Equals(raw.Trim(), StringComparison.OrdinalIgnoreCase) || Norm(m.Name) == norm)
                        return new ResolvedMachine(type, m);
                }
            }

            return null;
        }

        /// <summary>Packages the engine's machine table says this machine's CPU socket accepts.</summary>
        public static string[] MachinePackages(ResolvedMachine machine)
        {
            if (AuthenticCatalog.MachinePackages.TryGetValue(machine.Info.InternalName, out var pk))
                return pk;
            return string.IsNullOrWhiteSpace(machine.Info.CpuPackage) ? Array.Empty<string>() : new[] { machine.Info.CpuPackage };
        }

        public static bool FamilyFits(ResolvedMachine machine, CpuFamily family)
        {
            var packages = MachinePackages(machine).ToList();
            if (packages.Count == 0) return true;

            // cpu_is_eligible(): a Socket 3 board also takes Socket 1 parts; Slot 1 takes Socket 370 / Socket 8 parts.
            if (packages.Contains("CPU_PKG_SOCKET3")) packages.Add("CPU_PKG_SOCKET1");
            else if (packages.Contains("CPU_PKG_SLOT1")) { packages.Add("CPU_PKG_SOCKET370"); packages.Add("CPU_PKG_SOCKET8"); }

            return family.Packages.Any(p => packages.Contains(p, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// The CPU families this machine can actually run: a family is listed only when the engine accepts at
        /// least one of its speed steps on the board (socket/package, bus speed, voltage, multiplier).
        /// Labels are unique; families that share a display name get their engine id appended.
        /// </summary>
        public static List<(string Key, string Label)> EligibleFamilies(ResolvedMachine machine)
        {
            var keys = new List<string>();
            foreach (var (key, fam) in AuthenticCatalog.CpuFamilies)
            {
                bool any = fam.Steps.Any(s => StepEligible(machine, key, s) ?? FamilyFits(machine, fam));
                if (any) keys.Add(key);
            }

            var dupes = keys.GroupBy(k => AuthenticCatalog.CpuFamilies[k].Display).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
            return keys.Select(k =>
            {
                string display = AuthenticCatalog.CpuFamilies[k].Display;
                return (k, dupes.Contains(display) ? $"{display} ({k})" : display);
            }).ToList();
        }

        /// <summary>The list label of one family for this machine, or null when the machine cannot take it.</summary>
        public static string? FamilyLabel(ResolvedMachine machine, string familyKey)
        {
            foreach (var (k, label) in EligibleFamilies(machine))
                if (k == familyKey) return label;
            return null;
        }

        /// <summary>
        /// Resolves a CPU family (internal name or "Manufacturer Name" label) and, if given, a speed
        /// step. An empty speed is valid: config.c then picks the first eligible CPU of the family.
        /// </summary>
        public static bool Cpu(ResolvedMachine machine, string? familyRaw, string? speedRaw, out string family, out CpuStep? step, out string error)
        {
            family = string.Empty;
            step = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(familyRaw))
            {
                error = "No CPU family selected; the engine will pick the first eligible one.";
                return false;
            }

            string t = familyRaw.Trim();
            var tagged = Regex.Match(t, @"\(([a-z0-9_]+)\)$");
            if (tagged.Success && AuthenticCatalog.CpuFamilies.ContainsKey(tagged.Groups[1].Value))
                t = tagged.Groups[1].Value;
            string? key = AuthenticCatalog.CpuFamilies.Keys.FirstOrDefault(k => k.Equals(t, StringComparison.OrdinalIgnoreCase));
            if (key == null)
            {
                // A label can match several families ("Intel 8088" has three); prefer the one this machine can take.
                var candidates = AuthenticCatalog.CpuFamilies.Where(kv => Norm(kv.Value.Display) == Norm(t)).ToList();
                key = (candidates.FirstOrDefault(kv => FamilyFits(machine, kv.Value)).Key) ?? candidates.FirstOrDefault().Key;
            }

            if (key == null)
            {
                error = $"CPU '{familyRaw}' is not a CPU family known to the engine.";
                return false;
            }

            var fam = AuthenticCatalog.CpuFamilies[key];
            if (!FamilyFits(machine, fam))
            {
                error = $"CPU family '{key}' ({string.Join("|", fam.Packages)}) does not fit '{machine.Info.Name}' ({string.Join("|", MachinePackages(machine))}); the engine would ignore it.";
                return false;
            }

            family = key;
            string sp = (speedRaw ?? string.Empty).Trim();
            if (sp.Length == 0)
                return true;

            foreach (var st in fam.Steps)
                if (st.Name.Equals(sp, StringComparison.OrdinalIgnoreCase)) { step = st; return true; }

            if (double.TryParse(sp, NumberStyles.Float, CultureInfo.InvariantCulture, out double mhz) && mhz > 0)
            {
                foreach (var st in fam.Steps)
                    if (Math.Abs(st.RSpeedHz / 1e6 - mhz) <= Math.Max(0.5, mhz * 0.005)) { step = st; return true; }
            }

            error = $"'{speedRaw}' is not a selectable speed for CPU family '{key}'.";
            return false;
        }

        /// <summary>
        /// Asks the engine's own cpu_is_eligible() (package, bus speed/FSB, voltage and multiplier limits of the
        /// machine) whether a CPU step can run on it. null = the engine could not be asked.
        /// </summary>
        public static bool? StepEligible(ResolvedMachine machine, string familyKey, CpuStep step)
        {
            try
            {
                return Box86_WinUI.Interop.Box86CoreNative.EmuCpuStepEligible(machine.Info.InternalName, familyKey, step.RSpeedHz, step.Multi) switch
                {
                    1 => true,
                    0 => false,
                    _ => null
                };
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                return null;
            }
        }

        /// <summary>Steps of a family the machine accepts; every step when the engine cannot be asked.</summary>
        public static List<CpuStep> EligibleSteps(ResolvedMachine machine, string familyKey)
        {
            if (!AuthenticCatalog.CpuFamilies.TryGetValue(familyKey, out var fam)) return new List<CpuStep>();
            var all = fam.Steps.ToList();
            var ok = all.Where(s => StepEligible(machine, familyKey, s) != false).ToList();
            return ok.Count > 0 ? ok : all;
        }

        /// <summary>"233 MHz (3.5x, 66.67 MHz FSB)": the label the speed ComboBox shows.</summary>
        public static string StepLabel(CpuStep s)
        {
            double fsb = s.Multi > 0 ? s.RSpeedHz / s.Multi / 1e6 : 0;
            return $"{s.Name} MHz ({s.Multi.ToString("0.0#", CultureInfo.InvariantCulture)}x, {fsb.ToString("0.##", CultureInfo.InvariantCulture)} MHz FSB)";
        }

        /// <summary>
        /// Whether the machine has ACPI (soft power button), from the engine's own MACHINE_ACPI flag: boards with
        /// PIIX4 / 440BX / Slot 1 / Socket 370 era chipsets, not XT/AT or early Pentium boards. False if the engine cannot be asked.
        /// </summary>
        public static bool SupportsAcpi(string? machineInternalName)
        {
            if (string.IsNullOrWhiteSpace(machineInternalName)) return false;
            try
            {
                return Box86_WinUI.Interop.Box86CoreNative.EmuMachineSupportsAcpi(machineInternalName) == 1;
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                return false;
            }
        }

        public static string? Fpu(string? raw)
        {
            string n = Norm(raw);
            if (n.Length == 0 || n == "none") return null;
            if (n.StartsWith("internal") || n == "builtin") return "internal";
            if (n is "8087" or "287" or "287xl" or "387" or "487sx") return n;
            return null;
        }

        // UI labels for input devices are generic; the engine's names differ.
        private static readonly Dictionary<string, string> InputAliases = new()
        {
            ["standard83keyxtkeyboard"] = "keyboard_pc_xt",
            ["standard84keyatkeyboard"] = "keyboard_at",
            ["standard101keyenhancedkeyboard"] = "keyboard_ps2",
            ["standard104keywindowskeyboard"] = "keyboard_ps2",
            ["standardps2keyboard"] = "keyboard_ps2",
            ["standardps2mouse"] = "ps2",
            ["microsoftserialmouse"] = "msserial",
            ["logitechserialmouse"] = "ltserial",
            ["mousesystemsserialmouse"] = "mssystems",
            ["busmouseinport"] = "msbus",
        };

        private static readonly Regex Parenthetical = new(@"\([^)]*\)", RegexOptions.Compiled);

        public static bool IsNoneLabel(string? raw) => IsNone(raw);

        public static string? Device(string kind, string? raw)
        {
            if (IsNone(raw)) return "none";

            string t = raw!.Trim();
            string n = Norm(t);

            // "Internal controller", "Internal / Motherboard Sound", ... are all the engine's "internal".
            if (n.StartsWith("internal")) return "internal";

            // A label the engine's own device list produced ("[PCI] S3 Trio64V+") is exact.
            var fromEngine = EngineDeviceLists.Resolve(kind, t);
            if (fromEngine != null) return fromEngine;

            if (!AuthenticCatalog.Devices.TryGetValue(kind, out var devices)) return null;

            foreach (var d in devices)
                if (d.Internal.Equals(t, StringComparison.OrdinalIgnoreCase)) return d.Internal;

            if (kind == "input" && InputAliases.TryGetValue(n, out var alias)) return alias;

            foreach (var d in devices)
                if (Norm(d.Name) == n) return d.Internal;

            // Ignore parenthetical model codes/bus tags ("(CT2760/CT3900)"); accept only an unambiguous hit.
            string bare = Norm(Parenthetical.Replace(t, ""));
            if (bare.Length > 0)
            {
                var hits = devices.Where(d => Norm(Parenthetical.Replace(d.Name, "")) == bare).Select(d => d.Internal).Distinct().ToList();
                if (hits.Count == 1) return hits[0];

                // Several share the bare name ("Sound Blaster 16" vs "... (Compatibility)"): the plain one wins.
                var plain = devices.Where(d => Norm(Parenthetical.Replace(d.Name, "")) == bare && !d.Name.Contains('(')).Select(d => d.Internal).Distinct().ToList();
                if (plain.Count == 1) return plain[0];
            }

            return null;
        }

        public static string? FloppyType(string? raw)
        {
            if (IsNone(raw)) return "none";
            string t = raw!.Trim();
            foreach (var d in AuthenticCatalog.FloppyTypes)
                if (d.Internal.Equals(t, StringComparison.OrdinalIgnoreCase)) return d.Internal;

            // Legacy tokens used by the built-in presets.
            switch (t.ToLowerInvariant())
            {
                case "360k_525": return "525_2dd";
                case "12m_525": return "525_2hd";
                case "720k_35": return "35_2dd";
                case "144m_35": return "35_2hd";
                case "288m_35": return "35_2ed";
            }

            string n = Norm(t);
            foreach (var d in AuthenticCatalog.FloppyTypes)
                if (Norm(d.Name) == n) return d.Internal;

            return null;
        }

        public static string? CdromType(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string internalName = HardwareCatalog.GetCdromInternalName(raw);
            return string.IsNullOrWhiteSpace(internalName) ? null : internalName;
        }

        public static string? HddPreset(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string t = raw.Trim();
            foreach (var d in AuthenticCatalog.HddPresets)
                if (d.Internal.Equals(t, StringComparison.OrdinalIgnoreCase)) return d.Internal;

            string n = Norm(t);
            foreach (var d in AuthenticCatalog.HddPresets)
                if (Norm(d.Name) == n) return d.Internal;

            return null;
        }

        public static string HddBus(string? raw) => Norm(raw) switch
        {
            "ide" => "ide",
            "atapi" => "atapi",
            "scsi" => "scsi",
            "esdi" => "esdi",
            "mfm" or "st506" => "mfm",
            "xta" => "xta",
            _ => "none"
        };

        public static string CdromBus(string? raw) => Norm(raw) switch
        {
            "atapi" or "ide" => "atapi",
            "scsi" => "scsi",
            "mitsumi" => "mitsumi",
            "mke" or "panasonic" or "creative" => "mke",
            _ => "none"
        };

        // "primary:secondary" style channel strings as stored by the engine.
        public static string IdeChannel(string? channel)
        {
            var m = Regex.Match(channel ?? string.Empty, @"(\d+)\s*:\s*(\d+)");
            return m.Success ? $"{int.Parse(m.Groups[1].Value) & 3}:{int.Parse(m.Groups[2].Value) & 1}" : "0:0";
        }

        // scsi_location is "%01u:%02u" (bus:id).
        public static string ScsiLocation(string? channel)
        {
            var m = Regex.Match(channel ?? string.Empty, @"(\d+)\s*:\s*(\d+)");
            return m.Success ? $"{int.Parse(m.Groups[1].Value) & 15}:{int.Parse(m.Groups[2].Value) & 15:D2}" : "0:00";
        }

        /// <summary>UI/legacy network mode to the engine's net_type string (null = no host connection).</summary>
        public static string? NetworkType(string? raw) => Norm(raw) switch
        {
            "" or "none" or "nulldriver" or "null" => null,
            "slirp" => "slirp",
            "pcap" or "winpcap" => "pcap",
            "vde" => "vde",
            "tap" => "tap",
            "localswitch" or "nlswitch" => "nlswitch",
            "remoteswitch" or "nrswitch" => "nrswitch",
            _ => null
        };

        /// <summary>Bus string for MO / removable disk / tape drives ("none" = disabled).</summary>
        public static string RemovableBus(string? raw) => Norm(raw) switch
        {
            "atapi" or "ide" => "atapi",
            "scsi" => "scsi",
            "lpt" => "lpt",
            "fdc" or "floppy" => "fdc",
            _ => "none"
        };

        /// <summary>Index of a drive model in the engine's table; the config stores the index.</summary>
        public static int RemovableTypeIndex((string Label, string Bus)[] table, string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return -1;
            string n = Norm(raw);
            for (int i = 0; i < table.Length; i++)
                if (Norm(table[i].Label) == n) return i;

            // A numeric index (config value) is accepted as-is.
            if (int.TryParse(raw.Trim(), out int idx) && idx >= 0 && idx < table.Length) return idx;
            return -1;
        }

        public static int SecondNumber(string? channel)
        {
            var m = Regex.Match(channel ?? string.Empty, @"(\d+)\s*:\s*(\d+)");
            if (m.Success) return int.Parse(m.Groups[2].Value);
            return int.TryParse(channel, out int v) ? v : 0;
        }
    }
}
