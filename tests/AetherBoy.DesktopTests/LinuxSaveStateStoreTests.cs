namespace AetherBoy.Desktop.Tests;

[TestClass]
public sealed class LinuxSaveStateStoreTests
{
    [TestMethod]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(5)]
    public void WritesAndReadsEverySupportedSlotAtomically(int slot)
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string romPath = Path.Combine(directory, "game.gbc");
            byte[] expected = [0x41, 0x45, 0x54, 0x48, (byte)slot];

            LinuxSaveStateStore.WriteAtomic(romPath, slot, expected);
            byte[] actual = LinuxSaveStateStore.Read(romPath, slot);

            CollectionAssert.AreEqual(expected, actual);
            Assert.IsFalse(File.Exists(LinuxSaveStateStore.GetPath(romPath, slot) + ".tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void OverwritesExistingSlotWithoutLeavingTemporaryFile()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string romPath = Path.Combine(directory, "game.gba");
            LinuxSaveStateStore.WriteAtomic(romPath, 2, new byte[] { 1, 2, 3 });
            LinuxSaveStateStore.WriteAtomic(romPath, 2, new byte[] { 4, 5 });

            CollectionAssert.AreEqual(new byte[] { 4, 5 }, LinuxSaveStateStore.Read(romPath, 2));
            Assert.IsFalse(File.Exists(LinuxSaveStateStore.GetPath(romPath, 2) + ".tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void UsesWindowsCompatibleStateExtension()
    {
        string path = LinuxSaveStateStore.GetPath(Path.Combine("folder", "game.gb"), 4);

        Assert.AreEqual(".ss4", Path.GetExtension(path));
    }

    [TestMethod]
    public void RejectsEmptyStateDocument()
    {
        string romPath = Path.Combine(CreateTemporaryDirectory(), "game.gb");
        try
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
                LinuxSaveStateStore.WriteAtomic(romPath, 1, ReadOnlySpan<byte>.Empty));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(romPath)!, recursive: true);
        }
    }

    [TestMethod]
    public void RejectsEmptyStateFile()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string romPath = Path.Combine(directory, "game.gb");
            File.WriteAllBytes(LinuxSaveStateStore.GetPath(romPath, 1), []);

            Assert.ThrowsExactly<InvalidDataException>(() => LinuxSaveStateStore.Read(romPath, 1));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"aetherboy-linux-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
