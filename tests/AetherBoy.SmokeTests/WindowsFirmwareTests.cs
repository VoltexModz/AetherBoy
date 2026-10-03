using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy;
using nanoboy.Controls;
using nanoboy.Input;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class WindowsFirmwareTests
{
    private string root = null!;
    private WindowsFirmwareStore store = null!;
    private string executable = null!;
    private string working = null!;

    [TestInitialize]
    public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "aetherboy-firmware-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        executable = Path.Combine(root, "program");
        working = Path.Combine(root, "working");
        store = new(new WindowsDataPaths(Path.Combine(root, "data")), executable, working);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public void MissingFirmwareDoesNotCreateFoldersOrFiles()
    {
        foreach (WindowsFirmwareKind kind in Enum.GetValues<WindowsFirmwareKind>())
        {
            Assert.IsNull(store.Load(kind));
            WindowsFirmwareStatus status = store.GetStatus(kind);
            Assert.AreEqual(WindowsFirmwareState.Missing, status.State);
            Assert.AreEqual(WindowsFirmwareSource.None, status.Source);
            Assert.IsFalse(status.IsAvailable);
        }
        Assert.AreEqual(0, Directory.GetFileSystemEntries(root).Length);
    }

    [TestMethod]
    [DataRow(0, 256, "dmg_boot.bin")]
    [DataRow(1, 2304, "gbc_boot.bin")]
    [DataRow(2, 16384, "gba_bios.bin")]
    public void ImportUsesCanonicalNameAndValidatedFrozenBytes(int kindValue, int length, string name)
    {
        var kind = (WindowsFirmwareKind)kindValue;
        string source = Synthetic("selected.bin", length, 0x23);
        byte[] expected = File.ReadAllBytes(source);
        WindowsFirmwareImport prepared = store.PrepareImport(source, kind);
        File.WriteAllBytes(source, [0xEE]);
        string imported = store.Import(prepared);
        Assert.AreEqual(name, Path.GetFileName(imported));
        Assert.AreEqual(store.DirectoryPath, Path.GetDirectoryName(imported));
        CollectionAssert.AreEqual(expected, File.ReadAllBytes(imported));
        CollectionAssert.AreEqual(expected, store.Load(kind));
        Assert.AreEqual(WindowsFirmwareSource.Managed, store.GetStatus(kind).Source);
        Assert.IsTrue(store.GetStatus(kind).IsAvailable);
        Assert.AreEqual(1, Directory.GetFiles(store.DirectoryPath).Length);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(255)]
    [DataRow(257)]
    [DataRow(2304)]
    [DataRow(16384)]
    [DataRow(1048576)]
    public void InvalidOrWrongModelSizeDoesNotTouchDestination(int length)
    {
        string source = Synthetic("wrong.bin", length, 0xA0);
        Assert.Throws<InvalidDataException>(() => store.PrepareImport(source, WindowsFirmwareKind.Dmg));
        Assert.IsFalse(Directory.Exists(store.DirectoryPath));
    }

    [TestMethod]
    public void ExistingFirmwareRequiresExplicitReplacementAndFailureLeavesBytesIntact()
    {
        var kind = WindowsFirmwareKind.Dmg;
        string original = Synthetic("old.bin", 256, 0x12);
        byte[] expected = File.ReadAllBytes(original);
        string destination = store.Import(store.PrepareImport(original, kind));
        var replacement = store.PrepareImport(Synthetic("new.bin", 256, 0x34), kind);
        Assert.Throws<IOException>(() => store.Import(replacement));
        CollectionAssert.AreEqual(expected, File.ReadAllBytes(destination));
        using (var locked = new FileStream(destination, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Throws<IOException>(() => store.Import(replacement, replaceExisting: true));
            byte[] stillThere = new byte[256];
            locked.ReadExactly(stillThere);
            CollectionAssert.AreEqual(expected, stillThere);
        }
        Assert.AreEqual(1, Directory.GetFiles(store.DirectoryPath).Length, "Temporary files must be cleaned up.");
        store.Import(replacement, replaceExisting: true);
        CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(root, "new.bin")), File.ReadAllBytes(destination));
    }

    [TestMethod]
    public void LoadPreservesManagedWorkingThenExecutablePrecedence()
    {
        var kind = WindowsFirmwareKind.Cgb;
        Directory.CreateDirectory(executable);
        Directory.CreateDirectory(working);
        string name = WindowsFirmwareStore.FileName(kind);
        File.WriteAllBytes(Path.Combine(executable, name), Enumerable.Repeat((byte)1, 2304).ToArray());
        Assert.AreEqual(WindowsFirmwareSource.Executable, store.GetStatus(kind).Source);
        File.WriteAllBytes(Path.Combine(working, name), Enumerable.Repeat((byte)2, 2304).ToArray());
        Assert.AreEqual(WindowsFirmwareSource.WorkingDirectory, store.GetStatus(kind).Source);
        Assert.AreEqual((byte)2, store.Load(kind)![0]);
        store.Import(store.PrepareImport(Synthetic("managed.bin", 2304, 3), kind));
        Assert.AreEqual(WindowsFirmwareSource.Managed, store.GetStatus(kind).Source);
        Assert.AreEqual((byte)3, store.Load(kind)![0]);
    }

    [TestMethod]
    public void InvalidManagedFirmwareDoesNotSilentlySelectOtherExternalBios()
    {
        var kind = WindowsFirmwareKind.Gba;
        Directory.CreateDirectory(store.DirectoryPath);
        Directory.CreateDirectory(executable);
        File.WriteAllBytes(store.PathFor(kind), [0xAB]);
        File.WriteAllBytes(Path.Combine(executable, WindowsFirmwareStore.FileName(kind)), new byte[16384]);
        var status = store.GetStatus(kind);
        Assert.AreEqual(WindowsFirmwareState.Invalid, status.State);
        Assert.IsFalse(status.IsAvailable);
        Assert.AreEqual(WindowsFirmwareSource.Managed, status.Source);
        Assert.IsFalse(status.Problem!.Contains(root));
        Assert.Throws<InvalidDataException>(() => store.Load(kind));
    }

    [TestMethod]
    public void LockedFirmwareReportsUnreadableWithoutLeakingPathInProblem()
    {
        var kind = WindowsFirmwareKind.Dmg;
        string destination = store.Import(store.PrepareImport(Synthetic("boot.bin", 256, 0), kind));
        using var locked = new FileStream(destination, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var status = store.GetStatus(kind);
        Assert.AreEqual(WindowsFirmwareState.Unreadable, status.State);
        Assert.IsFalse(status.Problem!.Contains(root));
        Assert.Throws<IOException>(() => store.Load(kind));
    }

    [STATestMethod]
    public void FirmwareDialogCancellationPreservesBytesAndPolicyAndInvalidInputNeverAsksToOverwrite()
    {
        using var settings = new NanoboySettings();
        bool previous = settings.BootRomEnable;
        try
        {
            settings.BootRomEnable = false;
            var kind = WindowsFirmwareKind.Dmg;
            string first = Synthetic("first.bin", 256, 0x16);
            string destination = store.Import(store.PrepareImport(first, kind));
            using var form = new frmFirmwareManager(store, settings);
            int confirmations = 0;
            Assert.IsFalse(form.ImportFirmware(Synthetic("second.bin", 256, 0x32), kind,
                () => { confirmations++; return false; }));
            Assert.AreEqual(1, confirmations);
            CollectionAssert.AreEqual(File.ReadAllBytes(first), File.ReadAllBytes(destination));
            Assert.IsFalse(settings.BootRomEnable);
            Assert.Throws<InvalidDataException>(() => form.ImportFirmware(Synthetic("invalid.bin", 4, 0), kind,
                () => { confirmations++; return true; }));
            Assert.AreEqual(1, confirmations);
            CollectionAssert.AreEqual(File.ReadAllBytes(first), File.ReadAllBytes(destination));
            Assert.IsTrue(form.ImportFirmware(Path.Combine(root, "second.bin"), kind, () => true));
            Assert.IsTrue(settings.BootRomEnable);
            Assert.AreEqual((byte)0x32, store.Load(kind)![0]);
        }
        finally { settings.BootRomEnable = previous; }
    }

    [STATestMethod]
    public void FirmwareDialogUsesAetherChromeAndControllerNavigation()
    {
        using var settings = new NanoboySettings();
        bool previous = settings.BootRomEnable;
        try
        {
            using var form = new frmFirmwareManager(store, settings);
            form.Show();
            Application.DoEvents();
            Assert.AreEqual(FormBorderStyle.None, form.FormBorderStyle);
            Assert.AreEqual(1, form.Controls.Find("aetherDialogHeader", true).Length);
            foreach (WindowsFirmwareKind kind in Enum.GetValues<WindowsFirmwareKind>())
            {
                var button = (AetherButton)form.Controls.Find("firmwareImport" + kind, true).Single();
                Assert.IsTrue(button.Visible && button.Enabled && button.TabStop);
            Assert.AreEqual(AetherBoy.Runtime.Localization.UiText.Get("NICHT VORHANDEN · Eingebauter Startpfad"), form.Controls.Find("firmwareStatus" + kind, true).Single().Text);
            }
            var policy = form.Controls.Find("firmwarePolicy", true).Single();
            policy.Focus();
            GamepadNavigation.Navigate(form, PadUiAction.Accept);
            Assert.AreEqual(!previous, settings.BootRomEnable);
            string? directory = Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS");
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
                form.BringToFront(); form.Refresh(); Application.DoEvents();
                using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
                using (Graphics capture = Graphics.FromImage(bitmap))
                    capture.CopyFromScreen(form.PointToScreen(Point.Empty), Point.Empty, form.ClientSize);
                bitmap.Save(Path.Combine(directory, "firmware-station.png"));
            }
            GamepadNavigation.Navigate(form, PadUiAction.Back);
            Assert.IsFalse(form.Visible);
        }
        finally { settings.BootRomEnable = previous; }
    }

    private string Synthetic(string name, int length, byte value)
    {
        string path = Path.Combine(root, name);
        File.WriteAllBytes(path, Enumerable.Repeat(value, length).ToArray());
        return path;
    }
}
