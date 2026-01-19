using System;
using GreyHackTerminalUI.Browser.Core;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace GreyHackTerminalUI.Browser.GameBrowser
{
    public class UltralightHtmlBridge : UltralightBrowserCore
    {
        private readonly HtmlBrowser _htmlBrowser;
        private readonly RawImage _rawImage;
        private readonly string _securityToken;
        private bool _listenersActive;
        private string _lastHtml;

        // Resize debouncing
        private int _targetWidth;
        private int _targetHeight;
        private float _resizeDebounceTimer;
        private float _resizeCooldownTimer;

        // Reusable pixel buffer to avoid per-frame allocation
        private byte[] _pixelBuffer;
        private int _pixelBufferSize;

        private const float ResizeDebounceTime = 0.15f; // Wait 150ms after last resize before applying
        private const float ResizeCooldownTime = 0.2f;  // Wait 200ms after resize for Ultralight to render

        // Base CSS for HTML4 - W3C CSS2 Default Stylesheet (https://www.w3.org/TR/CSS2/sample.html)
        private static readonly string BaseCss = @"
            html, address, blockquote, body, dd, div, dl, dt, fieldset, form,
            frame, frameset, h1, h2, h3, h4, h5, h6, noframes, ol, p, ul,
            center, dir, hr, menu, pre { display: block; unicode-bidi: embed }
            li { display: list-item }
            head { display: none }
            table { display: table }
            tr { display: table-row }
            thead { display: table-header-group }
            tbody { display: table-row-group }
            tfoot { display: table-footer-group }
            col { display: table-column }
            colgroup { display: table-column-group }
            td, th { display: table-cell }
            caption { display: table-caption }
            th { font-weight: bolder; text-align: center }
            caption { text-align: center }
            body { margin: 8px }
            h1 { font-size: 2em; margin: .67em 0 }
            h2 { font-size: 1.5em; margin: .75em 0 }
            h3 { font-size: 1.17em; margin: .83em 0 }
            h4, p, blockquote, ul, fieldset, form, ol, dl, dir, menu { margin: 1.12em 0 }
            h5 { font-size: .83em; margin: 1.5em 0 }
            h6 { font-size: .75em; margin: 1.67em 0 }
            h1, h2, h3, h4, h5, h6, b, strong { font-weight: bolder }
            blockquote { margin-left: 40px; margin-right: 40px }
            i, cite, em, var, address { font-style: italic }
            pre, tt, code, kbd, samp { font-family: monospace }
            pre { white-space: pre }
            button, textarea, input, select { display: inline-block; border-radius: 0px }
            big { font-size: 1.17em }
            small, sub, sup { font-size: .83em }
            sub { vertical-align: sub }
            sup { vertical-align: super }
            table { border-spacing: 2px }
            thead, tbody, tfoot { vertical-align: middle }
            td, th, tr { vertical-align: inherit }
            s, strike, del { text-decoration: line-through }
            hr { border: 1px inset }
            ol, ul, dir, menu, dd { margin-left: 40px }
            ol { list-style-type: decimal }
            ol ul, ul ol, ul ul, ol ol { margin-top: 0; margin-bottom: 0 }
            u, ins { text-decoration: underline }
            center { text-align: center }
            :link, :visited { text-decoration: underline }
            :focus { outline: thin dotted invert }
        ";

        private const string LoadingHtml = @"
<html>
<head>
<style>
* { margin: 0; padding: 0; box-sizing: border-box; }
html, body { height: 100%; background: #0a0a0f; }
.container {
    display: flex;
    flex-direction: column;
    justify-content: center;
    align-items: center;
    height: 100%;
    font-family: sans-serif;
}
.spinner {
    width: 40px;
    height: 40px;
    border: 3px solid #222;
    border-top-color: #4af;
    border-radius: 50%;
    animation: spin 0.8s linear infinite;
}
.text {
    margin-top: 16px;
    color: #667;
    font-size: 14px;
    letter-spacing: 2px;
}
@keyframes spin {
    to { transform: rotate(360deg); }
}
</style>
</head>
<body>
<div class=""container"">
    <div class=""spinner""></div>
    <div class=""text"">LOADING</div>
</div>
</body>
</html>";

        public UltralightHtmlBridge(HtmlBrowser htmlBrowser)
            : base("UltralightBridge", GetInitialSize(htmlBrowser).width, GetInitialSize(htmlBrowser).height, "htmlbrowser")
        {
            _htmlBrowser = htmlBrowser;

            // Get the RawImage component
            _rawImage = htmlBrowser.GetComponent<RawImage>();

            // Initialize Ultralight if not already
            if (!ULBridge.IsInitialized)
            {
                ULBridge.Initialize(enableGpu: false);
            }

            // Create the view immediately
            ULBridge.ulbridge_view_create(ViewName, ViewWidth, ViewHeight);
            _securityToken = ULBridge.GetViewToken(ViewName);
            ViewTexture = new Texture2D(ViewWidth, ViewHeight, TextureFormat.BGRA32, false);
            State = ViewState.Active;

            // Register callback for button clicks from JavaScript
            ULBridge.RegisterJSCallback($"btn_{ViewName}", OnJSButtonClick);

            // Subscribe to cursor changes
            ULBridge.OnCursor += HandleCursorChange;
            
            // Subscribe to native errors
            ULBridge.OnError += HandleNativeError;

            // Add scroll handler component to the RawImage
            if (_rawImage != null)
            {
                var scrollHandler = _rawImage.gameObject.AddComponent<HtmlBrowserScrollHandler>();
                scrollHandler.Initialize(this);
            }
        }

        private static (int width, int height) GetInitialSize(HtmlBrowser htmlBrowser)
        {
            var rawImage = htmlBrowser.GetComponent<RawImage>();
            var rect = rawImage?.rectTransform;
            int width = rect != null ? Mathf.Max(100, (int)rect.rect.width) : 800;
            int height = rect != null ? Mathf.Max(100, (int)rect.rect.height) : 600;
            return (width, height);
        }

        public void LoadHtml(string html, bool loadListener)
        {
            if (State != ViewState.Active) return;

            _listenersActive = loadListener;
            _lastHtml = html;

            base.LoadHtml(html);
        }

        public void ShowLoadingScreen()
        {
            if (State != ViewState.Active) return;

            // Reset cursor to normal when showing loading screen
            MouseCursor.Singleton?.SetNormalCursor();

            base.LoadHtml(LoadingHtml);
        }

        protected override string PreprocessHtml(string html)
        {
            // First, pre-process to fix malformed attribute syntax
            string processedHtml = LegacyHtmlConverter.PreProcess(html);

            // Replace broken templates with fixed versions
            processedHtml = HtmlTemplateReplacer.Process(processedHtml);

            // Convert legacy HTML4 tags to HTML5 equivalents
            processedHtml = LegacyHtmlConverter.Convert(processedHtml);

            // Inject base CSS
            processedHtml = HtmlStyleInjector.Inject(processedHtml, BaseCss, 1.2f);

            // Convert image sources for ImageSourceProvider
            processedHtml = HtmlImagePreprocessor.Process(processedHtml);

            return processedHtml;
        }

        public void InjectEventListeners()
        {
            if (State != ViewState.Active || !_listenersActive) return;

            // Inject JavaScript to intercept clicks on .btn.btn-primary elements
            // The security token is captured in the closure - user JS cannot access it
            // The native function is hidden (non-enumerable) and requires valid token
            string script = $@"
                (function(t) {{
                    var buttons = document.querySelectorAll('.btn.btn-primary');
                    buttons.forEach(function(btn) {{
                        btn.addEventListener('mousedown', function(e) {{
                            var id = this.id || '';
                            if (typeof __ulb_nc__ === 'function') {{
                                __ulb_nc__(t, 'btn_{ViewName}', id);
                            }}
                        }});
                    }});
                }})('{_securityToken}');
            ";

            ExecuteJavaScript(script);
        }

        private void OnJSButtonClick(string elementId)
        {
            if (State != ViewState.Active) return;

            try
            {
                EnablePanelWebInputs();
                DispatchButtonAction(elementId);
            }
            catch (Exception ex)
            {
                Log.LogError($"Error handling button click '{elementId}': {ex}");
            }
        }

        private void EnablePanelWebInputs()
        {
            try
            {
                if (_htmlBrowser.panelWebHelper != null)
                {
                    foreach (var helper in _htmlBrowser.panelWebHelper)
                    {
                        helper.EnableInputs(true);
                    }
                }
            }
            catch { /* Ignore - non-critical */ }
        }

        private void DispatchButtonAction(string buttonId)
        {
            switch (buttonId)
            {
                case "Main":
                    _htmlBrowser.EnterWeb();
                    break;
                case "HackShopTools":
                    _htmlBrowser.ShowHackShop();
                    break;
                case "HackShopExploits":
                    _htmlBrowser.ShowHackShopExploits();
                    break;
                case "InformaticaShop":
                    ShowPanel("Shop");
                    Traverse.Create(_htmlBrowser).Method("OnShowListInformaticaShop").GetValue();
                    break;
                case "Jobs":
                    ShowPanel("Jobs");
                    _htmlBrowser.OnShowListJobs();
                    break;
                case "CTF":
                    _htmlBrowser.ShowCTF();
                    break;
                case "JobsPolice":
                    ShowPanel("PoliceJobs");
                    _htmlBrowser.OnShowListJobs();
                    break;
                case "RegisterBank":
                    _htmlBrowser.ShowBankRegister();
                    break;
                case "LoginBank":
                    ShowPanel("BankLogin");
                    break;
                case "RegisterMail":
                    ShowPanel("MailRegister");
                    break;
                case "Reports":
                    ShowPanel("PoliceReport");
                    break;
                case "ISPConfig":
                    ShowPanel("ISPConfig");
                    break;
                case "CreateCurrency":
                    ShowPanel("CreateCurrency");
                    break;
                case "FindDeviceManual":
                    ShowPanel("PanelFindDeviceManual");
                    break;
                default:
                    break;
            }
        }

        private void ShowPanel(string panelName)
        {
            var enumType = typeof(HtmlBrowser).GetNestedType("SubPanelWebs", System.Reflection.BindingFlags.NonPublic);
            if (enumType != null)
            {
                MouseCursor.Singleton?.SetNormalCursor();
                var enumValue = Enum.Parse(enumType, panelName);
                Traverse.Create(_htmlBrowser).Method("ShowPanel", enumValue).GetValue();
            }
        }

        public void Update()
        {
            if (State != ViewState.Active || _rawImage == null) return;

            // Check for resize (browser window is resizable)
            var rect = _rawImage.rectTransform.rect;
            int currentWidth = Mathf.Max(100, (int)rect.width);
            int currentHeight = Mathf.Max(100, (int)rect.height);

            // Debounce resize - track target size but delay actual resize
            if (currentWidth != _targetWidth || currentHeight != _targetHeight)
            {
                _targetWidth = currentWidth;
                _targetHeight = currentHeight;
                _resizeDebounceTimer = ResizeDebounceTime;
            }

            // While debouncing, keep showing the old texture
            if (_resizeDebounceTimer > 0)
            {
                _resizeDebounceTimer -= Time.deltaTime;

                if (_resizeDebounceTimer <= 0 && (_targetWidth != ViewWidth || _targetHeight != ViewHeight))
                {
                    ULBridge.ulbridge_view_resize(ViewName, _targetWidth, _targetHeight);
                    ViewWidth = _targetWidth;
                    ViewHeight = _targetHeight;

                    // Set cooldown to let Ultralight render before we try to grab pixels
                    _resizeCooldownTimer = ResizeCooldownTime;
                }

                // Don't update texture while debouncing - keep old image visible
                return;
            }

            if (_resizeCooldownTimer > 0)
            {
                _resizeCooldownTimer -= Time.deltaTime;
                ULBridge.Update();
                return;
            }

            ULBridge.Update();
            UpdateTextureFromView();
        }

        private void UpdateTextureFromView()
        {
            IntPtr pixels = ULBridge.ulbridge_view_get_pixels(ViewName, out int w, out int h, out int stride);
            if (pixels == IntPtr.Zero || w <= 0 || h <= 0 || stride <= 0)
            {
                if (pixels != IntPtr.Zero)
                    ULBridge.ulbridge_view_unlock_pixels(ViewName);
                return;
            }

            try
            {
                int rowBytes = w * 4;
                int totalBytes = rowBytes * h;

                // Reuse buffer if size matches, otherwise reallocate
                if (_pixelBuffer == null || _pixelBufferSize != totalBytes)
                {
                    _pixelBuffer = new byte[totalBytes];
                    _pixelBufferSize = totalBytes;
                }

                // Ultralight: top-left origin, Unity: bottom-left origin
                unsafe
                {
                    byte* src = (byte*)pixels;
                    for (int y = 0; y < h; y++)
                    {
                        int dstOffset = (h - 1 - y) * rowBytes;
                        System.Runtime.InteropServices.Marshal.Copy(
                            (IntPtr)(src + y * stride), _pixelBuffer, dstOffset, rowBytes);
                    }
                }

                // Resize texture if needed
                if (ViewTexture == null || ViewTexture.width != w || ViewTexture.height != h)
                {
                    if (ViewTexture != null)
                        UnityEngine.Object.Destroy(ViewTexture);
                    ViewTexture = new Texture2D(w, h, TextureFormat.BGRA32, false);
                }

                ViewTexture.LoadRawTextureData(_pixelBuffer);
                ViewTexture.Apply();
                _rawImage.texture = ViewTexture;
            }
            catch (Exception ex)
            {
                Log.LogError($"Error copying pixels: {ex.Message}");
            }
            finally
            {
                ULBridge.ulbridge_view_unlock_pixels(ViewName);
            }
        }
        
        public void HandleKeyEvent(Event ev)
        {
            KeyEvent(ev);
        }

        private void HandleCursorChange(string viewName, CursorEvent e)
        {
            // Only handle cursor changes for our view
            if (viewName != ViewName) return;
            if (State != ViewState.Active) return;

            var cursorType = (ULCursorType)e.CursorType;

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
                Log.LogWarning($"Error setting cursor: {ex.Message}");
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
        
        private void HandleNativeError(LogEvent e)
        {
            if (State != ViewState.Active) return;
            
            // Show error via the game's HtmlBrowser message system
            try
            {
                _htmlBrowser?.ShowMessage($"[Browser Error] {e.Message}");
            }
            catch { /* Ignore if browser is unavailable */ }
        }

        protected override void OnDisposing()
        {
            ULBridge.UnregisterJSCallback($"btn_{ViewName}");
            ULBridge.OnCursor -= HandleCursorChange;
            ULBridge.OnError -= HandleNativeError;

            // Restore normal cursor when bridge is disposed
            try
            {
                MouseCursor.Singleton?.SetNormalCursor();
            }
            catch { /* Ignore */ }
        }
    }

    internal class HtmlBrowserScrollHandler : MonoBehaviour
    {
        private UltralightHtmlBridge _bridge;
        private RectTransform _rectTransform;
        private UnityEngine.Canvas _canvas;
        private Camera _canvasCamera;

        private const float ScrollMultiplier = 400f;  // Pixels per scroll unit

        public void Initialize(UltralightHtmlBridge bridge)
        {
            _bridge = bridge;
            _rectTransform = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<UnityEngine.Canvas>();
            _canvasCamera = _canvas?.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas?.worldCamera;
        }

        private void Update()
        {
            if (_bridge == null || _bridge.IsDisposed) return;

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Approximately(scroll, 0f)) return;
            if (!IsMouseOver()) return;

            int deltaY = Mathf.RoundToInt(scroll * ScrollMultiplier);
            _bridge.Scroll(0, deltaY);
        }

        private bool IsMouseOver()
        {
            if (_rectTransform == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(
                _rectTransform, Input.mousePosition, _canvasCamera);
        }
    }
}
