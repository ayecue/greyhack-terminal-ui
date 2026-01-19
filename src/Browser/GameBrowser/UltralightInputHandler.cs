using System;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GreyHackTerminalUI.Browser.Core;

namespace GreyHackTerminalUI.Browser.GameBrowser
{
    public class UltralightInputHandler : MonoBehaviour, 
        IPointerDownHandler, 
        IPointerUpHandler, 
        IPointerMoveHandler,
        IScrollHandler,
        IDragHandler,
        IPointerEnterHandler,
        IPointerExitHandler,
        ISelectHandler,
        IDeselectHandler
    {
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("ULInputHandler");

        private UltralightHtmlBridge _bridge;
        private RectTransform _rectTransform;
        private bool _isDragging;
        private bool _isPointerInside;
        private bool _hasFocus;

        public void Initialize(UltralightHtmlBridge bridge)
        {
            _bridge = bridge;
            _rectTransform = GetComponent<RectTransform>();

            // Ensure there's a Graphic component for raycasting
            var graphic = GetComponent<Graphic>();
            if (graphic != null)
            {
                graphic.raycastTarget = true;
            }
        }

        private void OnGUI()
        {
            // Handle keyboard input via OnGUI for proper key events
            if (_bridge == null || !_hasFocus) return;

            var ev = Event.current;
            if (ev.type == EventType.KeyDown || ev.type == EventType.KeyUp)
            {                
                // Check for Cmd/Ctrl+C to copy selection to game clipboard
                if (ev.type == EventType.KeyDown && 
                    (ev.keyCode == KeyCode.C) && 
                    (ev.command || ev.control))
                {
                    _bridge.CopySelectionToClipboard();
                    ev.Use();
                    return;
                }

                _bridge.HandleKeyEvent(ev);
                ev.Use();
            }
        }

        private Vector2 GetLocalPositionFromScreen(Vector2 screenPos)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform,
                screenPos,
                null, // screen space overlay
                out Vector2 localPoint
            );

            // Convert from center-origin to top-left origin
            var rect = _rectTransform.rect;
            float x = localPoint.x + rect.width / 2;
            float y = rect.height / 2 - localPoint.y; // Flip Y

            return new Vector2(x, y);
        }

        private Vector2 GetLocalPosition(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint
            );

            // Convert from center-origin to top-left origin
            var rect = _rectTransform.rect;
            float x = localPoint.x + rect.width / 2;
            float y = rect.height / 2 - localPoint.y; // Flip Y

            return new Vector2(x, y);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_bridge == null) return;

            // Focus the view for text selection
            _bridge.Focus();
            _hasFocus = true;

            // Select this object in the EventSystem
            EventSystem.current.SetSelectedGameObject(gameObject);

            var localPos = GetLocalPosition(eventData);
            var button = ConvertButton(eventData.button);
            _bridge.HandleMouseEvent(localPos, ULMouseEventType.MouseDown, button);
            _isDragging = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_bridge == null) return;

            var localPos = GetLocalPosition(eventData);
            var button = ConvertButton(eventData.button);
            _bridge.HandleMouseEvent(localPos, ULMouseEventType.MouseUp, button);
            _isDragging = false;
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (_bridge == null || _isDragging) return; // Drag handles movement during drag

            var localPos = GetLocalPosition(eventData);
            _bridge.HandleMouseEvent(localPos, ULMouseEventType.MouseMoved, ULMouseButton.None);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_bridge == null) return;

            // During drag, send MouseMoved events for text selection
            var localPos = GetLocalPosition(eventData);
            _bridge.HandleMouseEvent(localPos, ULMouseEventType.MouseMoved, ULMouseButton.Left);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isPointerInside = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerInside = false;
        }

        public void OnSelect(BaseEventData eventData)
        {
            _hasFocus = true;
            _bridge?.Focus();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _hasFocus = false;
            _bridge?.Unfocus();
        }

        public void OnScroll(PointerEventData eventData)
        {
        }

        private ULMouseButton ConvertButton(PointerEventData.InputButton button)
        {
            return button switch
            {
                PointerEventData.InputButton.Left => ULMouseButton.Left,
                PointerEventData.InputButton.Right => ULMouseButton.Right,
                PointerEventData.InputButton.Middle => ULMouseButton.Middle,
                _ => ULMouseButton.None
            };
        }

        private void OnDestroy()
        {
            _bridge = null;
        }
    }
}
