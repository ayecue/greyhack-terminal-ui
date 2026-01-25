using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx.Logging;
using UnityEngine;

namespace GreyHackTerminalUI.Browser.Core
{
    public enum ViewState
    {
        Idle,
        Active,
        Disposed
    }

    public abstract class UltralightBrowserCore : IDisposable
    {
        private static int _instanceCounter = 0;

        protected readonly ManualLogSource Log;
        public readonly string ViewName;
        protected ViewState State = ViewState.Idle;

        protected int ViewWidth;
        protected int ViewHeight;
        protected Texture2D ViewTexture;
        protected Color32[] PixelBuffer;

        // Keycode mapping for keyboard input
        protected static readonly Dictionary<KeyCode, int> KeycodeMap = new Dictionary<KeyCode, int>
        {
            {KeyCode.Backspace, 0x08},
            {KeyCode.Delete, 0x2E},
            {KeyCode.Tab, 0x09},
            {KeyCode.Clear, 0x0C},
            {KeyCode.Return, 0x0D},
            {KeyCode.Pause, 0x13},
            {KeyCode.Escape, 0x1B},
            {KeyCode.Space, 0x20},
            {KeyCode.UpArrow, 0x26},
            {KeyCode.DownArrow, 0x28},
            {KeyCode.LeftArrow, 0x25},
            {KeyCode.RightArrow, 0x27},
            {KeyCode.Insert, 0x2D},
            {KeyCode.Home, 0x24},
            {KeyCode.End, 0x23},
            {KeyCode.PageUp, 0x21},
            {KeyCode.PageDown, 0x22},
            {KeyCode.Keypad0, 0x60},
            {KeyCode.Keypad1, 0x61},
            {KeyCode.Keypad2, 0x62},
            {KeyCode.Keypad3, 0x63},
            {KeyCode.Keypad4, 0x64},
            {KeyCode.Keypad5, 0x65},
            {KeyCode.Keypad6, 0x66},
            {KeyCode.Keypad7, 0x67},
            {KeyCode.Keypad8, 0x68},
            {KeyCode.Keypad9, 0x69},
            {KeyCode.KeypadMultiply, 0x6A},
            {KeyCode.KeypadDivide, 0x6F},
            {KeyCode.KeypadPlus, 0x6B},
            {KeyCode.KeypadMinus, 0x6D},
            {KeyCode.F1, 0x70},
            {KeyCode.F2, 0x71},
            {KeyCode.F3, 0x72},
            {KeyCode.F4, 0x73},
            {KeyCode.F5, 0x74},
            {KeyCode.F6, 0x75},
            {KeyCode.F7, 0x76},
            {KeyCode.F8, 0x77},
            {KeyCode.F9, 0x78},
            {KeyCode.F10, 0x79},
            {KeyCode.F11, 0x7A},
            {KeyCode.F12, 0x7B},
            {KeyCode.Alpha0, 0x30},
            {KeyCode.Alpha1, 0x31},
            {KeyCode.Alpha2, 0x32},
            {KeyCode.Alpha3, 0x33},
            {KeyCode.Alpha4, 0x34},
            {KeyCode.Alpha5, 0x35},
            {KeyCode.Alpha6, 0x36},
            {KeyCode.Alpha7, 0x37},
            {KeyCode.Alpha8, 0x38},
            {KeyCode.Alpha9, 0x39},
            {KeyCode.Period, 0xBE},
            {KeyCode.Comma, 0xBC},
            {KeyCode.Plus, 0xBB},
            {KeyCode.Minus, 0xBD},
            {KeyCode.Slash, 0xBF},
            {KeyCode.Semicolon, 0xBA},
            {KeyCode.Equals, 0xBB},
            {KeyCode.LeftBracket, 0xDB},
            {KeyCode.RightBracket, 0xDD},
            {KeyCode.Backslash, 0xDC},
            {KeyCode.BackQuote, 0xC0},
            {KeyCode.Quote, 0xDE},
            {KeyCode.A, 0x41}, {KeyCode.B, 0x42}, {KeyCode.C, 0x43}, {KeyCode.D, 0x44},
            {KeyCode.E, 0x45}, {KeyCode.F, 0x46}, {KeyCode.G, 0x47}, {KeyCode.H, 0x48},
            {KeyCode.I, 0x49}, {KeyCode.J, 0x4A}, {KeyCode.K, 0x4B}, {KeyCode.L, 0x4C},
            {KeyCode.M, 0x4D}, {KeyCode.N, 0x4E}, {KeyCode.O, 0x4F}, {KeyCode.P, 0x50},
            {KeyCode.Q, 0x51}, {KeyCode.R, 0x52}, {KeyCode.S, 0x53}, {KeyCode.T, 0x54},
            {KeyCode.U, 0x55}, {KeyCode.V, 0x56}, {KeyCode.W, 0x57}, {KeyCode.X, 0x58},
            {KeyCode.Y, 0x59}, {KeyCode.Z, 0x5A},
            {KeyCode.LeftShift, 0xA0},
            {KeyCode.RightShift, 0xA1},
            {KeyCode.LeftControl, 0xA2},
            {KeyCode.RightControl, 0xA3},
            {KeyCode.LeftAlt, 0x12},
            {KeyCode.RightAlt, 0x12},
        };

