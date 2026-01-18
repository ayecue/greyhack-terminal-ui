using HarmonyLib;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GreyHackTerminalUI.Patches
{
    [HarmonyPatch]
    public static class SettingsWindowPatch
    {
        private static ManualLogSource _logger;

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }
        
        [HarmonyPatch(typeof(SettingsWindow), "Start")]
        [HarmonyPostfix]
        public static void Postfix_SettingsWindowStart(SettingsWindow __instance)
        {
            try
            {
                var contentOptions = Traverse.Create(__instance).Field("contentOptions").GetValue<Transform>();

                if (contentOptions == null)
                {
                    _logger?.LogError("[SettingsWindowPatch] contentOptions is null");
                    return;
                }
                
                // Find an existing button to use as visual template
                Button existingButton = null;
                foreach (Transform child in contentOptions)
                {
                    var btn = child.GetComponent<Button>();
                    if (btn != null)
                    {
                        existingButton = btn;
                        break;
                    }
                }
                
                if (existingButton == null)
                {
                    _logger?.LogError("[SettingsWindowPatch] Could not find existing button template");
                    return;
                }
                
                // Clone the existing button's GameObject for visual styling (layout, images, etc.)
                var newButtonGO = Object.Instantiate(existingButton.gameObject, contentOptions);
                newButtonGO.name = "TerminalCanvasSettingsButton";
                
                // Update the text
                var textComponent = newButtonGO.GetComponentInChildren<TMP_Text>();
                if (textComponent != null)
                {
                    textComponent.text = "Terminal Canvas";
                }
                
                // Clear all existing click listeners and add our own
                var button = newButtonGO.GetComponent<Button>();
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(() => OnTerminalCanvasSettings(__instance));
                
                // Move to the end of the list
                newButtonGO.transform.SetAsLastSibling();
            }
            catch (System.Exception ex)
            {
                _logger?.LogError($"[SettingsWindowPatch] Error injecting button: {ex}");
            }
        }
        
        private static void OnTerminalCanvasSettings(SettingsWindow parentWindow)
        {
            Settings.SettingsWindow.Show();
        }
    }
}
