using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using nanoboy.Controls;

namespace nanoboy.Storage;

internal sealed class WindowsDataPaths
{
    internal static WindowsDataPaths Default { get; set; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductInfo.Name));

    internal WindowsDataPaths(string root) => Root = Path.GetFullPath(root);

    internal string Root { get; }
    internal string Roms => Path.Combine(Root, "Roms");
    internal string Saves => Path.Combine(Root, "Saves");
    internal string States => Path.Combine(Root, "States");
    internal string Settings => Path.Combine(Root, "Settings");
    internal string Firmware => Path.Combine(Root, "Firmware");
    internal string Recordings => Path.Combine(Root, "Recordings");
    internal string Screenshots => Path.Combine(Root, "Screenshots");
    internal string Development => Path.Combine(Root, "development");
    internal string Sessions => Path.Combine(Development, "Sessions");
    internal string CrashLogs => Path.Combine(ProductInfo.IsDevelopmentBuild ? Development : Root, "Crashes");
    internal string SettingsFile => Path.Combine(Settings, "settings.json");
    internal string RecentRoms => Path.Combine(Settings, "recent-roms.txt");
    internal string LegacySettingsRoot => Path.Combine(Path.GetDirectoryName(Root)!, "AetherBoy_contributors");

    internal static void OpenFolder(IWin32Window owner, string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception)
        {
            AetherSignal.Show(owner, $"Der Ordner konnte nicht geöffnet werden.\n\n{exception.Message}",
                "Ordner nicht verfügbar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
