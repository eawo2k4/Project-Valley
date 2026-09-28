using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Box86_WinUI.Models;

namespace Box86_WinUI.Services
{
    /// <summary>
    /// The inverse of <see cref="Cfg86Writer"/>: turns authentic 86box.cfg values (internal
    /// identifiers, KB memory, Hz clock speeds, "spt, hpc, tracks, wp, bus" strings) back into the
    /// labels the Settings UI works with.
    /// </summary>
    internal static class Cfg86Reader
    {
        /// <summary>Label of the UI list entry that resolves to <paramref name="internalName"/>.</summary>
        public static string DeviceLabel(string kind, string? internalName, IEnumerable<string> uiList, string noneLabel = "None")
        {
            if (string.IsNullOrWhiteSpace(internalName) || internalName.Equals("none", StringComparison.OrdinalIgnoreCase))
                return noneLabel;

            var items = uiList.ToList();

            if (internalName.Equals("internal", StringComparison.OrdinalIgnoreCase))
                return items.FirstOrDefault(i => i.StartsWith("Internal", StringComparison.OrdinalIgnoreCase)) ?? noneLabel;

            foreach (var item in items)
                if (Cfg86Resolver.Device(kind, item) == internalName)
                    return item;

            // Not in the UI list: fall back to the engine's own name so the value is never lost.
            if (AuthenticCatalog.Devices.TryGetValue(kind, out var devs))
            {
                var d = devs.FirstOrDefault(x => x.Internal == internalName);
                if (d.Internal != null) return d.Name;
            }

            return internalName;
        }

        public static string CpuFamilyLabel(string? familyInternal, IEnumerable<string> available, string fallback)
        {
            if (string.IsNullOrWhiteSpace(familyInternal) || !AuthenticCatalog.CpuFamilies.TryGetValue(familyInternal, out var fam))
                return fallback;

            var list = available.ToList();
            string tagged = $"{fam.Display} ({familyInternal})";
            var exact = list.FirstOrDefault(a => a == tagged || a == fam.Display);
            if (exact != null) return exact;

            string n = Cfg86Resolver.Norm(fam.Display);
            return list.FirstOrDefault(a => Cfg86Resolver.Norm(a) == n) ?? fam.Display;
        }

        /// <summary>cpu_speed (Hz) + cpu_multi back to the selectable step label, e.g. "133".</summary>
        public static string? CpuStepLabel(string? familyInternal, long speedHz, double multi)
        {
            if (string.IsNullOrWhiteSpace(familyInternal) || !AuthenticCatalog.CpuFamilies.TryGetValue(familyInternal, out var fam) || fam.Steps.Length == 0)
                return null;

            if (speedHz <= 0)
                return fam.Steps[0].Name;

            foreach (var st in fam.Steps)
                if (st.RSpeedHz == speedHz && Math.Abs(st.Multi - multi) < 0.001) return st.Name;

            foreach (var st in fam.Steps)
                if (st.RSpeedHz >= speedHz) return st.Name;

            return fam.Steps[^1].Name;
        }

        public static string FloppyLabel(string? internalName, int slot)
        {
            // config.c: slots 1-2 default to 5.25" 360k, the rest to none, and the key is omitted at the default.
            string t = string.IsNullOrWhiteSpace(internalName) ? (slot < 2 ? "525_2dd" : "none") : internalName;
            var hit = AuthenticCatalog.FloppyTypes.FirstOrDefault(f => f.Internal == t);
            return hit.Internal != null ? hit.Name : "None";
        }

        public static string TimeSyncLabel(string? v) => (v ?? string.Empty).ToLowerInvariant() switch
        {
            "disabled" => "Disabled",
            "utc" or "gmt" => "UTC",
            _ => "Local"
        };

        public static string PitModeLabel(int mode) => mode switch
        {
            0 => "Slow",
            1 => "Fast",
            _ => "Auto"
        };

        public static string FpuLabel(string? internalName) => (internalName ?? string.Empty).ToLowerInvariant() switch
        {
            "" or "none" => "None",
            "internal" => "Internal",
            var other => other
        };

        /// <summary>"spt, hpc, tracks, wp, bus" as saved by config.c.</summary>
        public static bool TryParseHdd(string? parameters, out int spt, out int hpc, out int tracks, out string bus)
        {
            spt = hpc = tracks = 0;
            bus = "ide";
            if (string.IsNullOrWhiteSpace(parameters)) return false;

            var parts = parameters.Split(',').Select(p => p.Trim()).ToArray();
            if (parts.Length < 5) return false;
            if (!int.TryParse(parts[0], out spt) || !int.TryParse(parts[1], out hpc) || !int.TryParse(parts[2], out tracks))
                return false;

            bus = parts[4].ToLowerInvariant();
            return true;
        }


        /// <summary>net_NN_net_type back to the UI's connection mode.</summary>
        public static string NetModeLabel(string? netType) => (netType ?? string.Empty).ToLowerInvariant() switch
        {
            "slirp" => "SLiRP",
            "pcap" => "PCap",
            "nlswitch" => "Local Switch",
            "nrswitch" => "Remote Switch",
            _ => "Null Driver"
        };

        /// <summary>"index, bus" (mo/rdisk/tape *_parameters) back to a drive model label and bus.</summary>
        public static bool TryParseRemovable((string Label, string Bus)[] table, string? parameters, out string label, out string bus)
        {
            label = "None";
            bus = "none";
            if (string.IsNullOrWhiteSpace(parameters)) return false;

            var parts = parameters.Split(',').Select(p => p.Trim()).ToArray();
            if (parts.Length < 2 || !int.TryParse(parts[0], out int idx) || idx < 0 || idx >= table.Length) return false;

            label = table[idx].Label;
            bus = parts[1].ToLowerInvariant();
            return true;
        }

        public static string BusLabel(string bus) => bus.ToLowerInvariant() switch
        {
            "ide" => "IDE",
            "atapi" => "ATAPI",
            "scsi" => "SCSI",
            "esdi" => "ESDI",
            "mfm" => "MFM",
            "xta" => "XTA",
            "mitsumi" => "Mitsumi",
            "mke" => "MKE",
            "lpt" => "LPT",
            "fdc" => "Floppy",
            _ => "Disabled"
        };

        // "1:0" ide_channel / "0:03" scsi_location -> UI "1:0" / "0:3"
        public static string ChannelLabel(string? raw)
        {
            var m = Regex.Match(raw ?? string.Empty, @"(\d+)\s*:\s*(\d+)");
            return m.Success ? $"{int.Parse(m.Groups[1].Value)}:{int.Parse(m.Groups[2].Value)}" : "0:0";
        }
    }
}
