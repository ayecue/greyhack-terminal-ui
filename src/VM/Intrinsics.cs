using System;
using System.Collections.Generic;
using UnityEngine;
using GreyHackTerminalUI.Canvas;
using GreyHackTerminalUI.Sound;

namespace GreyHackTerminalUI.VM
{
    public delegate object IntrinsicFunction(object[] args, VMContext context);
    
    public delegate object IntrinsicMethod(object target, object[] args, VMContext context);

    public class IntrinsicObject
    {
        public string Name { get; }
        public Dictionary<string, IntrinsicMethod> Methods { get; } = new Dictionary<string, IntrinsicMethod>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Func<object, VMContext, object>> Getters { get; } = new Dictionary<string, Func<object, VMContext, object>>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Action<object, object, VMContext>> Setters { get; } = new Dictionary<string, Action<object, object, VMContext>>(StringComparer.OrdinalIgnoreCase);

        public IntrinsicObject(string name)
        {
            Name = name;
        }
    }

    public class IntrinsicRegistry
    {
        private readonly Dictionary<string, IntrinsicFunction> _functions = new Dictionary<string, IntrinsicFunction>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IntrinsicObject> _objects = new Dictionary<string, IntrinsicObject>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, float> _lastSetSizeTime = new Dictionary<int, float>();
        private const float SET_SIZE_COOLDOWN = 10.0f; // 10 second cooldown

        public IntrinsicRegistry()
        {
            RegisterBuiltins();
        }

        private void RegisterBuiltins()
        {
            // Global functions
            RegisterFunction("hasInContext", (args, ctx) =>
            {
                if (args.Length < 1) return false;
                string name = args[0]?.ToString();
                return ctx.HasVariable(name);
            });

            RegisterFunction("print", (args, ctx) =>
            {
                // Print from UI script - silent by default
                return null;
            });

            RegisterFunction("typeof", (args, ctx) =>
            {
                if (args.Length < 1 || args[0] == null) return "null";
                return args[0].GetType().Name.ToLower();
            });

            RegisterFunction("toNumber", (args, ctx) =>
            {
                if (args.Length < 1) return 0.0;
                if (args[0] is double d) return d;
                if (args[0] is int i) return (double)i;
                if (double.TryParse(args[0]?.ToString(), out double parsed)) return parsed;
                return 0.0;
            });

            RegisterFunction("toString", (args, ctx) =>
            {
                if (args.Length < 1) return "";
                return args[0]?.ToString() ?? "null";
            });

            RegisterFunction("floor", (args, ctx) =>
            {
                if (args.Length < 1) return 0.0;
                return Math.Floor(ToDouble(args[0]));
            });

            RegisterFunction("ceil", (args, ctx) =>
            {
                if (args.Length < 1) return 0.0;
                return Math.Ceiling(ToDouble(args[0]));
            });

            RegisterFunction("round", (args, ctx) =>
            {
                if (args.Length < 1) return 0.0;
                return Math.Round(ToDouble(args[0]));
            });

            RegisterFunction("abs", (args, ctx) =>
            {
                if (args.Length < 1) return 0.0;
                return Math.Abs(ToDouble(args[0]));
            });

            RegisterFunction("min", (args, ctx) =>
            {
                if (args.Length < 2) return 0.0;
                return Math.Min(ToDouble(args[0]), ToDouble(args[1]));
            });

            RegisterFunction("max", (args, ctx) =>
            {
                if (args.Length < 2) return 0.0;
                return Math.Max(ToDouble(args[0]), ToDouble(args[1]));
            });

            RegisterFunction("sin", (args, ctx) =>
            {
                if (args.Length < 1) return 0.0;
                return Math.Sin(ToDouble(args[0]));
            });

            RegisterFunction("cos", (args, ctx) =>
            {
                if (args.Length < 1) return 0.0;
                return Math.Cos(ToDouble(args[0]));
            });

            RegisterFunction("random", (args, ctx) =>
            {
                return UnityEngine.Random.value;
            });

            RegisterFunction("randomRange", (args, ctx) =>
            {
                if (args.Length < 2) return 0.0;
                return UnityEngine.Random.Range((float)ToDouble(args[0]), (float)ToDouble(args[1]));
            });

            // Register Canvas object
            RegisterCanvasObject();
            
            // Register Sound object
            RegisterSoundObject();
        }

