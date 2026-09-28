using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using Box86_WinUI.Interop;
using Box86_WinUI.Models;
using Box86_WinUI.Services;
using Box86_WinUI.ViewModels;

namespace Box86_WinUI.Views
{
    /// <summary>
    /// "Create New Hard Disk": file, size and format on top, the next free bus channel chosen for you, and the drive
    /// profile with manual cylinders/heads/sectors tucked into a collapsed Expander. Size and geometry follow each
    /// other exactly like the original 86Box dialog (hdd_image_calc_chs, the 127 historical profiles, per-bus limits).
    /// </summary>
    public sealed partial class NewHardDiskPanel : UserControl
    {
        private readonly SettingsViewModel _vm;
        private readonly IntPtr _ownerHwnd;
        private readonly string _vmDirectory;
        private bool _updating;

        public string CreatedPath { get; private set; } = string.Empty;
        public DiskGeometry Geometry { get; private set; }
        public string Bus => (string)(BusCombo.SelectedItem ?? "IDE");
        public string Channel => (string)(ChannelCombo.SelectedValue ?? "0:0");

        private bool IsVhd => FormatCombo.SelectedIndex == 0;

        public string PanelTitle => "Create hard disk";

        public NewHardDiskPanel(SettingsViewModel vm, IntPtr ownerHwnd)
        {
            InitializeComponent();
            _vm = vm;
            _ownerHwnd = ownerHwnd;
            _vmDirectory = string.IsNullOrWhiteSpace(vm.VM.Path) ? Environment.CurrentDirectory : vm.VM.Path;

            ProfileCombo.ItemsSource = Enumerable.Range(0, 129).Where(i => i != 127 || true).Select(HddGeometry.ProfileLabel).ToList();

            BusCombo.ItemsSource = HardwareCatalog.HddBuses;
            var (bus, channel) = vm.NextFreeBusChannel();
            _updating = true;
            BusCombo.SelectedItem = bus;
            FillChannels(bus, channel);
            FormatCombo.SelectedIndex = 0;
            FileNameBox.Text = UniqueName("hdd", ".vhd");
            LocationText.Text = $"Created in the machine folder: {_vmDirectory}";

            // NumberBox.Value is set here, not as a XAML literal ("Value=\"512\""): assigning it
            // from a plain XAML string crashes InitializeComponent (XamlParseException, "Failed to
            // assign to property NumberBox.Value") on this WinAppSDK version. Cylinders/Heads/Sectors
            // don't need the same treatment - ApplySize() below fills them in from this size anyway.
            SizeBox.Value = 512;
            _updating = false;

            AutoChannelText.Text = $"Next available channel selected automatically: {bus} {channel}";
            ApplySize();
        }

        private string UniqueName(string stem, string ext)
        {
            for (int n = _vm.HardDisks.Count + 1; n < 1000; n++)
            {
                string candidate = $"{stem}_{n:D2}{ext}";
                if (!File.Exists(Path.Combine(_vmDirectory, candidate))) return candidate;
            }
            return stem + ext;
        }

        // ------------------------------------------------------------ bus / channel
        private void FillChannels(string bus, string? select)
        {
            // Channels another drive already uses stay in the list but cannot be picked.
            var channels = _vm.GetChannelOptions(bus, null);
            ChannelCombo.ItemsSource = channels;
            var wanted = channels.FirstOrDefault(c => c.Name == select && c.IsAvailable) ?? channels.FirstOrDefault(c => c.IsAvailable);
            ChannelCombo.SelectedValue = wanted?.Name;
        }

        private void OnBusChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updating || BusCombo.SelectedItem is not string bus) return;
            var (autoBus, autoChannel) = _vm.NextFreeBusChannel(bus);
            FillChannels(bus, autoChannel);
            AutoChannelText.Text = $"Next available channel selected automatically: {bus} {autoChannel}";

