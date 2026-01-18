using HarmonyLib;
using BepInEx.Logging;
using GreyHackTerminalUI.Canvas;
using GreyHackTerminalUI.Utils;

namespace GreyHackTerminalUI.Patches
{
    [HarmonyPatch]
    public static class TerminalPatches
    {
        private static ManualLogSource _logger;

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }

        [HarmonyPatch(typeof(PlayerClientMethods), nameof(PlayerClientMethods.PrintSentClientRpc))]
        [HarmonyPrefix]
        public static bool Prefix_PrintSentClientRpc(
            PlayerClientMethods __instance,
            ref byte[] zipOutput,
            bool replaceText,
            int windowPID)
        {
            // Only process if we have data and the canvas manager is initialized
            if (zipOutput == null || zipOutput.Length == 0 || CanvasManager.Instance == null)
            {
                return true; // Continue with original method
            }

            try
            {
                // Decompress the output using our helper
                string output = StringCompressorHelper.Unzip(zipOutput);

                // Quick check for UI blocks
                if (!output.Contains(VM.Lexer.BLOCK_START))
                {
                    return true; // No UI blocks, continue normally
                }

                _logger?.LogDebug($"Intercepted print with UI blocks for window {windowPID}");

                // Process the output and get the stripped version
                string processedOutput = CanvasManager.Instance.ProcessOutput(output, windowPID);

                // If the processed output is different (blocks were stripped)
                if (processedOutput != output)
                {
                    // If there's still content to display, recompress and replace
                    if (!string.IsNullOrWhiteSpace(processedOutput))
                    {
                        zipOutput = StringCompressorHelper.Zip(processedOutput);
                    }
                    else
                    {
                        // No content left after stripping blocks, skip the original call
                        return false;
                    }
                }
            }
            catch (System.Exception ex)
            {
                _logger?.LogError($"Error processing UI blocks: {ex}");
            }

            return true; // Continue with (potentially modified) output
        }

        [HarmonyPatch(typeof(PlayerClientMethods), nameof(PlayerClientMethods.CloseProgramClientRpc))]
        [HarmonyPostfix]
        public static void Postfix_CloseProgramClientRpc(int PID, byte[] zipProcs, bool isScript)
        {
            CanvasManager.Instance.DestroyWindow(PID);
        }

        [HarmonyPatch(typeof(Terminal), "CloseWindow")]
        [HarmonyPostfix]
        static void Postfix_CloseWindow(Terminal __instance)
        {
            CanvasManager.Instance.DestroyWindow(__instance.GetPID());
        }

        [HarmonyPatch(typeof(Ventana), nameof(Ventana.CloseTaskBar), new System.Type[] { })]
        [HarmonyPostfix]
        static void Postfix_CloseTaskBar(Ventana __instance)
        {
            CanvasManager.Instance.DestroyWindow(__instance.GetPID());
        }
    }
}
