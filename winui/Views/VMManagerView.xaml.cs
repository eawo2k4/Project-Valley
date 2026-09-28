using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Box86_WinUI.Models;
using Box86_WinUI.ViewModels;
using Box86_WinUI.Services;

namespace Box86_WinUI.Views
{
    public sealed partial class VMManagerView : UserControl
    {
        public VMManagerViewModel ViewModel { get; } = new();

        public VMManagerView()
        {
            this.InitializeComponent();

            // The machine settings are one native ContentDialog shown over the manager window - the
            // main window's own XamlRoot, not this UserControl's, so it is anchored and smoke-dims
            // against the whole window regardless of what hosts this control.
            ViewModel.SettingsDialogHandler = async vm =>
            {
                var xamlRoot = App.MainWindowInstance?.Content?.XamlRoot ?? this.XamlRoot;
                var dialog = new MachineSettingsDialog(vm, App.MainWindowHandle, xamlRoot, this.ActualHeight + 200);
                await dialog.ShowAsync();
            };

            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            this.Loaded += (s, e) =>
            {
                if (ViewModel.SelectedVM != null)
                {
                    TriggerInspectorEntranceAnimation();
                }
            };
        }

        private async void OnNewVMClicked(object sender, RoutedEventArgs e)
        {
            // The main window's own XamlRoot, not this UserControl's: ContentDialog centers and
            // smoke-dims against whichever XamlRoot it is given, and the main window's is the one
            // native centering should be measured against, regardless of what hosts this control.
            var wizardDialog = new NewVMWizardDialog
            {
                XamlRoot = App.MainWindowInstance?.Content?.XamlRoot ?? this.XamlRoot
            };

            await wizardDialog.ShowAsync();
            if (wizardDialog.CreatedVM != null)
            {
                ViewModel.AddCreatedVirtualMachine(wizardDialog.CreatedVM);
                ViewModel.SelectedVM = wizardDialog.CreatedVM;

                // Automatically launch the configuration settings window for the new system
                await ViewModel.ShowSettingsAsync(wizardDialog.CreatedVM);
            }
        }

        // AutoSuggestBox binds Text TwoWay to SearchText already; these just keep the standard
        // AutoSuggestBox pattern (Enter / suggestion pick) working the same way.
        private void OnVmSearchBoxTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                ViewModel.SearchText = sender.Text;
        }

