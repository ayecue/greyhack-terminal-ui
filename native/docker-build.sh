#!/bin/bash
#
# Build Linux native library using Docker
#
# Usage:
#   ./docker-build.sh              # Build for Linux x64
#   ./docker-build.sh --clean      # Rebuild Docker image from scratch
#   ./docker-build.sh --shell      # Open a shell in the container
#

set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR"

IMAGE_NAME="ulbridge-linux"
CONTAINER_NAME="ulbridge-linux-build"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

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

# Check Docker is available
if ! command -v docker &> /dev/null; then
    print_error "Docker is not installed or not in PATH"
    echo "Install Docker Desktop from: https://www.docker.com/products/docker-desktop"
    exit 1
fi

# Check Docker is running
if ! docker info &> /dev/null; then
    print_error "Docker is not running"
    echo "Please start Docker Desktop and try again"
    exit 1
fi

# Parse arguments
DO_CLEAN=false
DO_SHELL=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --clean|-c)
            DO_CLEAN=true
            shift
            ;;
        --shell|-s)
            DO_SHELL=true
            shift
            ;;
        --help|-h)
            echo "Usage: $0 [options]"
            echo ""
            echo "Options:"
            echo "  --clean, -c   Rebuild Docker image from scratch"
            echo "  --shell, -s   Open a shell in the container for debugging"
            echo "  --help, -h    Show this help"
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Clean Docker image if requested
if [ "$DO_CLEAN" = true ]; then
    print_header "Cleaning Docker image"
    docker rmi "$IMAGE_NAME" 2>/dev/null || true
    print_success "Docker image removed"
fi

# Build Docker image if it doesn't exist
if ! docker image inspect "$IMAGE_NAME" &> /dev/null || [ "$DO_CLEAN" = true ]; then
    print_header "Building Docker image"
    docker build --platform linux/amd64 -f Dockerfile.linux -t "$IMAGE_NAME" .
    print_success "Docker image built"
fi

# Create dist directory if it doesn't exist
mkdir -p "$SCRIPT_DIR/dist/linux-x64/GreyHackTerminalUI"

# Check that Linux SDK exists locally
if [ ! -f "$SCRIPT_DIR/sdk/linux-x64/include/Ultralight/Ultralight.h" ]; then
    print_error "Linux SDK not found at $SCRIPT_DIR/sdk/linux-x64"
    echo "Please run: python3 setup-sdk.py"
    exit 1
fi

if [ "$DO_SHELL" = true ]; then
    # Open interactive shell
    print_header "Opening shell in container"
    docker run -it --rm --platform linux/amd64 \
        -v "$SCRIPT_DIR/dist:/native/dist" \
        -v "$SCRIPT_DIR/sdk/linux-x64:/native/sdk/linux-x64:ro" \
        "$IMAGE_NAME" \
        /bin/bash
else
    # Run build
    print_header "Building Linux x64 in Docker"
    
    # Remove old container if exists
    docker rm -f "$CONTAINER_NAME" 2>/dev/null || true
    
    # Run build container (x86_64 emulation on Apple Silicon)
    # Mount dist directory for output, and local Linux SDK (read-only)
    docker run --name "$CONTAINER_NAME" --platform linux/amd64 \
        -v "$SCRIPT_DIR/dist:/native/dist" \
        -v "$SCRIPT_DIR/sdk/linux-x64:/native/sdk/linux-x64:ro" \
        "$IMAGE_NAME" \
        ./build.sh linux
    
    # Check result
    BUILD_EXIT_CODE=$(docker inspect "$CONTAINER_NAME" --format='{{.State.ExitCode}}')
    
    # Clean up container
    docker rm "$CONTAINER_NAME" > /dev/null
    
    if [ "$BUILD_EXIT_CODE" = "0" ]; then
        print_success "Linux build complete!"
        echo ""
        echo "Output files:"
        ls -la "$SCRIPT_DIR/dist/linux-x64/GreyHackTerminalUI/" 2>/dev/null || echo "  (no files)"
    else
        print_error "Build failed with exit code $BUILD_EXIT_CODE"
        exit 1
    fi
fi
