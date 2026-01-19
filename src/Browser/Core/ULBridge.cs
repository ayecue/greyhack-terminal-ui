using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using GreyHackTerminalUI.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace GreyHackTerminalUI.Browser.Core
{
    public class CommandEvent
    {
        [JsonProperty("command")] public string Command { get; set; } = "";
        [JsonProperty("args")] public string Args { get; set; } = "";
    }

    public class ConsoleEvent
    {
        [JsonProperty("level")] public int Level { get; set; }
        [JsonProperty("message")] public string Message { get; set; } = "";
        [JsonProperty("sourceId")] public string SourceId { get; set; } = "";
        [JsonProperty("line")] public int Line { get; set; }
        [JsonProperty("column")] public int Column { get; set; }
    }

    public class CursorEvent
    {
        [JsonProperty("cursorType")] public int CursorType { get; set; }
    }

    public class LoadEvent
    {
        [JsonProperty("loadEventType")] public int LoadEventType { get; set; }
        [JsonProperty("frameId")] public ulong FrameId { get; set; }
        [JsonProperty("url")] public string Url { get; set; } = "";
        [JsonProperty("errorDescription")] public string ErrorDescription { get; set; } = "";
        [JsonProperty("errorDomain")] public string ErrorDomain { get; set; } = "";
        [JsonProperty("errorCode")] public int ErrorCode { get; set; }
    }

    public class LogEvent
    {
        [JsonProperty("message")] public string Message { get; set; } = "";
    }

    internal static class ULBridge
    {
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("ULBridge");

        private const string LibName = "ulbridge";
        private static IntPtr _libraryHandle = IntPtr.Zero;
        private static bool _initialized = false;
        private static bool _shuttingDown = false;

        private static UnifiedEventCallback _nativeEventCallback;
        private static Dictionary<string, Action<string>> _jsCallbacks = new();

        // All image assets used in Grey Hack HTML pages
        private static readonly string[] GameImageAssets = new[]
        {
            "badge",
            "bank",
            "blog",
            "carwork",
            "Currency",
            "email_alter",
            "fast-food",
            "gecko",
            "hospital",
            "hotel",
            "isp",
            "manufacturer",
            "party",
            "shop_alter",
            "skull",
            "smartphone",
            "supermarket",
            "university"
        };

        public static event Action<CommandEvent> OnCommand;
        public static event Action<string, ConsoleEvent> OnConsole;
        public static event Action<string, CursorEvent> OnCursor;
        public static event Action<string, LoadEvent> OnLoad;
        public static event Action<LogEvent> OnLog;
        public static event Action<LogEvent> OnError;

        // Base names without prefix/suffix
        private static readonly string[] UltralightDependencies =
        {
            "UltralightCore",
            "WebCore",
            "Ultralight",
            "AppCore"
        };

        private static string[] GetDependencies()
        {
            return UltralightDependencies
                .Select(name => NativeLibraryLoader.GetLibraryFileName(name))
                .ToArray();
        }

        public static bool PreloadLibrary()
        {
            if (_libraryHandle != IntPtr.Zero)
                return true;

            _libraryHandle = NativeLibraryLoader.LoadWithDependencies(LibName, GetDependencies());
            return _libraryHandle != IntPtr.Zero;
        }

        public static bool IsInitialized => _initialized;

        public static void Initialize(bool enableGpu = false)
        {
            if (_initialized)
                return;

            // Get the path where native resources are located
            var resourcePath = NativeLibraryLoader.GetNativeResourcePath();

            // Initialize the native library with the resource path
            ulbridge_init(enableGpu, resourcePath);

            // Subscribe to internal command handling
            OnCommand += HandleCommand;
            
            // Register the event callback with native code
            RegisterEventCallback();

            // Preregister all game image assets with Ultralight
            RegisterGameImages();

            // Register default log callback
            RegisterJSCallback("log", value => Log.LogInfo($"[JS] {value}"));

            _initialized = true;
            _shuttingDown = false;
        }

        public static void Shutdown()
        {
            if (!_initialized || _shuttingDown) return;

            _shuttingDown = true;

            OnCommand -= HandleCommand;
            ulbridge_shutdown();
            _jsCallbacks.Clear();
            _initialized = false;
        }

        public static void Update()
        {
            if (!_initialized || _shuttingDown) return;

            ulbridge_update();
            ulbridge_refresh_display(0);
            ulbridge_render();
        }

        public static void RegisterJSCallback(string name, Action<string> callback)
        {
            _jsCallbacks[name] = callback;
        }

        public static void UnregisterJSCallback(string name)
        {
            _jsCallbacks.Remove(name);
        }

        private static void HandleCommand(CommandEvent e)
        {
            if (_jsCallbacks.TryGetValue(e.Command, out var callback))
                callback(e.Args);
            else
                Log.LogWarning($"No JS callback registered for '{e.Command}'");
        }

        private static void RegisterGameImages()
        {
            int registered = 0;
            foreach (var imageId in GameImageAssets)
            {
                if (RegisterImage(imageId))
                    registered++;
            }
            
            if (registered < GameImageAssets.Length)
                Log.LogWarning($"Only registered {registered}/{GameImageAssets.Length} game images");
        }

        private static byte[] ToPremultipliedBgra(Color32[] pixels, int width, int height)
        {
            var result = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
            {
                int srcRow = (height - 1 - y) * width;
                int dstRow = y * width * 4;
                for (int x = 0; x < width; x++)
                {
                    ref Color32 p = ref pixels[srcRow + x];
                    int i = dstRow + x * 4;
                    int a = p.a;
                    result[i + 0] = (byte)((p.b * a + 127) / 255);
                    result[i + 1] = (byte)((p.g * a + 127) / 255);
                    result[i + 2] = (byte)((p.r * a + 127) / 255);
                    result[i + 3] = (byte)a;
                }
            }
            return result;
        }

        private static Color32[] GetReadablePixels(Texture2D texture)
        {
            if (texture.isReadable)
                return texture.GetPixels32();

            // RenderTexture workaround for non-readable textures
            var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;

            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;

            var readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            readable.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            var pixels = readable.GetPixels32();
            UnityEngine.Object.Destroy(readable);
            return pixels;
        }

        private static bool RegisterImage(string id)
        {
            try
            {
                // Try to load as Texture2D, then as Sprite
                var texture = Resources.Load<Texture2D>(id);
                if (texture == null)
                {
                    var sprite = Resources.Load<Sprite>(id);
                    texture = sprite?.texture;
                }

                if (texture == null)
                    return false;

                var pixels = GetReadablePixels(texture);
                var bgraData = ToPremultipliedBgra(pixels, texture.width, texture.height);
                
                var handle = GCHandle.Alloc(bgraData, GCHandleType.Pinned);
                try
                {
                    return ulbridge_register_image(id, handle.AddrOfPinnedObject(), texture.width, texture.height);
                }
                finally
                {
                    handle.Free();
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"Error registering image '{id}': {ex}");
                return false;
            }
        }

        // Delegates for callbacks
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void UnifiedEventCallback(
            int eventType,
            [MarshalAs(UnmanagedType.LPStr)] string viewName,
            [MarshalAs(UnmanagedType.LPStr)] string jsonData);

        public enum ULEventType
        {
            Command = 0,
            Console = 1,
            Cursor = 2,
            Load = 3,
            Log = 4,
            Error = 5
        }

        public static void RegisterEventCallback()
        {
            _nativeEventCallback = ProcessUnifiedEvent;
            ulbridge_set_event_callback(_nativeEventCallback);
        }

        private static T ParseEvent<T>(string json) where T : new()
            => JsonConvert.DeserializeObject<T>(json) ?? new T();

        private static void ProcessUnifiedEvent(int eventType, string viewName, string jsonData)
        {
            var type = (ULEventType)eventType;

            switch (type)
            {
                case ULEventType.Command:
                    OnCommand?.Invoke(ParseEvent<CommandEvent>(jsonData));
                    break;
                case ULEventType.Console:
                    OnConsole?.Invoke(viewName, ParseEvent<ConsoleEvent>(jsonData));
                    break;
                case ULEventType.Cursor:
                    OnCursor?.Invoke(viewName, ParseEvent<CursorEvent>(jsonData));
                    break;
                case ULEventType.Load:
                    OnLoad?.Invoke(viewName, ParseEvent<LoadEvent>(jsonData));
                    break;
                case ULEventType.Log:
                    OnLog?.Invoke(ParseEvent<LogEvent>(jsonData));
                    break;
                case ULEventType.Error:
                    OnError?.Invoke(ParseEvent<LogEvent>(jsonData));
                    break;
            }
        }

        // Initialization
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ulbridge_init(bool gpu, 
            [MarshalAs(UnmanagedType.LPStr)] string resourcePath);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ulbridge_shutdown();

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ulbridge_set_event_callback(UnifiedEventCallback cb);

        // Rendering
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ulbridge_render();

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ulbridge_update();

        // Refresh display - triggers requestAnimationFrame callbacks
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ulbridge_refresh_display(uint displayId);

        // View management (synchronous)
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ulbridge_view_create(
            [MarshalAs(UnmanagedType.LPStr)] string name, int w, int h);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr ulbridge_view_get_token(
            [MarshalAs(UnmanagedType.LPStr)] string name);

        public static string GetViewToken(string name)
        {
            IntPtr ptr = ulbridge_view_get_token(name);
            if (ptr == IntPtr.Zero)
                return string.Empty;
            return Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
        }

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool ulbridge_view_is_dirty(
            [MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ulbridge_view_get_pixels(
            [MarshalAs(UnmanagedType.LPStr)] string name,
            out int w, out int h, out int stride);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ulbridge_view_unlock_pixels(
            [MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ulbridge_view_load_html(
            [MarshalAs(UnmanagedType.LPStr)] string name,
            [MarshalAs(UnmanagedType.LPStr)] string html);

        [Obsolete("LoadURL is disabled for security. Use LoadHTML instead.", true)]
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ulbridge_view_load_url(
            [MarshalAs(UnmanagedType.LPStr)] string name,
            [MarshalAs(UnmanagedType.LPStr)] string url);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ulbridge_view_eval_script(
            [MarshalAs(UnmanagedType.LPStr)] string name,
            [MarshalAs(UnmanagedType.LPStr)] string script);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr ulbridge_view_get_selection(
            [MarshalAs(UnmanagedType.LPStr)] string name);

        public static string GetViewSelection(string name)
        {
            IntPtr ptr = ulbridge_view_get_selection(name);
            if (ptr == IntPtr.Zero)
                return string.Empty;
            return Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
        }

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ulbridge_view_resize(
            [MarshalAs(UnmanagedType.LPStr)] string name, int w, int h);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ulbridge_view_width(
            [MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ulbridge_view_height(
            [MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ulbridge_view_stride(
            [MarshalAs(UnmanagedType.LPStr)] string name);

        // Input events
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ulbridge_view_mouse_event(
            [MarshalAs(UnmanagedType.LPStr)] string name,
            int x, int y, int type, int button);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ulbridge_view_scroll_event(
            [MarshalAs(UnmanagedType.LPStr)] string name,
            int x, int y, int type);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ulbridge_view_key_event(
            [MarshalAs(UnmanagedType.LPStr)] string name,
            int type, int vcode, int mods);

        // Focus management
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ulbridge_view_focus(
            [MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ulbridge_view_unfocus(
            [MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool ulbridge_view_has_focus(
            [MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ulbridge_view_delete(
            [MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool ulbridge_register_image(
            [MarshalAs(UnmanagedType.LPStr)] string id,
            IntPtr pixels,
            int width,
            int height);
    }

    public enum ULMouseEventType
    {
        MouseMoved = 0,
        MouseDown = 1,
        MouseUp = 2
    }

    public enum ULMouseButton
    {
        None = 0,
        Left = 1,
        Middle = 2,
        Right = 3
    }

    public enum ULScrollEventType
    {
        ScrollByPixel = 0,
        ScrollByPage = 1
    }

    public enum ULKeyEventType
    {
        KeyUp = 0,
        KeyDown = 1,
        RawKeyDown = 2,
        Char = 3
    }

    [Flags]
    public enum ULKeyModifiers
    {
        None = 0,
        Alt = 1 << 0,
        Ctrl = 1 << 1,
        Meta = 1 << 2,  // Command on macOS
        Shift = 1 << 3
    }

    public enum ULMessageLevel
    {
        Log = 0,
        Warning = 1,
        Error = 2,
        Debug = 3,
        Info = 4
    }

    public enum ULCursorType
    {
        Pointer = 0,
        Cross = 1,
        Hand = 2,
        IBeam = 3,
        Wait = 4,
        Help = 5,
        EastResize = 6,
        NorthResize = 7,
        NorthEastResize = 8,
        NorthWestResize = 9,
        SouthResize = 10,
        SouthEastResize = 11,
        SouthWestResize = 12,
        WestResize = 13,
        NorthSouthResize = 14,
        EastWestResize = 15,
        NorthEastSouthWestResize = 16,
        NorthWestSouthEastResize = 17,
        ColumnResize = 18,
        RowResize = 19,
        MiddlePanning = 20,
        EastPanning = 21,
        NorthPanning = 22,
        NorthEastPanning = 23,
        NorthWestPanning = 24,
        SouthPanning = 25,
        SouthEastPanning = 26,
        SouthWestPanning = 27,
        WestPanning = 28,
        Move = 29,
        VerticalText = 30,
        Cell = 31,
        ContextMenu = 32,
        Alias = 33,
        Progress = 34,
        NoDrop = 35,
        Copy = 36,
        None = 37,
        NotAllowed = 38,
        ZoomIn = 39,
        ZoomOut = 40,
        Grab = 41,
        Grabbing = 42,
        Custom = 43
    }

    public enum ULLoadEventType
    {
        BeginLoading = 0,
        FinishLoading = 1,
        FailLoading = 2,
        DOMReady = 3,
        WindowObjectReady = 4
    }
}
