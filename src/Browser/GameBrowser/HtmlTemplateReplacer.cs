using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx.Logging;

namespace GreyHackTerminalUI.Browser.GameBrowser
{
    public static class HtmlTemplateReplacer
    {
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("HtmlTemplateReplacer");

        private class TemplateDefinition
        {
            public string Name { get; set; }
            public string DetectionPattern { get; set; } // Unique string to identify this template
            public string FixedHtml { get; set; }
            public bool HasDynamicContent { get; set; } // If true, preserves [WEB_CONTENT]
        }

        private static readonly List<TemplateDefinition> Templates = new List<TemplateDefinition>
        {
            // NetServices (ISP) - has font: 18; and <div padding:11px;">
            new TemplateDefinition
            {
                Name = "NetServices",
                DetectionPattern = "isp.jpg",
                HasDynamicContent = true,
                FixedHtml = @"<!doctype html>
<html>
<head>
<style>
  h1 { font-size: 16px; text-align: center; color: grey;}
  p { color: whitesmoke; }
  body { font: 20px Helvetica, sans-serif; color: #333; margin:0; overflow-y:auto; height:100%; }
  .btn {
    background-color: #006699;
    border: 1px solid grey;
    color: white;
    padding: 8px 8px;
    text-align: center;
    text-decoration: none;
    display: inline-block;
    font-size: 18px;
    width: 130px;
  }
  article { display: block; text-align: left; width: 600px; margin: 0 auto; }  
  html{
    background-color: #10063D;
    height:100%;
  }
  .btn-sel{
      background-color: #008CD1;
  }
  .btn-group button:hover {
      background-color: #008CD1;
  }
  .btn-group{
   padding-top: 4px;
  }
  .logo{
    text-align: center;
    padding: 10px;
  }
  img{
    display: block;
    margin: 0 auto;
  }
</style>
</head>
<body>
<div style=""padding:11px;"">
<div class=""btn-group"" style=""text-align: center;"">
  <button type=""button"" class=""btn btn-primary"" id=""Home"">Main</button>
  <button type=""button"" class=""btn btn-primary"" id=""ISPConfig"">Services</button>
</div>
</div>
<article>
    <div class=""logo text-center""> 
      <p><i>[WEB_CONTENT]</i></p>
      <img src=""isp.jpg"" width=""440"" height=""180"" align=""center""><br>
    </div>
</article>
</body>
</html>"
            },

            // CurrencyCreation - has font: 18; and <div padding:11px;">
            new TemplateDefinition
            {
                Name = "CurrencyCreation",
                DetectionPattern = "Currency.jpg",
                HasDynamicContent = true,
                FixedHtml = @"<!doctype html>
<html>
<head>
<style>
  h1 { font-size: 16px; text-align: center; color: grey;}
  p { color: whitesmoke; }
  body { font: 20px Helvetica, sans-serif; color: #333; margin:0; overflow-y:auto; height:100%; }
  .btn {
    background-color: #006100;
    border: 1px solid grey;
    color: white;
    padding: 8px 8px;
    text-align: center;
    text-decoration: none;
    display: inline-block;
    font-size: 18px;
    width: 130px;
  }
  article { display: block; text-align: left; width: 600px; margin: 0 auto; }  
  html{
    background-color: #21333D;
    height:100%;
  }
  .btn-sel{
      background-color: #008CD1;
  }
  .btn-group button:hover {
      background-color: #006199;
  }
  .btn-group{
   padding-top: 4px;
  }
  .logo{
    text-align: center;
    padding: 10px;
  }
  img{
    display: block;
    margin: 0 auto;
  }
</style>
</head>
<body>
<div style=""padding:11px;"">
<div class=""btn-group"" style=""text-align: center;"">
  <button type=""button"" class=""btn btn-primary"" id=""Home"">Main</button>
  <button type=""button"" class=""btn btn-primary"" id=""CreateCurrency"">Services</button>
</div>
</div>
<article>
    <div class=""logo text-center""> 
      <p><i>[WEB_CONTENT]</i></p>
      <img src=""Currency.jpg"" width=""440"" height=""180"" align=""center""><br>
    </div>
</article>
</body>
</html>"
            },

            // HardwareManufacturer - has <div padding:11px;">
            new TemplateDefinition
            {
                Name = "HardwareManufacturer",
                DetectionPattern = "manufacturer.jpg",
                HasDynamicContent = true,
                FixedHtml = @"<!doctype html>
<html>
<head>
<style>
  h1 { font-size: 30px; text-align: center; color: darkorchid;}
  p { color: whitesmoke; }
  body { font: 20px Helvetica, sans-serif; color: #333; margin:0; overflow-y:auto; height:100%; }
  .btn {
    background-color: #006699;
    border: 1px solid grey;
    color: white;
    padding: 8px 36px;
    text-align: center;
    text-decoration: none;
    display: inline-block;
    font-size: 12px;
  }
  article { display: block; text-align: left; width: 600px; margin: 0 auto; }  
  html{
    background-color: #0A201F;
    height:100%;
  }
  .btn-group{
    padding-top: 25px;
    text-align: center;
  }
  .logo{
    text-align: center;
    padding: 10px;
  }
  img{
    display: block;
    margin: 0 auto;
  }
</style>
</head>
<body>
<div style=""padding:11px;"">
    <div class=""btn-group"" style=""text-align: center;"">
      <button type=""button"" class=""btn btn-primary"" id=""Home"">Main</button>
      <button type=""button"" class=""btn btn-primary"" id=""FindDeviceManual"">Device Manuals</button>
    </div>
</div>
<article>
    <div class=""logo text-center""> 
      <p><i>[WEB_CONTENT]</i></p>
      <img src=""manufacturer.jpg"" width=""540"" height=""195"" align=""center"">
    </div>    
</article>
</body>
</html>"
            },

            // HotelMission - has mismatched <h1></p> tags
            new TemplateDefinition
            {
                Name = "HotelMission",
                DetectionPattern = "hotel.jpg",
                HasDynamicContent = false,
                FixedHtml = @"<!doctype html>
<html>
<head>
<style>
  h1 { font-size: 30px; text-align: center; color: darkorchid;}
  p { color: whitesmoke; }
  body { font: 20px Helvetica, sans-serif; color: #333; margin:0; overflow-y:auto; height:100%; }
  .btn {
    background-color: #006699;
    border: 1px solid grey;
    color: white;
    padding: 8px 36px;
    text-align: center;
    text-decoration: none;
    display: inline-block;
    font-size: 16px;
  }
  article { display: block; text-align: left; width: 600px; margin: 0 auto; }  
  html{
    background-color: black;
    height:100%;
  }
  .btn-group{
    padding-top: 25px;
    text-align: center;
  }
  .logo{
    text-align: center;
    padding: 10px;
  }
  img{
    display: block;
    margin: 0 auto;
  }
</style>
</head>
<body>
<article>
    <div class=""logo text-center"">
      <h1 style=""font-family: monospace;"">Hotel Darkroad</h1>
    </div>    
    <img src=""hotel.jpg"" align=""middle"">    

    <div class=""logo text-center""> 
        <h1 style=""font-family: monospace; font-size: 20px;"">If you are looking for Privacy this is your place</h1>
        <h1 style=""font-family: impact; color: darkorange;"">[Book closed temporarily]</h1>
    </div>    
</article>
</body>
</html>"
            }
        };

        public static string Process(string html)
        {
            if (string.IsNullOrEmpty(html))
                return html;

            foreach (var template in Templates)
            {
                if (html.Contains(template.DetectionPattern))
                {
                    if (template.HasDynamicContent)
                    {
                        // Extract dynamic content from original HTML
                        string dynamicContent = ExtractDynamicContent(html);
                        
                        // Replace placeholder in fixed template
                        return template.FixedHtml.Replace("[WEB_CONTENT]", dynamicContent);
                    }
                    else
                    {
                        // Static template, just return the fixed version
                        return template.FixedHtml;
                    }
                }
            }

            // No template matched, return original
            return html;
        }

        private static string ExtractDynamicContent(string html)
        {
            // The dynamic content is typically inside <p><i>...</i></p> or just <p>...</p>
            // within the .logo div, near the image

            // Try to extract from <p><i>CONTENT</i></p> pattern (NetServices, CurrencyCreation, HardwareManufacturer)
            var match = Regex.Match(html, @"<p>\s*<i>(.+?)</i>\s*</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (match.Success)
            {
                // Convert legacy HTML tags in dynamic content
                return LegacyHtmlConverter.Convert(match.Groups[1].Value.Trim());
            }

            // Try plain <p>CONTENT</p> pattern
            match = Regex.Match(html, @"<p>(.+?)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string content = match.Groups[1].Value.Trim();
                // Filter out common static content
                if (!content.Contains("Sorry for the inconvenience") && 
                    !content.Contains("The Team") &&
                    content.Length > 0)
                {
                    return LegacyHtmlConverter.Convert(content);
                }
            }

            // Fallback: return empty string, template will show [WEB_CONTENT] as-is
            return "[WEB_CONTENT]";
        }
    }
}
