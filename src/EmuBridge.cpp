/**
 * 86Box - EmuBridge.cpp
 *
 * Implementation of the C-ABI bridge layer between the 86Box engine core and
 * modern host applications (WinUI 3, WPF, C#). Replaces Qt runtime dependencies
 * with a clean Direct3D 11 swapchain renderer and dedicated worker thread loop.
 */

#define EMUBRIDGE_EXPORTS
#include <86box/EmuBridge.h>

#include <atomic>
#include <chrono>
#include <condition_variable>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

#ifdef _WIN32
#  define WIN32_LEAN_AND_MEAN
#  include <windows.h>
#  include <d3d11.h>
#  include <dxgi1_2.h>
#  pragma comment(lib, "d3d11.lib")
#  pragma comment(lib, "dxgi.lib")
#endif

// 86Box Core Includes
extern "C" {
#include <86box/86box.h>
#include <86box/plat.h>
#include <86box/ui.h>
#include <86box/video.h>
#include <86box/keyboard.h>
#include <86box/mouse.h>
#include <86box/machine_status.h>
#include <86box/nvr.h>
#include <86box/timer.h>

// Core variables defined in 86box.c / video.c
extern int dopause;
extern int is_quit;
extern int cpu_thread_run;
extern volatile int cpu_thread_running;
extern int hard_reset_pending;
extern int mouse_capture;
extern int fast_forward;
extern int force_10ms;
extern int framecount;
extern int framecountx;
extern int fps;
extern uint32_t fps_sample_elapsed_ms;
extern int title_update;
extern int nvr_dosave;

void ack_pause(void);
void pc_reset_hard_init(void);
void pc_reset_hard_close(void);
void pc_run(void);
void pc_close(void *ptr);
void pc_onesec(void);
}

namespace {

// ============================================================================
// Bridge State & Thread Management
// ============================================================================

std::atomic<bool> s_isInitialized{false};
std::atomic<bool> s_isRunning{false};
std::atomic<bool> s_isPaused{false};
std::atomic<bool> s_stopRequested{false};

std::unique_ptr<std::thread> s_cpuThread;
std::unique_ptr<std::thread> s_telemetryThread;
std::mutex                   s_lifecycleMutex;
std::mutex                   s_renderMutex;

EmuTelemetryCallback s_telemetryCallback = nullptr;
void*                s_telemetryUserData = nullptr;

EmuLogCallback       s_logCallback       = nullptr;
void*                s_logUserData       = nullptr;

std::string          s_vmName;
std::string          s_configPath;
std::string          s_statusString;

// Telemetry state
std::atomic<double>   s_speedPercentage{100.0};
std::atomic<uint32_t> s_fps{0};

// ============================================================================
// Direct3D 11 Render Target State
// ============================================================================

#ifdef _WIN32
ID3D11Device*           s_d3dDevice        = nullptr;
ID3D11DeviceContext*    s_d3dContext       = nullptr;
IDXGISwapChain1*        s_swapChain        = nullptr;
ID3D11RenderTargetView* s_renderTargetView = nullptr;
ID3D11Texture2D*        s_guestTexture     = nullptr;
HWND                    s_targetHwnd       = nullptr;
bool                    s_isSwapChainHost  = false;
uint32_t                s_viewportWidth    = 640;
uint32_t                s_viewportHeight   = 480;
#endif

void LogMessage(int level, const char* format, ...) {
    char buffer[1024];
    va_list args;
    va_start(args, format);
    vsnprintf(buffer, sizeof(buffer), format, args);
    va_end(args);

    if (s_logCallback) {
        s_logCallback(level, buffer, s_logUserData);
    } else {
        printf("[86Box Bridge] %s\n", buffer);
    }
}

// ============================================================================
// Direct3D 11 Texture Blit Helpers
// ============================================================================

#ifdef _WIN32
void CleanupD3DResources() {
    std::lock_guard<std::mutex> lock(s_renderMutex);
    if (s_guestTexture)     { s_guestTexture->Release(); s_guestTexture = nullptr; }
    if (s_renderTargetView) { s_renderTargetView->Release(); s_renderTargetView = nullptr; }
    if (s_d3dContext)       { s_d3dContext->Release(); s_d3dContext = nullptr; }
    if (s_d3dDevice)        { s_d3dDevice->Release(); s_d3dDevice = nullptr; }
    s_swapChain = nullptr;
    s_targetHwnd = nullptr;
}

bool EnsureGuestTexture(uint32_t width, uint32_t height) {
    if (!s_d3dDevice) return false;

    if (s_guestTexture) {
        D3D11_TEXTURE2D_DESC desc;
        s_guestTexture->GetDesc(&desc);
        if (desc.Width == width && desc.Height == height) {
            return true;
        }
        s_guestTexture->Release();
        s_guestTexture = nullptr;
    }

    D3D11_TEXTURE2D_DESC desc = {};
    desc.Width = width;
    desc.Height = height;
    desc.MipLevels = 1;
    desc.ArraySize = 1;
    desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    desc.SampleDesc.Count = 1;
    desc.Usage = D3D11_USAGE_DYNAMIC;
    desc.BindFlags = D3D11_BIND_SHADER_RESOURCE;
    desc.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;

    HRESULT hr = s_d3dDevice->CreateTexture2D(&desc, nullptr, &s_guestTexture);
    return SUCCEEDED(hr);
}

void UpdateRenderTargetView() {
    if (!s_swapChain || !s_d3dDevice) return;

    if (s_renderTargetView) {
        s_renderTargetView->Release();
        s_renderTargetView = nullptr;
    }

    ID3D11Texture2D* backBuffer = nullptr;
    HRESULT hr = s_swapChain->GetBuffer(0, __uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&backBuffer));
    if (SUCCEEDED(hr) && backBuffer) {
        s_d3dDevice->CreateRenderTargetView(backBuffer, nullptr, &s_renderTargetView);
        backBuffer->Release();
    }
}
#endif

