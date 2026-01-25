using System.Collections.Generic;
using UnityEngine;
using BepInEx.Logging;
using UI.Dialogs;
using GreyHackTerminalUI.Browser.Core;

namespace GreyHackTerminalUI.Browser.Window
{
    internal class BrowserManager : MonoBehaviour
    {
        private static BrowserManager _instance;
        private static ManualLogSource _logger;

        private readonly Dictionary<int, UltralightWindow> _browsers = new Dictionary<int, UltralightWindow>();
        private readonly object _browsersLock = new object();

        private Transform _parentCanvas;

        private readonly Dictionary<int, System.Action<string>> _navigationHandlers = new Dictionary<int, System.Action<string>>();
        private readonly Dictionary<int, System.Action<string, int, int>> _clickHandlers = new Dictionary<int, System.Action<string, int, int>>();

        public static BrowserManager Instance => _instance;
        
        public bool HasBrowserEngine => UltralightManager.IsInitialized;
        
        public bool IsBrowserUIAvailable => 
            Settings.PluginSettings.BrowserEnabled.Value 
            && Settings.PluginSettings.BrowserUIEnabled.Value 
            && Settings.PluginSettings.BrowserNativeLibrariesAvailable;

        public static void Initialize(ManualLogSource logger)
        {
            if (_instance != null)
                return;

            _logger = logger;

            var managerGO = new GameObject("GreyHackBrowserManager");
            DontDestroyOnLoad(managerGO);
            _instance = managerGO.AddComponent<BrowserManager>();
            
            // First check if native libraries are available
            bool nativeAvailable = UltralightManager.CheckNativeLibrariesAvailable();
            
            if (!nativeAvailable)
            {
                _logger?.LogWarning("[BrowserManager] Browser native libraries not available - browser features disabled");
                return;
            }
            
            // Only initialize Ultralight if browser features are enabled
            if (!Settings.PluginSettings.BrowserEnabled.Value)
            {
                _logger?.LogInfo("[BrowserManager] Browser feature disabled in settings, skipping Ultralight initialization");
                return;
            }
            
            // Initialize Ultralight
            try
            {
                UltralightManager.Initialize(false);
                _logger?.LogInfo("[BrowserManager] Ultralight initialized successfully");
            }
            catch (System.Exception ex)
            {
                _logger?.LogError($"[BrowserManager] Failed to initialize Ultralight: {ex.Message}");
            }

            _logger?.LogDebug("BrowserManager initialized");
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private RectTransform GetOrCreateParentCanvas()
        {
            if (_parentCanvas != null)
                return _parentCanvas as RectTransform;

            var taskBar = Object.FindObjectOfType<uDialog_TaskBar>();
            if (taskBar != null)
            {
                _parentCanvas = taskBar.transform.parent;
                return _parentCanvas as RectTransform;
            }

            var desktop = Object.FindObjectOfType<DesktopFinder>();
            if (desktop != null)
            {
                _parentCanvas = desktop.transform;
                return _parentCanvas as RectTransform;
            }

            var canvasGO = new GameObject("BrowserManagerCanvas");
            var canvas = canvasGO.AddComponent<UnityEngine.Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            DontDestroyOnLoad(canvasGO);
            _parentCanvas = canvasGO.transform;

            return _parentCanvas as RectTransform;
        }

        public UltralightWindow GetOrCreateBrowser(int terminalPID)
        {
            // Check if browser UI feature is enabled and available
            if (!IsBrowserUIAvailable)
            {
                _logger?.LogDebug($"[BrowserManager] Browser UI feature disabled or not available, skipping creation for terminal {terminalPID}");
                return null;
            }
            
            lock (_browsersLock)
            {
                if (_browsers.TryGetValue(terminalPID, out var existingBrowser))
                    return existingBrowser;
            }

            if (!HasBrowserEngine)
            {
                _logger?.LogWarning($"[BrowserManager] Ultralight not initialized.");
                return null;
            }

            var browser = UltralightWindow.Create(GetOrCreateParentCanvas(), terminalPID);
            
            if (browser == null)
            {
                _logger?.LogError($"[BrowserManager] Failed to create browser for terminal {terminalPID}");
                return null;
            }

            lock (_browsersLock)
            {
                if (_browsers.TryGetValue(terminalPID, out var existingBrowser))
                {
                    browser.Destroy();
                    return existingBrowser;
                }
                
                _browsers[terminalPID] = browser;
            }

            browser.OnNavigate += (url) => HandleNavigation(terminalPID, url);
            browser.OnClick += (url, x, y) => HandleClick(terminalPID, url, x, y);

            _logger?.LogDebug($"[BrowserManager] Created browser for terminal {terminalPID}");
            return browser;
        }

        public UltralightWindow GetBrowser(int terminalPID)
        {
            lock (_browsersLock)
            {
                if (_browsers.TryGetValue(terminalPID, out var browser))
                    return browser;
                return null;
            }
        }

        public void RegisterNavigationHandler(int terminalPID, System.Action<string> handler)
        {
            lock (_browsersLock)
            {
                _navigationHandlers[terminalPID] = handler;
            }
        }

        public void RegisterClickHandler(int terminalPID, System.Action<string, int, int> handler)
        {
            lock (_browsersLock)
            {
                _clickHandlers[terminalPID] = handler;
            }
        }

        public void ClearNavigationHandler(int terminalPID)
        {
            lock (_browsersLock)
            {
                _navigationHandlers.Remove(terminalPID);
            }
        }

        public void ClearClickHandler(int terminalPID)
        {
            lock (_browsersLock)
            {
                _clickHandlers.Remove(terminalPID);
            }
        }

        private void HandleNavigation(int terminalPID, string url)
        {
            lock (_browsersLock)
            {
                if (_navigationHandlers.TryGetValue(terminalPID, out var handler))
                {
                    try
                    {
                        handler(url);
                    }
                    catch (System.Exception ex)
                    {
                        _logger?.LogError($"[BrowserManager] Navigation handler error: {ex.Message}");
                    }
                }
            }

            _logger?.LogDebug($"[BrowserManager] Navigation to {url} for terminal {terminalPID}");
        }

        private void HandleClick(int terminalPID, string url, int x, int y)
        {
            lock (_browsersLock)
            {
                if (_clickHandlers.TryGetValue(terminalPID, out var handler))
                {
                    try
                    {
                        handler(url, x, y);
                    }
                    catch (System.Exception ex)
                    {
                        _logger?.LogError($"[BrowserManager] Click handler error: {ex.Message}");
                    }
                }
            }
        }

        public void DestroyBrowser(int terminalPID)
        {
            lock (_browsersLock)
            {
                if (_browsers.TryGetValue(terminalPID, out var browser))
                {
                    browser.Destroy();
                    _browsers.Remove(terminalPID);
                    _navigationHandlers.Remove(terminalPID);
                    _clickHandlers.Remove(terminalPID);
                    _logger?.LogDebug($"[BrowserManager] Destroyed browser for terminal {terminalPID}");
                }
            }
        }

        public void DestroyAllBrowsers()
        {
            lock (_browsersLock)
            {
                foreach (var browser in _browsers.Values)
                {
                    browser.Destroy();
                }
                _browsers.Clear();
                _navigationHandlers.Clear();
                _clickHandlers.Clear();
                _logger?.LogDebug("[BrowserManager] Destroyed all browsers");
            }
            
            UltralightManager.Shutdown();
        }

        public void MarkWindowVisible(int terminalPID)
        {
            _logger?.LogDebug($"[BrowserManager] Browser window visible for terminal {terminalPID}");
        }
    }
}
