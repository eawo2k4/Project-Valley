using System;
using System.IO;
using Box86_WinUI.Interop;
using Box86_WinUI.Models;

namespace Box86_WinUI.Services
{
    public readonly record struct DiskGeometry(int Cylinders, int Heads, int Sectors)
    {
        public long Sectors512 => (long)Cylinders * Heads * Sectors;
        public long SizeBytes => Sectors512 * 512L;
        public int SizeMb => (int)(SizeBytes >> 20);
    }

    /// <summary>Per-bus limits of the CHS fields, as in the original "New hard disk" dialog.</summary>
    public readonly record struct BusLimits(int MaxCylinders, int MaxHeads, int MaxSectors);

    /// <summary>
    /// The 86Box disk geometry rules, ported from the engine (hdd_image_calc_chs) and its Qt hard disk dialog,
    /// so a disk created or attached here gets exactly the geometry the original would give it.
    /// </summary>
    public static class HddGeometry
    {
        /// <summary>hdd_image_calc_chs(): the VHD-specification CHS calculation for a size in MB.</summary>
        public static DiskGeometry FromSizeMb(long sizeMb)
        {
            ulong ts = (ulong)Math.Max(0, sizeMb) << 11;
            uint spt, heads, cth;

            if (ts > 65535UL * 16 * 255) ts = 65535UL * 16 * 255;

            if (ts >= 65535UL * 16 * 63)
            {
                spt = 255;
                heads = 16;
                cth = (uint)(ts / spt);
            }
            else
            {
                spt = 17;
                cth = (uint)(ts / spt);
                heads = (cth + 1023) / 1024;
                if (heads < 4) heads = 4;
                if (cth >= heads * 1024 || heads > 16)
                {
                    spt = 31;
                    heads = 16;
                    cth = (uint)(ts / spt);
                }
                if (cth >= heads * 1024)
                {
                    spt = 63;
                    heads = 16;
                    cth = (uint)(ts / spt);
                }
            }

            return new DiskGeometry((int)(cth / heads), (int)heads, (int)spt);
        }

        public static BusLimits LimitsFor(string? bus) => (bus ?? "IDE").ToUpperInvariant() switch
        {
            "MFM" => new BusLimits(2047, 15, 26),            // 17 for MFM, 26 for RLL
            "XTA" => new BusLimits(1023, 16, 63),
            "ESDI" => new BusLimits(266305, 16, 99),
            _ => new BusLimits(266305, 255, 255)             // IDE, ATAPI, SCSI
        };

        public static DiskGeometry Clamp(DiskGeometry g, BusLimits l)
            => new(Math.Clamp(g.Cylinders, 1, l.MaxCylinders), Math.Clamp(g.Heads, 1, l.MaxHeads), Math.Clamp(g.Sectors, 1, l.MaxSectors));

        /// <summary>Index into <see cref="HddProfiles.Table"/> matching the geometry, 127 = custom, 128 = custom (large, 16/63).</summary>
        public static int ProfileIndex(DiskGeometry g)
        {
            int selection = 127;
            for (int i = 0; i < 127; i++)
            {
                var p = HddProfiles.Table[i];
                if (g.Cylinders == p.Cylinders && g.Heads == p.Heads && g.Sectors == p.Sectors) selection = i;
            }
            if (selection == 127 && g.Heads == 16 && g.Sectors == 63) selection = 128;
            return selection;
        }

        /// <summary>Combo label of a profile, e.g. "40 MB (CHS: 615, 4, 17)".</summary>
        public static string ProfileLabel(int index)
        {
            if (index == 127) return "Custom…";
            if (index == 128) return "Custom (large)…";
            var p = HddProfiles.Table[index];
            long mb = ((long)p.Cylinders * p.Heads * p.Sectors) >> 11;
            return $"{mb} MB (CHS: {p.Cylinders}, {p.Heads}, {p.Sectors})";
        }

