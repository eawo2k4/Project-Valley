# Copies every DLL from the current vcpkg triplet's bin directory into the
# WinUI frontend's native-runtime staging folder, so `dotnet build` on
# Box86.WinUI.csproj always has a fresh, matching set of dependency DLLs
# next to 86box_core.dll. Invoked as a POST_BUILD step on the 86box_core
# target (see src/CMakeLists.txt) with -DVCPKG_BIN_DIR=... -DDEST_DIR=...
#
# libfluidsynth's debug build is named libfluidsynth-3_debug.dll on disk,
# but 86box_core.dll's import table asks the loader for libfluidsynth-3.dll
# (its actual DLL name), so that one file is copied under the renamed name
# it will actually be looked up by.

if (NOT EXISTS "${VCPKG_BIN_DIR}")
    return()
endif()

file(MAKE_DIRECTORY "${DEST_DIR}")

file(GLOB VCPKG_RUNTIME_DLLS "${VCPKG_BIN_DIR}/*.dll")
foreach(dll ${VCPKG_RUNTIME_DLLS})
    get_filename_component(dll_name "${dll}" NAME)
    if (dll_name MATCHES "^libfluidsynth-3.*_debug\\.dll$")
        set(dll_name "libfluidsynth-3.dll")
    endif()
    configure_file("${dll}" "${DEST_DIR}/${dll_name}" COPYONLY)
endforeach()
