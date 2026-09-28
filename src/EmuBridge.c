/**
 * 86Box - EmuBridge.c
 *
 * Direct C-ABI in-process engine bridge for 86box_core.dll.
 * Implements Direct3D 11 swap chain presentation, background CPU loop pacing,
 * telemetry dispatching, and all platform hooks without Qt dependencies.
 */

#define EMUBRIDGE_EXPORTS
#include <86box/EmuBridge.h>

#define WIN32_LEAN_AND_MEAN
#define COBJMACROS
#include <windows.h>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <timeapi.h>
#include <direct.h>
#include <stdbool.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

/* 86Box Core Headers */
#include <86box/86box.h>
#include <86box/version.h>
#include <86box/plat.h>
#include <86box/ui.h>
#include <86box/timer.h>
#include <86box/device.h>
#include <86box/video.h>
#include <86box/keyboard.h>
#include <86box/mouse.h>
#include <86box/sound.h>
#include <86box/fdd.h>
#include <86box/hdc.h>
#include <86box/scsi.h>
#include <86box/scsi_device.h>
#include <86box/cartridge.h>
#include <86box/cassette.h>
#include <86box/cdrom.h>
#include <86box/rdisk.h>
#include <86box/mo.h>
#include <86box/scsi_tape.h>
#include "cpu.h"
#include <86box/hdd.h>
#include <86box/thread.h>
#include <86box/network.h>
#include <86box/mem.h>
#include <86box/machine.h>
#include <86box/rom.h>
#include <86box/machine_status.h>
#include <86box/path.h>
#include <86box/nvr.h>
#include <86box/config.h>
#include <86box/gameport.h>
#include <86box/fdc_ext.h>
#include <86box/qt_glsl.h>
#include "disk/minivhd/minivhd.h"
#include <86box/cdrom_interface.h>
#include <86box/vid_ddc.h>
#include <86box/midi.h>

/*
 * dopause, hard_reset_pending, force_10ms, framecountx and nvr_dosave are
 * already declared (with the correct type) by the headers included above,
 * and buffer32 is a macro (see video.h), not a variable - none of these
 * are re-declared here to avoid conflicting declarations.
 */
extern FILE *stdlog;
extern int  start_vmm;
extern char log_path[1024];
extern int  framecount;
extern int  fps;
extern int  title_update;

/* This worker thread's own liveness flag; not shared with the rest of the
 * core, so it is not declared extern. */
static volatile int s_cpu_thread_running = 0;
/* Set on the CPU emulation thread only; lets fatal() (86box.c) end just that thread instead of the process. */
static __thread int s_is_emu_thread = 0;

/*
 * These globals are declared extern by the core headers but were only ever
 * defined by the classic Qt/SDL frontend (src/qt, src/unix), which the
 * WinUI frontend does not build. They are defined here instead so the core
 * links; the UI-facing ones (toolbar/status-bar visibility, fixed window
 * size, key remap, shader paths) are not yet exposed anywhere in the WinUI
 * UI and simply hold their config-file-persisted defaults.
 */
int             mouse_capture   = 0;
volatile int    cpu_thread_run  = 0;
bool            fast_forward    = false;
int             fixed_size_x    = 640;
int             fixed_size_y    = 480;
int             rctrl_is_lalt   = 0;
int             update_icons    = 0;
int             kbd_req_capture = 0;
int             hide_status_bar = 0;
int             hide_tool_bar   = 0;
char            gl3_shader_file[MAX_USER_SHADERS][512];
char            vk_shader_file[20][512];

/* No joystick/gamepad input support in the WinUI frontend yet. */
plat_joystick_state_t plat_joystick_state[MAX_PLAT_JOYSTICKS];
joystick_state_t       joystick_state[GAMEPORT_MAX][MAX_JOYSTICKS];
int                     joysticks_present = 0;

void joystick_init(void) {}
void joystick_close(void) {}
void joystick_process(uint8_t gp) { (void)gp; }

/* Core functions declared in 86box.c */
extern void ack_pause(void);
extern void pc_reset_hard_init(void);
extern void pc_reset_hard_close(void);
extern void pc_run(void);
extern void pc_close(void *ptr);
extern void pc_onesec(void);
extern void pc_send_cad(void);

/* ========================================================================= */
/* Internal Bridge State                                                     */
/* ========================================================================= */

static volatile LONG s_is_initialized = 0;
static volatile LONG s_is_running     = 0;
static volatile LONG s_is_paused      = 0;
static volatile LONG s_stop_requested = 0;

static HANDLE s_cpu_thread       = NULL;
static HANDLE s_telemetry_thread = NULL;

static emu_telemetry_callback_t s_telemetry_cb   = NULL;
static void                    *s_telemetry_user = NULL;
static emu_log_callback_t       s_log_cb         = NULL;
static void                    *s_log_user       = NULL;
static emu_message_callback_t   s_message_cb     = NULL;
static void                    *s_message_user   = NULL;

static char s_config_path[MAX_PATH] = { 0 };
static char s_rom_path[MAX_PATH]    = { 0 };
static char s_status_buffer[256]    = { 0 };

/* Direct3D 11 Render Target Objects */
static IDXGISwapChain1          *s_swapchain           = NULL;
static ID3D11Device             *s_d3d_device          = NULL;
static ID3D11DeviceContext      *s_d3d_context         = NULL;
static ID3D11RenderTargetView   *s_render_target_view  = NULL;
static ID3D11Texture2D          *s_guest_texture       = NULL;
static ID3D11ShaderResourceView *s_guest_srv           = NULL;
static CRITICAL_SECTION          s_render_lock;
static CRITICAL_SECTION          s_lifecycle_lock;

/* This DLL's own module handle (captured in DllMain), used only to find CrtFilter.*.cso
 * next to 86box_core.dll regardless of where the host process itself lives. */
static HMODULE s_dll_module = NULL;

/* CRT post-process pipeline (Shaders/CrtFilter.hlsl -> CrtFilter.vs.cso / .ps.cso). Created
 * lazily on first use; s_crt_pipeline_ready is -1 until the first attempt, then 0 (failed -
 * files missing or device error, falls back to a plain CPU blit) or 1 (ready). */
static ID3D11VertexShader   *s_crt_vs           = NULL;
static ID3D11PixelShader    *s_crt_ps           = NULL;
static ID3D11SamplerState   *s_crt_sampler      = NULL;
static ID3D11RasterizerState*s_crt_rasterizer   = NULL;
static ID3D11Buffer         *s_crt_cbuffer      = NULL;
static int                   s_crt_pipeline_ready = -1;

/* Mirrors CrtParams in CrtFilter.hlsl (cbuffer register b0): 32 bytes, two 16-byte registers,
 * no implicit HLSL packing padding since every field is a naturally-aligned float/float2. */
typedef struct crt_params_t {
    float guest_w, guest_h;
    float output_w, output_h;
    float curvature;
    float mode;
    float bloom;
    float pad0;
} crt_params_t;

/* 0 = None (Sharp), 1 = Scanlines (Flat), 2 = Authentic CRT (Curved Glass). */
static volatile LONG s_crt_filter_mode = 0;
/* Curvature and bloom are 0..1000 fixed-point (i.e. amount * 1000) so they can share the
 * lock-free InterlockedExchange style the other display options already use. */
static volatile LONG s_crt_curvature_x1000 = 0;
static volatile LONG s_crt_bloom_x1000     = 0;

static uint32_t s_viewport_width  = 640;
static uint32_t s_viewport_height = 480;

static volatile LONG s_blit_count = 0;
static volatile LONG s_blit_nonzero = 0;
static volatile LONG s_present_ok = 0;
static volatile LONG s_present_hr = 0;
static volatile LONG s_resize_hr = 0;
static volatile LONG s_update_skipped = 0;
static volatile LONG s_act_latch_fdd, s_act_latch_hdd, s_act_latch_cd, s_act_latch_net;
static uint32_t *s_scaled     = NULL;
static size_t    s_scaled_cap = 0;
static volatile LONG s_aspect_mode = 0; /* 0 = force 4:3, 1 = stretch to fit */
static volatile LONG s_integer_scaling = 0;
static volatile LONG s_crt_scanlines = 0;
static uint32_t *s_frame     = NULL;
static size_t    s_frame_cap = 0;
static int       s_frame_w   = 0;
static int       s_frame_h   = 0;

static void log_bridge(int level, const char *fmt, ...)
{
    char buf[1024];
    va_list ap;
    va_start(ap, fmt);
    vsnprintf(buf, sizeof(buf), fmt, ap);
    va_end(ap);

    if (s_log_cb) {
        s_log_cb(level, buf, s_log_user);
    } else {
        OutputDebugStringA(buf);
        OutputDebugStringA("\n");
    }
}

/* ========================================================================= */
/* Direct3D 11 Resource Helpers                                              */
/* ========================================================================= */

static void cleanup_d3d_resources(void)
{
    EnterCriticalSection(&s_render_lock);
    if (s_crt_cbuffer) {
        ID3D11Buffer_Release(s_crt_cbuffer);
        s_crt_cbuffer = NULL;
    }
    if (s_crt_rasterizer) {
        ID3D11RasterizerState_Release(s_crt_rasterizer);
        s_crt_rasterizer = NULL;
    }
    if (s_crt_sampler) {
        ID3D11SamplerState_Release(s_crt_sampler);
        s_crt_sampler = NULL;
    }
    if (s_crt_ps) {
        ID3D11PixelShader_Release(s_crt_ps);
        s_crt_ps = NULL;
    }
    if (s_crt_vs) {
        ID3D11VertexShader_Release(s_crt_vs);
        s_crt_vs = NULL;
    }
    s_crt_pipeline_ready = -1;
    if (s_guest_srv) {
        ID3D11ShaderResourceView_Release(s_guest_srv);
        s_guest_srv = NULL;
    }
    if (s_guest_texture) {
        ID3D11Texture2D_Release(s_guest_texture);
        s_guest_texture = NULL;
    }
    if (s_render_target_view) {
        ID3D11RenderTargetView_Release(s_render_target_view);
        s_render_target_view = NULL;
    }
    if (s_d3d_context) {
        ID3D11DeviceContext_Release(s_d3d_context);
        s_d3d_context = NULL;
    }
    if (s_d3d_device) {
        ID3D11Device_Release(s_d3d_device);
        s_d3d_device = NULL;
    }
    if (s_swapchain) {
        IDXGISwapChain_Release(s_swapchain);
        s_swapchain = NULL;
    }
    LeaveCriticalSection(&s_render_lock);
}

static void update_render_target_view(void)
{
    if (!s_swapchain || !s_d3d_device)
        return;

    if (s_render_target_view) {
        ID3D11RenderTargetView_Release(s_render_target_view);
        s_render_target_view = NULL;
    }

    ID3D11Texture2D *back_buffer = NULL;
    HRESULT hr = IDXGISwapChain_GetBuffer(s_swapchain, 0, &IID_ID3D11Texture2D, (void **)&back_buffer);
    if (SUCCEEDED(hr) && back_buffer) {
        ID3D11Device_CreateRenderTargetView(s_d3d_device, (ID3D11Resource *)back_buffer, NULL, &s_render_target_view);
        ID3D11Texture2D_Release(back_buffer);
    }
}

