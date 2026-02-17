#!/bin/bash
#
# Multiplatform build script for ulbridge
#
# Usage:
#   ./build.sh              # Build for current platform
#   ./build.sh mac          # Build for macOS x64
#   ./build.sh mac-arm64    # Build for macOS arm64 (Apple Silicon native)
#   ./build.sh linux        # Build for Linux x64
#   ./build.sh win          # Build for Windows x64 (requires cross-compiler)
#   ./build.sh all          # Build all platforms (x64)
#   ./build.sh --install    # Build and install to Grey Hack
#   ./build.sh --clean      # Clean build directories
#

set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

print_header() {
    echo -e "\n${BLUE}========================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}========================================${NC}\n"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

# Default options
PLATFORMS=""
DO_INSTALL=false
DO_CLEAN=false
BUILD_TYPE="Release"

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        mac|macos)
            PLATFORMS="$PLATFORMS mac-x64"
            shift
            ;;
        mac-arm64|macos-arm64)
            PLATFORMS="$PLATFORMS mac-arm64"
            shift
            ;;
        linux)
            PLATFORMS="$PLATFORMS linux-x64"
            shift
            ;;
        win|windows)
            PLATFORMS="$PLATFORMS win-x64"
            shift
            ;;
        all)
            PLATFORMS="mac-x64 linux-x64 win-x64"
            shift
            ;;
        all-arch)
            PLATFORMS="mac-x64 mac-arm64 linux-x64 win-x64"
            shift
            ;;
        --install|-i)
            DO_INSTALL=true
            shift
            ;;
        --clean|-c)
            DO_CLEAN=true
            shift
            ;;
        --debug|-d)
            BUILD_TYPE="Debug"
            shift
            ;;
        --help|-h)
            echo "Usage: $0 [platform...] [options]"
            echo ""
            echo "Platforms:"
            echo "  mac, macos        Build for macOS x64"
            echo "  mac-arm64         Build for macOS arm64 (Apple Silicon native)"
            echo "  linux             Build for Linux x64"
            echo "  win, windows      Build for Windows x64"
            echo "  all               Build all platforms (x64)"
            echo "  all-arch          Build all platforms including arm64"
            echo ""
            echo "Options:"
            echo "  --install, -i   Install to Grey Hack after building"
            echo "  --clean, -c     Clean build directories"
            echo "  --debug, -d     Build with debug symbols"
            echo "  --help, -h      Show this help"
            echo ""
            echo "Environment variables:"
            echo "  GREY_HACK_PATH  Path to Grey Hack installation"
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Clean if requested
if [ "$DO_CLEAN" = true ]; then
    print_header "Cleaning build directories"
    rm -rf "$SCRIPT_DIR/build"
    rm -rf "$SCRIPT_DIR/dist"
    print_success "Clean complete"
    exit 0
fi

# Detect current platform if none specified
if [ -z "$PLATFORMS" ]; then
    case "$(uname -s)" in
        Darwin*)
            # Detect native architecture
            if [[ "$(uname -m)" == "arm64" ]]; then
                # Apple Silicon: default to arm64 native build
                PLATFORMS="mac-arm64"
            else
                PLATFORMS="mac-x64"
            fi
            ;;
        Linux*)
            PLATFORMS="linux-x64"
            ;;
        MINGW*|MSYS*|CYGWIN*)
            PLATFORMS="win-x64"
            ;;
        *)
            print_error "Unknown platform: $(uname -s)"
            exit 1
            ;;
    esac
fi

# Check for required tools
check_tools() {
    local missing=""
    
    if ! command -v cmake &> /dev/null; then
        missing="$missing cmake"
    fi
    
    if ! command -v python3 &> /dev/null; then
        missing="$missing python3"
    fi
    
    if [ -n "$missing" ]; then
        print_error "Missing required tools:$missing"
        echo "Install with:"
        echo "  macOS: brew install cmake python3"
        echo "  Linux: apt install cmake python3"
        exit 1
    fi
}

# Check SDK setup
check_sdk() {
    local platform=$1
    local sdk_dir="$SCRIPT_DIR/sdk/$platform"
    
    if [ ! -f "$sdk_dir/include/Ultralight/Ultralight.h" ]; then
        print_warning "SDK not found for $platform"
        echo "Running SDK setup..."
        python3 "$SCRIPT_DIR/setup-sdk.py"
        
        if [ ! -f "$sdk_dir/include/Ultralight/Ultralight.h" ]; then
            print_error "SDK setup failed for $platform"
            return 1
        fi
    fi
    
    return 0
}

