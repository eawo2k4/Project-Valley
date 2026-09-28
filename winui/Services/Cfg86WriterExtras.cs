using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Box86_WinUI.Models;

namespace Box86_WinUI.Services
{
    public sealed class NetworkCardSetting
    {
        public string Card { get; set; } = "none";
        /// <summary>"Null driver", "SLiRP", "Local switch", "Remote switch", or an internal type (slirp, pcap, vde, tap, nlswitch, nrswitch).</summary>
        public string NetType { get; set; } = "slirp";
        public string HostDevice { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
        public bool Promiscuous { get; set; }
    }

    public sealed class RemovableSetting
    {
        public string Bus { get; set; } = "none";
        public string Channel { get; set; } = "0:0";
        /// <summary>Drive model label as listed by the engine's drive-type table.</summary>
        public string Type { get; set; } = string.Empty;
    }

    public sealed partial class Cfg86Settings
    {
        // [Video] monitor EDID: null = the engine's built-in EDID, otherwise the file to load.
        public string? MonitorEdidPath { get; set; }

        /// <summary>Device settings by "section/key" (e.g. "voodoo/framebuffer_memory"), written verbatim.</summary>
        public Dictionary<string, string> DeviceSettings { get; } = new();

        // [Network]
        public List<NetworkCardSetting> NetworkCards { get; } = new();

        // [Ports (COM & LPT)]
        public bool[] ComEnabled { get; set; } = { true, true, false, false };
        public string[] ComDevices { get; set; } = { "none", "none", "none", "none" };
        public bool[] LptEnabled { get; set; } = { true, false, false, false };
        public string[] LptDevices { get; set; } = { "none", "none", "none", "none" };

        // [Other removable devices]
        public List<RemovableSetting> MoDrives { get; } = new();
        public List<RemovableSetting> RemovableDisks { get; } = new();
        public List<RemovableSetting> TapeDrives { get; } = new();

        // [Other peripherals]
        public bool IsaBugger { get; set; }
        public bool PostCard { get; set; }
        public bool UnitTester { get; set; }
        public bool NovellKeycard { get; set; }
        public bool SoftPower { get; set; }
        public string IsaRtc { get; set; } = "none";
        /// <summary>The four memory expansion slots (isamem0-3_type, or mcamem0-3_type on an MCA machine).</summary>
        public string[] MemExpCards { get; set; } = { "none", "none", "none", "none" };
        public bool MemExpIsMca { get; set; }
        /// <summary>The four ISA ROM card slots (isarom0-3_type).</summary>
        public string[] IsaRomCards { get; set; } = { "none", "none", "none", "none" };
    }

    public sealed partial class Cfg86Writer
    {
        // ------------------------------------------------------------------ [Video] EDID + per-device sections
        private static void WriteMonitorAndDeviceSettings(MachineConfig c, Cfg86Settings s, CfgWriteResult r)
        {
            const string video = "Video";
            if (string.IsNullOrWhiteSpace(s.MonitorEdidPath))
            {
                c.RemoveKey(video, "monitor_edid");
                c.RemoveKey(video, "monitor_edid_path");
            }
            else
            {
                if (!System.IO.File.Exists(s.MonitorEdidPath))
                    r.Issues.Add(new(video, "monitor_edid_path", $"The EDID file '{s.MonitorEdidPath}' does not exist.", true));
                c.SetInt(video, "monitor_edid", 1);
                c.SetString(video, "monitor_edid_path", s.MonitorEdidPath);
            }

            // Device options live in the section named after the device (e.g. [Sound Blaster AWE32]); values are
            // already in the engine's own format (0/1, numbers, hex, strings) as produced by the config dialogs.
            foreach (var kv in s.DeviceSettings)
            {
                var (section, key) = DeviceSettingKey.Split(kv.Key);
                if (section.Length == 0 || key.Length == 0) continue;
                c.SetString(section, key, kv.Value);
            }
        }

