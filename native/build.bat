@echo off
REM Windows build script for ulbridge
REM 
REM Usage:
REM   build.bat              - Build for Windows x64
REM   build.bat --install    - Build and install to Grey Hack
REM   build.bat --clean      - Clean build directories
REM

setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
cd /d "%SCRIPT_DIR%"

set BUILD_TYPE=Release
set DO_INSTALL=0
set DO_CLEAN=0

REM Parse arguments
:parse_args
if "%~1"=="" goto :done_args
if "%~1"=="--install" (
    set DO_INSTALL=1
    shift
    goto :parse_args
)
if "%~1"=="-i" (
    set DO_INSTALL=1
    shift
    goto :parse_args
)
if "%~1"=="--clean" (
    set DO_CLEAN=1
    shift
    goto :parse_args
)
if "%~1"=="-c" (
    set DO_CLEAN=1
    shift
    goto :parse_args
)
if "%~1"=="--debug" (
    set BUILD_TYPE=Debug
    shift
    goto :parse_args
)
if "%~1"=="-d" (
    set BUILD_TYPE=Debug
    shift
    goto :parse_args
)
if "%~1"=="--help" goto :help
if "%~1"=="-h" goto :help

echo Unknown option: %~1
exit /b 1

:help
echo Usage: build.bat [options]
echo.
echo Options:
echo   --install, -i   Install to Grey Hack after building
echo   --clean, -c     Clean build directories
echo   --debug, -d     Build with debug symbols
echo   --help, -h      Show this help
echo.
echo Environment variables:
echo   GREY_HACK_PATH  Path to Grey Hack installation
exit /b 0

:done_args

REM Clean if requested
if %DO_CLEAN%==1 (
    echo ========================================
    echo Cleaning build directories
    echo ========================================
    if exist "%SCRIPT_DIR%build" rmdir /s /q "%SCRIPT_DIR%build"
    if exist "%SCRIPT_DIR%dist" rmdir /s /q "%SCRIPT_DIR%dist"
    echo Clean complete
    exit /b 0
)

echo ========================================
echo ulbridge Windows Build
echo ========================================
echo.

REM Check for required tools
where cmake >nul 2>&1
if errorlevel 1 (
    echo ERROR: cmake not found
    echo Install Visual Studio with C++ development tools
    echo or install cmake: https://cmake.org/download/
    exit /b 1
)

REM Check SDK
set SDK_DIR=%SCRIPT_DIR%sdk\win-x64
if not exist "%SDK_DIR%\include\Ultralight\Ultralight.h" (
    echo SDK not found at: %SDK_DIR%
    echo Running SDK setup...
    python "%SCRIPT_DIR%setup-sdk.py"
    if errorlevel 1 (
        echo SDK setup failed
        exit /b 1
    )
)

REM Create build directory
set BUILD_DIR=%SCRIPT_DIR%build\win-x64
if not exist "%BUILD_DIR%" mkdir "%BUILD_DIR%"

echo Building for Windows x64...
echo SDK: %SDK_DIR%
echo Build type: %BUILD_TYPE%
echo.

cd /d "%BUILD_DIR%"

REM Remove trailing backslash from SCRIPT_DIR for CMake source path
set SOURCE_DIR=%SCRIPT_DIR:~0,-1%

REM Find Visual Studio generator or use Ninja with Build Tools
set VS_GENERATOR=
set USE_NINJA=0

REM Check if cl.exe is in PATH (Developer Command Prompt)
where cl >nul 2>&1
if not errorlevel 1 (
    REM cl.exe found - we can use Ninja generator
    where ninja >nul 2>&1
    if not errorlevel 1 (
        set USE_NINJA=1
        goto :found_generator
    )
)

