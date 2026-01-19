using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using GreyHackTerminalUI.Utils.Loaders;

namespace GreyHackTerminalUI.Utils
{
    internal static class NativeLibraryLoader
    {
        private static readonly INativeLibraryLoader _loader;
        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("NativeLibraryLoader");

        // Platform detection
        public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        public static string PlatformName => _loader.PlatformName;
        public static string LibExtension => _loader.LibExtension;
        public static string LibPrefix => _loader.LibPrefix;

        static NativeLibraryLoader()
        {
            if (IsWindows)
                _loader = new WindowsLibraryLoader();
            else if (IsMacOS)
                _loader = new MacOSLibraryLoader();
            else
                _loader = new LinuxLibraryLoader();
        }

        public static (IntPtr handle, string error) Load(string path) => _loader.Load(path);

        public static string GetLibraryFileName(string baseName)
        {
            return LibPrefix + baseName + LibExtension;
        }

        public static string GetAssemblyResourcePath()
        {
            var assemblyDir = Path.GetDirectoryName(typeof(NativeLibraryLoader).Assembly.Location);
            var subfolderPath = Path.Combine(assemblyDir, PluginInfo.PLUGIN_GUID);
            return subfolderPath;
        }

        public static string GetBepInExResourcePath()
        {
            var bepInExPlugins = Path.Combine(BepInEx.Paths.BepInExRootPath, "plugins");
            var pluginsSubfolder = Path.Combine(bepInExPlugins, PluginInfo.PLUGIN_GUID);
            return pluginsSubfolder;
        }

        public static string[] GetSearchPaths()
        {
            return new[] { GetAssemblyResourcePath(), GetBepInExResourcePath() }
                .Where(Directory.Exists)
                .ToArray();
        }


        public static string GetNativeResourcePath()
        {
            return new[] { GetAssemblyResourcePath(), GetBepInExResourcePath() }
                .FirstOrDefault(Directory.Exists) ?? GetAssemblyResourcePath();
        }

        public static IntPtr LoadWithDependencies(string libraryName, string[] dependencies)
        {
            var libFileName = GetLibraryFileName(libraryName);
            var searchPaths = GetSearchPaths();

            Log.LogInfo($"Platform: {PlatformName}");
            Log.LogInfo($"Searching for {libFileName} in {searchPaths.Length} paths...");

            foreach (var basePath in searchPaths)
            {
                Log.LogInfo($"Checking path: {basePath}");

                var libPath = Path.Combine(basePath, libFileName);
                if (!File.Exists(libPath))
                {
                    Log.LogInfo($"  {libFileName} not found");
                    continue;
                }

                Log.LogInfo($"  Found {libFileName}, loading dependencies...");

                if (!LoadDependencies(basePath, dependencies))
                {
                    Log.LogWarning("  Not all dependencies loaded, trying next path");
                    continue;
                }

                Log.LogInfo($"  Loading {libFileName}...");
                var (handle, error) = Load(libPath);

                if (handle != IntPtr.Zero)
                {
                    Log.LogInfo($"Successfully loaded {libraryName} from: {libPath}");
                    return handle;
                }

                Log.LogWarning($"Failed to load {libPath}: {error}");
            }

            Log.LogError($"Could not find {libFileName} native library in any search path");
            return IntPtr.Zero;
        }

        private static bool LoadDependencies(string basePath, string[] dependencies)
        {
            foreach (var dep in dependencies)
            {
                var depPath = Path.Combine(basePath, dep);

                if (!File.Exists(depPath))
                {
                    Log.LogWarning($"  Dependency {dep} not found at {depPath}");
                    return false;
                }

                Log.LogInfo($"  Loading {dep}...");
                var (handle, error) = Load(depPath);

                if (handle == IntPtr.Zero)
                {
                    Log.LogWarning($"  Failed to load {dep}: {error}");
                    return false;
                }

                Log.LogInfo($"  Loaded {dep} OK");
            }

            return true;
        }
    }
}
