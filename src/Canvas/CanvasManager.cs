using UnityEngine;
using BepInEx.Logging;
using UI.Dialogs;

namespace GreyHackTerminalUI.Canvas
{
    public class CanvasManager
    {
        private static CanvasManager _instance;
        private static ManualLogSource _logger;
        
        private Transform _parentCanvas;
        
        public static CanvasManager Instance => _instance;
        
        public static void Initialize(ManualLogSource logger)
        {
            if (_instance != null)
                return;
                
            _logger = logger;
            _instance = new CanvasManager();
            
            _logger?.LogDebug("CanvasManager initialized");
        }

        public RectTransform GetOrCreateParentCanvas()
        {
            if (_parentCanvas != null)
                return _parentCanvas as RectTransform;

            // Use the game's desktop as parent for native window integration
            var taskBar = Object.FindObjectOfType<uDialog_TaskBar>();
            if (taskBar != null)
            {
                _parentCanvas = taskBar.transform.parent;
                return _parentCanvas as RectTransform;
            }

            var desktop = Object.FindObjectOfType<DesktopFinder>();
            if (desktop != null)
            {
                _parentCanvas = desktop.transform;
                return _parentCanvas as RectTransform;
            }

            // Fallback: create our own canvas if desktop not available
            var canvasGO = new GameObject("CanvasManagerCanvas");
            var canvas = canvasGO.AddComponent<UnityEngine.Canvas>();
            
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            Object.DontDestroyOnLoad(canvasGO);
            _parentCanvas = canvasGO.transform;

            return _parentCanvas as RectTransform;
        }
        
        public CanvasWindow CreateWindow(int terminalPID)
        {
            var parent = GetOrCreateParentCanvas();
            return CanvasWindow.Create(parent, terminalPID);
        }
    }
}

