using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BepInEx.Logging;
using UI.Dialogs;
using GreyHackTerminalUI.Utils;

namespace GreyHackTerminalUI.Settings
{
    public static class SettingsWindow
    {
        private static uDialog _dialog;
        private static ManualLogSource _logger;
        private static bool _isVisible = false;
        
        // UI References
        private static Toggle _canvasToggle;
        private static Toggle _soundToggle;
        private static Toggle _browserToggle;
        private static Toggle _browserUIToggle;
        private static Toggle _browserPowerUIToggle;
        private static Slider _volumeSlider;
        private static TextMeshProUGUI _volumeLabel;
        
        public static uDialog Instance => _dialog;
        public static bool IsVisible => _isVisible;
        
        public static void Create(ManualLogSource logger)
        {
            _logger = logger;
        }
        
        public static void Show()
        {
            if (_dialog != null && _dialog.isVisible)
            {
                _dialog.Focus();
                return;
            }
            
            // If dialog exists but is hidden, just show it
            if (_dialog != null)
            {
                _dialog.Show();
                _isVisible = true;
                return;
            }
            
            // Find parent
            var taskBar = Object.FindObjectOfType<uDialog_TaskBar>();
            RectTransform parent = null;
            
            if (taskBar != null)
            {
                parent = taskBar.transform.parent.GetComponent<RectTransform>();
            }
            else
            {
                var desktop = Object.FindObjectOfType<DesktopFinder>();
                if (desktop != null)
                {
                    parent = desktop.GetComponent<RectTransform>();
                }
            }
            
            if (parent == null)
            {
                _logger?.LogError("[SettingsWindow] Could not find parent for dialog");
                return;
            }
            
            // Create dialog from scratch
            _dialog = DialogBuilder.Create(parent, "Terminal Canvas Settings", new Vector2(320, 360));
            
            if (_dialog == null)
            {
                _logger?.LogError("[SettingsWindow] Failed to create dialog");
                return;
            }
            
            // Build our settings UI in the content area
            var contentArea = DialogBuilder.GetContentArea(_dialog);
            if (contentArea != null)
            {
                BuildSettingsUI(contentArea);
            }
            
            // Register close event
            _dialog.Event_OnClose.AddListener(OnDialogClosed);
            
            _dialog.Show();
            _isVisible = true;
        }
        
        private static void BuildSettingsUI(RectTransform parent)
        {
            // Create a container with vertical layout
            var containerGO = new GameObject("SettingsContent");
            containerGO.transform.SetParent(parent, false);
            
            var containerRect = containerGO.AddComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = new Vector2(15, 15);
            containerRect.offsetMax = new Vector2(-15, -15);
            
            var layout = containerGO.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            
            // Canvas toggle
            _canvasToggle = CreateToggle(containerGO.transform, "Canvas Rendering", PluginSettings.CanvasEnabled.Value);
            _canvasToggle.onValueChanged.AddListener(value => PluginSettings.CanvasEnabled.Value = value);
            
            // Sound toggle
            _soundToggle = CreateToggle(containerGO.transform, "Sound Effects", PluginSettings.SoundEnabled.Value);
            _soundToggle.onValueChanged.AddListener(value => {
                PluginSettings.SoundEnabled.Value = value;
                UpdateVolumeSliderInteractable();
            });
            
            // Browser toggle
            _browserToggle = CreateToggle(containerGO.transform, "Browser (Master)", PluginSettings.BrowserEnabled.Value);
            _browserToggle.onValueChanged.AddListener(value => {
                // Only allow enabling if native libraries are available
                if (value && !PluginSettings.CanEnableBrowserFeatures)
                {
                    _browserToggle.SetIsOnWithoutNotify(false);
                    return;
                }
                PluginSettings.BrowserEnabled.Value = value;
                UpdateBrowserTogglesInteractable();
            });
            
            // Browser UI toggle (for UI block browsers)
            _browserUIToggle = CreateToggle(containerGO.transform, "  UI Block Browsers", PluginSettings.BrowserUIEnabled.Value);
            _browserUIToggle.onValueChanged.AddListener(value => {
                if (value && !PluginSettings.CanEnableBrowserFeatures)
                {
                    _browserUIToggle.SetIsOnWithoutNotify(false);
                    return;
                }
                PluginSettings.BrowserUIEnabled.Value = value;
            });
            
            // Browser PowerUI replacement toggle
            _browserPowerUIToggle = CreateToggle(containerGO.transform, "  PowerUI Replacement", PluginSettings.BrowserPowerUIReplacementEnabled.Value);
            _browserPowerUIToggle.onValueChanged.AddListener(value => {
                if (value && !PluginSettings.CanEnableBrowserFeatures)
                {
                    _browserPowerUIToggle.SetIsOnWithoutNotify(false);
                    return;
                }
                PluginSettings.BrowserPowerUIReplacementEnabled.Value = value;
            });
            
            // Volume slider
            CreateVolumeControl(containerGO.transform);
            
            UpdateVolumeSliderInteractable();
            UpdateBrowserTogglesInteractable();
        }
        
