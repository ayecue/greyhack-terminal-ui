#!/usr/bin/env python3
"""
Ultralight SDK Setup Script

Extracts and organizes Ultralight SDK archives for multiplatform builds.
Supports Windows, Linux, and macOS (x64 and arm64).

Usage:
    python3 setup-sdk.py                    # Auto-detect archives in common locations
    python3 setup-sdk.py --archives-dir /path/to/archives
    python3 setup-sdk.py --win /path/to/win.7z --linux /path/to/linux.7z --mac /path/to/mac.7z
"""

import os
import sys
import argparse
import subprocess
import shutil
import tempfile
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent.resolve()
SDK_DIR = SCRIPT_DIR / "sdk"

# Expected SDK structure after extraction
PLATFORMS = {
    "win-x64": {
        "archive_patterns": ["*win*x64*", "*win*64*", "*windows*"],
        "test_file": "include/Ultralight/Ultralight.h",
        "libs": ["bin/*.dll", "lib/*.lib"],
    },
    "linux-x64": {
        "archive_patterns": ["*linux*x64*", "*linux*64*"],
        "test_file": "include/Ultralight/Ultralight.h",
        "libs": ["bin/*.so*", "lib/*.so*"],
    },
    "mac-x64": {
        "archive_patterns": ["*mac*x64*", "*macos*x64*", "*osx*x64*"],
        "test_file": "include/Ultralight/Ultralight.h",
        "libs": ["bin/*.dylib", "lib/*.dylib"],
    },
    "mac-arm64": {
        "archive_patterns": ["*mac*arm64*", "*macos*arm64*", "*mac*aarch64*", "*osx*arm64*"],
        "test_file": "include/Ultralight/Ultralight.h",
        "libs": ["bin/*.dylib", "lib/*.dylib"],
    },
}


def find_7z():
    """Find 7z executable"""
    for cmd in ["7z", "7za", "7zz"]:
        if shutil.which(cmd):
            return cmd
    return None


def extract_archive(archive_path: Path, dest_dir: Path) -> bool:
    """Extract a 7z or zip archive"""
    archive_path = Path(archive_path)
    dest_dir = Path(dest_dir)
    
    print(f"  Extracting: {archive_path.name}")
    
    if archive_path.suffix == ".7z":
        sevenz = find_7z()
        if not sevenz:
            print("Error: 7z not found. Install with:")
            print("  macOS: brew install p7zip")
            print("  Linux: apt install p7zip-full")
            print("  Windows: choco install 7zip")
            return False
        
        dest_dir.mkdir(parents=True, exist_ok=True)
        result = subprocess.run(
            [sevenz, "x", "-y", f"-o{dest_dir}", str(archive_path)],
            capture_output=True,
            text=True
        )
        if result.returncode != 0:
            print(f"  Error: {result.stderr}")
            return False
    
    elif archive_path.suffix == ".zip":
        import zipfile
        dest_dir.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(archive_path, 'r') as zf:
            zf.extractall(dest_dir)
    
    else:
        print(f"  Unknown archive format: {archive_path.suffix}")
        return False
    
    return True


def find_sdk_root(extracted_dir: Path) -> Path:
    """Find the actual SDK root within extracted content"""
    # The SDK might be in a subdirectory after extraction
    test_file = "include/Ultralight/Ultralight.h"
    
    # Check if it's directly in the extracted dir
    if (extracted_dir / test_file).exists():
        return extracted_dir
    
    # Check one level deep
    for subdir in extracted_dir.iterdir():
        if subdir.is_dir():
            if (subdir / test_file).exists():
                return subdir
            # Check two levels deep
            for subsubdir in subdir.iterdir():
                if subsubdir.is_dir() and (subsubdir / test_file).exists():
                    return subsubdir
    
    return None


def setup_platform(platform: str, archive_path: Path) -> bool:
    """Setup SDK for a specific platform"""
    print(f"\n{'='*50}")
    print(f"Setting up {platform}")
    print(f"{'='*50}")
    
    target_dir = SDK_DIR / platform
    
    # Check if already set up
    test_file = PLATFORMS[platform]["test_file"]
    if (target_dir / test_file).exists():
        print(f"  SDK already exists at: {target_dir}")
        response = input("  Overwrite? [y/N]: ").strip().lower()
        if response != 'y':
            print("  Skipping.")
            return True
        shutil.rmtree(target_dir)
    
    # Extract to temp directory first
    with tempfile.TemporaryDirectory() as temp_dir:
        temp_path = Path(temp_dir)
        
        if not extract_archive(archive_path, temp_path):
            return False
        
        # Find SDK root
        sdk_root = find_sdk_root(temp_path)
        if not sdk_root:
            print(f"  Error: Could not find SDK structure in extracted archive")
            print(f"  Expected to find: {test_file}")
            return False
        
        print(f"  Found SDK root: {sdk_root}")
        
        # Move to final location
        target_dir.parent.mkdir(parents=True, exist_ok=True)
        shutil.move(str(sdk_root), str(target_dir))
    
    print(f"  ✓ Installed to: {target_dir}")
    return True