        public int Width => ViewWidth;
        public int Height => ViewHeight;
        public Texture2D Texture => ViewTexture;
        public bool IsDisposed => State == ViewState.Disposed;
        public bool IsActive => State == ViewState.Active;

        protected UltralightBrowserCore(string logSourceName, int width, int height, string viewNamePrefix = "ulview")
        {
            Log = BepInEx.Logging.Logger.CreateLogSource(logSourceName);
            ViewName = $"{viewNamePrefix}_{++_instanceCounter}";
            ViewWidth = width;
            ViewHeight = height;
            PixelBuffer = new Color32[width * height];

            Log.LogInfo($"Creating Ultralight view: {ViewName} ({width}x{height})");
        }

        public virtual void Create()
        {
            if (State != ViewState.Idle) return;

            ULBridge.ulbridge_view_create(ViewName, ViewWidth, ViewHeight);
            ViewTexture = new Texture2D(ViewWidth, ViewHeight, TextureFormat.BGRA32, false);
            State = ViewState.Active;

            // Give the view focus so it can handle text selection
            ULBridge.ulbridge_view_focus(ViewName);

            Log.LogInfo($"Ultralight view created: {ViewName}");
        }

        #region HTML Loading

        public virtual void LoadHtml(string html)
        {
            if (State != ViewState.Active) return;

            // Preprocess HTML: replace image extensions with .imgsrc for ImageSourceProvider
            string processedHtml = PreprocessHtml(html);
            ULBridge.ulbridge_view_load_html(ViewName, processedHtml);
        }

        protected virtual string PreprocessHtml(string html)
        {
            return html;
        }

        #endregion

        #region JavaScript

        public virtual void ExecuteJavaScript(string script)
        {
            if (State != ViewState.Active) return;
            ULBridge.ulbridge_view_eval_script(ViewName, script);
        }

        public virtual string GetSelectedText()
        {
            if (State != ViewState.Active) return string.Empty;
            return ULBridge.GetViewSelection(ViewName);
        }

        public virtual void CopySelectionToClipboard()
        {
            if (State != ViewState.Active) return;

            string selectedText = GetSelectedText();
            if (!string.IsNullOrEmpty(selectedText))
            {
                GUIUtility.systemCopyBuffer = selectedText;
                Log.LogDebug($"Copied to clipboard: {selectedText.Length} chars");
            }
        }

        #endregion

        #region Focus

        public virtual void Focus()
        {
            if (State != ViewState.Active) return;
            ULBridge.ulbridge_view_focus(ViewName);
        }

