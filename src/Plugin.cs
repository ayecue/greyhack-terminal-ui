using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using GreyHackTerminalUI.Canvas;
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
            Logger.LogDebug("UI scripting available via #UI{ ... } blocks in print statements");
            Logger.LogDebug("Example:");
            Logger.LogDebug("  print(\"#UI{");
            Logger.LogDebug("    Canvas.setSize(320, 240)");
            Logger.LogDebug("    Canvas.show()");
            Logger.LogDebug("    Canvas.clear(\"black\")");
            Logger.LogDebug("    Canvas.fillRect(\"red\", 10, 10, 50, 50)");
            Logger.LogDebug("    Canvas.render()");
            Logger.LogDebug("  }\")");
            Logger.LogDebug("Features: variables, math, if/then/else, while loops, persistent state");
            Logger.LogDebug("See README.md for full documentation");
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
