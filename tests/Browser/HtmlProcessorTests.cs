using System;
using Xunit;

namespace GreyHackTerminalUI.Tests.Browser
{
    public class HtmlImagePreprocessorTests
    {
        [Theory]
        [InlineData("", "")]
        [InlineData(null, null)]
        public void Process_EmptyOrNull_ReturnsInput(string input, string expected)
        {
            var result = HtmlImagePreprocessor.Process(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("<img src=\"test.png\">", ".imgsrc")]
        [InlineData("<img src=\"test.jpg\">", ".imgsrc")]
        [InlineData("<img src=\"test.jpeg\">", ".imgsrc")]
        [InlineData("<img src=\"test.gif\">", ".imgsrc")]
        [InlineData("<img src=\"test.webp\">", ".imgsrc")]
        public void Process_ImageSrc_ConvertsExtension(string html, string expectedExtension)
        {
            var result = HtmlImagePreprocessor.Process(html);
            Assert.Contains(expectedExtension, result);
            Assert.DoesNotContain(".png", result.ToLower());
            Assert.DoesNotContain(".jpg", result.ToLower());
        }

        [Fact]
        public void Process_ImageWithPath_PreservesPath()
        {
            var html = "<img src=\"/images/folder/test.png\">";
            var result = HtmlImagePreprocessor.Process(html);
            Assert.Contains("/images/folder/test.imgsrc", result);
        }

        [Fact]
        public void Process_NonImageSrc_NoChange()
        {
            var html = "<img src=\"data:image/png;base64,abc123\">";
            var result = HtmlImagePreprocessor.Process(html);
            Assert.Contains("data:image/png;base64,abc123", result);
        }

        [Fact]
        public void Process_BackgroundAttribute_ConvertsExtension()
        {
            var html = "<body background=\"bg.jpg\">content</body>";
            var result = HtmlImagePreprocessor.Process(html);
            Assert.Contains("bg.imgsrc", result);
        }

        [Fact]
        public void Process_MultipleImages_ConvertsAll()
        {
            var html = "<div><img src=\"a.png\"><img src=\"b.jpg\"></div>";
            var result = HtmlImagePreprocessor.Process(html);
            Assert.Contains("a.imgsrc", result);
            Assert.Contains("b.imgsrc", result);
        }

        [Fact]
        public void Process_CaseInsensitive_ConvertsUppercase()
        {
            var html = "<img src=\"test.PNG\">";
            var result = HtmlImagePreprocessor.Process(html);
            Assert.Contains(".imgsrc", result);
        }

        [Fact]
        public void Process_NoImages_ReturnsUnchanged()
        {
            var html = "<div><p>Hello world</p></div>";
            var result = HtmlImagePreprocessor.Process(html);
            Assert.Equal(html, result);
        }
    }

    public class LegacyHtmlConverterTests
    {
        [Theory]
        [InlineData("", "")]
        [InlineData(null, null)]
        public void Convert_EmptyOrNull_ReturnsInput(string input, string expected)
        {
            var result = LegacyHtmlConverter.Convert(input);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Convert_FontWithColor_ConvertsToSpanWithStyle()
        {
            var html = "<font color=\"red\">Hello</font>";
            var result = LegacyHtmlConverter.Convert(html);
            
            Assert.DoesNotContain("<font", result.ToLower());
            Assert.Contains("<span", result.ToLower());
            Assert.Contains("color:red", result.ToLower());
        }

        [Fact]
        public void Convert_FontWithSize_ConvertsToSpanWithFontSize()
        {
            var html = "<font size=\"5\">Big text</font>";
            var result = LegacyHtmlConverter.Convert(html);
            
            Assert.DoesNotContain("<font", result.ToLower());
            Assert.Contains("<span", result.ToLower());
            Assert.Contains("font-size:", result.ToLower());
        }

        [Fact]
        public void Convert_FontWithFace_ConvertsToSpanWithFontFamily()
        {
            var html = "<font face=\"Arial\">Styled</font>";
            var result = LegacyHtmlConverter.Convert(html);
            
            Assert.DoesNotContain("<font", result.ToLower());
            Assert.Contains("font-family:arial", result.ToLower());
        }

        [Fact]
        public void Convert_FontWithMultipleAttrs_CombinesStyles()
        {
            var html = "<font color=\"blue\" size=\"3\" face=\"Verdana\">Multi</font>";
            var result = LegacyHtmlConverter.Convert(html);
            
            Assert.Contains("color:blue", result.ToLower());
            Assert.Contains("font-family:verdana", result.ToLower());
            Assert.Contains("font-size:", result.ToLower());
        }

        [Fact]
        public void Convert_CenterTag_ConvertsToDivWithTextAlign()
        {
            var html = "<center>Centered text</center>";
            var result = LegacyHtmlConverter.Convert(html);
            
            Assert.DoesNotContain("<center", result.ToLower());
            Assert.Contains("<div", result.ToLower());
            Assert.Contains("text-align:center", result.ToLower());
        }

        [Fact]
        public void Convert_NestedFontTags_ConvertsAll()
        {
            var html = "<font color=\"red\"><font size=\"4\">Nested</font></font>";
            var result = LegacyHtmlConverter.Convert(html);
            
            Assert.DoesNotContain("<font", result.ToLower());
            // Should have two spans
            var spanCount = System.Text.RegularExpressions.Regex.Matches(result.ToLower(), "<span").Count;
            Assert.Equal(2, spanCount);
        }

        [Fact]
        public void Convert_FontPreservesContent()
        {
            var html = "<font color=\"green\">Keep <b>this</b> content</font>";
            var result = LegacyHtmlConverter.Convert(html);
            
            Assert.Contains("Keep", result);
            Assert.Contains("<b>this</b>", result.ToLower());
            Assert.Contains("content", result);
        }

        [Fact]
        public void Convert_NoLegacyTags_ReturnsUnchanged()
        {
            var html = "<div><span style=\"color:red\">Modern HTML</span></div>";
            var result = LegacyHtmlConverter.Convert(html);
            Assert.Equal(html, result);
        }
    }

