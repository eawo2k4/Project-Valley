using System;

namespace Box86_WinUI.Models
{
    /// <summary>
    /// Keys for device options held by the settings view model: an engine device stores its options in the section
    /// named after the device ("[Sound Blaster AWE32]", "[FluidSynth]", "[3Dfx Voodoo Graphics]") in 86box.cfg.
    /// The option lists themselves come from <see cref="DeviceConfigCatalog"/>, generated from the engine sources.
    /// </summary>
    public static class DeviceSettingKey
    {
        private const char Separator = '\t';

        public static string Make(string section, string key) => section + Separator + key;

        public static (string Section, string Key) Split(string id)
        {
            int i = id.LastIndexOf(Separator);
            return i < 0 ? (string.Empty, id) : (id.Substring(0, i), id.Substring(i + 1));
        }
    }
}
