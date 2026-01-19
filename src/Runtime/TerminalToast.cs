using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GreyHackTerminalUI.Runtime
{
    public class TerminalToast : MonoBehaviour
    {
        private static Dictionary<int, TerminalToast> _instances = new Dictionary<int, TerminalToast>();
        
        private GameObject _toastPopup;
        private TextMeshProUGUI _toastText;
        private Image _toastBackground;
        private float _toastHideTime;
        private int _terminalPID;
        
        private const float TOAST_DURATION = 4f;
        
        public static TerminalToast GetOrCreate(int terminalPID)
        {
            if (_instances.TryGetValue(terminalPID, out var existing) && existing != null)
            {
                return existing;
            }
            
            // Find the terminal window by PID
            var terminal = FindTerminalByPID(terminalPID);
            if (terminal == null)
            {
                Debug.LogWarning($"[TerminalToast] Could not find terminal with PID {terminalPID}");
                return null;
            }
            
            // Add toast component to the terminal
            var toast = terminal.gameObject.GetComponent<TerminalToast>();
            if (toast == null)
            {
                toast = terminal.gameObject.AddComponent<TerminalToast>();
                toast.Initialize(terminalPID, terminal.GetComponent<RectTransform>());
            }
            
            _instances[terminalPID] = toast;
            return toast;
        }
        
        public static void Show(int terminalPID, string message, bool isError = true)
        {
            var toast = GetOrCreate(terminalPID);
            toast?.ShowToast(message, isError);
        }
        
        public static void Remove(int terminalPID)
        {
            if (_instances.TryGetValue(terminalPID, out var toast))
            {
                if (toast != null && toast._toastPopup != null)
                {
                    Destroy(toast._toastPopup);
                }
                _instances.Remove(terminalPID);
            }
        }
        
        private static Terminal FindTerminalByPID(int pid)
        {
            return PlayerClient.Singleton.player.GetVentana(pid) as Terminal;
        }
        
        private void Initialize(int terminalPID, RectTransform parentRect)
        {
            _terminalPID = terminalPID;
            
            if (parentRect == null)
            {
                Debug.LogError("[TerminalToast] Parent RectTransform is null");
                return;
            }
            
            CreateToastUI(parentRect);
        }
        
        private void CreateToastUI(RectTransform parent)
        {
            // Create toast container
            _toastPopup = new GameObject("TerminalToast");
            _toastPopup.transform.SetParent(parent, false);
            
            var toastRect = _toastPopup.AddComponent<RectTransform>();
            // Position at bottom of terminal
            toastRect.anchorMin = new Vector2(0.05f, 0.02f);
            toastRect.anchorMax = new Vector2(0.95f, 0.12f);
            toastRect.offsetMin = Vector2.zero;
            toastRect.offsetMax = Vector2.zero;
            
            // Background
            _toastBackground = _toastPopup.AddComponent<Image>();
            _toastBackground.color = new Color32(80, 30, 30, 240); // Dark red default
            
            // Add rounded corners via a simple sprite if available, otherwise solid color
            _toastBackground.type = Image.Type.Sliced;
            
            // Text
            var textGO = new GameObject("ToastText");
            textGO.transform.SetParent(_toastPopup.transform, false);
            
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 5);
            textRect.offsetMax = new Vector2(-10, -5);
            
            _toastText = textGO.AddComponent<TextMeshProUGUI>();
            _toastText.fontSize = 12;
            _toastText.alignment = TextAlignmentOptions.Left;
            _toastText.color = new Color(1f, 0.4f, 0.4f); // Light red for errors
            _toastText.enableWordWrapping = true;
            _toastText.overflowMode = TextOverflowModes.Ellipsis;
            
            // Start hidden
            _toastPopup.SetActive(false);
        }
        
        public void ShowToast(string message, bool isError)
        {
            if (_toastPopup == null || _toastText == null || _toastBackground == null)
                return;
            
            // Truncate long messages
            if (message.Length > 120)
            {
                message = message.Substring(0, 117) + "...";
            }
            
            _toastText.text = message;
            _toastText.color = isError ? new Color(1f, 0.4f, 0.4f) : Color.white;
            _toastBackground.color = isError 
                ? new Color32(80, 30, 30, 240)  // Dark red for errors
                : new Color32(40, 40, 45, 230); // Dark gray for info
            
            _toastPopup.SetActive(true);
            _toastHideTime = Time.time + TOAST_DURATION;
        }
        
        private void Update()
        {
            // Auto-hide toast after duration
            if (_toastPopup != null && _toastPopup.activeSelf && Time.time >= _toastHideTime)
            {
                _toastPopup.SetActive(false);
            }
        }
        
        private void OnDestroy()
        {
            _instances.Remove(_terminalPID);
            if (_toastPopup != null)
            {
                Destroy(_toastPopup);
            }
        }
    }
}
