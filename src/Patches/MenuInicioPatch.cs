using HarmonyLib;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GreyHackTerminalUI.Patches
{
    [HarmonyPatch]
    public static class MenuInicioPatch
    {
        private static ManualLogSource _logger;
        private static GameObject _terminalCanvasButton;

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }
        
        [HarmonyPatch(typeof(MenuInicio), "Start")]
        [HarmonyPostfix]
        public static void Postfix_MenuInicioStart(MenuInicio __instance)
        {
            try
            {
                // Find an existing button to clone (e.g., the Preferences button)
                var preferencesButton = __instance.transform.Find("Preferences");
                if (preferencesButton == null)
                {
                    _logger?.LogError("[MenuInicioPatch] Could not find Preferences button");
                    return;
                }
                
                // Clone the button
                _terminalCanvasButton = Object.Instantiate(preferencesButton.gameObject, __instance.transform);
                _terminalCanvasButton.name = "TerminalCanvas";
                
                // Update the text
                var textComponent = _terminalCanvasButton.GetComponentInChildren<TMP_Text>();
                if (textComponent != null)
                {
                    textComponent.text = "Terminal Canvas";
                }
                
                // Clear existing click listeners and add our own
                var button = _terminalCanvasButton.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick = new Button.ButtonClickedEvent();
                    button.onClick.AddListener(() => OnTerminalCanvasClick(__instance));
                }
                
                // Position it after Help (before Reboot/Shutdown)
                // Find Help button index and place after it
                var helpButton = __instance.transform.Find("Help");
                if (helpButton != null)
                {
                    int helpIndex = helpButton.GetSiblingIndex();
                    _terminalCanvasButton.transform.SetSiblingIndex(helpIndex + 1);
                }
                
                _logger?.LogInfo("[MenuInicioPatch] Added Terminal Canvas button to main menu");
            }
            catch (System.Exception ex)
            {
                _logger?.LogError($"[MenuInicioPatch] Error injecting button: {ex}");
            }
        }
        
        private static void OnTerminalCanvasClick(MenuInicio menu)
        {
            // Hide the menu first
            menu.OnHideMainMenu();
            
            // Show our settings window
            Settings.SettingsWindow.Show();
        }
    }
}
