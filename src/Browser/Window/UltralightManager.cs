using System;
using BepInEx.Logging;
using GreyHackTerminalUI.Browser.Core;
using GreyHackTerminalUI.Utils;

namespace GreyHackTerminalUI.Browser.Window
{
    public static class UltralightManager
    {
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("UltralightManager");

        private static bool _initialized = false;
        private static bool _nativeLibrariesAvailable = false;
        private static bool _nativeLibrariesChecked = false;
        
        // Public events for higher-level subscribers
        public static event Action<string, ULMessageLevel, string, string, int, int> OnConsoleMessage;
        public static event Action<string, ULCursorType> OnCursorChange;
        public static event Action<string, ULLoadEventType, ulong, string, string, string, int> OnLoadEvent;

        public static bool CheckNativeLibrariesAvailable()
        {
            if (_nativeLibrariesChecked)
            {
                return _nativeLibrariesAvailable;
            }
            
            _nativeLibrariesChecked = true;
            
            try
            {
                Log.LogInfo("Checking if browser native libraries are available...");
                _nativeLibrariesAvailable = ULBridge.PreloadLibrary();
                
                if (_nativeLibrariesAvailable)
                {
                    Log.LogInfo("Browser native libraries are available");
                }
                else
                {
                    Log.LogWarning("Browser native libraries are NOT available - browser features will be disabled");
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to check native libraries: {ex.Message}");
                _nativeLibrariesAvailable = false;
            }
            
            // Update plugin settings with availability
            Settings.PluginSettings.SetBrowserNativeLibrariesAvailable(_nativeLibrariesAvailable);
            
            return _nativeLibrariesAvailable;
        }
        
        public static bool NativeLibrariesAvailable => _nativeLibrariesAvailable;

        public static void Initialize(bool enableGpu = false)
        {
            if (_initialized)
            {
                Log.LogWarning("Ultralight already initialized");
                return;
            }

            try
            {
                Log.LogInfo("Initializing Ultralight...");

                // Check native libraries if not already done
                if (!_nativeLibrariesChecked)
                {
                    CheckNativeLibrariesAvailable();
                }
                
                // If native libraries aren't available, don't try to initialize
                if (!_nativeLibrariesAvailable)
                {
                    Log.LogWarning("Cannot initialize Ultralight - native libraries not available");
                    return;
                }

                // Initialize ULBridge (handles native init, event callbacks, image registration)
                ULBridge.Initialize(enableGpu);

                // Subscribe to bridge events for higher-level event forwarding
                ULBridge.OnConsole += HandleConsole;
                ULBridge.OnCursor += HandleCursor;
                ULBridge.OnLoad += HandleLoad;
                ULBridge.OnLog += HandleLog;
                ULBridge.OnError += HandleError;

                _initialized = true;
                Log.LogInfo("Ultralight initialized successfully");
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to initialize Ultralight: {ex}");
                throw;
            }
        }

        public static bool IsInitialized => _initialized;

        public static void Shutdown()
        {
            if (!_initialized) return;

            try
            {
                // Unsubscribe from bridge events
                ULBridge.OnConsole -= HandleConsole;
                ULBridge.OnCursor -= HandleCursor;
                ULBridge.OnLoad -= HandleLoad;
                ULBridge.OnLog -= HandleLog;
                ULBridge.OnError -= HandleError;

                ULBridge.Shutdown();
                _initialized = false;
            }
            catch (Exception ex)
            {
                Log.LogError($"Error shutting down Ultralight: {ex}");
            }
        }

        // =====================================================================
        // Bridge event handlers
        // =====================================================================

        private static void HandleConsole(string viewName, ConsoleEvent e)
        {
            var level = (ULMessageLevel)e.Level;

            if (level == ULMessageLevel.Error)
                Log.LogError($"[JS:{viewName}] {e.Message} ({e.SourceId}:{e.Line}:{e.Column})");

            OnConsoleMessage?.Invoke(viewName, level, e.Message, e.SourceId, e.Line, e.Column);
        }

        private static void HandleCursor(string viewName, CursorEvent e)
            => OnCursorChange?.Invoke(viewName, (ULCursorType)e.CursorType);

        private static void HandleLoad(string viewName, LoadEvent e)
        {
            var loadEventType = (ULLoadEventType)e.LoadEventType;

            // Log load events
            switch (loadEventType)
            {
                case ULLoadEventType.FailLoading:
                    Log.LogError($"[Load Failed:{viewName}] {e.Url} - {e.ErrorDescription} ({e.ErrorDomain}:{e.ErrorCode})");
                    break;
                case ULLoadEventType.BeginLoading:
                    Log.LogDebug($"[Load Begin:{viewName}] {e.Url}");
                    break;
                case ULLoadEventType.FinishLoading:
                    Log.LogDebug($"[Load Finish:{viewName}] {e.Url}");
                    break;
                case ULLoadEventType.DOMReady:
                    Log.LogDebug($"[DOM Ready:{viewName}] {e.Url}");
                    break;
            }

            OnLoadEvent?.Invoke(viewName, loadEventType, e.FrameId, e.Url, e.ErrorDescription, e.ErrorDomain, e.ErrorCode);
        }

        private static void HandleLog(LogEvent e) => Log.LogInfo($"[Native] {e.Message}");

        private static void HandleError(LogEvent e) => Log.LogError($"[Native] {e.Message}");
    }
}