        /// <summary>
        /// Size and geometry of an existing image, derived from the file alone (no manual CHS form):
        /// HDI/HDX carry it in their header, VHD in its footer (through the engine's own reader), raw images
        /// use the 86Box detection rules. Returns null and an error text when the file cannot be used as a disk.
        /// </summary>
        public static DiskGeometry? ReadExisting(string path, out string? error)
        {
            error = null;
            try
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".chd")
                {
                    error = "CHD images are only supported as CD-ROM images; the emulation engine cannot open a CHD as a hard disk.";
                    return null;
                }

                var fi = new FileInfo(path);
                if (!fi.Exists) { error = "The file does not exist."; return null; }

                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                if (ext is ".hdi" or ".hdx")
                {
                    var header = new byte[0x20];
                    if (fs.Read(header, 0, header.Length) < header.Length) { error = "The image header is truncated."; return null; }
                    uint sectorSize = BitConverter.ToUInt32(header, 0x10);
                    if (sectorSize != 512) { error = "HDI or HDX images with a sector size other than 512 are not supported."; return null; }
                    return Validate(new DiskGeometry((int)BitConverter.ToUInt32(header, 0x1C), (int)BitConverter.ToUInt32(header, 0x18), (int)BitConverter.ToUInt32(header, 0x14)), out error);
                }

                // "conectix" cookie: a VHD footer (fixed VHDs carry it at the end, dynamic ones also at the start)
                if (IsVhd(fs))
                {
                    uint c = 0, h = 0, s = 0;
                    int rc = Box86CoreNative.EmuVhdReadGeometry(path, out c, out h, out s);
                    if (rc != 0) { error = $"The VHD could not be read (error {rc})."; return null; }
                    return Validate(new DiskGeometry((int)c, (int)h, (int)s), out error);
                }

                return Validate(FromRawSize(fi.Length), out error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }

        private static bool IsVhd(FileStream fs)
        {
            var cookie = new byte[8];
            byte[] want = { (byte)'c', (byte)'o', (byte)'n', (byte)'e', (byte)'c', (byte)'t', (byte)'i', (byte)'x' };

            fs.Seek(0, SeekOrigin.Begin);
            if (fs.Read(cookie, 0, 8) == 8 && cookie.AsSpan().SequenceEqual(want)) return true;

            if (fs.Length >= 512)
            {
                fs.Seek(-512, SeekOrigin.End);
                if (fs.Read(cookie, 0, 8) == 8 && cookie.AsSpan().SequenceEqual(want)) return true;
            }
            return false;
        }

        /// <summary>The engine's rules for a plain image: 17 sectors/track for small MFM-era sizes, otherwise 16 heads x 63.</summary>
        public static DiskGeometry FromRawSize(long size)
        {
            long sectors, heads;
            if (size % 17 == 0 && size <= 142606336)
            {
                sectors = 17;
                if (size <= 26738688) heads = 4;
                else if (size % 3072 == 0 && size <= 53477376) heads = 6;
                else
                {
                    long i;
                    for (i = 5; i < 16; i++)
                    {
                        if (size % (i << 9) == 0 && size <= ((i * 17) << 19)) break;
                        if (i == 5) i++;
                    }
                    heads = i;
                }
            }
            else
            {
                sectors = 63;
                heads = 16;
            }

            return new DiskGeometry((int)(((size >> 9) / heads) / sectors), (int)heads, (int)sectors);
        }

        private static DiskGeometry? Validate(DiskGeometry g, out string? error)
        {
            var limits = LimitsFor("IDE");
            if (g.Cylinders < 1 || g.Heads < 1 || g.Sectors < 1
                || g.Cylinders > limits.MaxCylinders || g.Heads > limits.MaxHeads || g.Sectors > limits.MaxSectors)
            {
                error = "The image is too large or its geometry is not valid for a hard disk.";
                return null;
            }
            error = null;
            return g;
        }
    }
}