            // the geometry limits depend on the bus (MFM / XTA / ESDI are much tighter than IDE)
            var limits = HddGeometry.LimitsFor(bus);
            CylindersBox.Maximum = limits.MaxCylinders;
            HeadsBox.Maximum = limits.MaxHeads;
            SectorsBox.Maximum = limits.MaxSectors;
            ApplyGeometry(HddGeometry.Clamp(CurrentGeometry(), limits), updateSize: true);
        }

        // ------------------------------------------------------------ size <-> geometry
        private DiskGeometry CurrentGeometry()
            => new((int)Val(CylindersBox), (int)Val(HeadsBox), (int)Val(SectorsBox));

        private static double Val(NumberBox b) => double.IsNaN(b.Value) ? b.Minimum : b.Value;

        private void ApplyGeometry(DiskGeometry g, bool updateSize)
        {
            _updating = true;
            try
            {
                CylindersBox.Value = g.Cylinders;
                HeadsBox.Value = g.Heads;
                SectorsBox.Value = g.Sectors;
                if (updateSize) SizeBox.Value = Math.Max(1, g.SizeMb);
                ProfileCombo.SelectedIndex = HddGeometry.ProfileIndex(g);
                GeometrySummary.Text = $"{g.SizeMb} MB - {g.Cylinders} cylinders x {g.Heads} heads x {g.Sectors} sectors";
            }
            finally { _updating = false; }
        }

        /// <summary>Size edited: derive C/H/S from it (hdd_image_calc_chs), then clamp to the bus limits.</summary>
        private void ApplySize()
        {
            var g = HddGeometry.Clamp(HddGeometry.FromSizeMb((long)Val(SizeBox)), HddGeometry.LimitsFor(Bus));
            ApplyGeometry(g, updateSize: false);
        }

        private void OnSizeChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_updating) return;
            ApplySize();
        }

        /// <summary>Manual C/H/S edit: the size follows from the geometry.</summary>
        private void OnChsChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_updating) return;
            ApplyGeometry(HddGeometry.Clamp(CurrentGeometry(), HddGeometry.LimitsFor(Bus)), updateSize: true);
        }

        private void OnProfileChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updating || ProfileCombo.SelectedIndex < 0) return;
            int index = ProfileCombo.SelectedIndex;
            var limits = HddGeometry.LimitsFor(Bus);

            if (index < 127)
            {
                var p = HddProfiles.Table[index];
                ApplyGeometry(HddGeometry.Clamp(new DiskGeometry(p.Cylinders, p.Heads, p.Sectors), limits), updateSize: true);
            }
            else if (index == 128)
            {
                ApplyGeometry(HddGeometry.Clamp(new DiskGeometry((int)Val(CylindersBox), 16, 63), limits), updateSize: true);
            }
        }

        // ------------------------------------------------------------ file / format
        private void OnFormatChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updating || FileNameBox == null) return;
            string want = IsVhd ? ".vhd" : ".img";
            string current = FileNameBox.Text;
            if (!string.IsNullOrWhiteSpace(current))
                FileNameBox.Text = Path.ChangeExtension(current, want);
        }

        private void OnFileNameChanged(object sender, TextChangedEventArgs e) => ErrorBar.IsOpen = false;

        // ------------------------------------------------------------ creation
        /// <summary>Validates and creates the image. False keeps the panel open with the reason shown in the error bar.</summary>
        public async Task<bool> TryCreateAsync()
        {
            try
            {
                ErrorBar.IsOpen = false;
                string name = FileNameBox.Text.Trim();
                if (name.Length > 0 && string.IsNullOrEmpty(Path.GetExtension(name)))
                    name += IsVhd ? ".vhd" : ".img";
                string path = name.Length == 0 ? string.Empty : Path.Combine(_vmDirectory, name);
                string? problem = Validate(name, path);
                if (problem != null) { Fail(problem); return false; }

                var g = HddGeometry.Clamp(CurrentGeometry(), HddGeometry.LimitsFor(Bus));
                Progress.Visibility = Visibility.Visible;

                bool vhd = IsVhd;
                var result = await Task.Run(() => CreateImage(path, g, vhd));
                if (result.Error != null) { Fail(result.Error); return false; }

                CreatedPath = path;
                Geometry = result.Geometry;
                return true;
            }
            catch (Exception ex)
            {
                Fail(ex.Message);
                return false;
            }
            finally
            {
                Progress.Visibility = Visibility.Collapsed;
            }
        }

        private void Fail(string message)
        {
            ErrorBar.Message = message;
            ErrorBar.IsOpen = true;
        }

        private string? Validate(string name, string path)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Enter a name for the new disk.";
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return "The name contains characters that are not allowed.";
            if (File.Exists(path)) return "A disk with this name already exists. Choose another name.";
            if (Val(SizeBox) < 1) return "The size must be at least 1 MB.";

            string? dir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return $"The folder '{dir}' does not exist.";
            return null;
        }

        private static (DiskGeometry Geometry, string? Error) CreateImage(string path, DiskGeometry g, bool vhd)
        {
            try
            {
                if (vhd)
                {
                    int rc = Box86CoreNative.EmuVhdCreateDynamic(path, (uint)g.Cylinders, (uint)g.Heads, (uint)g.Sectors,
                                                                 out uint c, out uint h, out uint s);
                    return rc == 0
                        ? (new DiskGeometry((int)c, (int)h, (int)s), null)
                        : (g, $"The VHD could not be created (error {rc}). Check that the folder is writable.");
                }

                using var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                fs.SetLength(g.SizeBytes); // a raw image is simply zero-filled to size
                fs.Flush(true);
                return (g, null);
            }
            catch (Exception ex)
            {
                try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { /* keep the original error */ }
                return (g, ex.Message);
            }
        }
    }
}
