using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace AetherBoy.Runtime;

/// <summary>Opt-in app-local storage. Never migrates or deletes the normal user profile.</summary>
public static class PortableStorage
{
    public const string Argument = "--portable";
    public const string MarkerName = "aetherboy.portable";
    private static int requested;

    public static bool IsEnabled => Volatile.Read(ref requested) != 0;

    public static string Root => Path.Combine(AppContext.BaseDirectory, "AetherBoyData");

    public static string[] Configure(string[] args)
    {
        Volatile.Write(ref requested, IsRequested(args, AppContext.BaseDirectory) ? 1 : 0);
        return args.Where(arg => !string.Equals(arg, Argument, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    public static void EnsureWritable()
    {
        if (!IsEnabled) return;
        EnsureWritable(Root);
    }

    internal static void EnsureWritable(string root)
    {
        Directory.CreateDirectory(root);
        string probe = Path.Combine(root, ".write-check-" + Guid.NewGuid().ToString("N"));
        using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            1, FileOptions.DeleteOnClose | FileOptions.WriteThrough);
        stream.WriteByte(0);
        stream.Flush(true);
    }

    public static bool IsRequested(string[] args, string executableDirectory) =>
        args.Any(arg => string.Equals(arg, Argument, StringComparison.OrdinalIgnoreCase)) ||
        File.Exists(Path.Combine(executableDirectory, MarkerName));
}
