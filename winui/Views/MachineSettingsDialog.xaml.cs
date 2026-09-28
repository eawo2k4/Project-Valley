using System;
using System.Threading.Tasks;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Box86_WinUI.Models;
using Box86_WinUI.ViewModels;

namespace Box86_WinUI.Views
{
    /// <summary>
    /// The machine settings, shown as a single native ContentDialog. Sub-panels (New Hard Disk, device options,
    /// messages) appear as an overlay inside it: WinUI cannot show a second ContentDialog while one is open.
    /// </summary>
    public sealed partial class MachineSettingsDialog : ContentDialog
    {
        public SettingsViewModel ViewModel { get; }

        private readonly IntPtr _ownerHwnd;
        private TaskCompletionSource<bool>? _overlayResult;
        private Func<Task<bool>>? _overlayPrimary;

        /// <summary>True when the settings were saved while the machine was running: the caller tells the user to restart.</summary>
        public bool RestartRequired { get; private set; }

        public MachineSettingsDialog(VirtualMachine vm, IntPtr ownerHwnd, XamlRoot xamlRoot, double availableHeight)
        {
            _ownerHwnd = ownerHwnd;
            // This dialog has no built-in Title/PrimaryButtonText/CloseButtonText (see the custom
            // header and footer in the XAML), so nothing else closes it automatically on Save/Cancel -
            // the view model's own "I'm done" signal is what does that.
            ViewModel = new SettingsViewModel(vm, () => this.Hide());
            this.InitializeComponent();
            XamlRoot = xamlRoot;

            // Size the page to the window it is shown over.
            DialogRoot.Width = Math.Min(1120, Math.Max(760, xamlRoot.Size.Width - 96));
            DialogRoot.Height = Math.Min(700, Math.Max(440, availableHeight - 140));

            ViewModel.RestartRequired += () =>
            {
                RestartRequired = true;
                this.Hide();
            };

            // Esc closes the overlay first, and only then the dialog.
            Closing += (s, args) =>
            {
                if (OverlayScrim.Visibility == Visibility.Visible)
                {
                    args.Cancel = true;
                    CloseOverlay(false);
                }
            };
        }

        /// <summary>The custom footer's Save button. ViewModel.Save() closes the dialog itself
        /// (via the constructor's callback) once it succeeds; on a save error it stays open with
        /// the InfoBar showing why, exactly as the old PrimaryButtonClick handler did.</summary>
        private void OnSaveClicked(object sender, RoutedEventArgs e)
        {
            if (OverlayScrim.Visibility == Visibility.Visible) return; // covered by the scrim anyway
            ViewModel.Save();
        }

        /// <summary>The custom footer's Cancel button. An open overlay panel is closed first,
        /// matching Esc's behaviour (see the Closing handler above); otherwise the whole dialog closes.</summary>
        private void OnCancelClicked(object sender, RoutedEventArgs e)
        {
            if (OverlayScrim.Visibility == Visibility.Visible)
            {
                CloseOverlay(false);
                return;
            }
            ViewModel.Cancel();
        }

        // ---------------------------------------------------------------- overlay
        /// <summary>Shows a panel over the settings page; completes true when its primary button succeeded.</summary>
        public Task<bool> ShowOverlayAsync(FrameworkElement panel, string title, string primaryText, string closeText, Func<Task<bool>>? primaryAction = null)
        {
            _overlayResult?.TrySetResult(false);
            _overlayResult = new TaskCompletionSource<bool>();
            _overlayPrimary = primaryAction;

            OverlayTitle.Text = title;
            OverlayHost.Content = panel;
            OverlayPrimaryButton.Content = primaryText;
            OverlayPrimaryButton.Visibility = string.IsNullOrEmpty(primaryText) ? Visibility.Collapsed : Visibility.Visible;
            OverlayPrimaryButton.IsEnabled = true;
            OverlayCloseButton.Content = closeText;
            OverlayScrim.Visibility = Visibility.Visible;
            return _overlayResult.Task;
        }

        /// <summary>A short message with an OK button.</summary>
        private Task<bool> ShowMessageAsync(string title, string message)
            => ShowOverlayAsync(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, title, string.Empty, "OK");

        private void CloseOverlay(bool result)
        {
            OverlayScrim.Visibility = Visibility.Collapsed;
            OverlayHost.Content = null;
            _overlayPrimary = null;
            _overlayResult?.TrySetResult(result);
            _overlayResult = null;
        }

