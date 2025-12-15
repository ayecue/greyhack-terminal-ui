using System;
using System.Reflection;
using BepInEx.Logging;

namespace GreyHackTerminalUI.Utils
{
    public static class StringCompressorHelper
    {
        private static Type _stringCompressorType;
        private static MethodInfo _unzipMethod;
        private static MethodInfo _zipMethod;
        private static bool _initialized;
        private static ManualLogSource _logger;

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;

            if (_initialized)
                return;

            try
            {
                // Find the StringCompressor type in Assembly-CSharp
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == "Assembly-CSharp")
                    {
                        _stringCompressorType = assembly.GetType("CompressString.StringCompressor");
                        if (_stringCompressorType != null)
                        {
                            _unzipMethod = _stringCompressorType.GetMethod("Unzip",
                                BindingFlags.Public | BindingFlags.Static,
                                null,
                                new Type[] { typeof(byte[]) },
                                null);

                            _zipMethod = _stringCompressorType.GetMethod("Zip",
                                BindingFlags.Public | BindingFlags.Static,
                                null,
                                new Type[] { typeof(string) },
                                null);

                            if (_unzipMethod != null && _zipMethod != null)
                            {
                                _initialized = true;
                                _logger?.LogDebug("StringCompressor methods found via reflection");
                            }
                            else
                            {
                                _logger?.LogWarning("StringCompressor type found but methods not found");
                            }
                        }
                        break;
                    }
                }

                if (!_initialized)
                {
                    _logger?.LogWarning("StringCompressor type not found, using fallback compression");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error initializing StringCompressor helper: {ex}");
            }
        }
        
        public static string Unzip(byte[] data)
        {
            if (data == null || data.Length == 0)
                return "";

            if (_initialized && _unzipMethod != null)
            {
                try
                {
                    return (string)_unzipMethod.Invoke(null, new object[] { data });
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Error calling StringCompressor.Unzip: {ex}");
                }
            }

            // Fallback: try standard GZip decompression
            return FallbackUnzip(data);
        }

        public static byte[] Zip(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new byte[0];

            if (_initialized && _zipMethod != null)
            {
                try
                {
                    return (byte[])_zipMethod.Invoke(null, new object[] { text });
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Error calling StringCompressor.Zip: {ex}");
                }
            }

            // Fallback: try standard GZip compression
            return FallbackZip(text);
        }

        private static string FallbackUnzip(byte[] data)
        {
            try
            {
                using (var compressedStream = new System.IO.MemoryStream(data))
                using (var decompressor = new System.IO.Compression.GZipStream(compressedStream, System.IO.Compression.CompressionMode.Decompress))
                using (var reader = new System.IO.StreamReader(decompressor))
                {
                    return reader.ReadToEnd();
                }
            }
            catch
            {
                // If GZip fails, try returning as UTF8 string directly
                try
                {
                    return System.Text.Encoding.UTF8.GetString(data);
                }
                catch
                {
                    return "";
                }
            }
        }

        private static byte[] FallbackZip(string text)
        {
            try
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(text);
                using (var output = new System.IO.MemoryStream())
                {
                    using (var compressor = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionMode.Compress))
                    {
                        compressor.Write(bytes, 0, bytes.Length);
                    }
                    return output.ToArray();
                }
            }
            catch
            {
                return System.Text.Encoding.UTF8.GetBytes(text);
            }
        }
    }
}
