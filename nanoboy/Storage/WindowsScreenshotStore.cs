using System;
using System.IO;
using AetherBoy.Runtime;

namespace nanoboy.Storage;

internal sealed class WindowsScreenshotStore(WindowsDataPaths paths)
{
    internal string Write(string rom, VideoGeometry geometry, int[] pixels)
    {
        if (pixels.Length != geometry.PixelCount) throw new ArgumentException("Incomplete video frame.", nameof(pixels));
        string id = new WindowsRomLibrary(paths).GetIdentity(rom);
        string directory = Path.Combine(paths.Screenshots, id);
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.png");
        string temporary = destination + ".tmp";
        try
        {
            byte[] png = Convert.FromBase64String(WindowsSaveStateStore.EncodeFrame(pixels, geometry));
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(png); stream.Flush(true); }
            File.Move(temporary, destination); // Unique name; never overwrite another screenshot.
            return destination;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