        private async void OnOverlayPrimaryClick(object sender, RoutedEventArgs e)
        {
            bool ok = true;
            if (_overlayPrimary != null)
            {
                OverlayPrimaryButton.IsEnabled = false;
                try { ok = await _overlayPrimary(); }
                finally { OverlayPrimaryButton.IsEnabled = true; }
            }
            if (ok) CloseOverlay(true);
        }

        private void OnOverlayCloseClick(object sender, RoutedEventArgs e) => CloseOverlay(false);

        private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                ViewModel.CurrentCategory = tag;
            }
        }

        private async void OnOpenConfigFileClicked(object sender, RoutedEventArgs e)
        {
            await ViewModel.OpenConfigFileAsync();
        }

        private void OnSelectPortsSubTab0(object sender, RoutedEventArgs e)
        {
            ViewModel.PortsSubTabIndex = 0;
        }

        private void OnSelectPortsSubTab1(object sender, RoutedEventArgs e)
        {
            ViewModel.PortsSubTabIndex = 1;
        }

        private void OnSelectStorageSubTab0(object sender, RoutedEventArgs e)
        {
            ViewModel.StorageSubTabIndex = 0;
        }

        private void OnSelectStorageSubTab1(object sender, RoutedEventArgs e)
        {
            ViewModel.StorageSubTabIndex = 1;
        }

        private void OnSelectFloppyCDSubTab0(object sender, RoutedEventArgs e)
        {
            ViewModel.FloppyCDSubTabIndex = 0;
        }

        private void OnSelectFloppyCDSubTab1(object sender, RoutedEventArgs e)
        {
            ViewModel.FloppyCDSubTabIndex = 1;
        }

        private void OnSelectDisplaySubTab0(object sender, RoutedEventArgs e)
        {
            ViewModel.DisplaySubTabIndex = 0;
        }

        private void OnSelectDisplaySubTab1(object sender, RoutedEventArgs e)
        {
            ViewModel.DisplaySubTabIndex = 1;
        }

        private void OnSelectSoundSubTab0(object sender, RoutedEventArgs e)
        {
            ViewModel.SoundSubTabIndex = 0;
        }

        private void OnSelectSoundSubTab1(object sender, RoutedEventArgs e)
        {
            ViewModel.SoundSubTabIndex = 1;
        }

        private void OnSelectNetworkSubTab0(object sender, RoutedEventArgs e)
        {
            ViewModel.NetworkSubTabIndex = 0;
        }

        private void OnSelectNetworkSubTab1(object sender, RoutedEventArgs e)
        {
            ViewModel.NetworkSubTabIndex = 1;
        }

        private void OnSelectNetworkSubTab2(object sender, RoutedEventArgs e)
        {
            ViewModel.NetworkSubTabIndex = 2;
        }

        private void OnSelectNetworkSubTab3(object sender, RoutedEventArgs e)
        {
            ViewModel.NetworkSubTabIndex = 3;
        }

        private void OnSelectOtherPeripheralsSubTab0(object sender, RoutedEventArgs e)
        {
            ViewModel.OtherPeripheralsSubTabIndex = 0;
        }

        private void OnSelectOtherPeripheralsSubTab1(object sender, RoutedEventArgs e)
        {
            ViewModel.OtherPeripheralsSubTabIndex = 1;
        }

        private void OnSelectOtherPeripheralsSubTab2(object sender, RoutedEventArgs e)
        {
            ViewModel.OtherPeripheralsSubTabIndex = 2;
        }

        private void OnSelectOtherRemovableSubTab0(object sender, RoutedEventArgs e)
        {
            ViewModel.OtherRemovableSubTabIndex = 0;
        }

        private void OnSelectOtherRemovableSubTab1(object sender, RoutedEventArgs e)
        {
            ViewModel.OtherRemovableSubTabIndex = 1;
        }

        private void OnSelectOtherRemovableSubTab2(object sender, RoutedEventArgs e)
        {
            ViewModel.OtherRemovableSubTabIndex = 2;
        }

        // ---------------------------------------------------------------- hard disk model search
        // The engine lists ~620 drive presets (like the original's completer), so the model box is a searchable list.
        /// <summary>
        /// Drive presets matching every word of <paramref name="text"/> anywhere in "[interface] vendor model (size...)":
        /// "Caviar", "1024", "ATA-2" or "quantum fireball" all work. The list is never cut off - it used to stop after
        /// 120 entries, partway through the ATA-1 drives, so most vendors never showed. With nothing typed it is the
        /// engine's own table order, which starts with the [Generic] presets; a search keeps the Generic ones first and
        /// then the names that start with the search before the rest.
        /// </summary>
        private static List<string> FilterModels(string text)
        {
            var words = (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var all = Models.HardwareCatalog.HddModels;
            if (words.Length == 0) return all.ToList();

            static string Bare(string m) => m.StartsWith('[') && m.IndexOf("] ", StringComparison.Ordinal) is int i and > 0 ? m[(i + 2)..] : m;

            return all
                .Where(m => words.All(w => m.Contains(w, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(m => m.StartsWith("[Generic]", StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(m => Bare(m).StartsWith(words[0], StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ToList();
        }

        private void OnHddModelGotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is AutoSuggestBox box)
            {
                box.ItemsSource = FilterModels(string.Empty);
                box.IsSuggestionListOpen = true;
            }
        }

        private void OnHddModelTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                sender.ItemsSource = FilterModels(sender.Text);
        }

        private void OnHddModelSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is string model && ViewModel.SelectedHardDisk is { } disk)
                disk.Model = model;
        }

        private void OnHddModelQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (ViewModel.SelectedHardDisk is not { } disk) return;

            // Enter on a partial search takes the first match; an exact name is kept as it is.
            string? chosen = args.ChosenSuggestion as string
                             ?? Models.HardwareCatalog.HddModels.FirstOrDefault(m => m.Equals(args.QueryText, StringComparison.OrdinalIgnoreCase))
                             ?? FilterModels(args.QueryText).FirstOrDefault();
            if (chosen != null)
            {
                disk.Model = chosen;
                sender.Text = chosen;
            }
            else
            {
                sender.Text = disk.Model; // nothing matched: keep the current model
            }
        }

        /// <summary>The original's "Export…": writes the engine's built-in monitor EDID to a file of your choice.</summary>
        private async void OnExportEdidClicked(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker { SuggestedFileName = "monitor" };
            picker.FileTypeChoices.Add("EDID", new System.Collections.Generic.List<string> { ".bin" });
            WinRT.Interop.InitializeWithWindow.Initialize(picker, _ownerHwnd);

            var file = await picker.PickSaveFileAsync();
            if (file == null) return;

            int rc = Interop.Box86CoreNative.EmuExportDefaultEdid(file.Path);
            if (rc != 0)
            {
                await ShowMessageAsync("Export EDID", $"The EDID could not be written (error {rc}).");
            }
        }

        // ---------------------------------------------------------------- hard disks
        private async void OnAddHardDiskClicked(object sender, SplitButtonClickEventArgs e) => await CreateNewHardDiskAsync();

        private async void OnCreateNewHardDiskClicked(object sender, RoutedEventArgs e) => await CreateNewHardDiskAsync();

        private async System.Threading.Tasks.Task CreateNewHardDiskAsync()
        {
            var panel = new NewHardDiskPanel(ViewModel, _ownerHwnd);
            if (await ShowOverlayAsync(panel, panel.PanelTitle, "Create", "Cancel", panel.TryCreateAsync) && !string.IsNullOrEmpty(panel.CreatedPath))
                ViewModel.AddHardDiskEntry(panel.CreatedPath, panel.Geometry, panel.Bus, panel.Channel);
        }

        /// <summary>Attach an existing image: size and geometry come from the file, the channel is the next free one.</summary>
        private async void OnAttachExistingHardDiskClicked(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            foreach (var ext in new[] { ".vhd", ".img", ".hda", ".chd" }) picker.FileTypeFilter.Add(ext);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, _ownerHwnd);

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            var geometry = Services.HddGeometry.ReadExisting(file.Path, out string? error);
            if (geometry is not { } g)
            {
                await ShowMessageAsync("Unable to attach this image", error ?? "The file could not be read as a hard disk image.");
                return;
            }

            var (bus, channel) = ViewModel.NextFreeBusChannel();
            ViewModel.AddHardDiskEntry(file.Path, g, bus, channel);
        }

        private async void OnConfigureDeviceClicked(object sender, RoutedEventArgs e)
        {
            string deviceName = (sender as FrameworkElement)?.Tag as string ?? "Device";
            var panel = new DeviceConfigPanel(ViewModel, _ownerHwnd, deviceName);
            if (await ShowOverlayAsync(panel, panel.PanelTitle, panel.PrimaryText, panel.CloseText))
                panel.Commit();
        }
    }
}
