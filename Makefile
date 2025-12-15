# Makefile for GreyHack Customizable Output

# Variables
DOTNET = dotnet
SRC_DIR = src

# Default target
.PHONY: all
all: build-bepinex5 build-bepinex6

# Build BepInEx 5 project
.PHONY: build-bepinex5
build-bepinex5:
	@echo "Building for BepInEx 5..."
	cd $(SRC_DIR) && $(DOTNET) build --configuration Release-BepInEx5

# Build BepInEx 6 project
.PHONY: build-bepinex6
build-bepinex6:
	@echo "Building for BepInEx 6..."
	cd $(SRC_DIR) && $(DOTNET) build --configuration Release-BepInEx6

# Clean project
.PHONY: clean
clean:
	@echo "Cleaning project..."
	cd $(SRC_DIR) && $(DOTNET) clean

# Restore packages
.PHONY: restore
restore:
	@echo "Restoring packages..."
	cd $(SRC_DIR) && $(DOTNET) restore

# Rebuild both configurations (clean + build)
.PHONY: rebuild
rebuild: clean all

# Debug builds
.PHONY: debug
debug: debug-bepinex5 debug-bepinex6

.PHONY: debug-bepinex5
debug-bepinex5:
	@echo "Building for BepInEx 5 (Debug)..."
	cd $(SRC_DIR) && $(DOTNET) build --configuration Debug-BepInEx5

.PHONY: debug-bepinex6
debug-bepinex6:
	@echo "Building for BepInEx 6 (Debug)..."
	cd $(SRC_DIR) && $(DOTNET) build --configuration Debug-BepInEx6

# Release builds (default)
.PHONY: release
release: all

# Help target
.PHONY: help
help:
	@echo "Available targets:"
	@echo "  all            - Build for both BepInEx 5 and 6 (Release, default)"
	@echo "  build-bepinex5 - Build only for BepInEx 5 (Release)"
	@echo "  build-bepinex6 - Build only for BepInEx 6 (Release)"
	@echo "  clean          - Clean project"
	@echo "  restore        - Restore packages"
	@echo "  rebuild        - Clean and build both configurations"
	@echo "  debug          - Build both configurations in Debug mode"
	@echo "  debug-bepinex5 - Build only for BepInEx 5 (Debug)"
	@echo "  debug-bepinex6 - Build only for BepInEx 6 (Debug)"
	@echo "  release        - Build both configurations in Release mode"
	@echo "  help           - Show this help message"