        private static Toggle CreateToggle(Transform parent, string labelText, bool defaultValue)
        {
            var toggleGO = new GameObject($"Toggle_{labelText}");
            toggleGO.transform.SetParent(parent, false);
            
            var toggleRect = toggleGO.AddComponent<RectTransform>();
            toggleRect.sizeDelta = new Vector2(0, 28);
            
            var toggleLayout = toggleGO.AddComponent<HorizontalLayoutGroup>();
            toggleLayout.spacing = 10;
            toggleLayout.childAlignment = TextAnchor.MiddleLeft;
            toggleLayout.childControlHeight = false;
            toggleLayout.childControlWidth = false;
            
            // Checkbox background
            var checkboxGO = new GameObject("Checkbox");
            checkboxGO.transform.SetParent(toggleGO.transform, false);
            var checkboxRect = checkboxGO.AddComponent<RectTransform>();
            checkboxRect.sizeDelta = new Vector2(22, 22);
            var checkboxBg = checkboxGO.AddComponent<Image>();
            checkboxBg.color = new Color32(50, 50, 50, 255);
            
            // Checkmark
            var checkmarkGO = new GameObject("Checkmark");
            checkmarkGO.transform.SetParent(checkboxGO.transform, false);
            var checkmarkRect = checkmarkGO.AddComponent<RectTransform>();
            checkmarkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkmarkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkmarkRect.sizeDelta = new Vector2(14, 14);
            var checkmarkImg = checkmarkGO.AddComponent<Image>();
            checkmarkImg.color = new Color(0f, 0.86f, 0.78f, 1f); // Match theme
            
            // Label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(toggleGO.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(200, 28);
            var label = labelGO.AddComponent<TextMeshProUGUI>();
            label.text = labelText;
            label.fontSize = 14;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            
            // Toggle component
            var toggle = toggleGO.AddComponent<Toggle>();
            toggle.targetGraphic = checkboxBg;
            toggle.graphic = checkmarkImg;
            toggle.isOn = defaultValue;
            
            return toggle;
        }
        
        private static void CreateVolumeControl(Transform parent)
        {
            // Container
            var volumeGO = new GameObject("VolumeControl");
            volumeGO.transform.SetParent(parent, false);
            
            var volumeRect = volumeGO.AddComponent<RectTransform>();
            volumeRect.sizeDelta = new Vector2(0, 28);
            
            var volumeLayout = volumeGO.AddComponent<HorizontalLayoutGroup>();
            volumeLayout.spacing = 10;
            volumeLayout.childAlignment = TextAnchor.MiddleLeft;
            volumeLayout.childControlHeight = false;
            volumeLayout.childControlWidth = false;
            
            // Label
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(volumeGO.transform, false);
            var labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(60, 28);
            var label = labelGO.AddComponent<TextMeshProUGUI>();
            label.text = "Volume";
            label.fontSize = 14;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            
            // Slider container
            var sliderGO = new GameObject("Slider");
            sliderGO.transform.SetParent(volumeGO.transform, false);
            
            var sliderRect = sliderGO.AddComponent<RectTransform>();
            sliderRect.sizeDelta = new Vector2(140, 20);
            
            // Background
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(sliderGO.transform, false);
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.25f);
            bgRect.anchorMax = new Vector2(1, 0.75f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImage = bgGO.AddComponent<Image>();
            bgImage.color = new Color32(50, 50, 50, 255);
            
            // Fill area
            var fillAreaGO = new GameObject("Fill Area");
            fillAreaGO.transform.SetParent(sliderGO.transform, false);
            var fillAreaRect = fillAreaGO.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1, 0.75f);
            fillAreaRect.offsetMin = new Vector2(5, 0);
            fillAreaRect.offsetMax = new Vector2(-5, 0);
            
            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            var fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fillGO.AddComponent<Image>();
            fillImage.color = new Color(0f, 0.86f, 0.78f, 1f); // Match theme
            