static int ensure_guest_texture(uint32_t width, uint32_t height)
{
    if (!s_d3d_device)
        return 0;

    if (s_guest_texture) {
        D3D11_TEXTURE2D_DESC desc;
        ID3D11Texture2D_GetDesc(s_guest_texture, &desc);
        if (desc.Width == width && desc.Height == height)
            return 1;

        if (s_guest_srv) {
            ID3D11ShaderResourceView_Release(s_guest_srv);
            s_guest_srv = NULL;
        }
        ID3D11Texture2D_Release(s_guest_texture);
        s_guest_texture = NULL;
    }

    D3D11_TEXTURE2D_DESC desc;
    memset(&desc, 0, sizeof(desc));
    desc.Width              = width;
    desc.Height             = height;
    desc.MipLevels          = 1;
    desc.ArraySize          = 1;
    desc.Format             = DXGI_FORMAT_B8G8R8A8_UNORM;
    desc.SampleDesc.Count   = 1;
    desc.Usage              = D3D11_USAGE_DYNAMIC;
    desc.BindFlags          = D3D11_BIND_SHADER_RESOURCE;
    desc.CPUAccessFlags     = D3D11_CPU_ACCESS_WRITE;

    HRESULT hr = ID3D11Device_CreateTexture2D(s_d3d_device, &desc, NULL, &s_guest_texture);
    if (FAILED(hr) || !s_guest_texture)
        return 0;

    hr = ID3D11Device_CreateShaderResourceView(s_d3d_device, (ID3D11Resource *) s_guest_texture, NULL, &s_guest_srv);
    if (FAILED(hr)) {
        ID3D11Texture2D_Release(s_guest_texture);
        s_guest_texture = NULL;
        return 0;
    }
    return 1;
}

/* Reads an entire file into a malloc'd buffer (used for the precompiled .cso shader stages;
 * there is no D3DCompiler dependency, only the D3D11/DXGI headers this file already uses). */
static void *read_whole_file(const char *path, size_t *out_size)
{
    *out_size = 0;
    HANDLE h = CreateFileA(path, GENERIC_READ, FILE_SHARE_READ, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (h == INVALID_HANDLE_VALUE)
        return NULL;

    LARGE_INTEGER size;
    if (!GetFileSizeEx(h, &size) || size.QuadPart <= 0 || size.QuadPart > (16 * 1024 * 1024)) {
        CloseHandle(h);
        return NULL;
    }

    void *buf = malloc((size_t) size.QuadPart);
    if (!buf) {
        CloseHandle(h);
        return NULL;
    }

    DWORD read = 0;
    BOOL  ok   = ReadFile(h, buf, (DWORD) size.QuadPart, &read, NULL);
    CloseHandle(h);
    if (!ok || read != (DWORD) size.QuadPart) {
        free(buf);
        return NULL;
    }

    *out_size = (size_t) size.QuadPart;
    return buf;
}

/* Builds "<dir containing 86box_core.dll>\Shaders\<name>" into out. */
static void crt_shader_path(char *out, size_t cap, const char *name)
{
    char dll_path[MAX_PATH];
    dll_path[0] = '\0';
    if (!s_dll_module || !GetModuleFileNameA(s_dll_module, dll_path, sizeof(dll_path))) {
        out[0] = '\0';
        return;
    }

    char *slash = strrchr(dll_path, '\\');
    if (slash)
        *slash = '\0';
    else {
        out[0] = '\0';
        return;
    }

    snprintf(out, cap, "%s\\Shaders\\%s", dll_path, name);
}

/* Creates the CRT post-process pipeline (shaders, sampler, rasterizer state, constant buffer)
 * the first time present_locked() needs it. Safe to call every frame: after the first attempt
 * it just returns the cached result, success or failure (failure means CrtFilter.*.cso is
 * missing - e.g. fxc.exe was not available at build time - and present_locked() falls back to
 * its plain CPU blit instead). Caller must hold s_render_lock. */
static int ensure_crt_pipeline(void)
{
    if (s_crt_pipeline_ready >= 0)
        return s_crt_pipeline_ready;

    s_crt_pipeline_ready = 0;
    if (!s_d3d_device)
        return 0;

    char vs_path[MAX_PATH], ps_path[MAX_PATH];
    crt_shader_path(vs_path, sizeof(vs_path), "CrtFilter.vs.cso");
    crt_shader_path(ps_path, sizeof(ps_path), "CrtFilter.ps.cso");
    if (!vs_path[0] || !ps_path[0])
        return 0;

    size_t vs_size = 0, ps_size = 0;
    void  *vs_bytes = read_whole_file(vs_path, &vs_size);
    void  *ps_bytes = vs_bytes ? read_whole_file(ps_path, &ps_size) : NULL;
    if (!vs_bytes || !ps_bytes) {
        log_bridge(2, "CRT shader not found (%s) - the viewport will use a plain, unfiltered blit.", vs_path);
        free(vs_bytes);
        free(ps_bytes);
        return 0;
    }

    HRESULT hr = ID3D11Device_CreateVertexShader(s_d3d_device, vs_bytes, vs_size, NULL, &s_crt_vs);
    if (SUCCEEDED(hr))
        hr = ID3D11Device_CreatePixelShader(s_d3d_device, ps_bytes, ps_size, NULL, &s_crt_ps);
    free(vs_bytes);
    free(ps_bytes);
    if (FAILED(hr)) {
        log_bridge(3, "Failed to create the CRT shader pipeline: 0x%08X", hr);
        return 0;
    }

    D3D11_SAMPLER_DESC sd;
    memset(&sd, 0, sizeof(sd));
    sd.Filter         = D3D11_FILTER_MIN_MAG_MIP_LINEAR;
    sd.AddressU       = D3D11_TEXTURE_ADDRESS_CLAMP;
    sd.AddressV       = D3D11_TEXTURE_ADDRESS_CLAMP;
    sd.AddressW       = D3D11_TEXTURE_ADDRESS_CLAMP;
    sd.ComparisonFunc = D3D11_COMPARISON_NEVER;
    sd.MaxLOD         = D3D11_FLOAT32_MAX;
    hr = ID3D11Device_CreateSamplerState(s_d3d_device, &sd, &s_crt_sampler);
    if (FAILED(hr))
        return 0;

    D3D11_RASTERIZER_DESC rd;
    memset(&rd, 0, sizeof(rd));
    rd.FillMode = D3D11_FILL_SOLID;
    rd.CullMode = D3D11_CULL_NONE;
    hr = ID3D11Device_CreateRasterizerState(s_d3d_device, &rd, &s_crt_rasterizer);
    if (FAILED(hr))
        return 0;

    D3D11_BUFFER_DESC cbd;
    memset(&cbd, 0, sizeof(cbd));
    cbd.ByteWidth      = sizeof(crt_params_t);
    cbd.Usage          = D3D11_USAGE_DYNAMIC;
    cbd.BindFlags      = D3D11_BIND_CONSTANT_BUFFER;
    cbd.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
    hr = ID3D11Device_CreateBuffer(s_d3d_device, &cbd, NULL, &s_crt_cbuffer);
    if (FAILED(hr))
        return 0;

    log_bridge(1, "CRT post-process shader pipeline ready.");
    s_crt_pipeline_ready = 1;
    return 1;
}

/* ========================================================================= */
/* Emulation Worker Threads                                                  */
/* ========================================================================= */

/* Called by fatal() in 86box.c after it has logged the error, queued the message for the window and asked the
 * core to stop. The stock behaviour is exit(-1), which here would silently kill the whole WinUI application
 * (the queued message never gets shown). On the CPU emulation thread only that thread is ended - the window
 * stays up showing the error, and closing the machine shuts the engine down normally. On any other thread
 * (e.g. the one starting the engine) this returns and fatal() falls back to exit(). */
void emu_bridge_fatal_exit(void)
{
    if (!s_is_emu_thread)
        return;

    log_bridge(3, "Fatal engine error - emulation thread ended; the host application keeps running.");
    InterlockedExchange(&s_stop_requested, 1);
    cpu_thread_run       = 0;
    s_cpu_thread_running = 0;
    timeEndPeriod(1);
    ExitThread(1);
}

static DWORD WINAPI emu_cpu_worker_thread(LPVOID lpParam)
{
    (void)lpParam;
    log_bridge(1, "86Box CPU emulation thread started.");
    SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_HIGHEST);
    timeBeginPeriod(1);

    LARGE_INTEGER freq, start_ticks, now_ticks;
    QueryPerformanceFrequency(&freq);
    QueryPerformanceCounter(&start_ticks);

    const int64_t quantum_ns  = force_10ms ? 10000000LL : 1000000LL;
    const int64_t max_debt_ns = 50000000LL;
    int64_t debt_ns = 0;
    int frames = 0;

    s_is_emu_thread      = 1;
    s_cpu_thread_running = 1;
    cpu_thread_run       = 1;

    while (!InterlockedCompareExchange(&s_stop_requested, 0, 0) && cpu_thread_run && !is_quit) {
        QueryPerformanceCounter(&now_ticks);
        int64_t delta_ns = ((now_ticks.QuadPart - start_ticks.QuadPart) * 1000000000LL) / freq.QuadPart;
        start_ticks = now_ticks;

        debt_ns += delta_ns;
        if (debt_ns > max_debt_ns)
            debt_ns = max_debt_ns;

        if (((debt_ns >= quantum_ns) || fast_forward) && !dopause) {
            pc_run();

            if (++frames >= (force_10ms ? 200 : 2000) && nvr_dosave) {
                nvr_save();
                nvr_dosave = 0;
                frames = 0;
            }

            if (!fast_forward && debt_ns >= quantum_ns)
                debt_ns -= quantum_ns;
            else
                debt_ns = 0;
        } else {
            if (hard_reset_pending) {
                hard_reset_pending = 0;

                /* video_blit_memtoscreen_monitor() wakes the core's own blit thread and returns
                 * immediately without waiting for it (emu_blit, on that separate thread, may still
                 * be mid-copy from monitors[0].target_buffer, and present_locked() mid-Present on
                 * s_d3d_device/s_swapchain). pc_reset_hard_close() tears all of that down on THIS
                 * thread with no awareness of the other one, so without this wait a reset lands
                 * mid-blit at random and the process dies to a native access violation - reliably,
                 * since present_locked() (screenshot PNG encode, texture Map/Unmap, Present) keeps
                 * that window open far longer than the core's own, much leaner blit callback would.
                 * video_wait_for_blit_monitor() is the core's own primitive for exactly this: it
                 * already gates every new blit request on the previous one being done. */
                video_wait_for_blit_monitor(0);
                pc_reset_hard_close();
                pc_reset_hard_init();
            }

            if (dopause)
                ack_pause();

            Sleep(1);
        }
    }

    s_cpu_thread_running = 0;
    timeEndPeriod(1);
    log_bridge(1, "86Box CPU emulation thread finished.");
    return 0;
}

