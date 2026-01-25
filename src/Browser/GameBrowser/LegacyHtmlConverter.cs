using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx.Logging;
using HtmlAgilityPack;

namespace GreyHackTerminalUI.Browser.GameBrowser
{
    public static class LegacyHtmlConverter
    {
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("LegacyHtmlConverter");

        public static string Convert(string html)
        {
            if (string.IsNullOrEmpty(html))
                return html;

            try
            {
                var doc = new HtmlDocument();
                doc.OptionWriteEmptyNodes = true; // Write <br /> not <br>
                doc.OptionOutputAsXml = false;
                doc.LoadHtml(html);

                bool modified = false;

                // Convert <font> tags to <span> with inline styles
                modified |= ConvertFontTags(doc);

                // Convert <center> tags to <div style="text-align:center">
                modified |= ConvertCenterTags(doc);

                // Fix malformed attributes like <div padding:11px;"> 
                // (HAP won't catch these since they're not valid attributes)
                // We handle this with a pre-pass regex

                if (modified)
                {
                    return doc.DocumentNode.OuterHtml;
                }

                return html;
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Legacy HTML conversion failed: {ex.Message}");
                return html;
            }
        }

        private static bool ConvertFontTags(HtmlDocument doc)
        {
            var fontNodes = doc.DocumentNode.SelectNodes("//font");
            if (fontNodes == null || fontNodes.Count == 0)
                return false;

            foreach (var font in fontNodes.ToList())
            {
                var styles = new List<string>();

                // Convert size attribute
                var size = font.GetAttributeValue("size", null);
                if (!string.IsNullOrEmpty(size))
                {
                    // HTML4 font sizes: 1-7 or relative (+1, -1, etc.)
                    // PowerUI seems to use raw pixel values, so treat as pixels
                    if (int.TryParse(size, out int sizeValue))
                    {
                        // If it's a small number (1-7), it might be HTML4 size scale
                        // But PowerUI uses direct pixel values, so just use as-is
                        if (sizeValue <= 7)
                        {
                            // Convert HTML4 size scale to approximate pixels
                            // 1=10px, 2=13px, 3=16px(default), 4=18px, 5=24px, 6=32px, 7=48px
                            int[] sizeMap = { 10, 10, 13, 16, 18, 24, 32, 48 };
                            sizeValue = sizeValue >= 1 && sizeValue <= 7 ? sizeMap[sizeValue] : sizeValue;
                        }
                        styles.Add($"font-size:{sizeValue}px");
                    }
                    else
                    {
                        // Might be a relative size like +1 or -1, just pass through
                        styles.Add($"font-size:{size}");
                    }
                }

                // Convert color attribute
                var color = font.GetAttributeValue("color", null);
                if (!string.IsNullOrEmpty(color))
                {
                    styles.Add($"color:{color}");
                }

                // Convert face attribute
                var face = font.GetAttributeValue("face", null);
                if (!string.IsNullOrEmpty(face))
                {
                    styles.Add($"font-family:{face}");
                }

                // Create replacement span
                var span = doc.CreateElement("span");
                
                if (styles.Count > 0)
                {
                    span.SetAttributeValue("style", string.Join("; ", styles));
                }

                // Copy any other attributes (like class, id)
                foreach (var attr in font.Attributes)
                {
                    if (attr.Name != "size" && attr.Name != "color" && attr.Name != "face")
                    {
                        span.SetAttributeValue(attr.Name, attr.Value);
                    }
                }

                // Move children to span
                foreach (var child in font.ChildNodes.ToList())
                {
                    span.AppendChild(child);
                }

                // Replace font with span
                font.ParentNode.ReplaceChild(span, font);
            }

            Log.LogDebug($"Converted {fontNodes.Count} <font> tags to <span>");
            return true;
        }

        private static bool ConvertCenterTags(HtmlDocument doc)
        {
            var centerNodes = doc.DocumentNode.SelectNodes("//center");
            if (centerNodes == null || centerNodes.Count == 0)
                return false;

            foreach (var center in centerNodes.ToList())
            {
                var div = doc.CreateElement("div");
                
                // Merge with existing style if present
                var existingStyle = center.GetAttributeValue("style", "");
                var newStyle = string.IsNullOrEmpty(existingStyle) 
                    ? "text-align:center" 
                    : $"text-align:center; {existingStyle}";
                div.SetAttributeValue("style", newStyle);

                // Copy other attributes
                foreach (var attr in center.Attributes)
                {
                    if (attr.Name != "style")
                    {
                        div.SetAttributeValue(attr.Name, attr.Value);
                    }
                }

                // Move children
                foreach (var child in center.ChildNodes.ToList())
                {
                    div.AppendChild(child);
                }

                center.ParentNode.ReplaceChild(div, center);
            }

            Log.LogDebug($"Converted {centerNodes.Count} <center> tags to <div>");
            return true;
        }

        public static string PreProcess(string html)
        {
            if (string.IsNullOrEmpty(html))
                return html;

            // Fix invalid attribute syntax like <div padding:11px;"> 
            // Convert to style attribute: <div style="padding:11px;">
            var result = Regex.Replace(
                html,
                @"<(\w+)\s+([\w-]+:\s*[^""'>\s]+;?)\s*("">|""\s|>)",
                m =>
                {
                    string tag = m.Groups[1].Value;
                    string cssLike = m.Groups[2].Value;
                    string ending = m.Groups[3].Value;

                    // Check if it looks like CSS (contains : and doesn't have =)
                    if (cssLike.Contains(":") && !cssLike.Contains("="))
                    {
                        // Clean up the ending - remove stray quotes
                        string cleanEnding = ending.TrimStart('"');
                        return $"<{tag} style=\"{cssLike}\"{cleanEnding}";
                    }
                    return m.Value;
                },
                RegexOptions.IgnoreCase);

            // Fix orphan </div> at the start (seen in CurrencyCreation, NetServices)
            result = Regex.Replace(result, @"^\s*</div>\s*", "", RegexOptions.IgnoreCase);

            return result;
        }
    }
}
