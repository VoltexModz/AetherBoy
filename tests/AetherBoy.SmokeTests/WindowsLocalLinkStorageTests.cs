using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class WindowsLocalLinkStorageTests
{
    [TestMethod]
    public void SameCartridgeUsesSeparatePersistentPlayerTwoSaveWithoutSeedingIt()
    {
        using var fixture = new Fixture();
        string source = fixture.Rom("first.gb");
        string managed = fixture.Library.Import(source);
        string primary = fixture.Library.GetSavePath(managed);
        File.WriteAllBytes(primary, [1, 2, 3, 4]);

        WindowsLocalLinkPlan plan = fixture.Storage.CreatePlan(source, source);

        Assert.IsTrue(plan.SameRom);
        Assert.AreEqual(managed, plan.FirstRomPath);
        Assert.AreEqual(managed, plan.SecondRomPath);
        Assert.AreEqual(primary, plan.FirstSavePath);
        Assert.AreEqual(Path.Combine(Path.GetDirectoryName(primary)!, "LinkPlayer2", "game.sav"),
            plan.SecondSavePath);
        Assert.AreNotEqual(plan.FirstSavePath, plan.SecondSavePath);
        Assert.IsFalse(File.Exists(plan.SecondSavePath));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(primary));

        Directory.CreateDirectory(Path.GetDirectoryName(plan.SecondSavePath)!);
        File.WriteAllBytes(plan.SecondSavePath, [9, 8, 7]);
        WindowsLocalLinkPlan reopened = fixture.Storage.CreatePlan(managed, managed);
        Assert.AreEqual(plan, reopened);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(primary));
        CollectionAssert.AreEqual(new byte[] { 9, 8, 7 }, File.ReadAllBytes(reopened.SecondSavePath));
    }

    [TestMethod]
    public void IdenticalContentWithDifferentNamesAndExtensionsStillNeedsPlayerTwoSave()
    {
        using var fixture = new Fixture();
        string first = fixture.Rom("one.gb", color: true);
        string second = Path.Combine(fixture.Input, "renamed.GBC");
        File.Copy(first, second);

        WindowsLocalLinkPlan plan = fixture.Storage.CreatePlan(first, second);

        Assert.IsTrue(plan.SameRom);
        Assert.AreEqual(plan.FirstRomPath, plan.SecondRomPath);
        Assert.AreNotEqual(plan.FirstSavePath, plan.SecondSavePath);
        Assert.AreEqual(1, fixture.Library.GetRoms().Count);
    }

    [TestMethod]
    public void DifferentCartridgesUseTheirOwnPrimarySavesAndKeepExistingStates()
    {
        using var fixture = new Fixture();
        string first = fixture.Library.Import(fixture.Rom("one.gb", marker: 1));
        string second = fixture.Library.Import(fixture.Rom("two.gbc", marker: 2, color: true));
        string firstSave = fixture.Library.GetSavePath(first);
        string secondSave = fixture.Library.GetSavePath(second);
        string firstState = fixture.Library.GetStatePath(first, 1);
        File.WriteAllText(firstSave, "first progress");
        File.WriteAllText(secondSave, "second progress");
        File.WriteAllText(firstState, "existing state");

        WindowsLocalLinkPlan plan = fixture.Storage.CreatePlan(first, second);

        Assert.IsFalse(plan.SameRom);
        Assert.AreEqual(firstSave, plan.FirstSavePath);
        Assert.AreEqual(secondSave, plan.SecondSavePath);
        Assert.AreNotEqual(plan.FirstSavePath, plan.SecondSavePath);
        Assert.AreEqual("first progress", File.ReadAllText(firstSave));
        Assert.AreEqual("second progress", File.ReadAllText(secondSave));
        Assert.AreEqual("existing state", File.ReadAllText(firstState));
    }

    [TestMethod]
    public void LinkImportCopiesRomsButNeverExternalBatteryRtcOrStateFiles()
    {
        using var fixture = new Fixture();
        string first = fixture.Rom("one.gb");
        string second = fixture.Rom("two.gbc", marker: 2, color: true);
        foreach (string rom in new[] { first, second })
        {
            File.WriteAllText(Path.ChangeExtension(rom, "sav"), "external progress");
            File.WriteAllText(Path.ChangeExtension(rom, "sav") + ".rtc", "external clock");
            File.WriteAllText(Path.ChangeExtension(rom, "sav") + ".bak1", "external backup");
            File.WriteAllText(Path.ChangeExtension(rom, "ss1"), "external state");
        }

        WindowsLocalLinkPlan plan = fixture.Storage.CreatePlan(first, second);

        CollectionAssert.AreEqual(File.ReadAllBytes(first), File.ReadAllBytes(plan.FirstRomPath));
        CollectionAssert.AreEqual(File.ReadAllBytes(second), File.ReadAllBytes(plan.SecondRomPath));
        Assert.AreEqual(0, Directory.GetFiles(fixture.Paths.Saves, "*", SearchOption.AllDirectories).Length);
        Assert.AreEqual(0, Directory.GetFiles(fixture.Paths.States, "*", SearchOption.AllDirectories).Length);
        Assert.AreEqual("external progress", File.ReadAllText(Path.ChangeExtension(first, "sav")));
        Assert.AreEqual("external state", File.ReadAllText(Path.ChangeExtension(second, "ss1")));
    }

    [TestMethod]
    [DataRow(".gba")]
    [DataRow(".zip")]
    [DataRow(".txt")]
    public void UnsupportedSecondCartridgeDoesNotImportFirstOrCreateDataFolders(string extension)
    {
        using var fixture = new Fixture();
        string first = fixture.Rom("valid.gb");
        string second = fixture.Rom("unsupported" + extension);

        Assert.ThrowsExactly<InvalidDataException>(() => fixture.Storage.CreatePlan(first, second));

        Assert.IsFalse(Directory.Exists(fixture.Paths.Root));
        Assert.AreEqual(0, fixture.Library.GetRoms().Count);
    }

    [TestMethod]
    [DataRow(0x148, 0x09)] // Unsupported ROM size code.
    [DataRow(0x148, 0x01)] // Header declares more bytes than are present.
    [DataRow(0x149, 0x06)] // Unsupported RAM size code.
    public void InvalidSecondHeaderIsRejectedBeforeAnyImport(int offset, int value)
    {
        using var fixture = new Fixture();
        string first = fixture.Rom("valid.gb");
        string second = fixture.Rom("invalid.gbc", color: true);
        byte[] bytes = File.ReadAllBytes(second);
        bytes[offset] = (byte)value;
        File.WriteAllBytes(second, bytes);

        Assert.ThrowsExactly<InvalidDataException>(() => fixture.Storage.CreatePlan(first, second));

        Assert.IsFalse(Directory.Exists(fixture.Paths.Root));
    }

    [TestMethod]
    public void UnsupportedSecondMapperIsRejectedBeforeAnyImport()
    {
        using var fixture = new Fixture();
        string first = fixture.Rom("valid.gb");
        string second = fixture.Rom("camera.gb");
        byte[] bytes = File.ReadAllBytes(second);
        bytes[0x147] = 0xFC;
        File.WriteAllBytes(second, bytes);

        Assert.ThrowsExactly<NotSupportedException>(() => fixture.Storage.CreatePlan(first, second));

        Assert.IsFalse(Directory.Exists(fixture.Paths.Root));
    }

    [TestMethod]
    public void TruncatedSecondHeaderIsRejectedBeforeAnyImport()
    {
        using var fixture = new Fixture();
        string first = fixture.Rom("valid.gb");
        string second = fixture.Rom("truncated.gb");
        File.WriteAllBytes(second, new byte[0x100]);

        Assert.ThrowsExactly<InvalidDataException>(() => fixture.Storage.CreatePlan(first, second));

        Assert.IsFalse(Directory.Exists(fixture.Paths.Root));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "aetherboy-link-storage-" + Guid.NewGuid().ToString("N"));
        internal WindowsDataPaths Paths { get; }
        internal WindowsRomLibrary Library { get; }
        internal WindowsLocalLinkStorage Storage { get; }
        internal string Input => Path.Combine(root, "input");

        internal Fixture()
        {
            Paths = new WindowsDataPaths(Path.Combine(root, "data"));
            Library = new WindowsRomLibrary(Paths);
            Storage = new WindowsLocalLinkStorage(Paths);
            Directory.CreateDirectory(Input);
        }

        internal string Rom(string name, byte marker = 1, bool color = false)
        {
            byte[] bytes = new byte[0x8000];
            bytes[0x100] = 0x18; // Harmless synthetic infinite loop.
            bytes[0x101] = 0xFE;
            bytes[0x143] = color ? (byte)0x80 : (byte)0;
            bytes[0x147] = 0x09; // ROM + RAM + battery probes persistence isolation.
            bytes[0x149] = 0x02;
            bytes[0x200] = marker;
            string path = Path.Combine(Input, name);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