static DWORD WINAPI emu_telemetry_worker_thread(LPVOID lpParam)
{
    (void)lpParam;
    int sec_counter = 0;

    while (!InterlockedCompareExchange(&s_stop_requested, 0, 0)) {
        Sleep(50);

        if (++sec_counter >= 20) {
            sec_counter = 0;
            pc_onesec();
        }

        if (s_telemetry_cb) {
            emu_telemetry_t data;
            memset(&data, 0, sizeof(data));

            data.speed_percentage = (double)(fps > 0 ? (fps * 100 / (force_10ms ? 100 : 1000)) : 100);
            data.fps              = (uint32_t)fps;
            data.is_paused        = dopause ? 1 : 0;
            data.is_running       = InterlockedCompareExchange(&s_is_running, 0, 0);
            data.mouse_captured   = mouse_capture;

            data.fdd_active = (uint32_t) InterlockedExchange(&s_act_latch_fdd, 0);
            for (size_t i = 0; i < FDD_NUM; ++i) {
                if (machine_status.fdd[i].active)
                    data.fdd_active |= (1 << i);
            }

            data.hdd_active = (uint32_t) InterlockedExchange(&s_act_latch_hdd, 0);
            for (size_t i = 0; i < HDD_BUS_USB; ++i) {
                if (machine_status.hdd[i].active)
                    data.hdd_active |= (1 << i);
            }

            data.net_active = (uint32_t) InterlockedExchange(&s_act_latch_net, 0);
            for (size_t i = 0; i < NET_CARD_MAX; ++i) {
                if (machine_status.net[i].active)
                    data.net_active |= (1 << i);
            }

            data.cd_active = (uint32_t) InterlockedExchange(&s_act_latch_cd, 0);
            for (size_t i = 0; i < CDROM_NUM; ++i) {
                if (machine_status.cdrom[i].active)
                    data.cd_active |= (1 << i);
            }

            snprintf(s_status_buffer, sizeof(s_status_buffer), "86Box - %d%% (%u FPS)%s",
                     (int)data.speed_percentage, data.fps, data.is_paused ? " [Paused]" : "");
            data.status_string = s_status_buffer;

            s_telemetry_cb(&data, s_telemetry_user);
        }
    }
    return 0;
}

/* ========================================================================= */
/* Exported C-ABI Implementation                                             */
/* ========================================================================= */

static void emu_blit(int x, int y, int w, int h, int monitor_index);

__declspec(dllexport) void emu_set_rom_path(const char *rom_path)
{
    s_rom_path[0] = 0;
    if (rom_path && rom_path[0])
        strncpy(s_rom_path, rom_path, sizeof(s_rom_path) - 1);
}

/* Asks the engine itself whether the ROM set for a machine is present under rom_path.
 * Returns 1 = available, 0 = ROM files missing, -1 = unknown machine, -2 = engine busy. */
/* Uses the engine's own cpu_is_eligible(): package, bus-speed (FSB), voltage and multiplier rules of
 * the machine. Returns 1 = eligible, 0 = not eligible, -1 = unknown machine/family/step. */
__declspec(dllexport) int emu_cpu_step_eligible(const char *machine_internal_name, const char *family_internal_name,
                                                long long rspeed_hz, double multi)
{
    if (!machine_internal_name || !family_internal_name)
        return -1;

    int m = machine_get_machine_from_internal_name(machine_internal_name);
    if (m < 0)
        return -1;

    for (int f = 0; cpu_families[f].package != 0; f++) {
        if (!cpu_families[f].internal_name || strcmp(cpu_families[f].internal_name, family_internal_name) != 0)
            continue;
        for (int i = 0; cpu_families[f].cpus[i].name != NULL && cpu_families[f].cpus[i].rspeed != 0; i++) {
            const CPU *cpu_entry = &cpu_families[f].cpus[i];
            if ((long long) cpu_entry->rspeed == rspeed_hz && cpu_entry->multi > multi - 0.001 && cpu_entry->multi < multi + 0.001)
                return cpu_is_eligible(&cpu_families[f], i, m) ? 1 : 0;
        }
        return -1;
    }
    return -1;
}

/* Is the video card usable (its BIOS ROM present)? 1 = yes, 0 = ROM missing, -1 = unknown card, -2 = engine busy. */
__declspec(dllexport) int emu_check_video_card(const char *rom_path, const char *card_internal_name)
{
    if (!card_internal_name || !card_internal_name[0])
        return -1;

    EnterCriticalSection(&s_lifecycle_lock);
    if (InterlockedCompareExchange(&s_is_initialized, 0, 0) != 0) {
        LeaveCriticalSection(&s_lifecycle_lock);
        return -2;
    }

    if (rom_path && rom_path[0])
        rom_add_path(rom_path);

    int card = video_get_video_from_internal_name((char *) card_internal_name);
    /* An unknown name comes back as card 0 ("none"), so confirm the engine really knows this card. */
    const char *known = (card > 0) ? video_get_internal_name(card) : NULL;
    int ret = (card <= 0 || known == NULL || strcmp(known, card_internal_name) != 0)
                  ? -1
                  : (video_card_available(card) ? 1 : 0);

    LeaveCriticalSection(&s_lifecycle_lock);
    return ret;
}

__declspec(dllexport) int emu_check_machine_roms(const char *rom_path, const char *machine_internal_name)
{
    if (!machine_internal_name || !machine_internal_name[0])
        return -1;

    EnterCriticalSection(&s_lifecycle_lock);
    if (InterlockedCompareExchange(&s_is_initialized, 0, 0) != 0) {
        LeaveCriticalSection(&s_lifecycle_lock);
        return -2;
    }

    if (rom_path && rom_path[0])
        rom_add_path(rom_path);

    int m   = machine_get_machine_from_internal_name(machine_internal_name);
    int ret = (m < 0) ? -1 : (machine_available(m) ? 1 : 0);

    LeaveCriticalSection(&s_lifecycle_lock);
    return ret;
}

__declspec(dllexport) int emu_initialize(const char *config_path)
{
    /* s_render_lock/s_lifecycle_lock are initialized in DllMain
     * (DLL_PROCESS_ATTACH), not here: emu_set_dxgi_swapchain() and
     * emu_resize_viewport() also take s_render_lock and can be called from
     * the UI thread (e.g. a SwapChainPanel Loaded/SizeChanged event) before
     * this function ever runs on its background thread. Lazily
     * initializing the locks here left a window where those calls could
     * EnterCriticalSection on a still-zeroed CRITICAL_SECTION, corrupting
     * the heap and crashing later (observed as an access violation deep
     * inside emu_resize_viewport). */
    EnterCriticalSection(&s_lifecycle_lock);

    if (InterlockedCompareExchange(&s_is_initialized, 1, 0) != 0) {
        LeaveCriticalSection(&s_lifecycle_lock);
        return 0; /* Already initialized */
    }

    if (config_path && config_path[0]) {
        strncpy(s_config_path, config_path, sizeof(s_config_path) - 1);
    }

    /* The VM directory is authoritative: it must exist and hold this machine's
     * 86box.cfg. Refuse to start rather than let the core fall back to a default
     * configuration created in the working directory. */
    static char s_log_file[MAX_PATH + 16];
    if (!s_config_path[0]) {
        log_bridge(3, "No VM directory supplied");
        InterlockedExchange(&s_is_initialized, 0);
        LeaveCriticalSection(&s_lifecycle_lock);
        return 3;
    }
    {
        char cfg_file[MAX_PATH + 16];
        snprintf(cfg_file, sizeof(cfg_file), "%s\\86box.cfg", s_config_path);
        DWORD attr = GetFileAttributesA(s_config_path);
        if (attr == INVALID_FILE_ATTRIBUTES || !(attr & FILE_ATTRIBUTE_DIRECTORY)
            || GetFileAttributesA(cfg_file) == INVALID_FILE_ATTRIBUTES) {
            log_bridge(3, "VM directory or 86box.cfg not found: %s", cfg_file);
            InterlockedExchange(&s_is_initialized, 0);
            LeaveCriticalSection(&s_lifecycle_lock);
            return 3;
        }
        /* Relative paths in the config (images, nvr, screenshots) resolve against the VM folder. */
        SetCurrentDirectoryA(s_config_path);
        snprintf(s_log_file, sizeof(s_log_file), "%s\\86box.log", s_config_path);
    }

    char *argv[9];
    int   argc = 0;
    argv[argc++] = "86box_core";
    argv[argc++] = "-P";
    argv[argc++] = s_config_path;
    argv[argc++] = "-L";
    argv[argc++] = s_log_file;
    if (s_rom_path[0]) {
        argv[argc++] = "-R";
        argv[argc++] = s_rom_path;
    }
    argv[argc] = NULL;

    log_bridge(1, "Initializing 86Box engine: %s", s_config_path);

    /* The core keeps process-wide state. After a previous run in this process vmm_path holds the default "."
     * and pc_init() then treats it as an explicit -E, launching the VM-manager mode instead of this machine
     * (black screen on every second VM). Reset what pc_init() reads before it parses the arguments. */
    EnterCriticalSection(&s_render_lock);
    s_frame_w = 0; /* drop the previous machine's last frame */
    s_frame_h = 0;
    LeaveCriticalSection(&s_render_lock);

    is_quit     = 0;  /* set by emu_shutdown(); the CPU loop would exit at once on the next machine */
    vmm_path[0] = '\0';
    start_vmm   = 1;
    log_path[0] = '\0';

    if (!pc_init(argc, argv)) {
        log_bridge(3, "pc_init failed");
        InterlockedExchange(&s_is_initialized, 0);
        LeaveCriticalSection(&s_lifecycle_lock);
        return 1;
    }

    if (!pc_init_roms()) {
        log_bridge(3, "pc_init_roms failed: ROM images missing");
        InterlockedExchange(&s_is_initialized, 0);
        LeaveCriticalSection(&s_lifecycle_lock);
        return 2;
    }

    pc_init_modules();
    machine_status_init();
    video_setblit(emu_blit);

    LeaveCriticalSection(&s_lifecycle_lock);
    return 0;
}

__declspec(dllexport) int emu_start(void)
{
    EnterCriticalSection(&s_lifecycle_lock);

    if (!InterlockedCompareExchange(&s_is_initialized, 1, 1)) {
        LeaveCriticalSection(&s_lifecycle_lock);
        return 1;
    }

    if (InterlockedCompareExchange(&s_is_running, 1, 0) != 0) {
        LeaveCriticalSection(&s_lifecycle_lock);
        return 0; /* Already running */
    }

    InterlockedExchange(&s_stop_requested, 0);
    InterlockedExchange(&s_is_paused, 0);
    dopause = 0;

    pc_reset_hard_init();

    s_cpu_thread = CreateThread(NULL, 0, emu_cpu_worker_thread, NULL, 0, NULL);
    s_telemetry_thread = CreateThread(NULL, 0, emu_telemetry_worker_thread, NULL, 0, NULL);

    LeaveCriticalSection(&s_lifecycle_lock);
    return 0;
}

__declspec(dllexport) int emu_start_emulation(const char *config_path, void *target_surface)
{
    if (target_surface) {
        emu_set_dxgi_swapchain(target_surface);
    }

    int init_res = emu_initialize(config_path);
    if (init_res != 0) {
        return init_res;
    }

    return emu_start();
}

/* Writes the engine's effective settings back to 86box.cfg (config_save). */
__declspec(dllexport) int emu_save_config(void)
{
    if (!InterlockedCompareExchange(&s_is_initialized, 1, 1))
        return 1;
    config_save();
    return 0;
}

