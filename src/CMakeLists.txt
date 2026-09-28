#
# 86Box    A hypervisor and IBM PC system emulator that specializes in
#          running old operating systems and software designed for IBM
#          PC systems and compatibles from 1981 through fairly recent
#          system designs based on the PCI bus.
#
#          This file is part of the 86Box distribution.
#
#          CMake build script.
#
# Authors: David Hrdlička, <hrdlickadavid@outlook.com>
#          dob205
#
#          Copyright 2020-2022 David Hrdlička.
#          Copyright 2021      dob205.
#          Copyright 2024-2026 Jasmine Iwanek.
#

if(NOT CMAKE_TOP_LEVEL_PROCESSED)
    message(FATAL_ERROR "Incorrect source directory specified. Delete your build directory and retry with the top-level directory instead")
endif()

if(APPLE)
    set(CMAKE_INSTALL_RPATH_USE_LINK_PATH TRUE)
endif()

add_library(86box_core SHARED
    86box.c
    config.c
    timer.c
    io.c
    acpi.c
    apm.c
    dma.c
    ddma.c
    nmi.c
    pci_dummy.c
    pic.c
    pit.c
    pit_exact.c
    pit_fast.c
    port_6x.c
    port_92.c
    ppi.c
    pci.c
    mca.c
    usb.c
    device.c
    nvr.c
    nvr_at.c
    nvr_ps2.c
    machine_status.c
    EmuBridge.c
)

if(WIN32)
    # Windows platform-abstraction layer (threading primitives, path/file
    # utilities, dynamic library loading, sockets, CD-ROM ioctl access).
    # These are plain OS glue with no Qt/SDL windowing dependency, so they
    # are reused as-is by the WinUI frontend via EmuBridge.
    target_sources(86box_core PRIVATE
        qt/win_thread.c
        qt/win_dynld.c
        qt/win_netsocket.c
        qt/win_cdrom_ioctl.c
        unix/sdl_plat_win.c
        unix/dummy_floppy_ioctl.c
    )
    target_include_directories(86box_core PRIVATE unix)

    if (WINUI)
        # Stage 86box_core.dll and its runtime DLL dependencies where the
        # WinUI csproj picks them up automatically (see the
        # runtimes\win-x64\native item group in Box86.WinUI.csproj).
        # MinGW's own runtime DLLs (libgcc/libstdc++/libwinpthread) are
        # found next to the C compiler; everything else comes from the
        # vcpkg triplet's bin dir for the current config.
        get_filename_component(MINGW_BIN_DIR "${CMAKE_C_COMPILER}" DIRECTORY)
        set(WINUI_NATIVE_DIR "${CMAKE_SOURCE_DIR}/winui/runtimes/win-x64/native")

        add_custom_command(TARGET 86box_core POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E make_directory "${WINUI_NATIVE_DIR}"
            COMMAND ${CMAKE_COMMAND} -E copy_if_different
                "$<TARGET_FILE:86box_core>"
                "${WINUI_NATIVE_DIR}/86box_core.dll"
            COMMAND ${CMAKE_COMMAND} -E copy_if_different
                "${MINGW_BIN_DIR}/libgcc_s_seh-1.dll"
                "${MINGW_BIN_DIR}/libstdc++-6.dll"
                "${MINGW_BIN_DIR}/libwinpthread-1.dll"
                "${WINUI_NATIVE_DIR}/"
            COMMENT "Staging 86box_core.dll for the WinUI frontend"
            VERBATIM
        )

        if (VCPKG_TOOLCHAIN)
            add_custom_command(TARGET 86box_core POST_BUILD
                COMMAND ${CMAKE_COMMAND}
                    "-DVCPKG_BIN_DIR=${CMAKE_BINARY_DIR}/vcpkg_installed/${VCPKG_TARGET_TRIPLET}/$<$<CONFIG:Debug>:debug/>bin"
                    "-DDEST_DIR=${WINUI_NATIVE_DIR}"
                    -P "${CMAKE_SOURCE_DIR}/cmake/CopyWinUIRuntimeDlls.cmake"
                COMMENT "Copying vcpkg runtime DLLs for the WinUI frontend"
                VERBATIM
            )
        endif()

        # CRT post-process shader (Shaders/CrtFilter.hlsl -> two precompiled .cso stages),
        # loaded at runtime by EmuBridge.c's ensure_crt_pipeline() next to 86box_core.dll.
        # fxc.exe ships with the Windows SDK, not with MinGW, so it is found separately;
        # its absence only disables the shader (EmuBridge falls back to a plain CPU blit),
        # it does not fail the rest of the build.
        file(GLOB CRT_FXC_SDK_DIRS
            "C:/Program Files (x86)/Windows Kits/10/bin/*/x64"
            "C:/Program Files/Windows Kits/10/bin/*/x64"
        )
        find_program(FXC_EXECUTABLE
            NAMES fxc.exe fxc
            HINTS
                "$ENV{WindowsSdkVerBinPath}x64"
                "$ENV{WindowsSdkDir}bin/x64"
                ${CRT_FXC_SDK_DIRS}
        )

        if (FXC_EXECUTABLE)
            set(CRT_SHADER_SRC "${CMAKE_SOURCE_DIR}/Shaders/CrtFilter.hlsl")
            set(CRT_SHADER_DIR "${WINUI_NATIVE_DIR}/Shaders")
            set(CRT_SHADER_VS  "${CRT_SHADER_DIR}/CrtFilter.vs.cso")
            set(CRT_SHADER_PS  "${CRT_SHADER_DIR}/CrtFilter.ps.cso")

            add_custom_command(
                OUTPUT "${CRT_SHADER_VS}" "${CRT_SHADER_PS}"
                COMMAND ${CMAKE_COMMAND} -E make_directory "${CRT_SHADER_DIR}"
                COMMAND "${FXC_EXECUTABLE}" /nologo /T vs_5_0 /E VSMain /Fo "${CRT_SHADER_VS}" "${CRT_SHADER_SRC}"
                COMMAND "${FXC_EXECUTABLE}" /nologo /T ps_5_0 /E PSMain /Fo "${CRT_SHADER_PS}" "${CRT_SHADER_SRC}"
                DEPENDS "${CRT_SHADER_SRC}"
                COMMENT "Compiling CrtFilter.hlsl (fxc) for the WinUI CRT post-process pass"
                VERBATIM
            )
            add_custom_target(86box_core_crt_shader DEPENDS "${CRT_SHADER_VS}" "${CRT_SHADER_PS}")
            add_dependencies(86box_core 86box_core_crt_shader)
        else()
            message(WARNING "fxc.exe (Windows SDK) not found: the CRT post-process shader will not be built; "
                             "the WinUI viewport falls back to a plain, unfiltered blit.")
        endif()
    endif()
