using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using Box86_WinUI.Models;
using Box86_WinUI.Services;
using Box86_WinUI.ViewModels;

namespace Box86_WinUI.Views
{
    /// <summary>
    /// One dialog for every device: the controls are generated from the engine's own device_config_t table
    /// (<see cref="DeviceConfigCatalog"/>), so a sound card, FluidSynth, an MPU-401 or a Voodoo shows exactly the
    /// options the engine reads, with its ranges, choices and defaults. Values are written to [device name] in 86box.cfg.
    /// </summary>
    public sealed partial class DeviceConfigPanel : UserControl
    {
        private const string EdidDefaultId = "";
        private const string EdidCustomId = "custom";

        private sealed class Row
        {
            public required ConfigItem Item;
            public required FrameworkElement Control;
            public Func<string?> Read = () => null;          // value as it is written to 86box.cfg (null = leave untouched)
            public Action<ConfigBios?>? ApplyBios;           // memory rows re-filter their choices when the card type changes
            public List<ConfigChoice> Choices = new();
        }

        private readonly SettingsViewModel _vm;
        private readonly IntPtr _ownerHwnd;
        private readonly bool _isEdid;
        private readonly string _section = string.Empty;
        private readonly List<Row> _rows = new();
        private string _customEdidPath;

        /// <summary>Heading of the overlay this panel is shown in.</summary>
        public string PanelTitle { get; private set; }
        public string PrimaryText { get; private set; } = "OK";
        public string CloseText { get; private set; } = "Cancel";

        public DeviceConfigPanel(SettingsViewModel vm, IntPtr ownerHwnd, string deviceTag)
        {
            InitializeComponent();
            _vm = vm;
            _ownerHwnd = ownerHwnd;
            PanelTitle = $"Configure {deviceTag}";
            _customEdidPath = vm.CustomEdidPath;

            if (deviceTag == "Monitor EDID")
            {
                _isEdid = true;
                IntroText.Text = "Stored as monitor_edid / monitor_edid_path under [Video].";
                BuildEdid();
                return;
            }

            var resolved = vm.ResolveConfigDevice(deviceTag);
            if (resolved is not { } dev
                || !DeviceConfigCatalog.Devices.TryGetValue(dev.DeviceName, out var entry)
                || entry.Items.Length == 0)
            {
                ShowNoOptions(resolved?.DeviceName ?? deviceTag);
                return;
            }

            _section = dev.Section;
            PanelTitle = $"Configure {dev.DeviceName}";
            IntroText.Text = $"Stored under [{_section}] in 86box.cfg.";

            foreach (var item in entry.Items)
                AddRow(item);

            ApplyBiosLimits();
        }

        private void ShowNoOptions(string name)
        {
            NoOptionsText.Text = $"{name} has no configurable options.";
            NoOptionsText.Visibility = Visibility.Visible;
            PrimaryText = string.Empty;
            CloseText = "Close";
        }

        // ---------------------------------------------------------------- stored values
        private string? Stored(ConfigItem item)
        {
            var id = DeviceSettingKey.Make(_section, item.Name);
            if (_vm.DeviceSettings.TryGetValue(id, out var pending)) return pending;
            return _vm.ReadDeviceSetting(_section, item.Name);
        }

        /// <summary>Numbers in 86box.cfg are decimal, except hex16/hex20 options which are stored as hex digits.</summary>
        private static int ParseNumber(string? s, int fallback, bool hex)
        {
            if (string.IsNullOrWhiteSpace(s)) return fallback;
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) { s = s.Substring(2); hex = true; }
            return int.TryParse(s, hex ? NumberStyles.AllowHexSpecifier : NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;
        }