            // Handle area
            var handleAreaGO = new GameObject("Handle Slide Area");
            handleAreaGO.transform.SetParent(sliderGO.transform, false);
            var handleAreaRect = handleAreaGO.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(5, 0);
            handleAreaRect.offsetMax = new Vector2(-5, 0);
            
            var handleGO = new GameObject("Handle");
            handleGO.transform.SetParent(handleAreaGO.transform, false);
            var handleRect = handleGO.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(16, 0);
            var handleImage = handleGO.AddComponent<Image>();
            handleImage.color = Color.white;
            
            // Slider component
            _volumeSlider = sliderGO.AddComponent<Slider>();
            _volumeSlider.fillRect = fillRect;
            _volumeSlider.handleRect = handleRect;
            _volumeSlider.minValue = 0f;
            _volumeSlider.maxValue = 1f;
            _volumeSlider.value = PluginSettings.SoundVolume.Value;
            _volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            
            // Volume percentage label
            var valueLabelGO = new GameObject("ValueLabel");
            valueLabelGO.transform.SetParent(volumeGO.transform, false);
            var valueLabelRect = valueLabelGO.AddComponent<RectTransform>();
            valueLabelRect.sizeDelta = new Vector2(45, 28);
            _volumeLabel = valueLabelGO.AddComponent<TextMeshProUGUI>();
            _volumeLabel.fontSize = 14;
            _volumeLabel.color = Color.white;
            _volumeLabel.alignment = TextAlignmentOptions.MidlineRight;
            UpdateVolumeLabel();
        }
        
        private static void OnVolumeChanged(float value)
        {
            PluginSettings.SoundVolume.Value = value;
            UpdateVolumeLabel();
        }
        
        private static void UpdateVolumeLabel()
        {
            if (_volumeLabel != null)
            {
                _volumeLabel.text = $"{Mathf.RoundToInt(PluginSettings.SoundVolume.Value * 100)}%";
            }
        }
        
        private static void UpdateVolumeSliderInteractable()
        {
            if (_volumeSlider != null)
            {
                _volumeSlider.interactable = PluginSettings.SoundEnabled.Value;
            }
            if (_volumeLabel != null)
            {
                _volumeLabel.color = PluginSettings.SoundEnabled.Value ? Color.white : Color.gray;
            }
        }
        
        private static void UpdateBrowserTogglesInteractable()
        {
            bool canEnable = PluginSettings.CanEnableBrowserFeatures;
            bool masterEnabled = PluginSettings.BrowserEnabled.Value && canEnable;
            
            // Master toggle - only interactable if native libraries are available
            if (_browserToggle != null)
            {
                _browserToggle.interactable = canEnable;
                // Update the toggle visuals to show disabled state
                var checkmark = _browserToggle.graphic;
                if (checkmark != null)
                {
                    checkmark.color = canEnable ? new Color(0f, 0.86f, 0.78f, 1f) : Color.gray;
                }
            }
            
            // Sub-toggles - only interactable if master is enabled and native libraries available
            if (_browserUIToggle != null)
            {
                _browserUIToggle.interactable = masterEnabled;
                var checkmark = _browserUIToggle.graphic;
                if (checkmark != null)
                {
                    checkmark.color = masterEnabled ? new Color(0f, 0.86f, 0.78f, 1f) : Color.gray;
                }
            }
            
            if (_browserPowerUIToggle != null)
            {
                _browserPowerUIToggle.interactable = masterEnabled;
                var checkmark = _browserPowerUIToggle.graphic;
                if (checkmark != null)
                {
                    checkmark.color = masterEnabled ? new Color(0f, 0.86f, 0.78f, 1f) : Color.gray;
                }
            }
        }
        
        private static void OnDialogClosed(uDialog dialog)
        {
            _isVisible = false;
            // Ensure the dialog is properly hidden (deactivated) for re-show later
            if (_dialog != null && _dialog.gameObject != null)
            {
                _dialog.gameObject.SetActive(false);
            }
        }
        
        public static void Hide()
        {
            if (_dialog != null)
            {
                _dialog.Close();
            }
            _isVisible = false;
        }
        
        public static void Destroy()
        {
            if (_dialog != null)
            {
                Object.Destroy(_dialog.gameObject);
                _dialog = null;
            }
            _isVisible = false;
        }
    }
}
