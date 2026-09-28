using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Box86_WinUI.Models;
using Box86_WinUI.Services;

namespace Box86_WinUI.ViewModels
{
    public partial class NewVMWizardViewModel : ObservableObject
    {
        private readonly ConfigService _configService = new();

        [ObservableProperty]
        public partial int CurrentStep { get; set; } = 1;

        public bool IsStep1 => CurrentStep == 1;
        public bool IsStep2 => CurrentStep == 2;
        public bool IsStep3 => CurrentStep == 3;
        public bool CanGoBack => CurrentStep > 1;
        public bool IsFinishStep => CurrentStep == 3;
        public bool CanGoNext => CurrentStep < 3 && (CurrentStep != 2 || !string.IsNullOrWhiteSpace(SystemName));

        // Step 1: Mode
        [ObservableProperty]
        public partial bool IsNewConfiguration { get; set; } = true;

        [ObservableProperty]
        public partial bool IsExistingConfiguration { get; set; } = false;

        [ObservableProperty]
        public partial string ExistingConfigPath { get; set; } = string.Empty;

        // Step 2: System name & Location
        [ObservableProperty]
        public partial string SystemName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string DisplayName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string DestinationPath { get; set; } = string.Empty;

        [ObservableProperty]
        public partial MachinePreset SelectedPreset { get; set; } = HardwareCatalog.Presets[0];

        [ObservableProperty]
        public partial ObservableCollection<MachinePreset> Presets { get; set; } = new(HardwareCatalog.Presets);

        private bool _systemNameTouched = false;
        public bool HasSystemNameError => _systemNameTouched && string.IsNullOrWhiteSpace(SystemName);
        public bool HasDisplayName => !string.IsNullOrWhiteSpace(DisplayName);

        public NewVMWizardViewModel()
        {
            DestinationPath = _configService.GetDefaultVmDirectory();
        }

        partial void OnCurrentStepChanged(int value)
        {
            OnPropertyChanged(nameof(IsStep1));
            OnPropertyChanged(nameof(IsStep2));
            OnPropertyChanged(nameof(IsStep3));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(IsFinishStep));
        }

        partial void OnSystemNameChanged(string value)
        {
            _systemNameTouched = true;
            OnPropertyChanged(nameof(HasSystemNameError));
            OnPropertyChanged(nameof(CanGoNext));
        }

        partial void OnDisplayNameChanged(string value)
        {
            OnPropertyChanged(nameof(HasDisplayName));
        }

        [RelayCommand]
        public void NextStep()
        {
            if (CurrentStep < 3)
            {
                if (CurrentStep == 2 && string.IsNullOrWhiteSpace(SystemName))
                    return;

                CurrentStep++;
            }
        }

        [RelayCommand]
        public void PreviousStep()
        {
            if (CurrentStep > 1)
            {
                CurrentStep--;
            }
        }

        public void TriggerSystemNameValidation()
        {
            _systemNameTouched = true;
            OnPropertyChanged(nameof(HasSystemNameError));
        }

        [ObservableProperty]
        public partial string CreationError { get; set; } = string.Empty;

        public bool HasCreationError => !string.IsNullOrEmpty(CreationError);

        partial void OnCreationErrorChanged(string value) => OnPropertyChanged(nameof(HasCreationError));

        public VirtualMachine? FinalizeCreation()
        {
            CreationError = string.Empty;
            try
            {
                var finalName = string.IsNullOrWhiteSpace(SystemName) ? "New 86Box VM" : SystemName.Trim();

                if (IsExistingConfiguration && !string.IsNullOrWhiteSpace(ExistingConfigPath))
                {
                    string path = ExistingConfigPath.Trim();
                    string dir = File.Exists(path) ? Path.GetDirectoryName(path)! : path;
                    if (!Directory.Exists(dir) || !File.Exists(Path.Combine(dir, "86box.cfg")))
                    {
                        CreationError = "Could not find 86box.cfg at the specified location.";
                        return null;
                    }

                    var loadedVm = _configService.LoadVirtualMachine(dir);
                    if (loadedVm != null)
                    {
                        if (!string.IsNullOrWhiteSpace(SystemName))
                            loadedVm.Name = SystemName.Trim();
                        return loadedVm;
                    }

                    CreationError = "Failed to load the specified 86box.cfg file.";
                    return null;
                }

                var basePreset = SelectedPreset with
                {
                    Name = finalName,
                    HardDiskMb = 0
                };

                var vm = _configService.TryCreateVirtualMachine(finalName, basePreset, out var result, DestinationPath);
                if (vm == null)
                {
                    CreationError = string.Join(Environment.NewLine, result.Issues.Where(i => i.IsError).Select(i => i.Message));
                    return null;
                }
                if (vm != null && !string.IsNullOrWhiteSpace(DisplayName))
                {
                    vm.Description = DisplayName.Trim();
                }
                return vm;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NewVMWizard] Failed: {ex.Message}");
                CreationError = ex.Message;
                return null;
            }
        }
    }
}
