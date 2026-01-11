using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace GreyHackTerminalUI.Canvas
{
    public class CanvasWindow : MonoBehaviour
    {
        private RectTransform _windowRect;
        private RectTransform _titleBar;
        private RectTransform _contentArea;
        private RawImage _canvasImage;
        private TextMeshProUGUI _titleText;
        private Button _closeButton;
        private CanvasRenderer _canvasRenderer;

        private int _terminalPID = -1;
        private bool _isVisible = false;
        private bool _isDragging = false;
        private Vector2 _dragOffset;

        private const int DEFAULT_WIDTH = 320;
        private const int DEFAULT_HEIGHT = 240;
        private const int TITLE_BAR_HEIGHT = 25;
        private const int WINDOW_PADDING = 5;

        public int TerminalPID => _terminalPID;
        public bool IsVisible => _isVisible;
        public CanvasRenderer Renderer => _canvasRenderer;

        public static CanvasWindow Create(Transform parent, int terminalPID)
        {
            // Create the window GameObject
            GameObject windowGO = new GameObject($"CanvasWindow_{terminalPID}");
            windowGO.transform.SetParent(parent, false);

            // Add RectTransform
            RectTransform windowRect = windowGO.AddComponent<RectTransform>();
            windowRect.sizeDelta = new Vector2(DEFAULT_WIDTH + WINDOW_PADDING * 2, DEFAULT_HEIGHT + TITLE_BAR_HEIGHT + WINDOW_PADDING * 2);
            windowRect.anchorMin = new Vector2(0.5f, 0.5f);
            windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.anchoredPosition = Vector2.zero;

            // Add CanvasWindow component
            CanvasWindow canvasWindow = windowGO.AddComponent<CanvasWindow>();
            canvasWindow._terminalPID = terminalPID;
            canvasWindow._windowRect = windowRect;

            // Build the UI
            canvasWindow.BuildUI();

            // Initialize the renderer
            canvasWindow._canvasRenderer = new CanvasRenderer(DEFAULT_WIDTH, DEFAULT_HEIGHT);
            canvasWindow._canvasRenderer.Clear(Color.black);
            canvasWindow._canvasRenderer.Render();
            canvasWindow._canvasImage.texture = canvasWindow._canvasRenderer.Texture;

            // Start hidden
            windowGO.SetActive(false);

            return canvasWindow;
        }

        private void BuildUI()
        {
            // Main window background
            Image windowBg = gameObject.AddComponent<Image>();
            windowBg.color = new Color(0.15f, 0.15f, 0.15f, 0.95f);

            // Add Canvas Group for interactions
            CanvasGroup canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            // Create title bar
            CreateTitleBar();

            // Create content area with canvas
            CreateContentArea();
        }

        private void CreateTitleBar()
        {
            // Title bar container
            GameObject titleBarGO = new GameObject("TitleBar");
            titleBarGO.transform.SetParent(transform, false);

            _titleBar = titleBarGO.AddComponent<RectTransform>();
            _titleBar.anchorMin = new Vector2(0, 1);
            _titleBar.anchorMax = new Vector2(1, 1);
            _titleBar.pivot = new Vector2(0.5f, 1);
            _titleBar.sizeDelta = new Vector2(0, TITLE_BAR_HEIGHT);
            _titleBar.anchoredPosition = Vector2.zero;

            // Title bar background
            Image titleBg = titleBarGO.AddComponent<Image>();
            titleBg.color = new Color(0.2f, 0.4f, 0.2f, 1f); // Green-ish to match Grey Hack theme

            // Make title bar draggable
            TitleBarDrag dragHandler = titleBarGO.AddComponent<TitleBarDrag>();
            dragHandler.Initialize(this);

            // Title text
            GameObject titleTextGO = new GameObject("TitleText");
            titleTextGO.transform.SetParent(titleBarGO.transform, false);

            RectTransform titleTextRect = titleTextGO.AddComponent<RectTransform>();
            titleTextRect.anchorMin = new Vector2(0, 0);
            titleTextRect.anchorMax = new Vector2(1, 1);
            titleTextRect.offsetMin = new Vector2(10, 0);
            titleTextRect.offsetMax = new Vector2(-30, 0);

            _titleText = titleTextGO.AddComponent<TextMeshProUGUI>();
            _titleText.text = "Canvas";
            _titleText.fontSize = 14;
            _titleText.color = Color.white;
            _titleText.alignment = TextAlignmentOptions.MidlineLeft;

            // Close button
            GameObject closeButtonGO = new GameObject("CloseButton");
            closeButtonGO.transform.SetParent(titleBarGO.transform, false);

            RectTransform closeButtonRect = closeButtonGO.AddComponent<RectTransform>();
            closeButtonRect.anchorMin = new Vector2(1, 0.5f);
            closeButtonRect.anchorMax = new Vector2(1, 0.5f);
            closeButtonRect.pivot = new Vector2(1, 0.5f);
            closeButtonRect.sizeDelta = new Vector2(20, 20);
            closeButtonRect.anchoredPosition = new Vector2(-5, 0);

            Image closeButtonBg = closeButtonGO.AddComponent<Image>();
            closeButtonBg.color = new Color(0.8f, 0.2f, 0.2f, 1f);

            _closeButton = closeButtonGO.AddComponent<Button>();
            _closeButton.targetGraphic = closeButtonBg;
            _closeButton.onClick.AddListener(Hide);

            // X text on close button
            GameObject closeTextGO = new GameObject("CloseText");
            closeTextGO.transform.SetParent(closeButtonGO.transform, false);

            RectTransform closeTextRect = closeTextGO.AddComponent<RectTransform>();
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.offsetMin = Vector2.zero;
            closeTextRect.offsetMax = Vector2.zero;

            TextMeshProUGUI closeText = closeTextGO.AddComponent<TextMeshProUGUI>();
            closeText.text = "×";
            closeText.fontSize = 16;
            closeText.color = Color.white;
            closeText.alignment = TextAlignmentOptions.Center;
        }

        private void CreateContentArea()
        {
            // Content area container
            GameObject contentGO = new GameObject("ContentArea");
            contentGO.transform.SetParent(transform, false);

            _contentArea = contentGO.AddComponent<RectTransform>();
            _contentArea.anchorMin = new Vector2(0, 0);
            _contentArea.anchorMax = new Vector2(1, 1);
            _contentArea.offsetMin = new Vector2(WINDOW_PADDING, WINDOW_PADDING);
            _contentArea.offsetMax = new Vector2(-WINDOW_PADDING, -TITLE_BAR_HEIGHT);

            // Canvas image (displays the rendered texture)
            GameObject canvasImageGO = new GameObject("CanvasImage");
            canvasImageGO.transform.SetParent(contentGO.transform, false);

            RectTransform canvasImageRect = canvasImageGO.AddComponent<RectTransform>();
            canvasImageRect.anchorMin = Vector2.zero;
            canvasImageRect.anchorMax = Vector2.one;
            canvasImageRect.offsetMin = Vector2.zero;
            canvasImageRect.offsetMax = Vector2.zero;

            _canvasImage = canvasImageGO.AddComponent<RawImage>();
            _canvasImage.color = Color.white;
        }

        private void Update()
        {
            // Apply any pending texture updates on the main thread
            if (_canvasRenderer != null && _canvasRenderer.NeedsApply)
            {
                _canvasRenderer.ApplyTexture();
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
            _isVisible = true;
            BringToFront();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            _isVisible = false;
        }

        public void SetTitle(string title)
        {
            if (_titleText != null)
            {
                _titleText.text = title;
            }
        }
        
        public void SetSize(int width, int height)
        {
            _canvasRenderer?.Resize(width, height);
            _canvasRenderer?.Clear(Color.black);
            _canvasRenderer?.Render();

            if (_canvasImage != null && _canvasRenderer != null)
            {
                _canvasImage.texture = _canvasRenderer.Texture;
            }

            // Update window size
            if (_windowRect != null)
            {
                _windowRect.sizeDelta = new Vector2(width + WINDOW_PADDING * 2, height + TITLE_BAR_HEIGHT + WINDOW_PADDING * 2);
            }
        }

        public void BringToFront()
        {
            transform.SetAsLastSibling();
        }

        public void OnBeginDrag(Vector2 pointerPosition)
        {
            _isDragging = true;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _windowRect.parent as RectTransform,
                pointerPosition,
                null,
                out Vector2 localPoint
            );
            _dragOffset = (Vector2)_windowRect.anchoredPosition - localPoint;
            BringToFront();
        }

        public void OnDrag(Vector2 pointerPosition)
        {
            if (!_isDragging) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _windowRect.parent as RectTransform,
                pointerPosition,
                null,
                out Vector2 localPoint
            );
            _windowRect.anchoredPosition = localPoint + _dragOffset;
        }

        public void OnEndDrag()
        {
            _isDragging = false;
        }

        private void OnDestroy()
        {
            _canvasRenderer?.Destroy();
        }
    }

    public class TitleBarDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler
    {
        private CanvasWindow _canvasWindow;

        public void Initialize(CanvasWindow canvasWindow)
        {
            _canvasWindow = canvasWindow;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _canvasWindow?.BringToFront();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _canvasWindow?.OnBeginDrag(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _canvasWindow?.OnDrag(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _canvasWindow?.OnEndDrag();
        }
    }
}
