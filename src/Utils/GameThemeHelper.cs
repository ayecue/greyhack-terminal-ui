using System;
using UnityEngine;
using BepInEx.Logging;

namespace GreyHackTerminalUI.Utils
{
    public static class GameThemeHelper
    {
        private static ManualLogSource _logger;
        
        private static Color _titleColor = new Color32(30, 30, 30, 255);
        private static Color _titleTextColor = new Color32(220, 220, 220, 255);
        private static Color _windowBgColor = new Color32(45, 45, 45, 255);
        private static Color _outlineColor = new Color32(0, 158, 145, 225);
        private static Color _buttonColor = new Color32(60, 60, 60, 255);
        private static Color _buttonHighlight = new Color32(80, 80, 80, 255);
        private static Color _textColor = new Color32(220, 220, 220, 255);
        private static Color _buttonImages = new Color32(0, 158, 145, 255);
        
        public static Color TitleColor => _titleColor;
        public static Color TitleTextColor => _titleTextColor;
        public static Color WindowBackgroundColor => _windowBgColor;
        public static Color OutlineColor => _outlineColor;
        public static Color ButtonColor => _buttonColor;
        public static Color ButtonHighlightColor => _buttonHighlight;
        public static Color TextColor => _textColor;
        public static Color ButtonImagesColor => _buttonImages;
        
        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }
        
        private static void ApplyThemeColors(UI_Theme theme)
        {
            _titleColor = theme.title;
            _titleTextColor = theme.titleText;
            _windowBgColor = theme.window_background;
            _outlineColor = theme.outline;
            _buttonColor = theme.buttons_background;
            _buttonHighlight = theme.buttonsHighlight;
            _textColor = theme.titleText;
            _buttonImages = theme.buttons_images;
        }
        
        public static void UpdateTheme()
        {
            try
            {
                var theme = Util.OS.GetThemeFromFile();
                ApplyThemeColors(theme);
                _logger?.LogDebug("[GameThemeHelper] Theme colors updated");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[GameThemeHelper] UpdateTheme failed: {ex.Message}");
            }
        }
    }
}
