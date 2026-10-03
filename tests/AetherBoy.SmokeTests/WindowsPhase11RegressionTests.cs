using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using nanoboy;
using nanoboy.Input;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsPhase11RegressionTests
{
    private string root = null!;
    private WindowsDataPaths paths = null!;
    private string rom = null!;
    [TestInitialize] public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "aetherboy-phase11-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        paths = new WindowsDataPaths(Path.Combine(root, "before"));
        string source = Path.Combine(root, "test.gb"); File.WriteAllBytes(source, new byte[32768]);
        rom = new WindowsRomLibrary(paths).Import(source);
    }
    [TestCleanup] public void Cleanup() => Directory.Delete(root, true);

    [TestMethod]
    public void XInputUsesPointerAndExactNativeStructureLayout()
    {
        var method = typeof(XInputGamepad).GetMethod("XInputSetState", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.IsTrue(method.GetParameters()[1].ParameterType.IsByRef);
        Assert.AreEqual(typeof(NativeVibration), method.GetParameters()[1].ParameterType.GetElementType());
        Assert.AreEqual(4, Marshal.SizeOf<NativeVibration>());
        Assert.AreEqual(new IntPtr(2), Marshal.OffsetOf<NativeVibration>(nameof(NativeVibration.RightMotorSpeed)));
        Assert.IsFalse(XInputGamepad.SetVibration(4, 0), "Invalid slots never call native code.");
    }

    private static HostGamepadState Pad(string id) => new(HostGamepadButtons.None,
        source: GamepadInputSource.XInput, deviceId: id);

    [TestMethod]
    public void RumbleStopsOnSuppressionDisconnectDeviceSwitchWatchdogAndDispose()
    {
        long now = 0;
        var writes = new List<(string, ushort)>();
        var rumble = new GamepadRumble((id, _, value) => { writes.Add((id, value)); return true; }, () => now, false);
        rumble.Update(Pad("xinput:0"), true); Assert.IsTrue(rumble.IsActive);
        rumble.Update(Pad("xinput:0"), false); Assert.AreEqual(("xinput:0", (ushort)0), writes[^1]);
        rumble.Update(Pad("xinput:0"), true);
        rumble.Update(Pad("xinput:1"), true);
        Assert.AreEqual(("xinput:0", (ushort)0), writes[^2]);
        rumble.Update(HostGamepadState.Disconnected, true); Assert.IsFalse(rumble.IsActive);
        Assert.AreEqual(("xinput:1", (ushort)0), writes[^1]);
        rumble.Update(Pad("xinput:1"), true);
        now = 200; rumble.Update(Pad("xinput:1"), true);
        now = 250; rumble.CheckWatchdog(); Assert.IsTrue(rumble.IsActive, "An old queued timer must not stop a refreshed pulse.");
        now = 450; rumble.CheckWatchdog(); Assert.IsFalse(rumble.IsActive);
        rumble.Update(Pad("xinput:1"), true); rumble.Dispose();
        Assert.AreEqual(("xinput:1", (ushort)0), writes[^1]);
        int count = writes.Count; rumble.Dispose(); rumble.Update(Pad("xinput:0"), true); Assert.HasCount(count, writes);
    }

    [TestMethod]
    public void FailedRumbleIsNotReportedActiveAndIsRetriedAtBoundedRate()
    {
        long now = 0; int attempts = 0;
        using var rumble = new GamepadRumble((_, _, _) => { attempts++; return false; }, () => now, false);
        rumble.Update(Pad("xinput:0"), true); Assert.IsFalse(rumble.IsActive);
        now = 10; rumble.Update(Pad("xinput:0"), true); Assert.AreEqual(1, attempts);
        now = 100; rumble.Update(Pad("xinput:0"), true); Assert.AreEqual(2, attempts);
    }

    [TestMethod]
    public void RecentManagedReferencesRelocateWithoutDuplicateMissingRowsAndKeepExternalFiles()
    {
        WindowsDataPaths previous = WindowsDataPaths.Default;
        try
        {
            WindowsDataPaths.Default = paths;
            string external = Path.Combine(root, "test.gb");
            RecentRomStore.Save([rom, external]);
            Assert.IsTrue(File.ReadAllLines(paths.RecentRoms)[0].StartsWith("managed:"));
            var moved = new WindowsDataPaths(Path.Combine(root, "after"));
            Directory.Move(paths.Root, moved.Root); WindowsDataPaths.Default = moved;
            string relocated = new WindowsRomLibrary(moved).GetRoms().Single();
            CollectionAssert.AreEqual(new[] { relocated, external }, RecentRomStore.Load().ToArray());
            string id = new WindowsRomLibrary(moved).GetIdentity(relocated);
            File.WriteAllLines(moved.RecentRoms, [rom, $"Q:\\old\\Roms\\{id}\\test.gb", "managed:" + id, external, "..\\test.gb", "managed:../../test"]);
            CollectionAssert.AreEqual(new[] { relocated, external }, RecentRomStore.Load().ToArray());
            Directory.CreateDirectory(Path.GetDirectoryName(rom)!); File.Copy(relocated, rom);
            CollectionAssert.AreEqual(new[] { relocated, external }, RecentRomStore.Load().ToArray(),
                "A portable copy must use its own managed ROM even while the old installation exists.");
            File.WriteAllBytes(relocated, [1]);
            File.WriteAllLines(moved.RecentRoms, ["managed:" + id]);
            Assert.IsEmpty(RecentRomStore.Load(), "A changed ROM must not be accepted as the original content.");
        }
        finally { WindowsDataPaths.Default = previous; }
    }

    [STATestMethod]
    public void WindowsPlaytimeRetriesAfterLibraryFailureEvenWithoutAnActiveSession()
    {
        string source = Path.Combine(root, "activity.gb"); byte[] bytes = new byte[32768]; Guid.NewGuid().ToByteArray().CopyTo(bytes, 0x200);
        File.WriteAllBytes(source, bytes);
        string imported = WindowsRomLibrary.Default.Import(source);
        WindowsGameLibraryStore.Default.Update(imported, entry => entry with { PlayedSeconds = 10 });
        string file = Path.Combine(WindowsDataPaths.Default.Root, "Library", WindowsRomLibrary.Default.GetIdentity(imported) + ".json");
        string valid = File.ReadAllText(file); File.WriteAllText(file, "{");
        using var window = new frmNano();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var journal = (AetherBoy.Runtime.PlaytimeJournal)typeof(frmNano).GetField("activityJournal", flags)!.GetValue(window)!;
        void Flush() => typeof(frmNano).GetMethod("FlushGameActivity", flags)!.Invoke(window, null);
        journal.Add(imported, 7); Flush(); Assert.AreEqual("{", File.ReadAllText(file));
        File.WriteAllText(file, valid); journal.Add(imported, 2); Flush(); Flush();
        Assert.AreEqual(19d, WindowsGameLibraryStore.Default.Read(imported).PlayedSeconds);
    }

    [TestMethod]
    public void SemanticallyDamagedMetadataRecoversBackupAndNeverPoisonsIt()
    {
        var store = new WindowsGameLibraryStore(paths);
        store.Update(rom, item => item with { Favorite = true, PlayedSeconds = 12 });
        store.Update(rom, item => item with { PlayedSeconds = 30 });
        string file = Path.Combine(paths.Root, "Library", new WindowsRomLibrary(paths).GetIdentity(rom) + ".json");
        File.WriteAllText(file, "{\"Title\":null,\"System\":\"GB\"}");
        var recovered = store.Read(rom, out bool usedBackup);
        Assert.IsTrue(usedBackup); Assert.IsTrue(recovered.Favorite); Assert.AreEqual(12d, recovered.PlayedSeconds);
        store.Update(rom, item => item with { PlayedSeconds = 15 });
        File.WriteAllText(file, "{");
        Assert.AreEqual(12d, store.Read(rom).PlayedSeconds);
        File.WriteAllText(file + ".bak", "{\"Title\":null,\"System\":\"GB\"}");
        Assert.ThrowsExactly<InvalidDataException>(() => store.Read(rom));
    }

    [STATestMethod]
    public void VaultSearchSurvivesNullTitleWithoutChangingDamagedMetadata()
    {
        string source = Path.Combine(root, "unique.gb"); byte[] bytes = new byte[32768]; Guid.NewGuid().ToByteArray().CopyTo(bytes, 0x200);
        File.WriteAllBytes(source, bytes);
        string imported = WindowsRomLibrary.Default.Import(source);
        string id = WindowsRomLibrary.Default.GetIdentity(imported);
        string file = Path.Combine(WindowsDataPaths.Default.Root, "Library", id + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        const string damaged = "{\"Title\":null,\"System\":\"GB\"}";
        File.WriteAllText(file, damaged);
        using var vault = new frmRomLibrary([imported]);
        ((nanoboy.Controls.AetherTextBox)vault.Controls.Find("romLibrarySearch", true).Single()).Text = "unique";
        var notice = (Label)vault.Controls.Find("romLibraryMetadataNotice", true).Single();
        Assert.IsFalse(string.IsNullOrWhiteSpace(notice.Text));
        Assert.IsTrue(TextRenderer.MeasureText(notice.Text, notice.Font).Width < notice.Width);
        Assert.AreEqual(damaged, File.ReadAllText(file));
        Assert.IsTrue(vault.Controls.Find("romLibraryList", true).OfType<nanoboy.Controls.AetherList>().Single().Items.Count > 0);
    }
}