        private void RegisterCanvasObject()
        {
            var canvas = new IntrinsicObject("Canvas");

            // Canvas.show()
            canvas.Methods["show"] = (target, args, ctx) =>
            {
                var window = GetCanvasWindow(ctx);
                if (window != null)
                {
                    window.Show();
                    // Mark this terminal as having a visible window
                    CanvasManager.Instance?.MarkWindowVisible(window.TerminalPID);
                }
                return null;
            };

            // Canvas.hide()
            canvas.Methods["hide"] = (target, args, ctx) =>
            {
                var window = GetCanvasWindow(ctx);
                window?.Hide();
                return null;
            };

            // Canvas.setSize(width, height)
            canvas.Methods["setSize"] = (target, args, ctx) =>
            {
                if (args.Length < 2) return null;
                
                // Cooldown to prevent abuse
                int terminalPID = ctx.GetInternal("terminalPID") as int? ?? 0;
                float currentTime = Time.time;
                if (_lastSetSizeTime.TryGetValue(terminalPID, out float lastTime))
                {
                    if (currentTime - lastTime < SET_SIZE_COOLDOWN)
                    {
                        return null; // Silently ignore during cooldown
                    }
                }
                _lastSetSizeTime[terminalPID] = currentTime;
                
                var window = GetCanvasWindow(ctx);
                window?.SetSize(ToInt(args[0]), ToInt(args[1]));
                return null;
            };

            // Canvas.setTitle(title)
            canvas.Methods["setTitle"] = (target, args, ctx) =>
            {
                if (args.Length < 1) return null;
                var window = GetCanvasWindow(ctx);
                window?.SetTitle(args[0]?.ToString() ?? "");
                return null;
            };

            // Canvas.clear() or Canvas.clear(color)
            canvas.Methods["clear"] = (target, args, ctx) =>
            {
                var window = GetCanvasWindow(ctx);
                if (window == null) return null;

                if (args.Length > 0)
                {
                    string colorStr = args[0]?.ToString() ?? "";
                    Color color = ParseColor(colorStr);
                    window.Renderer.Clear(color);
                }
                else
                {
                    window.Renderer.Clear();
                }
                return null;
            };

            // Canvas.setPixel(color, x, y)
            canvas.Methods["setPixel"] = (target, args, ctx) =>
            {
                if (args.Length < 3) return null;
                var window = GetCanvasWindow(ctx);
                if (window == null) return null;

                Color color = ParseColor(args[0]?.ToString());
                int x = ToInt(args[1]);
                int y = ToInt(args[2]);
                window.Renderer.SetPixel(x, y, color);
                return null;
            };

            // Canvas.drawLine(color, x1, y1, x2, y2)
            canvas.Methods["drawLine"] = (target, args, ctx) =>
            {
                if (args.Length < 5) return null;
                var window = GetCanvasWindow(ctx);
                if (window == null) return null;

                Color color = ParseColor(args[0]?.ToString());
                int x1 = ToInt(args[1]);
                int y1 = ToInt(args[2]);
                int x2 = ToInt(args[3]);
                int y2 = ToInt(args[4]);
                window.Renderer.DrawLine(x1, y1, x2, y2, color);
                return null;
            };

            // Canvas.drawRect(color, x, y, width, height)
            canvas.Methods["drawRect"] = (target, args, ctx) =>
            {
                if (args.Length < 5) return null;
                var window = GetCanvasWindow(ctx);
                if (window == null) return null;

                Color color = ParseColor(args[0]?.ToString());
                int x = ToInt(args[1]);
                int y = ToInt(args[2]);
                int w = ToInt(args[3]);
                int h = ToInt(args[4]);
                window.Renderer.DrawRect(x, y, w, h, color);
                return null;
            };

            // Canvas.fillRect(color, x, y, width, height)
            canvas.Methods["fillRect"] = (target, args, ctx) =>
            {
                if (args.Length < 5) return null;
                var window = GetCanvasWindow(ctx);
                if (window == null) return null;

                Color color = ParseColor(args[0]?.ToString());
                int x = ToInt(args[1]);
                int y = ToInt(args[2]);
                int w = ToInt(args[3]);
                int h = ToInt(args[4]);
                window.Renderer.FillRect(x, y, w, h, color);
                return null;
            };

            // Canvas.drawCircle(color, x, y, radius)
            canvas.Methods["drawCircle"] = (target, args, ctx) =>
            {
                if (args.Length < 4) return null;
                var window = GetCanvasWindow(ctx);
                if (window == null) return null;

                Color color = ParseColor(args[0]?.ToString());
                int x = ToInt(args[1]);
                int y = ToInt(args[2]);
                int r = ToInt(args[3]);
                window.Renderer.DrawCircle(x, y, r, color);
                return null;
            };

            // Canvas.fillCircle(color, x, y, radius)
            canvas.Methods["fillCircle"] = (target, args, ctx) =>
            {
                if (args.Length < 4) return null;
                var window = GetCanvasWindow(ctx);
                if (window == null) return null;

                Color color = ParseColor(args[0]?.ToString());
                int x = ToInt(args[1]);
                int y = ToInt(args[2]);
                int r = ToInt(args[3]);
                window.Renderer.FillCircle(x, y, r, color);
                return null;
            };

            // Canvas.drawText(color, x, y, text) or Canvas.drawText(color, x, y, text, size)
            canvas.Methods["drawText"] = (target, args, ctx) =>
            {
                if (args.Length < 4) return null;
                var window = GetCanvasWindow(ctx);
                if (window == null) return null;

                string colorStr = args[0]?.ToString() ?? "";
                Color color = ParseColor(colorStr);
                int x = ToInt(args[1]);
                int y = ToInt(args[2]);
                string text = args[3]?.ToString() ?? "";
                int size = args.Length > 4 ? ToInt(args[4]) : 12;
                
                window.Renderer.DrawText(x, y, text, color, size);
                return null;
            };

            // Canvas.render()
            canvas.Methods["render"] = (target, args, ctx) =>
            {
                var window = GetCanvasWindow(ctx);
                if (window == null)
                {
                    UnityEngine.Debug.LogError("[Intrinsics] render() called but window is null!");
                    return null;
                }
                if (window.Renderer == null)
                {
                    UnityEngine.Debug.LogError("[Intrinsics] render() called but renderer is null!");
                    return null;
                }
                window.Renderer.Render();
                return null;
            };

            // Canvas.width (getter)
            canvas.Getters["width"] = (target, ctx) =>
            {
                var window = GetCanvasWindow(ctx);
                return window?.Renderer.Width ?? 0;
            };

            // Canvas.height (getter)
            canvas.Getters["height"] = (target, ctx) =>
            {
                var window = GetCanvasWindow(ctx);
                return window?.Renderer.Height ?? 0;
            };

            RegisterObject(canvas);
        }

