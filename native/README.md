# Ultralight Native Bridge (ulbridge)

Native bridge library for integrating Ultralight HTML renderer into Grey Hack Terminal Canvas.

## Features

- **HTML5/CSS3 rendering** via Ultralight engine
- **JavaScript execution** for dynamic content
- **Multiplatform** - Windows, Linux, macOS (all x64)
- **Security hardened** - All external network requests blocked

## Quick Start

### 1. Download Ultralight SDK

Download the SDK for each target platform from [ultralig.ht](https://ultralig.ht/):
- `ultralight-free-sdk-1.4.0-win-x64.7z`
- `ultralight-free-sdk-1.4.0-linux-x64.7z`
- `ultralight-free-sdk-1.4.0-mac-x64.7z`

### 2. Setup SDKs

```bash
# Run the setup script (auto-detects archives in ~/Downloads)
python3 setup-sdk.py

# Or specify archive locations
python3 setup-sdk.py \
    --win ~/Downloads/ultralight-free-sdk-1.4.0-win-x64.7z \
    --linux ~/Downloads/ultralight-free-sdk-1.4.0-linux-x64.7z \
    --mac ~/Downloads/ultralight-free-sdk-1.4.0-mac-x64.7z
```

This extracts SDKs to:
```
native/sdk/
  win-x64/
  linux-x64/
  mac-x64/
```

### 3. Build

```bash
# Build for current platform
./build.sh

# Build for specific platform
./build.sh mac      # macOS x64
./build.sh linux    # Linux x64
./build.sh win      # Windows x64

# Build all platforms
./build.sh all

# Build and install to Grey Hack
./build.sh --install
```

**Windows native build:**
```batch
build.bat
build.bat --install
```

## Requirements

### All Platforms
- CMake 3.16+
- Python 3.6+
- 7z/p7zip (for extracting SDKs)

### macOS
```bash
brew install cmake python3 p7zip
xcode-select --install  # For clang
```

### Linux
```bash
sudo apt install cmake python3 p7zip-full build-essential
```

### Windows
- Visual Studio 2019+ with C++ workload
- CMake (included with VS or install separately)
- Python 3

## License

This bridge code is provided as-is. Ultralight SDK has its own license terms.