endif()

set_target_properties(86box_core PROPERTIES OUTPUT_NAME "86box_core" PREFIX "")
target_compile_definitions(86box_core PRIVATE EMUBRIDGE_EXPORTS)

if(CMAKE_SYSTEM_NAME MATCHES "Linux")
    add_compile_definitions(_FILE_OFFSET_BITS=64 _LARGEFILE_SOURCE=1 _LARGEFILE64_SOURCE=1)
endif()

if(CPPTHREADS)
    target_sources(86box_core PRIVATE thread.cpp)
endif()

if(GDBSTUB)
    add_compile_definitions(USE_GDBSTUB)
    target_sources(86box_core PRIVATE gdbstub.c)
endif()

if(NEW_DYNAREC)
    add_compile_definitions(USE_NEW_DYNAREC)
endif()

if(RELEASE)
    add_compile_definitions(RELEASE_BUILD)
endif()

if(DYNAREC)
    add_compile_definitions(USE_DYNAREC)
endif()

if(DISCORD)
    add_compile_definitions(DISCORD)
    target_sources(86box_core PRIVATE discord.c)
endif()

if(DEBUGREGS486)
    add_compile_definitions(USE_DEBUG_REGS_486)
endif()

if(SCREENSHOT_MODE)
    add_compile_definitions(SCREENSHOT_MODE)
endif()

if(VNC)
    find_package(LibVNCServer)
    if(LibVNCServer_FOUND)
        add_compile_definitions(USE_VNC)
        add_library(vnc OBJECT
            vnc.c
            vnc_keymap.c
        )
        target_link_libraries(86box_core vnc LibVNCServer::vncserver)
        if(WIN32)
            target_link_libraries(86box_core ws2_32)
        endif()
    endif()
endif()

if(CMAKE_SYSTEM_NAME MATCHES "Linux")
    if(VFIO)
        include(CheckIncludeFile)
        check_include_file("linux/vfio.h" VFIO_AVAILABLE)
        if(VFIO_AVAILABLE)
            add_compile_definitions(USE_VFIO)
        endif()
    endif()
endif()

if(INSTRUMENT)
    add_compile_definitions(USE_INSTRUMENT)
endif()

target_link_libraries(86box_core
    cpu
    char
    chipset
    mch
    dev
    mem
    fdd
    game
    cdrom
    rdisk
    mo
    hdd
    net
    print
    scsi
    sio
    snd
    mdsx_dll
    utils
    vid
    voodoo
)

if(WIN32)
    target_link_libraries(86box_core d3d11 dxgi winmm ws2_32)
endif()

if(HAIKU)
    target_link_libraries(86box_core be)
endif()

if(STATIC_BUILD)
    if(MINGW OR UNIX)
        target_link_options(86box_core PRIVATE "-static")
        set(CMAKE_FIND_LIBRARY_SUFFIXES ".a")
    endif()

    if(MINGW AND NOT $ENV{MSYSTEM} MATCHES "MINGW")
        target_link_libraries(86box_core ucrt)
    endif()
endif()