def find_archives(search_dir: Path) -> dict:
    """Find SDK archives in a directory"""
    archives = {}
    
    if not search_dir.exists():
        return archives
    
    for platform, config in PLATFORMS.items():
        for pattern in config["archive_patterns"]:
            # Look for .7z and .zip files
            for ext in [".7z", ".zip"]:
                matches = list(search_dir.glob(f"{pattern}{ext}"))
                if matches:
                    # Prefer most recent or most specific match
                    archives[platform] = sorted(matches)[-1]
                    break
            if platform in archives:
                break
    
    return archives


def main():
    parser = argparse.ArgumentParser(
        description="Setup Ultralight SDK for multiplatform builds",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
    # Auto-detect from Downloads folder
    python3 setup-sdk.py
    
    # Specify archives directory
    python3 setup-sdk.py --archives-dir ~/Downloads
    
    # Specify individual archives
    python3 setup-sdk.py \\
        --win ~/Downloads/ultralight-free-sdk-1.4.0-win-x64.7z \\
        --linux ~/Downloads/ultralight-free-sdk-1.4.0-linux-x64.7z \\
        --mac ~/Downloads/ultralight-free-sdk-1.4.0-mac-x64.7z \
        --mac-arm64 ~/Downloads/ultralight-free-sdk-1.4.0-mac-arm64.7z
        """
    )
    
    parser.add_argument("--archives-dir", type=Path,
        help="Directory containing SDK archives")
    parser.add_argument("--win", type=Path,
        help="Path to Windows x64 SDK archive")
    parser.add_argument("--linux", type=Path,
        help="Path to Linux x64 SDK archive")
    parser.add_argument("--mac", type=Path,
        help="Path to macOS x64 SDK archive")
    parser.add_argument("--mac-arm64", type=Path, dest="mac_arm64",
        help="Path to macOS arm64 SDK archive")
    parser.add_argument("--list", action="store_true",
        help="List current SDK status and exit")
    
    args = parser.parse_args()
    
    print("="*60)
    print("Ultralight SDK Setup")
    print("="*60)
    
    # Check current status
    print("\nCurrent SDK status:")
    for platform, config in PLATFORMS.items():
        target_dir = SDK_DIR / platform
        test_file = target_dir / config["test_file"]
        status = "✓ Installed" if test_file.exists() else "✗ Not found"
        print(f"  {platform}: {status}")
    
    if args.list:
        return 0
    
    # Collect archives to process
    archives = {}
    
    # Manual overrides take precedence
    if args.win:
        archives["win-x64"] = args.win
    if args.linux:
        archives["linux-x64"] = args.linux
    if args.mac:
        archives["mac-x64"] = args.mac
    if args.mac_arm64:
        archives["mac-arm64"] = args.mac_arm64
    
    # Search directories for remaining platforms
    if len(archives) < len(PLATFORMS):
        search_dirs = []
        
        if args.archives_dir:
            search_dirs.append(args.archives_dir)
        else:
            # Default search locations
            search_dirs.extend([
                Path.home() / "Downloads",
                SCRIPT_DIR,
                SCRIPT_DIR.parent,
            ])
        
        for search_dir in search_dirs:
            found = find_archives(search_dir)
            for platform, path in found.items():
                if platform not in archives:
                    archives[platform] = path
    
    if not archives:
        print("\nNo SDK archives found!")
        print("Please download the Ultralight SDK from:")
        print("  https://ultralig.ht/")
        print("\nThen run:")
        print(f"  python3 {sys.argv[0]} --archives-dir /path/to/downloads")
        return 1
    
    print("\nFound archives:")
    for platform, path in archives.items():
        print(f"  {platform}: {path}")
    
    # Process each archive
    success = True
    for platform, archive_path in archives.items():
        if not setup_platform(platform, archive_path):
            success = False
    
    # Summary
    print("\n" + "="*60)
    print("Setup complete!")
    print("="*60)
    print("\nSDK status:")
    for platform, config in PLATFORMS.items():
        target_dir = SDK_DIR / platform
        test_file = target_dir / config["test_file"]
        status = "✓ Ready" if test_file.exists() else "✗ Missing"
        print(f"  {platform}: {status}")
    
    print("\nNext steps:")
    print("  1. Build for a specific platform:")
    print("     ./build.sh mac      # macOS")
    print("     ./build.sh linux    # Linux")
    print("     ./build.sh win      # Windows (requires cross-compiler or native build)")
    print("  2. Or build all platforms:")
    print("     ./build.sh all")
    
    return 0 if success else 1


if __name__ == "__main__":
    sys.exit(main())