        public virtual void Unfocus()
        {
            if (State != ViewState.Active) return;
            ULBridge.ulbridge_view_unfocus(ViewName);
        }

        public virtual bool HasFocus()
        {
            if (State != ViewState.Active) return false;
            return ULBridge.ulbridge_view_has_focus(ViewName);
        }

        #endregion

        #region Resize

        public virtual void Resize(int width, int height)
        {
            if (State != ViewState.Active) return;
            if (width == ViewWidth && height == ViewHeight) return;

            ViewWidth = width;
            ViewHeight = height;
            PixelBuffer = new Color32[width * height];

            if (ViewTexture != null)
            {
                UnityEngine.Object.Destroy(ViewTexture);
            }
            ViewTexture = new Texture2D(width, height, TextureFormat.BGRA32, false);

            ULBridge.ulbridge_view_resize(ViewName, width, height);

            Log.LogDebug($"Resized view to {width}x{height}");
        }

        #endregion

        #region Input Handling

        public virtual void HandleMouseEvent(Vector2 localPos, ULMouseEventType eventType, ULMouseButton button)
        {
            if (State != ViewState.Active) return;

            int x = (int)localPos.x;
            int y = (int)localPos.y;

            ULBridge.ulbridge_view_mouse_event(ViewName, x, y, (int)eventType, (int)button);
        }

        public virtual void MouseMove(int x, int y)
        {
            if (State != ViewState.Active) return;
            ULBridge.ulbridge_view_mouse_event(ViewName, x, y, (int)ULMouseEventType.MouseMoved, (int)ULMouseButton.Left);
        }

        public virtual void MouseDown(int x, int y, int button = 0)
        {
            if (State != ViewState.Active) return;
            ULBridge.ulbridge_view_mouse_event(ViewName, x, y, (int)ULMouseEventType.MouseDown, (int)ULMouseButton.Left);
        }

        public virtual void MouseUp(int x, int y, int button = 0)
        {
            if (State != ViewState.Active) return;
            ULBridge.ulbridge_view_mouse_event(ViewName, x, y, (int)ULMouseEventType.MouseUp, (int)ULMouseButton.Left);
        }

        public virtual void Scroll(int deltaX, int deltaY)
        {
            // Default to center of view
            Scroll(deltaX, deltaY, ViewWidth / 2, ViewHeight / 2);
        }

        public virtual void Scroll(int deltaX, int deltaY, int mouseX, int mouseY)
        {
            if (State != ViewState.Active) return;
            ULBridge.ulbridge_view_scroll_event(ViewName, deltaX, deltaY, (int)ULScrollEventType.ScrollByPixel);
        }

        public virtual void ScrollByPage(int deltaX, int deltaY)
        {
            if (State != ViewState.Active) return;
            ULBridge.ulbridge_view_scroll_event(ViewName, deltaX, deltaY, (int)ULScrollEventType.ScrollByPage);
        }

        public virtual void KeyEvent(Event ev)
        {
            if (State != ViewState.Active) return;

            // Get modifier flags
            int mods = 0;
            if (ev.shift) mods |= (int)ULKeyModifiers.Shift;
            if (ev.alt) mods |= (int)ULKeyModifiers.Alt;
            if (ev.control) mods |= (int)ULKeyModifiers.Ctrl;
            if (ev.command) mods |= (int)ULKeyModifiers.Meta;

            if (ev.type == EventType.KeyDown)
            {
                // Try to map the key code
                if (KeycodeMap.TryGetValue(ev.keyCode, out int vcode))
                {
                    ULBridge.ulbridge_view_key_event(ViewName, (int)ULKeyEventType.KeyDown, vcode, mods);
                }

                // For printable characters, also send a Char event
                if (ev.character != '\0' && !char.IsControl(ev.character))
                {
                    ULBridge.ulbridge_view_key_event(ViewName, (int)ULKeyEventType.Char, ev.character, mods);
                }
                // Special handling for Enter and Tab which need Char events too
                else if (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.Tab)
                {
                    char c = ev.keyCode == KeyCode.Return ? '\r' : '\t';
                    ULBridge.ulbridge_view_key_event(ViewName, (int)ULKeyEventType.Char, c, mods);
                }
            }
            else if (ev.type == EventType.KeyUp)
            {
                if (KeycodeMap.TryGetValue(ev.keyCode, out int vcode))
                {
                    ULBridge.ulbridge_view_key_event(ViewName, (int)ULKeyEventType.KeyUp, vcode, mods);
                }
            }
        }

