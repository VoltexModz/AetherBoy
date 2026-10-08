using System.IO.Compression;
using System.Security.Cryptography;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Video;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;

namespace AetherBoy.Testing;

// Optional local integration fixture. No commercial ROM, saved game or image is
// shipped. The supplied archive is only read; every writable path is isolated.
internal sealed class BattleSpaceFixture : IDisposable
{
    internal const string RomHash = "85B16134B866E1A1009CB0E8EEA892293D60B7592262E64C0FBA85BF73B67DBC";
    // PNG digests of the two card screens visually accepted on 2026-10-06.
    // These pin the complete game image (including stats), not just a byte count.
    internal const string BerserkerImage = "09EACA4D972EBA57A2C0E8E685E3743DE0BBF54413FF02EC2B8549034F357B89";
    internal const string ValkyrieImage = "850BD23257AEB11AB143777B2B98775139CA2596E8489ACAE2825C2BEF53330A";
    internal string Root { get; } = Path.Combine(Path.GetTempPath(), "aether-battle-space-" + Guid.NewGuid().ToString("N"));
    internal string RomPath => Path.Combine(Root, "test.gb");
    internal Nanoboy Emulator { get; }
    internal BarcodeBoy Scanner => Emulator.Memory.BarcodeScanner!;
    private readonly string archivePath;
    private readonly byte[] archiveHash;

    internal BattleSpaceFixture(bool connect = true)
    {
        string? configured = Environment.GetEnvironmentVariable("AETHERBOY_BATTLE_SPACE_ZIP");
        if (string.IsNullOrWhiteSpace(configured))
            Assert.Inconclusive("Set AETHERBOY_BATTLE_SPACE_ZIP to a locally supplied Battle Space ZIP. No ROM is downloaded.");
        archivePath = Path.GetFullPath(configured!);
        archiveHash = SHA256.HashData(File.ReadAllBytes(archivePath));
        byte[] rom;
        using (var archive = ZipFile.OpenRead(archivePath))
        {
            Assert.HasCount(1, archive.Entries);
            var entry = archive.Entries[0];
            Assert.IsTrue(entry.FullName.EndsWith(".gb", StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(65536L, entry.Length);
            using var source = entry.Open();
            rom = new byte[65536]; source.ReadExactly(rom);
            Assert.AreEqual(-1, source.ReadByte());
        }
        Assert.AreEqual(RomHash, Convert.ToHexString(SHA256.HashData(rom)), "This fixture only verifies the reviewed revision.");
        Directory.CreateDirectory(Root);
        File.WriteAllBytes(RomPath, rom);
        Emulator = new Nanoboy(new ROM(RomPath, Path.Combine(Root, "test.sav")));
        if (connect) Emulator.Memory.SetBarcodeBoyEnabled(true);
    }

    internal void Frames(int count, GameBoyButtons buttons = GameBoyButtons.None)
    {
        Emulator.SetButtons(buttons);
        for (int i = 0; i < count; i++) Emulator.Frame();
        Emulator.SetButtons(GameBoyButtons.None);
    }

    internal void BootToScan(bool assertReady = true)
    {
        Frames(180); Frames(8, GameBoyButtons.Start); Frames(180);
        Frames(8, GameBoyButtons.A); Frames(120); Frames(8, GameBoyButtons.A); Frames(120);
        if (assertReady) Assert.IsTrue(Scanner.Ready, "Game must complete scanner detection.");
    }

    internal void Rescan()
    {
        Frames(8, GameBoyButtons.Down); Frames(10); Frames(8, GameBoyButtons.A); Frames(120);
        Assert.IsTrue(Scanner.Ready, "The game's No option must reopen card entry.");
    }

    internal IDisposable TraceSerial(string capture) => new SerialTrace(Emulator, capture);

    private sealed class SerialTrace : IObserver<CPUStatusUpdate>, IDisposable
    {
        private readonly List<string> lines = new();
        private readonly IDisposable subscription;
        private readonly string capture;
        internal SerialTrace(Nanoboy emulator, string capture)
        { this.capture = capture; subscription = emulator.Cpu.Subscribe(this); }
        public void OnNext(CPUStatusUpdate update)
        {
            if (lines.Count >= 10000) return;
            if ((update.Offset == 0xFF01 && update.Reason is CPUStatusUpdate.UpdateReason.MemoryRead or CPUStatusUpdate.UpdateReason.MemoryWrite) ||
                (update.Offset == 0xFF02 && update.Reason == CPUStatusUpdate.UpdateReason.MemoryWrite))
                lines.Add($"{update.Reason} PC={update.CPU.PC:X4} address={update.Offset:X4} value={update.Value:X2}");
        }
        public void OnError(Exception error) { }
        public void OnCompleted() { }
        public void Dispose()
        {
            subscription.Dispose();
            string? output = Environment.GetEnvironmentVariable("AETHERBOY_BATTLE_SPACE_CAPTURE_DIR");
            if (string.IsNullOrWhiteSpace(output)) return;
            Directory.CreateDirectory(output);
            File.WriteAllLines(Path.Combine(output, capture + ".serial.txt"), lines);
        }
    }

    internal byte[] Image()
    {
        int[] pixels = new int[Video.FramePixelCount]; long sequence = -1;
        Assert.IsTrue(Emulator.Memory.Video.TryCopyPublishedFrame(pixels, ref sequence));
        return NativeScreenshot.Encode(VideoGeometry.GameBoy, pixels);
    }

    internal void AssertCard(string code, string capture)
    {
        Assert.IsFalse(Scanner.HasPendingScan);
        AssertImage(Image(), code, capture);
    }

    internal static void AssertImage(byte[] image, string code, string capture)
    {
        SaveImage(image, capture);
        Assert.AreEqual(code == BarcodeBoyInput.BattleSpaceBerserker ? BerserkerImage : ValkyrieImage,
            Convert.ToHexString(SHA256.HashData(image)), "Wrong game image/card/stats (or renderer/PNG encoding changed; inspect the capture).");
    }

    internal static byte[] SessionImage(EmulationSession session)
    {
        int[] pixels = new int[Video.FramePixelCount]; long sequence = -1;
        Assert.IsTrue(session.TryCopyLatestFrame(pixels, ref sequence));
        return NativeScreenshot.Encode(VideoGeometry.GameBoy, pixels);
    }

    internal static bool SessionShowsCard(EmulationSession session, string code)
    {
        int[] pixels = new int[Video.FramePixelCount]; long sequence = -1;
        return session.TryCopyLatestFrame(pixels, ref sequence) &&
            Convert.ToHexString(SHA256.HashData(NativeScreenshot.Encode(VideoGeometry.GameBoy, pixels))) ==
            (code == BarcodeBoyInput.BattleSpaceBerserker ? BerserkerImage : ValkyrieImage);
    }

    internal static void SaveImage(byte[] image, string capture)
    {
        string? output = Environment.GetEnvironmentVariable("AETHERBOY_BATTLE_SPACE_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(output)) return;
        Directory.CreateDirectory(output);
        File.WriteAllBytes(Path.Combine(output, capture + ".png"), image);
    }

    public void Dispose()
    {
        try { Emulator.Dispose(); }
        finally
        {
            // Root is a fresh, fixed-prefix temp directory owned by this fixture.
            Directory.Delete(Root, true);
            CollectionAssert.AreEqual(archiveHash, SHA256.HashData(File.ReadAllBytes(archivePath)), "Original archive was changed.");
        }
    }
}
