using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Box86_WinUI.Services
{
    /// <summary>A standard display profile the user can pick instead of supplying an EDID file.</summary>
    public sealed record EdidProfile(
        string Id, string Label, string MonitorName,
        int PixelClock10kHz, int HActive, int HBlank, int HSyncOffset, int HSyncWidth,
        int VActive, int VBlank, int VSyncOffset, int VSyncWidth,
        bool HSyncPositive, bool VSyncPositive, int WidthCm, int HeightCm, int VRefreshMin, int VRefreshMax, int HRefreshMinKhz, int HRefreshMaxKhz);

    /// <summary>
    /// Builds a valid 128-byte EDID 1.3 base block. The engine only knows "built-in" or "load this file"
    /// (monitor_edid / monitor_edid_path), so standard profiles are realised as generated .bin files.
    /// </summary>
    public static class EdidBuilder
    {
        // VESA DMT timings.
        public static readonly IReadOnlyList<EdidProfile> Profiles = new[]
        {
            new EdidProfile("crt43", "Standard 4:3 CRT (800x600, 60 Hz)", "STD CRT 4:3",
                4000, 800, 256, 40, 128, 600, 28, 1, 4, true, true, 32, 24, 55, 75, 30, 60),
            new EdidProfile("xga60", "1024x768, 60 Hz", "XGA 60HZ",
                6500, 1024, 320, 24, 136, 768, 38, 3, 6, false, false, 32, 24, 55, 75, 30, 60),
            new EdidProfile("sxga75", "1280x1024, 75 Hz", "SXGA 75HZ",
                13500, 1280, 408, 16, 144, 1024, 42, 1, 3, true, true, 36, 27, 55, 85, 30, 82),
        };

        public static EdidProfile? Find(string? id)
        {
            foreach (var p in Profiles) if (p.Id == id) return p;
            return null;
        }

        public static byte[] Build(EdidProfile p)
        {
            var e = new byte[128];
            byte[] header = { 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00 };
            header.CopyTo(e, 0);

            // Manufacturer "BOX": three 5-bit letters (A=1).
            int man = (('B' - 'A' + 1) << 10) | (('O' - 'A' + 1) << 5) | ('X' - 'A' + 1);
            e[8] = (byte)(man >> 8); e[9] = (byte)man;
            e[10] = 0x01; e[11] = 0x00;          // product code
            e[16] = 1; e[17] = 30;               // week 1, year 2020
            e[18] = 1; e[19] = 3;                // EDID 1.3
            e[20] = 0x08;                        // analog, separate sync
            e[21] = (byte)p.WidthCm; e[22] = (byte)p.HeightCm;
            e[23] = 120;                         // gamma 2.20
            e[24] = 0x0A;                        // RGB colour, preferred timing in descriptor 1

            byte[] chroma = { 0xEE, 0x91, 0xA3, 0x54, 0x4C, 0x99, 0x26, 0x0F, 0x50, 0x54 };
            chroma.CopyTo(e, 25);

            for (int i = 38; i < 54; i += 2) { e[i] = 0x01; e[i + 1] = 0x01; } // unused standard timings

            // Descriptor 1: preferred detailed timing.
            int d = 54;
            int hSizeMm = p.WidthCm * 10, vSizeMm = p.HeightCm * 10;
            e[d + 0] = (byte)(p.PixelClock10kHz & 0xFF);
            e[d + 1] = (byte)(p.PixelClock10kHz >> 8);
            e[d + 2] = (byte)(p.HActive & 0xFF);
            e[d + 3] = (byte)(p.HBlank & 0xFF);
            e[d + 4] = (byte)(((p.HActive >> 8) << 4) | (p.HBlank >> 8));
            e[d + 5] = (byte)(p.VActive & 0xFF);
            e[d + 6] = (byte)(p.VBlank & 0xFF);
            e[d + 7] = (byte)(((p.VActive >> 8) << 4) | (p.VBlank >> 8));
            e[d + 8] = (byte)(p.HSyncOffset & 0xFF);
            e[d + 9] = (byte)(p.HSyncWidth & 0xFF);
            e[d + 10] = (byte)(((p.VSyncOffset & 0xF) << 4) | (p.VSyncWidth & 0xF));
            e[d + 11] = (byte)(((p.HSyncOffset >> 8) << 6) | ((p.HSyncWidth >> 8) << 4)
                               | ((p.VSyncOffset >> 4) << 2) | (p.VSyncWidth >> 4));
            e[d + 12] = (byte)(hSizeMm & 0xFF);
            e[d + 13] = (byte)(vSizeMm & 0xFF);
            e[d + 14] = (byte)(((hSizeMm >> 8) << 4) | (vSizeMm >> 8));
            e[d + 17] = (byte)(0x18 | (p.VSyncPositive ? 0x04 : 0) | (p.HSyncPositive ? 0x02 : 0));

            // Descriptor 2: monitor name.
            d = 72;
            e[d + 3] = 0xFC;
            var name = Encoding.ASCII.GetBytes((p.MonitorName + "\n").PadRight(13));
            Array.Copy(name, 0, e, d + 5, 13);

            // Descriptor 3: monitor range limits.
            d = 90;
            e[d + 3] = 0xFD;
            e[d + 5] = (byte)p.VRefreshMin; e[d + 6] = (byte)p.VRefreshMax;
            e[d + 7] = (byte)p.HRefreshMinKhz; e[d + 8] = (byte)p.HRefreshMaxKhz;
            e[d + 9] = (byte)Math.Min(255, (p.PixelClock10kHz / 100) / 10 + 1);
            e[d + 10] = 0x00; e[d + 11] = 0x0A;
            for (int i = 12; i < 18; i++) e[d + i] = 0x20;

            // Descriptor 4: dummy.
            d = 108;
            e[d + 3] = 0x10;

            e[126] = 0; // no extensions
            int sum = 0;
            for (int i = 0; i < 127; i++) sum += e[i];
            e[127] = (byte)((256 - (sum & 0xFF)) & 0xFF);
            return e;
        }

        /// <summary>Writes the profile's EDID next to the VM's config and returns the file path.</summary>
        public static string WriteProfile(EdidProfile p, string vmDirectory)
        {
            string path = Path.Combine(vmDirectory, $"edid_{p.Id}.bin");
            File.WriteAllBytes(path, Build(p));
            return path;
        }
    }
}
