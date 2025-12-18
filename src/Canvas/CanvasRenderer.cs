using System.Collections.Generic;
using UnityEngine;

namespace GreyHackTerminalUI.Canvas
{
    public class CanvasRenderer
    {
        private Texture2D _texture;
        private Color[] _pixels;
        private int _width;
        private int _height;
        private bool _isDirty;
        private Color _clearColor = Color.black;

        // Font rendering using GL
        private Font _font;
        private Material _fontMaterial;
        private RenderTexture _textRenderTexture;
        private Texture2D _textReadTexture;

        public int Width => _width;
        public int Height => _height;
        public Texture2D Texture => _texture;
        public bool IsDirty => _isDirty;

        public CanvasRenderer(int width = 320, int height = 240)
        {
            InitializeFont();
            Resize(width, height);
        }

        private void InitializeFont()
        {
            // Try to get Arial or any available system font
            string[] preferredFonts = { "Arial", "Liberation Sans", "DejaVu Sans", "Helvetica", "Verdana" };
            
            foreach (string fontName in preferredFonts)
            {
                _font = Font.CreateDynamicFontFromOSFont(fontName, 64);
                if (_font != null)
                    break;
            }
            
            if (_font == null)
            {
                string[] availableFonts = Font.GetOSInstalledFontNames();
                if (availableFonts.Length > 0)
                {
                    _font = Font.CreateDynamicFontFromOSFont(availableFonts[0], 64);
                }
            }

            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            // Create material for font rendering
            if (_font != null && _font.material != null)
            {
                _fontMaterial = new Material(_font.material);
                _fontMaterial.SetPass(0);
            }
        }

        public void Resize(int width, int height)
        {
            // Clamp to actual screen resolution instead of hardcoded values
            int maxWidth = Screen.width;
            int maxHeight = Screen.height;
            
            _width = Mathf.Clamp(width, 1, maxWidth);
            _height = Mathf.Clamp(height, 1, maxHeight);

            if (_texture != null)
            {
                Object.Destroy(_texture);
            }

            _texture = new Texture2D(_width, _height, TextureFormat.RGBA32, false);
            _texture.filterMode = FilterMode.Point;
            _texture.wrapMode = TextureWrapMode.Clamp;

            _pixels = new Color[_width * _height];
            Clear(_clearColor);
        }

        public void Clear(Color color)
        {
            _clearColor = color;
            for (int i = 0; i < _pixels.Length; i++)
            {
                _pixels[i] = color;
            }
            _isDirty = true;
        }

        public void Clear()
        {
            Clear(Color.black);
        }

        public void SetPixel(int x, int y, Color color)
        {
            if (x < 0 || x >= _width || y < 0 || y >= _height)
                return;

            // Flip Y coordinate (0,0 at top-left)
            int flippedY = _height - 1 - y;
            _pixels[flippedY * _width + x] = color;
            _isDirty = true;
        }

        public Color GetPixel(int x, int y)
        {
            if (x < 0 || x >= _width || y < 0 || y >= _height)
                return Color.clear;

            int flippedY = _height - 1 - y;
            return _pixels[flippedY * _width + x];
        }

