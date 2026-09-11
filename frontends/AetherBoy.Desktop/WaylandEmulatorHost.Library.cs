using System.Runtime.InteropServices;
using SDL3;

namespace AetherBoy.Desktop;

internal sealed partial class WaylandEmulatorHost
{
    private readonly LinuxLibrary library;
    private IReadOnlyList<LinuxLibraryEntry> libraryEntries = [];
    private string librarySearch = "";
    private bool editingSearch;
    private int libraryPage;
    private bool pickingFirmware;

    private void RefreshLibrary()
    {
        libraryEntries = library.Read();
        libraryPage = 0;
    }

    private void DrawLibraryPage()
    {
        ActionButton(300, 198, 520, 44, editingSearch ? "Type to search: " + librarySearch :
            string.IsNullOrEmpty(librarySearch) ? "SEARCH CARTRIDGES" : "Search: " + librarySearch,
            () => { editingSearch = true; SDL.StartTextInput(window); }, editingSearch);
        ActionButton(840, 198, 160, 44, "CLEAR", () => { librarySearch = ""; libraryPage = 0; });
        var matches = libraryEntries.Where(entry => entry.Title.Contains(librarySearch, StringComparison.OrdinalIgnoreCase)).ToArray();
        libraryPage = Math.Clamp(libraryPage, 0, Math.Max(0, (matches.Length - 1) / 4));
        if (matches.Length == 0)
        {
            Ink(300, 293, libraryEntries.Count == 0 ? "Your cartridges will appear here after you open them." : "No matching cartridges.", 16);
            Ink(300, 330, "Files stay where you keep them. Saves follow the ROM content.", 14, Colors.Muted);
        }
        int row = 0;
        foreach (var entry in matches.Skip(libraryPage * 4).Take(4))
        {
            float y = 264 + row++ * 64;
            bool exists = File.Exists(entry.Path);
            ActionButton(300, y, 540, 42, entry.Title, () => TryLoadRom(entry.Path), enabled: exists);
            Ink(855, y + 10, exists ? entry.LastPlayed.ToLocalTime().ToString("dd MMM yyyy") : "FILE MISSING", 14, exists ? Colors.Muted : Colors.Danger);
        }
        ActionButton(300, 548, 160, 44, "PREVIOUS", () => libraryPage--, enabled: libraryPage > 0);
        ActionButton(476, 548, 160, 44, "NEXT", () => libraryPage++, enabled: (libraryPage + 1) * 4 < matches.Length);
        ActionButton(660, 548, 200, 44, "OPEN / RELOCATE", ShowRomDialog, true);
        Ink(300, 598, "Missing file? Open its new location; matching content keeps its saves.", 14, Colors.Muted);
    }

    private void ShowFirmwareDialog()
    {
        if (fileDialogOpen != 0 || pendingSession is not null) return;
        pickingFirmware = true;
        ShowRomDialog();
        statusMessage = "Choose a DMG boot ROM (256 B), CGB boot ROM (2304 B), or GBA BIOS (16 KiB).";
    }

    private void ImportFirmware(string path)
    {
        long length = new FileInfo(path).Length;
        string name = length switch { 256 => "dmg_boot.bin", 2304 => "cgb_boot.bin", 16384 => "gba_bios.bin",
            _ => throw new InvalidDataException("Firmware must be 256, 2304 or 16384 bytes. No file was imported.") };
        string directory = Path.Combine(dataPaths.Data, "firmware");
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, name);
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.Copy(path, temporary); File.Move(temporary, destination, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        options.UseFirmware = true;
        MarkSettingsChanged();
        statusMessage = name + " imported. It will be used the next time you open a cartridge.";
    }

    private byte[]? FirmwareFor(string path)
    {
        if (!options.UseFirmware) return null;
        string name;
        if (Path.GetExtension(path).Equals(".gba", StringComparison.OrdinalIgnoreCase)) name = "gba_bios.bin";
        else
        {
            using var input = File.OpenRead(path);
            input.Position = 0x143;
            int color = input.ReadByte();
            name = (color & 0x80) != 0 ? "cgb_boot.bin" : "dmg_boot.bin";
        }
        string firmware = Path.Combine(dataPaths.Data, "firmware", name);
        if (!File.Exists(firmware)) return null;
        long expected = name == "gba_bios.bin" ? 16384 : name == "cgb_boot.bin" ? 2304 : 256;
        if (new FileInfo(firmware).Length != expected) throw new InvalidDataException("The imported firmware has changed size. Import it again.");
        return File.ReadAllBytes(firmware);
    }
}
