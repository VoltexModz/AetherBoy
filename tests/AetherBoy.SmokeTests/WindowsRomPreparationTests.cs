using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class WindowsRomPreparationTests
{
    [TestMethod]
    public void CancelledImportDoesNotCreateManagedRomOrChangeSource()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-rom-prep-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var paths = new WindowsDataPaths(Path.Combine(root, "data"));
            string source = Path.Combine(root, "generated.gb");
            byte[] bytes = new byte[0x8000];
            bytes[0x100] = 0x04;
            File.WriteAllBytes(source, bytes);
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();

            Assert.ThrowsExactly<OperationCanceledException>(() =>
                new WindowsRomLibrary(paths).Import(source, cancelled.Token));
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(source));
            Assert.IsFalse(Directory.Exists(paths.Roms));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void PreparedImportKeepsContentIdentityAndSaveLocation()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-rom-prep-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var paths = new WindowsDataPaths(Path.Combine(root, "data"));
            var library = new WindowsRomLibrary(paths);
            string source = Path.Combine(root, "generated.gbc");
            byte[] bytes = new byte[0x8000];
            bytes[0x100] = 0x04;
            File.WriteAllBytes(source, bytes);

            string first = library.Import(source);
            string second = library.Import(source, CancellationToken.None);
            frmNano.ValidatePreparedRom(source, second, CancellationToken.None);
            Assert.ThrowsExactly<InvalidDataException>(() =>
                frmNano.ValidatePreparedRom(Path.ChangeExtension(source, ".gba"), second, CancellationToken.None));
            Assert.AreEqual(first, second);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(second));
            Assert.AreEqual(library.GetSavePath(first), library.GetSavePath(second));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
