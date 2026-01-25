using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UI.Dialogs;
using GreyHackTerminalUI.Utils;
using GreyHackTerminalUI.Browser.Core;
using System;
using HarmonyLib;
using Newtonsoft.Json;
using System.Security;

namespace GreyHackTerminalUI.Browser.Window
{
    internal class UltralightWindow
    {
        private uDialog _dialog;
        private RawImage _contentImage;
        private UltralightBrowser _browser;
        private int _terminalPID = -1;
        private bool _isVisible = false;
        private bool _isInitialized = false;
        private string _securityToken;
        private bool _jsApiRegistered = false;
        
        // Toast popup for errors
        private GameObject _toastPopup;
        private TextMeshProUGUI _toastText;
        private Image _toastBackground;
        private float _toastHideTime = 0f;
        private const float TOAST_DURATION = 5f;
        
        // Console error tracking
        private int _consoleErrorCount = 0;
        private string _lastConsoleError = "";
        
        // Load state tracking for render failure detection
        private bool _isLoadPending = false;
        private float _loadStartTime = 0f;
        private const float LOAD_TIMEOUT_SECONDS = 5f;
        private bool _loadErrorShown = false;

        private const int DEFAULT_WIDTH = 800;
        private const int DEFAULT_HEIGHT = 600;

        // Events for script interaction
        public event Action<string> OnNavigate;
        public event Action<string, int, int> OnClick;

        public int TerminalPID => _terminalPID;
        public bool IsVisible => _isVisible;
        public bool IsInitialized => _isInitialized;
        public UltralightBrowser Browser => _browser;
        public uDialog Dialog => _dialog;
        public string CurrentUrl => _browser?.CurrentUrl ?? "";
        public string PageTitle => _browser?.CurrentTitle ?? "Browser";
        public bool IsLoading => _browser?.IsLoading ?? false;
        public int Width => _browser?.Width ?? DEFAULT_WIDTH;
        public int Height => _browser?.Height ?? DEFAULT_HEIGHT;

        public static UltralightWindow Create(RectTransform parent, int terminalPID)
        {
            // Ensure Ultralight is initialized
            if (!UltralightManager.IsInitialized)
            {
                UltralightManager.Initialize(false);
            }

            var window = new UltralightWindow();
            window._terminalPID = terminalPID;

            // Subscribe to view created event to get the security token
            ULBridge.OnViewCreated += window.HandleViewCreated;

            // Create browser instance
            window._browser = new UltralightBrowser(DEFAULT_WIDTH, DEFAULT_HEIGHT);
            window._browser.Create();

            // Wire up events
            window._browser.OnTitleChanged += title => window.SetTitle(title);
            window._browser.OnConsoleMessage += window.HandleConsoleMessage;
            window._browser.OnLoadStarted += window.HandleLoadStarted;
            window._browser.OnLoadFinished += window.HandleLoadFinished;
            window._browser.OnLoadFailed += window.HandleLoadFailed;
            window._browser.OnDOMReady += window.HandleDOMReady;

            // Subscribe to cursor changes
            UltralightManager.OnCursorChange += window.HandleCursorChange;
            
            // Subscribe to native errors for toast display
            ULBridge.OnError += window.HandleNativeError;
            
            // Register JS callbacks for terminal interaction
            window.RegisterJSCallbacks();

            window._isInitialized = true;
            window.CreateDialog(parent);

            Debug.Log($"[UltralightWindow] Created for terminal {terminalPID}");
            return window;
        }

        private void CreateDialog(RectTransform parent)
        {
            _dialog = DialogBuilder.Create(parent, "Browser", new Vector2(DEFAULT_WIDTH + 20, DEFAULT_HEIGHT + 50), useLayoutGroup: false);

            if (_dialog == null)
            {
                Debug.LogError("[UltralightWindow] Failed to create dialog");
                return;
            }

            var contentArea = DialogBuilder.GetContentArea(_dialog);
            if (contentArea != null)
            {
                BuildBrowserUI(contentArea);
            }

            _dialog.Event_OnClose.AddListener(OnDialogClosed);

            // Set initial texture
            if (_contentImage != null && _browser?.Texture != null)
            {
                _contentImage.texture = _browser.Texture;
            }

            // Start hidden
            _dialog.gameObject.SetActive(false);
        }

