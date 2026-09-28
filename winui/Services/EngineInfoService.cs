using System;
using System.Text;
using Box86_WinUI.Interop;

namespace Box86_WinUI.Services
{
    /// <summary>
    /// Real, compile-time facts about the currently loaded 86box_core.dll - not the user's update
    /// channel/recompiler PREFERENCE in CoreUpdateService (that's only what a future download would
    /// target), but what this specific build actually is. Backed by native exports reading the same
    /// EMU_VERSION_FULL/EMU_BUILD_NUM/RELEASE_BUILD/USE_NEW_DYNAREC macros the engine's own CMake
    /// build bakes in - NEW_DYNAREC and BUILD_TYPE are compile-time-only, never runtime-switchable.
    /// </summary>
    public static class EngineInfoService
    {
        private static string? _versionString;
        private static int? _buildNumber;
        private static bool? _isReleaseBuild;
        private static bool? _isNewDynarec;

        public static string VersionString => _versionString ??= ReadVersionString();
        public static int BuildNumber => _buildNumber ??= SafeCall(Box86CoreNative.EmuGetBuildNumber, 0);
        public static bool IsReleaseBuild => _isReleaseBuild ??= SafeCall(Box86CoreNative.EmuIsReleaseBuild, 0) != 0;
        public static bool IsNewDynarec => _isNewDynarec ??= SafeCall(Box86CoreNative.EmuIsNewDynarec, 0) != 0;

        /// <summary>"New Dynamic Recompiler (NDR)" or "Old Dynamic Recompiler (ODR)", matching the engine's own terms.</summary>
        public static string RecompilerLabel => IsNewDynarec ? "New Dynamic Recompiler (NDR)" : "Old Dynamic Recompiler (ODR)";

        /// <summary>"Stable" or "Experimental" - a real BUILD_TYPE=release fact, not a guess.</summary>
        public static string ChannelLabel => IsReleaseBuild ? "Stable" : "Experimental";

        private static string ReadVersionString()
        {
            try
            {
                var buf = new byte[256];
                int n = Box86CoreNative.EmuGetVersionString(buf, buf.Length);
                return n > 0 ? Encoding.UTF8.GetString(buf, 0, n) : "Unknown";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EngineInfoService] {ex.Message}");
                return "Unknown";
            }
        }

        private static T SafeCall<T>(Func<T> call, T fallback)
        {
            try { return call(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EngineInfoService] {ex.Message}");
                return fallback;
            }
        }
    }
}