__declspec(dllexport) int emu_pause(int pause)
{
    InterlockedExchange(&s_is_paused, pause ? 1 : 0);
    plat_pause(pause ? 1 : 0);
    return 0;
}

__declspec(dllexport) int emu_reset(int hard_reset)
{
    if (hard_reset) {
        hard_reset_pending = 1;
    } else {
        pc_send_cad();
    }
    return 0;
}

extern atomic_int acpi_pwrbut_pressed;

/* ACPI power button press (graceful guest shutdown on ACPI machines). */
__declspec(dllexport) int emu_acpi_power_button(void)
{
    acpi_pwrbut_pressed = 1;
    return 0;
}

/* Queues a screenshot of monitor 0; the engine writes it to <vm>/screenshots. */
__declspec(dllexport) int emu_take_screenshot(void)
{
    /* The "raw" counter is the one the core's own PNG writer consumes (video_screenshot_monitor) from the blit path. */
    atomic_fetch_add(&monitors[0].mon_screenshots_raw, 1);
    return 0;
}

/* Display aspect: 0 = keep 4:3 (letterbox), 1 = stretch to the viewport. */
__declspec(dllexport) void emu_set_aspect_mode(int mode)
{
    InterlockedExchange(&s_aspect_mode, mode ? 1 : 0);
}

/* Hot-mount / eject of removable media, mirroring the engine's own media menu. */
__declspec(dllexport) int emu_floppy_mount(int id, const char *path, int write_protect)
{
    if (id < 0 || id >= FDD_NUM || !path || !path[0])
        return 1;

    char buf[1100];
    fdd_close(id);
    ui_writeprot[id] = write_protect ? 1 : 0;
    if (write_protect)
        snprintf(buf, sizeof(buf), "wp://%s", path);
    else
        snprintf(buf, sizeof(buf), "%s", path);
    fdd_load(id, buf);
    return 0;
}

__declspec(dllexport) int emu_floppy_eject(int id)
{
    if (id < 0 || id >= FDD_NUM)
        return 1;
    fdd_close(id);
    return 0;
}

__declspec(dllexport) int emu_cdrom_mount(int id, const char *path)
{
    if (id < 0 || id >= CDROM_NUM || !path || !path[0])
        return 1;

    int was_empty = cdrom_is_empty((uint8_t) id);
    cdrom_exit((uint8_t) id);
    memset(cdrom[id].image_path, 0, sizeof(cdrom[id].image_path));
    cdrom_load(&cdrom[id], path, 1);

    /* Signal the media change to the emulated machine. */
    if (cdrom[id].insert) {
        cdrom[id].insert(cdrom[id].priv);
        if (was_empty)
            cdrom[id].insert(cdrom[id].priv);
    }
    return 0;
}

__declspec(dllexport) int emu_cdrom_eject(int id)
{
    if (id < 0 || id >= CDROM_NUM)
        return 1;
    cdrom_eject((uint8_t) id);
    return 0;
}

/* Fast forward: run the CPU loop unthrottled and let the audio backend skip its sync (86Box "fast_forward"). */
__declspec(dllexport) void emu_set_fast_forward(int enable)
{
    fast_forward = enable ? true : false;
}

/* Network link state of card slot `id` (0-based): connect / disconnect the emulated cable. */
__declspec(dllexport) int emu_network_connect(int id, int connect)
{
    if (id < 0 || id >= NET_CARD_MAX)
        return 1;
    network_connect(id, connect ? 1 : 0);
    return 0;
}

/* 1 = link up, 0 = link down, -1 = unknown slot. */
__declspec(dllexport) int emu_network_is_connected(int id)
{
    if (id < 0 || id >= NET_CARD_MAX)
        return -1;
    return network_is_connected(id) ? 1 : 0;
}

/* Display options: integer (pixel-perfect) scaling, and the quick viewport-toolbar CRT toggle.
 * crt_scanlines is the older two-state on/off control kept for that toolbar button; it maps onto
 * the richer three-mode filter below (see emu_set_crt_filter_mode) without discarding an
 * "Authentic CRT" choice already made in Settings - only None <-> Scanlines is toggled here. */
__declspec(dllexport) void emu_set_display_options(int integer_scaling, int crt_scanlines)
{
    InterlockedExchange(&s_integer_scaling, integer_scaling ? 1 : 0);
    InterlockedExchange(&s_crt_scanlines, crt_scanlines ? 1 : 0);

    LONG mode = InterlockedCompareExchange(&s_crt_filter_mode, 0, 0);
    if (crt_scanlines) {
        if (mode == 0)
            InterlockedExchange(&s_crt_filter_mode, 1);
    } else if (mode == 1) {
        InterlockedExchange(&s_crt_filter_mode, 0);
    }
}

/* The full CRT post-process filter, set from the Settings > Display page.
 * mode: 0 = None (Sharp), 1 = Scanlines (Flat), 2 = Authentic CRT (Curved Glass). */
__declspec(dllexport) void emu_set_crt_filter_mode(int mode)
{
    InterlockedExchange(&s_crt_filter_mode, (mode < 0 || mode > 2) ? 0 : mode);
}

/* amount: 0.0 .. 0.15 (0% - 15% screen curvature, "Authentic CRT" mode only). */
__declspec(dllexport) void emu_set_crt_curvature(float amount)
{
    if (amount < 0.0f) amount = 0.0f;
    if (amount > 0.15f) amount = 0.15f;
    InterlockedExchange(&s_crt_curvature_x1000, (LONG) (amount * 1000.0f));
}

/* amount: 0.0 .. 1.0 phosphor bloom intensity ("Authentic CRT" mode only). */
__declspec(dllexport) void emu_set_crt_bloom(float amount)
{
    if (amount < 0.0f) amount = 0.0f;
    if (amount > 1.0f) amount = 1.0f;
    InterlockedExchange(&s_crt_bloom_x1000, (LONG) (amount * 1000.0f));
}

extern int nmi;

/* Raises a non-maskable interrupt on the emulated CPU. */
__declspec(dllexport) int emu_trigger_nmi(void)
{
    nmi = 1;
    return 0;
}

/* Master volume (dB, 0 = full) and mute. */
__declspec(dllexport) void emu_set_sound(int gain_db, int muted)
{
    sound_gain  = gain_db;
    sound_muted = muted ? 1 : 0;
}

/* Copies the current guest frame (BGRA, tightly packed). Returns the pixel count, 0 = no frame, -1 = buffer too small. */
__declspec(dllexport) int emu_copy_frame(uint32_t *dst, int capacity_pixels, int *width, int *height)
{
    int result = 0;
    EnterCriticalSection(&s_render_lock);
    if (s_frame && s_frame_w > 0 && s_frame_h > 0) {
        int need = s_frame_w * s_frame_h;
        if (dst && capacity_pixels >= need) {
            memcpy(dst, s_frame, (size_t) need * sizeof(uint32_t));
            result = need;
        } else {
            result = -1;
        }
        if (width) *width = s_frame_w;
        if (height) *height = s_frame_h;
    }
    LeaveCriticalSection(&s_render_lock);
    return result;
}

/* 1 = the machine has ACPI (soft power button), 0 = it does not, -1 = unknown machine. */
__declspec(dllexport) int emu_machine_supports_acpi(const char *machine_internal_name)
{
    if (!machine_internal_name || !machine_internal_name[0])
        return -1;

    int m = machine_get_machine_from_internal_name(machine_internal_name);
    if (m < 0)
        return -1;

    return machine_has_flags(m, MACHINE_ACPI) ? 1 : 0;
}

/* ---- Hard disk image helpers (the engine's own minivhd, same geometry rules as the Qt "New hard disk" dialog) ---- */

/* Creates a dynamic VHD for the 86Box geometry cyl/heads/spt. A geometry the VHD format cannot hold (more than
 * 65535 cylinders) is rounded up to a compatible size; the geometry 86Box must use is returned in out_*.
 * Returns 0 on success, otherwise the minivhd error code (or -1 for bad arguments). */
__declspec(dllexport) int emu_vhd_create_dynamic(const char *path, unsigned cyl, unsigned heads, unsigned spt,
                                                 unsigned *out_cyl, unsigned *out_heads, unsigned *out_spt)
{
    if (!path || !path[0] || !cyl || !heads || !spt)
        return -1;

    unsigned c = cyl, h = heads, sp = spt;
    MVHDGeom vg;
    if (c <= 65535) {
        vg.cyl = (uint16_t) c; vg.heads = (uint8_t) h; vg.spt = (uint8_t) sp;
    } else {
        unsigned long long desired = (unsigned long long) c * h * sp;
        if (desired > 267321600ULL)
            desired = 267321600ULL;
        unsigned rem = (unsigned) (desired % 85680ULL);
        if (rem > 0)
            desired += (85680ULL - rem);
        c = (unsigned) (desired / (16 * 63)); h = 16; sp = 63;
        vg.cyl = (uint16_t) (desired / (16 * 255)); vg.heads = 16; vg.spt = 255;
    }

    int                 err = 0;
    MVHDCreationOptions opt;
    memset(&opt, 0, sizeof(opt));
    opt.type                  = MVHD_TYPE_DYNAMIC;
    opt.path                  = (char *) path;
    opt.size_in_bytes         = 0;
    opt.geometry              = vg;
    opt.block_size_in_sectors = MVHD_BLOCK_LARGE;

    MVHDMeta *vhd = mvhd_create_ex(opt, &err);
    if (!vhd)
        return err ? err : -2;
    mvhd_close(vhd);

    if (out_cyl) *out_cyl = c;
    if (out_heads) *out_heads = h;
    if (out_spt) *out_spt = sp;
    return 0;
}

/* Geometry of an existing VHD as 86Box must use it (adjust_vhd_geometry_for_86box). 0 = ok, otherwise error. */
__declspec(dllexport) int emu_vhd_read_geometry(const char *path, unsigned *out_cyl, unsigned *out_heads, unsigned *out_spt)
{
    if (!path || !path[0])
        return -1;

    int       err = 0;
    MVHDMeta *vhd = mvhd_open(path, 1, &err);
    if (!vhd)
        return err ? err : -2;

    MVHDGeom g = mvhd_get_geometry(vhd);
    mvhd_close(vhd);

    unsigned c = g.cyl, h = g.heads, sp = g.spt;
    if (g.spt > 63) {
        long long desired = (long long) g.cyl * g.heads * g.spt;
        if (desired > 267321600LL)
            desired = 267321600LL;
        int rem = (int) (desired % 85680LL);
        if (rem > 0)
            desired -= rem;
        c = (unsigned) (desired / (16 * 63)); h = 16; sp = 63;
    }

    if (out_cyl) *out_cyl = c;
    if (out_heads) *out_heads = h;
    if (out_spt) *out_spt = sp;
    return 0;
}

/* MIDI output (input = 0) or input (input = 1) devices exactly as the original Sound page lists them
 * (midi_out_devices[] / midi_in_devices[] in midi.c, so only what this build was compiled with, and only
 * the ones that are available). "internal_name<TAB>label<LF>" per entry; returns the count. */
