using System.Buffers.Binary;
using System.Text;
using System.Reflection;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Storage;
using GameboyAdvanced.Core;
using nanoboy.Core;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class LocalGbaLinkSessionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    private static readonly EmulatorConfiguration Configuration = new(0, true, true, true, true, true, 44_100);
    private string directory = null!;

    [TestInitialize]
    public void CreateDirectory() => directory = Directory.CreateTempSubdirectory("aether-gba-link-tests-").FullName;

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(directory, recursive: true);

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task ArmProgramsExchangeMultiplayerWordsAndExecuteRealSerialInterrupts(int baud)
    {
        var first = MakePlayer("master", CreateRom(true, 0x1234, baud));
        var second = MakePlayer("slave", CreateRom(false, 0xABCD, baud));
        using var pacer = new ManualFramePacer();
        await using var session = new LocalLinkSession(first, second, pacer);
        await session.Ready.WaitAsync(Timeout);
        pacer.WaitForWaitCount(1, Timeout);
        Assert.IsNull(session.Fault);
        Assert.IsTrue(session.IsGameBoyAdvance);
        Assert.IsTrue(session.LatestSnapshot.First!.IsGameBoyAdvance);
        Assert.IsTrue(session.LatestSnapshot.Second!.IsGameBoyAdvance);
        Assert.IsGreaterThan(0L, session.LatestSnapshot.ClockEdges);
        await session.ShutdownAsync().WaitAsync(Timeout);

        byte[] firstSave = File.ReadAllBytes(first.SavePath);
        byte[] secondSave = File.ReadAllBytes(second.SavePath);
        foreach (byte[] save in new[] { firstSave, secondSave })
        {
            Assert.AreEqual((ushort)0x1234, BinaryPrimitives.ReadUInt16LittleEndian(save));
            Assert.AreEqual((ushort)0xABCD, BinaryPrimitives.ReadUInt16LittleEndian(save.AsSpan(2)));
            Assert.AreEqual((ushort)0xFFFF, BinaryPrimitives.ReadUInt16LittleEndian(save.AsSpan(4)));
            Assert.AreEqual((ushort)0xFFFF, BinaryPrimitives.ReadUInt16LittleEndian(save.AsSpan(6)));
            Assert.AreEqual((byte)0x42, save[10], "The cartridge's actual ARM interrupt handler must execute.");
            Assert.AreEqual(0, save[8] & 0x80, "IRQ handler must observe transfer no longer busy.");
        }
        Assert.AreEqual(0, firstSave[8] & 0x30);
        Assert.AreEqual(0x10, secondSave[8] & 0x30);
    }

    [TestMethod]
    public async Task BothPlayersPublishNativeGbaFramesAndRejectGbSizedBuffers()
    {
        var first = MakePlayer("first", CreateRom(true, 1));
        var second = MakePlayer("second", CreateRom(false, 2, color: 0x03E0));
        using var pacer = new ManualFramePacer();
        await using var session = new LocalLinkSession(first, second, pacer);
        Assert.AreEqual(VideoGeometry.GameBoyAdvance, session.GetVideoGeometry(0), "Geometry is known before Ready.");
        await session.Ready.WaitAsync(Timeout);
        pacer.WaitForWaitCount(1, Timeout);
        Assert.AreEqual(VideoGeometry.GameBoyAdvance, session.LatestSnapshot.VideoGeometry);
        for (int player = 0; player < 2; player++)
        {
            int[] pixels = new int[240 * 160];
            long sequence = 0;
            Assert.IsTrue(session.TryCopyLatestFrame(player, pixels, ref sequence));
            Assert.AreEqual(player == 0 ? 0x00F80000 : 0x0000F800, pixels[240] & 0x00FFFFFF,
                "The native frames must contain each cartridge's own color, not its peer's CPU/rendering state.");
            Assert.IsFalse(session.TryCopyLatestFrame(player, pixels, ref sequence));
        }
        long invalidSequence = 0;
        Assert.ThrowsExactly<ArgumentException>(() => session.TryCopyLatestFrame(0, new int[160 * 144], ref invalidSequence));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.GetVideoGeometry(2));
        await session.ShutdownAsync().WaitAsync(Timeout);
    }

    [TestMethod]
    public async Task ShoulderButtonsPauseAndReconnectRemainIndependentAndOwnerThreadControlled()
    {
        var first = MakePlayer("first", CreateRom(true, 1));
        var second = MakePlayer("second", CreateRom(false, 2));
        using var pacer = new ManualFramePacer();
        await using var session = new LocalLinkSession(first, second, pacer);
        await session.Ready.WaitAsync(Timeout);
        pacer.WaitForWaitCount(1, Timeout);
        Task pause = session.SetPausedAsync(true);
        pacer.ReleaseOneFrame();
        await pause.WaitAsync(Timeout);
        long count = session.LatestSnapshot.FrameCount;
        await session.SetGameBoyAdvanceButtonsAsync(0, GameBoyAdvanceButtons.L).WaitAsync(Timeout);
        await session.SetGameBoyAdvanceButtonsAsync(1, GameBoyAdvanceButtons.R).WaitAsync(Timeout);
        await session.SetButtonsAsync(0, GameBoyButtons.A).WaitAsync(Timeout);
        await session.SetButtonsAsync(1, GameBoyButtons.B).WaitAsync(Timeout);
        await session.SetConnectedAsync(false).WaitAsync(Timeout);
        Assert.IsFalse(session.LatestSnapshot.Connected);
        Assert.AreEqual(count, session.LatestSnapshot.FrameCount);
        await session.SetConnectedAsync(true).WaitAsync(Timeout);
        Assert.IsTrue(session.LatestSnapshot.Connected);
        await session.SetPausedAsync(false).WaitAsync(Timeout);
        pacer.WaitForWaitCount(2, Timeout);
        await session.ShutdownAsync().WaitAsync(Timeout);
        ushort firstKeys = BinaryPrimitives.ReadUInt16LittleEndian(File.ReadAllBytes(first.SavePath).AsSpan(12));
        ushort secondKeys = BinaryPrimitives.ReadUInt16LittleEndian(File.ReadAllBytes(second.SavePath).AsSpan(12));
        Assert.AreEqual(0x01FE, firstKeys & 0x03FF, "P1: only A and L are pressed (active-low).");
        Assert.AreEqual(0x02FD, secondKeys & 0x03FF, "P2: only B and R are pressed (active-low).");
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => session.SetGameBoyAdvanceButtonsAsync(0, GameBoyAdvanceButtons.None));
    }

    [TestMethod]
    public async Task SameGbaRomUsesExclusiveIndependentBatteryFiles()
    {
        var first = MakePlayer("same", CreateRom(false, 0x1234));
        var second = first with { SavePath = Path.Combine(directory, "player2.sav") };
        File.WriteAllBytes(first.SavePath, Enumerable.Repeat((byte)0x11, 0x8000).ToArray());
        File.WriteAllBytes(second.SavePath, Enumerable.Repeat((byte)0x22, 0x8000).ToArray());
        using var pacer = new ManualFramePacer();
        await using var session = new LocalLinkSession(first, second, pacer);
        await session.Ready.WaitAsync(Timeout);
        Assert.AreEqual(session.LatestSnapshot.First!.RomSha256, session.LatestSnapshot.Second!.RomSha256);
        Assert.ThrowsExactly<IOException>(() => RomWriteLease.Acquire(first.SavePath + ".lock"));
        Assert.ThrowsExactly<IOException>(() => RomWriteLease.Acquire(second.SavePath + ".lock"));
        await session.ShutdownAsync().WaitAsync(Timeout);
        Assert.AreEqual((byte)0x11, File.ReadAllBytes(first.SavePath)[0x100]);
        Assert.AreEqual((byte)0x22, File.ReadAllBytes(second.SavePath)[0x100]);
        using var firstLease = RomWriteLease.Acquire(first.SavePath + ".lock");
        using var secondLease = RomWriteLease.Acquire(second.SavePath + ".lock");
    }

    [TestMethod]
    public void MixedFamiliesAndSharedSavesAreRejectedBeforeAnyLeaseOrSaveMutation()
    {
        var first = MakePlayer("first", CreateRom(true, 1));
        var second = MakePlayer("second", CreateRom(false, 2));
        Assert.ThrowsExactly<NotSupportedException>(() => new LocalLinkSession(first, second with { RomPath = "classic.gb" }));
        Assert.ThrowsExactly<NotSupportedException>(() => new LocalLinkSession(first with { RomPath = "classic.gbc" }, second));
        Assert.ThrowsExactly<ArgumentException>(() => new LocalLinkSession(first, second with { SavePath = first.SavePath }));
        Assert.IsFalse(File.Exists(first.SavePath));
        Assert.IsFalse(File.Exists(first.SavePath + ".lock"));
    }

    [TestMethod]
    public async Task InvalidSecondGbaReleasesBothWriteLeases()
    {
        var first = MakePlayer("first", CreateRom(true, 1));
        var second = MakePlayer("invalid", new byte[10]);
        var session = new LocalLinkSession(first, second);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => session.Ready.WaitAsync(Timeout));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => session.Completion.WaitAsync(Timeout));
        Assert.AreEqual(SessionState.Faulted, session.State);
        using var firstLease = RomWriteLease.Acquire(first.SavePath + ".lock");
        using var secondLease = RomWriteLease.Acquire(second.SavePath + ".lock");
    }

    [TestMethod]
    public void SyntheticArmProgramConfiguresMultiplayerAndExecutesFromCartridge()
    {
        var first = MakePlayer("program-check", CreateRom(true, 0x1234));
        using var machine = new GbaProductionMachine(first.RomPath, first.SavePath, Configuration,
            initializeRewind: false);
        var device = (Device)typeof(GbaProductionMachine).GetField("device", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(machine)!;
        for (int cycle = 0; cycle < Device.CPU_CYCLES_PER_FRAME; cycle++) machine.RunLocalLinkCycle();
        string state = $"PC={device.Cpu.R[15]:X8}; cycles={device.Cpu.Cycles}; RCNT={device.InspectHalfWord(0x04000134):X4}; SIOCNT={device.InspectHalfWord(0x04000128):X4}; " +
            $"R0={device.Cpu.R[0]:X8}, R1={device.Cpu.R[1]:X8}, R2={device.Cpu.R[2]:X8}, R4={device.Cpu.R[4]:X8}; IRQ={device.InspectHalfWord(0x04000202):X4}";
        Assert.AreEqual(0, device.InspectHalfWord(0x04000134) & 0x8000, state);
        Assert.AreEqual(0x2000, device.InspectHalfWord(0x04000128) & 0x3000, state);
        Assert.AreEqual((byte)0x42, device.InspectByte(0x0E00000A), state);
    }

    private LocalLinkPlayerConfiguration MakePlayer(string name, byte[] rom)
    {
        string romPath = Path.Combine(directory, name + ".gba");
        File.WriteAllBytes(romPath, rom);
        return new(romPath, Path.Combine(directory, name + ".sav"), null, Configuration);
    }

    private static byte[] CreateRom(bool master, ushort outgoing, int baud = 3, ushort color = 0x001F)
    {
        byte[] rom = new byte[0x1000];
        BinaryPrimitives.WriteUInt32LittleEndian(rom, 0xEA00003E); // B 0x100: skip cartridge header.
        Encoding.ASCII.GetBytes("AETHER LINK").CopyTo(rom, 0xA0);
        Encoding.ASCII.GetBytes("ALNE00").CopyTo(rom, 0xAC);
        rom[0xB2] = 0x96;
        Encoding.ASCII.GetBytes("SRAM_V113").CopyTo(rom, 0xC0);

        var code = new ArmBuilder();
        code.Load(0, 0x04000000); code.Load(1, 0x00000403); code.Emit(0xE5801000); // Mode 3 + BG2.
        code.Load(0, 0x060001E0); code.Load(1, color | ((uint)color << 16)); code.Emit(0xE5801000); // Player-specific pixels.
        code.Load(0, 0x03007FFC); code.Load(1, 0x08000600); code.Emit(0xE5801000); // BIOS IRQ callback.
        code.Load(0, 0x04000200); code.Emit(0xE3A01080, 0xE1C010B0); // IE: serial.
        code.Emit(0xE3A01001, 0xE1C010B8); // IME.
        code.Load(0, 0x04000134); code.Emit(0xE3A01000, 0xE1C010B0); // RCNT: leave power-on GPIO mode.
        code.Load(0, 0x04000128); code.Load(1, (uint)(0x6000 | baud)); code.Emit(0xE1C010B0); // Multiplayer + IRQ.
        code.Load(1, outgoing); code.Emit(0xE1C010B2); // SIOMLT_SEND.
        if (master)
        {
            code.Load(4, 0x800); code.Emit(0xE2544001, 0x1AFFFFFD); // Give slave time to configure.
            code.Load(1, (uint)(0x6080 | baud)); code.Emit(0xE1C010B0); // Begin transfer.
        }
        code.Load(0, 0x04000130); code.Load(2, 0x0E00000C);
        code.Emit(0xE1D010B0, 0xE5C21000, 0xE1A01421, 0xE5C21001, 0xEAFFFFFA); // Persist KEYINPUT continuously.
        code.CopyTo(rom, 0x100);

        var irq = new ArmBuilder();
        irq.Load(0, 0x04000120); irq.Load(2, 0x0E000000);
        for (uint slot = 0; slot < 4; slot++)
        {
            irq.Emit(0xE1D010B0 | slot * 2); // LDRH r1, [r0, #slot*2].
            irq.Emit(0xE5C21000 | slot * 2, 0xE1A01421, 0xE5C21000 | (slot * 2 + 1));
        }
        irq.Emit(0xE1D010B8, 0xE5C21008, 0xE1A01421, 0xE5C21009); // SIOCNT and player ID.
        irq.Emit(0xE3A01042, 0xE5C2100A); // Prove the actual CPU handled the interrupt.
        irq.Load(0, 0x04000200); irq.Emit(0xE3A01080, 0xE1C010B2, 0xE12FFF1E); // Acknowledge IF, BX LR.
        irq.CopyTo(rom, 0x600);
        return rom;
    }

    private sealed class ArmBuilder
    {
        private readonly List<uint> words = [];
        private readonly List<(int Index, int Register, uint Value)> literals = [];
        internal void Emit(params uint[] instructions) => words.AddRange(instructions);
        internal void Load(int register, uint value)
        {
            literals.Add((words.Count, register, value));
            words.Add(0);
        }
        internal void CopyTo(byte[] destination, int offset)
        {
            foreach (var literal in literals)
            {
                int displacement = words.Count * 4 - (literal.Index * 4 + 8);
                words[literal.Index] = 0xE59F0000u | (uint)(literal.Register << 12) | (uint)displacement;
                words.Add(literal.Value);
            }
            for (int index = 0; index < words.Count; index++)
                BinaryPrimitives.WriteUInt32LittleEndian(destination.AsSpan(offset + index * 4), words[index]);
        }
    }
}
