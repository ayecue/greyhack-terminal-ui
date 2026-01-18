using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using GreyHackTerminalUI.Canvas;
using GreyHackTerminalUI.Sound;
using GreyHackTerminalUI.Patches;
using GreyHackTerminalUI.Settings;
using GreyHackTerminalUI.Utils;
#if BEPINEX6
using BepInEx.Unity.Mono;
#endif

namespace GreyHackTerminalUI
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("GreyHackMessageHook", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }
        public static Harmony Harmony { get; private set; }

        void Awake()
        {
            Instance = this;
            Log = Logger;

            Logger.LogDebug($"Plugin {PluginInfo.PLUGIN_GUID} v{PluginInfo.PLUGIN_VERSION} is loading...");

            // Initialize settings first
            PluginSettings.Initialize(Config, Logger);

            // Initialize Harmony
            Harmony = new Harmony(PluginInfo.PLUGIN_GUID);

            // Initialize components
            TerminalPatches.Initialize(Logger);
            SettingsWindowPatch.Initialize(Logger);
            CanvasManager.Initialize(Logger);
            SoundManager.Initialize(Logger);
            GameThemeHelper.Initialize(Logger);

            // Apply all patches
            try
            {
                Harmony.PatchAll();
                Logger.LogDebug("Harmony patches applied successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to apply Harmony patches: {ex}");
            }

            Logger.LogDebug($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }
        
        void Start()
        {
            // Initialize the settings window after the game has started
            try
            {
                Settings.SettingsWindow.Create(Logger);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize settings window: {ex}");
            }
        }

        void OnDestroy()
        {
            // Cleanup
            Harmony?.UnpatchSelf();
            
            if (CanvasManager.Instance != null)
            {
                CanvasManager.Instance.DestroyAllWindows();
            }

            Logger.LogDebug($"Plugin {PluginInfo.PLUGIN_GUID} unloaded");
        }
    }
}
