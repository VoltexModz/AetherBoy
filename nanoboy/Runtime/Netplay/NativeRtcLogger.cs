using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace AetherBoy.Runtime.Netplay;

/// <summary>The C logger has process lifetime and no peer/user pointer. Never attribute it to a peer.</summary>
internal static class NativeRtcLogger
{
    private static readonly object Sync = new();
    private static readonly Dictionary<OnlineRoomDiagnostics, string[]> Listeners = new();
    // A static root is essential: rtcInitLogger retains this function pointer past an individual peer.
    private static readonly LogCallback Callback = OnLog;
    private static bool initialized;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void LogCallback(int level, IntPtr message);
    [DllImport("datachannel", CallingConvention = CallingConvention.Cdecl)]
    private static extern void rtcInitLogger(int level, LogCallback callback);

    internal static void Register(OnlineRoomDiagnostics diagnostics, IceServer[] servers)
    {
        lock (Sync)
        {
            if (!initialized) { rtcInitLogger(5, Callback); initialized = true; }
            var secrets = new List<string>();
            foreach (var server in servers)
                foreach (string value in new[] { server.Username, server.Credential })
                    if (!string.IsNullOrEmpty(value)) { secrets.Add(value); secrets.Add(Uri.EscapeDataString(value)); }
            Listeners[diagnostics] = secrets.ToArray();
        }
    }

    internal static void Unregister(OnlineRoomDiagnostics diagnostics) { lock (Sync) Listeners.Remove(diagnostics); }

    internal static string Sanitize(string message)
    {
        lock (Sync)
        {
            foreach (var secrets in Listeners.Values)
                foreach (string secret in secrets) message = message.Replace(secret, "[redacted]", StringComparison.Ordinal);
            return OnlineRoomDiagnostics.SanitizeNativeMessage(message);
        }
    }

    internal static string CopyMessage(IntPtr pointer)
    {
        if (pointer == IntPtr.Zero) return "";
        int length = 0;
        while (length < 4096 && Marshal.ReadByte(pointer, length) != 0) length++;
        byte[] bytes = new byte[length]; Marshal.Copy(pointer, bytes, 0, length);
        return Encoding.UTF8.GetString(bytes);
    }

    private static void OnLog(int level, IntPtr pointer)
    {
        // No I/O or callbacks into the emulator; never unwind a managed exception into native code.
        try
        {
            string message = CopyMessage(pointer);
            lock (Sync)
            {
                foreach (var secrets in Listeners.Values)
                    foreach (string secret in secrets) message = message.Replace(secret, "[redacted]", StringComparison.Ordinal);
                foreach (var diagnostic in Listeners.Keys) diagnostic.Native(level, message);
            }
        }
        catch (Exception) { /* Logging must never break the transport. */ }
    }
}
