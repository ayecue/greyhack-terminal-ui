using System.Collections.Generic;
using UnityEngine;

namespace GreyHackTerminalUI.Canvas
{
    public class CanvasRenderer
    {
        private Texture2D _texture;
        private Color[] _backBuffer;   // Draw to this
        private Color[] _frontBuffer;  // Display this
        private int _width;
        private int _height;
        private bool _isDirty;
        private Color _clearColor = Color.black;
        private bool _needsApply = false;
        private readonly object _bufferLock = new object();
        
        // Debug counters
        private int _renderCount = 0;
        private int _clearCount = 0;
        private float _lastLogTime = 0f;

        public int Width => _width;
        public int Height => _height;
        public Texture2D Texture => _texture;
        public bool IsDirty => _isDirty;
        public bool NeedsApply => _needsApply;

        public CanvasRenderer(int width = 320, int height = 240)
        {
            Resize(width, height);
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

            _backBuffer = new Color[_width * _height];
            _frontBuffer = new Color[_width * _height];
            Clear(_clearColor);
            
            // Initialize front buffer too
            for (int i = 0; i < _frontBuffer.Length; i++)
            {
                _frontBuffer[i] = _clearColor;
            }
        }

        public void Clear(Color color)
        {
            _clearColor = color;
            _clearCount++;
            
            // Debug: Log if we're clearing to white (shouldn't happen normally)
            if (color.r > 0.9f && color.g > 0.9f && color.b > 0.9f)
            {
                UnityEngine.Debug.LogWarning($"[CanvasRenderer] Clear called with near-white color: R={color.r}, G={color.g}, B={color.b}");
            }
            
            for (int i = 0; i < _backBuffer.Length; i++)
            {
                _backBuffer[i] = color;
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
            _backBuffer[flippedY * _width + x] = color;
            _isDirty = true;
        }

        public Color GetPixel(int x, int y)
        {
            if (x < 0 || x >= _width || y < 0 || y >= _height)
                return Color.clear;

            int flippedY = _height - 1 - y;
            return _backBuffer[flippedY * _width + x];
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

        // 5x7 bitmap font - each character is 5 pixels wide, 7 pixels tall
        // Each byte represents a row, with bits representing pixels (LSB = left)
        private static readonly Dictionary<char, byte[]> BitmapFont = new Dictionary<char, byte[]>
        {
            // Letters
            {'A', new byte[] {0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11}},
            {'B', new byte[] {0x0F, 0x11, 0x11, 0x0F, 0x11, 0x11, 0x0F}},
            {'C', new byte[] {0x0E, 0x11, 0x01, 0x01, 0x01, 0x11, 0x0E}},
            {'D', new byte[] {0x07, 0x09, 0x11, 0x11, 0x11, 0x09, 0x07}},
            {'E', new byte[] {0x1F, 0x01, 0x01, 0x0F, 0x01, 0x01, 0x1F}},
            {'F', new byte[] {0x1F, 0x01, 0x01, 0x0F, 0x01, 0x01, 0x01}},
            {'G', new byte[] {0x0E, 0x11, 0x01, 0x1D, 0x11, 0x11, 0x0E}},
            {'H', new byte[] {0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11}},
            {'I', new byte[] {0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E}},
            {'J', new byte[] {0x1C, 0x08, 0x08, 0x08, 0x08, 0x09, 0x06}},
            {'K', new byte[] {0x11, 0x09, 0x05, 0x03, 0x05, 0x09, 0x11}},
            {'L', new byte[] {0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x1F}},
            {'M', new byte[] {0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11}},
            {'N', new byte[] {0x11, 0x13, 0x15, 0x19, 0x11, 0x11, 0x11}},
            {'O', new byte[] {0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E}},
            {'P', new byte[] {0x0F, 0x11, 0x11, 0x0F, 0x01, 0x01, 0x01}},
            {'Q', new byte[] {0x0E, 0x11, 0x11, 0x11, 0x15, 0x09, 0x16}},
            {'R', new byte[] {0x0F, 0x11, 0x11, 0x0F, 0x05, 0x09, 0x11}},
            {'S', new byte[] {0x0E, 0x11, 0x01, 0x0E, 0x10, 0x11, 0x0E}},
            {'T', new byte[] {0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04}},
            {'U', new byte[] {0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E}},
            {'V', new byte[] {0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04}},
            {'W', new byte[] {0x11, 0x11, 0x11, 0x15, 0x15, 0x15, 0x0A}},
            {'X', new byte[] {0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11}},
            {'Y', new byte[] {0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04}},
            {'Z', new byte[] {0x1F, 0x10, 0x08, 0x04, 0x02, 0x01, 0x1F}},
            
            // Numbers
            {'0', new byte[] {0x0E, 0x11, 0x19, 0x15, 0x13, 0x11, 0x0E}},
            {'1', new byte[] {0x04, 0x06, 0x04, 0x04, 0x04, 0x04, 0x0E}},
            {'2', new byte[] {0x0E, 0x11, 0x10, 0x08, 0x04, 0x02, 0x1F}},
            {'3', new byte[] {0x0E, 0x11, 0x10, 0x0C, 0x10, 0x11, 0x0E}},
            {'4', new byte[] {0x08, 0x0C, 0x0A, 0x09, 0x1F, 0x08, 0x08}},
            {'5', new byte[] {0x1F, 0x01, 0x0F, 0x10, 0x10, 0x11, 0x0E}},
            {'6', new byte[] {0x0C, 0x02, 0x01, 0x0F, 0x11, 0x11, 0x0E}},
            {'7', new byte[] {0x1F, 0x10, 0x08, 0x04, 0x02, 0x02, 0x02}},
            {'8', new byte[] {0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E}},
            {'9', new byte[] {0x0E, 0x11, 0x11, 0x1E, 0x10, 0x08, 0x06}},
            
            // Punctuation and symbols
            {' ', new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00}},
            {'!', new byte[] {0x04, 0x04, 0x04, 0x04, 0x04, 0x00, 0x04}},
            {'"', new byte[] {0x0A, 0x0A, 0x0A, 0x00, 0x00, 0x00, 0x00}},
            {'#', new byte[] {0x0A, 0x0A, 0x1F, 0x0A, 0x1F, 0x0A, 0x0A}},
            {'$', new byte[] {0x04, 0x0F, 0x05, 0x0E, 0x14, 0x0F, 0x04}},
            {'%', new byte[] {0x03, 0x13, 0x08, 0x04, 0x02, 0x19, 0x18}},
            {'&', new byte[] {0x06, 0x09, 0x05, 0x02, 0x15, 0x09, 0x16}},
            {'\'', new byte[] {0x04, 0x04, 0x02, 0x00, 0x00, 0x00, 0x00}},
            {'(', new byte[] {0x08, 0x04, 0x02, 0x02, 0x02, 0x04, 0x08}},
            {')', new byte[] {0x02, 0x04, 0x08, 0x08, 0x08, 0x04, 0x02}},
            {'*', new byte[] {0x00, 0x04, 0x15, 0x0E, 0x15, 0x04, 0x00}},
            {'+', new byte[] {0x00, 0x04, 0x04, 0x1F, 0x04, 0x04, 0x00}},
            {',', new byte[] {0x00, 0x00, 0x00, 0x00, 0x04, 0x04, 0x02}},
            {'-', new byte[] {0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00}},
            {'.', new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04}},
            {'/', new byte[] {0x10, 0x10, 0x08, 0x04, 0x02, 0x01, 0x01}},
            {':', new byte[] {0x00, 0x04, 0x04, 0x00, 0x04, 0x04, 0x00}},
            {';', new byte[] {0x00, 0x04, 0x04, 0x00, 0x04, 0x04, 0x02}},
            {'<', new byte[] {0x08, 0x04, 0x02, 0x01, 0x02, 0x04, 0x08}},
            {'=', new byte[] {0x00, 0x00, 0x1F, 0x00, 0x1F, 0x00, 0x00}},
            {'>', new byte[] {0x02, 0x04, 0x08, 0x10, 0x08, 0x04, 0x02}},
            {'?', new byte[] {0x0E, 0x11, 0x10, 0x08, 0x04, 0x00, 0x04}},
            {'@', new byte[] {0x0E, 0x11, 0x1D, 0x1D, 0x1D, 0x01, 0x0E}},
            {'[', new byte[] {0x0E, 0x02, 0x02, 0x02, 0x02, 0x02, 0x0E}},
            {'\\', new byte[] {0x01, 0x01, 0x02, 0x04, 0x08, 0x10, 0x10}},
            {']', new byte[] {0x0E, 0x08, 0x08, 0x08, 0x08, 0x08, 0x0E}},
            {'^', new byte[] {0x04, 0x0A, 0x11, 0x00, 0x00, 0x00, 0x00}},
            {'_', new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1F}},
            {'`', new byte[] {0x02, 0x04, 0x08, 0x00, 0x00, 0x00, 0x00}},
            {'{', new byte[] {0x0C, 0x04, 0x04, 0x02, 0x04, 0x04, 0x0C}},
            {'|', new byte[] {0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04}},
            {'}', new byte[] {0x06, 0x04, 0x04, 0x08, 0x04, 0x04, 0x06}},
            {'~', new byte[] {0x00, 0x00, 0x02, 0x15, 0x08, 0x00, 0x00}},
            