        // ------------------------------------------------------------------ [Network]
        private static void WriteNetwork(MachineConfig c, Cfg86Settings s, CfgWriteResult r)
        {
            const string sec = "Network";

            // Keys the engine itself deletes on every save (pre-multi-card format) plus ones this app invented.
            foreach (var legacy in new[] { "net_type", "net_host_device", "net_card", "net_mac", "local_switch_secret", "local_switch_promiscuous" })
                c.RemoveKey(sec, legacy);

            for (int i = 0; i < 4; i++)
            {
                string n = $"net_{i + 1:D2}_";
                var card = i < s.NetworkCards.Count ? s.NetworkCards[i] : null;

                string? dev = card == null ? "none" : Cfg86Resolver.Device("network", card.Card);
                if (card != null && dev == null)
                    r.Issues.Add(new(sec, n + "card", $"'{card.Card}' is not a network card known to the engine; ignored.", false));

                string? type = card == null ? null : Cfg86Resolver.NetworkType(card.NetType);

                if (card == null || dev == null || dev == "none")
                {
                    foreach (var suffix in new[] { "card", "net_type", "host_device", "link", "addr", "secret", "promisc", "nrs_host" })
                        c.RemoveKey(sec, n + suffix);
                    continue;
                }

                c.SetString(sec, n + "card", dev);
                if (type == null) c.RemoveKey(sec, n + "net_type"); else c.SetString(sec, n + "net_type", type);

                if (string.IsNullOrWhiteSpace(card.HostDevice) || card.HostDevice.Equals("none", StringComparison.OrdinalIgnoreCase))
                    c.RemoveKey(sec, n + "host_device");
                else
                    c.SetString(sec, n + "host_device", card.HostDevice);

                if (string.IsNullOrEmpty(card.Secret)) c.RemoveKey(sec, n + "secret"); else c.SetString(sec, n + "secret", card.Secret);
                if (card.Promiscuous) c.SetInt(sec, n + "promisc", 1); else c.RemoveKey(sec, n + "promisc");
            }
        }

        // ------------------------------------------------------------------ [Ports (COM & LPT)]
        private static void WritePorts(MachineConfig c, Cfg86Settings s, CfgWriteResult r)
        {
            const string sec = "Ports (COM & LPT)";

            for (int i = 0; i < 4; i++)
            {
                // serialN_enabled is stored only when it differs from the default (on for COM1-2, off after).
                string en = $"serial{i + 1}_enabled";
                bool enabled = i < s.ComEnabled.Length ? s.ComEnabled[i] : i < 2;
                bool def = i < 2;
                if (enabled == def) c.RemoveKey(sec, en); else c.SetInt(sec, en, enabled ? 1 : 0);
                CharDevice(c, r, sec, $"serial{i + 1}_device", "char_com", i < s.ComDevices.Length ? s.ComDevices[i] : "none");

                string lpt = $"lpt{i + 1}_enabled";
                bool lEnabled = i < s.LptEnabled.Length ? s.LptEnabled[i] : i == 0;
                bool lDef = i == 0;
                if (lEnabled == lDef) c.RemoveKey(sec, lpt); else c.SetInt(sec, lpt, lEnabled ? 1 : 0);
                CharDevice(c, r, sec, $"lpt{i + 1}_device", "char_lpt", i < s.LptDevices.Length ? s.LptDevices[i] : "none");
            }
        }

        private static void CharDevice(MachineConfig c, CfgWriteResult r, string section, string key, string kind, string raw)
        {
            string? dev = Cfg86Resolver.Device(kind, raw);
            if (dev == null)
                r.Issues.Add(new(section, key, $"'{raw}' is not a serial/parallel device known to the engine; ignored.", false));
            if (dev == null || dev == "none") c.RemoveKey(section, key); else c.SetString(section, key, dev);
        }

        // ------------------------------------------------------------------ [Other removable devices]
        private static void WriteRemovableDevices(MachineConfig c, Cfg86Settings s, CfgWriteResult r)
        {
            const string sec = "Other removable devices";

            // Keys this app invented (bus/channel/type/fn/wp per drive); the engine stores "type, bus" in *_parameters.
            var legacy = new Regex(@"^(mo|rdisk|tape)_\d+_(bus|channel|type|fn|wp)$", RegexOptions.IgnoreCase);
            if (c.Sections.TryGetValue(sec, out var section))
                foreach (var key in section.Keys.Where(k => legacy.IsMatch(k)).ToList())
                    section.Remove(key);

            WriteRemovableKind(c, r, sec, "mo", 4, s.MoDrives, AuthenticCatalog.MoDriveTypes);
            WriteRemovableKind(c, r, sec, "rdisk", 4, s.RemovableDisks, AuthenticCatalog.RdiskDriveTypes);
            WriteRemovableKind(c, r, sec, "tape", 4, s.TapeDrives, AuthenticCatalog.TapeDriveTypes);
        }

