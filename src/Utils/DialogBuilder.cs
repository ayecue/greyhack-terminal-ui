using UnityEngine;
using UnityEngine.UI;
using UI.Dialogs;

namespace GreyHackTerminalUI.Utils
{
    public static class DialogBuilder
    {
        private static GameObject _cachedPrefab;
        
        public static uDialog Create(RectTransform parent, string title, Vector2 size, bool useLayoutGroup = true)
        {
            // Load prefab from Resources (not PoolPrefabs which requires runtime registration)
            if (_cachedPrefab == null)
            {
                _cachedPrefab = Resources.Load<GameObject>("Prefabs/uDialog_Default");
                if (_cachedPrefab == null)
                {
                    Debug.LogError("[DialogBuilder] Could not load uDialog_Default prefab from Resources");
                    return null;
                }
            }
            
            // Instantiate the prefab
            var dialogGO = Object.Instantiate(_cachedPrefab, parent);
            dialogGO.name = title.Replace(" ", "");
            
            var dialog = dialogGO.GetComponent<uDialog>();
            if (dialog == null)
            {
                Debug.LogError("[DialogBuilder] Prefab does not have uDialog component");
                Object.Destroy(dialogGO);
                return null;
            }
            
            // Configure the dialog
            dialog.SetTitleText(title);
            dialog.ShowTitleCloseButton = true;
            dialog.ShowTitleMinimizeButton = false;
            dialog.ShowTitleMaximizeButton = false;
            dialog.AllowDraggingViaTitle = true;
            dialog.FocusOnShow = true;
            dialog.Modal = false;
            dialog.DestroyAfterClose = true;
            dialog.VisibleOnStart = false;
            
            // Set size
            var rectTransform = dialog.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = size;
                rectTransform.anchoredPosition = Vector2.zero;
            }
            
            // Prepare content area for custom UI
            PrepareContentArea(dialog, useLayoutGroup);
            
            return dialog;
        }
        
        private static void PrepareContentArea(uDialog dialog, bool useLayoutGroup)
        {
            // Hide default Message container (used for text dialogs)
            if (dialog.GO_MessageContainer != null)
            {
                dialog.GO_MessageContainer.SetActive(false);
            }
            
            // Hide Icon container if present
            if (dialog.GO_Icon != null)
            {
                dialog.GO_Icon.gameObject.SetActive(false);
            }
            
            // The Viewport has a VerticalLayoutGroup that controls child positioning
            // For raw content (canvas), we need to remove it so our anchors work
            if (!useLayoutGroup && dialog.GO_Viewport != null)
            {
                var viewportLayout = dialog.GO_Viewport.GetComponent<LayoutGroup>();
                if (viewportLayout != null)
                {
                    Object.Destroy(viewportLayout);
                }
            }
            
            // GO_Content is the designated content area but starts inactive in the prefab
            // Activate it for custom content
            if (dialog.GO_Content != null)
            {
                dialog.GO_Content.SetActive(true);
                
                // Clear any existing children from content
                var contentTransform = dialog.GO_Content.transform;
                for (int i = contentTransform.childCount - 1; i >= 0; i--)
                {
                    Object.Destroy(contentTransform.GetChild(i).gameObject);
                }
                
                // Setup content rect to fill viewport
                var contentRect = dialog.GO_Content.GetComponent<RectTransform>();
                if (contentRect != null)
                {
                    // Anchor to fill parent
                    contentRect.anchorMin = Vector2.zero;
                    contentRect.anchorMax = Vector2.one;
                    contentRect.pivot = new Vector2(0.5f, 0.5f);
                    contentRect.anchoredPosition = Vector2.zero;
                    
                    // Only add padding for layout-based content (forms)
                    // For raw content (canvas), use full area
                    if (useLayoutGroup)
                    {
                        contentRect.offsetMin = new Vector2(10, 10);
                        contentRect.offsetMax = new Vector2(-10, -10);
                    }
                    else
                    {
                        contentRect.offsetMin = Vector2.zero;
                        contentRect.offsetMax = Vector2.zero;
                    }
                }
                
                // Remove any existing layout group/element on GO_Content
                var existingLayout = dialog.GO_Content.GetComponent<LayoutGroup>();
                if (existingLayout != null)
                {
                    Object.Destroy(existingLayout);
                }
                var existingLayoutElement = dialog.GO_Content.GetComponent<LayoutElement>();
                if (existingLayoutElement != null)
                {
                    Object.Destroy(existingLayoutElement);
                }
                
                // Add vertical layout group only if requested (for form-style content)
                if (useLayoutGroup)
                {
                    var layoutGroup = dialog.GO_Content.AddComponent<VerticalLayoutGroup>();
                    layoutGroup.padding = new RectOffset(5, 5, 5, 5);
                    layoutGroup.spacing = 10;
                    layoutGroup.childAlignment = TextAnchor.UpperLeft;
                    layoutGroup.childForceExpandWidth = true;
                    layoutGroup.childForceExpandHeight = false;
                    layoutGroup.childControlWidth = true;
                    layoutGroup.childControlHeight = true;
                }
            }
            else
            {
                Debug.LogWarning("[DialogBuilder] GO_Content is null - custom content may not display correctly");
            }
        }
        public static RectTransform GetContentArea(uDialog dialog)
        {
            if (dialog == null) return null;
            
            // Use GO_Content directly - this is the designated custom content area
            if (dialog.GO_Content != null)
            {
                return dialog.GO_Content.GetComponent<RectTransform>();
            }
            
            // Fallback to viewport transform
            if (dialog.GO_Viewport != null)
            {
                return dialog.GO_Viewport.transform as RectTransform;
            }
            
            Debug.LogWarning("[DialogBuilder] Could not find content area in dialog");
            return null;
        }
    }
}
