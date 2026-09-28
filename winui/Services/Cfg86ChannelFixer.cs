using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Box86_WinUI.Models;

namespace Box86_WinUI.Services
{
    /// <summary>
    /// Keeps every drive on its own IDE channel / SCSI ID in an 86box.cfg. The original settings dialog never lets two
    /// drives share one (its channel boxes offer only free slots); the engine itself does not check, and two devices
    /// on the same channel (typically a hard disk and a CD-ROM both on 0:0) corrupt its heap when the machine is
    /// stopped or reset. This runs over the raw config: when the file is written, when settings are opened and
    /// right before a machine starts, so old files are repaired too.
    /// </summary>
    public static class Cfg86ChannelFixer
    {
        private static readonly Regex ParametersKey =
            new(@"^(hdd|cdrom|mo|rdisk|zip|tape)_(\d\d)_parameters$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // The order drives keep their place in when two collide: earlier kinds keep theirs, later ones move.
        private static readonly string[] KindOrder = { "hdd", "cdrom", "rdisk", "zip", "mo", "tape" };

        private sealed class Slot
        {
            public string Section = "", Prefix = "", Number = "";
            public bool IsIde;      // IDE / ATAPI channel ("board:dev")
            public bool IsScsi;     // SCSI location ("bus:id")
            public int Index;       // zero-based drive number
        }

        /// <summary>Repairs colliding channels in place; returns a human-readable line per change (empty when nothing changed).</summary>
        public static List<string> Fix(MachineConfig cfg)
        {
            var slots = new List<Slot>();
            foreach (var (section, keys) in cfg.Sections)
            {
                foreach (var key in keys.Keys)
                {
                    var m = ParametersKey.Match(key);
                    if (!m.Success) continue;

                    string bus = BusOf(m.Groups[1].Value.ToLowerInvariant(), keys[key]);
                    if (bus.Length == 0) continue;
                    slots.Add(new Slot
                    {
                        Section = section,
                        Prefix = m.Groups[1].Value.ToLowerInvariant(),
                        Number = m.Groups[2].Value,
                        IsIde = bus is "ide" or "atapi",
                        IsScsi = bus == "scsi",
                        Index = int.Parse(m.Groups[2].Value) - 1
                    });
                }
            }

            var changes = new List<string>();
            var ordered = slots.OrderBy(s => Array.IndexOf(KindOrder, s.Prefix)).ThenBy(s => s.Index).ToList();

            // IDE: eight channels, "board:dev" = board * 2 + dev.
            var usedIde = new HashSet<int>();
            foreach (var s in ordered.Where(s => s.IsIde))
            {
                string key = $"{s.Prefix}_{s.Number}_ide_channel";
                int channel = ParseIde(cfg.GetString(s.Section, key, ""), DefaultIde(s));
                if (usedIde.Add(channel)) continue;

                int free = Enumerable.Range(0, 8).FirstOrDefault(c => !usedIde.Contains(c), -1);
                if (free < 0) continue; // all eight channels taken: nothing better to offer
                usedIde.Add(free);
                cfg.SetString(s.Section, key, $"{free >> 1}:{free & 1}");
                changes.Add($"{Describe(s)} moved from IDE channel {channel >> 1}:{channel & 1} to {free >> 1}:{free & 1} (it shared the channel with another drive)");
            }

            // SCSI: 16 IDs per bus. Only explicit locations are considered; the defaults are already unique per drive.
            var usedScsi = new HashSet<(int Bus, int Id)>();
            foreach (var s in ordered.Where(s => s.IsScsi))
            {
                string key = $"{s.Prefix}_{s.Number}_scsi_location";
                if (!TryParseScsi(cfg.GetString(s.Section, key, ""), out int bus, out int id)) continue;
                if (usedScsi.Add((bus, id))) continue;

                int free = Enumerable.Range(0, 16).FirstOrDefault(i => i != 7 && !usedScsi.Contains((bus, i)), -1);
                if (free < 0) continue;
                usedScsi.Add((bus, free));
                cfg.SetString(s.Section, key, $"{bus}:{free:D2}");
                changes.Add($"{Describe(s)} moved from SCSI {bus}:{id:D2} to {bus}:{free:D2} (it shared the ID with another drive)");
            }

            return changes;
        }

        /// <summary>
        /// Hard disk image files a config refers to that do not exist. A missing image is not reported by the engine at
        /// start - the machine boots without the disk - but a later reset then crashes it with an access violation, so
        /// the front end refuses to start such a machine and names the files instead.
        /// </summary>
        public static List<string> MissingHardDiskImages(string cfgPath)
        {
            var missing = new List<string>();
            try
            {
                if (!System.IO.File.Exists(cfgPath)) return missing;
                string dir = System.IO.Path.GetDirectoryName(cfgPath) ?? string.Empty;
                var cfg = MachineConfig.Load(cfgPath);
                foreach (var (section, keys) in cfg.Sections)
                    foreach (var (key, value) in keys)
                    {
                        var m = Regex.Match(key, @"^hdd_(\d\d)_fn$", RegexOptions.IgnoreCase);
                        if (!m.Success || string.IsNullOrWhiteSpace(value)) continue;
                        string full = System.IO.Path.IsPathRooted(value) ? value : System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, value));
                        if (!System.IO.File.Exists(full)) missing.Add($"Hard disk {int.Parse(m.Groups[1].Value)}: {full}");
                    }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Cfg86ChannelFixer] {cfgPath}: {ex.Message}");
            }
            return missing;
        }

        /// <summary>Loads a config file, repairs it and saves it back only when something changed.</summary>
        public static List<string> FixFile(string cfgPath)
        {
            try
            {
                if (!System.IO.File.Exists(cfgPath)) return new List<string>();
                var cfg = MachineConfig.Load(cfgPath);
                var changes = Fix(cfg);
                if (changes.Count > 0) cfg.Save(cfgPath);
                return changes;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Cfg86ChannelFixer] {cfgPath}: {ex.Message}");
                return new List<string>();
            }
        }

        // hdd: last token of "spt, hpc, tracks, wp, bus"; the others: last token of "sound/speed, bus".
        private static string BusOf(string prefix, string parameters)
        {
            var parts = parameters.Split(',').Select(p => p.Trim().ToLowerInvariant()).ToList();
            if (parts.Count < 2) return "";
            return parts[^1];
        }

        // The engine's defaults when the key is absent (config.c): hdd -> (n>>1):(n&1), cdrom -> (n&3), zip/mo/tape -> n+2.
        private static int DefaultIde(Slot s) => s.Prefix switch
        {
            "hdd" => Math.Min(s.Index, 7),
            "cdrom" => s.Index & 3,
            _ => Math.Min(s.Index + 2, 7)
        };

        private static int ParseIde(string text, int fallback)
        {
            var m = Regex.Match(text ?? "", @"^\s*(\d):(\d)\s*$");
            if (!m.Success) return fallback;
            int board = int.Parse(m.Groups[1].Value) & 3, dev = int.Parse(m.Groups[2].Value) & 1;
            return (board << 1) + dev;
        }

        private static bool TryParseScsi(string text, out int bus, out int id)
        {
            bus = id = 0;
            var m = Regex.Match(text ?? "", @"^\s*(\d):(\d+)\s*$");
            if (!m.Success) return false;
            bus = int.Parse(m.Groups[1].Value);
            id = int.Parse(m.Groups[2].Value) & 15;
            return true;
        }

        private static string Describe(Slot s) => s.Prefix switch
        {
            "hdd" => $"Hard disk {s.Index + 1}",
            "cdrom" => $"CD-ROM drive {s.Index + 1}",
            "mo" => $"MO drive {s.Index + 1}",
            "tape" => $"Tape drive {s.Index + 1}",
            _ => $"Removable disk {s.Index + 1}"
        };
    }
}
