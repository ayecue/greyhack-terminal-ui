using System.Collections.Generic;
using System.Text;
using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using GreyHackTerminalUI.VM;
#if BEPINEX6
using BepInEx.Unity.Mono;
#endif

namespace GreyHackTerminalUI.Canvas
{
    internal class PendingUIBlock
    {
        public int TerminalPID { get; set; }
        public CompiledChunk Chunk { get; set; }
    }

    // LRU cache entry for compiled scripts
    internal class CachedScript
    {
        public CompiledChunk Chunk { get; set; }
        public long LastUsed { get; set; }
    }

    public class CanvasManager : MonoBehaviour
    {
        private static CanvasManager _instance;
        private static ManualLogSource _logger;

        // Dictionary mapping terminal PID to canvas window
        private Dictionary<int, CanvasWindow> _canvasWindows = new Dictionary<int, CanvasWindow>();

        // Dictionary mapping terminal PID to VM context (persistent state)
        private Dictionary<int, VMContext> _vmContexts = new Dictionary<int, VMContext>();

        // Dictionary mapping terminal PID to VM instance (one VM per terminal)
        private Dictionary<int, VirtualMachine> _virtualMachines = new Dictionary<int, VirtualMachine>();

        // Accumulated tokens per terminal - collect all scripts within a time window
        private Dictionary<int, List<Token>> _accumulatedTokens = new Dictionary<int, List<Token>>();
        
        // Track which terminals have shown their window (to ensure first show always executes)
        private HashSet<int> _terminalsWithVisibleWindow = new HashSet<int>();
        
        // Time-based batching: collect scripts for this duration before executing
        private const float BATCH_INTERVAL = 0.033f; // ~30 FPS
        private float _lastBatchTime = 0f;
        private bool _isExecuting = false;

        // Lock for thread-safe access
        private readonly object _pendingLock = new object();

        // Shared intrinsics registry
        private IntrinsicRegistry _intrinsics;

        // The parent canvas for all our windows
        private Transform _parentCanvas;

        public static CanvasManager Instance => _instance;

        public static void Initialize(ManualLogSource logger)
        {
            if (_instance != null)
            {
                return;
            }

            _logger = logger;

            // Create a persistent GameObject for the manager
            GameObject managerGO = new GameObject("GreyHackCanvasManager");
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
            {
                _instance = null;
            }
        }

