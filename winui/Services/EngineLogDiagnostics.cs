using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Box86_WinUI.Services
{
    /// <summary>
    /// Reads the engine's own 86box.log from the VM folder and pulls out the lines that explain a
    /// failed or degraded start (missing ROMs, unknown devices, unreadable configuration).
    /// </summary>
    public static class EngineLogDiagnostics
    {
        private static readonly Regex Interesting = new(
            @"(rom|bios).*(missing|not found|unavailable|failed|could not|cannot)|" +
            @"(missing|not found|unavailable|failed|invalid|unknown|could not|cannot).*(rom|bios|device|card|machine|cpu|config)|" +
            @"\b(error|fatal)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Extra lines for error code 30 (set by the engine service right before it refuses to start).</summary>
        public static string StartDetail { get; set; } = string.Empty;

        public static string DescribeStartError(int code) => code switch
        {
            30 => "A hard disk image this machine uses could not be found. Starting it would crash the emulator on the next reset, so it was not started. Attach the disk again in Machine Settings (Hard disks), or restore the file:" + Environment.NewLine + StartDetail,
            1 => "The emulation core could not read the machine configuration (pc_init failed).",
            2 => "One or more ROM images required by this machine are missing (pc_init_roms failed). Check the ROM directory in Manager Settings.",
            3 => "The machine folder or its 86box.cfg could not be found.",
            _ => $"The emulation core returned error code {code}."
        };

        /// <summary>Interesting lines from the VM's 86box.log (most recent last), also written to the debug output.</summary>
        public static IReadOnlyList<string> Scan(string vmDirectory, int maxLines = 6)
        {
            var result = new List<string>();
            try
            {
                string log = Path.Combine(vmDirectory, "86box.log");
                if (!File.Exists(log))
                {
                    Debug.WriteLine($"[EngineLog] no log at {log}");
                    return result;
                }

                using var fs = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var sr = new StreamReader(fs);
                foreach (var line in sr.ReadToEnd().Split('\n'))
                {
                    string l = line.Trim();
                    if (l.Length == 0 || l.StartsWith('#')) continue;
                    if (Interesting.IsMatch(l))
                    {
                        Debug.WriteLine($"[EngineLog] {l}");
                        result.Add(l);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EngineLog] failed to read log: {ex.Message}");
            }

            return result.Count <= maxLines ? result : result.Skip(result.Count - maxLines).ToList();
        }
    }
}
