using System;
using System.Collections.Generic;
using System.Linq;

namespace Box86_WinUI.Models
{
    /// <summary>
    /// Classifies a video-card entry's engine label (e.g. "[PCI] S3 Trio64V+") by its bus, for the
    /// grouped "Video:" combo box. The engine already restricts which cards a machine offers at
    /// all (device_is_valid against machine->flags, queried through EngineDeviceLists) - this only
    /// decides how those already-compatible entries are grouped and ordered for display, newest
    /// bus first so Socket 7 / Slot 1 boards show PCI/AGP up top while an 8088 or 286 is left with
    /// only its ISA (or MCA, on a PS/2) group.
    /// </summary>
    public static class VideoBusGrouping
    {
        public const string GeneralGroup = "General";

        /// <summary>(header text, sort priority - lower sorts first) for one engine label.</summary>
        public static (string Group, int Priority) Classify(string label)
        {
            if (string.IsNullOrEmpty(label) || label[0] != '[')
                return (GeneralGroup, -1); // "None", "Internal device"

            int end = label.IndexOf(']');
            if (end <= 1)
                return (GeneralGroup, -1);

            string bus = label.Substring(1, end - 1).ToUpperInvariant();
            return bus switch
            {
                "AGP" => ("AGP Video Adapters", 0),
                "PCI" => ("PCI Video Adapters", 1),
                "VLB" => ("VLB Video Adapters", 2),
                "ISA" or "ISA16" => ("ISA Video Adapters", 3),
                "MCA" or "MCA32" => ("MCA Video Adapters", 4),
                _ => ("Other Video Adapters", 5)
            };
        }

        /// <summary>
        /// The engine's own list, grouped by bus (newest first) for the combo box, "None"/"Internal
        /// device" kept in their own ungrouped-looking header at the very top. Item order within a
        /// group follows the engine's own order (roughly chronological already).
        /// </summary>
        public static List<VideoAdapterGroup> Group(IEnumerable<string> labels)
        {
            var buckets = new SortedDictionary<int, VideoAdapterGroup>();
            foreach (var label in labels)
            {
                var (group, priority) = Classify(label);
                if (!buckets.TryGetValue(priority, out var bucket))
                    buckets[priority] = bucket = new VideoAdapterGroup(group);
                bucket.Add(new VideoAdapterEntry(label));
            }
            return new List<VideoAdapterGroup>(buckets.Values);
        }

        /// <summary>Per bus, the well-behaved 2D cards a board should default to, best first (substring of the label).</summary>
        private static readonly Dictionary<int, string[]> PreferredCards = new()
        {
            [0] = new[] { "Rage Pro", "Voodoo3 3000", "Matrox Millennium II", "Trio3D/2X", "ViRGE" },          // AGP
            [1] = new[] { "S3 Trio64V+", "S3 Trio64", "S3 ViRGE", "S3 Trio32" },               // PCI
            [2] = new[] { "S3 Trio64V+", "S3 Trio64", "Cirrus Logic GD5428", "S3 Trio32" },    // VLB
            [3] = new[] { "IBM VGA", "Tseng Labs ET4000AX", "Cirrus Logic GD5428", "Cirrus Logic GD5402" }, // ISA
            [4] = new[] { "Cirrus Logic GD5428", "Tseng Labs ET4000AX", "IBM VGA" },           // MCA
        };

        /// <summary>
        /// The card a machine switch falls back to when the previous one is no longer compatible: a
        /// real card (never "None") from the newest bus this board has. Within that bus a plain,
        /// well-behaved 2D card is preferred (S3 Trio64V+ on PCI, IBM VGA on ISA, ...) rather than
        /// whatever sorts first - that is a 3dfx Banshee, a 3D accelerator that stops the engine
        /// outright on some boards. Returns null when the list has no real card at all.
        /// </summary>
        public static string? HighestPriorityCard(IEnumerable<string> labels)
        {
            var real = labels.Select(l => (Label: l, Priority: Classify(l).Priority))
                             .Where(x => x.Priority >= 0 && x.Priority < 5)
                             .ToList();
            if (real.Count == 0)
                real = labels.Select(l => (Label: l, Priority: Classify(l).Priority)).Where(x => x.Priority >= 0).ToList();
            if (real.Count == 0) return null;

            int best = real.Min(x => x.Priority);
            var inBus = real.Where(x => x.Priority == best).Select(x => x.Label).ToList();

            if (PreferredCards.TryGetValue(best, out var prefs))
                foreach (var pref in prefs)
                {
                    var hit = inBus.FirstOrDefault(l => l.Contains(pref, StringComparison.OrdinalIgnoreCase));
                    if (hit != null) return hit;
                }

            return inBus.FirstOrDefault(l => !l.Contains("3dfx", StringComparison.OrdinalIgnoreCase)) ?? inBus[0];
        }
    }

    /// <summary>
    /// One selectable entry in a grouped video-adapter combo box. Value equality by label: the
    /// grouped list is rebuilt (fresh instances) every time it is asked for, so the combo box's
    /// SelectedItem - matched by Equals, not by reference - would otherwise show no selection.
    /// </summary>
    public sealed class VideoAdapterEntry : IEquatable<VideoAdapterEntry>
    {
        public string Label { get; }
        public VideoAdapterEntry(string label) => Label = label;

        public bool Equals(VideoAdapterEntry? other) => other != null && Label == other.Label;
        public override bool Equals(object? obj) => Equals(obj as VideoAdapterEntry);
        public override int GetHashCode() => Label.GetHashCode();
    }

    /// <summary>One bus header ("PCI Video Adapters", ...) and the cards under it, for a grouped
    /// ComboBox bound with IsSourceGrouped="True".</summary>
    public sealed class VideoAdapterGroup : List<VideoAdapterEntry>
    {
        public string GroupName { get; }
        public VideoAdapterGroup(string groupName) => GroupName = groupName;
    }
}
