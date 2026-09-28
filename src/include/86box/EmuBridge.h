/**
 * 86Box - EmuBridge.h
 *
 * C-ABI interface for 86box_core.dll. Exposes direct in-process engine control,
 * Direct3D 11 DXGI SwapChain presentation, input injection, and telemetry callbacks.
 */

#ifndef EMU_BRIDGE_H
#define EMU_BRIDGE_H

#include <stdint.h>
#include <stddef.h>

#ifdef _WIN32
#  ifdef EMUBRIDGE_EXPORTS
#    define EMUBRIDGE_API __declspec(dllexport)
#  else
#    define EMUBRIDGE_API __declspec(dllimport)
#  endif
#  define EMUBRIDGE_CALL __cdecl
#else
#  define EMUBRIDGE_API __attribute__((visibility("default")))
#  define EMUBRIDGE_CALL
#endif

#ifdef __cplusplus
extern "C" {
#endif

#pragma pack(push, 8)
typedef struct emu_telemetry_t {
    double      speed_percentage;    /* e.g. 100.0 */
    uint32_t    fps;                 /* Emulated frames per second */
    int32_t     fdd_active;          /* Floppy drive activity bitmask */
    int32_t     hdd_active;          /* Hard disk activity bitmask */
    int32_t     cd_active;           /* CD-ROM activity bitmask */
    int32_t     net_active;          /* Network transmit/receive activity */
    int32_t     is_paused;           /* 1 if emulation paused */
    int32_t     is_running;          /* 1 if emulation worker thread running */
    int32_t     mouse_captured;      /* 1 if guest mouse captured */
    const char *status_string;       /* Status string */
} emu_telemetry_t;
#pragma pack(pop)

typedef void (EMUBRIDGE_CALL *emu_telemetry_callback_t)(
    const emu_telemetry_t *telemetry,
    void *user_context
);

typedef void (EMUBRIDGE_CALL *emu_log_callback_t)(
    int level,
    const char *message,
    void *user_context
);

typedef void (EMUBRIDGE_CALL *emu_message_callback_t)(
    int level,
    const char *header,
    const char *message,
    void *user_context
);

/* Direct in-process lifecycle exports */
EMUBRIDGE_API void EMUBRIDGE_CALL emu_set_rom_path(const char *rom_path);
EMUBRIDGE_API int  EMUBRIDGE_CALL emu_check_machine_roms(const char *rom_path, const char *machine_internal_name);
EMUBRIDGE_API int  EMUBRIDGE_CALL emu_initialize(const char *config_path);
EMUBRIDGE_API int  EMUBRIDGE_CALL emu_start(void);
EMUBRIDGE_API int  EMUBRIDGE_CALL emu_start_emulation(const char *config_path, void *target_surface);
EMUBRIDGE_API int  EMUBRIDGE_CALL emu_pause(int pause);
EMUBRIDGE_API int  EMUBRIDGE_CALL emu_reset(int hard_reset);
EMUBRIDGE_API int  EMUBRIDGE_CALL emu_shutdown(void);
EMUBRIDGE_API int  EMUBRIDGE_CALL emu_is_running(void);
EMUBRIDGE_API int  EMUBRIDGE_CALL emu_is_paused(void);

/* Direct3D 11 DXGI SwapChain presentation */
EMUBRIDGE_API int  EMUBRIDGE_CALL emu_set_dxgi_swapchain(void *swapchain);
EMUBRIDGE_API void EMUBRIDGE_CALL emu_resize_viewport(int width, int height);
EMUBRIDGE_API void EMUBRIDGE_CALL emu_render_frame(void);

/* Input injection */
EMUBRIDGE_API void EMUBRIDGE_CALL emu_send_input_keyboard(uint32_t scancode, int is_down);
EMUBRIDGE_API void EMUBRIDGE_CALL emu_send_input_mouse(int dx, int dy, uint32_t buttons);
EMUBRIDGE_API void EMUBRIDGE_CALL emu_set_mouse_capture(int capture);
EMUBRIDGE_API int  EMUBRIDGE_CALL emu_get_mouse_capture(void);

/* Telemetry & diagnostics */
EMUBRIDGE_API void EMUBRIDGE_CALL emu_set_telemetry_callback(
    emu_telemetry_callback_t callback,
    void *user_context
);

EMUBRIDGE_API void EMUBRIDGE_CALL emu_set_log_callback(
    emu_log_callback_t callback,
    void *user_context
);

EMUBRIDGE_API void EMUBRIDGE_CALL emu_set_message_callback(
    emu_message_callback_t callback,
    void *user_context
);

#ifdef __cplusplus
}
#endif

#endif /* EMU_BRIDGE_H */