__declspec(dllexport) int emu_list_midi_devices(int input, char *out, int capacity)
{
    if (!out || capacity <= 0)
        return -1;
    out[0] = '\0';

    size_t used  = 0;
    int    count = 0;
    for (int c = 0;; c++) {
        const device_t *dev = input ? midi_in_device_getdevice(c) : midi_out_device_getdevice(c);
        const char     *internal = input ? midi_in_device_get_internal_name(c) : midi_out_device_get_internal_name(c);
        if (!dev && c > 0)
            break; /* the table ends with a NULL device; entry 0 is "none" (which is also a NULL device) */
        if (!internal)
            break;
        if (!(input ? midi_in_device_available(c) : midi_out_device_available(c)))
            continue;

        char label[512];
        if (!dev)
            snprintf(label, sizeof(label), "None");
        else
            device_get_name(dev, 0, label);

        int len = snprintf(out + used, (size_t) capacity - used, "%s\t%s\n", internal, label);
        if (len < 0 || used + (size_t) len >= (size_t) capacity)
            return -2;
        used += (size_t) len;
        count++;
    }
    return count;
}

/* Label of an input device as Qt shows it (no bus prefix). */
static void input_label(const device_t *dev, const char *internal, char *out, size_t cap)
{
    if (!strcmp(internal, "none")) {
        snprintf(out, cap, "None");
    } else if (!strcmp(internal, "internal")) {
        snprintf(out, cap, "Internal device");
    } else if (dev == NULL) {
        out[0] = '\0';
    } else {
        char temp[512];
        device_get_name(dev, 0, temp);
        snprintf(out, cap, "%s", temp);
    }
}

/* kind: 0 keyboard, 1 mouse, 2 tablet. Same filtering as the original's Input page. */
__declspec(dllexport) int emu_list_input_devices(int kind, const char *machine_internal_name, char *out, int capacity)
{
    if (!out || capacity <= 0)
        return -1;
    out[0] = '\0';

    int m = machine_get_machine_from_internal_name(machine_internal_name ? machine_internal_name : "");
    if (m < 0)
        return -1;

    int n   = kind == 0 ? keyboard_get_ndev() : kind == 1 ? mouse_get_ndev() : tablet_get_ndev();
    int has_int_kbd = !!machine_has_flags(m, MACHINE_KEYBOARD);

    size_t used  = 0;
    int    count = 0;
    for (int i = 0; i < n; i++) {
        const device_t *dev      = kind == 0 ? keyboard_get_device(i) : kind == 1 ? mouse_get_device(i) : tablet_get_device(i);
        const char     *internal = kind == 0 ? keyboard_get_internal_name(i) : kind == 1 ? mouse_get_internal_name(i) : tablet_get_internal_name(i);
        if (!internal)
            continue;

        if (kind == 0) {
            int ikbd = (i == KEYBOARD_TYPE_INTERNAL);
            int pc5086_filter = (strstr(internal, "ps") && machines[m].init == machine_xt_pc5086_init);
            if ((ikbd != has_int_kbd) || !device_is_valid(dev, m) || pc5086_filter)
                continue;
        } else if (kind == 1) {
            if ((i == MOUSE_TYPE_INTERNAL) && !machine_has_flags(m, MACHINE_MOUSE))
                continue;
            if (!device_is_valid(dev, m))
                continue;
        } else {
            if (!device_is_valid(dev, m))
                continue;
            if (!strcmp(internal, "cga_lightpen"))
                continue;
        }

        char label[640];
        input_label(dev, internal, label, sizeof(label));
        if (label[0] == '\0')
            continue;

        int len = snprintf(out + used, (size_t) capacity - used, "%s\t%s\n", internal, label);
        if (len < 0 || used + (size_t) len >= (size_t) capacity)
            return -2;
        used += (size_t) len;
        count++;
    }
    return count;
}

/* ---- Selectable devices, exactly as the original settings dialog lists them ---------------------------------
 * kind: 0 video, 1 sound, 2 network, 3 hard disk controller, 4 SCSI, 5 floppy controller, 6 secondary video.
 * slot: the card slot (the "internal" entry only belongs to the first slot, as in Qt).
 * primary_video: internal name of the primary card (used for the secondary video list).
 * Output: one "internal_name<TAB>label<LF>" per entry (label has the "[bus]" prefix), returns the entry count. */
static void list_label(const device_t *dev, const char *internal, char *out, size_t cap)
{
    if (!strcmp(internal, "none")) {
        snprintf(out, cap, "None");
    } else if (!strcmp(internal, "internal")) {
        snprintf(out, cap, "Internal device");
    } else if (dev == NULL) {
        out[0] = '\0';
    } else {
        char        temp[512];
        const char *bus;
        device_get_name(dev, -1, temp);
        bus = device_get_bus_name(dev);
        if (bus != NULL)
            snprintf(out, cap, "[%s] %s", bus, temp);
        else
            snprintf(out, cap, "%s", temp);
    }
}

__declspec(dllexport) int emu_list_devices(int kind, const char *machine_internal_name, int slot, const char *primary_video,
                                           char *out, int capacity)
{
    if (!out || capacity <= 0)
        return -1;
    out[0] = '\0';

    int m = machine_get_machine_from_internal_name(machine_internal_name ? machine_internal_name : "");
    if (m < 0)
        return -1;

    /* Availability means "its ROMs are present", so the ROM directory must be known to the search. */
    static char added_rom_path[MAX_PATH];
    if (s_rom_path[0] && strcmp(added_rom_path, s_rom_path) != 0) {
        rom_add_path(s_rom_path);
        strncpy(added_rom_path, s_rom_path, sizeof(added_rom_path) - 1);
    }

    int primary = 0;
    if (primary_video && primary_video[0])
        primary = video_get_video_from_internal_name((char *) primary_video);

    int   count = 0;
    size_t used = 0;

    for (int c = 0;; c++) {
        const device_t *dev      = NULL;
        const char     *internal = NULL;
        int             avail    = 0;

        switch (kind) {
            case 0:
            case 6:
                if (kind == 0 && c == 1 && !machine_has_flags(m, MACHINE_VIDEO)) continue;
                dev = video_card_getdevice(c); internal = video_get_internal_name(c); avail = video_card_available(c); break;
            case 1: dev = sound_card_getdevice(c); internal = sound_card_get_internal_name(c); avail = sound_card_available(c); break;
            case 2: dev = network_card_getdevice(c); internal = network_card_get_internal_name(c); avail = network_card_available(c); break;
            case 3: dev = hdc_get_device(c); internal = hdc_get_internal_name(c); avail = hdc_available(c); break;
            case 4: dev = scsi_card_getdevice(c); internal = scsi_card_get_internal_name(c); avail = scsi_card_available(c); break;
            case 5: dev = fdc_card_getdevice(c); internal = fdc_card_get_internal_name(c); avail = fdc_card_available(c); break;
            case 7: dev = cdrom_interface_get_device(c); internal = cdrom_interface_get_internal_name(c); avail = cdrom_interface_available(c); break;
            default: return -1;
        }

        char label[640];
        if (!internal) break;
        list_label(dev, internal, label, sizeof(label));
        if (label[0] == '\0')
            break;

        if (!avail || !device_is_valid(dev, m))
            continue;

        /* the "internal" entry (index 1) only exists where the machine has that part, and only in the first slot */
        if (kind == 1 && c == 1 && (slot > 0 || !machine_has_flags(m, MACHINE_SOUND))) continue;
        if (kind == 2 && c == 1 && (slot > 0 || !machine_has_flags(m, MACHINE_NIC))) continue;
        if (kind == 3 && c == 1 && (slot > 0 || !machine_has_flags(m, MACHINE_HDC))) continue;

        if (kind == 6) {
            if (c == 1) continue; /* no "internal" as a secondary card */
            int pf = video_card_get_flags(primary), sf = video_card_get_flags(c);
            const device_t *pd = video_card_getdevice(primary);
            int p_agp = pd && (pd->flags & DEVICE_AGP), s_agp = dev && (dev->flags & DEVICE_AGP);
            if (pf == VIDEO_FLAG_TYPE_MDA) continue;
            if (p_agp && s_agp) continue;
            if (sf == pf && sf != VIDEO_FLAG_TYPE_SECONDARY) continue;
            if ((pf == VIDEO_FLAG_TYPE_8514 || pf == VIDEO_FLAG_TYPE_XGA) && sf != VIDEO_FLAG_TYPE_MDA && sf != VIDEO_FLAG_TYPE_SECONDARY) continue;
            if (pf != VIDEO_FLAG_TYPE_MDA && pf != VIDEO_FLAG_TYPE_SECONDARY && (sf == VIDEO_FLAG_TYPE_8514 || sf == VIDEO_FLAG_TYPE_XGA)) continue;
        }

        /* built-in part: "(Name)" of what the board actually has, like Qt */
        if (kind == 0 && c == 1 && machine_get_vid_device(m))
            snprintf(label + strlen(label), sizeof(label) - strlen(label), " (%s)", machine_get_vid_device(m)->name);
        if (kind == 1 && c == 1 && machine_get_snd_device(m))
            snprintf(label + strlen(label), sizeof(label) - strlen(label), " (%s)", machine_get_snd_device(m)->name);

        int n = snprintf(out + used, (size_t) capacity - used, "%s\t%s\n", internal, label);
        if (n < 0 || used + (size_t) n >= (size_t) capacity)
            return -2;
        used += (size_t) n;
        count++;
    }

    return count;
}

/* Host network devices for the PCap mode ("device<TAB>description<LF>"), as the original's Interface list.
 * network_init() fills the table (loads the pcap library); it is only run when no machine is running. */
__declspec(dllexport) int emu_list_network_host_devices(char *out, int capacity)
{
    if (!out || capacity <= 0)
        return -1;
    out[0] = '\0';

    EnterCriticalSection(&s_lifecycle_lock);
    if (InterlockedCompareExchange(&s_is_initialized, 0, 0) == 0)
        network_init();
    LeaveCriticalSection(&s_lifecycle_lock);

    size_t used = 0;
    int    count = 0;
    for (int i = 0; i < network_ndev; i++) {
        int n = snprintf(out + used, (size_t) capacity - used, "%s\t%s\n", network_devs[i].device, network_devs[i].description);
        if (n < 0 || used + (size_t) n >= (size_t) capacity)
            break;
        used += (size_t) n;
        count++;
    }
    return count;
}

/* 1 = the machine has an MCA bus (MCA memory expansion boards instead of ISA ones), 0 = no, -1 = unknown machine. */
__declspec(dllexport) int emu_machine_has_mca(const char *machine_internal_name)
{
    int m = machine_get_machine_from_internal_name(machine_internal_name ? machine_internal_name : "");
    if (m < 0)
        return -1;
    return machine_has_bus(m, MACHINE_BUS_MCA) ? 1 : 0;
}

/* Writes the engine's built-in monitor EDID to a file (the original's "Export…" button). 0 = ok. */
__declspec(dllexport) int emu_export_default_edid(const char *path)
{
    if (!path || !path[0])
        return -1;

    uint8_t *bytes = NULL;
    size_t   size  = ddc_create_default_edid(&bytes);
    if (!bytes || size == 0)
        return -2;

    FILE *f = fopen(path, "wb");
    if (!f)
        return -3;
    size_t written = fwrite(bytes, 1, size, f);
    fclose(f);
    return written == size ? 0 : -4;
}

