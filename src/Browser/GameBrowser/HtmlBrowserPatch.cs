using System;
using System.Collections.Generic;
using BepInEx.Logging;
using GreyHackTerminalUI.Browser.Core;
using GreyHackTerminalUI.Browser.Window;
using GreyHackTerminalUI.Settings;
using HarmonyLib;
using PowerUI;
using UnityEngine;
using UnityEngine.UI;

namespace GreyHackTerminalUI.Browser.GameBrowser
{
    [HarmonyPatch]
    public static class HtmlBrowserPatch
    {
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("HtmlBrowserPatch");

        // Track Ultralight browsers per HtmlBrowser instance
        private static readonly Dictionary<int, UltralightHtmlBridge> _bridges = new();
        private static readonly Dictionary<int, UltralightInputHandler> _inputHandlers = new();
        
        // Track which HtmlUIPanelCustom instances we've disabled
        private static readonly HashSet<int> _disabledPanels = new();

        private static bool IsPowerUIReplacementEnabled()
        {
            return PluginSettings.BrowserEnabled.Value 
                && PluginSettings.BrowserPowerUIReplacementEnabled.Value
                && PluginSettings.BrowserNativeLibrariesAvailable;
        }

        private static UltralightHtmlBridge GetBridge(HtmlBrowser htmlBrowser)
        {
            int instanceId = htmlBrowser.GetInstanceID();
            if (!_bridges.TryGetValue(instanceId, out var bridge))
            {
                // Disable PowerUI rendering for this browser
                DisablePowerUI(htmlBrowser);
                
                // Hide the game's scrollbar - Ultralight handles scrolling internally
                HideGameScrollbar(htmlBrowser);
                
                bridge = new UltralightHtmlBridge(htmlBrowser);
                _bridges[instanceId] = bridge;

                // Attach input handler
                AttachInputHandler(htmlBrowser, bridge);


            }
            return bridge;
        }

        private static void HideGameScrollbar(HtmlBrowser htmlBrowser)
        {
            try
            {
                // The scrollbarMain field is the Unity scrollbar that was used with PowerUI
                // Since we're using Ultralight with CSS overflow scrolling, this is no longer functional
                if (htmlBrowser.scrollbarMain != null)
                {
                    htmlBrowser.scrollbarMain.gameObject.SetActive(false);
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to hide scrollbar: {ex.Message}");
            }
        }

        private static void DisablePowerUI(HtmlBrowser htmlBrowser)
        {
            var panelCustom = htmlBrowser.GetComponent<HtmlUIPanelCustom>();
            if (panelCustom == null) return;

            int panelId = panelCustom.GetInstanceID();
            if (_disabledPanels.Contains(panelId)) return;

            try
            {
                var htmlUI = panelCustom.GetHtmlUI();
                if (htmlUI != null)
                {
                    // Disable the OnUpdate callback that overwrites our texture
                    htmlUI.OnUpdate = null;
                }
                _disabledPanels.Add(panelId);
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to disable PowerUI: {ex.Message}");
            }
        }

        private static void AttachInputHandler(HtmlBrowser htmlBrowser, UltralightHtmlBridge bridge)
        {
            int instanceId = htmlBrowser.GetInstanceID();
            if (_inputHandlers.ContainsKey(instanceId)) return;

            var handler = htmlBrowser.gameObject.GetComponent<UltralightInputHandler>();
            if (handler == null)
            {
                handler = htmlBrowser.gameObject.AddComponent<UltralightInputHandler>();
            }
            handler.Initialize(bridge);
            _inputHandlers[instanceId] = handler;
        }

        public static void RemoveBridge(HtmlBrowser htmlBrowser)
        {
            int instanceId = htmlBrowser.GetInstanceID();
            
            if (_bridges.TryGetValue(instanceId, out var bridge))
            {
                bridge.Dispose();
                _bridges.Remove(instanceId);
            }

            if (_inputHandlers.TryGetValue(instanceId, out var handler))
            {
                if (handler != null)
                {
                    UnityEngine.Object.Destroy(handler);
                }
                _inputHandlers.Remove(instanceId);
            }
        }

        [HarmonyPatch(typeof(HtmlBrowser), "LoadWebpage")]
        [HarmonyPrefix]
        public static bool LoadWebpage_Prefix(HtmlBrowser __instance, string contenidoWeb, bool loadListener)
        {
            // Skip patch if PowerUI replacement is disabled
            if (!IsPowerUIReplacementEnabled())
            {
                return true; // Run original method
            }
            
            try
            {
                var bridge = GetBridge(__instance);

                // Apply streaming mode redaction (from original game logic)
                string htmlContent = contenidoWeb;
                string nombreEmpresa = Traverse.Create(__instance).Field("nombreEmpresa").GetValue<string>();
                if (Util.OS.IsStreamingMode(Util.OS.StreamingMode.URL) && !string.IsNullOrEmpty(nombreEmpresa))
                {
                    htmlContent = contenidoWeb.Replace(nombreEmpresa.CapitalizeFirstLetter(), "[REDACTED]");
                }
                
                bridge.LoadHtml(htmlContent, loadListener);
                
                if (!string.IsNullOrEmpty(htmlContent))
                {
                    bridge.InjectEventListeners();
                }
                
                return false; // Skip original method
            }
            catch (Exception ex)
            {
                Log.LogError($"Error in LoadWebpage patch: {ex}");
                return true; // Fall back to original
            }
        }

        [HarmonyPatch(typeof(HtmlBrowser), "AddListeners")]
        [HarmonyPrefix]
        public static bool AddListeners_Prefix(HtmlBrowser __instance)
        {
            // Skip patch if PowerUI replacement is disabled
            if (!IsPowerUIReplacementEnabled())
            {
                return true; // Run original method
            }
            
            try
            {
                var bridge = GetBridge(__instance);
                bridge.InjectEventListeners();
                return false; // Skip original
            }
            catch (Exception ex)
            {
                Log.LogError($"Error in AddListeners patch: {ex}");
                return true;
            }
        }

        [HarmonyPatch(typeof(HtmlBrowser), "Update")]
        [HarmonyPostfix]
        public static void Update_Postfix(HtmlBrowser __instance)
        {
            // Skip update if PowerUI replacement is disabled
            if (!IsPowerUIReplacementEnabled())
            {
                return;
            }
            
            try
            {
                if (_bridges.TryGetValue(__instance.GetInstanceID(), out var bridge))
                {
                    bridge.Update();
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"Error in Update patch: {ex}");
            }
        }

        [HarmonyPatch(typeof(HtmlBrowser), "CloseTaskBar")]
        [HarmonyFinalizer]
        public static void CloseTaskBar_Postfix(HtmlBrowser __instance)
        {
            RemoveBridge(__instance);
        }

        [HarmonyPatch(typeof(HtmlBrowser), "ShowPanel")]
        [HarmonyPrefix]
        public static void ShowPanel_Prefix(HtmlBrowser __instance, int panel)
        {
            // Skip patch if PowerUI replacement is disabled
            if (!IsPowerUIReplacementEnabled())
            {
                return;
            }
            
            try
            {
                if (panel == 0)
                {
                    if (_bridges.TryGetValue(__instance.GetInstanceID(), out var bridge))
                    {
                        bridge.ShowLoadingScreen();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"ShowPanel patch error: {ex.Message}");
            }
        }
    }
}
