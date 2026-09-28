using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.StartScreen;
using Box86_WinUI.Models;

namespace Box86_WinUI.Services
{
    public static class JumpListService
    {
        public static async Task UpdateJumpListAsync(IEnumerable<VirtualMachine> vms)
        {
            try
            {
                if (!JumpList.IsSupported()) return;

                var jumpList = await JumpList.LoadCurrentAsync();
                jumpList.SystemGroupKind = JumpListSystemGroupKind.None;
                jumpList.Items.Clear();

                int count = 0;
                foreach (var vm in vms)
                {
                    if (count++ >= 6) break; // Keep top 6 in jump list
                    var item = JumpListItem.CreateWithArguments($"--start-vm \"{vm.Id}\"", vm.Name);
                    item.Description = $"{vm.Cpu} • {vm.MemoryMb} MB RAM • {vm.VideoCard}";
                    item.GroupName = "Recent Virtual Machines";
                    jumpList.Items.Add(item);
                }

                await jumpList.SaveAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[JumpList] Update failed: {ex.Message}");
            }
        }
    }
}
