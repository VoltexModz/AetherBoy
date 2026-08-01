using System.Text;
using AetherBoy.Runtime;
using nanoboy.Core;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class Phase2CommandCoverageTests
{
    private const int RomSize = 32 * 1024;
    private static readonly TimeSpan DeadlockTimeout = TimeSpan.FromSeconds(10);

    private static readonly byte[] NintendoLogo =
    {
        0xCE, 0xED, 0x66, 0x66, 0xCC, 0x0D, 0x00, 0x0B,
        0x03, 0x73, 0x00, 0x83, 0x00, 0x0C, 0x00, 0x0D,
        0x00, 0x08, 0x11, 0x1F, 0x88, 0x89, 0x00, 0x0E,
        0xDC, 0xCC, 0x6E, 0xE6, 0xDD, 0xDD, 0xD9, 0x99,
        0xBB, 0xBB, 0x67, 0x63, 0x6E, 0x0E, 0xEC, 0xCC,
        0xDD, 0xDC, 0x99, 0x9F, 0xBB, 0xB9, 0x33, 0x3E
    };

    [TestMethod]
    public async Task TurboCommandPublishesStateAndCanReturnToPacedExecution()
    {
        var factory = new RecordingMachineFactory();
        using var pacer = new ManualFramePacer();
        await using var session = new EmulationSession(factory, pacer);

        await factory.Created.Task.WaitAsync(DeadlockTimeout);
        pacer.WaitForWaitCount(1, DeadlockTimeout);

        Task enableTurbo = session.SetTurboAsync(true);
        pacer.ReleaseOneFrame();
        await enableTurbo.WaitAsync(DeadlockTimeout);

        Assert.IsTrue(session.LatestSnapshot.IsTurboEnabled);
        Assert.AreEqual(SessionState.Running, session.LatestSnapshot.State);

        await session.SetTurboAsync(false).WaitAsync(DeadlockTimeout);
        pacer.WaitForWaitCount(2, DeadlockTimeout);

        Assert.IsFalse(session.LatestSnapshot.IsTurboEnabled);
        Assert.AreEqual(SessionState.Running, session.State);

        await session.ShutdownAsync().WaitAsync(DeadlockTimeout);
    }

    [TestMethod]
    public async Task CheatCommandsPublishCompleteLifecycleSnapshots()
    {
        var factory = new RecordingMachineFactory();
        using var pacer = new ManualFramePacer();
        await using var session = new EmulationSession(factory, pacer);

        await factory.Created.Task.WaitAsync(DeadlockTimeout);
        pacer.WaitForWaitCount(1, DeadlockTimeout);

        Task<CheatSnapshot> addCheat = session.AddCheatAsync("Lives", "014200C0");
        pacer.ReleaseOneFrame();
        CheatSnapshot added = await addCheat.WaitAsync(DeadlockTimeout);
        pacer.WaitForWaitCount(2, DeadlockTimeout);

        Assert.AreNotEqual(Guid.Empty, added.Id);
        Assert.AreEqual("Lives", added.Name);
        Assert.AreEqual("014200C0", added.Code);
        Assert.IsTrue(added.Enabled);
        Assert.AreEqual(1, session.LatestSnapshot.Cheats.Count);
        Assert.AreEqual(added, session.LatestSnapshot.Cheats[0]);

        Task<bool> toggleCheat = session.ToggleCheatAsync(added.Id);
        pacer.ReleaseOneFrame();
        bool enabledAfterToggle = await toggleCheat.WaitAsync(DeadlockTimeout);
        pacer.WaitForWaitCount(3, DeadlockTimeout);

        Assert.IsFalse(enabledAfterToggle);
        Assert.AreEqual(1, session.LatestSnapshot.Cheats.Count);
        Assert.AreEqual(added.Id, session.LatestSnapshot.Cheats[0].Id);
        Assert.IsFalse(session.LatestSnapshot.Cheats[0].Enabled);

        Task<bool> removeCheat = session.RemoveCheatAsync(added.Id);
        pacer.ReleaseOneFrame();
        bool removed = await removeCheat.WaitAsync(DeadlockTimeout);
        pacer.WaitForWaitCount(4, DeadlockTimeout);

        Assert.IsTrue(removed);
        Assert.AreEqual(0, session.LatestSnapshot.Cheats.Count);

        await session.ShutdownAsync().WaitAsync(DeadlockTimeout);
    }

    [TestMethod]
    public async Task GeneratedRomRunsThroughProductionSessionAndPublishesVideoFrame()
    {
        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"AetherBoy.RuntimeTests-{Guid.NewGuid():N}");
        string romPath = Path.Combine(temporaryDirectory, "generated-runtime.gb");
        string savePath = Path.Combine(temporaryDirectory, "generated-runtime.sav");

        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            File.WriteAllBytes(romPath, CreateVideoRom());
            var configuration = new EmulatorConfiguration(
                Frameskip: 0,
                AudioEnabled: false,
                Channel1Enabled: false,
                Channel2Enabled: false,
                Channel3Enabled: false,
                Channel4Enabled: false,
                SampleRate: 44_100);
            var factory = new ProductionMachineFactory(
                romPath,
                savePath,
                bootRom: null,
                configuration,
                paletteIndex: 0);
            using var pacer = new ManualFramePacer();
            await using var session = new EmulationSession(factory, pacer);

            pacer.WaitForWaitCount(1, DeadlockTimeout);

            EmulationSnapshot snapshot = session.LatestSnapshot;
            Assert.AreEqual(SessionState.Running, snapshot.State);
            Assert.IsTrue(snapshot.EmulatedFrameCount >= 1);
            Assert.IsTrue(snapshot.HasVideoFrame);
            Assert.AreEqual("AETHERBOY RT", snapshot.Rom?.Title);

            var frame = new int[EmulationSnapshot.FramePixelCount];
            long frameSequence = 0;
            Assert.IsTrue(session.TryCopyLatestFrame(frame, ref frameSequence));
            Assert.AreEqual(snapshot.VideoFrameSequence, frameSequence);
            Assert.AreEqual(unchecked((int)0xFF000000), frame[0]);
            Assert.AreEqual(
                unchecked((int)0xFFF5F5F5),
                frame[EmulationSnapshot.FrameWidth]);

            await session.ShutdownAsync().WaitAsync(DeadlockTimeout);
        }
        finally
        {
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }

            if (File.Exists(romPath))
            {
                File.Delete(romPath);
            }

            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: false);
            }
        }
    }

    private static byte[] CreateVideoRom()
    {
        var rom = new byte[RomSize];

        rom[0x0100] = 0xC3; // JP $0150
        rom[0x0101] = 0x50;
        rom[0x0102] = 0x01;
        rom[0x0103] = 0x00;

        NintendoLogo.CopyTo(rom, 0x0104);
        Encoding.ASCII.GetBytes("AETHERBOY RT").CopyTo(rom, 0x0134);
        rom[0x0143] = 0x00; // DMG-compatible
        rom[0x0146] = 0x00; // no SGB features
        rom[0x0147] = (byte)Mbc.ROM_NONE;
        rom[0x0148] = 0x00; // 32 KiB ROM
        rom[0x0149] = 0x00; // no cartridge RAM
        rom[0x014A] = 0x01; // non-Japanese destination
        rom[0x014B] = 0x00;
        rom[0x014C] = 0x00;
        rom[0x014D] = ComputeHeaderChecksum(rom);

        // Make tile zero's first row use color index 3, then halt before rendering starts.
        rom[0x0150] = 0x3E; // LD A,$FF
        rom[0x0151] = 0xFF;
        rom[0x0152] = 0xEA; // LD ($8000),A
        rom[0x0153] = 0x00;
        rom[0x0154] = 0x80;
        rom[0x0155] = 0xEA; // LD ($8001),A
        rom[0x0156] = 0x01;
        rom[0x0157] = 0x80;
        rom[0x0158] = 0x76; // HALT

        ushort globalChecksum = ComputeGlobalChecksum(rom);
        rom[0x014E] = (byte)(globalChecksum >> 8);
        rom[0x014F] = (byte)globalChecksum;
        return rom;
    }

    private static byte ComputeHeaderChecksum(byte[] rom)
    {
        byte checksum = 0;
        for (int address = 0x0134; address <= 0x014C; address++)
        {
            checksum = unchecked((byte)(checksum - rom[address] - 1));
        }

        return checksum;
    }

    private static ushort ComputeGlobalChecksum(byte[] rom)
    {
        ushort checksum = 0;
        for (int address = 0; address < rom.Length; address++)
        {
            if (address is 0x014E or 0x014F)
            {
                continue;
            }

            checksum = unchecked((ushort)(checksum + rom[address]));
        }

        return checksum;
    }
}
