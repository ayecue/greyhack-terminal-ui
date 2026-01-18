using System;
using UnityEngine;
using BepInEx.Logging;

namespace GreyHackTerminalUI.Utils
{
    public static class GameThemeHelper
    {
        private static ManualLogSource _logger;
        private static Type _osType;
        private static Type _uiThemeType;
        private static bool _initialized = false;
        private static bool _isAvailable = false;
        
        // Cached theme colors (updated periodically)
        private static Color _titleColor = new Color32(30, 30, 30, 255);
        private static Color _titleTextColor = new Color32(220, 220, 220, 255);
        private static Color _windowBgColor = new Color32(45, 45, 45, 255);
        private static Color _outlineColor = new Color32(0, 158, 145, 225);
        private static Color _buttonColor = new Color32(60, 60, 60, 255);
        private static Color _buttonHighlight = new Color32(80, 80, 80, 255);
        private static Color _textColor = new Color32(220, 220, 220, 255);
        private static Color _buttonImages = new Color32(0, 158, 145, 255);
        
        private static float _lastUpdateTime = 0f;
        private const float UPDATE_INTERVAL = 5f; // Update theme every 5 seconds
        
        public static Color TitleColor => _titleColor;
        public static Color TitleTextColor => _titleTextColor;
        public static Color WindowBackgroundColor => _windowBgColor;
        public static Color OutlineColor => _outlineColor;
        public static Color ButtonColor => _buttonColor;
        public static Color ButtonHighlightColor => _buttonHighlight;
        public static Color TextColor => _textColor;
        public static Color ButtonImagesColor => _buttonImages;
        
        public static bool IsAvailable => _isAvailable;
        
        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
            
            try
            {
                // Try to find the OS class
                _osType = Type.GetType("Util.OS, Assembly-CSharp");
                if (_osType == null)
                {
                    // Try alternative assembly
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        _osType = assembly.GetType("Util.OS");
                        if (_osType != null) break;
                    }
                }
                
                if (_osType == null)
                {
                    _logger?.LogWarning("[GameThemeHelper] Could not find OS type");
                    _initialized = true;
                    return;
                }
                
                // Find UI_Theme type
                _uiThemeType = Type.GetType("UI_Theme, Assembly-CSharp");
                if (_uiThemeType == null)
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        _uiThemeType = assembly.GetType("UI_Theme");
                        if (_uiThemeType != null) break;
                    }
                }
                
                if (_uiThemeType == null)
                {
                    _logger?.LogWarning("[GameThemeHelper] Could not find UI_Theme type");
                    _initialized = true;
                    return;
                }
                
                _isAvailable = true;
                _initialized = true;
                _logger?.LogInfo("[GameThemeHelper] Initialized successfully");
                
                // Do initial update
                UpdateTheme();
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[GameThemeHelper] Initialization failed: {ex}");
                _initialized = true;
            }
        }
        
        public static void UpdateTheme()
        {
            if (!_initialized)
            {
                Initialize(_logger);
            }
            
            if (!_isAvailable)
                return;
            
            float now = Time.realtimeSinceStartup;
            if (now - _lastUpdateTime < UPDATE_INTERVAL)
                return;
            
            _lastUpdateTime = now;
            
            try
            {
                // Call OS.GetThemeFromFile()
                var method = _osType.GetMethod("GetThemeFromFile", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                
                if (method == null)
                {
                    _logger?.LogWarning("[GameThemeHelper] GetThemeFromFile method not found");
                    return;
                }
                
                var theme = method.Invoke(null, null);
                if (theme == null)
                {
                    return;
                }
                
                // Read theme fields
                _titleColor = GetThemeColor(theme, "title");
                _titleTextColor = GetThemeColor(theme, "titleText");
                _windowBgColor = GetThemeColor(theme, "window_background");
                _outlineColor = GetThemeColor(theme, "outline");
                _buttonColor = GetThemeColor(theme, "buttons_background");
                _buttonHighlight = GetThemeColor(theme, "buttonsHighlight");
                _textColor = GetThemeColor(theme, "titleText");
                _buttonImages = GetThemeColor(theme, "buttons_images");
                
                _logger?.LogDebug("[GameThemeHelper] Theme colors updated");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[GameThemeHelper] UpdateTheme failed: {ex.Message}");
            }
        }
        
        private static Color GetThemeColor(object theme, string fieldName)
        {
            try
            {
                var field = _uiThemeType.GetField(fieldName, 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                if (field != null)
                {
                    var value = field.GetValue(theme);
                    if (value is Color32 c32)
                    {
                        return c32;
                    }
                    if (value is Color c)
                    {
                        return c;
                    }
                }
            }
            catch { }
            
            return Color.white;
        }
        
        public static void ForceUpdate()
        {
            _lastUpdateTime = 0f;
            UpdateTheme();
        }
    }
}