        private void BuildBrowserUI(RectTransform parent)
        {
            parent.anchorMin = Vector2.zero;
            parent.anchorMax = Vector2.one;
            parent.pivot = new Vector2(0.5f, 0.5f);
            parent.offsetMin = Vector2.zero;
            parent.offsetMax = Vector2.zero;

            CreateContentArea(parent);
            CreateToastPopup(parent);
        }

        private void CreateContentArea(RectTransform parent)
        {
            var contentGO = new GameObject("BrowserContent");
            contentGO.transform.SetParent(parent, false);

            var contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var imageGO = new GameObject("ContentImage");
            imageGO.transform.SetParent(contentGO.transform, false);

            var imageRect = imageGO.AddComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            _contentImage = imageGO.AddComponent<RawImage>();
            _contentImage.color = Color.white;

            // Add click handler
            var clickHandler = imageGO.AddComponent<UltralightClickHandler>();
            clickHandler.Initialize(this);

            // Add updater
            var updater = imageGO.AddComponent<UltralightUpdater>();
            updater.Initialize(this);
        }

        private void CreateToastPopup(RectTransform parent)
        {
            _toastPopup = new GameObject("ToastPopup");
            _toastPopup.transform.SetParent(parent, false);

            var toastRect = _toastPopup.AddComponent<RectTransform>();
            toastRect.anchorMin = new Vector2(0, 0);
            toastRect.anchorMax = new Vector2(1, 0);
            toastRect.pivot = new Vector2(0.5f, 0);
            toastRect.anchoredPosition = new Vector2(0, 10);
            toastRect.sizeDelta = new Vector2(-20, 30);

            _toastBackground = _toastPopup.AddComponent<Image>();
            _toastBackground.color = new Color32(40, 40, 45, 230);

            var textGO = new GameObject("ToastText");
            textGO.transform.SetParent(_toastPopup.transform, false);

            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 2);
            textRect.offsetMax = new Vector2(-10, -2);

            _toastText = textGO.AddComponent<TextMeshProUGUI>();
            _toastText.fontSize = 11;
            _toastText.color = Color.white;
            _toastText.alignment = TextAlignmentOptions.MidlineLeft;
            _toastText.overflowMode = TextOverflowModes.Ellipsis;

            // Start hidden
            _toastPopup.SetActive(false);
        }

        private void OnDialogClosed(uDialog dialog)
        {
            _isVisible = false;
            // Ensure the dialog is properly hidden (deactivated) for re-show later
            if (_dialog != null && _dialog.gameObject != null)
            {
                _dialog.gameObject.SetActive(false);
            }
        }

        // Public API
        
        // Static frame tracking to ensure PollEvents is called once per frame
        private static int _lastPolledFrame = -1;

        public void Update()
        {
            int currentFrame = Time.frameCount;
            if (_lastPolledFrame != currentFrame)
            {
                _lastPolledFrame = currentFrame;
                ULBridge.PollEvents();
            }

            if (_browser != null)
            {
                if (_browser.UpdateTexture() && _contentImage != null)
                {
                    _contentImage.texture = _browser.Texture;
                }
            }
            
            // Check for load timeout (render failure)
            if (_isLoadPending && !_loadErrorShown)
            {
                float elapsed = Time.time - _loadStartTime;
                if (elapsed > LOAD_TIMEOUT_SECONDS)
                {
                    // Check if there were console errors during load
                    if (_consoleErrorCount > 0)
                    {
                        ShowToast($"Page failed to render: {_lastConsoleError}", true);
                    }
                    else
                    {
                        ShowToast("Page load timed out - content may not have rendered correctly", true);
                    }
                    _loadErrorShown = true;
                    _isLoadPending = false;
                }
            }
            
            // Auto-hide toast after duration
            if (_toastPopup != null && _toastPopup.activeSelf && _toastHideTime > 0 && Time.time >= _toastHideTime)
            {
                _toastPopup.SetActive(false);
                _toastHideTime = 0f;
            }
        }
        