        private CanvasWindow GetCanvasWindow(VMContext ctx)
        {
            if (ctx.Globals.TryGetValue("__canvasWindow", out object window))
            {
                return window as CanvasWindow;
            }
            return null;
        }

        public void RegisterFunction(string name, IntrinsicFunction function)
        {
            _functions[name] = function;
        }

        public void RegisterObject(IntrinsicObject obj)
        {
            _objects[obj.Name] = obj;
        }

        public object Call(string name, object[] args, VMContext context)
        {
            if (_functions.TryGetValue(name, out var func))
            {
                return func(args, context);
            }
            throw new VMException($"Unknown function: {name}");
        }

        public object CallMethod(object target, string methodName, object[] args, VMContext context)
        {
            // If target is a string, it might be an object name
            if (target is string objName)
            {
                if (_objects.TryGetValue(objName, out var obj))
                {
                    if (obj.Methods.TryGetValue(methodName, out var method))
                    {
                        return method(target, args, context);
                    }
                    throw new VMException($"Unknown method '{methodName}' on '{objName}'");
                }
            }

            // Check if it's an IntrinsicObject directly
            if (target is IntrinsicObject intrinsicObj)
            {
                if (intrinsicObj.Methods.TryGetValue(methodName, out var method))
                {
                    return method(target, args, context);
                }
                throw new VMException($"Unknown method '{methodName}' on '{intrinsicObj.Name}'");
            }
            