            // Lowercase letters (same as uppercase for simplicity, or simplified versions)
            {'a', new byte[] {0x00, 0x00, 0x0E, 0x10, 0x1E, 0x11, 0x1E}},
            {'b', new byte[] {0x01, 0x01, 0x0D, 0x13, 0x11, 0x11, 0x0F}},
            {'c', new byte[] {0x00, 0x00, 0x0E, 0x01, 0x01, 0x11, 0x0E}},
            {'d', new byte[] {0x10, 0x10, 0x16, 0x19, 0x11, 0x11, 0x1E}},
            {'e', new byte[] {0x00, 0x00, 0x0E, 0x11, 0x1F, 0x01, 0x0E}},
            {'f', new byte[] {0x0C, 0x12, 0x02, 0x07, 0x02, 0x02, 0x02}},
            {'g', new byte[] {0x00, 0x1E, 0x11, 0x11, 0x1E, 0x10, 0x0E}},
            {'h', new byte[] {0x01, 0x01, 0x0D, 0x13, 0x11, 0x11, 0x11}},
            {'i', new byte[] {0x04, 0x00, 0x06, 0x04, 0x04, 0x04, 0x0E}},
            {'j', new byte[] {0x08, 0x00, 0x0C, 0x08, 0x08, 0x09, 0x06}},
            {'k', new byte[] {0x01, 0x01, 0x09, 0x05, 0x03, 0x05, 0x09}},
            {'l', new byte[] {0x06, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E}},
            {'m', new byte[] {0x00, 0x00, 0x0B, 0x15, 0x15, 0x11, 0x11}},
            {'n', new byte[] {0x00, 0x00, 0x0D, 0x13, 0x11, 0x11, 0x11}},
            {'o', new byte[] {0x00, 0x00, 0x0E, 0x11, 0x11, 0x11, 0x0E}},
            {'p', new byte[] {0x00, 0x0F, 0x11, 0x11, 0x0F, 0x01, 0x01}},
            {'q', new byte[] {0x00, 0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10}},
            {'r', new byte[] {0x00, 0x00, 0x0D, 0x13, 0x01, 0x01, 0x01}},
            {'s', new byte[] {0x00, 0x00, 0x0E, 0x01, 0x0E, 0x10, 0x0F}},
            {'t', new byte[] {0x02, 0x02, 0x07, 0x02, 0x02, 0x12, 0x0C}},
            {'u', new byte[] {0x00, 0x00, 0x11, 0x11, 0x11, 0x19, 0x16}},
            {'v', new byte[] {0x00, 0x00, 0x11, 0x11, 0x11, 0x0A, 0x04}},
            {'w', new byte[] {0x00, 0x00, 0x11, 0x11, 0x15, 0x15, 0x0A}},
            {'x', new byte[] {0x00, 0x00, 0x11, 0x0A, 0x04, 0x0A, 0x11}},
            {'y', new byte[] {0x00, 0x11, 0x11, 0x11, 0x1E, 0x10, 0x0E}},
            {'z', new byte[] {0x00, 0x00, 0x1F, 0x08, 0x04, 0x02, 0x1F}},
        };
        
