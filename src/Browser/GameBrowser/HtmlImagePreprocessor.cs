using System;
using System.Linq;
using BepInEx.Logging;
using HtmlAgilityPack;

namespace GreyHackTerminalUI.Browser.GameBrowser
{
    public static class HtmlImagePreprocessor
    {
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("HtmlImagePreprocessor");

        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".webp" };

        public static string Process(string html)
        {
            if (string.IsNullOrEmpty(html))
                return html;

            try
            {
                var doc = new HtmlDocument();
                doc.OptionWriteEmptyNodes = true;
                doc.OptionOutputAsXml = false;
                doc.LoadHtml(html);

                bool modified = false;

                // Find all elements with src attribute (img, source, etc.)
                var nodesWithSrc = doc.DocumentNode.SelectNodes("//*[@src]");
                if (nodesWithSrc != null)
                {
                    foreach (var node in nodesWithSrc)
                    {
                        var src = node.GetAttributeValue("src", null);
                        if (!string.IsNullOrEmpty(src))
                        {
                            string newSrc = ConvertImageSrc(src);
                            if (newSrc != src)
                            {
                                node.SetAttributeValue("src", newSrc);
                                modified = true;
                            }
                        }
                    }
                }

                // Also check background attribute (legacy HTML)
                var nodesWithBackground = doc.DocumentNode.SelectNodes("//*[@background]");
                if (nodesWithBackground != null)
                {
                    foreach (var node in nodesWithBackground)
                    {
                        var bg = node.GetAttributeValue("background", null);
                        if (!string.IsNullOrEmpty(bg))
                        {
                            string newBg = ConvertImageSrc(bg);
                            if (newBg != bg)
                            {
                                node.SetAttributeValue("background", newBg);
                                modified = true;
                            }
                        }
                    }
                }

                if (modified)
                    return doc.DocumentNode.OuterHtml;

                return html;
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Image preprocessing failed: {ex.Message}");
                return html;
            }
        }

        private static string ConvertImageSrc(string src)
        {
            if (string.IsNullOrEmpty(src))
                return src;

            // Check if it ends with a known image extension
            foreach (var ext in ImageExtensions)
            {
                if (src.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    // Replace extension with .imgsrc
                    return src.Substring(0, src.Length - ext.Length) + ".imgsrc";
                }
            }

            return src;
        }
    }
}