            // Check if it's a SoundInstance
            if (target is SoundInstance soundInstance)
            {
                if (_objects.TryGetValue("SoundInstance", out var soundObj))
                {
                    if (soundObj.Methods.TryGetValue(methodName, out var method))
                    {
                        return method(target, args, context);
                    }
                    throw new VMException($"Unknown method '{methodName}' on sound instance '{soundInstance.Name}'");
                }
            }

            throw new VMException($"Cannot call method '{methodName}' on {target?.GetType().Name ?? "null"}");
        }

        public object GetMember(object target, string memberName, VMContext context)
        {
            // If target is a string, it might be an object name
            if (target is string objName)
            {
                if (_objects.TryGetValue(objName, out var obj))
                {
                    // Check for getter
                    if (obj.Getters.TryGetValue(memberName, out var getter))
                    {
                        return getter(target, context);
                    }
                    // Return a callable for methods (allows Canvas.render to work)
                    if (obj.Methods.ContainsKey(memberName))
                    {
                        return $"{objName}.{memberName}"; // Method reference
                    }
                }
            }
            
            // Check if it's a SoundInstance
            if (target is SoundInstance soundInstance)
            {
                if (_objects.TryGetValue("SoundInstance", out var soundObj))
                {
                    // Check for getter
                    if (soundObj.Getters.TryGetValue(memberName, out var getter))
                    {
                        return getter(target, context);
                    }
                }
            }

            throw new VMException($"Cannot get member '{memberName}' on {target?.GetType().Name ?? "null"}");
        }

        public void SetMember(object target, string memberName, object value, VMContext context)
        {
            if (target is string objName)
            {
                if (_objects.TryGetValue(objName, out var obj))
                {
                    if (obj.Setters.TryGetValue(memberName, out var setter))
                    {
                        setter(target, value, context);
                        return;
                    }
                }
            }

            throw new VMException($"Cannot set member '{memberName}' on {target?.GetType().Name ?? "null"}");
        }

        // Helper to check if an identifier is a known global object
        public bool IsGlobalObject(string name)
        {
            return _objects.ContainsKey(name);
        }

        public object GetGlobalObject(string name)
        {
            if (_objects.TryGetValue(name, out var obj))
            {
                return name; // Return name as handle
            }
            return null;
        }

        private static int ToInt(object value)
        {
            if (value is int i) return i;
            if (value is double d) return (int)d;
            if (value is string s && int.TryParse(s, out int parsed)) return parsed;
            return 0;
        }

        private static double ToDouble(object value)
        {
            if (value is double d) return d;
            if (value is int i) return i;
            if (value is string s && double.TryParse(s, out double parsed)) return parsed;
            return 0;
        }

        private static bool IsTruthy(object value)
        {
            if (value == null) return false;
            if (value is bool b) return b;
            if (value is double d) return d != 0;
            if (value is int i) return i != 0;
            if (value is string s) return s.Length > 0;
            return true;
        }

