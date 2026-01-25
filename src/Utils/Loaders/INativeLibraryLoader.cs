using System;

namespace GreyHackTerminalUI.Utils.Loaders
{
    internal interface INativeLibraryLoader
    {
        string PlatformName { get; }
        string LibExtension { get; }
        string LibPrefix { get; }
        (IntPtr handle, string error) Load(string path);
    }
}