/* Real, compile-time facts about the currently loaded 86box_core.dll - not the user's update
   channel/recompiler PREFERENCE (that's just what a future download would target), but what this
   specific build actually is. Backed by the same macros/defines the engine's own about box uses. */
__declspec(dllexport) int emu_get_version_string(char *buf, int len)
{
    if (!buf || len <= 0)
        return -1;
    const char *v = EMU_VERSION_FULL;
    size_t n = strlen(v);
    if ((int)n >= len)
        n = (size_t)(len - 1);
    memcpy(buf, v, n);
    buf[n] = 0;
    return (int)n;
}

__declspec(dllexport) int emu_get_build_number(void)
{
    return EMU_BUILD_NUM;
}

/* 1 = built with BUILD_TYPE=release (a real tagged-release build); 0 = a dev/local/nightly-style
   build - which, honestly, every locally-built winui-mingw DLL currently is, since that preset
   never sets BUILD_TYPE at all. */
__declspec(dllexport) int emu_is_release_build(void)
{
#ifdef RELEASE_BUILD
    return 1;
#else
    return 0;
#endif
}

/* 1 = New Dynamic Recompiler (NDR), 0 = Old Dynamic Recompiler (ODR). Fixed at compile time by the
   NEW_DYNAREC CMake option - never a runtime toggle - so this reflects what's actually running,
   not the user's update-channel recompiler preference. */
__declspec(dllexport) int emu_is_new_dynarec(void)
{
#ifdef USE_NEW_DYNAREC
    return 1;
#else
    return 0;
#endif
}

__declspec(dllexport) int emu_shutdown(void)
{
    EnterCriticalSection(&s_lifecycle_lock);

    if (!InterlockedCompareExchange(&s_is_running, 0, 1)) {
        LeaveCriticalSection(&s_lifecycle_lock);
        return 0;
    }

    log_bridge(1, "Shutting down 86Box core...");
    InterlockedExchange(&s_stop_requested, 1);
    cpu_thread_run = 0;
    is_quit = 1;

    if (s_cpu_thread) {
        /* pc_close() below releases disk image / NVR file handles the CPU thread owns; if the
           thread hasn't actually exited yet (still cleaning up under load, or a slow I/O), running
           that concurrently with it is a race that can leave a handle open just long enough for a
           delete right after shutdown to hit a sharing violation. Give it real extra time rather
           than silently proceeding once the first wait times out. */
        DWORD wait_result = WaitForSingleObject(s_cpu_thread, 8000);
        if (wait_result == WAIT_TIMEOUT) {
            log_bridge(0, "CPU thread did not exit within 8s of shutdown request; waiting longer before releasing its resources");
            wait_result = WaitForSingleObject(s_cpu_thread, 15000);
            if (wait_result == WAIT_TIMEOUT)
                log_bridge(0, "CPU thread still has not exited after 23s total; proceeding with cleanup anyway, file handles may still be held");
        }
        CloseHandle(s_cpu_thread);
        s_cpu_thread = NULL;
    }

    if (s_telemetry_thread) {
        WaitForSingleObject(s_telemetry_thread, 3000);
        CloseHandle(s_telemetry_thread);
        s_telemetry_thread = NULL;
    }

    pc_close(NULL);
    endblit();

    if (stdlog != NULL && stdlog != stdout && stdlog != stderr) {
        fclose(stdlog);
        stdlog = NULL;
    }

    cleanup_d3d_resources();

    InterlockedExchange(&s_is_initialized, 0);
    LeaveCriticalSection(&s_lifecycle_lock);
    log_bridge(1, "86Box core shutdown complete.");
    return 0;
}

__declspec(dllexport) int emu_is_running(void)
{
    return InterlockedCompareExchange(&s_is_running, 0, 0);
}

__declspec(dllexport) int emu_is_paused(void)
{
    return InterlockedCompareExchange(&s_is_paused, 0, 0);
}

__declspec(dllexport) int emu_set_dxgi_swapchain(void *swapchain)
{
    EnterCriticalSection(&s_render_lock);

    if (!swapchain) {
        cleanup_d3d_resources();
        LeaveCriticalSection(&s_render_lock);
        return 0;
    }

    if (s_swapchain != (IDXGISwapChain1 *)swapchain) {
        IDXGISwapChain_AddRef((IDXGISwapChain1 *)swapchain);
        if (s_swapchain)
            IDXGISwapChain_Release(s_swapchain);
        s_swapchain = (IDXGISwapChain1 *)swapchain;
    }

    if (s_d3d_context) {
        ID3D11DeviceContext_Release(s_d3d_context);
        s_d3d_context = NULL;
    }
    if (s_d3d_device) {
        ID3D11Device_Release(s_d3d_device);
        s_d3d_device = NULL;
    }

    HRESULT hr = IDXGISwapChain_GetDevice(s_swapchain, &IID_ID3D11Device, (void **)&s_d3d_device);
    if (FAILED(hr) || !s_d3d_device) {
        log_bridge(3, "Failed to obtain ID3D11Device from SwapChain: 0x%08X", hr);
        LeaveCriticalSection(&s_render_lock);
        return 1;
    }

    ID3D11Device_GetImmediateContext(s_d3d_device, &s_d3d_context);
    update_render_target_view();
    log_bridge(1, "Successfully bound DXGI SwapChain render target to 86box_core.");

    LeaveCriticalSection(&s_render_lock);
    return 0;
}

__declspec(dllexport) void emu_resize_viewport(int width, int height)
{
    EnterCriticalSection(&s_render_lock);
    uint32_t new_w = width > 0 ? (uint32_t)width : 640;
    uint32_t new_h = height > 0 ? (uint32_t)height : 480;
    int      changed = (new_w != s_viewport_width) || (new_h != s_viewport_height);
    s_viewport_width  = new_w;
    s_viewport_height = new_h;

    if (s_swapchain && (changed || !s_render_target_view)) {
        if (s_render_target_view) {
            ID3D11RenderTargetView_Release(s_render_target_view);
            s_render_target_view = NULL;
        }
        HRESULT rhr = IDXGISwapChain_ResizeBuffers(s_swapchain, 2, s_viewport_width, s_viewport_height, DXGI_FORMAT_B8G8R8A8_UNORM, 0);
        InterlockedExchange(&s_resize_hr, (LONG) rhr);
        update_render_target_view();
    }
    LeaveCriticalSection(&s_render_lock);
}

/* Last frame handed over by the core's blit callback (packed, 0xFFRRGGBB). */

/* Caller must hold s_render_lock. */
/* Destination rectangle within the backbuffer: stretch to fill, the largest whole-number pixel
 * multiple of the guest frame (integer scaling), or the largest 4:3 rectangle (letterboxed). */
static void compute_dest_rect(UINT bb_w, UINT bb_h, UINT *out_dx, UINT *out_dy, UINT *out_dw, UINT *out_dh)
{
    UINT dw = bb_w, dh = bb_h;
    UINT scale_int = 0;
    if (InterlockedCompareExchange(&s_integer_scaling, 0, 0) && s_frame_w > 0 && s_frame_h > 0) {
        UINT sx = bb_w / (UINT) s_frame_w, sy = bb_h / (UINT) s_frame_h;
        scale_int = sx < sy ? sx : sy;
    }
    if (scale_int >= 1) {
        dw = (UINT) s_frame_w * scale_int;
        dh = (UINT) s_frame_h * scale_int;
    } else if (InterlockedCompareExchange(&s_aspect_mode, 0, 0) == 0) {
        if ((unsigned long long) dw * 3 > (unsigned long long) dh * 4)
            dw = (dh * 4) / 3;
        else
            dh = (dw * 3) / 4;
    }
    *out_dw = dw;
    *out_dh = dh;
    *out_dx = (bb_w - dw) / 2;
    *out_dy = (bb_h - dh) / 2;
}

/* Fallback path used when the CRT shader pipeline could not be built (e.g. CrtFilter.*.cso is
 * missing because fxc.exe was not available when 86box_core.dll was built): the previous plain
 * nearest-neighbour CPU scale straight into the backbuffer, with no post-process effects. */
static void present_cpu_fallback_locked(ID3D11Texture2D *back, const D3D11_TEXTURE2D_DESC *bd)
{
    size_t need = (size_t) bd->Width * bd->Height;
    if (need > s_scaled_cap) {
        uint32_t *nb = (uint32_t *) realloc(s_scaled, need * sizeof(uint32_t));
        if (nb) { s_scaled = nb; s_scaled_cap = need; }
    }
    if (!s_scaled || need > s_scaled_cap || bd->Width == 0 || bd->Height == 0)
        return;

    UINT dx, dy, dw, dh;
    compute_dest_rect(bd->Width, bd->Height, &dx, &dy, &dw, &dh);

    const uint32_t bg = 0xFF0A0A0Bu;
    for (size_t i = 0; i < need; i++)
        s_scaled[i] = bg;

    for (UINT y = 0; y < dh; y++) {
        const uint32_t *src = s_frame + (size_t) ((unsigned long long) y * s_frame_h / dh) * s_frame_w;
        uint32_t       *dst = s_scaled + (size_t) (dy + y) * bd->Width + dx;
        for (UINT x = 0; x < dw; x++)
            dst[x] = src[(unsigned long long) x * s_frame_w / dw];
    }

    D3D11_BOX box = { 0, 0, 0, bd->Width, bd->Height, 1 };
    ID3D11DeviceContext_UpdateSubresource(s_d3d_context, (ID3D11Resource *) back, 0, &box,
                                          s_scaled, bd->Width * sizeof(uint32_t), 0);
}

/* GPU path: uploads the raw guest frame into s_guest_texture unscaled, then draws it through
 * CrtFilter.hlsl into the backbuffer's destination rectangle - the pixel shader does the
 * scaling (via the viewport transform), curvature, chromatic aberration, scanlines, aperture
 * mask and vignette in one pass. */
