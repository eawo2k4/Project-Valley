using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Box86_WinUI.Models;

namespace Box86_WinUI.Services
{
    /// <summary>
    /// In-process supervisor for virtual machine lifecycle.
    /// Operates without spawning external 86Box.exe processes or opening secondary desktop windows.
    /// </summary>
    public class VMProcessSupervisor
    {
        private readonly ConcurrentDictionary<string, VirtualMachine> _runningVMs = new();
        private readonly ConcurrentDictionary<string, EmuEngineService> _activeEngines = new();

        public void RegisterEngine(VirtualMachine vm, EmuEngineService engine)
        {
            _runningVMs[vm.Id] = vm;
            _activeEngines[vm.Id] = engine;
            vm.Status = VMStatus.Running;
            vm.LastRun = DateTime.Now;
        }

        public void UnregisterEngine(VirtualMachine vm)
        {
            _runningVMs.TryRemove(vm.Id, out _);
            _activeEngines.TryRemove(vm.Id, out _);
            vm.Status = VMStatus.Stopped;
        }

        public Task<bool> StartVMAsync(VirtualMachine vm, bool settingsOnly = false)
        {
            if (vm.IsRunning) return Task.FromResult(false);
            vm.Status = VMStatus.Starting;
            _runningVMs[vm.Id] = vm;
            return Task.FromResult(true);
        }

        public Task TogglePauseAsync(VirtualMachine vm)
        {
            if (_activeEngines.TryGetValue(vm.Id, out var engine))
            {
                engine.TogglePause();
                vm.Status = engine.IsPaused ? VMStatus.Paused : VMStatus.Running;
            }
            return Task.CompletedTask;
        }

        public Task HardResetAsync(VirtualMachine vm)
        {
            if (_activeEngines.TryGetValue(vm.Id, out var engine))
            {
                engine.Reset(hardReset: true);
            }
            return Task.CompletedTask;
        }

        public Task SendCtrlAltDelAsync(VirtualMachine vm)
        {
            if (_activeEngines.TryGetValue(vm.Id, out var engine))
            {
                engine.Reset(hardReset: false);
            }
            return Task.CompletedTask;
        }

        public Task ShowSettingsAsync(VirtualMachine vm)
        {
            // Settings are displayed directly in-process via MachineSettingsDialog
            return Task.CompletedTask;
        }

        public async Task StopVMAsync(VirtualMachine vm, bool force = false)
        {
            if (_activeEngines.TryGetValue(vm.Id, out var engine))
            {
                vm.Status = VMStatus.Stopping;
                await engine.ShutdownAsync();
                UnregisterEngine(vm);
            }
            else
            {
                vm.Status = VMStatus.Stopped;
                _runningVMs.TryRemove(vm.Id, out _);
            }
        }
    }
}
