using System.Runtime.InteropServices;
using System.Security.Cryptography;
using nanoboy.Core;

namespace AetherBoy.Desktop;

internal sealed record LinuxStateCard(int Slot, bool Exists, DateTime SavedAt, string? Error, int Width = 0, int Height = 0, int[]? Pixels = null);
internal sealed record LinuxDiskSnapshot(string BasePath, IReadOnlyList<LinuxStateCard> States, IReadOnlyList<BatterySaveFile> Backups, string? Error);

internal static class LinuxStateGallery
{
    public static string PathFor(string basePath, int slot) => slot == 0 ? Path.ChangeExtension(basePath, "resume") : LinuxSaveStateStore.GetPath(basePath, slot);
    public static void Write(string basePath, int slot, byte[] state, int width, int height, int[] pixels)
    {
        string file = PathFor(basePath, slot);
        LinuxSaveStateStore.WritePathAtomic(file, state);
        // A preview is useful only if bound to the exact state. Stale sidecars never masquerade as current.
        string temporary = file + ".preview." + Guid.NewGuid().ToString("N");
        try
        {
            if (pixels.Length != width * height || width is < 1 or > 240 || height is < 1 or > 160) return;
            using (var output = new BinaryWriter(File.Create(temporary)))
            { output.Write(0x41455031); output.Write(SHA256.HashData(state)); output.Write(width); output.Write(height); output.Write(MemoryMarshal.AsBytes(pixels.AsSpan())); }
            File.Move(temporary, file + ".preview", true);
        }
        catch (IOException) { /* The state is committed; a missing optional preview is shown honestly. */ }
        catch (UnauthorizedAccessException) { }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static LinuxDiskSnapshot Inspect(string basePath, string? savePath, int batteryLength)
    {
        var cards = Enumerable.Range(0, 6).Select(slot => ReadCard(basePath, slot)).ToArray();
        try
        {
            return new(basePath, cards, savePath is not null && batteryLength > 0 ? BatterySaveStore.Inspect(savePath, batteryLength) : [], null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        { return new(basePath, cards, [], "Backups could not be inspected: " + ex.Message); }
    }
    private static LinuxStateCard ReadCard(string basePath, int slot)
    {
        string file = PathFor(basePath, slot);
        try
        {
            // GetAttributes propagates inaccessible-directory failures; File.Exists would disguise them as empty.
            File.GetAttributes(file);
            byte[] state = LinuxSaveStateStore.ReadPath(file);
            DateTime stamp = File.GetLastWriteTime(file);
            try
            {
                using var reader = new BinaryReader(File.OpenRead(file + ".preview"));
                if (reader.BaseStream.Length > 44 + 240 * 160 * 4 || reader.ReadInt32() != 0x41455031
                    || !reader.ReadBytes(32).AsSpan().SequenceEqual(SHA256.HashData(state))) return new(slot, true, stamp, null);
                int width = reader.ReadInt32(), height = reader.ReadInt32();
                if (width is < 1 or > 240 || height is < 1 or > 160 || reader.BaseStream.Length != 44 + width * height * 4) return new(slot, true, stamp, null);
                int[] pixels = MemoryMarshal.Cast<byte, int>(reader.ReadBytes(width * height * 4)).ToArray();
                return new(slot, true, stamp, null, width, height, pixels);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new(slot, true, stamp, null); }
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return new(slot, false, default, null); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return new(slot, true, default, ex.Message); }
    }
}
