using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using AetherBoy.Runtime;
using nanoboy.Core;

namespace nanoboy.Storage;

internal sealed record StatePreview(string Title, DateTimeOffset SavedUtc, string StateSha256,
    string? Png, long Frame);
internal sealed record SavedCheckpoint(byte[] State, StatePreview Preview);
internal sealed record StateSlotInfo(int Slot, string Path, bool Exists, DateTimeOffset? WrittenUtc,
    StatePreview? Preview, string? Warning);

internal sealed class WindowsSaveStateStore
{
    // Raw .ss1-.ss5 remain readable by the existing core and by previous frontends.
    internal const int ResumeSlot = 0;
    internal static WindowsSaveStateStore Default { get; } = new(WindowsDataPaths.Default);
    private readonly WindowsDataPaths paths;
    private readonly WindowsRomLibrary library;
    internal WindowsSaveStateStore(WindowsDataPaths paths) { this.paths = paths; library = new(paths); }
    internal string PathFor(string rom, int slot) => slot == ResumeSlot
        ? Path.Combine(paths.States, library.GetIdentity(rom), "game.resume")
        : library.GetStatePath(rom, slot);

    internal void Write(string rom, int slot, SavedCheckpoint checkpoint)
    {
        if (checkpoint.State.Length is 0 or > EmulatorStateCodec.MaximumDocumentLength)
            throw new InvalidDataException("Ungültige Save-State-Größe.");
        string path = PathFor(rom, slot);
        StatePreview preview = checkpoint.Preview with { StateSha256 = Convert.ToHexString(SHA256.HashData(checkpoint.State)) };
        // Publish the optional metadata first. A crash between files leaves the old raw state intact;
        // readers only display a preview whose checksum matches the actual state bytes.
        LocalJson.Write(path + ".preview.json", preview);
        LocalJson.WriteBytes(path, checkpoint.State);
    }

    internal byte[] Read(string rom, int slot)
    {
        using var stream = File.OpenRead(PathFor(rom, slot));
        if (stream.Length is 0 or > EmulatorStateCodec.MaximumDocumentLength)
            throw new InvalidDataException("Ungültige Save-State-Größe.");
        byte[] result = new byte[(int)stream.Length];
        stream.ReadExactly(result);
        return result;
    }

    internal StateSlotInfo Inspect(string rom, int slot)
    {
        string path = PathFor(rom, slot);
        var file = new FileInfo(path);
        if (!file.Exists) return new(slot, path, false, null, null, null);
        try
        {
            StatePreview? preview = LocalJson.Read<StatePreview>(path + ".preview.json");
            if (preview is null) return new(slot, path, true, file.LastWriteTimeUtc, null, "Älterer State ohne Vorschau");
            byte[] state = Read(rom, slot);
            if (!string.Equals(preview.StateSha256, Convert.ToHexString(SHA256.HashData(state)), StringComparison.Ordinal))
                return new(slot, path, true, file.LastWriteTimeUtc, null, "Vorschau gehört nicht zu dieser Datei");
            return new(slot, path, true, file.LastWriteTimeUtc, preview, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return new(slot, path, true, file.LastWriteTimeUtc, null, "Vorschau/Datei nicht lesbar"); }
    }

    internal static async Task<SavedCheckpoint> CaptureAsync(EmulationSession session, CancellationToken cancellationToken = default)
    {
        bool paused = session.LatestSnapshot.IsPaused;
        await session.SetPausedAsync(true, cancellationToken).ConfigureAwait(false);
        try
        {
            byte[] state = await session.CaptureStateAsync(cancellationToken).ConfigureAwait(false);
            EmulationSnapshot snapshot = session.LatestSnapshot;
            int[] pixels = new int[snapshot.VideoGeometry.PixelCount];
            long sequence = 0;
            string? png = session.TryCopyLatestFrame(pixels, ref sequence)
                ? EncodeFrame(pixels, snapshot.VideoGeometry) : null;
            return new(state, new StatePreview(snapshot.Rom?.Title ?? "Cartridge", DateTimeOffset.UtcNow,
                "", png, snapshot.EmulatedFrameCount));
        }
        finally
        {
            if (!paused && session.State is SessionState.Running or SessionState.Paused)
                await session.SetPausedAsync(false, CancellationToken.None).ConfigureAwait(false);
        }
    }

    internal static string EncodeFrame(int[] pixels, VideoGeometry geometry)
    {
        if (pixels.Length != geometry.PixelCount) throw new ArgumentException("Incomplete video frame.", nameof(pixels));
        using var bitmap = new Bitmap(geometry.Width, geometry.Height, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try { Marshal.Copy(pixels, 0, data.Scan0, pixels.Length); }
        finally { bitmap.UnlockBits(data); }
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return Convert.ToBase64String(stream.ToArray());
    }

    internal static Bitmap? DecodePreview(string? png)
    {
        if (string.IsNullOrWhiteSpace(png) || png.Length > 512 * 1024) return null;
        try
        {
            byte[] bytes = Convert.FromBase64String(png);
            if (bytes.Length < 24 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return null;
            uint width = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4));
            uint height = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4));
            if (width is 0 or > 240 || height is 0 or > 160) return null;
            using var stream = new MemoryStream(bytes);
            using var image = Image.FromStream(stream);
            if (image.Width > 240 || image.Height > 160) return null;
            return new Bitmap(image);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OutOfMemoryException) { return null; }
    }
}
