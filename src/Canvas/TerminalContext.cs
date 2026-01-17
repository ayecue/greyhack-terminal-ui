using System.Collections.Generic;
using UnityEngine;
using BepInEx.Logging;
using GreyHackTerminalUI.VM;

namespace GreyHackTerminalUI.Canvas
{
    public class TerminalContext
    {
        private readonly int _terminalPID;
        private readonly ManualLogSource _logger;
        private readonly IntrinsicRegistry _intrinsics;
        
        // Terminal-specific state
        private CanvasWindow _window;
        private VirtualMachine _vm;
        private VMContext _vmContext;
        private List<Token> _accumulatedTokens = new List<Token>();
        
        // Execution state - per terminal to prevent interference
        private bool _isExecuting = false;
        private bool _hasVisibleWindow = false;
        private float _lastExecuteTime = 0f;
        
        // Batching configuration - increased to 60 FPS for smoother response
        private const float BATCH_INTERVAL = 0.016f; // ~60 FPS
        
        public int TerminalPID => _terminalPID;
        public CanvasWindow Window => _window;
        public bool HasVisibleWindow => _hasVisibleWindow;
        public bool HasPendingTokens => _accumulatedTokens.Count > 0;
        
        public TerminalContext(int terminalPID, IntrinsicRegistry intrinsics, ManualLogSource logger)
        {
            _terminalPID = terminalPID;
            _intrinsics = intrinsics;
            _logger = logger;
            
            // Create VM and context
            _vm = new VirtualMachine(_intrinsics);
            _vmContext = new VMContext();
            _vmContext.SetGlobal("Canvas", "Canvas");
            _vmContext.SetGlobal("Sound", "Sound");
            // Store terminal PID internally (not accessible to user scripts)
            _vmContext.SetInternal("terminalPID", terminalPID);
        }

        public CanvasWindow GetOrCreateWindow(Transform parent)
        {
            if (_window != null)
                return _window;

            _window = CanvasWindow.Create(parent, _terminalPID);
            _vmContext.SetGlobal("__canvasWindow", _window);

            _logger?.LogDebug($"[TerminalContext] Created window for terminal {_terminalPID}");
            return _window;
        }
          
        public bool AccumulateTokens(List<Token> tokens, bool replaceExisting = true)
        {
            if (_isExecuting)
            {
                _logger?.LogDebug($"[TerminalContext] Ignoring tokens while executing for terminal {_terminalPID}");
                return false;
            }
            
            if (replaceExisting && _accumulatedTokens.Count > 0)
            {
                _accumulatedTokens.Clear();
            }
            
            _accumulatedTokens.AddRange(tokens);
            return true;
        }
        
        public void MarkWindowVisible()
        {
            _hasVisibleWindow = true;
        }
        
        public bool ShouldExecute(float currentTime)
        {
            if (_accumulatedTokens.Count == 0)
                return false;
            
            // First frame should execute immediately
            if (!_hasVisibleWindow)
                return true;
            
            return (currentTime - _lastExecuteTime) >= BATCH_INTERVAL;
        }

        public void Execute()
        {
            if (_accumulatedTokens.Count == 0)
                return;
            
            // Take the tokens and clear
            var tokens = _accumulatedTokens;
            _accumulatedTokens = new List<Token>();
            
            _isExecuting = true;
            _lastExecuteTime = Time.time;
            
            try
            {
                // Compile
                var parser = new Parser();
                var compiler = new Compiler();
                var ast = parser.Parse(tokens);
                var chunk = compiler.Compile(ast);
                
                if (chunk == null)
                    return;
                
                // Ensure window exists
                if (_window == null)
                {
                    _logger?.LogWarning($"[TerminalContext] No window for terminal {_terminalPID}, cannot execute");
                    return;
                }
                
                // Stop any previous execution
                _vm.Stop();
                
                // Execute
                var result = _vm.Execute(chunk, _vmContext);
                
                if (result.Error != null)
                {
                    _logger?.LogError($"[TerminalContext] VM error for terminal {_terminalPID}: {result.Error}");
                }
            }
            catch (System.Exception ex)
            {
                _logger?.LogError($"[TerminalContext] Execution error for terminal {_terminalPID}: {ex.Message}");
            }
            finally
            {
                _isExecuting = false;
            }
        }
        
        public void Destroy()
        {
            _accumulatedTokens.Clear();
            
            if (_window != null)
            {
                Object.Destroy(_window.gameObject);
                _window = null;
            }
            
            _vmContext = null;
            _vm = null;
            
            _logger?.LogDebug($"[TerminalContext] Destroyed context for terminal {_terminalPID}");
        }
    }
}