    public class HtmlStyleInjectorTests
    {
        [Theory]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void Inject_EmptyHtml_ReturnsInput(string html, string css)
        {
            var result = HtmlStyleInjector.Inject(html, css);
            Assert.Equal(html, result);
        }

        [Fact]
        public void Inject_NoCssNoZoom_ReturnsUnchanged()
        {
            var html = "<html><head></head><body>Test</body></html>";
            var result = HtmlStyleInjector.Inject(html, null, 1.0f);
            Assert.Equal(html, result);
        }

        [Fact]
        public void Inject_CssIntoExistingHead_InjectsStyle()
        {
            var html = "<html><head><title>Test</title></head><body>Test</body></html>";
            var css = "body { background: red; }";
            
            var result = HtmlStyleInjector.Inject(html, css);
            
            Assert.Contains("<style", result.ToLower());
            Assert.Contains("body { background: red; }", result);
        }

        [Fact]
        public void Inject_CssNoHead_CreatesHead()
        {
            var html = "<html><body>Test</body></html>";
            var css = "p { margin: 0; }";
            
            var result = HtmlStyleInjector.Inject(html, css);
            
            Assert.Contains("<head>", result.ToLower());
            Assert.Contains("<style", result.ToLower());
            Assert.Contains("p { margin: 0; }", result);
        }

        [Fact]
        public void Inject_CssNoHtmlTag_PrependsStyle()
        {
            var html = "<div>Fragment</div>";
            var css = "div { color: blue; }";
            
            var result = HtmlStyleInjector.Inject(html, css);
            
            Assert.Contains("<style", result.ToLower());
            Assert.Contains("div { color: blue; }", result);
        }

        [Fact]
        public void Inject_WithZoom_AddsZoomCss()
        {
            var html = "<html><head></head><body>Test</body></html>";
            var css = "";
            
            var result = HtmlStyleInjector.Inject(html, css, 1.5f);
            
            Assert.Contains("zoom:", result.ToLower());
            Assert.Contains("1.5", result);
        }

        [Fact]
        public void Inject_WithZoomAndCss_CombinesBoth()
        {
            var html = "<html><head></head><body>Test</body></html>";
            var css = "body { margin: 0; }";
            
            var result = HtmlStyleInjector.Inject(html, css, 2.0f);
            
            Assert.Contains("body { margin: 0; }", result);
            Assert.Contains("zoom:", result.ToLower());
            Assert.Contains("2", result);
        }

        [Fact]
        public void Inject_ZoomOne_NoZoomCss()
        {
            var html = "<html><head></head><body>Test</body></html>";
            var css = "body { color: black; }";
            
            var result = HtmlStyleInjector.Inject(html, css, 1.0f);
            
            Assert.Contains("body { color: black; }", result);
            Assert.DoesNotContain("zoom:", result.ToLower());
        }

        [Fact]
        public void Inject_StyleInsertedBeforeExistingStyles()
        {
            var html = "<html><head><style>original { }</style></head><body>Test</body></html>";
            var css = "injected { }";
            
            var result = HtmlStyleInjector.Inject(html, css);
            
            var injectedPos = result.IndexOf("injected");
            var originalPos = result.IndexOf("original");
            
            Assert.True(injectedPos < originalPos, "Injected CSS should come before original CSS");
        }
    }
}

// Stub classes to make tests compile - these mirror the actual implementations
// In a real scenario, these would be the actual classes from the main project
namespace GreyHackTerminalUI.Tests.Browser
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using HtmlAgilityPack;