        // ---------------------------------------------------------------- control generation
        private void AddRow(ConfigItem item)
        {
            string? stored = Stored(item);
            Row? row;

            switch (item.Kind)
            {
                case ConfigKind.Binary:
                {
                    bool on = stored != null ? stored.Trim() != "0" : item.DefaultInt != 0;
                    var cb = new CheckBox { Content = item.Description, IsChecked = on };
                    row = new Row { Item = item, Control = cb, Read = () => cb.IsChecked == true ? "1" : "0" };
                    break;
                }

                case ConfigKind.Selection:
                case ConfigKind.Hex16:
                case ConfigKind.Hex20:
                {
                    bool hex = item.Kind != ConfigKind.Selection;
                    int current = ParseNumber(stored, item.DefaultInt, hex);
                    row = NewChoiceRow(item, item.Choices.ToList(), current,
                        v => item.Kind switch
                        {
                            ConfigKind.Hex16 => v.ToString("X4", CultureInfo.InvariantCulture),
                            ConfigKind.Hex20 => v.ToString("X5", CultureInfo.InvariantCulture),
                            _ => v.ToString(CultureInfo.InvariantCulture)
                        });
                    break;
                }

                case ConfigKind.Spinner:
                    row = NewSpinnerRow(item, ParseNumber(stored, item.DefaultInt, false));
                    break;

                case ConfigKind.File:
                    row = NewFileRow(item, stored ?? item.DefaultString);
                    break;

                case ConfigKind.String:
                case ConfigKind.Mac:
                {
                    var box = new TextBox { Header = item.Description, Text = stored ?? item.DefaultString };
                    row = new Row { Item = item, Control = box, Read = () => box.Text };
                    break;
                }

                case ConfigKind.Bios:
                {
                    var combo = new ComboBox { Header = item.Description, HorizontalAlignment = HorizontalAlignment.Stretch };
                    combo.ItemsSource = item.Bios.Select(b => b.Name).ToList();
                    string cur = stored ?? item.DefaultString;
                    int idx = Array.FindIndex(item.Bios, b => b.Internal == cur);
                    combo.SelectedIndex = idx >= 0 ? idx : 0;
                    combo.SelectionChanged += (_, _) => ApplyBiosLimits();
                    row = new Row
                    {
                        Item = item, Control = combo,
                        Read = () => combo.SelectedIndex >= 0 ? item.Bios[combo.SelectedIndex].Internal : item.DefaultString
                    };
                    break;
                }

                default: // host MIDI in/out device pickers need the host device list, which this dialog does not have yet
                {
                    var note = new TextBlock
                    {
                        Text = $"{item.Description}: choose the host MIDI device in the Sound settings.",
                        TextWrapping = TextWrapping.Wrap,
                        Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                        Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                    };
                    row = new Row { Item = item, Control = note, Read = () => null };
                    break;
                }
            }

            _rows.Add(row);
            SettingsHost.Children.Add(row.Control);
        }

        private Row NewChoiceRow(ConfigItem item, List<ConfigChoice> choices, int current, Func<int, string> format)
        {
            var combo = new ComboBox { Header = item.Description, HorizontalAlignment = HorizontalAlignment.Stretch };
            var row = new Row { Item = item, Control = combo, Choices = choices };

            void Fill(List<ConfigChoice> list, int select)
            {
                row.Choices = list;
                combo.ItemsSource = list.Select(c => c.Label).ToList();
                int idx = list.FindIndex(c => c.Value == select);
                if (idx < 0 && list.Count > 0)
                    idx = list.Select((c, i) => (c, i)).OrderBy(x => Math.Abs(x.c.Value - select)).First().i; // nearest allowed, like the engine's clamp
                combo.SelectedIndex = idx;
                combo.IsEnabled = list.Count >= 2; // a single allowed value is shown but fixed
            }

            Fill(choices, current);
            row.Read = () => combo.SelectedIndex >= 0 && combo.SelectedIndex < row.Choices.Count
                ? format(row.Choices[combo.SelectedIndex].Value) : format(item.DefaultInt);

            // memory choices are limited by the selected card type (BIOS_LIMIT_* flags in the engine)
            bool isMemory = item.Name is "memory" or "framebuffer_memory";
            bool isMemory2 = item.Name == "texture_memory";
            if (isMemory || isMemory2)
            {
                row.ApplyBios = bios =>
                {
                    int min = 0, max = 65535;
                    if (bios is { } b)
                    {
                        int lo = isMemory ? b.MinMem : b.MinMem2, hi = isMemory ? b.MaxMem : b.MaxMem2;
                        if (lo >= 0) min = lo;
                        if (hi >= 0) max = hi;
                    }
                    int keep = row.Choices.Count > 0 && combo.SelectedIndex >= 0 && combo.SelectedIndex < row.Choices.Count
                        ? row.Choices[combo.SelectedIndex].Value : current;
                    Fill(item.Choices.Where(c => c.Value >= min && c.Value <= max).ToList(), Math.Min(Math.Max(keep, min), max));
                };
            }
            return row;
        }

