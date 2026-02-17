cmake_minimum_required(VERSION 3.16)
project(ulbridge LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Fetch dependencies
include(FetchContent)

# utfcpp (header-only UTF conversion library)
FetchContent_Declare(
    utfcpp
    GIT_REPOSITORY https://github.com/nemtrif/utfcpp.git
    GIT_TAG        v4.0.6
)

# libsodium (cryptographic library for secure random generation)
FetchContent_Declare(
    sodium
    GIT_REPOSITORY https://github.com/robinlinden/libsodium-cmake.git
    GIT_TAG        e5b985ad0dd235d8c4307ea3a385b45e76c74c6a  # libsodium 1.0.20
)
set(SODIUM_DISABLE_TESTS ON CACHE BOOL "" FORCE)

# nlohmann/json (header-only JSON library)
FetchContent_Declare(
    json
    GIT_REPOSITORY https://github.com/nlohmann/json.git
    GIT_TAG        v3.11.3
)
set(JSON_BuildTests OFF CACHE BOOL "" FORCE)

FetchContent_MakeAvailable(utfcpp sodium json)

# Detect platform
if(WIN32)
    set(PLATFORM "win")
    set(LIB_PREFIX "")
    set(LIB_SUFFIX ".dll")
elseif(APPLE)
    set(PLATFORM "mac")
    set(LIB_PREFIX "lib")
    set(LIB_SUFFIX ".dylib")
else()
    set(PLATFORM "linux")
    set(LIB_PREFIX "lib")
    set(LIB_SUFFIX ".so")
endif()

# Architecture detection
if(CMAKE_SYSTEM_PROCESSOR MATCHES "arm64|aarch64|ARM64")
    set(ARCH "arm64")
else()
    set(ARCH "x64")
endif()

# Allow overriding architecture (for cross-compilation)
if(DEFINED TARGET_ARCH)
    set(ARCH ${TARGET_ARCH})
endif()

message(STATUS "Building for: ${PLATFORM}-${ARCH}")

# SDK path configuration
set(ULSDK_DIR "${CMAKE_SOURCE_DIR}/sdk/${PLATFORM}-${ARCH}" CACHE PATH "Ultralight SDK directory")

# Verify SDK exists
if(NOT EXISTS "${ULSDK_DIR}/include/Ultralight/Ultralight.h")
    message(FATAL_ERROR 
        "Ultralight SDK not found at: ${ULSDK_DIR}\n"
        "Please run: python3 setup-sdk.py\n"
        "Or manually extract the SDK to: ${ULSDK_DIR}")
endif()

# Find SDK library directory (bin/ on Windows, lib/ on Unix)
if(WIN32)
    set(ULSDK_LIB_DIR "${ULSDK_DIR}/lib")
    set(ULSDK_BIN_DIR "${ULSDK_DIR}/bin")
else()
    if(EXISTS "${ULSDK_DIR}/bin")
        set(ULSDK_LIB_DIR "${ULSDK_DIR}/bin")
    else()
        set(ULSDK_LIB_DIR "${ULSDK_DIR}/lib")
    endif()
    set(ULSDK_BIN_DIR "${ULSDK_LIB_DIR}")
endif()

message(STATUS "SDK include: ${ULSDK_DIR}/include")
message(STATUS "SDK libs: ${ULSDK_LIB_DIR}")

# Create the shared library
add_library(ulbridge SHARED ulbridge.cc)

target_include_directories(ulbridge PRIVATE
    "${ULSDK_DIR}/include"
)

# Link utfcpp (header-only, just adds include path), libsodium, and nlohmann_json
target_link_libraries(ulbridge PRIVATE utf8cpp sodium nlohmann_json::nlohmann_json)

target_link_directories(ulbridge PRIVATE
    "${ULSDK_LIB_DIR}"
)

# Link Ultralight libraries
if(WIN32)
    target_link_libraries(ulbridge PRIVATE
        Ultralight
        UltralightCore
        WebCore
        AppCore
    )
    # Copy DLLs to output directory
    add_custom_command(TARGET ulbridge POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_directory
        "${ULSDK_BIN_DIR}" "$<TARGET_FILE_DIR:ulbridge>"
    )
elseif(APPLE)
    # macOS-specific configuration
    if(FORCE_X64 OR ARCH STREQUAL "x64")
        # Force x86_64 architecture for Grey Hack (runs under Rosetta on Apple Silicon)
        set(CMAKE_OSX_ARCHITECTURES "x86_64" CACHE STRING "" FORCE)
    elseif(ARCH STREQUAL "arm64")
        # Native Apple Silicon build
        set(CMAKE_OSX_ARCHITECTURES "arm64" CACHE STRING "" FORCE)
    endif()
    
    # Ensure we use the correct SDK and standard library
    execute_process(
        COMMAND xcrun --show-sdk-path
        OUTPUT_VARIABLE MACOS_SDK_PATH
        OUTPUT_STRIP_TRAILING_WHITESPACE
    )
    set(CMAKE_OSX_SYSROOT "${MACOS_SDK_PATH}" CACHE PATH "" FORCE)
    
    # Explicitly add C++ standard library include path (needed when cross-compiling arm64->x86_64)
    set(CXX_INCLUDE_PATH "${MACOS_SDK_PATH}/usr/include/c++/v1")
    
    target_include_directories(ulbridge PRIVATE
        "${CXX_INCLUDE_PATH}"
    )
    
    target_compile_options(ulbridge PRIVATE 
        -stdlib=libc++
        -isysroot ${MACOS_SDK_PATH}
    )
    
    target_link_libraries(ulbridge PRIVATE
        "-lUltralight"
        "-lUltralightCore"
        "-lWebCore"
        "-lAppCore"
    )
    
    target_link_options(ulbridge PRIVATE 
        -stdlib=libc++
        -isysroot ${MACOS_SDK_PATH}
    )
    
    # Set rpath for finding libraries at runtime
    set_target_properties(ulbridge PROPERTIES
        BUILD_WITH_INSTALL_RPATH TRUE
        INSTALL_RPATH "@loader_path"
        MACOSX_RPATH TRUE
    )
else()
    # Linux
    target_link_libraries(ulbridge PRIVATE
        Ultralight
        UltralightCore
        WebCore
        AppCore
    )
    set_target_properties(ulbridge PROPERTIES
        BUILD_WITH_INSTALL_RPATH TRUE
        INSTALL_RPATH "$ORIGIN"
    )
endif()

# Output directory - all native files go into GreyHackTerminalUI subfolder
set(OUTPUT_DIR "${CMAKE_SOURCE_DIR}/dist/${PLATFORM}-${ARCH}/GreyHackTerminalUI")
set_target_properties(ulbridge PROPERTIES
    LIBRARY_OUTPUT_DIRECTORY "${OUTPUT_DIR}"
    RUNTIME_OUTPUT_DIRECTORY "${OUTPUT_DIR}"
)

# Copy SDK runtime libraries and resources
if(WIN32)
    # Copy DLLs
    file(GLOB ULSDK_DLLS "${ULSDK_BIN_DIR}/*.dll")
    foreach(DLL ${ULSDK_DLLS})
        add_custom_command(TARGET ulbridge POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E copy_if_different "${DLL}" "${OUTPUT_DIR}/"
        )
    endforeach()
elseif(APPLE)
    # Copy dylibs
    file(GLOB ULSDK_DYLIBS "${ULSDK_LIB_DIR}/*.dylib")
    foreach(DYLIB ${ULSDK_DYLIBS})
        add_custom_command(TARGET ulbridge POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E copy_if_different "${DYLIB}" "${OUTPUT_DIR}/"
        )
    endforeach()
else()
    # Copy .so files
    file(GLOB ULSDK_SOS "${ULSDK_LIB_DIR}/*.so*")
    foreach(SO ${ULSDK_SOS})
        add_custom_command(TARGET ulbridge POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E copy_if_different "${SO}" "${OUTPUT_DIR}/"
        )
    endforeach()
endif()

# Copy resources
if(EXISTS "${ULSDK_DIR}/resources")
    add_custom_command(TARGET ulbridge POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_directory
        "${ULSDK_DIR}/resources" "${OUTPUT_DIR}/resources"
    )
endif()

# Create fonts folder (for custom fonts like DejaVu)
add_custom_command(TARGET ulbridge POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory "${OUTPUT_DIR}/fonts"
)

# Copy fonts if source folder exists
if(EXISTS "${CMAKE_SOURCE_DIR}/fonts")
    add_custom_command(TARGET ulbridge POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_directory
        "${CMAKE_SOURCE_DIR}/fonts" "${OUTPUT_DIR}/fonts"
    )
endif()

# Optional: Debug build with verbose logging
option(ULBRIDGE_DEBUG "Enable debug logging" OFF)
if(ULBRIDGE_DEBUG)
    target_compile_definitions(ulbridge PRIVATE ULBRIDGE_DEBUG)
endif()

# Install target
install(TARGETS ulbridge
    LIBRARY DESTINATION lib
    RUNTIME DESTINATION bin
)

install(DIRECTORY "${ULSDK_DIR}/resources"
    DESTINATION .
    OPTIONAL
)

message(STATUS "")
message(STATUS "Configuration complete!")
message(STATUS "  Build with: cmake --build build --config Release")
message(STATUS "  Output will be in: ${OUTPUT_DIR}")
message(STATUS "")