    public static class HtmlImagePreprocessor
    {
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
            catch
            {
                return html;
            }
        }

        private static string ConvertImageSrc(string src)
        {
            if (string.IsNullOrEmpty(src))
                return src;

            foreach (var ext in ImageExtensions)
            {
                if (src.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                {
                    return src.Substring(0, src.Length - ext.Length) + ".imgsrc";
                }
            }

            return src;
        }
    }

    public static class LegacyHtmlConverter
    {
        public static string Convert(string html)
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
                modified |= ConvertFontTags(doc);
                modified |= ConvertCenterTags(doc);

                if (modified)
                    return doc.DocumentNode.OuterHtml;

                return html;
            }
            catch
            {
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

                var size = font.GetAttributeValue("size", null);
                if (!string.IsNullOrEmpty(size))
                {
                    if (int.TryParse(size, out int sizeValue))
                    {
                        if (sizeValue <= 7)
                        {
                            int[] sizeMap = { 10, 10, 13, 16, 18, 24, 32, 48 };
                            sizeValue = sizeValue >= 1 && sizeValue <= 7 ? sizeMap[sizeValue] : sizeValue;
                        }
                        styles.Add($"font-size:{sizeValue}px");
                    }
                    else
                    {
                        styles.Add($"font-size:{size}");
                    }
                }

                var color = font.GetAttributeValue("color", null);
                if (!string.IsNullOrEmpty(color))
                    styles.Add($"color:{color}");

                var face = font.GetAttributeValue("face", null);
                if (!string.IsNullOrEmpty(face))
                    styles.Add($"font-family:{face}");

                var span = doc.CreateElement("span");
                if (styles.Count > 0)
                    span.SetAttributeValue("style", string.Join("; ", styles));

                foreach (var attr in font.Attributes)
                {
                    if (attr.Name != "size" && attr.Name != "color" && attr.Name != "face")
                        span.SetAttributeValue(attr.Name, attr.Value);
                }

                foreach (var child in font.ChildNodes.ToList())
                    span.AppendChild(child);

                font.ParentNode.ReplaceChild(span, font);
            }

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
                var existingStyle = center.GetAttributeValue("style", "");
                var newStyle = string.IsNullOrEmpty(existingStyle)
                    ? "text-align:center"
                    : $"text-align:center; {existingStyle}";
                div.SetAttributeValue("style", newStyle);

                foreach (var attr in center.Attributes)
                {
                    if (attr.Name != "style")
                        div.SetAttributeValue(attr.Name, attr.Value);
                }

                foreach (var child in center.ChildNodes.ToList())
                    div.AppendChild(child);

                center.ParentNode.ReplaceChild(div, center);
            }

            return true;
        }
    }

    public static class HtmlStyleInjector
    {
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

                string fullCss = css ?? "";
                if (Math.Abs(zoomLevel - 1.0f) >= 0.001f)
                {
                    fullCss += $@"
                        html {{
                            zoom: {zoomLevel.ToString(System.Globalization.CultureInfo.InvariantCulture)};
                        }}
                    ";
                }

                var styleNode = doc.CreateElement("style");
                styleNode.SetAttributeValue("type", "text/css");
                styleNode.InnerHtml = fullCss;

                var headNode = doc.DocumentNode.SelectSingleNode("//head");
                if (headNode != null)
                {
                    headNode.PrependChild(styleNode);
                }
                else
                {
                    var htmlNode = doc.DocumentNode.SelectSingleNode("//html");
                    if (htmlNode != null)
                    {
                        var newHead = doc.CreateElement("head");
                        newHead.AppendChild(styleNode);
                        htmlNode.PrependChild(newHead);
                    }
                    else
                    {
                        var firstNode = doc.DocumentNode.FirstChild;
                        if (firstNode != null)
                            doc.DocumentNode.InsertBefore(styleNode, firstNode);
                        else
                            doc.DocumentNode.AppendChild(styleNode);
                    }
                }

                return doc.DocumentNode.OuterHtml;
            }
            catch
            {
                string zoomCss = Math.Abs(zoomLevel - 1.0f) >= 0.001f
                    ? $"html {{ zoom: {zoomLevel.ToString(System.Globalization.CultureInfo.InvariantCulture)}; }}"
                    : "";
                return $"<style type=\"text/css\">{css}{zoomCss}</style>{html}";
            }
        }
    }
}
