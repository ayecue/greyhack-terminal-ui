using System.Collections.Generic;
using System.Text;
using UnityEngine;
using BepInEx.Logging;
using GreyHackTerminalUI.VM;
using HarmonyLib;

namespace GreyHackTerminalUI.Canvas
{
    public class CanvasManager : MonoBehaviour
    {
        private static CanvasManager _instance;
        private static ManualLogSource _logger;

        // Single dictionary mapping terminal PID to its context
        private readonly Dictionary<int, TerminalContext> _terminals = new Dictionary<int, TerminalContext>();
        private readonly object _terminalsLock = new object();

        // Shared resources
        private IntrinsicRegistry _intrinsics;
        private Transform _parentCanvas;

        public static CanvasManager Instance => _instance;
        public int WindowCount => _terminals.Count;

        public static void Initialize(ManualLogSource logger)
        {
            if (_instance != null)
                return;

            _logger = logger;

            var managerGO = new GameObject("GreyHackCanvasManager");
            DontDestroyOnLoad(managerGO);
            _instance = managerGO.AddComponent<CanvasManager>();

            _logger?.LogDebug("CanvasManager initialized");
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            _intrinsics = new IntrinsicRegistry();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private Transform GetOrCreateParentCanvas()
        {
            if (_parentCanvas != null)
                return _parentCanvas;

            var canvasGO = new GameObject("CanvasManagerCanvas");
            var canvas = canvasGO.AddComponent<UnityEngine.Canvas>();
            
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            DontDestroyOnLoad(canvasGO);
            _parentCanvas = canvasGO.transform;

            return _parentCanvas;
        }

        public string ProcessOutput(string output, int terminalPID)
        {
            if (string.IsNullOrEmpty(output))
                return output;

            if (!output.Contains(Lexer.BLOCK_START))
                return output;

            var context = GetOrCreateContext(terminalPID);
            // Parse all UI blocks
            var lexer = new Lexer(output);
            int blockCount = 0;

            try
            {
                while (true)
                {
                    var tokens = lexer.NextUIBlock();
                    if (tokens == null)
                        break;

                    context.AccumulateTokens(tokens, replaceExisting: blockCount == 0);
                    blockCount++;
                }

                if (!context.HasVisibleWindow && context.HasPendingTokens)
                {
                    context.Execute();
                }
            }
            catch (System.Exception ex)
            {
                _logger?.LogError($"[CanvasManager] Error parsing UI blocks: {ex.Message}");
            }

            return StripUIBlocks(output);
        }

        private TerminalContext GetOrCreateContext(int terminalPID)
        {
            lock (_terminalsLock)
            {
                if (_terminals.TryGetValue(terminalPID, out var context))
                    return context;

                context = new TerminalContext(terminalPID, _intrinsics, _logger);
                context.GetOrCreateWindow(GetOrCreateParentCanvas());
                _terminals[terminalPID] = context;

                _logger?.LogDebug($"[CanvasManager] Created context for terminal {terminalPID}");
                return context;
            }
        }

        private void Update()
        {
            // Execute pending tokens for all terminals that are ready
            float now = Time.time;
            List<TerminalContext> contextsToExecute;
            lock (_terminalsLock)
            {
                contextsToExecute = new List<TerminalContext>(_terminals.Count);
                foreach (var ctx in _terminals.Values)
                {
                    if (ctx.ShouldExecute(now))
                    {
                        contextsToExecute.Add(ctx);
                    }
                }
            }

            foreach (var ctx in contextsToExecute)
            {
                ctx.Execute();
            }
        }

        private string StripUIBlocks(string output)
        {
            var result = new StringBuilder(output.Length);
            int i = 0;

            while (i < output.Length)
            {
                int blockStart = output.IndexOf(Lexer.BLOCK_START, i, System.StringComparison.Ordinal);
                if (blockStart == -1)
                {
                    result.Append(output.Substring(i));
                    break;
                }

                result.Append(output.Substring(i, blockStart - i));

                // Find matching closing brace
                int braceDepth = 0;
                int blockEnd = blockStart + 4;
                bool inString = false;
                char stringChar = '\0';

                while (blockEnd < output.Length)
                {
                    char c = output[blockEnd];

                    if (!inString && (c == '"' || c == '\''))
                    {
                        inString = true;
                        stringChar = c;
                    }
                    else if (inString && c == stringChar && (blockEnd == 0 || output[blockEnd - 1] != '\\'))
                    {
                        inString = false;
                    }
                    else if (!inString)
                    {
                        if (c == '{')
                        {
                            braceDepth++;
                        }
                        else if (c == '}')
                        {
                            if (braceDepth == 0)
                            {
                                blockEnd++;
                                break;
                            }
                            braceDepth--;
                        }
                    }

                    blockEnd++;
                }

                i = blockEnd;
            }

            return result.ToString().Trim();
        }

        // Public API

        public CanvasWindow GetOrCreateWindow(int terminalPID)
        {
            return GetOrCreateContext(terminalPID).Window;
        }

        public CanvasWindow GetWindow(int terminalPID)
        {
            lock (_terminalsLock)
            {
                if (_terminals.TryGetValue(terminalPID, out var context))
                    return context.Window;
                return null;
            }
        }

        public void MarkWindowVisible(int terminalPID)
        {
            lock (_terminalsLock)
            {
                if (_terminals.TryGetValue(terminalPID, out var context))
                    context.MarkWindowVisible();
            }
        }

        public void DestroyWindow(int terminalPID)
        {
            lock (_terminalsLock)
            {
                if (_terminals.TryGetValue(terminalPID, out var context))
                {
                    context.Destroy();
                    _terminals.Remove(terminalPID);
                    _logger?.LogDebug($"[CanvasManager] Destroyed terminal {terminalPID}");
                }
            }
        }

        public void DestroyAllWindows()
        {
            lock (_terminalsLock)
            {
                foreach (var context in _terminals.Values)
                {
                    context.Destroy();
                }
                _terminals.Clear();
                _logger?.LogDebug("[CanvasManager] Destroyed all terminals");
            }
        }
    }
}

