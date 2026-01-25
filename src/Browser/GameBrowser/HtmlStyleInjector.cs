using System;
using System.Globalization;
using BepInEx.Logging;
using HtmlAgilityPack;

namespace GreyHackTerminalUI.Browser.GameBrowser
{
    public static class HtmlStyleInjector
    {
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("HtmlStyleInjector");

        public static string Inject(string html, string css, float zoomLevel = 1.0f)
        {
            if (string.IsNullOrEmpty(html))
                return html;

            if (string.IsNullOrEmpty(css) && Math.Abs(zoomLevel - 1.0f) < 0.001f)
                return html;

            try
            {
                var doc = new HtmlDocument();
                doc.OptionWriteEmptyNodes = true;
                doc.OptionOutputAsXml = false;
                doc.LoadHtml(html);

                // Build the complete CSS with zoom if needed
                string fullCss = css ?? "";
                if (Math.Abs(zoomLevel - 1.0f) >= 0.001f)
                {
                    fullCss += $@"
                        /* Zoom emulation - matching PowerUI's devicePixelRatio scaling */
                        html {{
                            zoom: {zoomLevel.ToString(CultureInfo.InvariantCulture)};
                        }}
                    ";
                }

                // Create the style element
                var styleNode = doc.CreateElement("style");
                styleNode.SetAttributeValue("type", "text/css");
                styleNode.InnerHtml = fullCss;

                // Find or create head element
                var headNode = doc.DocumentNode.SelectSingleNode("//head");
                if (headNode != null)
                {
                    // Insert at the beginning of head (so page styles can override)
                    headNode.PrependChild(styleNode);
                    Log.LogDebug("Injected CSS into existing <head>");
                }
                else
                {
                    // No head element - try to find html element
                    var htmlNode = doc.DocumentNode.SelectSingleNode("//html");
                    if (htmlNode != null)
                    {
                        // Create head and insert it as first child of html
                        var newHead = doc.CreateElement("head");
                        newHead.AppendChild(styleNode);
                        htmlNode.PrependChild(newHead);
                        Log.LogDebug("Created <head> and injected CSS");
                    }
                    else
                    {
                        // No html element either - wrap in proper structure or just prepend
                        // For fragments, prepend the style
                        var firstNode = doc.DocumentNode.FirstChild;
                        if (firstNode != null)
                        {
                            doc.DocumentNode.InsertBefore(styleNode, firstNode);
                        }
                        else
                        {
                            doc.DocumentNode.AppendChild(styleNode);
                        }
                        Log.LogDebug("Prepended CSS to document fragment");
                    }
                }

                return doc.DocumentNode.OuterHtml;
            }
            catch (Exception ex)
            {
                Log.LogWarning($"CSS injection failed, falling back to string prepend: {ex.Message}");
                // Fallback: just prepend
                string zoomCss = Math.Abs(zoomLevel - 1.0f) >= 0.001f
                    ? $"html {{ zoom: {zoomLevel.ToString(CultureInfo.InvariantCulture)}; }}"
                    : "";
                return $"<style type=\"text/css\">{css}{zoomCss}</style>{html}";
            }
        }
    }
}
