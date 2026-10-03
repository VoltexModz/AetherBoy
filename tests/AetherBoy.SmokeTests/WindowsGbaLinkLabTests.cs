using System.Buffers.Binary;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy;
using nanoboy.Controls;
using nanoboy.Storage;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class WindowsGbaLinkLabTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GbaPlanKeepsBothSavesSeparateAndNeverImportsAdjacentProgress(bool sameRom)
    {
        using var fixture = new Fixture();
        string first = fixture.Gba("first.gba", 0x001F);
        string second = sameRom ? first : fixture.Gba("second.gba", 0x03E0);
        File.WriteAllText(Path.ChangeExtension(first, "sav"), "external save must not migrate");
        File.WriteAllText(Path.ChangeExtension(first, "sav") + ".rtc", "external RTC must not migrate");
        File.WriteAllText(Path.ChangeExtension(first, "ss1"), "external state must not migrate");
        var storage = new WindowsLocalLinkStorage(fixture.Paths);
        var plan = storage.CreatePlan(first, second);
        Assert.IsTrue(plan.IsGameBoyAdvance);
        Assert.AreEqual(sameRom, plan.SameRom);
        Assert.AreNotEqual(plan.FirstSavePath, plan.SecondSavePath);
        Assert.AreEqual(0, Directory.GetFiles(fixture.Paths.Saves, "*", SearchOption.AllDirectories).Length);
        Assert.AreEqual(0, Directory.GetFiles(fixture.Paths.States, "*", SearchOption.AllDirectories).Length);
        Directory.CreateDirectory(Path.GetDirectoryName(plan.SecondSavePath)!);
        File.WriteAllBytes(plan.FirstSavePath, [0x12]);
        File.WriteAllBytes(plan.SecondSavePath, [0x34]);
        var reopened = storage.CreatePlan(plan.FirstRomPath, plan.SecondRomPath);
        Assert.AreEqual(plan.FirstSavePath, reopened.FirstSavePath);
        Assert.AreEqual(plan.SecondSavePath, reopened.SecondSavePath);
        CollectionAssert.AreEqual(new byte[] { 0x12 }, File.ReadAllBytes(plan.FirstSavePath));
        CollectionAssert.AreEqual(new byte[] { 0x34 }, File.ReadAllBytes(plan.SecondSavePath));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MixedHardwareIsRejectedBeforeCreatingAnyManagedData(bool gbaFirst)
    {
        using var fixture = new Fixture();
        string gb = fixture.Gb();
        string gba = fixture.Gba("advance.gba", 0x001F);
        var storage = new WindowsLocalLinkStorage(fixture.Paths);
        Assert.ThrowsExactly<InvalidDataException>(() => storage.CreatePlan(gbaFirst ? gba : gb, gbaFirst ? gb : gba));
        Assert.IsFalse(Directory.Exists(fixture.Paths.Root));
    }

    [TestMethod]
    public void InvalidSecondGbaDoesNotPartiallyImportTheFirst()
    {
        using var fixture = new Fixture();
        string first = fixture.Gba("valid.gba", 0x001F);
        string invalid = fixture.Gba("invalid.gba", 0x03E0);
        File.WriteAllBytes(invalid, new byte[0xBF]);
        Assert.ThrowsExactly<InvalidDataException>(() => new WindowsLocalLinkStorage(fixture.Paths).CreatePlan(first, invalid));
        Assert.IsFalse(Directory.Exists(fixture.Paths.Root));
    }

    [STATestMethod]
    public void UiRejectsMixedPairButAcceptsTwoGbaWithoutStoppingMainDuringSelection()
    {
        using var fixture = new Fixture();
        int preparations = 0;
        using var lab = new frmLocalLinkLab(fixture.Settings, fixture.Gba("first.gba", 0x001F), () => { preparations++; return true; });
        lab.Show();
        lab.SetRom(1, fixture.Gb());
        Assert.IsFalse(Find<AetherButton>(lab, "linkStart").Enabled);
        Assert.AreEqual(AetherBoy.Runtime.Localization.UiText.Get("Nicht kompatibel: GB/GBC und GBA können nicht miteinander verkabelt werden."), Find<Label>(lab, "linkStatus").Text);
        Pump(lab.StartAsync());
        Assert.AreEqual(0, preparations);
        Assert.IsNull(Session(lab));
        lab.SetRom(1, fixture.Gba("second.gba", 0x03E0));
        Assert.IsTrue(Find<AetherButton>(lab, "linkStart").Enabled);
        Assert.AreEqual(0, preparations);
        lab.Close();
    }

    [TestMethod]
    public void ExistingManagedRomWithWrongFamilyExtensionCannotSilentlySelectAnotherCore()
    {
        using var fixture = new Fixture();
        string gba = fixture.Gba("correct.gba", 0x001F);
        string mislabeled = Path.ChangeExtension(gba, ".gb");
        File.Copy(gba, mislabeled);
        string managed = new WindowsRomLibrary(fixture.Paths).Import(mislabeled);
        Assert.AreEqual(".gb", Path.GetExtension(managed));
        Assert.ThrowsExactly<InvalidDataException>(() => new WindowsLocalLinkStorage(fixture.Paths).CreatePlan(gba, gba));
        Assert.IsTrue(File.Exists(managed), "Do not rename/delete an existing library item implicitly.");
        Assert.IsFalse(Directory.GetFiles(fixture.Paths.Saves, "*.sav", SearchOption.AllDirectories).Any());
    }

    [STATestMethod]
    public void GbaViewsUseNativeGeometryAndCanRestartAsGameBoyWithoutSharingFramesOrSaves()
    {
        using var fixture = new Fixture();
        using var lab = new frmLocalLinkLab(fixture.Settings, null, () => true);
        string first = fixture.Gba("gba-one.gba", 0x001F);
        string second = fixture.Gba("gba-two.gba", 0x03E0);
        lab.SetRom(0, first); lab.SetRom(1, second); lab.Show();
        try
        {
            Pump(lab.StartAsync());
            var session = Session(lab)!;
            Assert.IsTrue(session.IsGameBoyAdvance);
            PumpUntil(() => session.LatestSnapshot.FrameCount >= 3);
            for (int player = 0; player < 2; player++)
            {
                Assert.AreEqual(VideoGeometry.GameBoyAdvance, session.GetVideoGeometry(player));
                Assert.AreEqual(VideoGeometry.GameBoyAdvance, Find<GameDisplayControl>(lab, "linkDisplay" + player).VideoGeometry);
                Assert.AreEqual(240 * 160, Field<int[][]>(lab, "pixels")[player].Length);
                var frame = new int[240 * 160]; long sequence = 0;
                Assert.IsTrue(session.TryCopyLatestFrame(player, frame, ref sequence));
                int expectedColor = player == 0 ? 0xF80000 : 0x00F800;
                Assert.IsTrue(frame.Count(pixel => (pixel & 0xFFFFFF) == expectedColor) > 240,
                    $"Player {player + 1} must render its own ROM's color, not the other CPU's cached load value.");
                Assert.IsFalse(frame.Any(pixel => (pixel & 0xFFFFFF) == (player == 0 ? 0x00F800 : 0xF80000)),
                    "The two emulated CPUs and frame exchanges must remain independent.");
            }
            Capture(lab, "local-gba-link-lab.png");
            var plan = new WindowsLocalLinkStorage(WindowsDataPaths.Default).CreatePlan(first, second);
            Assert.ThrowsExactly<IOException>(() => RomWriteLease.Acquire(plan.FirstSavePath + ".lock"));
            Pump(session.SetGameBoyAdvanceButtonsAsync(0, GameBoyAdvanceButtons.L | GameBoyAdvanceButtons.R));
            Pump(session.SetPausedAsync(true));
            Find<AetherButton>(lab, "linkCable").PerformClick();
            PumpUntil(() => !session.LatestSnapshot.Connected);
            Pump(lab.StopAsync());
            using (RomWriteLease.Acquire(plan.FirstSavePath + ".lock")) { }
            using (RomWriteLease.Acquire(plan.SecondSavePath + ".lock")) { }

            string gb = fixture.Gb();
            lab.SetRom(0, gb); lab.SetRom(1, gb);
            Pump(lab.StartAsync());
            Assert.IsFalse(Session(lab)!.IsGameBoyAdvance);
            for (int player = 0; player < 2; player++)
            {
                Assert.AreEqual(VideoGeometry.GameBoy, Find<GameDisplayControl>(lab, "linkDisplay" + player).VideoGeometry);
                Assert.AreEqual(160 * 144, Field<int[][]>(lab, "pixels")[player].Length);
            }
        }
        finally { Pump(lab.StopAsync()); lab.Close(); }
    }

    [STATestMethod]
    public void ShoulderKeyboardMappingUsesConfiguredKeysIndependently()
    {
        using var fixture = new Fixture();
        using var lab = new frmLocalLinkLab(fixture.Settings, null, () => true);
        var map = typeof(frmLocalLinkLab).GetMethod("MapAdvanceKey", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.AreEqual(GameBoyAdvanceButtons.L, map.Invoke(lab, [fixture.Settings.KeyL]));
        Assert.AreEqual(GameBoyAdvanceButtons.R, map.Invoke(lab, [fixture.Settings.KeyR]));
        Assert.AreEqual(GameBoyAdvanceButtons.None, map.Invoke(lab, [Keys.F12]));
    }

    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner)!;
    private static LocalLinkSession? Session(frmLocalLinkLab lab) => Field<LocalLinkSession?>(lab, "session");
    private static T Find<T>(Control parent, string name) where T : Control => (T)parent.Controls.Find(name, true).Single();
    private static void Pump(Task task) { PumpUntil(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
    private static void PumpUntil(Func<bool> predicate)
    {
        var clock = Stopwatch.StartNew();
        while (!predicate())
        {
            if (clock.Elapsed.TotalSeconds > 15) Assert.Fail("GBA link UI did not reach the expected state.");
            Application.DoEvents(); Thread.Sleep(1);
        }
        Application.DoEvents();
    }
    private static void Capture(Form form, string name)
    {
        string? output = Environment.GetEnvironmentVariable("AETHERBOY_SMOKE_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(output)) return;
        Directory.CreateDirectory(output);
        form.BringToFront(); form.Refresh(); Application.DoEvents();
        using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        using (Graphics graphics = Graphics.FromImage(bitmap))
            graphics.CopyFromScreen(form.PointToScreen(Point.Empty), Point.Empty, form.ClientSize);
        bitmap.Save(Path.Combine(output, name));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string directory = Directory.CreateTempSubdirectory("aether-gba-link-ui-").FullName;
        private readonly bool audio, gpu, boot;
        private readonly int frameskip;
        internal WindowsDataPaths Paths { get; }
        internal NanoboySettings Settings { get; }
        internal Fixture()
        {
            Paths = new WindowsDataPaths(Path.Combine(directory, "managed"));
            var defaults = nanoboy.Properties.Settings.Default;
            audio = defaults.AudioEnable; gpu = defaults.GpuRendering; boot = defaults.BootRomEnable; frameskip = defaults.Frameskip;
            defaults.AudioEnable = defaults.GpuRendering = defaults.BootRomEnable = false;
            defaults.Frameskip = 0;
            Settings = new NanoboySettings();
        }
        internal string Gba(string name, ushort color)
        {
            byte[] rom = new byte[0x200];
            uint[] program = [0xE59F0018, 0xE59F1018, 0xE5801000, 0xE59F0014, 0xE59F1014,
                0xE5801000, 0xE2800004, 0xEAFFFFFC, 0x04000000, 0x00000403, 0x060001E0,
                (uint)color | ((uint)color << 16)];
            for (int index = 0; index < program.Length; index++)
                BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(index * 4), program[index]);
            Encoding.ASCII.GetBytes("LINK GBA").CopyTo(rom, 0xA0);
            Encoding.ASCII.GetBytes("SRAM_V113").CopyTo(rom, 0xC0);
            rom[0xB2] = 0x96;
            string path = Path.Combine(directory, name); File.WriteAllBytes(path, rom); return path;
        }
        internal string Gb()
        {
            byte[] rom = new byte[0x8000];
            rom[0x100] = 0x18; rom[0x101] = 0xFE;
            string path = Path.Combine(directory, "classic.gb"); File.WriteAllBytes(path, rom); return path;
        }
        public void Dispose()
        {
            var defaults = nanoboy.Properties.Settings.Default;
            defaults.AudioEnable = audio; defaults.GpuRendering = gpu; defaults.BootRomEnable = boot; defaults.Frameskip = frameskip;
            Directory.Delete(directory, recursive: true);
        }
    }
}
