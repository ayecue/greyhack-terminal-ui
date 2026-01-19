using System;
using UnityEngine;
using GreyHackTerminalUI.Browser.Core;

namespace GreyHackTerminalUI.Browser.Window
{
    public class UltralightBrowser : UltralightBrowserCore
    {
        // Events for UI integration
        public event Action<string> OnUrlChanged;
        public event Action<string> OnTitleChanged;
        public event Action OnLoadStarted;
        public event Action OnLoadFinished;
        public event Action<string, string, int> OnLoadFailed;
        public event Action OnDOMReady;
        public event Action<ULMessageLevel, string, string, int, int> OnConsoleMessage;

        // Current state
        public string CurrentUrl { get; private set; } = "";
        public string CurrentTitle { get; private set; } = "New Tab";
        public bool IsLoading { get; private set; }

        public UltralightBrowser(int width, int height)
            : base("UltralightBrowser", width, height, "ulview")
        {
            // Subscribe to global console messages and filter for this view
            UltralightManager.OnConsoleMessage += HandleConsoleMessage;
            
            // Subscribe to load events
            UltralightManager.OnLoadEvent += HandleLoadEvent;
        }

        private void HandleConsoleMessage(string viewName, ULMessageLevel level, string message, string sourceId, int lineNumber, int columnNumber)
        {
            // Only process messages for this view
            if (viewName != ViewName) return;

            // Dispatch to subscribers
            OnConsoleMessage?.Invoke(level, message, sourceId, lineNumber, columnNumber);
        }

        private void HandleLoadEvent(string viewName, ULLoadEventType eventType, ulong frameId, 
                                      string url, string errorDescription, string errorDomain, int errorCode)
        {
            if (viewName != ViewName) return;

            switch (eventType)
            {
                case ULLoadEventType.BeginLoading:
                    IsLoading = true;
                    CurrentUrl = url;
                    OnUrlChanged?.Invoke(url);
                    OnLoadStarted?.Invoke();
                    break;
                    
                case ULLoadEventType.FinishLoading:
                    IsLoading = false;
                    OnLoadFinished?.Invoke();
                    break;
                    
                case ULLoadEventType.FailLoading:
                    IsLoading = false;
                    Log.LogError($"Load failed: {errorDescription} ({errorDomain}:{errorCode})");
                    OnLoadFailed?.Invoke(errorDescription, errorDomain, errorCode);
                    break;
                    
                case ULLoadEventType.DOMReady:
                    OnDOMReady?.Invoke();
                    break;
            }
        }

        public override void Focus()
        {
            if (State != ViewState.Active) return;
            Log.LogInfo($"Focus() called on {ViewName}");
            base.Focus();
        }

        public override void Unfocus()
        {
            if (State != ViewState.Active) return;
            Log.LogInfo($"Unfocus() called on {ViewName}");
            base.Unfocus();
        }

        public override bool HasFocus()
        {
            if (State != ViewState.Active) return false;
            bool result = base.HasFocus();
            Log.LogInfo($"HasFocus() on {ViewName} = {result}");
            return result;
        }

        [Obsolete("LoadUrl is disabled for security. Use LoadHtml instead.", true)]
        public void LoadUrl(string url)
        {
            Log.LogWarning("LoadUrl is DISABLED for security. Use LoadHtml to provide content directly.");
            throw new NotSupportedException("LoadUrl is disabled for security. External network requests are blocked. Use LoadHtml() instead.");
        }

        public override void LoadHtml(string html)
        {
            if (State != ViewState.Active) return;
            base.LoadHtml(html);
        }

        public override bool UpdateTexture()
        {
            bool updated = base.UpdateTexture();
            return updated;
        }

        public void GoBack()
        {
            ExecuteJavaScript("history.back()");
        }

        public void GoForward()
        {
            ExecuteJavaScript("history.forward()");
        }

        public void Reload()
        {
            ExecuteJavaScript("location.reload()");
        }

        protected override void OnDisposing()
        {
            UltralightManager.OnConsoleMessage -= HandleConsoleMessage;
            UltralightManager.OnLoadEvent -= HandleLoadEvent;
        }
    }
}
