using System.Collections.Generic;
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

        // Dictionary mapping terminal PID to VM busy state
        private Dictionary<int, bool> _vmBusy = new Dictionary<int, bool>();

        // Dictionary mapping terminal PID to queue of pending blocks (max 20 per terminal)
        private Dictionary<int, Queue<PendingUIBlock>> _pendingBlocks = new Dictionary<int, Queue<PendingUIBlock>>();
        private const int MAX_QUEUED_BLOCKS = 20;

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

            _logger?.LogDebug($"[CanvasManager] Found #UI{{ in output for terminal {terminalPID}, length={output.Length}");

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
                    _logger?.LogDebug($"[CanvasManager] Tokenized block {blockCount}, token count: {tokens.Count}");

                    // Parse the tokens
                    Parser parser = new Parser();
                    var ast = parser.Parse(tokens);
                    _logger?.LogDebug($"[CanvasManager] Parsed block {blockCount}, statements: {ast.Statements.Count}");

                    // Compile to bytecode
                    Compiler compiler = new Compiler();
                    CompiledChunk chunk = compiler.Compile(ast);
                    _logger?.LogDebug($"[CanvasManager] Compiled block {blockCount}, code length: {chunk.Code.Count}");

                    if (chunk != null)
                    {
                        lock (_pendingLock)
                        {
                            if (!_pendingBlocks.TryGetValue(terminalPID, out Queue<PendingUIBlock> queue))
                            {
                                queue = new Queue<PendingUIBlock>();
                                _pendingBlocks[terminalPID] = queue;
                            }

                            // Remove oldest block if at max capacity
                            if (queue.Count >= MAX_QUEUED_BLOCKS)
                            {
                                queue.Dequeue();
                                _logger?.LogWarning($"[CanvasManager] Queue full for terminal {terminalPID}, dropping oldest block");
                            }

                            queue.Enqueue(new PendingUIBlock
                            {
                                TerminalPID = terminalPID,
                                Chunk = chunk
                            });
                        }

                        _logger?.LogDebug($"[CanvasManager] Queued UI block for terminal {terminalPID}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                _logger?.LogError($"[CanvasManager] Error parsing UI blocks: {ex.Message}\n{ex.StackTrace}");
            }

            // Strip all UI blocks from output
            return StripUIBlocks(output);
        }

        private void Update()
        {
            ExecutePendingBlocks();
        }

        private void ExecutePendingBlocks()
        {
            // Execute one block per terminal per frame
            List<PendingUIBlock> blocksToExecute = new List<PendingUIBlock>();
            List<int> emptyTerminals = new List<int>();

            lock (_pendingLock)
            {
                if (_pendingBlocks.Count == 0)
                    return;

                foreach (var kvp in _pendingBlocks)
                {
                    int terminalPID = kvp.Key;
                    
                    // Only execute if VM is not busy
                    if (kvp.Value.Count > 0 && !_vmBusy.GetValueOrDefault(terminalPID, false))
                    {
                        blocksToExecute.Add(kvp.Value.Dequeue());
                        if (kvp.Value.Count == 0)
                            emptyTerminals.Add(kvp.Key);
                    }
                }

                foreach (var terminalPID in emptyTerminals)
                    _pendingBlocks.Remove(terminalPID);
            }

            // Execute each block
            foreach (var block in blocksToExecute)
            {
                ExecuteBlock(block);
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
                
                // Mark VM as busy
                lock (_pendingLock)
                {
                    _vmBusy[block.TerminalPID] = true;
                }
                
                // Execute the bytecode on background thread using BepInEx ThreadingHelper
                ThreadingHelper.Instance.StartAsyncInvoke(() => () =>
                {
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
                    finally
                    {
                        // Clear busy flag when execution completes
                        lock (_pendingLock)
                        {
                            _vmBusy[block.TerminalPID] = false;
                        }
                    }
                });
            }
            catch (System.Exception ex)
            {
                _logger?.LogError($"[CanvasManager] Error executing UI block: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private string StripUIBlocks(string output)
        {
            var result = new System.Text.StringBuilder();
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

        public void DestroyWindow(int terminalPID)
        {
            // Clear any pending blocks and busy flag for this terminal
            lock (_pendingLock)
            {
                _pendingBlocks.Remove(terminalPID);
                _vmBusy.Remove(terminalPID);
            }

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
            // Clear all pending blocks and busy flags
            lock (_pendingLock)
            {
                _pendingBlocks.Clear();
                _vmBusy.Clear();
            }

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