        private const int GLYPH_WIDTH = 5;
        private const int GLYPH_HEIGHT = 7;

        public void DrawText(int x, int y, string text, Color color, int fontSize = 12)
        {
            if (string.IsNullOrEmpty(text))
                return;

            // Limit text length
            if (text.Length > 500)
                text = text.Substring(0, 500);

            // Calculate scale factor based on font size (base size is 7 pixels tall)
            int scale = Mathf.Max(1, fontSize / GLYPH_HEIGHT);
            int charSpacing = 1 * scale;  // 1 pixel spacing between chars, scaled
            
            int cursorX = x;
            
            foreach (char c in text)
            {
                char upperC = char.ToUpper(c);
                byte[] glyph;
                
                // Try to get the glyph, fall back to uppercase, then to '?'
                if (!BitmapFont.TryGetValue(c, out glyph))
                {
                    if (!BitmapFont.TryGetValue(upperC, out glyph))
                    {
                        glyph = BitmapFont.ContainsKey('?') ? BitmapFont['?'] : null;
                    }
                }
                
                if (glyph != null)
                {
                    // Draw the character
                    for (int row = 0; row < GLYPH_HEIGHT; row++)
                    {
                        byte rowData = glyph[row];
                        for (int col = 0; col < GLYPH_WIDTH; col++)
                        {
                            if ((rowData & (1 << col)) != 0)
                            {
                                // Draw a scaled pixel
                                for (int sy = 0; sy < scale; sy++)
                                {
                                    for (int sx = 0; sx < scale; sx++)
                                    {
                                        SetPixel(cursorX + col * scale + sx, y + row * scale + sy, color);
                                    }
                                }
                            }
                        }
                    }
                }
                
                // Advance cursor by character width plus spacing
                cursorX += GLYPH_WIDTH * scale + charSpacing;
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
                _backBuffer[index] = color;
            }
            else if (color.a > 0f)
            {
                // Alpha blend
                Color existing = _backBuffer[index];
                float invAlpha = 1f - color.a;
                _backBuffer[index] = new Color(
                    color.r * color.a + existing.r * invAlpha,
                    color.g * color.a + existing.g * invAlpha,
                    color.b * color.a + existing.b * invAlpha,
                    Mathf.Max(color.a, existing.a)
                );
            }
        }