static void present_crt_locked(const D3D11_TEXTURE2D_DESC *bd)
{
    if (!ensure_guest_texture((uint32_t) s_frame_w, (uint32_t) s_frame_h) || !s_guest_srv)
        return;

    D3D11_MAPPED_SUBRESOURCE mapped;
    if (FAILED(ID3D11DeviceContext_Map(s_d3d_context, (ID3D11Resource *) s_guest_texture, 0,
                                       D3D11_MAP_WRITE_DISCARD, 0, &mapped)))
        return;
    for (int y = 0; y < s_frame_h; y++) {
        memcpy((uint8_t *) mapped.pData + (size_t) y * mapped.RowPitch,
               s_frame + (size_t) y * s_frame_w, (size_t) s_frame_w * sizeof(uint32_t));
    }
    ID3D11DeviceContext_Unmap(s_d3d_context, (ID3D11Resource *) s_guest_texture, 0);

    UINT dx, dy, dw, dh;
    compute_dest_rect(bd->Width, bd->Height, &dx, &dy, &dw, &dh);
    if (dw == 0 || dh == 0)
        return;

    crt_params_t params;
    params.guest_w   = (float) s_frame_w;
    params.guest_h   = (float) s_frame_h;
    params.output_w  = (float) dw;
    params.output_h  = (float) dh;
    params.curvature = InterlockedCompareExchange(&s_crt_curvature_x1000, 0, 0) / 1000.0f;
    params.mode      = (float) InterlockedCompareExchange(&s_crt_filter_mode, 0, 0);
    params.bloom     = InterlockedCompareExchange(&s_crt_bloom_x1000, 0, 0) / 1000.0f;
    params.pad0      = 0.0f;

    D3D11_MAPPED_SUBRESOURCE cbmap;
    if (FAILED(ID3D11DeviceContext_Map(s_d3d_context, (ID3D11Resource *) s_crt_cbuffer, 0,
                                       D3D11_MAP_WRITE_DISCARD, 0, &cbmap)))
        return;
    memcpy(cbmap.pData, &params, sizeof(params));
    ID3D11DeviceContext_Unmap(s_d3d_context, (ID3D11Resource *) s_crt_cbuffer, 0);

    D3D11_VIEWPORT vp = { (float) dx, (float) dy, (float) dw, (float) dh, 0.0f, 1.0f };
    ID3D11DeviceContext_RSSetViewports(s_d3d_context, 1, &vp);
    ID3D11DeviceContext_RSSetState(s_d3d_context, s_crt_rasterizer);
    ID3D11DeviceContext_OMSetRenderTargets(s_d3d_context, 1, &s_render_target_view, NULL);
    ID3D11DeviceContext_OMSetBlendState(s_d3d_context, NULL, NULL, 0xFFFFFFFFu);
    ID3D11DeviceContext_IASetPrimitiveTopology(s_d3d_context, D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    ID3D11DeviceContext_IASetInputLayout(s_d3d_context, NULL);
    ID3D11DeviceContext_VSSetShader(s_d3d_context, s_crt_vs, NULL, 0);
    ID3D11DeviceContext_PSSetShader(s_d3d_context, s_crt_ps, NULL, 0);
    ID3D11DeviceContext_PSSetShaderResources(s_d3d_context, 0, 1, &s_guest_srv);
    ID3D11DeviceContext_PSSetSamplers(s_d3d_context, 0, 1, &s_crt_sampler);
    ID3D11DeviceContext_PSSetConstantBuffers(s_d3d_context, 0, 1, &s_crt_cbuffer);
    ID3D11DeviceContext_Draw(s_d3d_context, 3, 0);

    ID3D11ShaderResourceView *null_srv = NULL;
    ID3D11DeviceContext_PSSetShaderResources(s_d3d_context, 0, 1, &null_srv);
}

/* Caller must hold s_render_lock. */
static void present_locked(void)
{
    if (!s_swapchain || !s_d3d_context || !s_render_target_view) {
        InterlockedIncrement(&s_update_skipped);
        return;
    }

    const float clear_color[4] = { 0.04f, 0.04f, 0.05f, 1.0f };
    ID3D11DeviceContext_ClearRenderTargetView(s_d3d_context, s_render_target_view, clear_color);

    if (s_frame && s_frame_w > 0 && s_frame_h > 0) {
        ID3D11Texture2D *back = NULL;
        if (SUCCEEDED(IDXGISwapChain_GetBuffer(s_swapchain, 0, &IID_ID3D11Texture2D, (void **) &back)) && back) {
            D3D11_TEXTURE2D_DESC bd;
            ID3D11Texture2D_GetDesc(back, &bd);

            if (ensure_crt_pipeline())
                present_crt_locked(&bd);
            else
                present_cpu_fallback_locked(back, &bd);

            ID3D11Texture2D_Release(back);
        }
    }

    HRESULT phr = IDXGISwapChain_Present(s_swapchain, 0, 0);
    InterlockedExchange(&s_present_hr, (LONG) phr);
    if (SUCCEEDED(phr))
        InterlockedIncrement(&s_present_ok);
}

/* Frame callback registered with the core via video_setblit(). Runs on the
 * core's blit thread; must copy the region out of the monitor's bitmap and
 * then call video_blit_complete_monitor(). */
static void emu_blit(int x, int y, int w, int h, int monitor_index)
{
    bitmap_t *bmp = NULL;

    if (monitor_index >= 0 && monitor_index < MONITORS_NUM)
        bmp = monitors[monitor_index].target_buffer;

    if (monitor_index == 0 && bmp && x >= 0 && y >= 0 && w > 0 && h > 0
        && x + w <= 2048 && y + h <= 2048) {
        EnterCriticalSection(&s_render_lock);

        size_t need = (size_t) w * (size_t) h;
        if (need > s_frame_cap) {
            uint32_t *nb = (uint32_t *) realloc(s_frame, need * sizeof(uint32_t));
            if (nb) {
                s_frame     = nb;
                s_frame_cap = need;
            }
        }

        if (s_frame && need <= s_frame_cap) {
            for (int row = 0; row < h; ++row) {
                const uint32_t *src = bmp->line[y + row] ? &bmp->line[y + row][x] : NULL;
                uint32_t       *dst = s_frame + (size_t) row * w;
                if (!src) {
                    memset(dst, 0, (size_t) w * sizeof(uint32_t));
                    continue;
                }
                for (int col = 0; col < w; ++col)
                    dst[col] = src[col] | 0xFF000000u;
            }
            s_frame_w = w;
            s_frame_h = h;
            /* Screenshot requested: write the frame just copied to <vm>/screenshots/Monitor_1_*.png. */
            if (atomic_load(&monitors[0].mon_screenshots_raw) > 0)
                video_screenshot_monitor(s_frame, 0, 0, w, 0);

            InterlockedIncrement(&s_blit_count);
            for (size_t i = 0; i < need; i += 97) {
                if (s_frame[i] & 0x00FFFFFFu) {
                    InterlockedIncrement(&s_blit_nonzero);
                    break;
                }
            }
            present_locked();
        }

        LeaveCriticalSection(&s_render_lock);
    }

    video_blit_complete_monitor(monitor_index);
}

__declspec(dllexport) void emu_get_frame_stats(int *blits, int *nonzero_blits, int *w, int *h)
{
    if (blits) *blits = (int) s_blit_count;
    if (nonzero_blits) *nonzero_blits = (int) s_blit_nonzero;
    if (w) *w = s_frame_w;
    if (h) *h = s_frame_h;
}

__declspec(dllexport) void emu_get_render_debug(int *present_ok, int *present_hr, int *resize_hr, int *skipped)
{
    if (present_ok) *present_ok = (int) s_present_ok;
    if (present_hr) *present_hr = (int) s_present_hr;
    if (resize_hr) *resize_hr = (int) s_resize_hr;
    if (skipped) *skipped = (int) s_update_skipped;
}

__declspec(dllexport) void emu_render_frame(void)
{
    EnterCriticalSection(&s_render_lock);
    present_locked();
    LeaveCriticalSection(&s_render_lock);
}

__declspec(dllexport) void emu_send_input_keyboard(uint32_t scancode, int is_down)
{
    keyboard_input(is_down, (uint16_t)scancode);
}

__declspec(dllexport) void emu_send_input_mouse(int dx, int dy, uint32_t buttons)
{
    mouse_scale(dx, dy);
    mouse_set_buttons_ex((int)buttons);
}

__declspec(dllexport) void emu_set_mouse_capture(int capture)
{
    mouse_capture = capture ? 1 : 0;
    plat_mouse_capture(mouse_capture);
}

__declspec(dllexport) int emu_get_mouse_capture(void)
{
    return mouse_capture ? 1 : 0;
}

__declspec(dllexport) void emu_set_telemetry_callback(
    emu_telemetry_callback_t callback,
    void *user_context
) {
    s_telemetry_cb   = callback;
    s_telemetry_user = user_context;
}

__declspec(dllexport) void emu_set_log_callback(
    emu_log_callback_t callback,
    void *user_context
) {
    s_log_cb   = callback;
    s_log_user = user_context;
}

__declspec(dllexport) void emu_set_message_callback(
    emu_message_callback_t callback,
    void *user_context
) {
    s_message_cb   = callback;
    s_message_user = user_context;
}

/* ========================================================================= */
/* Platform & UI Hook Implementations (Resolves all core symbols)            */
/* ========================================================================= */

void plat_mouse_capture(int on)
{
    mouse_capture = on;
}

void plat_pause(int p)
{
    dopause = p;
    if (!p)
        ack_pause();
}

void plat_resize(int x, int y, int monitor_index)
{
    (void)x; (void)y; (void)monitor_index;
}

void plat_resize_request(int w, int h, int monitor_index)
{
    (void)w; (void)h; (void)monitor_index;
}

void ui_emu_status(int speed_percent)
{
    (void)speed_percent;
}

void ui_hard_reset_completed(void)
{
    log_bridge(1, "Emulated machine reset complete.");
}

void ui_init_monitor(int monitor_index)
{
    (void)monitor_index;
}

void ui_deinit_monitor(int monitor_index)
{
    (void)monitor_index;
}

void ui_sb_set_ready(int ready)
{
    (void)ready;
}

void ui_sb_update_panes(void) {}
void ui_sb_update_text(void) {}
void ui_sb_update_tip(int meaning) { (void)meaning; }
/* Drive/NIC activity, exactly as the Qt frontend records it. The telemetry thread samples these flags; a latch
 * remembers pulses shorter than the sampling interval so a quick seek still lights the indicator. */

void ui_sb_update_icon(int tag, int active)
{
    const unsigned int t        = (unsigned int) tag;
    const int          category = (int) (t & 0xfffffff0u);
    const int          item     = tag & 0xf;
    const bool         on       = active > 0;
    const LONG         bit      = 1L << item;

    switch (category) {
        case SB_FLOPPY:
            if (item < FDD_NUM) { machine_status.fdd[item].active = on; if (on) InterlockedOr(&s_act_latch_fdd, bit); }
            break;
        case SB_CDROM:
            if (item < CDROM_NUM) { machine_status.cdrom[item].active = on; if (on) InterlockedOr(&s_act_latch_cd, bit); }
            break;
        case SB_RDISK:
            if (item < RDISK_NUM) machine_status.rdisk[item].active = on;
            break;
        case SB_MO:
            if (item < MO_NUM) machine_status.mo[item].active = on;
            break;
        case SB_HDD:
            if (item < HDD_BUS_USB) { machine_status.hdd[item].active = on; if (on) InterlockedOr(&s_act_latch_hdd, bit); }
            break;
        case SB_NETWORK:
            if (item < NET_CARD_MAX) { machine_status.net[item].active = on; if (on) InterlockedOr(&s_act_latch_net, bit); }
            break;
        default:
            break;
    }
}

void ui_sb_update_icon_write(int tag, int write)
{
    const unsigned int t        = (unsigned int) tag;
    const int          category = (int) (t & 0xfffffff0u);
    const int          item     = tag & 0xf;
    const bool         on       = write > 0;

    switch (category) {
        case SB_FLOPPY: if (item < FDD_NUM) machine_status.fdd[item].write_active = on; break;
        case SB_CDROM:  if (item < CDROM_NUM) machine_status.cdrom[item].write_active = on; break;
        case SB_HDD:    if (item < HDD_BUS_USB) machine_status.hdd[item].write_active = on; break;
        default: break;
    }
}
void ui_sb_update_icon_state(int tag, int state) { (void)tag; (void)state; }
void ui_sb_update_icon_wp(int tag, int state) { (void)tag; (void)state; }
void ui_sb_set_text(char *str) { (void)str; }
void ui_sb_bugui(char *str) { (void)str; }
void ui_sb_mt32lcd(char *str) { (void)str; }
void ui_update_force_interpreter(void) {}

int ui_msgbox(int flags, char *message)
{
    log_bridge(2, "UI MessageBox [0x%02X]: %s", flags, message);
    if (s_message_cb) {
        const char *header = "Notice";
        if (flags & MBX_FATAL)
            header = "Fatal Error";
        else if (flags & MBX_ERROR)
            header = "Error";
        else if (flags & MBX_WARNING)
            header = "Warning";

        s_message_cb(flags, header, message ? message : "", s_message_user);
    }
    return 1;
}

int ui_msgbox_header(int flags, char *header, char *message)
{
    log_bridge(2, "UI MessageBox [%s]: %s", header ? header : "", message ? message : "");
    if (s_message_cb) {
        s_message_cb(flags, header ? header : "Message", message ? message : "", s_message_user);
    }
    return 1;
}

void startblit(void) {}
void endblit(void)
{
}

uint32_t plat_get_ticks(void)
{
    return timeGetTime();
}

uint64_t plat_timer_read(void)
{
    LARGE_INTEGER count;
    QueryPerformanceCounter(&count);
    return (uint64_t)count.QuadPart;
}

void plat_delay_ms(uint32_t count)
{
    Sleep(count);
}

/* plat_set_thread_name is provided by unix/sdl_plat_win.c */

void plat_clean_up(void) {}

int plat_dir_check(char *path)
{
    DWORD attr = GetFileAttributesA(path);
    return (attr != INVALID_FILE_ATTRIBUTES && (attr & FILE_ATTRIBUTE_DIRECTORY));
}

int plat_file_check(const char *path)
{
    DWORD attr = GetFileAttributesA(path);
    return (attr != INVALID_FILE_ATTRIBUTES && !(attr & FILE_ATTRIBUTE_DIRECTORY));
}

int plat_dir_create(char *path)
{
    return CreateDirectoryA(path, NULL) || GetLastError() == ERROR_ALREADY_EXISTS;
}

int plat_getcwd(char *bufp, int max)
{
    return _getcwd(bufp, max) != NULL;
}

FILE *plat_fopen(const char *path, const char *mode)
{
    return fopen(path, mode);
}

FILE *plat_fopen64(const char *path, const char *mode)
{
    return fopen(path, mode);
}

void plat_remove(char *path)
{
    remove(path);
}

/* Path manipulation (ported from src/unix/sdl_plat.c, which pulls in an
 * SDL dependency this bridge does not otherwise need). path_abs and
 * path_normalize are provided by unix/sdl_plat_win.c. */
void path_slash(char *path)
{
    if (path[strlen(path) - 1] != '/') {
        strcat(path, "/");
    }
    path_normalize(path);
}

const char *path_get_slash(char *path)
{
    const char *ret = "";

    if (path[strlen(path) - 1] != '/')
        ret = "/";

    return ret;
}

char *path_get_filename(char *s)
{
    int c = (int) strlen(s) - 1;

    while (c > 0) {
        if (s[c] == '/' || s[c] == '\\')
            return (&s[c + 1]);
        c--;
    }

    return s;
}

char *path_get_extension(char *s)
{
    int c = (int) strlen(s) - 1;

    if (c <= 0)
        return s;

    while (c && s[c] != '.')
        c--;

    if (!c)
        return (&s[strlen(s)]);

    return (&s[c + 1]);
}

void path_append_filename(char *dest, const char *s1, const char *s2)
{
    strcpy(dest, s1);
    path_slash(dest);
    strcat(dest, s2);
}

void path_get_dirname(char *dest, const char *path)
{
    int   c   = (int) strlen(path);
    char *ptr = (char *) path;

    while (c > 0) {
        if (path[c] == '/' || path[c] == '\\') {
            ptr = (char *) &path[c];
            break;
        }
        c--;
    }

    while (path < ptr)
        *dest++ = *path++;
    *dest = '\0';
}

/* plat_mmap/plat_munmap are provided by unix/sdl_plat_win.c */

void plat_get_exe_name(char *s, int size)
{
    GetModuleFileNameA(NULL, s, size);
}

void plat_get_global_config_dir(char *outbuf, size_t len)
{
    strncpy(outbuf, ".", len);
}

/* plat_get_global_data_dir/plat_get_temp_dir are provided by unix/sdl_plat_win.c */

void plat_get_vmm_dir(char *outbuf, size_t len)
{
    strncpy(outbuf, ".", len);
}

/* plat_init_rom_paths/plat_init_asset_paths are provided by unix/sdl_plat_win.c */
void do_start(void) {}
void do_stop(void) {}
void plat_power_off(void) {}

void floppy_mount(uint8_t id, char *fn, uint8_t wp) { (void)id; (void)fn; (void)wp; }
void floppy_eject(uint8_t id) { (void)id; }
void cdrom_mount(uint8_t id, char *fn) { (void)id; (void)fn; }
void plat_cdrom_ui_update(uint8_t id, uint8_t reload) { (void)id; (void)reload; }
void rdisk_eject(uint8_t id) { (void)id; }
void rdisk_mount(uint8_t id, char *fn, uint8_t wp) { (void)id; (void)fn; (void)wp; }
void rdisk_reload(uint8_t id) { (void)id; }
void mo_eject(uint8_t id) { (void)id; }
void mo_mount(uint8_t id, char *fn, uint8_t wp) { (void)id; (void)fn; (void)wp; }
void mo_reload(uint8_t id) { (void)id; }
void tape_eject(uint8_t id) { (void)id; }
void tape_mount(uint8_t id, char *fn, uint8_t wp) { (void)id; (void)fn; (void)wp; }
void tape_reload(uint8_t id) { (void)id; }
void cassette_mount(char *fn, uint8_t wp) { (void)fn; (void)wp; }
void cassette_eject(void) {}
void cartridge_mount(uint8_t id, char *fn, uint8_t wp) { (void)id; (void)fn; (void)wp; }
void cartridge_eject(uint8_t id) { (void)id; }
/* plat_is_block_device/plat_get_block_device_size are provided by unix/sdl_plat_win.c */

char *plat_get_string(int id)
{
    switch (id) {
        case STRING_CDROM_DVD_IN_CD_DRIVE:
            return "The DVD image \"%s\" has been inserted into a drive that does not support DVD media and will be ignored.";
        case STRING_HW_NOT_AVAILABLE_TITLE:
            return "Hardware not available";
        case STRING_HW_NOT_AVAILABLE_MACHINE:
            return "Machine \"%s\" is not available due to missing ROMs in the roms/machines directory. Switching to an available machine.";
        case STRING_HW_NOT_AVAILABLE_VIDEO:
            return "Video card \"%s\" is not available due to missing ROMs in the roms/video directory. Switching to an available video card.";
        case STRING_HW_NOT_AVAILABLE_DEVICE:
            return "Device \"%s\" is not available due to missing ROMs. Ignoring the device.";
        case STRING_NET_ERROR:
            return "Failed to initialize network driver:\n\n%s\n\nThe network configuration will be switched to the null driver.";
        case STRING_PCAP_ERROR_NO_DEVICES:
            return "No PCap devices found. Make sure libpcap is installed and that you are on a libpcap-compatible network connection.";
        case STRING_PCAP_ERROR_INVALID_DEVICE:
            return "Invalid PCap device. Make sure libpcap is installed and that you are on a libpcap-compatible network connection.";
        case STRING_GHOSTSCRIPT_ERROR:
            return "Unable to initialize Ghostscript. libgs is required for automatic conversion of PostScript files to PDF.";
        case STRING_GHOSTPCL_ERROR:
            return "Unable to initialize GhostPCL. libgpcl6 is required for automatic conversion of PCL files to PDF.";
        case STRING_ESCP_ERROR:
            return "Unable to find Dot-Matrix fonts. TrueType fonts in the \"roms/printer/fonts\" directory are required for the emulation of the Generic ESC/P 2 Dot-Matrix Printer.";
        case STRING_EDID_READ_ERROR:
            return "EDID file \"%s\" is invalid.";
        case STRING_EDID_TOO_LARGE:
            return "EDID file \"%s\" is too large.";
        case STRING_CDROM_OPEN_ISO_ERROR:
            return "Unable to open image or folder \"%s\".";
        case STRING_CDROM_OPEN_CUE_ERROR:
            return "Unable to open cue sheet \"%s\".";
        case STRING_CDROM_OPEN_MDS_ERROR:
            return "Unable to open MDS file \"%s\".";
        case STRING_CDROM_LOAD_IMAGE_ERROR:
            return "Unable to load CD-ROM image \"%s\".";
        case STRING_CDROM_LOAD_MDSX_ERROR:
            return "Unable to load image \"%s\": mdsx library is missing.";
        case STRING_CDROM_LOAD_AARU_ERROR:
            return "Unable to load image \"%s\": libaaruformat is missing.";
        case STRING_CHARDEV_CONNECT_ERROR:
            return "%s: Could not connect to %s: %s";
        case STRING_CHARDEV_CREATE_ERROR:
            return "%s: Could not create %s: %s";
        case STRING_CHARDEV_ATTACHED:
            return "%s: Attached to %s";
        case STRING_CHARDEV_VCON_IN_USE:
            return "%s: Virtual console already in use by %s";
        case STRING_CHARDEV_TERMINAL_ERROR:
            return "%s: Could not create terminal: %s";
        default:
            return "";
    }
}
int plat_language_code(char *langcode) { (void)langcode; return 0; }
void plat_language_code_r(int id, char *outbuf, int len) { (void)id; (void)outbuf; (void)len; }
void plat_get_cpu_string(char *outbuf, uint8_t len) { (void)outbuf; (void)len; }
/* plat_get_system_directory/plat_run_command/mbstoc16s/c16stombs/
 * plat_lock_volumes/plat_unlock_volumes are provided by unix/sdl_plat_win.c */
void plat_break(void) { DebugBreak(); }

int plat_vidapi(const char *name) { (void)name; return 0; }
char *plat_vidapi_name(int api) { (void)api; return "Direct3D 11"; }

/* DllMain for 86box_core.dll */
BOOL WINAPI DllMain(HINSTANCE hinstDLL, DWORD fdwReason, LPVOID lpvReserved)
{
    (void)lpvReserved;

    switch (fdwReason) {
        case DLL_PROCESS_ATTACH:
            s_dll_module = (HMODULE) hinstDLL;
            DisableThreadLibraryCalls(hinstDLL);
            /* Must happen here, not lazily in emu_initialize(): the OS
             * guarantees DllMain finishes before any exported function can
             * be called, so this is the only place that is race-free. */
            InitializeCriticalSection(&s_render_lock);
            InitializeCriticalSection(&s_lifecycle_lock);
            break;
        case DLL_PROCESS_DETACH:
            cleanup_d3d_resources();
            DeleteCriticalSection(&s_render_lock);
            DeleteCriticalSection(&s_lifecycle_lock);
            break;
    }
    return TRUE;
}