        private Row NewSpinnerRow(ConfigItem item, int current)
        {
            int min = item.Min, max = item.Max, step = Math.Max(1, item.Step);
            current = Math.Min(Math.Max(current, min), max);

            // 0..100-style ranges (gain, levels, room size) read best as a slider; wide ranges get a NumberBox.
            if (max - min <= 100)
            {
                var value = new TextBlock { Text = current.ToString(CultureInfo.InvariantCulture), MinWidth = 36, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                var slider = new Slider { Minimum = min, Maximum = max, StepFrequency = step, Value = current, Header = item.Description };
                slider.ValueChanged += (_, e) => value.Text = ((int)e.NewValue).ToString(CultureInfo.InvariantCulture);
                var grid = new Grid { ColumnSpacing = 10 };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.Children.Add(slider);
                Grid.SetColumn(value, 1);
                grid.Children.Add(value);
                return new Row { Item = item, Control = grid, Read = () => ((int)slider.Value).ToString(CultureInfo.InvariantCulture) };
            }

            var nb = new NumberBox
            {
                Header = item.Description, Minimum = min, Maximum = max, SmallChange = step, LargeChange = step * 10,
                Value = current, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
            };
            return new Row { Item = item, Control = nb, Read = () => ((int)(double.IsNaN(nb.Value) ? current : nb.Value)).ToString(CultureInfo.InvariantCulture) };
        }

        private Row NewFileRow(ConfigItem item, string current)
        {
            var path = new TextBox { Text = current, IsReadOnly = true, PlaceholderText = "No file selected", HorizontalAlignment = HorizontalAlignment.Stretch };
            var browse = new Button { Content = "Browse…" };
            var clear = new Button { Content = "Clear" };

            // "SoundFont files (*.sf2 *.sf3)|*.sf2,*.sf3" -> .sf2 .sf3
            var extensions = item.FileFilter.Contains('|')
                ? item.FileFilter.Substring(item.FileFilter.IndexOf('|') + 1).Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(e => "." + e.TrimStart('*', '.')).Distinct().ToList()
                : new List<string>();

            browse.Click += async (_, _) =>
            {
                var picker = new FileOpenPicker();
                foreach (var e in extensions) picker.FileTypeFilter.Add(e);
                picker.FileTypeFilter.Add("*");
                WinRT.Interop.InitializeWithWindow.Initialize(picker, _ownerHwnd);
                var file = await picker.PickSingleFileAsync();
                if (file != null) path.Text = file.Path;
            };
            clear.Click += (_, _) => path.Text = string.Empty;

            var grid = new Grid { ColumnSpacing = 8, RowSpacing = 4 };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var header = new TextBlock { Text = item.Description };
            Grid.SetColumnSpan(header, 3);
            grid.Children.Add(header);
            Grid.SetRow(path, 1); grid.Children.Add(path);
            Grid.SetRow(browse, 1); Grid.SetColumn(browse, 1); grid.Children.Add(browse);
            Grid.SetRow(clear, 1); Grid.SetColumn(clear, 2); grid.Children.Add(clear);

            return new Row { Item = item, Control = grid, Read = () => path.Text };
        }

        /// <summary>Re-filters the memory sizes for the card type (Voodoo: framebuffer per model) after the type changed.</summary>
        private void ApplyBiosLimits()
        {
            var biosRow = _rows.FirstOrDefault(r => r.Item.Kind == ConfigKind.Bios);
            ConfigBios? selected = null;
            if (biosRow?.Control is ComboBox c && c.SelectedIndex >= 0 && c.SelectedIndex < biosRow.Item.Bios.Length)
                selected = biosRow.Item.Bios[c.SelectedIndex];

            foreach (var r in _rows) r.ApplyBios?.Invoke(selected);
        }

        // ---------------------------------------------------------------- Monitor EDID
        private void BuildEdid()
        {
            EdidPanel.Visibility = Visibility.Visible;
            var items = new List<(string Id, string Label)> { (EdidDefaultId, "Default (built-in EDID)") };
            items.AddRange(EdidBuilder.Profiles.Select(p => (p.Id, p.Label)));
            items.Add((EdidCustomId, "Custom EDID file…"));

            EdidProfileCombo.ItemsSource = items.Select(i => i.Label).ToList();
            EdidProfileCombo.Tag = items;

            string sel = _vm.MonitorEdidMode == "Custom" ? EdidCustomId : (_vm.EdidProfileId ?? EdidDefaultId);
            int idx = items.FindIndex(i => i.Id == sel);
            EdidProfileCombo.SelectedIndex = idx >= 0 ? idx : 0;
            UpdateEdidPathText();
        }

        private string SelectedEdidId
            => EdidProfileCombo.Tag is List<(string Id, string Label)> l && EdidProfileCombo.SelectedIndex >= 0
                ? l[EdidProfileCombo.SelectedIndex].Id : EdidDefaultId;

        private void UpdateEdidPathText()
        {
            EdidBrowseButton.IsEnabled = SelectedEdidId == EdidCustomId;
            EdidPathText.Text = SelectedEdidId == EdidCustomId ? _customEdidPath : string.Empty;
        }

        private void OnEdidProfileChanged(object sender, SelectionChangedEventArgs e) => UpdateEdidPathText();

        private async void OnBrowseEdidClicked(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".bin");
            picker.FileTypeFilter.Add(".hex");
            picker.FileTypeFilter.Add(".edid");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, _ownerHwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                _customEdidPath = file.Path;
                UpdateEdidPathText();
            }
        }

        /// <summary>Copies the dialog's choices into the view model; they are flushed to 86box.cfg when the settings are saved.</summary>
        public void Commit()
        {
            if (_isEdid)
            {
                string id = SelectedEdidId;
                if (id == EdidDefaultId)
                {
                    _vm.MonitorEdidMode = "Default";
                    _vm.EdidProfileId = null;
                }
                else if (id == EdidCustomId)
                {
                    _vm.MonitorEdidMode = "Custom";
                    _vm.EdidProfileId = null;
                    _vm.CustomEdidPath = _customEdidPath;
                }
                else
                {
                    _vm.MonitorEdidMode = "Custom";
                    _vm.EdidProfileId = id; // file is generated into the VM folder when settings are saved
                }
                return;
            }

            foreach (var row in _rows)
            {
                string? value = row.Read();
                if (value == null) continue;
                _vm.DeviceSettings[DeviceSettingKey.Make(_section, row.Item.Name)] = value;
            }
        }
    }
}
