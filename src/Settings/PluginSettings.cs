using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace GreyHackTerminalUI.Settings
{
    public static class PluginSettings
    {
        private static ConfigFile _configFile;
        private static ManualLogSource _logger;
        
        // Canvas settings
        public static ConfigEntry<bool> CanvasEnabled { get; private set; }
        public static ConfigEntry<bool> CanvasUseGameTheme { get; private set; }
        
        // Sound settings
        public static ConfigEntry<bool> SoundEnabled { get; private set; }
        public static ConfigEntry<float> SoundVolume { get; private set; }
        
        // General settings
        public static ConfigEntry<bool> ShowSettingsHotkey { get; private set; }
        public static ConfigEntry<KeyCode> SettingsHotkey { get; private set; }
        
        // Events for runtime changes
        public static event Action<bool> OnCanvasEnabledChanged;
        public static event Action<bool> OnSoundEnabledChanged;
        public static event Action<float> OnSoundVolumeChanged;
        
        public static void Initialize(ConfigFile configFile, ManualLogSource logger)
        {
            _configFile = configFile;
            _logger = logger;
            
            // Canvas settings
            CanvasEnabled = _configFile.Bind(
                "Canvas",
                "Enabled",
                true,
                "Enable or disable the Canvas rendering feature"
            );
            
            CanvasUseGameTheme = _configFile.Bind(
                "Canvas",
                "UseGameTheme",
                true,
                "Match canvas window styling to the game's UI theme"
            );
            
            // Sound settings
            SoundEnabled = _configFile.Bind(
                "Sound",
                "Enabled",
                true,
                "Enable or disable the Sound feature"
            );
            
            SoundVolume = _configFile.Bind(
                "Sound",
                "Volume",
                1.0f,
                new ConfigDescription(
                    "Master volume for all sounds (0.0 - 1.0)",
                    new AcceptableValueRange<float>(0f, 1f)
                )
            );
            
            // General settings
            ShowSettingsHotkey = _configFile.Bind(
                "General",
                "ShowSettingsHotkey",
                true,
                "Show settings hotkey hint in the game"
            );
            
            SettingsHotkey = _configFile.Bind(
                "General",
                "SettingsHotkey",
                KeyCode.F9,
                "Hotkey to open the settings menu"
            );
            
            // Wire up change events
            CanvasEnabled.SettingChanged += (s, e) => OnCanvasEnabledChanged?.Invoke(CanvasEnabled.Value);
            SoundEnabled.SettingChanged += (s, e) => OnSoundEnabledChanged?.Invoke(SoundEnabled.Value);
            SoundVolume.SettingChanged += (s, e) => OnSoundVolumeChanged?.Invoke(SoundVolume.Value);
            
            _logger?.LogInfo($"[PluginSettings] Initialized - Canvas: {CanvasEnabled.Value}, Sound: {SoundEnabled.Value}");
        }

        public static void ToggleCanvas()
        {
            CanvasEnabled.Value = !CanvasEnabled.Value;
            _logger?.LogInfo($"[PluginSettings] Canvas toggled: {CanvasEnabled.Value}");
        }

        public static void ToggleSound()
        {
            SoundEnabled.Value = !SoundEnabled.Value;
            _logger?.LogInfo($"[PluginSettings] Sound toggled: {SoundEnabled.Value}");
        }
        
        public static void SetVolume(float volume)
        {
            SoundVolume.Value = Mathf.Clamp01(volume);
            _logger?.LogInfo($"[PluginSettings] Volume set to: {SoundVolume.Value}");
        }
        
        public static void Save()
        {
            _configFile?.Save();
            _logger?.LogDebug("[PluginSettings] Settings saved");
        }
    }
}