        private void OnVmSearchBoxQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            ViewModel.SearchText = args.QueryText;
        }

        private void OnSortByNameClicked(object sender, RoutedEventArgs e) => ViewModel.SortMode = "Name";
        private void OnSortByStatusClicked(object sender, RoutedEventArgs e) => ViewModel.SortMode = "Status";
        private void OnSortByRecentClicked(object sender, RoutedEventArgs e) => ViewModel.SortMode = "Recently used";

        private VirtualMachine? GetVmFromSender(object sender)
        {
            return (sender as FrameworkElement)?.DataContext as VirtualMachine ?? ViewModel.SelectedVM;
        }

        private async void OnListViewDoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (ViewModel.SelectedVM is { } vm)
            {
                await ViewModel.StartVMAsync(vm);
            }
        }

        private async void OnItemStartClicked(object sender, RoutedEventArgs e)
        {
            if (GetVmFromSender(sender) is { } vm)
            {
                await ViewModel.StartVMAsync(vm);
            }
        }

        private async void OnItemStopClicked(object sender, RoutedEventArgs e)
        {
            if (GetVmFromSender(sender) is { } vm)
            {
                await ViewModel.StopVMAsync(vm);
            }
        }

        private async void OnItemPauseClicked(object sender, RoutedEventArgs e)
        {
            if (GetVmFromSender(sender) is { } vm)
            {
                await ViewModel.TogglePauseAsync(vm);
            }
        }

        private async void OnItemResetClicked(object sender, RoutedEventArgs e)
        {
            if (GetVmFromSender(sender) is { } vm)
            {
                await ViewModel.HardResetAsync(vm);
            }
        }

        private async void OnItemCadClicked(object sender, RoutedEventArgs e)
        {
            if (GetVmFromSender(sender) is { } vm)
            {
                await ViewModel.SendCadAsync(vm);
            }
        }

        private async void OnItemSettingsClicked(object sender, RoutedEventArgs e)
        {
            if (GetVmFromSender(sender) is { } vm)
            {
                await ViewModel.ShowSettingsAsync(vm);
            }
        }

        private void OnItemOpenFolderClicked(object sender, RoutedEventArgs e)
        {
            if (GetVmFromSender(sender) is { } vm)
            {
                ViewModel.OpenFolder(vm);
            }
        }

        private async void OnItemOpenConfigFileClicked(object sender, RoutedEventArgs e)
        {
            if (GetVmFromSender(sender) is { } vm)
            {
                await ViewModel.OpenConfigFileAsync(vm);
            }
        }

        private async void OnItemExportConfigClicked(object sender, RoutedEventArgs e)
        {
            if (GetVmFromSender(sender) is { } vm)
            {
                await ViewModel.ExportConfigFileAsync(vm);
            }
        }

        /// <summary>Shows the machine's 86box.cfg in a read-only text box with Copy and Show in Explorer buttons.</summary>
        private async void OnItemShowConfigClicked(object sender, RoutedEventArgs e)
        {
            if (GetVmFromSender(sender) is not { } vm) return;

            string cfg = vm.ConfigFilePath;
            if (!File.Exists(cfg))
            {
                ViewModel.StatusMessage = $"{vm.Name} has no 86box.cfg yet ({cfg}).";
                return;
            }

            string text;
            try
            {
                // FileShare.ReadWrite (+ Delete) so an editor or the running engine holding the file cannot block or cut the read.
                using var stream = new FileStream(cfg, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
                text = await reader.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Could not read the config file: {ex.Message}";
                return;
            }

            var box = new TextBox
            {
                Text = text,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                IsSpellCheckEnabled = false,
                IsTextPredictionEnabled = false,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12,
                MinWidth = 560,
                Height = 380
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(box, ScrollBarVisibility.Auto);
            ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Auto);

            var status = new TextBlock { Opacity = 0.7, FontSize = 12, Text = cfg, TextTrimming = TextTrimming.CharacterEllipsis };

            var dialog = new ContentDialog
            {
                Title = $"{vm.Name} - 86box.cfg",
                Content = new StackPanel { Spacing = 8, Children = { status, box } },
                PrimaryButtonText = "Copy",
                SecondaryButtonText = "Show in Explorer",
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = (sender as FrameworkElement)?.XamlRoot ?? this.XamlRoot
            };

            // Copy and Show in Explorer keep the dialog open.
            dialog.PrimaryButtonClick += (d, args) =>
            {
                args.Cancel = true;
                try
                {
                    var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    package.SetText(box.Text);
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                    Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
                    status.Text = "Copied to the clipboard";
                }
                catch (Exception ex) { status.Text = $"Copy failed: {ex.Message}"; }
            };
            dialog.SecondaryButtonClick += (d, args) =>
            {
                args.Cancel = true;
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{cfg}\"",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex) { status.Text = $"Could not open Explorer: {ex.Message}"; }
            };

            await dialog.ShowAsync();
        }

        /// <summary>Copies 86box.cfg to the clipboard as a file (paste into Explorer) and as text (paste into an editor).</summary>
        private async void OnItemCopyConfigClicked(object sender, RoutedEventArgs e)
        {
            if (GetVmFromSender(sender) is not { } vm) return;

            string cfg = vm.ConfigFilePath;
            if (!File.Exists(cfg))
            {
                ViewModel.StatusMessage = $"{vm.Name} has no 86box.cfg yet ({cfg}).";
                return;
            }

            try
            {
                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(cfg);
                var package = new Windows.ApplicationModel.DataTransfer.DataPackage { RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy };
                package.SetStorageItems(new Windows.Storage.IStorageItem[] { file });
                package.SetText(await File.ReadAllTextAsync(cfg));
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
                ViewModel.StatusMessage = $"Copied {vm.Name}'s 86box.cfg to the clipboard";
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = $"Could not copy the config file: {ex.Message}";
            }
        }

        private async void OnItemDeleteClicked(object sender, RoutedEventArgs e)
        {
            if (GetVmFromSender(sender) is not { } vm) return;

            var dialog = new ContentDialog
            {
                Title = $"Delete \"{vm.Name}\"?",
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { TextWrapping = TextWrapping.Wrap, Text = "This permanently deletes the machine's entire folder, including its 86box.cfg, hard disk images, NVRAM and screenshots. This cannot be undone." },
                        new TextBlock { TextWrapping = TextWrapping.Wrap, Text = vm.Path, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), Opacity = 0.75 }
                    }
                },
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = (sender as FrameworkElement)?.XamlRoot ?? this.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                await ViewModel.DeleteVMAsync(vm);
        }

        private void OnExitClicked(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
        }

        private void OnDocumentationClicked(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://86box.readthedocs.io",
                UseShellExecute = true
            });
        }

        private async void OnAbout86BoxClicked(object sender, RoutedEventArgs e)
        {
            var dialog = new AboutDialog
            {
                XamlRoot = App.MainWindowInstance?.Content?.XamlRoot ?? this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async void OnCheckUpdatesClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                await CoreUpdateService.Instance.CheckForUpdatesAsync(manualCheck: true);
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "Check for Updates",
                    Content = $"Failed to check for updates: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }

        private async void OnOpenManagerSettingsClicked(object sender, RoutedEventArgs e)
        {
            var dialog = new ManagerSettingsDialog(ViewModel)
            {
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VMManagerViewModel.SelectedVM))
            {
                DispatcherQueue.TryEnqueue(TriggerInspectorEntranceAnimation);
            }
        }

        private void TriggerInspectorEntranceAnimation()
        {
            if (InspectorContainer == null || InspectorScrollViewer == null)
                return;

            if (ViewModel.SelectedVM == null)
                return;

            // Reset scroll position to top for the newly selected machine
            InspectorScrollViewer.ChangeView(null, 0, null, true);

            // Re-trigger EntranceThemeTransition by re-adding the child to the transition-enabled Grid
            if (InspectorContainer.Children.Contains(InspectorScrollViewer))
            {
                InspectorContainer.Children.Remove(InspectorScrollViewer);
                InspectorContainer.Children.Add(InspectorScrollViewer);
            }
        }

        private void OnCardPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement card && card.FindName("QuickActionsPanel") is UIElement actionsPanel)
            {
                AnimateOpacity(actionsPanel, 1.0, 100);
            }
        }

        private void OnCardPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement card && card.FindName("QuickActionsPanel") is UIElement actionsPanel)
            {
                AnimateOpacity(actionsPanel, 0.3, 100);
            }
        }

        private static void AnimateOpacity(UIElement target, double toOpacity, double durationMs)
        {
            var animation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = toOpacity,
                Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseInOut }
            };

            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animation, target);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animation, "Opacity");
            storyboard.Children.Add(animation);
            storyboard.Begin();
        }
    }
}
