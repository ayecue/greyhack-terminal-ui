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
        private Queue<List<Token>> _pendingBlocks = new Queue<List<Token>>();
        private readonly object _tokensLock = new object();
        
        // Execution state - per terminal to prevent interference
        private bool _isExecuting = false;
        private bool _hasVisibleWindow = false;
        private float _lastExecuteTime = 0f;
        
        // Batching configuration - increased to 60 FPS for smoother response
        private const float BATCH_INTERVAL = 0.016f; // ~60 FPS
        
        public int TerminalPID => _terminalPID;
        public CanvasWindow Window => _window;
        public bool HasVisibleWindow => _hasVisibleWindow;
        public bool HasPendingTokens 
        { 
            get 
            { 
                lock (_tokensLock) 
                { 
                    return _pendingBlocks.Count > 0; 
                } 
            } 
        }
        
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

        public CanvasWindow GetOrCreateWindow(RectTransform parent)
        {
            if (_window != null)
                return _window;

            _window = CanvasWindow.Create(parent, _terminalPID);
            _vmContext.SetGlobal("__canvasWindow", _window);

            _logger?.LogDebug($"[TerminalContext] Created window for terminal {_terminalPID}");
            return _window;
        }
          
        public void AccumulateTokens(List<Token> tokens)
        {
            // Queue each block separately - they must be parsed independently
            // This ensures sound commands aren't lost when new frames arrive
            lock (_tokensLock)
            {
                _pendingBlocks.Enqueue(tokens);
            }
        }
        
        public void MarkWindowVisible()
        {
            _hasVisibleWindow = true;
        }
        
        public bool ShouldExecute(float currentTime)
        {
            lock (_tokensLock)
            {
                if (_pendingBlocks.Count == 0)
                    return false;
            }
            
            // Don't start a new execution if one is already running
            if (_isExecuting)
                return false;
            
            // First frame should execute immediately
            if (!_hasVisibleWindow)
                return true;
            
            return (currentTime - _lastExecuteTime) >= BATCH_INTERVAL;
        }

        public void Execute()
        {
            // Take all pending blocks atomically
            List<List<Token>> blocksToExecute;
            lock (_tokensLock)
            {
                if (_pendingBlocks.Count == 0)
                    return;
                
                // Take all pending blocks
                blocksToExecute = new List<List<Token>>(_pendingBlocks);
                _pendingBlocks.Clear();
            }
            
            _isExecuting = true;
            _lastExecuteTime = Time.time;
            
            try
            {
                // Ensure window exists
                if (_window == null)
                {
                    _logger?.LogWarning($"[TerminalContext] No window for terminal {_terminalPID}, cannot execute");
                    return;
                }
                
                var parser = new Parser();
                var compiler = new Compiler();
                
                // Execute each block separately - they are independent programs
                foreach (var tokens in blocksToExecute)
                {
                    try
                    {
                        var ast = parser.Parse(tokens);
                        var chunk = compiler.Compile(ast);
                        
                        if (chunk == null)
                            continue;
                        
                        var result = _vm.Execute(chunk, _vmContext);
                        
                        if (result.Error != null)
                        {
                            _logger?.LogError($"[TerminalContext] VM error for terminal {_terminalPID}: {result.Error}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        _logger?.LogError($"[TerminalContext] Block execution error: {ex.Message}");
                    }
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
            lock (_tokensLock)
            {
                _pendingBlocks.Clear();
            }
            
            if (_window != null)
            {
                _window.Destroy();
                _window = null;
            }
            
            _vmContext = null;
            _vm = null;
            
            _logger?.LogDebug($"[TerminalContext] Destroyed context for terminal {_terminalPID}");
        }
    }
}