// ============================================================================
// Emulation CPU Loop (Runs on dedicated worker thread)
// ============================================================================

void CPUWorkerThread() {
    LogMessage(1, "Emulation worker thread started.");

#ifdef _WIN32
    SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_HIGHEST);
    timeBeginPeriod(1);
#endif

    const int64_t quantum_ns  = force_10ms ? 10000000LL : 1000000LL;
    const int64_t max_debt_ns = 50000000LL;
    int64_t debt_ns = 0;
    int frames = 0;

    auto last_time = std::chrono::steady_clock::now();

    cpu_thread_running = 1;
    cpu_thread_run = 1;

    while (!s_stopRequested && cpu_thread_run && !is_quit) {
        auto now = std::chrono::steady_clock::now();
        int64_t delta_ns = std::chrono::duration_cast<std::chrono::nanoseconds>(now - last_time).count();
        last_time = now;

        debt_ns += delta_ns;
        if (debt_ns > max_debt_ns) {
            debt_ns = max_debt_ns;
        }

        if (((debt_ns >= quantum_ns) || fast_forward) && !dopause) {
            pc_run();

            if (++frames >= (force_10ms ? 200 : 2000) && nvr_dosave) {
                nvr_save();
                nvr_dosave = 0;
                frames = 0;
            }

            if (!fast_forward && debt_ns >= quantum_ns) {
                debt_ns -= quantum_ns;
            } else {
                debt_ns = 0;
            }
        } else {
            if (hard_reset_pending) {
                hard_reset_pending = 0;
                pc_reset_hard_close();
                pc_reset_hard_init();
            }

            if (dopause) {
                ack_pause();
            }

            // Yield or sleep for 1ms
            std::this_thread::sleep_for(std::chrono::milliseconds(1));
        }
    }

    cpu_thread_running = 0;
#ifdef _WIN32
    timeEndPeriod(1);
#endif
    LogMessage(1, "Emulation worker thread exiting.");
}

// ============================================================================
// Telemetry Pump (100ms interval for smooth UI metrics)
// ============================================================================