        private void HandleLoadStarted()
        {
            _isLoadPending = true;
            _loadStartTime = Time.time;
            _loadErrorShown = false;
            _consoleErrorCount = 0;
            _lastConsoleError = "";
            Debug.Log($"[UltralightWindow] Load started for terminal {_terminalPID}");
        }
        
        private void HandleLoadFinished()
        {
            // If we got here without errors and relatively quickly, load succeeded
            if (_isLoadPending && _consoleErrorCount == 0)
            {
                Debug.Log($"[UltralightWindow] Load finished successfully for terminal {_terminalPID}");
            }
            else if (_consoleErrorCount > 0 && !_loadErrorShown)
            {
                // Had errors during load - show them
                ShowToast($"Page loaded with errors: {_lastConsoleError}", true);
                _loadErrorShown = true;
            }
            _isLoadPending = false;
        }
        
        private void HandleLoadFailed(string errorDescription, string errorDomain, int errorCode)
        {
            Debug.LogError($"[UltralightWindow] Load FAILED for terminal {_terminalPID}: {errorDescription} ({errorDomain}:{errorCode})");
            _isLoadPending = false;
            _loadErrorShown = true;
            
            // Show error toast with details
            string errorMsg = string.IsNullOrEmpty(errorDescription) 
                ? $"Page failed to load (error {errorCode})"
                : $"{errorDescription}";
            ShowToast(errorMsg, true);
        }
        
        private void HandleDOMReady()
        {
            Debug.Log($"[UltralightWindow] DOM ready for terminal {_terminalPID}");
            
            // Auto-inject the terminal API so it's always available
            InjectTerminalJSApi();
        }

        public void Show()
        {
            if (_dialog == null) return;

            _dialog.gameObject.SetActive(true);
            _dialog.Show();
            _dialog.Focus();
            _isVisible = true;
        }

        public void Hide()
        {
            if (_dialog == null) return;
            _dialog.Close();
            _isVisible = false;
        }

        public void SetTitle(string title)
        {
            _dialog?.SetTitleText($"Browser - {title}");
        }