REM Check for full Visual Studio installation via vswhere
set VSWHERE="%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if exist %VSWHERE% (
    for /f "tokens=*" %%i in ('%VSWHERE% -latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath 2^>nul') do (
        if exist "%%i\Common7\IDE\devenv.exe" (
            REM Full VS found
            for %%G in ("Visual Studio 17 2022" "Visual Studio 16 2019" "Visual Studio 15 2017") do (
                cmake --help | findstr /C:%%G >nul 2>&1
                if not errorlevel 1 (
                    set VS_GENERATOR=%%~G
                    goto :found_generator
                )
            )
        ) else (
            REM Build Tools only - need to use from Developer Command Prompt
            echo.
            echo Build Tools detected but not running from Developer Command Prompt.
            echo.
            echo Please run this script from:
            echo   "Developer Command Prompt for VS 2022"
            echo   or "x64 Native Tools Command Prompt for VS 2022"
            echo.
            echo Find it in: Start Menu ^> Visual Studio 2022 ^> Developer Command Prompt
            echo.
            exit /b 1
        )
    )
)

echo Visual Studio or Build Tools not found.
echo.
echo Options:
echo   1. Install Visual Studio 2022 Community: https://visualstudio.microsoft.com/vs/community/
echo      Select "Desktop development with C++" workload
echo.
echo   2. Install Build Tools: https://visualstudio.microsoft.com/visual-cpp-build-tools/
echo      Then run from "Developer Command Prompt for VS 2022"
echo.
exit /b 1

:found_generator
if %USE_NINJA%==1 (
    echo Using generator: Ninja with Build Tools
    cmake -G "Ninja" -DCMAKE_BUILD_TYPE=%BUILD_TYPE% -DULSDK_DIR="%SDK_DIR%" -DTARGET_ARCH=x64 "%SOURCE_DIR%"
) else (
    echo Using generator: %VS_GENERATOR%
    cmake -G "%VS_GENERATOR%" -A x64 -DCMAKE_BUILD_TYPE=%BUILD_TYPE% -DULSDK_DIR="%SDK_DIR%" -DTARGET_ARCH=x64 "%SOURCE_DIR%"
)

if errorlevel 1 (
    echo CMake configuration failed
    exit /b 1
)

REM Build
cmake --build . --config %BUILD_TYPE%
if errorlevel 1 (
    echo Build failed
    exit /b 1
)

cd /d "%SCRIPT_DIR%"

REM Verify output
set DIST_DIR=%SCRIPT_DIR%dist\win-x64\GreyHackTerminalUI
if not exist "%DIST_DIR%" (
    echo Build output not found
    exit /b 1
)

echo.
echo ========================================
echo Build successful!
echo ========================================
echo Output: %DIST_DIR%
dir "%DIST_DIR%"

REM Install if requested
if %DO_INSTALL%==1 (
    echo.
    echo ========================================
    echo Installing to Grey Hack
    echo ========================================
    
    if not defined GREY_HACK_PATH (
        REM Try to auto-detect
        if exist "C:\Program Files (x86)\Steam\steamapps\common\Grey Hack" (
            set GREY_HACK_PATH=C:\Program Files ^(x86^)\Steam\steamapps\common\Grey Hack
        ) else if exist "C:\Program Files\Steam\steamapps\common\Grey Hack" (
            set GREY_HACK_PATH=C:\Program Files\Steam\steamapps\common\Grey Hack
        ) else (
            echo ERROR: Grey Hack not found. Set GREY_HACK_PATH environment variable.
            exit /b 1
        )
    )
    
    set PLUGINS_DIR=!GREY_HACK_PATH!\BepInEx\plugins
    set NATIVE_DIR=!PLUGINS_DIR!\GreyHackTerminalUI
    if not exist "!NATIVE_DIR!" mkdir "!NATIVE_DIR!"
    
    echo Installing to: !NATIVE_DIR!
    
    copy /y "%DIST_DIR%\*.dll" "!NATIVE_DIR!\"
    if exist "%DIST_DIR%\resources" xcopy /e /i /y "%DIST_DIR%\resources" "!NATIVE_DIR!\resources\"
    if exist "%DIST_DIR%\fonts" xcopy /e /i /y "%DIST_DIR%\fonts" "!NATIVE_DIR!\fonts\"
    
    echo.
    echo Installation complete!
    dir "!NATIVE_DIR!"
)

echo.
echo ========================================
echo Done
echo ========================================