void TelemetryWorkerThread() {
    LogMessage(1, "Telemetry thread started.");
    int tick_count = 0;

    while (!s_stopRequested) {
        std::this_thread::sleep_for(std::chrono::milliseconds(100));

        if (++tick_count >= 10) {
            tick_count = 0;
            pc_onesec(); // Updates fps and title_update
        }

        if (s_telemetryCallback) {
            EmuTelemetryData data = {};
            data.speed_percentage = s_speedPercentage.load();
            data.fps = static_cast<uint32_t>(fps);
            data.is_paused = dopause ? 1 : 0;
            data.is_running = s_isRunning.load() ? 1 : 0;
            data.mouse_captured = mouse_capture ? 1 : 0;

            // Disk & media activity bitmasks from machine_status
            data.fdd_active = 0;
            for (size_t i = 0; i < FDD_NUM; ++i) {
                if (machine_status.fdd[i].active) {
                    data.fdd_active |= (1 << i);
                }
            }

            data.hdd_active = 0;
            for (size_t i = 0; i < HDD_BUS_USB; ++i) {
                if (machine_status.hdd[i].active) {
                    data.hdd_active |= (1 << i);
                }
            }

            data.cd_active = 0;
            for (size_t i = 0; i < CDROM_NUM; ++i) {
                if (machine_status.cdrom[i].active) {
                    data.cd_active |= (1 << i);
                }
            }

            // Build status string
            char statusBuf[256];
            snprintf(statusBuf, sizeof(statusBuf), "%s - %d%% (%u FPS)%s",
                     s_vmName.empty() ? "86Box" : s_vmName.c_str(),
                     static_cast<int>(data.speed_percentage),
                     data.fps,
                     data.is_paused ? " [Paused]" : "");
            data.status_string = statusBuf;

            s_telemetryCallback(&data, s_telemetryUserData);
        }
    }

    LogMessage(1, "Telemetry thread exiting.");
}

} // namespace

// ============================================================================
// Exported C-ABI Implementation
// ============================================================================

