using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using GreyHackTerminalUI.Canvas;
using GreyHackTerminalUI.Sound;
using GreyHackTerminalUI.Patches;
#if BEPINEX6
using BepInEx.Unity.Mono;
#endif

namespace GreyHackTerminalUI
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
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

            // Initialize Harmony
            Harmony = new Harmony(PluginInfo.PLUGIN_GUID);

            // Initialize components
            TerminalPatches.Initialize(Logger);
            CanvasManager.Initialize(Logger);
            SoundManager.Initialize(Logger);

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