        public virtual void KeyDown(int virtualKeyCode, ULKeyModifiers modifiers = ULKeyModifiers.None)
        {
            if (State != ViewState.Active) return;
            ULBridge.ulbridge_view_key_event(ViewName, (int)ULKeyEventType.KeyDown, virtualKeyCode, (int)modifiers);
        }

        public virtual void KeyUp(int virtualKeyCode, ULKeyModifiers modifiers = ULKeyModifiers.None)
        {
            if (State != ViewState.Active) return;
            ULBridge.ulbridge_view_key_event(ViewName, (int)ULKeyEventType.KeyUp, virtualKeyCode, (int)modifiers);
        }

        public virtual void CharInput(char c, ULKeyModifiers modifiers = ULKeyModifiers.None)
        {
            if (State != ViewState.Active) return;
            ULBridge.ulbridge_view_key_event(ViewName, (int)ULKeyEventType.Char, c, (int)modifiers);
        }

        #endregion

        #region Texture Updates

        public virtual bool UpdateTexture()
        {
            if (State != ViewState.Active) return false;

            if (!ULBridge.ulbridge_view_is_dirty(ViewName))
                return false;

            return ForceUpdateTexture();
        }

        public virtual bool ForceUpdateTexture()
        {
            if (State != ViewState.Active) return false;

            IntPtr pixels = ULBridge.ulbridge_view_get_pixels(ViewName, out int w, out int h, out int stride);

            if (pixels == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                CopyPixelsToTexture(pixels, w, h, stride);
                return true;
            }
            finally
            {
                ULBridge.ulbridge_view_unlock_pixels(ViewName);
            }
        }

        protected virtual void CopyPixelsToTexture(IntPtr pixels, int w, int h, int stride)
        {
            unsafe
            {
                byte* src = (byte*)pixels.ToPointer();

                // Copy row by row, flipping vertically for Unity coordinate system
                for (int y = 0; y < ViewHeight && y < h; y++)
                {
                    int srcRow = y;
                    int dstRow = ViewHeight - 1 - y;  // Flip Y

                    byte* srcPtr = src + srcRow * stride;

                    for (int x = 0; x < ViewWidth && x < w; x++)
                    {
                        int srcIdx = x * 4;
                        int dstIdx = dstRow * ViewWidth + x;

                        // BGRA to RGBA
                        PixelBuffer[dstIdx] = new Color32(
                            srcPtr[srcIdx + 2],  // R (from B)
                            srcPtr[srcIdx + 1],  // G
                            srcPtr[srcIdx + 0],  // B (from R)
                            srcPtr[srcIdx + 3]   // A
                        );
                    }
                }
            }

            ViewTexture.SetPixels32(PixelBuffer);
            ViewTexture.Apply();
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (State == ViewState.Disposed) return;
            var originalState = State;
            State = ViewState.Disposed;

            if (disposing)
            {
                OnDisposing();

                // Delete the native Ultralight view
                if (originalState == ViewState.Active)
                {
                    ULBridge.ulbridge_view_delete(ViewName);
                }

                if (ViewTexture != null)
                {
                    UnityEngine.Object.Destroy(ViewTexture);
                    ViewTexture = null;
                }

                PixelBuffer = null;
                Log.LogInfo($"Disposing Ultralight view: {ViewName}");
            }
        }

        protected virtual void OnDisposing()
        {
        }

        ~UltralightBrowserCore()
        {
            Dispose(false);
        }

        #endregion
    }
}
