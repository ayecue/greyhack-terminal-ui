using System;
using System.Runtime.InteropServices;

namespace GreyHackTerminalUI.Utils.Loaders
{
    internal class WindowsLibraryLoader : INativeLibraryLoader
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryW(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetLastError();

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int FormatMessageW(uint dwFlags, IntPtr lpSource, uint dwMessageId,
            uint dwLanguageId, System.Text.StringBuilder lpBuffer, int nSize, IntPtr Arguments);

        private const uint FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;

        public string PlatformName => "Windows";
        public string LibExtension => ".dll";
        public string LibPrefix => "";

        public (IntPtr handle, string error) Load(string path)
        {
            var handle = LoadLibraryW(path);
            var error = handle == IntPtr.Zero ? GetError() : null;
            return (handle, error);
        }

        private static string GetError()
        {
            uint errorCode = GetLastError();
            if (errorCode == 0) return "Unknown error";

            var sb = new System.Text.StringBuilder(512);
            FormatMessageW(FORMAT_MESSAGE_FROM_SYSTEM, IntPtr.Zero, errorCode, 0, sb, sb.Capacity, IntPtr.Zero);
            return $"Error {errorCode}: {sb.ToString().Trim()}";
        }
    }
}
