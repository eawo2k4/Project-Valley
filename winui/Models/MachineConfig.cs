using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Box86_WinUI.Models
{
    public class MachineConfig
    {
        public Dictionary<string, Dictionary<string, string>> Sections { get; } = new(StringComparer.OrdinalIgnoreCase);

        public static MachineConfig Load(string filePath)
        {
            var config = new MachineConfig();
            if (!File.Exists(filePath))
                return config;

            string currentSection = "General";
            if (!config.Sections.ContainsKey(currentSection))
                config.Sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawLine in File.ReadAllLines(filePath, Encoding.UTF8))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith(";"))
                    continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2).Trim();
                    if (!config.Sections.ContainsKey(currentSection))
                        config.Sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    continue;
                }

                int eqIdx = line.IndexOf('=');
                if (eqIdx > 0)
                {
                    string key = line.Substring(0, eqIdx).Trim();
                    string val = line.Substring(eqIdx + 1).Trim();
                    config.Sections[currentSection][key] = val;
                }
            }

            return config;
        }

        /// <summary>
        /// Writes the file atomically: a temp file is written, flushed through to the disk and then swapped in,
        /// so a crash or a failed write can never leave a truncated 86box.cfg. I/O errors propagate to the caller.
        /// </summary>
        public void Save(string filePath)
        {
            string full = Path.GetFullPath(filePath);
            var dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string tmp = full + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(fs, new UTF8Encoding(false)))
            {
                foreach (var section in Sections)
                {
                    writer.WriteLine($"[{section.Key}]");
                    foreach (var kvp in section.Value)
                        writer.WriteLine($"{kvp.Key} = {kvp.Value}");
                    writer.WriteLine();
                }
                writer.Flush();
                fs.Flush(true); // force the OS to write it to the physical disk
            }

            try
            {
                if (File.Exists(full)) File.Replace(tmp, full, null);
                else File.Move(tmp, full);
            }
            catch
            {
                try { File.Delete(tmp); } catch (IOException) { /* best-effort cleanup; the original error is rethrown below */ }
                throw;
            }
        }

        public string GetString(string section, string key, string defaultValue = "")
        {
            if (Sections.TryGetValue(section, out var sec) && sec.TryGetValue(key, out var val))
                return val;
            return defaultValue;
        }

        public void SetString(string section, string key, string value)
        {
            if (!Sections.TryGetValue(section, out var sec))
            {
                sec = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Sections[section] = sec;
            }
            sec[key] = value;
        }

        public bool HasKey(string section, string key)
        {
            return Sections.TryGetValue(section, out var sec) && sec.ContainsKey(key);
        }

        public void RemoveKey(string section, string key)
        {
            if (Sections.TryGetValue(section, out var sec))
                sec.Remove(key);
        }

        public void RemoveSection(string section)
        {
            Sections.Remove(section);
        }

        public int GetInt(string section, string key, int defaultValue = 0)
        {
            var val = GetString(section, key);
            return int.TryParse(val, out int res) ? res : defaultValue;
        }

        public void SetInt(string section, string key, int value)
        {
            SetString(section, key, value.ToString());
        }

        public double GetDouble(string section, string key, double defaultValue = 0)
        {
            var val = GetString(section, key);
            return double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double res)
                ? res
                : defaultValue;
        }

        public bool GetBool(string section, string key, bool defaultValue = false)
        {
            var val = GetString(section, key);
            if (int.TryParse(val, out int intVal)) return intVal != 0;
            if (bool.TryParse(val, out bool boolVal)) return boolVal;
            return defaultValue;
        }

        public void SetBool(string section, string key, bool value)
        {
            SetString(section, key, value ? "1" : "0");
        }
    }
}