        private static Color ParseColor(string colorStr)
        {
            if (string.IsNullOrEmpty(colorStr))
            {
                return Color.white;
            }

            colorStr = colorStr.Trim().ToLowerInvariant();

            // Named colors
            var namedColors = new Dictionary<string, Color>
            {
                { "red", Color.red },
                { "green", Color.green },
                { "blue", Color.blue },
                { "white", Color.white },
                { "black", Color.black },
                { "yellow", Color.yellow },
                { "cyan", Color.cyan },
                { "magenta", Color.magenta },
                { "gray", Color.gray },
                { "grey", Color.gray },
                { "orange", new Color(1f, 0.5f, 0f) },
                { "purple", new Color(0.5f, 0f, 0.5f) },
                { "pink", new Color(1f, 0.75f, 0.8f) },
                { "brown", new Color(0.6f, 0.3f, 0f) },
                { "lime", new Color(0.5f, 1f, 0f) },
                { "navy", new Color(0f, 0f, 0.5f) },
                { "teal", new Color(0f, 0.5f, 0.5f) },
                { "olive", new Color(0.5f, 0.5f, 0f) },
                { "maroon", new Color(0.5f, 0f, 0f) },
                { "silver", new Color(0.75f, 0.75f, 0.75f) },
                { "gold", new Color(1f, 0.84f, 0f) }
            };

            if (namedColors.TryGetValue(colorStr, out Color namedColor))
                return namedColor;

            // Hex color
            if (colorStr.StartsWith("#"))
            {
                if (ColorUtility.TryParseHtmlString(colorStr, out Color hexColor))
                    return hexColor;
            }

            // RGB values
            if (colorStr.Contains(","))
            {
                string[] parts = colorStr.Split(',');
                if (parts.Length >= 3)
                {
                    if (float.TryParse(parts[0].Trim(), out float r) &&
                        float.TryParse(parts[1].Trim(), out float g) &&
                        float.TryParse(parts[2].Trim(), out float b))
                    {
                        // Check if values are 0-255 or 0-1
                        if (r > 1 || g > 1 || b > 1)
                        {
                            r /= 255f;
                            g /= 255f;
                            b /= 255f;
                        }
                        float a = parts.Length > 3 && float.TryParse(parts[3].Trim(), out float alpha) ? alpha : 1f;
                        if (a > 1) a /= 255f;
                        return new Color(r, g, b, a);
                    }
                }
            }
            
            // Fall back to white for unrecognized colors
            return Color.white;
        }

        private void RegisterSoundObject()
        {
            var sound = new IntrinsicObject("Sound");

            // Sound.create(name) - creates a named sound instance
            sound.Methods["create"] = (target, args, ctx) =>
            {
                if (args.Length < 1) 
                    throw new VMException("Sound.create requires a name parameter");
                
                string soundName = args[0]?.ToString();
                if (string.IsNullOrWhiteSpace(soundName))
                    throw new VMException("Sound.create requires a non-empty name");
                
                if (SoundManager.Instance == null)
                    throw new VMException("SoundManager not initialized");
                
                int terminalPID = ctx.GetInternal("terminalPID") as int? ?? 0;
                
                UnityEngine.Debug.Log($"[Sound.create] Creating sound '{soundName}' for terminal PID {terminalPID}");
                
                if (terminalPID == 0)
                    throw new VMException("Terminal PID not found");
                
                var player = SoundManager.Instance.CreateSound(terminalPID, soundName);
                if (player == null)
                    throw new VMException("Cannot create sound: maximum of 100 sounds per terminal reached");
                
                UnityEngine.Debug.Log($"[Sound.create] Successfully created sound '{soundName}'");
                
                return new SoundInstance(soundName, terminalPID);
            };

            // Sound.get(name) - retrieves a named sound instance
            sound.Methods["get"] = (target, args, ctx) =>
            {
                if (args.Length < 1) 
                    throw new VMException("Sound.get requires a name parameter");
                
                string soundName = args[0]?.ToString();
                if (string.IsNullOrWhiteSpace(soundName))
                    throw new VMException("Sound.get requires a non-empty name");
                
                if (SoundManager.Instance == null)
                    throw new VMException("SoundManager not initialized");
                
                int terminalPID = ctx.GetInternal("terminalPID") as int? ?? 0;
                if (terminalPID == 0)
                    throw new VMException("Terminal PID not found");
                
                var player = SoundManager.Instance.GetSound(terminalPID, soundName);
                if (player == null)
                    throw new VMException($"Sound '{soundName}' not found. Create it first with Sound.create()");
                
                return new SoundInstance(soundName, terminalPID);
            };

            // Sound.destroy(name) - destroys a named sound instance
            sound.Methods["destroy"] = (target, args, ctx) =>
            {
                if (args.Length < 1) 
                    throw new VMException("Sound.destroy requires a name parameter");
                
                string soundName = args[0]?.ToString();
                if (string.IsNullOrWhiteSpace(soundName))
                    throw new VMException("Sound.destroy requires a non-empty name");
                
                if (SoundManager.Instance == null)
                    return null;
                
                int terminalPID = ctx.GetInternal("terminalPID") as int? ?? 0;
                if (terminalPID == 0)
                    return null;
                
                SoundManager.Instance.DestroySound(terminalPID, soundName);
                return null;
            };

            // Sound.exists(name) - checks if a named sound instance exists
            sound.Methods["exists"] = (target, args, ctx) =>
            {
                if (args.Length < 1) 
                    throw new VMException("Sound.exists requires a name parameter");
                
                string soundName = args[0]?.ToString();
                if (string.IsNullOrWhiteSpace(soundName))
                    return false;
                
                if (SoundManager.Instance == null)
                    return false;
                
                int terminalPID = ctx.GetInternal("terminalPID") as int? ?? 0;
                if (terminalPID == 0)
                    return false;
                
                var player = SoundManager.Instance.GetSound(terminalPID, soundName);
                return player != null;
            };

            RegisterObject(sound);
            RegisterSoundInstanceObject();
        }

