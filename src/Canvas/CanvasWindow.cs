using UnityEngine;
using UnityEngine.UI;
using UI.Dialogs;
using GreyHackTerminalUI.Utils;

namespace GreyHackTerminalUI.Canvas
{
    public class CanvasWindow
    {
        private uDialog _dialog;
        private RawImage _canvasImage;
        private CanvasRenderer _canvasRenderer;
        private int _terminalPID = -1;
        private bool _isVisible = false;

        private const int DEFAULT_WIDTH = 320;
        private const int DEFAULT_HEIGHT = 240;

        public int TerminalPID => _terminalPID;
        public bool IsVisible => _isVisible;
        public CanvasRenderer Renderer => _canvasRenderer;
        public uDialog Dialog => _dialog;

        public static CanvasWindow Create(RectTransform parent, int terminalPID)
        {
            var window = new CanvasWindow();
            window._terminalPID = terminalPID;
            window.CreateDialog(parent);
            return window;
        }

        private void CreateDialog(RectTransform parent)
        {
            // Create dialog without layout group - canvas needs free positioning
            _dialog = DialogBuilder.Create(parent, "Canvas", new Vector2(DEFAULT_WIDTH + 20, DEFAULT_HEIGHT + 50), useLayoutGroup: false);
            
            if (_dialog == null)
            {
                Debug.LogError("[CanvasWindow] Failed to create dialog");
                return;
            }

            // Get content area and add our canvas image
            var contentArea = DialogBuilder.GetContentArea(_dialog);
            if (contentArea != null)
            {
                CreateCanvasImage(contentArea);
            }
            else
            {
                Debug.LogWarning("[CanvasWindow] Could not find content area");
            }

            // Register close event
            _dialog.Event_OnClose.AddListener(OnDialogClosed);

            // Initialize the renderer
            _canvasRenderer = new CanvasRenderer(DEFAULT_WIDTH, DEFAULT_HEIGHT);
            _canvasRenderer.Clear(Color.black);
            _canvasRenderer.Render();

            if (_canvasImage != null)
            {
                _canvasImage.texture = _canvasRenderer.Texture;
            }

            // Start hidden
            _dialog.gameObject.SetActive(false);
        }

        private void CreateCanvasImage(RectTransform parent)
        {
            // Reset parent to proper anchors for centering content
            parent.anchorMin = Vector2.zero;
            parent.anchorMax = Vector2.one;
            parent.pivot = new Vector2(0.5f, 0.5f);
            parent.offsetMin = Vector2.zero;
            parent.offsetMax = Vector2.zero;
            
            // Canvas image (displays the rendered texture) - directly on parent, centered
            var canvasImageGO = new GameObject("CanvasImage");
            canvasImageGO.transform.SetParent(parent, false);

            var canvasImageRect = canvasImageGO.AddComponent<RectTransform>();
            // Center anchors
            canvasImageRect.anchorMin = new Vector2(0.5f, 0.5f);
            canvasImageRect.anchorMax = new Vector2(0.5f, 0.5f);
            canvasImageRect.pivot = new Vector2(0.5f, 0.5f);
            canvasImageRect.sizeDelta = new Vector2(DEFAULT_WIDTH, DEFAULT_HEIGHT);
            canvasImageRect.anchoredPosition = Vector2.zero;

            _canvasImage = canvasImageGO.AddComponent<RawImage>();
            _canvasImage.color = Color.white;

            // Add an updater component to handle texture updates
            var updater = canvasImageGO.AddComponent<CanvasUpdater>();
            updater.Initialize(this);
        }

        private void OnDialogClosed(uDialog dialog)
        {
            _isVisible = false;
            // Ensure the dialog is properly hidden (deactivated) for re-show later
            if (_dialog != null && _dialog.gameObject != null)
            {
                _dialog.gameObject.SetActive(false);
            }
        }

        public void Update()
        {
            // Apply any pending texture updates on the main thread
            if (_canvasRenderer != null && _canvasRenderer.NeedsApply)
            {
                _canvasRenderer.ApplyTexture();
            }
        }

        public void Show()
        {
            if (_dialog == null)
            {
                Debug.LogWarning("[CanvasWindow] Show called but dialog is null");
                return;
            }

            Debug.Log($"[CanvasWindow] Show() called for terminal {_terminalPID}");
            
            _dialog.gameObject.SetActive(true);
            _dialog.Show();
            _dialog.Focus();
            _isVisible = true;
            
            Debug.Log($"[CanvasWindow] Window shown, isVisible={_dialog.isVisible}, gameObject.active={_dialog.gameObject.activeSelf}");
        }

        public void Hide()
        {
            if (_dialog == null) return;
            _dialog.Close();
            _isVisible = false;
        }

        public void SetTitle(string title)
        {
            _dialog?.SetTitleText(title);
        }

        public void SetSize(int width, int height)
        {
            _canvasRenderer?.Resize(width, height);
            _canvasRenderer?.Clear(Color.black);
            _canvasRenderer?.Render();

            if (_canvasImage != null && _canvasRenderer != null)
            {
                _canvasImage.texture = _canvasRenderer.Texture;

                // Update the image size
                var imageRect = _canvasImage.GetComponent<RectTransform>();
                if (imageRect != null)
                {
                    imageRect.sizeDelta = new Vector2(width, height);
                }
            }

            // Update dialog window size
            if (_dialog != null)
            {
                var rectTransform = _dialog.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.sizeDelta = new Vector2(width + 20, height + 50);
                }
            }
        }

        public void BringToFront()
        {
            _dialog?.Focus();
        }

        public void Destroy()
        {
            _canvasRenderer?.Destroy();
            if (_dialog != null)
            {
                Object.Destroy(_dialog.gameObject);
                _dialog = null;
            }
        }
    }

    public class CanvasUpdater : MonoBehaviour
    {
        private CanvasWindow _canvasWindow;

        public void Initialize(CanvasWindow canvasWindow)
        {
            _canvasWindow = canvasWindow;
        }

        private void Update()
        {
            _canvasWindow?.Update();
        }
    }
}
