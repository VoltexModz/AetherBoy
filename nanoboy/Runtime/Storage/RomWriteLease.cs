using System;
using System.IO;

namespace AetherBoy.Runtime.Storage;

/// <summary>Lifetime ownership shared by both frontends. Never delete a live lock file/inode.</summary>
public sealed class RomWriteLease : IDisposable
{
    private readonly FileStream stream;
    private RomWriteLease(FileStream stream) => this.stream = stream;

    public static RomWriteLease Acquire(string lockPath)
    {
        string path = Path.GetFullPath(lockPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            return new(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
        }
        catch (IOException exception)
        {
            throw new IOException("This cartridge's saves are in use or cannot be locked. " +
                "Close any other AetherBoy window using this game and check the storage permissions.", exception);
        }
    }

    public void Dispose() => stream.Dispose();
}