extern "C" {

EMUBRIDGE_API int EMUBRIDGE_CALL EmuBridge_Initialize(
    const char *config_path,
    const char *vm_name
) {
    std::lock_guard<std::mutex> lock(s_lifecycleMutex);

    if (s_isInitialized.load()) {
        LogMessage(2, "Engine already initialized.");
        return 0;
    }

    s_configPath = config_path ? config_path : "";
    s_vmName = vm_name ? vm_name : "VM";

    LogMessage(1, "Initializing 86Box engine for VM: %s, Path: %s", s_vmName.c_str(), s_configPath.c_str());

    // Build synthesized argc/argv command line for pc_init
    std::vector<std::string> args;
    args.push_back("86Box");
    if (!s_configPath.empty()) {
        args.push_back("-P");
        args.push_back(s_configPath);
    }

    std::vector<char*> argv;
    for (auto& arg : args) {
        argv.push_back(const_cast<char*>(arg.c_str()));
    }
    argv.push_back(nullptr);

    int ret = pc_init(static_cast<int>(args.size()), argv.data());
    if (!ret) {
        LogMessage(3, "pc_init failed.");
        return 1;
    }

    if (!pc_init_roms()) {
        LogMessage(3, "pc_init_roms failed: ROM files not found.");
        return 2;
    }

    pc_init_modules();
    machine_status_init();

    s_isInitialized.store(true);
    return 0;
}

EMUBRIDGE_API int EMUBRIDGE_CALL EmuBridge_Start(void) {
    std::lock_guard<std::mutex> lock(s_lifecycleMutex);

    if (!s_isInitialized.load()) {
        LogMessage(3, "Cannot start: engine not initialized.");
        return 1;
    }

    if (s_isRunning.load()) {
        LogMessage(2, "Engine is already running.");
        return 0;
    }

    s_stopRequested.store(false);
    s_isPaused.store(false);
    dopause = 0;

    pc_reset_hard_init();

    s_isRunning.store(true);
    s_cpuThread = std::make_unique<std::thread>(CPUWorkerThread);
    s_telemetryThread = std::make_unique<std::thread>(TelemetryWorkerThread);

    return 0;
}

EMUBRIDGE_API int EMUBRIDGE_CALL EmuBridge_Pause(int pause) {
    s_isPaused.store(pause != 0);
    plat_pause(pause != 0);
    return 0;
}

EMUBRIDGE_API int EMUBRIDGE_CALL EmuBridge_Reset(int hard_reset) {
    if (hard_reset) {
        hard_reset_pending = 1;
    } else {
        pc_send_cad();
    }
    return 0;
}

EMUBRIDGE_API int EMUBRIDGE_CALL EmuBridge_Shutdown(void) {
    std::lock_guard<std::mutex> lock(s_lifecycleMutex);

    if (!s_isRunning.load()) {
        return 0;
    }

    LogMessage(1, "Shutting down 86Box engine...");
    s_stopRequested.store(true);
    cpu_thread_run = 0;
    is_quit = 1;

    if (s_cpuThread && s_cpuThread->joinable()) {
        s_cpuThread->join();
        s_cpuThread.reset();
    }

    if (s_telemetryThread && s_telemetryThread->joinable()) {
        s_telemetryThread->join();
        s_telemetryThread.reset();
    }

    pc_close(nullptr);
    endblit();

#ifdef _WIN32
    CleanupD3DResources();
#endif

    s_isRunning.store(false);
    s_isInitialized.store(false);
    LogMessage(1, "86Box engine shutdown complete.");
    return 0;
}

EMUBRIDGE_API int EMUBRIDGE_CALL EmuBridge_IsRunning(void) {
    return s_isRunning.load() ? 1 : 0;
}

EMUBRIDGE_API int EMUBRIDGE_CALL EmuBridge_IsPaused(void) {
    return s_isPaused.load() ? 1 : 0;
}

EMUBRIDGE_API int EMUBRIDGE_CALL EmuBridge_SetRenderTarget(
    void *target_handle,
    int is_swapchain
) {
#ifdef _WIN32
    std::lock_guard<std::mutex> lock(s_renderMutex);

    if (!target_handle) {
        CleanupD3DResources();
        return 0;
    }

    if (is_swapchain) {
        s_swapChain = reinterpret_cast<IDXGISwapChain1*>(target_handle);
        s_isSwapChainHost = true;

        if (s_d3dDevice) {
            s_d3dDevice->Release();
            s_d3dDevice = nullptr;
        }
        if (s_d3dContext) {
            s_d3dContext->Release();
            s_d3dContext = nullptr;
        }

        HRESULT hr = s_swapChain->GetDevice(__uuidof(ID3D11Device), reinterpret_cast<void**>(&s_d3dDevice));
        if (FAILED(hr) || !s_d3dDevice) {
            LogMessage(3, "Failed to retrieve ID3D11Device from IDXGISwapChain1: 0x%08X", hr);
            return 1;
        }

        s_d3dDevice->GetImmediateContext(&s_d3dContext);
        UpdateRenderTargetView();
        LogMessage(1, "Bound WinUI 3 Direct3D 11 SwapChain render target.");
    } else {
        s_targetHwnd = reinterpret_cast<HWND>(target_handle);
        s_isSwapChainHost = false;
        LogMessage(1, "Bound HWND render target: 0x%p", s_targetHwnd);
    }
    return 0;
#else
    return 0;
#endif
}

EMUBRIDGE_API void EMUBRIDGE_CALL EmuBridge_ResizeViewport(
    int width,
    int height
) {
#ifdef _WIN32
    std::lock_guard<std::mutex> lock(s_renderMutex);
    s_viewportWidth = width > 0 ? static_cast<uint32_t>(width) : 640;
    s_viewportHeight = height > 0 ? static_cast<uint32_t>(height) : 480;

    if (s_swapChain && s_isSwapChainHost) {
        if (s_renderTargetView) {
            s_renderTargetView->Release();
            s_renderTargetView = nullptr;
        }
        s_swapChain->ResizeBuffers(2, s_viewportWidth, s_viewportHeight, DXGI_FORMAT_B8G8R8A8_UNORM, 0);
        UpdateRenderTargetView();
    }
#endif
}

EMUBRIDGE_API void EMUBRIDGE_CALL EmuBridge_RenderFrame(void) {
#ifdef _WIN32
    std::lock_guard<std::mutex> lock(s_renderMutex);
    if (!s_swapChain || !s_d3dContext || !s_renderTargetView) return;

    // Blit from 86Box buffer32 to guest texture, then present
    extern uint32_t *buffer32;
    if (buffer32) {
        // Ensure guest texture matches current display dimensions
        EnsureGuestTexture(SCREEN_RES_X, SCREEN_RES_Y);

        D3D11_MAPPED_SUBRESOURCE mapped;
        HRESULT hr = s_d3dContext->Map(s_guestTexture, 0, D3D11_MAP_WRITE_DISCARD, 0, &mapped);
        if (SUCCEEDED(hr)) {
            const uint8_t *src = reinterpret_cast<const uint8_t*>(buffer32);
            uint8_t *dst = reinterpret_cast<uint8_t*>(mapped.pData);
            size_t rowBytes = SCREEN_RES_X * sizeof(uint32_t);

            for (uint32_t y = 0; y < SCREEN_RES_Y; ++y) {
                memcpy(dst + (y * mapped.RowPitch), src + (y * rowBytes), rowBytes);
            }
            s_d3dContext->Unmap(s_guestTexture, 0);
        }
    }

    const float clearColor[4] = { 0.04f, 0.04f, 0.05f, 1.0f };
    s_d3dContext->ClearRenderTargetView(s_renderTargetView, clearColor);
    s_swapChain->Present(1, 0);
#endif
}

EMUBRIDGE_API void EMUBRIDGE_CALL EmuBridge_SendInputKeyboard(
    uint32_t scancode,
    int is_down
) {
    keyboard_input(is_down, static_cast<uint16_t>(scancode));
}

EMUBRIDGE_API void EMUBRIDGE_CALL EmuBridge_SendInputMouse(
    int dx,
    int dy,
    uint32_t buttons
) {
    mouse_scale(dx, dy);
    mouse_set_buttons_ex(static_cast<int>(buttons));
}

EMUBRIDGE_API void EMUBRIDGE_CALL EmuBridge_SetMouseCapture(int capture) {
    mouse_capture = capture ? 1 : 0;
    plat_mouse_capture(mouse_capture);
}

EMUBRIDGE_API int EMUBRIDGE_CALL EmuBridge_GetMouseCapture(void) {
    return mouse_capture ? 1 : 0;
}

EMUBRIDGE_API void EMUBRIDGE_CALL EmuBridge_SetTelemetryCallback(
    EmuTelemetryCallback callback,
    void *user_context
) {
    s_telemetryCallback = callback;
    s_telemetryUserData = user_context;
}

EMUBRIDGE_API void EMUBRIDGE_CALL EmuBridge_SetLogCallback(
    EmuLogCallback callback,
    void *user_context
) {
    s_logCallback = callback;
    s_logUserData = user_context;
}

// ============================================================================
// 86Box UI / Video Callbacks Implementation
// ============================================================================

void ui_emu_status(int speed_percent) {
    s_speedPercentage.store(static_cast<double>(speed_percent));
}

void ui_hard_reset_completed(void) {
    LogMessage(1, "Hard reset completed.");
}

void plat_mouse_capture(int on) {
    mouse_capture = on;
}

void plat_pause(int p) {
    dopause = p;
    if (!p) {
        ack_pause();
    }
}

void plat_resize(int x, int y, int monitor_index) {
    (void)x;
    (void)y;
    (void)monitor_index;
}

void plat_resize_request(int w, int h, int monitor_index) {
    (void)w;
    (void)h;
    (void)monitor_index;
}

void ui_init_monitor(int monitor_index) {
    (void)monitor_index;
}

void ui_deinit_monitor(int monitor_index) {
    (void)monitor_index;
}

void ui_sb_update_icon(int tag, int active) {
    (void)tag;
    (void)active;
}

void ui_sb_update_icon_write(int tag, int write) {
    (void)tag;
    (void)write;
}

void ui_sb_set_text(char *str) {
    if (str) {
        s_statusString = str;
    }
}

int ui_msgbox(int flags, char *message) {
    LogMessage(2, "[UI MsgBox] (flags=%d): %s", flags, message);
    return 1;
}

int ui_msgbox_header(int flags, char *header, char *message) {
    LogMessage(2, "[UI MsgBox] %s: %s", header, message);
    return 1;
}

} // extern "C"
