using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using AetherBoy.Runtime;
using GameboyAdvanced.Core;
using GameboyAdvanced.Core.Debug;
using GameboyAdvanced.Core.Rom;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class EReaderTests
{
    public static byte[] Rom(string code = "PSAE")
    {
        byte[] rom = new byte[512]; BinaryPrimitives.WriteUInt32LittleEndian(rom, 0xEAFFFFFE);
        Encoding.ASCII.GetBytes(code).CopyTo(rom, 0xAC); return rom;
    }
    private static Device Device() => new([], new GamePak(Rom()), new TestDebugger(), true);

    [TestMethod]
    [DataRow("PSAE", true)] [DataRow("PSAJ", true)] [DataRow("PEAJ", true)] [DataRow("BPRE", false)]
    public void OnlyReaderCartridgesExposeHardware(string code, bool expected)
    {
        var pak = new GamePak(Rom(code)); Assert.AreEqual(expected, pak.EReader is not null);
        if (expected) Assert.AreEqual(RomBackupType.FLASH128, pak.RomBackupType);
    }

    [TestMethod]
    public void BusRegistersAreBoundedAndDoNotWriteRomOrFlash()
    {
        var d = Device(); byte[] rom = d.Gamepak.Data.ToArray(), flash = d.Gamepak._flashBackup!._data.ToArray();
        d.Bus.WriteHalfWord(0x0DF80000, 0xFFF9, 0, 0);
        Assert.AreEqual((ushort)9, d.Bus.ReadHalfWord(0x0DF80000, 0, 0, 0, 0, false));
        Assert.AreEqual((ushort)4, d.Bus.ReadHalfWord(0x0DFA0000, 0, 0, 0, 0, false));
        d.Bus.WriteByte(0x0E00FFB1, 0xFF, 0, 0);
        Assert.AreEqual((byte)0xB2, d.Bus.ReadByte(0x0E00FFB1, 0, 0, 0, 0, false));
        for (uint i = 0; i < 256; i += 2) _ = d.Bus.ReadHalfWord(0x0DFC0000 + i, 0, 0, 0, 0, false);
        Assert.AreEqual((ushort)0, d.Bus.ReadHalfWord(0x0DFE0000, 0, 0, 0, 0, false));
        CollectionAssert.AreEqual(rom, d.Gamepak.Data); CollectionAssert.AreEqual(flash, d.Gamepak._flashBackup._data);
    }

    [TestMethod]
    public void SerialSensorRegistersRoundTripAcrossBytesAndRespectReadOnlyIndices()
    {
        var e = new EReader(); WriteSerial(e, 0x14, [0x12, 0x34]);
        SetIndex(e, 0x14); StopSerial(e); BeginSerial(e); Send(e, 0x23);
        Assert.AreEqual((byte)0x12, Receive(e)); Assert.AreEqual((byte)0x34, Receive(e));
        StopSerial(e); WriteSerial(e, 0, [0xFF]); SetIndex(e, 0); StopSerial(e); BeginSerial(e); Send(e, 0x23);
        Assert.AreEqual((byte)0, Receive(e));
    }

    [TestMethod]
    // Independently executed, unmodified mGBA C decoder; synthetic byte i*37+11.
    [DataRow(1872, "D282472A3AC4953F599E045C8A0D7FA98E4E5EAF6F2379CD7C4CFF903D214D8C")]
    [DataRow(2912, "8089A9FE614E09A29CE65EADFDE93ED8737FEC11DDC1A7B2C3222DA8FF1C9D56")]
    [DataRow(3520, "38250A2ECACC5FD4B882E3AE6D62CC4D7BF9C8C53F91B62B6F5FD8CAA66BF1BF")]
    [DataRow(5456, "46D47F2CA9CBAA4A450625E98FBB47DF198E0637D89CF32E42B4D12697B8496B")]
    public void SupportedDotStripsMatchIndependentMgbaDecoder(int length, string expectedHash)
    {
        byte[] card = Enumerable.Range(0, length).Select(i => (byte)(i * 37 + 11)).ToArray();
        byte[] before = card.ToArray(), dots = EReaderDotCode.Decode(card);
        Assert.AreEqual(56800, dots.Length); Assert.IsTrue(dots.All(b => b <= 1));
        CollectionAssert.AreEqual(dots, EReaderDotCode.Decode(card)); CollectionAssert.AreEqual(before, card);
        Assert.IsTrue(dots.Any(b => b == 1));
        Assert.AreEqual(expectedHash, Convert.ToHexString(SHA256.HashData(dots)));
        TestContext.WriteLine($"{length}: {Convert.ToHexString(SHA256.HashData(dots))}");
    }
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void QueueUsesOwnedBytesFifoAndRejectsInvalidInputWithoutMutation()
    {
        var e = new EReader(); byte[] first = new byte[3520]; Array.Fill(first, (byte)255);
        e.QueueCard(first); Array.Clear(first); e.QueueCard(new byte[1872]);
        for (int i = 2; i < 16; i++) e.QueueCard(new byte[2912]);
        byte[] before = e.Capture();
        Assert.ThrowsExactly<InvalidOperationException>(() => e.QueueCard(new byte[1872]));
        Assert.ThrowsExactly<InvalidDataException>(() => e.QueueCard(new byte[13]));
        CollectionAssert.AreEqual(before, e.Capture());
        WriteSerial(e, 0x14, [0, 130]);
        StartScan(e); Assert.AreEqual(15, e.QueuedCards); Assert.AreEqual(1, e.CardsStarted);
        // Bring x into the card, then y into the first image line.
        for (int i = 0; i < 130 * 3 + 10; i++) e.WriteFlash(0xFFB1, 0);
        Assert.IsTrue(Enumerable.Range(0, 20).Any(i => e.Read((uint)(0x0DFC0000 + i * 2)) != 0));
        e.ClearCards(); Assert.AreEqual(0, e.QueuedCards); Assert.IsFalse(e.HasCard);
        Assert.AreEqual((ushort)0, e.Read(0x0DFC0000));
    }

    [TestMethod]
    public void ScanlineIrqStateRestoreAndResetFollowEmulatedCycles()
    {
        var d = Device(); var e = d.Gamepak.EReader!;
        e.QueueCard(new byte[1872]); e.QueueCard(new byte[2912]);
        e.WriteFlash(0xFFB2, 100); StartScan(e); // IRQ in 200 emulated cycles.
        byte[] before = d.CaptureState();
        Assert.IsFalse(d.InterruptRegisters._interruptRequest._gamepak);
        for (int i = 0; i < 250; i++) d.RunCycle();
        Assert.IsTrue(d.InterruptRegisters._interruptRequest._gamepak);
        byte[] after = d.CaptureState();
        d.RestoreState(before); CollectionAssert.AreEqual(before, d.CaptureState());
        for (int i = 0; i < 250; i++) d.RunCycle();
        CollectionAssert.AreEqual(after, d.CaptureState());
        d.Reset(true); Assert.AreEqual(0, d.Gamepak.EReader!.QueuedCards); Assert.IsFalse(d.Gamepak.EReader.HasCard);
    }

    [TestMethod]
    public void MalformedAccessoryTailIsRejectedBeforeMutatingCore()
    {
        var d = Device(); d.Gamepak.EReader!.QueueCard(new byte[1872]);
        byte[] before = d.CaptureState(), invalid = before.ToArray();
        int bodyLength = invalid.Length - 32, count = BinaryPrimitives.ReadInt32LittleEndian(invalid.AsSpan(bodyLength - 4));
        invalid[bodyLength - 4 - count] = 99;
        SHA256.HashData(invalid.AsSpan(0, bodyLength)).CopyTo(invalid, bodyLength);
        Assert.ThrowsExactly<InvalidDataException>(() => d.RestoreState(invalid));
        CollectionAssert.AreEqual(before, d.CaptureState());
    }

    [TestMethod]
    public void CalibrationInitializesOnlyCompletelyErasedSectors()
    {
        var pak = new GamePak(Rom()); var flash = pak._flashBackup!._data;
        flash[0xD020] = 0x12;
        byte[] partial = flash.AsSpan(0xD000, 4096).ToArray(); pak.InitializeEReaderCalibration();
        CollectionAssert.AreEqual(partial, flash.AsSpan(0xD000, 4096).ToArray());
        Assert.AreEqual((byte)'C', flash[0xE000]); Assert.AreEqual((byte)255, flash[0xCFFF]);
        byte[] before = flash.ToArray(); pak.InitializeEReaderCalibration(); CollectionAssert.AreEqual(before, flash);
    }

    [TestMethod]
    public void SubsequentScanConsumesNextCardAndStateKeepsQueueOrder()
    {
        var e = new EReader(); byte[] white = new byte[3520]; Array.Fill(white, (byte)255);
        e.QueueCard(white); e.QueueCard(new byte[3520]);
        WriteSerial(e, 0x14, [0, 130]); StartScan(e);
        for (int i = 0; i < 130 * 3 + 10; i++) e.WriteFlash(0xFFB1, 0);
        Assert.IsTrue(Enumerable.Range(0, 20).Any(i => e.Read((uint)(0x0DFC0000 + i * 2)) != 0));
        e = EReader.Decode(e.Capture());
        e.WriteFlash(0xFFB0, 0); StartScan(e);
        for (int i = 0; i < 130 * 3 + 10; i++) e.WriteFlash(0xFFB1, 0);
        Assert.AreEqual(0, e.QueuedCards); Assert.AreEqual(2, e.CardsStarted);
        Assert.IsTrue(Enumerable.Range(0, 20).All(i => e.Read((uint)(0x0DFC0000 + i * 2)) == 0));
    }

    [TestMethod]
    public void WaitingSecondScanAcceptsLateCardAndReplaysFromState()
    {
        var e = new EReader(); e.QueueCard(new byte[3520]);
        WriteSerial(e, 0x14, [0, 130]); StartScan(e);
        for (int i = 0; i < 260; i++) e.WriteFlash(0xFFB1, 0);
        e.WriteFlash(0xFFB0, 0); StartScan(e);
        for (int i = 0; i < 217; i++) e.WriteFlash(0xFFB1, 0);
        byte[] waiting = e.Capture();
        byte[] white = new byte[3520]; Array.Fill(white, (byte)255);
        void InsertAndScan(EReader reader)
        {
            reader.QueueCard(white);
            for (int i = 0; i < 130 * 3 + 11; i++) reader.WriteFlash(0xFFB1, 0);
            Assert.AreEqual(0, reader.QueuedCards, "A card inserted after scan start must not wait for another button press.");
            Assert.AreEqual(2, reader.CardsStarted);
            Assert.IsTrue(Enumerable.Range(0, 20).Any(i => reader.Read((uint)(0x0DFC0000 + i * 2)) != 0));
        }
        InsertAndScan(e); var restored = EReader.Decode(waiting); InsertAndScan(restored);
        CollectionAssert.AreEqual(e.Capture(), restored.Capture());
    }

    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public async Task NonReaderCartridgesRejectReaderCommandsWithoutFaulting(bool gba)
    {
        string root = Path.Combine(Path.GetTempPath(), "aether-reader-reject-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string rom = Path.Combine(root, gba ? "idle.gba" : "idle.gb"); byte[] bytes = gba ? Rom("BPRE") : new byte[32768];
            if (!gba) { bytes[0x100] = 0x18; bytes[0x101] = 0xFE; }
            File.WriteAllBytes(rom, bytes);
            await using var session = new EmulationSession(rom, Path.Combine(root, "idle.sav"), null, new(0, false, true, true, true, true, 44100));
            Assert.IsTrue(SpinWait.SpinUntil(() => session.State != SessionState.Starting, TimeSpan.FromSeconds(5)));
            await session.SetPausedAsync(true);
            await Assert.ThrowsExactlyAsync<NotSupportedException>(() => session.QueueEReaderCardAsync(new byte[1872]));
            await Assert.ThrowsExactlyAsync<NotSupportedException>(() => session.ClearEReaderCardsAsync());
            Assert.IsNull(session.Fault); Assert.IsNull(session.LatestSnapshot.EReader); Assert.IsTrue(session.LatestSnapshot.IsPaused);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task OwnerQueueSnapshotsResetAndBoundedImportWorkWithoutCommercialRom()
    {
        string root = Path.Combine(Path.GetTempPath(), "aether-ereader-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string rom = Path.Combine(root, "reader.gba"), card = Path.Combine(root, "card.raw");
            File.WriteAllBytes(rom, Rom()); File.WriteAllBytes(card, new byte[2912]);
            byte[] imported = EReaderInput.ReadFile(card);
            File.WriteAllBytes(card, new byte[5457]); Assert.ThrowsExactly<InvalidDataException>(() => EReaderInput.ReadFile(card));
            await using var session = new EmulationSession(rom, Path.Combine(root, "reader.sav"), null, new(0, false, true, true, true, true, 44100));
            Assert.IsTrue(SpinWait.SpinUntil(() => session.State != SessionState.Starting, TimeSpan.FromSeconds(5)));
            await session.SetPausedAsync(true); Assert.IsTrue(session.LatestSnapshot.Supports(EmulationFeature.EReader));
            await session.QueueEReaderCardAsync(imported);
            Assert.AreEqual(1, session.LatestSnapshot.EReader!.QueuedCards);
            Assert.AreEqual(session.LatestSnapshot.EReader, session.LatestSnapshot.WithState(SessionState.Running, true).EReader);
            Assert.IsTrue(session.LatestSnapshot.IsPaused);
            await session.ClearEReaderCardsAsync(); Assert.AreEqual(0, session.LatestSnapshot.EReader!.QueuedCards);
            await session.QueueEReaderCardAsync(imported); await session.ResetAsync();
            Assert.AreEqual(0, session.LatestSnapshot.EReader!.QueuedCards); Assert.IsNull(session.Fault);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task CalibrationPersistsOnShutdownAndExistingFlashSurvivesReopen()
    {
        string root = Path.Combine(Path.GetTempPath(), "aether-reader-flash-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string rom = Path.Combine(root, "reader.gba"), save = Path.Combine(root, "reader.sav"); File.WriteAllBytes(rom, Rom());
            async Task OpenAndStop(string savePath)
            {
                await using var session = new EmulationSession(rom, savePath, null, new(0, false, true, true, true, true, 44100));
                Assert.IsTrue(SpinWait.SpinUntil(() => session.State != SessionState.Starting, TimeSpan.FromSeconds(5)));
                await session.SetPausedAsync(true); Assert.IsNull(session.Fault);
            }
            await OpenAndStop(save); byte[] initialized = File.ReadAllBytes(save);
            Assert.AreEqual(0x20000, initialized.Length); Assert.AreEqual((byte)'C', initialized[0xD000]); Assert.AreEqual((byte)'C', initialized[0xE000]);
            await OpenAndStop(save); CollectionAssert.AreEqual(initialized, File.ReadAllBytes(save));
            // An imported raw save has no AetherBoy integrity sidecar. Do not
            // tamper with a managed save and then bypass its checksum protection.
            string imported = Path.Combine(root, "imported.sav");
            initialized[0x1234] = 0x42; initialized[0xD030] = 0x17; File.WriteAllBytes(imported, initialized);
            await OpenAndStop(imported); CollectionAssert.AreEqual(initialized, File.ReadAllBytes(imported));
        }
        finally { Directory.Delete(root, true); }
    }

    private static void StartScan(EReader e) { e.WriteFlash(0xFFB0, 0x18); e.WriteFlash(0xFFB0, 0x18); }
    private static void BeginSerial(EReader e) { e.WriteFlash(0xFFB0, 7); e.WriteFlash(0xFFB0, 6); e.WriteFlash(0xFFB0, 4); }
    private static void StopSerial(EReader e) { e.WriteFlash(0xFFB0, 6); e.WriteFlash(0xFFB0, 7); }
    private static void Send(EReader e, byte value)
    {
        for (int bit = 7; bit >= 0; bit--) { byte b = (byte)(4 | ((value >> bit) & 1)); e.WriteFlash(0xFFB0, (byte)(b | 2)); e.WriteFlash(0xFFB0, b); }
    }
    private static byte Receive(EReader e)
    {
        byte result = 0;
        for (int bit = 0; bit < 8; bit++)
        { e.WriteFlash(0xFFB0, 2); e.WriteFlash(0xFFB0, 0); result = (byte)((result << 1) | (e.ReadFlash(0xFFB0) & 1)); }
        return result;
    }
    private static void SetIndex(EReader e, byte index) { BeginSerial(e); Send(e, 0x22); Send(e, index); }
    private static void WriteSerial(EReader e, byte index, byte[] values)
    { SetIndex(e, index); foreach (byte value in values) Send(e, value); StopSerial(e); }
}
