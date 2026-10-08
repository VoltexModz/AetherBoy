using nanoboy.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class BarcodeBoyTests
{
    private const string Code = "4907981000301";

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(true, false, false)]
    [DataRow(true, true, false)]
    [DataRow(true, true, true)]
    public void HandshakeAndTwoCompletePacketsFollowEmulatedClock(bool color, bool fast, bool doubleSpeed)
    {
        using var f = new Fixture(color); var m = f.M;
        f.Emulator.Cpu.IsDoubleSpeed = doubleSpeed;
        m.BarcodeScanner!.QueueScan(Code);
        int internalCycles = fast ? 128 : 4096;
        byte sc = fast ? (byte)0x83 : (byte)0x81;
        byte[] requests = [0x10, 7, 0x10, 7], replies = [255, 255, 0x10, 7];
        for (int i = 0; i < 4; i++) Assert.AreEqual(replies[i], Transfer(m, requests[i], sc, internalCycles));
        Assert.IsTrue(m.BarcodeScanner.Ready);
        Assert.AreEqual((byte)255, Transfer(m, 0, sc, internalCycles), "Ready internal-clock polling returns FF without consuming a card.");
        byte[] packet = [2, .. System.Text.Encoding.ASCII.GetBytes(Code), 3];
        for (int i = 0; i < 30; i++)
        {
            Tick(m, BarcodeBoy.PacketGapDots * (doubleSpeed ? 2 : 1));
            Assert.AreEqual(i, m.BarcodeScanner.BytesSent, "No transfer without SC start.");
            Assert.AreEqual(packet[i % 15], Transfer(m, 0, 0x80, doubleSpeed ? 8192 : 4096));
        }
        Assert.AreEqual(1, m.BarcodeScanner.CompletedScans); Assert.IsFalse(m.BarcodeScanner.Ready);
        Assert.IsFalse(m.BarcodeScanner.HasPendingScan);
        m.BarcodeScanner.QueueScan(Code); m.WriteByte(0xFF02, 0x80); Tick(m, 20000);
        Assert.AreEqual(8, m.SerialBitsRemaining, "A second scan must wait for a new handshake.");
    }

    [TestMethod]
    public void ExternalClockWaitsForScanAndAbortDoesNotConsumeByte()
    {
        using var f = new Fixture(); var m = f.M; Handshake(m);
        m.WriteByte(0xFF02, 0x80); Tick(m, 20000); Assert.AreEqual(8, m.SerialBitsRemaining);
        m.BarcodeScanner!.QueueScan(Code); Tick(m, 512 * 3 + 50);
        m.WriteByte(0xFF02, 0); Assert.AreEqual(0, m.BarcodeScanner.BytesSent);
        Assert.AreEqual((byte)2, Transfer(m, 0, 0x80, 4096));
        Assert.AreEqual(1, m.BarcodeScanner.BytesSent);
    }

    [TestMethod]
    public void InvalidAndBusyInputDoesNotReplaceQueuedCard()
    {
        using var f = new Fixture(); var scanner = f.M.BarcodeScanner!;
        foreach (string invalid in new[] { "", "123", "490798100030x", "４９０７９８１０００３０１", "490798 1000301" })
            Assert.ThrowsExactly<ArgumentException>(() => scanner.QueueScan(invalid));
        scanner.QueueScan(" " + Code + "\r\n");
        Assert.ThrowsExactly<InvalidOperationException>(() => scanner.QueueScan("4908052808369"));
        Handshake(f.M);
        Transfer(f.M, 0, 0x80, 4096);
        Assert.AreEqual((byte)'4', Transfer(f.M, 0, 0x80, 4096));
    }

    [TestMethod]
    public void SaveStateRestoresScannerAndPartialByteWithoutLosingClockPhase()
    {
        using var f = new Fixture(); var m = f.M; Handshake(m); m.BarcodeScanner!.QueueScan(Code);
        m.WriteByte(0xFF02, 0x80); Tick(m, 777);
        byte[] saved = SaveState.Capture(f.Emulator);
        Tick(m, 4096 - 777); byte[] expected = SaveState.Capture(f.Emulator);
        m.SetBarcodeBoyEnabled(false); Assert.IsNull(m.BarcodeScanner);
        SaveState.Restore(f.Emulator, saved); CollectionAssert.AreEqual(saved, SaveState.Capture(f.Emulator));
        Tick(m, 4096 - 778); Assert.AreEqual(1, m.SerialBitsRemaining);
        Tick(m, 1); CollectionAssert.AreEqual(expected, SaveState.Capture(f.Emulator));
        Assert.AreEqual((byte)2, m.ReadByte(0xFF01));
    }

    [TestMethod]
    public void HandshakeAbortResetAndLegacyStateHaveDefinedBehavior()
    {
        using var f = new Fixture(); var m = f.M;
        Transfer(m, 0x10, 0x81, 4096);
        m.WriteByte(0xFF01, 7); m.WriteByte(0xFF02, 0x81); Tick(m, 1000); m.WriteByte(0xFF02, 0);
        Assert.AreEqual((byte)255, Transfer(m, 7, 0x81, 4096));
        Assert.AreEqual((byte)0x10, Transfer(m, 0x10, 0x81, 4096));
        Assert.AreEqual((byte)7, Transfer(m, 7, 0x81, 4096));
        m.BarcodeScanner!.QueueScan(Code); f.Emulator.Reset();
        Assert.IsFalse(m.BarcodeScanner.Ready); Assert.IsFalse(m.BarcodeScanner.HasPendingScan);
        m.SetBarcodeBoyEnabled(false); byte[] legacy = SaveState.Capture(f.Emulator);
        m.SetBarcodeBoyEnabled(true); SaveState.Restore(f.Emulator, legacy); Assert.IsNull(m.BarcodeScanner);
    }

    [TestMethod]
    public void MalformedAccessoryStateIsRejectedBeforeMutation()
    {
        using var f = new Fixture(); f.M.BarcodeScanner!.QueueScan(Code);
        byte[] before = SaveState.Capture(f.Emulator), payload = f.M.CaptureSerialStatePayload();
        payload[4] = 99;
        Assert.ThrowsExactly<InvalidDataException>(() => f.M.PrepareSerialStateRestore(payload));
        CollectionAssert.AreEqual(before, SaveState.Capture(f.Emulator));
        Assert.ThrowsExactly<InvalidDataException>(() => f.M.PrepareSerialStateRestore([1, 2, 3, 4]));
        var document = EmulatorStateCodec.Deserialize(before);
        var sections = document.Sections.Select(s => new EmulatorStateSection(s.Id, s.SchemaVersion, s.Required,
            s.Id == (ushort)CoreStateSection.Serial ? payload : s.CopyPayload())).ToArray();
        byte[] invalid = EmulatorStateCodec.Serialize(new EmulatorStateDocument(document.RomSha256, document.HardwareModel, sections));
        Assert.ThrowsExactly<InvalidDataException>(() => SaveState.Restore(f.Emulator, invalid));
        CollectionAssert.AreEqual(before, SaveState.Capture(f.Emulator));
    }

    [TestMethod]
    public void ExistingCableCannotBeReplacedByScanner()
    {
        using var first = new Fixture(); using var second = new Fixture();
        first.M.SetBarcodeBoyEnabled(false); second.M.SetBarcodeBoyEnabled(false);
        using var cable = new LocalSerialCable(first.M, second.M);
        Assert.ThrowsExactly<InvalidOperationException>(() => first.M.SetBarcodeBoyEnabled(true));
    }

    [TestMethod]
    public void RewindKeepsScannerAndQueuedCardOnTheSameTimeline()
    {
        using var f = new Fixture(); Handshake(f.M); f.M.BarcodeScanner!.QueueScan(Code);
        var rewind = new RewindManager(); rewind.Initialize(f.Emulator);
        byte[] expected = SaveState.Capture(f.Emulator);
        for (int i = 0; i < 8; i++) { f.Emulator.Frame(); rewind.CaptureFrame(f.Emulator); }
        Assert.IsTrue(rewind.Rewind(f.Emulator)); Assert.IsTrue(rewind.Rewind(f.Emulator));
        CollectionAssert.AreEqual(expected, SaveState.Capture(f.Emulator));
        Assert.IsTrue(f.M.BarcodeScanner!.HasPendingScan);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void InternalClockKeepsDividerPhaseAtEveryPossibleStartOffset(bool fast, bool doubleSpeed)
    {
        using var f = new Fixture(true);
        int half = fast ? 8 : 256;
        byte control = fast ? (byte)0x83 : (byte)0x81;
        // Offset coverage includes both halves of the source-divider waveform.
        for (int phase = 0; phase < half; phase++)
        {
            f.Emulator.Reset(); f.Emulator.Cpu.IsDoubleSpeed = doubleSpeed;
            Tick(f.M, phase);
            f.M.WriteByte(0xFF01, 0x10); f.M.WriteByte(0xFF02, control);
            int firstBit = 2 * half - phase;
            Tick(f.M, firstBit - 1); Assert.AreEqual(8, f.M.SerialBitsRemaining);
            Tick(f.M, 1); Assert.AreEqual(7, f.M.SerialBitsRemaining);
            Tick(f.M, 14 * half - 1); Assert.AreEqual(1, f.M.SerialBitsRemaining);
            Tick(f.M, 1); Assert.AreEqual(0, f.M.SerialBitsRemaining);
            Assert.AreEqual(8, f.M.Interrupt.IF & 8);
        }
    }

    [TestMethod]
    [DataRow(64, 0, 4096)]
    [DataRow(192, 0, 3840)]
    [DataRow(320, 0, 3840)]
    [DataRow(448, 1, 3584)]
    public void DivWriteResetsPrescalerAndOnlyFallingFullClockShiftsData(int elapsed, int shifted, int remaining)
    {
        using var f = new Fixture();
        f.M.WriteByte(0xFF01, 0x10); f.M.WriteByte(0xFF02, 0x81); Tick(f.M, elapsed);
        f.M.WriteByte(0xFF04, 0);
        Assert.AreEqual(8 - shifted, f.M.SerialBitsRemaining);
        Tick(f.M, remaining - 1); Assert.AreEqual(1, f.M.SerialBitsRemaining);
        Tick(f.M, 1); Assert.AreEqual(0, f.M.SerialBitsRemaining);
        Assert.AreEqual((byte)255, f.M.ReadByte(0xFF01));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SaveRestorePreservesDividerAndInternalHandshakeHalfBit(bool fast)
    {
        using var f = new Fixture(true);
        int period = fast ? 16 : 512;
        Tick(f.M, period / 4);
        f.M.WriteByte(0xFF01, 0x10); f.M.WriteByte(0xFF02, fast ? (byte)0x83 : (byte)0x81);
        Tick(f.M, period + 3);
        byte[] saved = SaveState.Capture(f.Emulator);
        Tick(f.M, period * 8); byte[] expected = SaveState.Capture(f.Emulator);
        SaveState.Restore(f.Emulator, saved);
        Tick(f.M, period * 8);
        CollectionAssert.AreEqual(expected, SaveState.Capture(f.Emulator));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DuplicatePacketGapSurvivesSaveRestoreAndScRearming(bool doubleSpeed)
    {
        using var f = new Fixture(true); f.Emulator.Cpu.IsDoubleSpeed = doubleSpeed;
        var m = f.M; Handshake(m); m.BarcodeScanner!.QueueScan(Code);
        int byteCycles = doubleSpeed ? 8192 : 4096;
        for (int i = 0; i < 15; i++) Transfer(m, 0, 0x80, byteCycles);
        Assert.AreEqual(15, m.BarcodeScanner.BytesSent);
        int gapCycles = BarcodeBoy.PacketGapDots * (doubleSpeed ? 2 : 1);
        m.WriteByte(0xFF02, 0x80); Tick(m, 1000);
        m.WriteByte(0xFF02, 0); m.WriteByte(0xFF02, 0x80);
        byte[] saved = SaveState.Capture(f.Emulator);
        void FinishDuplicateStart()
        {
            Tick(m, gapCycles - 1000); Assert.AreEqual(8, m.SerialBitsRemaining);
            Tick(m, byteCycles - 1); Assert.AreEqual(1, m.SerialBitsRemaining);
            Tick(m, 1); Assert.AreEqual((byte)2, m.ReadByte(0xFF01));
            Assert.AreEqual(16, m.BarcodeScanner!.BytesSent);
        }
        FinishDuplicateStart(); byte[] expected = SaveState.Capture(f.Emulator);
        SaveState.Restore(f.Emulator, saved); FinishDuplicateStart();
        CollectionAssert.AreEqual(expected, SaveState.Capture(f.Emulator));
        SaveState.Restore(f.Emulator, saved); f.Emulator.Reset();
        Assert.AreEqual(0, m.BarcodeScanner!.BytesSent); Assert.IsFalse(m.BarcodeScanner.HasPendingScan);
    }

    [TestMethod]
    public void Bcb1StateRemainsReadableAndInvalidBcb2DelayIsAtomic()
    {
        using var f = new Fixture(); Handshake(f.M); f.M.BarcodeScanner!.QueueScan(Code);
        byte[] current = f.M.CaptureSerialStatePayload(), before = SaveState.Capture(f.Emulator);
        byte[] legacy = current[..^4]; legacy[3] = (byte)'1';
        f.M.PrepareSerialStateRestore(legacy)();
        CollectionAssert.AreEqual(before, SaveState.Capture(f.Emulator));
        byte[] invalid = current.ToArray();
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(invalid.AsSpan(invalid.Length - 4), 1);
        Assert.ThrowsExactly<InvalidDataException>(() => f.M.PrepareSerialStateRestore(invalid));
        CollectionAssert.AreEqual(before, SaveState.Capture(f.Emulator));
    }

    [TestMethod]
    public void RepeatedScansPreserveEveryDigitWithoutRestrictingPrefixesOrCorrectingChecksums()
    {
        using var f = new Fixture(); var m = f.M;
        string[] codes = ["4907981000301", "4500000000000", "9900000000009", "1234567890123",
            "0000000000000", "9999999999999", "4907981000300"];
        for (int scan = 0; scan < 128; scan++)
        {
            string code = codes[scan % codes.Length];
            Tick(m, scan * 37 % 257);
            Handshake(m); m.BarcodeScanner!.QueueScan(code);
            byte[] expected = [2, .. System.Text.Encoding.ASCII.GetBytes(code), 3];
            for (int position = 0; position < 30; position++)
            {
                if (position == 15) Tick(m, BarcodeBoy.PacketGapDots);
                Assert.AreEqual(expected[position % 15], Transfer(m, 0, 0x80, 4096),
                    $"Scan {scan + 1}, position {position}: transport must not rewrite card data.");
            }
            Assert.AreEqual(scan + 1, m.BarcodeScanner.CompletedScans);
            Assert.IsFalse(m.BarcodeScanner.HasPendingScan); Assert.IsFalse(m.BarcodeScanner.Ready);
        }
    }

    [TestMethod]
    public void DetectionRetryKeepsQueuedCardAndSurvivesStateRestore()
    {
        using var f = new Fixture(); var m = f.M; Handshake(m);
        m.BarcodeScanner!.QueueScan(Code);
        Assert.AreEqual((byte)255, Transfer(m, 0x10, 0x81, 4096));
        Assert.IsFalse(m.BarcodeScanner.Ready);
        byte[] retry = SaveState.Capture(f.Emulator);
        void Finish()
        {
            Assert.AreEqual((byte)255, Transfer(m, 7, 0x81, 4096));
            Assert.AreEqual((byte)0x10, Transfer(m, 0x10, 0x81, 4096));
            Assert.AreEqual((byte)7, Transfer(m, 7, 0x81, 4096));
            Assert.IsTrue(m.BarcodeScanner!.Ready);
            Assert.IsTrue(m.BarcodeScanner.HasPendingScan);
        }
        Finish(); byte[] expected = SaveState.Capture(f.Emulator);
        SaveState.Restore(f.Emulator, retry); Finish();
        CollectionAssert.AreEqual(expected, SaveState.Capture(f.Emulator));
        Assert.AreEqual((byte)2, Transfer(m, 0, 0x80, 4096));
        // No retry may rewind or interrupt a card that has already started.
        foreach (byte request in new byte[] { 0x10, 7, 0x10, 7 })
            Assert.AreEqual((byte)255, Transfer(m, request, 0x81, 4096));
        Assert.IsTrue(m.BarcodeScanner.Ready); Assert.AreEqual(1, m.BarcodeScanner.BytesSent);
        Assert.AreEqual((byte)'4', Transfer(m, 0, 0x80, 4096));
    }

    [TestMethod]
    public void AbortedDetectionRetryDoesNotLoseReadiness()
    {
        using var f = new Fixture(); var m = f.M; Handshake(m);
        m.WriteByte(0xFF01, 0x10); m.WriteByte(0xFF02, 0x81); Tick(m, 512);
        m.WriteByte(0xFF02, 0);
        Assert.IsTrue(m.BarcodeScanner!.Ready);
        Assert.AreEqual((byte)255, Transfer(m, 7, 0x81, 4096));
        Assert.IsTrue(m.BarcodeScanner.Ready);
    }

    private static void Handshake(Memory m) { foreach (byte b in new byte[] { 0x10, 7, 0x10, 7 }) Transfer(m, b, 0x81, 4096); }
    private static byte Transfer(Memory m, byte data, byte sc, int cycles)
    {
        if ((sc & 1) != 0) cycles -= m.Timer.DividerCounter & (cycles / 16 - 1);
        m.Interrupt.IF = 0; m.WriteByte(0xFF01, data); m.WriteByte(0xFF02, sc);
        Tick(m, cycles - 1); Assert.AreEqual(0, m.Interrupt.IF & 8); Assert.AreEqual(1, m.SerialBitsRemaining);
        Tick(m, 1); Assert.AreEqual(8, m.Interrupt.IF & 8); Assert.AreEqual(0, m.ReadByte(0xFF02) & 0x80);
        return m.ReadByte(0xFF01);
    }
    private static void Tick(Memory m, int cycles) { for (int i = 0; i < cycles; i++) { m.Timer.Tick(); m.TickSerial(); } }
    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "aether-barcode-" + Guid.NewGuid().ToString("N"));
        public Nanoboy Emulator { get; }
        public Memory M => Emulator.Memory;
        public Fixture(bool color = false)
        {
            Directory.CreateDirectory(root); string path = Path.Combine(root, "test.gb"); byte[] rom = new byte[32768];
            rom[0x100] = 0x18; rom[0x101] = 0xFE; rom[0x143] = color ? (byte)0x80 : (byte)0;
            File.WriteAllBytes(path, rom); Emulator = new(new ROM(path, Path.Combine(root, "test.sav"))); M.SetBarcodeBoyEnabled(true);
        }
        public void Dispose() { Emulator.Dispose(); Directory.Delete(root, true); }
    }
}
