using System;
using System.Runtime.InteropServices;

namespace Box86_WinUI.Interop
{
    /// <summary>
    /// Blittable telemetry data struct passed across the C-ABI boundary from 86box_core.dll.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct EmuTelemetryData
    {
        public double SpeedPercentage;
        public uint Fps;
        public int FddActive;
        public int HddActive;
        public int CdActive;
        public int NetActive;
        public int IsPaused;
        public int IsRunning;
        public int MouseCaptured;
        public IntPtr StatusStringPtr;

        public string StatusString => Marshal.PtrToStringUTF8(StatusStringPtr) ?? string.Empty;
    }

    /// <summary>
    /// Telemetry update delegate matching the native C function pointer.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void EmuTelemetryNativeCallback(
        in EmuTelemetryData telemetry,
        IntPtr userContext
    );

    /// <summary>
    /// Diagnostics/logging callback delegate.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void EmuLogNativeCallback(
        int level,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string message,
        IntPtr userContext
    );

    /// <summary>
    /// Message/warning callback delegate for engine notifications.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void EmuMessageNativeCallback(
        int level,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string header,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string message,
        IntPtr userContext
    );

    /// <summary>
    /// Static P/Invoke bridge class directly calling the exported C-ABI functions in 86box_core.dll.
    /// No external process spawning or SetParent window capturing is performed.
    /// </summary>
    public static class Box86CoreNative
    {
        public const string DllName = "86box_core.dll";

        [DllImport(DllName, EntryPoint = "emu_set_rom_path", CallingConvention = CallingConvention.Cdecl)]
        public static extern void EmuSetRomPath([MarshalAs(UnmanagedType.LPUTF8Str)] string romPath);

        [DllImport(DllName, EntryPoint = "emu_cpu_step_eligible", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuCpuStepEligible(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string machineInternalName,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string familyInternalName,
            long rspeedHz, double multi);

        [DllImport(DllName, EntryPoint = "emu_check_video_card", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuCheckVideoCard(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string romPath,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string cardInternalName);

        [DllImport(DllName, EntryPoint = "emu_check_machine_roms", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuCheckMachineRoms(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string romPath,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string machineInternalName);

        [DllImport(DllName, EntryPoint = "emu_initialize", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int EmuInitialize([MarshalAs(UnmanagedType.LPUTF8Str)] string configPath);

        [DllImport(DllName, EntryPoint = "emu_start", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuStart();

        [DllImport(DllName, EntryPoint = "emu_start_emulation", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuStartEmulation([MarshalAs(UnmanagedType.LPUTF8Str)] string configPath, IntPtr targetSurface);

        [DllImport(DllName, EntryPoint = "emu_pause", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuPause(int pause);

        [DllImport(DllName, EntryPoint = "emu_reset", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuReset(int hardReset);

        [DllImport(DllName, EntryPoint = "emu_acpi_power_button", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuAcpiPowerButton();

        [DllImport(DllName, EntryPoint = "emu_take_screenshot", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuTakeScreenshot();

        [DllImport(DllName, EntryPoint = "emu_set_aspect_mode", CallingConvention = CallingConvention.Cdecl)]
        public static extern void EmuSetAspectMode(int mode);

        [DllImport(DllName, EntryPoint = "emu_floppy_mount", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuFloppyMount(int id, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int writeProtect);

        [DllImport(DllName, EntryPoint = "emu_floppy_eject", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuFloppyEject(int id);

        [DllImport(DllName, EntryPoint = "emu_cdrom_mount", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuCdromMount(int id, [MarshalAs(UnmanagedType.LPUTF8Str)] string path);

        [DllImport(DllName, EntryPoint = "emu_cdrom_eject", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuCdromEject(int id);

        [DllImport(DllName, EntryPoint = "emu_set_fast_forward", CallingConvention = CallingConvention.Cdecl)]
        public static extern void EmuSetFastForward(int enable);

        [DllImport(DllName, EntryPoint = "emu_network_connect", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuNetworkConnect(int id, int connect);

        [DllImport(DllName, EntryPoint = "emu_network_is_connected", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuNetworkIsConnected(int id);

        [DllImport(DllName, EntryPoint = "emu_set_display_options", CallingConvention = CallingConvention.Cdecl)]
        public static extern void EmuSetDisplayOptions(int integerScaling, int crtScanlines);

        /// <summary>0 = None (Sharp), 1 = Scanlines (Flat), 2 = Authentic CRT (Curved Glass).</summary>
        [DllImport(DllName, EntryPoint = "emu_set_crt_filter_mode", CallingConvention = CallingConvention.Cdecl)]
        public static extern void EmuSetCrtFilterMode(int mode);

        /// <summary>0.0 .. 0.15 (0% - 15% screen curvature); only visible in Authentic CRT mode.</summary>
        [DllImport(DllName, EntryPoint = "emu_set_crt_curvature", CallingConvention = CallingConvention.Cdecl)]
        public static extern void EmuSetCrtCurvature(float amount);

        /// <summary>0.0 .. 1.0 phosphor bloom intensity; only visible in Authentic CRT mode.</summary>
        [DllImport(DllName, EntryPoint = "emu_set_crt_bloom", CallingConvention = CallingConvention.Cdecl)]
        public static extern void EmuSetCrtBloom(float amount);

        [DllImport(DllName, EntryPoint = "emu_trigger_nmi", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuTriggerNmi();

        [DllImport(DllName, EntryPoint = "emu_set_sound", CallingConvention = CallingConvention.Cdecl)]
        public static extern void EmuSetSound(int gainDb, int muted);

        [DllImport(DllName, EntryPoint = "emu_copy_frame", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuCopyFrame(uint[]? dst, int capacityPixels, out int width, out int height);

        [DllImport(DllName, EntryPoint = "emu_machine_supports_acpi", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuMachineSupportsAcpi([MarshalAs(UnmanagedType.LPUTF8Str)] string machineInternalName);

        [DllImport(DllName, EntryPoint = "emu_vhd_create_dynamic", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuVhdCreateDynamic([MarshalAs(UnmanagedType.LPUTF8Str)] string path,
            uint cyl, uint heads, uint spt, out uint outCyl, out uint outHeads, out uint outSpt);

        [DllImport(DllName, EntryPoint = "emu_vhd_read_geometry", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuVhdReadGeometry([MarshalAs(UnmanagedType.LPUTF8Str)] string path,
            out uint outCyl, out uint outHeads, out uint outSpt);

        [DllImport(DllName, EntryPoint = "emu_list_devices", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuListDevices(int kind,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string machineInternalName, int slot,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? primaryVideo, byte[] output, int capacity);

        [DllImport(DllName, EntryPoint = "emu_list_network_host_devices", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuListNetworkHostDevices(byte[] output, int capacity);

        [DllImport(DllName, EntryPoint = "emu_machine_has_mca", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuMachineHasMca([MarshalAs(UnmanagedType.LPUTF8Str)] string machineInternalName);

        /// <summary>MIDI output (input = 0) or input (input = 1) devices this engine build offers: "internal\tlabel\n" lines.</summary>
        [DllImport(DllName, EntryPoint = "emu_list_midi_devices", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuListMidiDevices(int input, byte[] output, int capacity);

        [DllImport(DllName, EntryPoint = "emu_list_input_devices", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuListInputDevices(int kind,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string machineInternalName, byte[] output, int capacity);

        [DllImport(DllName, EntryPoint = "emu_export_default_edid", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuExportDefaultEdid([MarshalAs(UnmanagedType.LPUTF8Str)] string path);

        [DllImport(DllName, EntryPoint = "emu_get_version_string", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuGetVersionString(byte[] buf, int len);

        [DllImport(DllName, EntryPoint = "emu_get_build_number", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuGetBuildNumber();

        [DllImport(DllName, EntryPoint = "emu_is_release_build", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuIsReleaseBuild();

        [DllImport(DllName, EntryPoint = "emu_is_new_dynarec", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuIsNewDynarec();

        [DllImport(DllName, EntryPoint = "emu_shutdown", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuShutdown();

        [DllImport(DllName, EntryPoint = "emu_is_running", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuIsRunning();

        [DllImport(DllName, EntryPoint = "emu_is_paused", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuIsPaused();

        [DllImport(DllName, EntryPoint = "emu_set_dxgi_swapchain", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuSetDxgiSwapChain(IntPtr swapChain);

        [DllImport(DllName, EntryPoint = "emu_resize_viewport", CallingConvention = CallingConvention.Cdecl)]
        public static extern void EmuResizeViewport(int width, int height);

        [DllImport(DllName, EntryPoint = "emu_render_frame", CallingConvention = CallingConvention.Cdecl)]
        public static extern void EmuRenderFrame();

        [DllImport(DllName, EntryPoint = "emu_send_input_keyboard", CallingConvention = CallingConvention.Cdecl)]
        public static extern void EmuSendInputKeyboard(uint scancode, int isDown);

        [DllImport(DllName, EntryPoint = "emu_send_input_mouse", CallingConvention = CallingConvention.Cdecl)]
        public static extern void EmuSendInputMouse(int dx, int dy, uint buttons);

        [DllImport(DllName, EntryPoint = "emu_set_mouse_capture", CallingConvention = CallingConvention.Cdecl)]
        public static extern void EmuSetMouseCapture(int capture);

        [DllImport(DllName, EntryPoint = "emu_get_mouse_capture", CallingConvention = CallingConvention.Cdecl)]
        public static extern int EmuGetMouseCapture();

        [DllImport(DllName, EntryPoint = "emu_set_telemetry_callback", CallingConvention = CallingConvention.Cdecl)]
        public static extern void EmuSetTelemetryCallback(
            EmuTelemetryNativeCallback callback,
            IntPtr userContext
        );

        [DllImport(DllName, EntryPoint = "emu_set_log_callback", CallingConvention = CallingConvention.Cdecl)]
        public static extern void EmuSetLogCallback(
            EmuLogNativeCallback callback,
            IntPtr userContext
        );

        [DllImport(DllName, EntryPoint = "emu_set_message_callback", CallingConvention = CallingConvention.Cdecl)]
        public static extern void EmuSetMessageCallback(
            EmuMessageNativeCallback callback,
            IntPtr userContext
        );
    }
}