        private void RegisterSoundInstanceObject()
        {
            var soundInstance = new IntrinsicObject("SoundInstance");

            // instance.addNote(pitch, duration) or instance.addNote(pitch, duration, velocity)
            soundInstance.Methods["addNote"] = (target, args, ctx) =>
            {
                if (!(target is SoundInstance instance))
                    throw new VMException("Invalid sound instance");
                
                if (args.Length < 2) 
                    throw new VMException("addNote requires pitch and duration parameters");
                
                int pitch = ToInt(args[0]);
                float duration = (float)ToDouble(args[1]);
                float velocity = args.Length > 2 ? (float)ToDouble(args[2]) : 0.7f;
                
                var player = SoundManager.Instance?.GetSound(instance.TerminalPID, instance.Name);
                if (player == null)
                    throw new VMException($"Sound '{instance.Name}' no longer exists");
                
                player.AddNote(pitch, duration, velocity);
                return null;
            };

            // instance.clear()
            soundInstance.Methods["clear"] = (target, args, ctx) =>
            {
                if (!(target is SoundInstance instance))
                    throw new VMException("Invalid sound instance");
                
                var player = SoundManager.Instance?.GetSound(instance.TerminalPID, instance.Name);
                player?.Clear();
                return null;
            };

            // instance.play()
            soundInstance.Methods["play"] = (target, args, ctx) =>
            {
                if (!(target is SoundInstance instance))
                    throw new VMException("Invalid sound instance");
                
                var player = SoundManager.Instance?.GetSound(instance.TerminalPID, instance.Name);
                if (player == null)
                    throw new VMException($"Sound '{instance.Name}' no longer exists");
                
                player.Play();
                return null;
            };

            // instance.stop()
            soundInstance.Methods["stop"] = (target, args, ctx) =>
            {
                if (!(target is SoundInstance instance))
                    throw new VMException("Invalid sound instance");
                
                var player = SoundManager.Instance?.GetSound(instance.TerminalPID, instance.Name);
                player?.Stop();
                return null;
            };

            // instance.isPlaying (getter)
            soundInstance.Getters["isPlaying"] = (target, ctx) =>
            {
                if (!(target is SoundInstance instance))
                    return false;
                
                var player = SoundManager.Instance?.GetSound(instance.TerminalPID, instance.Name);
                return player?.IsPlaying ?? false;
            };

            // instance.setLoop(enabled)
            soundInstance.Methods["setLoop"] = (target, args, ctx) =>
            {
                if (!(target is SoundInstance instance))
                    throw new VMException("Invalid sound instance");
                
                if (args.Length < 1)
                    throw new VMException("setLoop requires a boolean parameter");
                
                bool enabled = IsTruthy(args[0]);
                
                var player = SoundManager.Instance?.GetSound(instance.TerminalPID, instance.Name);
                if (player != null)
                {
                    player.Loop = enabled;
                }
                return null;
            };

            // instance.loop (getter)
            soundInstance.Getters["loop"] = (target, ctx) =>
            {
                if (!(target is SoundInstance instance))
                    return false;
                
                var player = SoundManager.Instance?.GetSound(instance.TerminalPID, instance.Name);
                return player?.Loop ?? false;
            };

            RegisterObject(soundInstance);
        }
    }
}
