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
        return AetherBoy.Runtime.Video.NativeScreenshot.Write(directory, geometry, pixels);
    }
}