        /// <summary>
        /// Apply pixels to texture immediately - no throttling for now
        /// </summary>
        public void Render()
        {
            if (!_isDirty || _backBuffer == null || _texture == null)
                return;

            _renderCount++;
            
            // Log frame statistics every second
            float now = UnityEngine.Time.time;
            if (now - _lastLogTime >= 1.0f)
            {
                UnityEngine.Debug.Log($"[CanvasRenderer] Stats: {_renderCount} renders, {_clearCount} clears in last second");
                _renderCount = 0;
                _clearCount = 0;
                _lastLogTime = now;
            }

            // Swap buffers - copy back buffer to front buffer atomically
            lock (_bufferLock)
            {
                System.Array.Copy(_backBuffer, _frontBuffer, _backBuffer.Length);
            }
            
            // Apply front buffer to texture
            _texture.SetPixels(_frontBuffer);
            _texture.Apply();
            _isDirty = false;
            _needsApply = false;
        }

        /// <summary>
        /// Called from CanvasWindow.Update() - no longer needed but kept for compatibility
        /// </summary>
        public void ApplyTexture()
        {
            // Just call Render
            Render();
        }

        public void Destroy()
        {
            if (_texture != null)
            {
                Object.Destroy(_texture);
                _texture = null;
            }
            _backBuffer = null;
            _frontBuffer = null;
        }
    }
}