        public void SetSize(int width, int height)
        {
            _browser?.Resize(width, height);

            if (_dialog != null)
            {
                var rectTransform = _dialog.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.sizeDelta = new Vector2(width + 20, height + 50);
                }
            }
        }

        public void LoadHtml(string html)
        {
            _browser?.LoadHtml(html);
        }

        public void HandleClick(int x, int y)
        {
            _browser?.MouseDown(x, y, 0);
            _browser?.MouseUp(x, y, 0);
            OnClick?.Invoke(CurrentUrl, x, y);
        }

        public void HandleMouseDown(int x, int y, int button)
        {
            _browser?.MouseDown(x, y, button);
        }

        public void HandleMouseUp(int x, int y, int button)
        {
            _browser?.MouseUp(x, y, button);
        }

        public void HandleMouseMove(int x, int y)
        {
            _browser?.MouseMove(x, y);
        }

        public void HandleScroll(int deltaX, int deltaY)
        {
            _browser?.Scroll(deltaX, deltaY);
        }

        public void HandleScroll(int deltaX, int deltaY, int mouseX, int mouseY)
        {
            _browser?.Scroll(deltaX, deltaY, mouseX, mouseY);
        }

        public void HandleKeyEvent(Event ev)
        {
            _browser?.KeyEvent(ev);
        }

        public string GetSelectedText()
        {
            return _browser?.GetSelectedText() ?? string.Empty;
        }

        public void CopySelectionToClipboard()
        {
            if (_browser == null) return;

            string selectedText = _browser.GetSelectedText();
            if (string.IsNullOrEmpty(selectedText)) return;

            // Copy to game's clipboard system
            if (Clipboard.Singleton != null)
            {
                Clipboard.Singleton.SetTextCopy(selectedText, true);
                ShowToast($"Copied: {(selectedText.Length > 50 ? selectedText.Substring(0, 50) + "..." : selectedText)}", false);
                Debug.Log($"[UltralightWindow] Copied to clipboard: {selectedText.Length} chars");
            }
            else
            {
                // Fallback to Unity's clipboard
                GUIUtility.systemCopyBuffer = selectedText;
                ShowToast("Copied to system clipboard", false);
                Debug.Log($"[UltralightWindow] Copied to system clipboard (game clipboard unavailable): {selectedText.Length} chars");
            }
        }

        public void BringToFront()
        {
            _dialog?.Focus();
        }
        
        private void HandleConsoleMessage(ULMessageLevel level, string message, string sourceId, int lineNumber, int columnNumber)
        {
            // Track errors for load failure detection
            if (level == ULMessageLevel.Error)
            {
                _consoleErrorCount++;
                _lastConsoleError = message.Length > 100 ? message.Substring(0, 97) + "..." : message;
                Debug.LogError($"[UltralightWindow] Console error: {message}");
            }
            
            // Only show errors and warnings as toast
            if (level != ULMessageLevel.Error && level != ULMessageLevel.Warning)
                return;
            
            // Truncate message if too long
            string displayMsg = message.Length > 80 ? message.Substring(0, 77) + "..." : message;
            
            // Add source location if available
            string location = "";
            if (!string.IsNullOrEmpty(sourceId) && lineNumber > 0)
            {
                string fileName = sourceId;
                int lastSlash = sourceId.LastIndexOfAny(new[] { '/', '\\' });
                if (lastSlash >= 0 && lastSlash < sourceId.Length - 1)
                {
                    fileName = sourceId.Substring(lastSlash + 1);
                }
                location = $" [{fileName}:{lineNumber}]";
            }
            
            ShowToast($"{displayMsg}{location}", level == ULMessageLevel.Error);
        }
        
        private void ShowToast(string message, bool isError)
        {
            if (_toastPopup == null || _toastText == null || _toastBackground == null)
                return;
            
            _toastText.text = message;
            _toastText.color = isError ? new Color(1f, 0.4f, 0.4f) : Color.white;
            _toastBackground.color = isError 
                ? new Color32(80, 30, 30, 240)  // Dark red for errors
                : new Color32(40, 40, 45, 230); // Dark gray for info
            
            _toastPopup.SetActive(true);
            _toastHideTime = Time.time + TOAST_DURATION;
        }
        
        private void HandleNativeError(LogEvent e)
        {
            // Only show toasts when window is visible
            if (!_isVisible) return;
            
            ShowToast($"[Native] {e.Message}", true);
        }
        
        private void ShowMessage(string message, bool isError = false)
        {
            ShowToast(message, isError);
        }

        public void Destroy()
        {
            // Unregister JS callbacks
            UnregisterJSCallbacks();
            
            // Unsubscribe from cursor changes
            UltralightManager.OnCursorChange -= HandleCursorChange;
            
            // Unsubscribe from events
            ULBridge.OnViewCreated -= HandleViewCreated;
            ULBridge.OnError -= HandleNativeError;

            // Restore normal cursor
            try
            {
                MouseCursor.Singleton?.SetNormalCursor();
            }
            catch { /* Ignore */ }

            _browser?.Dispose();
            _browser = null;

            if (_dialog != null)
            {
                UnityEngine.Object.Destroy(_dialog.gameObject);
                _dialog = null;
            }
        }

        private void HandleCursorChange(string viewName, ULCursorType cursorType)
        {
            // Only handle cursor changes for our view
            if (_browser == null || viewName != _browser.ViewName) return;
            if (!_isVisible) return;

            // Map Ultralight cursor to game's MouseCursor
            try
            {
                var mc = MouseCursor.Singleton;
                if (mc == null) return;

                switch (cursorType)
                {
                    case ULCursorType.IBeam:
                    case ULCursorType.VerticalText:
                        // Text cursor - use hardware cursor directly by index
                        // CursorID.TEXT = 0
                        SetCursorByIndex(mc, 0);
                        break;

                    case ULCursorType.Hand:
                    case ULCursorType.Grab:
                    case ULCursorType.Grabbing:
                        // Hand/pointer cursor for links and buttons
                        // CursorID.HANDPOINTER = 2
                        SetCursorByIndex(mc, 2);
                        break;

                    case ULCursorType.Wait:
                    case ULCursorType.Progress:
                        mc.SetWaitCursor();
                        break;

                    case ULCursorType.Pointer:
                    default:
                        mc.SetNormalCursor();
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UltralightWindow] Error setting cursor: {ex.Message}");
            }
        }

        private static void SetCursorByIndex(MouseCursor mc, int index)
        {
            // Access hardware cursors directly by index to avoid sprite name lookup issues
            if (mc.hardwareCursors != null && index < mc.hardwareCursors.Count && mc.hardwareCursors[index] != null)
            {
                var tex = mc.hardwareCursors[index];
                // Hotspots:
                // - Text cursor (I-beam): center of the cursor
                // - Hand pointer: top-left (finger tip)
                Vector2 hotspot = index == 0 
                    ? new Vector2(tex.width / 2f, tex.height / 2f)  // Text: center
                    : Vector2.zero;                                  // Hand: top-left
                UnityEngine.Cursor.SetCursor(tex, hotspot, CursorMode.Auto);
                UnityEngine.Cursor.visible = true;
            }
            else
            {
                // Fallback to normal cursor
                mc.SetNormalCursor();
            }
        }

        public void ExecuteJs(string js)
        {
            _browser?.ExecuteJavaScript(js);
        }
        
        private void RegisterJSCallbacks()
        {
            if (_jsApiRegistered) return;
            
            string viewName = _browser?.ViewName;
            if (string.IsNullOrEmpty(viewName)) return;
            
            // Register callbacks for terminal interaction from JavaScript
            ULBridge.RegisterJSCallback($"terminal_sendInput_{viewName}", OnJSSendInput);
            ULBridge.RegisterJSCallback($"terminal_getState_{viewName}", OnJSGetState);
            
            _jsApiRegistered = true;
            Debug.Log($"[UltralightWindow] Registered JS callbacks for {viewName}");
        }
        
        private void UnregisterJSCallbacks()
        {
            if (!_jsApiRegistered) return;
            
            string viewName = _browser?.ViewName;
            if (string.IsNullOrEmpty(viewName)) return;
            
            ULBridge.UnregisterJSCallback($"terminal_sendInput_{viewName}");
            ULBridge.UnregisterJSCallback($"terminal_getState_{viewName}");
            
            _jsApiRegistered = false;
        }

        private void HandleViewCreated(string viewName, ViewCreatedEvent e)
        {
            if (_browser == null || viewName != _browser.ViewName) return;
            _securityToken = e.SecurityToken;
        }
        
        public void InjectTerminalJSApi()
        {
            if (_browser == null) return;
            
            // Check if we have the security token yet
            if (string.IsNullOrEmpty(_securityToken))
            {
                Debug.LogWarning("[UltralightWindow] InjectTerminalJSApi: security token not yet available");
                return;
            }
            
            string viewName = _browser.ViewName;
            
            // JavaScript API that exposes terminal interaction to web pages
            // The security token is captured in a closure - user JS cannot access it directly
            string script = $@"
(function(token) {{
    // Create greyhack namespace if it doesn't exist
    window.greyhack = window.greyhack || {{}};
    
    // Terminal interaction API
    window.greyhack.terminal = {{
        // Send input to the terminal (when it's waiting for user_input)
        sendInput: function(input) {{
            if (typeof input === 'object') {{
                input = JSON.stringify(input);
            }}
            if (typeof __ulb_nc__ === 'function') {{
                __ulb_nc__(token, 'terminal_sendInput_{viewName}', input || '');
                return true;
            }}
            return false;
        }},
        
        // Request terminal state (async - result comes via callback)
        // Usage: greyhack.terminal.getState(function(state) {{ console.log(state); }});
        getState: function(callback) {{
            window.__greyhack_stateCallback = callback;
            if (typeof __ulb_nc__ === 'function') {{
                __ulb_nc__(token, 'terminal_getState_{viewName}', '');
            }}
        }},
        
        // Cached state (updated by C#)
        _state: {{
            terminalPID: {_terminalPID},
            isWaitingForInput: false,
            isWaitingForAnyKey: false
        }},
        
        // Quick check if terminal is waiting (uses cached state)
        get isWaitingForInput() {{
            return this._state.isWaitingForInput;
        }},
        
        get isWaitingForAnyKey() {{
            return this._state.isWaitingForAnyKey;
        }},
        
        get terminalPID() {{
            return this._state.terminalPID;
        }}
    }};
    
    console.log('[greyhack] Terminal API initialized for PID {_terminalPID}');
}})('{_securityToken}');
";
            
            ExecuteJs(script);
            Debug.Log($"[UltralightWindow] Injected terminal JS API for terminal {_terminalPID}");
        }
        
        private void OnJSSendInput(string input)
        {
            Debug.Log($"[UltralightWindow] JS sendInput called with: {input}");
            SendInputToTerminal(input);
        }
        
        private void OnJSGetState(string _)
        {
            var state = GetTerminalState();
            string stateJson = JsonConvert.SerializeObject(state);
            
            // Update the cached state and call the callback in JS
            string script = $@"
(function() {{
    if (window.greyhack && window.greyhack.terminal) {{
        window.greyhack.terminal._state = {stateJson};
    }}
    if (typeof window.__greyhack_stateCallback === 'function') {{
        window.__greyhack_stateCallback({stateJson});
        delete window.__greyhack_stateCallback;
    }}
}})();
";
            ExecuteJs(script);
        }
        
        public void UpdateJSTerminalState()
        {
            var state = GetTerminalState();
            string stateJson = JsonConvert.SerializeObject(state);
            
            string script = $@"
if (window.greyhack && window.greyhack.terminal) {{
    window.greyhack.terminal._state = {stateJson};
}}
";
            ExecuteJs(script);
        }
        
        private Terminal FindTerminal()
        {
            if (_terminalPID < 0) return null;
            return PlayerClient.Singleton.player.GetVentana(_terminalPID) as Terminal;
        }
        
        public bool IsTerminalWaitingForInput()
        {
            var terminal = FindTerminal();
            if (terminal == null) return false;
            var field = Traverse.Create(terminal).Field("pendingInputScript");
            if (field == null) return false;
            return field.GetValue<bool>();
        }
        
        public bool IsTerminalWaitingForAnyKey()
        {
            var terminal = FindTerminal();
            if (terminal == null) return false;
            var field = Traverse.Create(terminal).Field("pendingAnyKey");
            if (field == null) return false;
            return field.GetValue<bool>();
        }
        
        public string GetLastInputPrompt()
        {
            var terminal = FindTerminal();
            if (terminal == null) return "";
            
            // Get the listAdapter field
            var listAdapterField = Traverse.Create(terminal).Field("listAdapter");
            if (listAdapterField == null) return "";
            
            var listAdapter = listAdapterField.GetValue();
            if (listAdapter == null) return "";
            
            // Try to get the last line text from the adapter
            // The prompt text is typically the last line before input
            var getPromptText = Traverse.Create(listAdapter).Method("GetPromptText");
            if (getPromptText != null)
            {
                try
                {
                    var promptText = getPromptText.GetValue<string>();
                    return promptText ?? "";
                }
                catch
                {
                    return "";
                }
            }
            
            return "";
        }
        
        public bool SendInputToTerminal(string input)
        {
            if (!IsTerminalWaitingForInput())
            {
                Debug.LogWarning("[UltralightWindow] Terminal is not waiting for input");
                return false;
            }
            
            var terminal = FindTerminal();
            if (terminal == null)
            {
                Debug.LogWarning("[UltralightWindow] Could not find terminal");
                return false;
            }
            
            // Use LaunchCommand which handles the input injection via coroutine
            terminal.LaunchCommand(input);
            
            Debug.Log($"[UltralightWindow] Sent input to terminal {_terminalPID}: {input}");
            return true;
        }
        
        public TerminalState GetTerminalState()
        {
            return new TerminalState
            {
                TerminalPID = _terminalPID,
                IsWaitingForInput = IsTerminalWaitingForInput(),
                IsWaitingForAnyKey = IsTerminalWaitingForAnyKey(),
                LastInputPrompt = GetLastInputPrompt(),
                BrowserIsLoading = _browser?.IsLoading ?? false,
                BrowserIsReady = _isInitialized && _browser != null,
                BrowserTitle = _browser?.CurrentTitle ?? "",
                ConsoleErrorCount = _consoleErrorCount,
                LastConsoleError = _lastConsoleError
            };
        }

        public void GoBack() => _browser?.GoBack();
        public void GoForward() => _browser?.GoForward();
        public void Refresh() => _browser?.Reload();
    }

    public class TerminalState
    {
        [JsonProperty("terminalPID")]
        public int TerminalPID { get; set; }
        
        [JsonProperty("isWaitingForInput")]
        public bool IsWaitingForInput { get; set; }
        
        [JsonProperty("isWaitingForAnyKey")]
        public bool IsWaitingForAnyKey { get; set; }
        
        [JsonProperty("lastInputPrompt")]
        public string LastInputPrompt { get; set; }
        
        // Browser state
        [JsonProperty("browserIsLoading")]
        public bool BrowserIsLoading { get; set; }
        
        [JsonProperty("browserIsReady")]
        public bool BrowserIsReady { get; set; }
        
        [JsonProperty("browserTitle")]
        public string BrowserTitle { get; set; }
        
        [JsonProperty("consoleErrorCount")]
        public int ConsoleErrorCount { get; set; }
        
        [JsonProperty("lastConsoleError")]
        public string LastConsoleError { get; set; }
    }

    internal class UltralightUpdater : MonoBehaviour
    {
        private UltralightWindow _window;

        public void Initialize(UltralightWindow window)
        {
            _window = window;
        }

        private void Update()
        {
            _window?.Update();
        }
    }

    internal class UltralightClickHandler : MonoBehaviour,
        UnityEngine.EventSystems.IPointerDownHandler,
        UnityEngine.EventSystems.IPointerUpHandler,
        UnityEngine.EventSystems.IPointerMoveHandler,
        UnityEngine.EventSystems.ISelectHandler,
        UnityEngine.EventSystems.IDeselectHandler,
        UnityEngine.EventSystems.IDragHandler,
        UnityEngine.EventSystems.IBeginDragHandler,
        UnityEngine.EventSystems.IEndDragHandler,
        UnityEngine.EventSystems.IInitializePotentialDragHandler
    {
        private UltralightWindow _window;
        private RectTransform _rectTransform;
        private bool _hasFocus;
        private bool _isDragging;
        private UnityEngine.Canvas _canvas;
        private Camera _canvasCamera;

        private const float ScrollMultiplier = 400f;

        public void Initialize(UltralightWindow window)
        {
            _window = window;
            _rectTransform = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<UnityEngine.Canvas>();
            _canvasCamera = _canvas?.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas?.worldCamera;

            // Make selectable for keyboard input
            var selectable = gameObject.AddComponent<Selectable>();
            selectable.transition = Selectable.Transition.None;
        }

        private (int x, int y) GetLocalCoords(UnityEngine.EventSystems.PointerEventData eventData)
        {
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform, eventData.position, eventData.pressEventCamera, out localPoint))
            {
                var rect = _rectTransform.rect;
                int x = Mathf.FloorToInt((localPoint.x - rect.xMin) / rect.width * _window.Width);
                int y = Mathf.FloorToInt((rect.yMax - localPoint.y) / rect.height * _window.Height);
                return (Mathf.Clamp(x, 0, _window.Width - 1), Mathf.Clamp(y, 0, _window.Height - 1));
            }
            return (0, 0);
        }

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
        {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(gameObject);
            // Give the Ultralight view focus for text selection to work
            _window?.Browser?.Focus();
            var (x, y) = GetLocalCoords(eventData);
            _window?.HandleMouseDown(x, y, (int)eventData.button);
        }

        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData)
        {
            var (x, y) = GetLocalCoords(eventData);
            _window?.HandleMouseUp(x, y, (int)eventData.button);
        }

        public void OnPointerMove(UnityEngine.EventSystems.PointerEventData eventData)
        {
            // Only send move events when not dragging (OnDrag handles that)
            if (!_isDragging)
            {
                var (x, y) = GetLocalCoords(eventData);
                _window?.HandleMouseMove(x, y);
            }
        }

        public void OnInitializePotentialDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            // Disable drag threshold so drag starts immediately
            eventData.useDragThreshold = false;
        }

        public void OnBeginDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            _isDragging = true;
            var (x, y) = GetLocalCoords(eventData);
            _window?.HandleMouseMove(x, y);
        }

        public void OnDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            var (x, y) = GetLocalCoords(eventData);
            _window?.HandleMouseMove(x, y);
        }

        public void OnEndDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            _isDragging = false;
            var (x, y) = GetLocalCoords(eventData);
            _window?.HandleMouseMove(x, y);
            
            // Debug: Check if any text was selected
            string selected = _window?.GetSelectedText() ?? "";
            UnityEngine.Debug.Log($"[UltralightClickHandler] Selected text after drag: '{selected}' (len={selected.Length})");
        }

        private void Update()
        {
            if (_window == null) return;

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Approximately(scroll, 0f)) return;
            if (!IsMouseOver()) return;

            int deltaY = Mathf.RoundToInt(scroll * ScrollMultiplier);
            _window.HandleScroll(0, deltaY);
        }

        private bool IsMouseOver()
        {
            return RectTransformUtility.RectangleContainsScreenPoint(_rectTransform, Input.mousePosition, _canvasCamera);
        }

        public void OnSelect(UnityEngine.EventSystems.BaseEventData eventData)
        {
            _hasFocus = true;
        }

        public void OnDeselect(UnityEngine.EventSystems.BaseEventData eventData)
        {
            _hasFocus = false;
        }

        private void OnGUI()
        {
            // Handle keyboard input via OnGUI for proper key events
            if (!_hasFocus || _window == null) return;

            var ev = Event.current;
            if (ev.type == EventType.KeyDown || ev.type == EventType.KeyUp)
            {                
                // Check for Cmd/Ctrl+C to copy selection to game clipboard
                if (ev.type == EventType.KeyDown && 
                    (ev.keyCode == KeyCode.C) && 
                    (ev.command || ev.control))
                {
                    _window.CopySelectionToClipboard();
                    ev.Use();
                    return;
                }

                _window.HandleKeyEvent(ev);
                ev.Use();
            }
        }
    }
}