        private Transform GetOrCreateParentCanvas()
        {
            if (_parentCanvas != null)
                return _parentCanvas;

            // Try to find the game's computer canvas (used by Grey Hack for windows)
            var helperCanvas = Object.FindObjectOfType<HelperComputerCanvas>();
            if (helperCanvas != null && helperCanvas.computerCanvas != null)
            {
                _parentCanvas = helperCanvas.computerCanvas.transform;
                _logger?.LogDebug("Found game's computer canvas as parent");
                return _parentCanvas;
            }

            // Fallback: Find any Canvas in the scene
            var existingCanvas = Object.FindObjectOfType<UnityEngine.Canvas>();
            if (existingCanvas != null)
            {
                _parentCanvas = existingCanvas.transform;
                _logger?.LogDebug("Using existing canvas as parent");
                return _parentCanvas;
            }

            // Last resort: Create our own canvas
            _logger?.LogDebug("Creating new canvas for windows");
            GameObject canvasGO = new GameObject("CanvasManagerCanvas");
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

            // Check if there are any UI blocks
            if (!output.Contains(Lexer.BLOCK_START))
                return output;

            // If we're currently executing, ignore incoming scripts
            if (_isExecuting)
            {
                _blocksIgnoredThisSecond++;
                _logger?.LogDebug($"[CanvasManager] Ignoring UI blocks while executing");
                return StripUIBlocks(output);
            }

            _logger?.LogDebug($"[CanvasManager] Found #UI{{ in output for terminal {terminalPID}, length={output.Length}");

            // Clear any previously accumulated tokens - we only want the LATEST frame
            // Each ProcessOutput call represents a complete frame from the game
            lock (_pendingLock)
            {
                if (_accumulatedTokens.ContainsKey(terminalPID) && _accumulatedTokens[terminalPID].Count > 0)
                {
                    _blocksReplacedThisSecond++;
                    _accumulatedTokens[terminalPID].Clear();
                }
            }

            // Create lexer and find all UI blocks
            Lexer lexer = new Lexer(output);
            int blockCount = 0;

            try
            {
                while (true)
                {
                    // Try to get next UI block
                    var tokens = lexer.NextUIBlock();

                    if (tokens == null)
                    {
                        _logger?.LogDebug($"[CanvasManager] No more UI blocks found, total blocks: {blockCount}");
                        break;
                    }

                    blockCount++;
                    _blocksReceivedThisSecond++;
                    
                    // Store tokens for this terminal - accumulate within a single print() call
                    lock (_pendingLock)
                    {
                        if (!_accumulatedTokens.ContainsKey(terminalPID))
                        {
                            _accumulatedTokens[terminalPID] = new List<Token>();
                        }
                        _accumulatedTokens[terminalPID].AddRange(tokens);
                    }
                }
                
                _logger?.LogDebug($"[CanvasManager] Accumulated {blockCount} blocks for terminal {terminalPID}");
                
                // If this is the first frame (window not shown yet), execute immediately
                bool isFirstFrame = !_terminalsWithVisibleWindow.Contains(terminalPID);
                if (isFirstFrame)
                {
                    _logger?.LogDebug($"[CanvasManager] First frame for terminal {terminalPID} - executing immediately");
                    ExecuteAccumulatedTokens(terminalPID);
                }
            }
            catch (System.Exception ex)
            {
                _logger?.LogError($"[CanvasManager] Error parsing UI blocks: {ex.Message}\n{ex.StackTrace}");
            }

            // Strip all UI blocks from output
            return StripUIBlocks(output);
        }

        // Debug counter for logging
        private int _frameCount = 0;
        private float _lastLogTime = 0f;
        private int _blocksReceivedThisSecond = 0;
        private int _blocksExecutedThisSecond = 0;
        private int _blocksIgnoredThisSecond = 0;
        private int _blocksReplacedThisSecond = 0;

        private void Update()
        {
            _frameCount++;
            
            // Log stats every second
            float now = Time.time;
            if (now - _lastLogTime >= 1.0f)
            {
                _logger?.LogInfo($"[CanvasManager] Stats: received={_blocksReceivedThisSecond}, executed={_blocksExecutedThisSecond}, ignored={_blocksIgnoredThisSecond}, replaced={_blocksReplacedThisSecond}");
                _blocksReceivedThisSecond = 0;
                _blocksExecutedThisSecond = 0;
                _blocksIgnoredThisSecond = 0;
                _blocksReplacedThisSecond = 0;
                _lastLogTime = now;
            }
            
            // Time-based batching: execute accumulated tokens at regular intervals
            float currentTime = Time.time;
            
            if (currentTime - _lastBatchTime < BATCH_INTERVAL)
                return;
            
            _lastBatchTime = currentTime;
            
            // Execute accumulated tokens for all terminals
            List<int> terminalsToExecute = new List<int>();
            
            lock (_pendingLock)
            {
                foreach (var kvp in _accumulatedTokens)
                {
                    if (kvp.Value.Count > 0)
                    {
                        terminalsToExecute.Add(kvp.Key);
                    }
                }
            }
            
            foreach (var terminalPID in terminalsToExecute)
            {
                ExecuteAccumulatedTokens(terminalPID);
            }
        }

