using System;
using System.Runtime.InteropServices;

namespace GreyHackTerminalUI.Utils.Loaders
{
    internal class LinuxLibraryLoader : INativeLibraryLoader
    {
        [DllImport("libdl.so.2", EntryPoint = "dlopen")]
        private static extern IntPtr dlopen(string filename, int flags);

        [DllImport("libdl.so.2", EntryPoint = "dlerror")]
        private static extern IntPtr dlerror();

        private const int RTLD_NOW = 0x2;
        private const int RTLD_GLOBAL = 0x8;

        public string PlatformName => "Linux";
        public string LibExtension => ".so";
        public string LibPrefix => "lib";

        public (IntPtr handle, string error) Load(string path)
        {
            var handle = dlopen(path, RTLD_NOW | RTLD_GLOBAL);
            var error = handle == IntPtr.Zero ? GetError() : null;
            return (handle, error);
        }

        private static string GetError()
        {
            var errPtr = dlerror();
            return errPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(errPtr) : "Unknown error";
        }
    }
}
