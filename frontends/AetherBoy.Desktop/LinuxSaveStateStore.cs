using nanoboy.Core;

namespace AetherBoy.Desktop;

internal static class LinuxSaveStateStore
{
    public static string GetPath(string romPath, int slot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(romPath);
        if (slot is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "Save slot must be between 1 and 5.");
        }

        return Path.ChangeExtension(Path.GetFullPath(romPath), $"ss{slot}");
    }

    public static byte[] Read(string romPath, int slot)
    {
        string path = GetPath(romPath, slot);
        using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.SequentialScan);
        if (stream.Length <= 0 || stream.Length > EmulatorStateCodec.MaximumDocumentLength)
        {
            throw new InvalidDataException(
                $"State data must contain between 1 and {EmulatorStateCodec.MaximumDocumentLength} bytes.");
        }

        byte[] state = new byte[(int)stream.Length];
        stream.ReadExactly(state);
        return state;
    }

    public static void WriteAtomic(string romPath, int slot, ReadOnlySpan<byte> state)
    {
        if (state.IsEmpty || state.Length > EmulatorStateCodec.MaximumDocumentLength)
        {
            throw new ArgumentException(
                $"State data must contain between 1 and {EmulatorStateCodec.MaximumDocumentLength} bytes.",
                nameof(state));
        }

        string path = GetPath(romPath, slot);
        string temporaryPath = path + ".tmp";
        try
        {
            using (FileStream stream = new(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.WriteThrough))
            {
                stream.Write(state);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