        public void DrawLine(int x1, int y1, int x2, int y2, Color color)
        {
            int dx = Mathf.Abs(x2 - x1);
            int dy = Mathf.Abs(y2 - y1);
            
            // Limit line length to prevent excessive iterations
            int maxDistance = Mathf.Max(_width, _height) * 3;
            int lineLength = Mathf.Max(dx, dy);
            if (lineLength > maxDistance)
                return;
            
            int sx = x1 < x2 ? 1 : -1;
            int sy = y1 < y2 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                SetPixel(x1, y1, color);

                if (x1 == x2 && y1 == y2)
                    break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x1 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y1 += sy;
                }
            }
            _isDirty = true;
        }

        public void DrawRect(int x, int y, int width, int height, Color color)
        {
            // Limit dimensions to prevent excessive operations
            width = Mathf.Clamp(width, 0, _width * 2);
            height = Mathf.Clamp(height, 0, _height * 2);
            if (width <= 0 || height <= 0) return;
            
            // Top line
            DrawLine(x, y, x + width - 1, y, color);
            // Bottom line
            DrawLine(x, y + height - 1, x + width - 1, y + height - 1, color);
            // Left line
            DrawLine(x, y, x, y + height - 1, color);
            // Right line
            DrawLine(x + width - 1, y, x + width - 1, y + height - 1, color);
        }

        public void FillRect(int x, int y, int width, int height, Color color)
        {
            // Limit dimensions to prevent excessive operations
            width = Mathf.Clamp(width, 0, _width * 2);
            height = Mathf.Clamp(height, 0, _height * 2);
            if (width <= 0 || height <= 0) return;
            
            // Clamp fill area to reasonable bounds
            int maxPixels = _width * _height;
            if (width * height > maxPixels) return;
            
            for (int py = y; py < y + height; py++)
            {
                for (int px = x; px < x + width; px++)
                {
                    SetPixel(px, py, color);
                }
            }
        }

        public void DrawCircle(int centerX, int centerY, int radius, Color color)
        {
            // Limit radius to prevent excessive operations
            radius = Mathf.Clamp(radius, 0, Mathf.Max(_width, _height));
            if (radius <= 0) return;
            
            int x = radius;
            int y = 0;
            int radiusError = 1 - x;

            while (x >= y)
            {
                SetPixel(centerX + x, centerY + y, color);
                SetPixel(centerX + y, centerY + x, color);
                SetPixel(centerX - x, centerY + y, color);
                SetPixel(centerX - y, centerY + x, color);
                SetPixel(centerX - x, centerY - y, color);
                SetPixel(centerX - y, centerY - x, color);
                SetPixel(centerX + x, centerY - y, color);
                SetPixel(centerX + y, centerY - x, color);

                y++;
                if (radiusError < 0)
                {
                    radiusError += 2 * y + 1;
                }
                else
                {
                    x--;
                    radiusError += 2 * (y - x + 1);
                }
            }
        }

        public void FillCircle(int centerX, int centerY, int radius, Color color)
        {
            // Limit radius to prevent excessive operations
            radius = Mathf.Clamp(radius, 0, Mathf.Max(_width, _height));
            if (radius <= 0) return;
            
            // Prevent excessive pixel iterations
            int maxPixels = _width * _height;
            if (radius * radius * 4 > maxPixels) return;
            
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y <= radius * radius)
                    {
                        SetPixel(centerX + x, centerY + y, color);
                    }
                }
            }
        }

        public void DrawText(int x, int y, string text, Color color, int fontSize = 12)
        {
            if (string.IsNullOrEmpty(text) || _font == null || _fontMaterial == null)
                return;

            // Limit font size to prevent memory abuse
            fontSize = Mathf.Clamp(fontSize, 1, 256);
            
            // Limit text length to prevent excessive render texture size
            if (text.Length > 500)
                text = text.Substring(0, 500);

            // Request characters to be in the font texture
            _font.RequestCharactersInTexture(text, fontSize, FontStyle.Normal);

            // Calculate text dimensions with generous padding
            int textWidth = 0;
            foreach (char c in text)
            {
                if (_font.GetCharacterInfo(c, out CharacterInfo info, fontSize, FontStyle.Normal))
                {
                    textWidth += Mathf.CeilToInt(info.advance);
                }
            }

            if (textWidth <= 0)
                return;

            // Use generous bounds to ensure text isn't clipped
            // Ensure minimum size for small fonts
            int rtWidth = Mathf.Max(textWidth + fontSize * 2, 128);
            int rtHeight = Mathf.Max(fontSize * 3, 96);
            
            // Cap render texture size to prevent memory exhaustion
            const int MAX_RT_SIZE = 4096;
            if (rtWidth > MAX_RT_SIZE || rtHeight > MAX_RT_SIZE)
                return;

            if (_textRenderTexture == null || _textRenderTexture.width < rtWidth || _textRenderTexture.height < rtHeight)
            {
                if (_textRenderTexture != null)
                {
                    _textRenderTexture.Release();
                    Object.Destroy(_textRenderTexture);
                }
                
                _textRenderTexture = new RenderTexture(rtWidth, rtHeight, 0, RenderTextureFormat.ARGB32);
                _textRenderTexture.Create();
            }

            // Ensure read texture matches
            if (_textReadTexture == null || _textReadTexture.width != _textRenderTexture.width || _textReadTexture.height != _textRenderTexture.height)
            {
                if (_textReadTexture != null)
                    Object.Destroy(_textReadTexture);
                
                _textReadTexture = new Texture2D(_textRenderTexture.width, _textRenderTexture.height, TextureFormat.ARGB32, false);
            }

            // Save current state
            RenderTexture previousRT = RenderTexture.active;
            
            // Render to our texture
            RenderTexture.active = _textRenderTexture;
            GL.Clear(true, true, new Color(0, 0, 0, 0));

            // Set up orthographic projection for pixel-perfect rendering
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, _textRenderTexture.width, _textRenderTexture.height, 0);

            // Get font texture
            Texture fontTex = _font.material.mainTexture;
            
            // Set up material
            _fontMaterial.mainTexture = fontTex;
            _fontMaterial.SetPass(0);

            // Render each character as a textured quad
            GL.Begin(GL.QUADS);
            GL.Color(Color.white);

            float cursorX = fontSize;
            float baselineY = fontSize * 2;

            foreach (char c in text)
            {
                if (_font.GetCharacterInfo(c, out CharacterInfo ch, fontSize, FontStyle.Normal))
                {
                    // Character quad vertices (in screen space, Y down)
                    // Position relative to baseline
                    float left = cursorX + ch.minX;
                    float right = cursorX + ch.maxX;
                    float top = baselineY - ch.maxY;
                    float bottom = baselineY - ch.minY;

                    // UV coordinates
                    Vector2 uvBL = ch.uvBottomLeft;
                    Vector2 uvBR = ch.uvBottomRight;
                    Vector2 uvTL = ch.uvTopLeft;
                    Vector2 uvTR = ch.uvTopRight;

                    // Bottom-left
                    GL.TexCoord2(uvBL.x, uvBL.y);
                    GL.Vertex3(left, bottom, 0);

                    // Bottom-right
                    GL.TexCoord2(uvBR.x, uvBR.y);
                    GL.Vertex3(right, bottom, 0);

                    // Top-right
                    GL.TexCoord2(uvTR.x, uvTR.y);
                    GL.Vertex3(right, top, 0);

                    // Top-left
                    GL.TexCoord2(uvTL.x, uvTL.y);
                    GL.Vertex3(left, top, 0);

                    cursorX += ch.advance;
                }
            }

            GL.End();
            GL.PopMatrix();

            // Read pixels back
            _textReadTexture.ReadPixels(new Rect(0, 0, rtWidth, rtHeight), 0, 0, false);
            _textReadTexture.Apply();

            // Restore render target
            RenderTexture.active = previousRT;

            // Copy render texture to canvas
            // Offset destination by padding so text appears at requested (x,y)
            Color[] textPixels = _textReadTexture.GetPixels(0, 0, rtWidth, rtHeight);
            int offsetX = -(fontSize / 2);  // Adjust for left padding
            int offsetY = -fontSize;       // Adjust for top padding

            for (int py = 0; py < rtHeight; py++)
            {
                for (int px = 0; px < rtWidth; px++)
                {
                    // Flip Y (RenderTexture has 0,0 at bottom-left)
                    int srcIndex = (rtHeight - 1 - py) * rtWidth + px;
                    if (srcIndex < 0 || srcIndex >= textPixels.Length)
                        continue;

                    Color srcColor = textPixels[srcIndex];
                    
                    // Use the source alpha as the glyph mask
                    float alpha = srcColor.a;
                    if (alpha > 0.01f)
                    {
                        Color finalColor = new Color(color.r, color.g, color.b, color.a * alpha);
                        SetPixelBlend(x + px + offsetX, y + py + offsetY, finalColor);
                    }
                }
            }

            _isDirty = true;
        }

        private void SetPixelBlend(int x, int y, Color color)
        {
            if (x < 0 || x >= _width || y < 0 || y >= _height)
                return;

            int flippedY = _height - 1 - y;
            int index = flippedY * _width + x;
            
            if (color.a >= 1f)
            {
                _pixels[index] = color;
            }
            else if (color.a > 0f)
            {
                // Alpha blend
                Color existing = _pixels[index];
                float invAlpha = 1f - color.a;
                _pixels[index] = new Color(
                    color.r * color.a + existing.r * invAlpha,
                    color.g * color.a + existing.g * invAlpha,
                    color.b * color.a + existing.b * invAlpha,
                    Mathf.Max(color.a, existing.a)
                );
            }
        }

        public void Render()
        {
            if (_texture != null && _isDirty)
            {
                _texture.SetPixels(_pixels);
                _texture.Apply();
                _isDirty = false;
            }
        }

        public void Destroy()
        {
            if (_texture != null)
            {
                Object.Destroy(_texture);
                _texture = null;
            }
            if (_fontMaterial != null)
            {
                Object.Destroy(_fontMaterial);
                _fontMaterial = null;
            }
            if (_textRenderTexture != null)
            {
                _textRenderTexture.Release();
                Object.Destroy(_textRenderTexture);
                _textRenderTexture = null;
            }
            if (_textReadTexture != null)
            {
                Object.Destroy(_textReadTexture);
                _textReadTexture = null;
            }
            _pixels = null;
        }
    }
}
