using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Box86_WinUI.Interop;

namespace Box86_WinUI.Services
{
    public enum DeviceListKind { Video = 0, Sound = 1, Network = 2, HardDiskController = 3, Scsi = 4, FloppyController = 5, SecondaryVideo = 6, Keyboard = 7, Mouse = 8, Tablet = 9, CdromInterface = 10, MidiOut = 11, MidiIn = 12 }

    /// <summary>
    /// The devices the original settings dialog offers for a machine, asked from the engine itself: available (ROMs
    /// present), valid for the machine's buses, with its own "[PCI] Name" labels. Also remembers label -> internal
    /// name so the config writer can resolve whatever the UI shows.
    /// </summary>
    public static class EngineDeviceLists
    {
        private static readonly Dictionary<DeviceListKind, Dictionary<string, string>> Labels = new();
        private static readonly object Gate = new();

        private static string KindName(DeviceListKind k) => k switch
        {
            DeviceListKind.Video or DeviceListKind.SecondaryVideo => "video",
            DeviceListKind.Sound => "sound",
            DeviceListKind.Network => "network",
            DeviceListKind.HardDiskController => "hdc",
            DeviceListKind.Scsi => "scsi",
            DeviceListKind.Keyboard or DeviceListKind.Mouse or DeviceListKind.Tablet => "input",
            DeviceListKind.CdromInterface => "cdrom_if",
            DeviceListKind.MidiOut => "midi_out",
            DeviceListKind.MidiIn => "midi_in",
            _ => "fdc"
        };

        /// <summary>(internal name, label) pairs in the engine's order; empty when the engine cannot be asked.</summary>
        public static List<(string Internal, string Label)> Get(DeviceListKind kind, string? machineInternal, int slot = 0, string? primaryVideo = null)
        {
            var result = new List<(string, string)>();
            if (string.IsNullOrWhiteSpace(machineInternal)) return result;

            try
            {
                var buffer = new byte[256 * 1024];
                int n = kind is DeviceListKind.MidiOut or DeviceListKind.MidiIn
                    ? Box86CoreNative.EmuListMidiDevices(kind == DeviceListKind.MidiIn ? 1 : 0, buffer, buffer.Length)
                    : kind is >= DeviceListKind.Keyboard and <= DeviceListKind.Tablet
                    ? Box86CoreNative.EmuListInputDevices((int)kind - (int)DeviceListKind.Keyboard, machineInternal, buffer, buffer.Length)
                    : Box86CoreNative.EmuListDevices(kind == DeviceListKind.CdromInterface ? 7 : (int)kind, machineInternal, slot, primaryVideo, buffer, buffer.Length);
                if (n < 0) return result;

                int len = Array.IndexOf(buffer, (byte)0);
                string text = Encoding.UTF8.GetString(buffer, 0, len < 0 ? buffer.Length : len);
                foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    int tab = line.IndexOf('\t');
                    if (tab > 0) result.Add((line.Substring(0, tab), line.Substring(tab + 1)));
                }
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                return result;
            }

            lock (Gate)
            {
                string key = KindName(kind);
                var map = Labels.TryGetValue(kind, out var m) ? m : Labels[kind] = new(StringComparer.OrdinalIgnoreCase);
                foreach (var (i, l) in result) map[l] = i;
            }
            return result;
        }

        /// <summary>Host network adapters for the PCap mode: (device, description), first entry is "none".</summary>
        public static List<(string Device, string Description)> NetworkHostDevices()
        {
            var result = new List<(string, string)>();
            try
            {
                var buffer = new byte[64 * 1024];
                if (Box86CoreNative.EmuListNetworkHostDevices(buffer, buffer.Length) < 0) return result;
                int len = Array.IndexOf(buffer, (byte)0);
                foreach (var line in Encoding.UTF8.GetString(buffer, 0, len < 0 ? buffer.Length : len).Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    int tab = line.IndexOf('\t');
                    if (tab > 0) result.Add((line.Substring(0, tab), line.Substring(tab + 1)));
                }
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                return result;
            }
            return result;
        }

        /// <summary>Internal name of a label the engine produced earlier (null when it never listed it).</summary>
        public static string? Resolve(string kind, string label)
        {
            lock (Gate)
            {
                foreach (var (k, map) in Labels)
                {
                    if (KindName(k) != kind) continue;
                    if (map.TryGetValue(label.Trim(), out var internalName)) return internalName;
                }
            }
            return null;
        }
    }
}
