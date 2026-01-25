using System.Collections.Generic;
using System.Text;
using UnityEngine;
using BepInEx.Logging;
using GreyHackTerminalUI.VM;
using GreyHackTerminalUI.Sound;
using GreyHackTerminalUI.Settings;
using GreyHackTerminalUI.Canvas;

namespace GreyHackTerminalUI.Runtime
{
    public class RuntimeManager : MonoBehaviour
    {
        private static RuntimeManager _instance;
        private static ManualLogSource _logger;

        // Single dictionary mapping terminal PID to its context
        private readonly Dictionary<int, TerminalContext> _terminals = new Dictionary<int, TerminalContext>();
        private readonly object _terminalsLock = new object();

        // Shared resources
        private IntrinsicRegistry _intrinsics;

        public static RuntimeManager Instance => _instance;
        public int WindowCount => _terminals.Count;

        public static void Initialize(ManualLogSource logger)
        {
            if (_instance != null)
                return;

            _logger = logger;
            
            // Initialize CanvasManager first
            CanvasManager.Initialize(logger);

            var managerGO = new GameObject("GreyHackRuntimeManager");
            DontDestroyOnLoad(managerGO);
            _instance = managerGO.AddComponent<RuntimeManager>();

            _logger?.LogDebug("RuntimeManager initialized");
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

        public string ProcessOutput(string output, int terminalPID)
        {
            if (string.IsNullOrEmpty(output))
                return output;

            if (!output.Contains(Lexer.BLOCK_START))
                return output;

            // Check if canvas is enabled in settings
            if (PluginSettings.CanvasEnabled != null && !PluginSettings.CanvasEnabled.Value)
            {
                // Strip UI blocks but don't process them
                return StripUIBlocks(output);
            }

            var context = GetOrCreateContext(terminalPID);
            // Parse all UI blocks
            var lexer = new Lexer(output);

            try
            {
                while (true)
                {
                    var tokens = lexer.NextUIBlock();
                    if (tokens == null)
                        break;

                    context.AccumulateTokens(tokens);
                }

                if (!context.HasVisibleWindow && context.HasPendingTokens)
                {
                    context.Execute();
                }
            }
            catch (System.Exception ex)
            {
                _logger?.LogError($"[RuntimeManager] Error parsing UI blocks: {ex.Message}");
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
                context.GetOrCreateWindow(CanvasManager.Instance.GetOrCreateParentCanvas());
                
                // Initialize browser asynchronously if CEF engine is configured
                _ = context.InitializeBrowserAsync();
                
                _terminals[terminalPID] = context;

                _logger?.LogDebug($"[RuntimeManager] Created context for terminal {terminalPID}");
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
                    _logger?.LogDebug($"[RuntimeManager] Destroyed terminal {terminalPID}");
                }
            }
            
            // Also destroy all sound instances for this terminal
            SoundManager.Instance?.DestroyAllSounds(terminalPID);
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
                _logger?.LogDebug("[RuntimeManager] Destroyed all terminals");
            }
        }
    }
}