        private void ExecuteAccumulatedTokens(int terminalPID)
        {
            List<Token> tokens;
            
            lock (_pendingLock)
            {
                if (!_accumulatedTokens.TryGetValue(terminalPID, out tokens) || tokens.Count == 0)
                    return;
                
                // Take the tokens and clear the accumulator
                _accumulatedTokens[terminalPID] = new List<Token>();
            }
            
            _isExecuting = true;
            
            try
            {
                // Compile and execute
                var parser = new Parser();
                var compiler = new Compiler();
                var ast = parser.Parse(tokens);
                CompiledChunk chunk = compiler.Compile(ast);
                
                if (chunk != null)
                {
                    // Log chunk info for debugging
                    _logger?.LogDebug($"[CanvasManager] Compiled chunk: {chunk.Code.Count} bytecodes, {chunk.Constants.Count} constants, {chunk.Names.Count} names");
                    if (chunk.Constants.Count > 0 && chunk.Constants.Count <= 10)
                    {
                        _logger?.LogDebug($"[CanvasManager] Constants: [{string.Join(", ", chunk.Constants)}]");
                    }
                    if (chunk.Names.Count > 0 && chunk.Names.Count <= 10)
                    {
                        _logger?.LogDebug($"[CanvasManager] Names: [{string.Join(", ", chunk.Names)}]");
                    }
                    
                    _blocksExecutedThisSecond++;
                    _logger?.LogDebug($"[CanvasManager] Executing {tokens.Count} tokens for terminal {terminalPID}");
                    ExecuteBlock(new PendingUIBlock
                    {
                        TerminalPID = terminalPID,
                        Chunk = chunk
                    });
                }
            }
            catch (System.Exception ex)
            {
                _logger?.LogError($"[CanvasManager] Error executing accumulated tokens: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                _isExecuting = false;
            }
        }

        private void ExecuteBlock(PendingUIBlock block)
        {
            _logger?.LogDebug($"[CanvasManager] Executing UI block for terminal {block.TerminalPID}");

            try
            {
                // Get the VM for this terminal (creates VM and context on first call)
                VirtualMachine vm = GetOrCreateVM(block.TerminalPID);
                VMContext context = _vmContexts[block.TerminalPID];
                
                // Stop any previous execution before starting new one
                vm.Stop();
                
                // Execute synchronously on main thread - simpler and avoids threading issues
                try
                {
                    var result = vm.Execute(block.Chunk, context);

                    if (result.Error != null)
                    {
                        _logger?.LogError($"[CanvasManager] VM execution error: {result.Error}");
                    }
                    else
                    {
                        _logger?.LogDebug($"[CanvasManager] VM executed successfully, result: {result.ReturnValue ?? "null"}");
                    }
                }
                catch (System.Exception execEx)
                {
                    _logger?.LogError($"[CanvasManager] VM execution exception: {execEx.Message}");
                }
            }
            catch (System.Exception ex)
            {
                _logger?.LogError($"[CanvasManager] Error executing UI block: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private string StripUIBlocks(string output)
        {
            // Pre-allocate with expected capacity to reduce allocations
            var result = new StringBuilder(output.Length);
            int i = 0;

            while (i < output.Length)
            {
                // Look for #UI{
                int blockStart = output.IndexOf(Lexer.BLOCK_START, i, System.StringComparison.Ordinal);
                if (blockStart == -1)
                {
                    // No more blocks, append rest
                    result.Append(output.Substring(i));
                    break;
                }

                // Append everything before the block
                result.Append(output.Substring(i, blockStart - i));

                // Find matching closing brace
                int braceDepth = 0;
                int blockEnd = blockStart + 4; // After #UI{
                bool inString = false;
                char stringChar = '\0';

                while (blockEnd < output.Length)
                {
                    char c = output[blockEnd];

                    // Handle strings (don't count braces inside strings)
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
                                // Found the matching close brace
                                blockEnd++;
                                break;
                            }
                            braceDepth--;
                        }
                    }

                    blockEnd++;
                }

                // Move past this block
                i = blockEnd;
            }

            return result.ToString().Trim();
        }

        // Compute a hash for script content to use as cache key
        private int ComputeScriptHash(string scriptContent)
        {
            // Simple but fast hash - good enough for cache key
            int hash = 17;
            foreach (char c in scriptContent)
            {
                hash = hash * 31 + c;
            }
            return hash;
        }

        // Compile script fresh each time (caching disabled for debugging)
        private CompiledChunk GetOrCompileScript(List<Token> tokens, string scriptContent)
        {
            // Compile fresh each time - no caching, no shared state
            var parser = new Parser();
            var compiler = new Compiler();
            var ast = parser.Parse(tokens);
            CompiledChunk chunk = compiler.Compile(ast);
            return chunk;
        }

        private VirtualMachine GetOrCreateVM(int terminalPID)
        {
            if (_virtualMachines.TryGetValue(terminalPID, out VirtualMachine vm))
            {
                return vm;
            }

            // Create new VM and context together
            vm = new VirtualMachine(_intrinsics);
            _virtualMachines[terminalPID] = vm;
            
            // Create context and set up globals once
            VMContext context = new VMContext();
            _vmContexts[terminalPID] = context;
            
            // Get or create the window and set it in globals once
            CanvasWindow window = GetOrCreateWindow(terminalPID);
            context.SetGlobal("__canvasWindow", window);
            context.SetGlobal("Canvas", "Canvas"); // Object reference
            
            _logger?.LogDebug($"[CanvasManager] Created new VM and context for terminal {terminalPID}");
            return vm;
        }

        public CanvasWindow GetOrCreateWindow(int terminalPID)
        {
            if (_canvasWindows.TryGetValue(terminalPID, out CanvasWindow existingWindow))
            {
                if (existingWindow != null)
                    return existingWindow;

                // Window was destroyed, remove from dictionary
                _canvasWindows.Remove(terminalPID);
            }

            // Create new window
            Transform parent = GetOrCreateParentCanvas();
            if (parent == null)
            {
                _logger?.LogError("Could not find or create parent canvas for windows");
                return null;
            }

            CanvasWindow newWindow = CanvasWindow.Create(parent, terminalPID);
            _canvasWindows[terminalPID] = newWindow;

            _logger?.LogDebug($"Created new canvas window for terminal {terminalPID}");

            return newWindow;
        }

        public CanvasWindow GetWindow(int terminalPID)
        {
            if (_canvasWindows.TryGetValue(terminalPID, out CanvasWindow window))
            {
                return window;
            }
            return null;
        }

        public void MarkWindowVisible(int terminalPID)
        {
            _terminalsWithVisibleWindow.Add(terminalPID);
            _logger?.LogDebug($"Marked terminal {terminalPID} as having visible window");
        }

        public void DestroyWindow(int terminalPID)
        {
            // Remove any accumulated tokens for this terminal
            lock (_pendingLock)
            {
                _accumulatedTokens.Remove(terminalPID);
            }

            // Clean up visibility tracking
            _terminalsWithVisibleWindow.Remove(terminalPID);

            if (_canvasWindows.TryGetValue(terminalPID, out CanvasWindow window))
            {
                if (window != null)
                {
                    Destroy(window.gameObject);
                }
                _canvasWindows.Remove(terminalPID);
                _logger?.LogDebug($"Destroyed canvas window for terminal {terminalPID}");
            }

            // Also clean up VM context and VM instance
            if (_vmContexts.ContainsKey(terminalPID))
            {
                _vmContexts.Remove(terminalPID);
                _logger?.LogDebug($"Cleaned up VM context for terminal {terminalPID}");
            }
            
            if (_virtualMachines.ContainsKey(terminalPID))
            {
                _virtualMachines.Remove(terminalPID);
                _logger?.LogDebug($"Cleaned up VM instance for terminal {terminalPID}");
            }
        }

        public void DestroyAllWindows()
        {
            // Clear all accumulated tokens
            lock (_pendingLock)
            {
                _accumulatedTokens.Clear();
            }
            
            // Clear visibility tracking
            _terminalsWithVisibleWindow.Clear();

            foreach (var kvp in _canvasWindows)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            _canvasWindows.Clear();
            _vmContexts.Clear();
            _virtualMachines.Clear();
            _logger?.LogDebug("Destroyed all canvas windows, VM contexts, and VM instances");
        }

        public int WindowCount => _canvasWindows.Count;
    }
}