# Build for a specific platform
build_platform() {
    local platform=$1
    local arch="${platform##*-}"
    local os="${platform%%-*}"
    
    print_header "Building for $platform"
    
    # Check SDK
    if ! check_sdk "$platform"; then
        return 1
    fi
    
    local build_dir="$SCRIPT_DIR/build/$platform"
    local sdk_dir="$SCRIPT_DIR/sdk/$platform"
    
    mkdir -p "$build_dir"
    cd "$build_dir"
    
    # Platform-specific CMake options
    local cmake_opts="-DCMAKE_BUILD_TYPE=$BUILD_TYPE"
    cmake_opts="$cmake_opts -DULSDK_DIR=$sdk_dir"
    
    case "$os-$arch" in
        mac-x64)
            # Force x86_64 for Grey Hack compatibility (Rosetta on Apple Silicon)
            cmake_opts="$cmake_opts -DCMAKE_OSX_ARCHITECTURES=x86_64"
            cmake_opts="$cmake_opts -DFORCE_X64=ON"
            cmake_opts="$cmake_opts -DTARGET_ARCH=x64"
            ;;
        mac-arm64)
            # Native Apple Silicon build
            cmake_opts="$cmake_opts -DCMAKE_OSX_ARCHITECTURES=arm64"
            cmake_opts="$cmake_opts -DTARGET_ARCH=arm64"
            ;;
        linux-*)
            cmake_opts="$cmake_opts -DTARGET_ARCH=$arch"
            ;;
        win-*)
            # Windows cross-compilation (if on non-Windows)
            if [[ "$(uname -s)" != MINGW* ]] && [[ "$(uname -s)" != MSYS* ]]; then
                print_warning "Cross-compiling for Windows requires MinGW or native Windows build"
                cmake_opts="$cmake_opts -DCMAKE_TOOLCHAIN_FILE=$SCRIPT_DIR/cmake/mingw-toolchain.cmake"
            fi
            cmake_opts="$cmake_opts -DTARGET_ARCH=$arch"
            ;;
    esac
    
    echo "CMake options: $cmake_opts"
    
    # Configure
    cmake $cmake_opts "$SCRIPT_DIR"
    
    # Build
    cmake --build . --config "$BUILD_TYPE" -j$(nproc 2>/dev/null || sysctl -n hw.ncpu 2>/dev/null || echo 4)
    
    # Verify output
    local dist_dir="$SCRIPT_DIR/dist/$platform"
    if [ -d "$dist_dir" ]; then
        print_success "Build successful!"
        echo "Output: $dist_dir"
        ls -la "$dist_dir"
    else
        print_error "Build output not found"
        return 1
    fi
    
    cd "$SCRIPT_DIR"
    return 0
}

# Install to Grey Hack
install_to_game() {
    print_header "Installing to Grey Hack"
    
    if [ -z "$GREY_HACK_PATH" ]; then
        # Try to auto-detect
        if [ -d "/Applications/Grey Hack.app" ]; then
            GREY_HACK_PATH="/Applications/Grey Hack.app/Contents"
        elif [ -d "$HOME/Library/Application Support/Steam/steamapps/common/Grey Hack" ]; then
            GREY_HACK_PATH="$HOME/Library/Application Support/Steam/steamapps/common/Grey Hack/Grey Hack.app/Contents"
        else
            print_error "Grey Hack not found. Set GREY_HACK_PATH environment variable."
            return 1
        fi
    fi
    
    local plugins_dir="$GREY_HACK_PATH/BepInEx/plugins"
    local native_dir="$plugins_dir/GreyHackTerminalUI"
    mkdir -p "$native_dir"
    
    # Determine which platform to install
    local platform=""
    case "$(uname -s)" in
        Darwin*)
            if [[ "$(uname -m)" == "arm64" ]]; then
                platform="mac-arm64"
            else
                platform="mac-x64"
            fi
            ;;
        Linux*)
            platform="linux-x64"
            ;;
        MINGW*|MSYS*)
            platform="win-x64"
            ;;
    esac
    
    # The build now outputs to dist/<platform>/GreyHackTerminalUI/
    local dist_dir="$SCRIPT_DIR/dist/$platform/GreyHackTerminalUI"
    
    if [ ! -d "$dist_dir" ]; then
        print_error "Build output not found: $dist_dir"
        print_warning "Run build first: ./build.sh $platform"
        return 1
    fi
    
    echo "Installing from: $dist_dir"
    echo "Installing to: $native_dir"
    
    # Copy all native libraries to GreyHackTerminalUI subfolder
    cp -v "$dist_dir"/*.dylib "$native_dir/" 2>/dev/null || true
    cp -v "$dist_dir"/*.so* "$native_dir/" 2>/dev/null || true
    cp -v "$dist_dir"/*.dll "$native_dir/" 2>/dev/null || true
    
    # Copy resources and fonts folders
    if [ -d "$dist_dir/resources" ]; then
        cp -rv "$dist_dir/resources" "$native_dir/"
    fi
    
    if [ -d "$dist_dir/fonts" ]; then
        cp -rv "$dist_dir/fonts" "$native_dir/"
    fi
    
    # Sign on macOS
    if [ "$(uname -s)" = "Darwin" ]; then
        echo "Signing libraries..."
        for lib in "$native_dir"/*.dylib; do
            codesign --force --sign - "$lib" 2>/dev/null || true
        done
    fi
    
    print_success "Installation complete!"
    echo ""
    echo "Files installed to GreyHackTerminalUI folder:"
    ls -la "$native_dir"
}

# Main
print_header "ulbridge Multiplatform Build"
echo "Platforms: $PLATFORMS"
echo "Build type: $BUILD_TYPE"

check_tools

build_success=true
for platform in $PLATFORMS; do
    if ! build_platform "$platform"; then
        print_error "Build failed for $platform"
        build_success=false
    fi
done

if [ "$build_success" = false ]; then
    print_error "Some builds failed"
    exit 1
fi

if [ "$DO_INSTALL" = true ]; then
    install_to_game
fi

print_header "Build Complete"
echo "Built platforms:"
for platform in $PLATFORMS; do
    if [ -d "$SCRIPT_DIR/dist/$platform" ]; then
        print_success "$platform"
    else
        print_error "$platform (failed)"
    fi
done

echo ""
echo "Next steps:"
echo "  1. Run './build.sh --install' to install to Grey Hack"
echo "  2. Or manually copy files from dist/<platform>/ to your plugins folder"