if(APPLE)
    # Force using the newest library if it's installed by homebrew
    set(CMAKE_FIND_FRAMEWORK LAST)

    # setting our compilation target to macOS 10.15 Catalina if targeting Qt6,
    # macOS 10.14 Mojave for vulkan support, 10.13 High Sierra otherwise
    if (USE_QT6)
        set(CMAKE_OSX_DEPLOYMENT_TARGET "10.15")
    else()
        if(MOLTENVK)
            set(CMAKE_OSX_DEPLOYMENT_TARGET "10.14")
        else()
            set(CMAKE_OSX_DEPLOYMENT_TARGET "10.13")
        endif()
    endif()
endif()

find_package(Freetype REQUIRED)
include_directories(${FREETYPE_INCLUDE_DIRS})
if(FREETYPE_INCLUDE_DIR_ft2build)
    include_directories(${FREETYPE_INCLUDE_DIR_ft2build})
endif()
if(APPLE)
    # Freetype is dynamically loaded by the emulator, however, we link it
    # on macOS so it gets copied to the bundle by the installation process
    target_link_libraries(86box_core Freetype::Freetype)
endif()

find_package(PNG REQUIRED)
include_directories(${PNG_INCLUDE_DIRS})
target_link_libraries(86box_core PNG::PNG)

configure_file(include/86box/version.h.in include/86box/version.h @ONLY)
include_directories(${CMAKE_CURRENT_BINARY_DIR}/include)

include_directories(include)
if(NEW_DYNAREC)
    include_directories(cpu codegen_new)
elseif(DYNAREC)
    include_directories(cpu codegen)
else()
    include_directories(cpu)
endif()

add_subdirectory(cdrom)
add_subdirectory(char)
add_subdirectory(chipset)

add_subdirectory(cpu)
if(NEW_DYNAREC)
    add_subdirectory(codegen_new)
elseif(DYNAREC)
    add_subdirectory(codegen)
endif()

if(MINITRACE)
    add_compile_definitions(MTR_ENABLED)
    add_library(minitrace OBJECT minitrace/minitrace.c)
    target_link_libraries(86box_core minitrace)
endif()

if(WIN32 OR (APPLE AND CMAKE_MACOSX_BUNDLE))
    # Copy the binary to the root of the install prefix on Windows and macOS
    install(TARGETS 86box_core DESTINATION ".")
else()
    # On Linux we want to copy the binary to the `bin` folder.
    install(TARGETS 86box_core)
endif()


# Install our dependencies if using vcpkg
if(VCPKG_TOOLCHAIN)
    x_vcpkg_install_local_dependencies(TARGETS 86box_core DESTINATION ".")
endif()


# Install other dependencies
if(WIN32)
    install(CODE "
        include(BundleUtilities)
        get_filename_component(CMAKE_INSTALL_PREFIX_ABSOLUTE \${CMAKE_INSTALL_PREFIX} ABSOLUTE)
        fixup_bundle(\"\${CMAKE_INSTALL_PREFIX_ABSOLUTE}/$<TARGET_FILE_NAME:86box_core>\" \"\" \"\")"
    COMPONENT Runtime)
endif()

# Install the PDB file on Windows builds
if(MSVC)
    # CMake fully supports PDB files on MSVC-compatible compilers
    install(FILES $<TARGET_PDB_FILE:86box_core>
        CONFIGURATIONS Debug RelWithDebInfo
        DESTINATION ".")
elseif(WIN32)
    install(FILES "$<TARGET_FILE_DIR:86box_core>/$<TARGET_FILE_BASE_NAME:86box_core>.pdb"
        CONFIGURATIONS Debug RelWithDebInfo
        DESTINATION "."
        OPTIONAL)
endif()


add_subdirectory(device)
add_subdirectory(disk)
add_subdirectory(floppy)
add_subdirectory(game)
add_subdirectory(machine)
add_subdirectory(mem)
add_subdirectory(network)
add_subdirectory(printer)
add_subdirectory(sio)
add_subdirectory(scsi)
add_subdirectory(sound)
add_subdirectory(utils)
add_subdirectory(video)

if (APPLE)
    add_subdirectory(mac)
endif()

add_subdirectory(osd)

# Note: src/qt/ completely excluded - Qt frontend replaced by WinUI 3 via EmuBridge

if (WIN32 AND WINUI)
    find_program(DOTNET_EXECUTABLE dotnet)
    if (DOTNET_EXECUTABLE)
        add_custom_target(86Box_WinUI ALL
            COMMAND ${DOTNET_EXECUTABLE} build "${CMAKE_SOURCE_DIR}/winui/Box86.WinUI.csproj" -c $<CONFIG>
            WORKING_DIRECTORY "${CMAKE_SOURCE_DIR}/winui"
            COMMENT "Building 86Box WinUI 3 Application"
        )
    endif()
endif()

if(CMAKE_SYSTEM_NAME MATCHES "NetBSD")
    add_custom_command(TARGET 86box_core POST_BUILD COMMAND paxctl ARGS +m $<TARGET_FILE:86box_core> COMMENT "Disable PaX MPROTECT")
endif()