        private static void WriteRemovableKind(MachineConfig c, CfgWriteResult r, string sec, string prefix, int count,
                                               List<RemovableSetting> drives, (string Label, string Bus)[] table)
        {
            for (int i = 0; i < count; i++)
            {
                string n = $"{prefix}_{i + 1:D2}_";
                var d = i < drives.Count ? drives[i] : null;
                string bus = d == null ? "none" : Cfg86Resolver.RemovableBus(d.Bus);
                int type = d == null ? -1 : Cfg86Resolver.RemovableTypeIndex(table, d.Type);

                if (d != null && bus != "none" && type < 0)
                {
                    r.Issues.Add(new(sec, n + "parameters", $"'{d.Type}' is not a {prefix} drive type known to the engine; drive {i + 1} disabled.", false));
                    bus = "none";
                }

                if (bus == "none")
                {
                    foreach (var suffix in new[] { "parameters", "ide_channel", "scsi_location", "scsi_id", "fdd_unit", "lpt_port", "image_path", "medium_type", "writeprot" })
                        c.RemoveKey(sec, n + suffix);
                    continue;
                }

                // Some tape drives only exist on one bus (FDC / LPT): the table wins over the UI's bus choice.
                if (prefix == "tape" && table[type].Bus is "lpt" or "fdc")
                    bus = table[type].Bus;

                c.SetString(sec, n + "parameters", $"{type}, {bus}");
                c.RemoveKey(sec, n + "ide_channel");
                c.RemoveKey(sec, n + "scsi_location");
                c.RemoveKey(sec, n + "scsi_id");
                c.RemoveKey(sec, n + "fdd_unit");
                c.RemoveKey(sec, n + "lpt_port");
                c.RemoveKey(sec, n + "writeprot");

                switch (bus)
                {
                    case "atapi": c.SetString(sec, n + "ide_channel", Cfg86Resolver.IdeChannel(d!.Channel)); break;
                    case "scsi": c.SetString(sec, n + "scsi_location", Cfg86Resolver.ScsiLocation(d!.Channel)); break;
                    case "fdc": c.SetInt(sec, n + "fdd_unit", Cfg86Resolver.SecondNumber(d!.Channel)); break;
                    case "lpt": c.SetInt(sec, n + "lpt_port", Cfg86Resolver.SecondNumber(d!.Channel)); break;
                }
            }
        }

        // ------------------------------------------------------------------ [Other peripherals]
        private static void WriteOtherPeripherals(MachineConfig c, Cfg86Settings s, CfgWriteResult r)
        {
            const string sec = "Other peripherals";

            // Keys this app invented; the engine's are <name>_enabled / <board>N_type.
            foreach (var legacy in new[] { "isa_rtc", "post_card", "isa_bugger", "unit_tester", "novell_key", "isa_mem", "mca_mem", "pci_mem" })
                c.RemoveKey(sec, legacy);

            OptionalInt(c, sec, "bugger_enabled", s.IsaBugger ? 1 : 0, 0);
            OptionalInt(c, sec, "postcard_enabled", s.PostCard ? 1 : 0, 0);
            OptionalInt(c, sec, "unittester_enabled", s.UnitTester ? 1 : 0, 0);
            OptionalInt(c, sec, "novell_keycard_enabled", s.NovellKeycard ? 1 : 0, 0);
            OptionalInt(c, sec, "softpower_enabled", s.SoftPower ? 1 : 0, 0);

            Board(c, r, sec, "isartc_type", "isartc", s.IsaRtc);
            for (int i = 0; i < 4; i++)
            {
                string card = i < s.MemExpCards.Length ? s.MemExpCards[i] : "none";
                Board(c, r, sec, s.MemExpIsMca ? $"mcamem{i}_type" : $"isamem{i}_type", s.MemExpIsMca ? "mcamem" : "isamem", card);
                // the other bus's key is stale for this machine
                c.RemoveKey(sec, s.MemExpIsMca ? $"isamem{i}_type" : $"mcamem{i}_type");
                Board(c, r, sec, $"isarom{i}_type", "isarom", i < s.IsaRomCards.Length ? s.IsaRomCards[i] : "none");
            }
        }

        private static void Board(MachineConfig c, CfgWriteResult r, string sec, string key, string kind, string raw)
        {
            string? dev = Cfg86Resolver.Device(kind, raw);
            if (dev == null)
                r.Issues.Add(new(sec, key, $"'{raw}' is not a board known to the engine; ignored.", false));
            if (dev == null || dev == "none") c.RemoveKey(sec, key); else c.SetString(sec, key, dev);
        }
    }
}
