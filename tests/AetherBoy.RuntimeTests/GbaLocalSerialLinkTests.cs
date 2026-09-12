using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using GameboyAdvanced.Core;
using GameboyAdvanced.Core.Bus;
using GameboyAdvanced.Core.Debug;
using GameboyAdvanced.Core.Rom;
using GameboyAdvanced.Core.Serial;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class GbaLocalSerialLinkTests
{
    [TestMethod]
    public void DebugRegisterNamesSupportMultiplayerAliasesWithoutDuplicateKeyFailure()
    {
        string? name = IORegs.GetNameFromAddress(IORegs.SIODATA32);
        Assert.IsNotNull(name);
        StringAssert.Contains(name, nameof(IORegs.SIODATA32));
        StringAssert.Contains(name, nameof(IORegs.SIOMULTI0));
        Assert.IsNull(IORegs.GetNameFromAddress(0));
    }

    [TestMethod]
    [DataRow(0, 63_427)]
    [DataRow(1, 16_241)]
    [DataRow(2, 10_998)]
    [DataRow(3, 5_755)]
    public void MultiplayerLatchesBothWordsAndCompletesExactlyAtSelectedBaud(int baud, int cycles)
    {
        Device parent = CreateDevice();
        Device child = CreateDevice();
        using var link = new LocalSerialLink(parent.SerialController, child.SerialController);
        ConfigureMultiplayer(parent, 0x1234, baud, irq: true);
        ConfigureMultiplayer(child, 0xABCD, baud, irq: true);
        parent.SerialController.WriteHalfWord(IORegs.SIOCNT, (ushort)(0x6080 | baud));
        AssertBusy(parent, true);
        AssertBusy(child, true);
        Assert.AreEqual(uint.MaxValue, parent.InspectWord(IORegs.SIOMULTI0));
        Assert.AreEqual(uint.MaxValue, child.InspectWord(IORegs.SIOMULTI2));
        // Changing SEND while busy affects the next transaction only.
        parent.SerialController.WriteHalfWord(IORegs.SIODATA8, 0x9999);
        child.SerialController.WriteHalfWord(IORegs.SIODATA8, 0x8888);
        RunPair(parent, child, cycles - 1);
        AssertBusy(parent, true);
        AssertBusy(child, true);
        Assert.AreEqual(0, parent.InspectHalfWord(IORegs.IF) & 0x80);
        RunPair(parent, child, 1);
        foreach (Device device in new[] { parent, child })
        {
            Assert.AreEqual(0xABCD_1234u, device.InspectWord(IORegs.SIOMULTI0));
            Assert.AreEqual(uint.MaxValue, device.InspectWord(IORegs.SIOMULTI2));
            AssertBusy(device, false);
            Assert.AreEqual(0, device.InspectHalfWord(IORegs.SIOCNT) & 0x40);
            Assert.AreEqual(0x80, device.InspectHalfWord(IORegs.IF) & 0x80);
        }
        Assert.AreEqual(0, parent.InspectHalfWord(IORegs.SIOCNT) & 0x34);
        Assert.AreEqual(0x14, child.InspectHalfWord(IORegs.SIOCNT) & 0x34);
        Assert.AreEqual(1L, link.CompletedTransfers);
        Assert.AreEqual(32L, link.ClockEdges);
    }

    [TestMethod]
    public void MultiplayerStatusCannotBeForgedAndChildCannotStartOrCancelParentTransfer()
    {
        Device parent = CreateDevice();
        Device child = CreateDevice();
        using var link = new LocalSerialLink(parent.SerialController, child.SerialController);
        ConfigureMultiplayer(parent, 1);
        Assert.AreEqual(0, parent.InspectHalfWord(IORegs.SIOCNT) & 8);
        ConfigureMultiplayer(child, 2);
        Assert.AreEqual(8, parent.InspectHalfWord(IORegs.SIOCNT) & 8);
        child.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x20FF);
        Assert.AreEqual(0x201F, child.InspectHalfWord(IORegs.SIOCNT));
        AssertBusy(child, false);
        RunPair(parent, child, 100);
        Assert.AreEqual(0L, link.ClockEdges);
        parent.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x20FF);
        Assert.AreEqual(0x208B, parent.InspectHalfWord(IORegs.SIOCNT));
        child.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x2003);
        AssertBusy(child, true);
        RunPair(parent, child, 5_755);
        Assert.AreEqual(0x0002_0001u, child.InspectWord(IORegs.SIOMULTI0));
    }

    [TestMethod]
    public void MultiplayerReceiveRegistersSupportByteHalfWordAndWordAccess()
    {
        Device device = CreateDevice();
        ConfigureMultiplayer(device, 0x1234);
        device.SerialController.WriteWord(IORegs.SIOMULTI0, 0x8765_4321);
        device.SerialController.WriteHalfWord(IORegs.SIOMULTI2, 0xBA98);
        device.SerialController.WriteByte(IORegs.SIOMULTI3, 0xDC);
        device.SerialController.WriteByte(IORegs.SIOMULTI3 + 1, 0xFE);
        Assert.AreEqual((byte)0x65, device.InspectByte(IORegs.SIOMULTI1));
        Assert.AreEqual((ushort)0x8765, device.InspectHalfWord(IORegs.SIOMULTI1));
        Assert.AreEqual(0xFEDC_BA98u, device.InspectWord(IORegs.SIOMULTI2));
        Assert.AreEqual((ushort)0x1234, device.InspectHalfWord(IORegs.SIODATA8));
    }

    [TestMethod]
    public void MultiplayerDisconnectSignalsErrorAndReconnectAllowsFreshTransfer()
    {
        Device parent = CreateDevice();
        Device child = CreateDevice();
        using var link = new LocalSerialLink(parent.SerialController, child.SerialController);
        ConfigureMultiplayer(parent, 0x1234);
        ConfigureMultiplayer(child, 0xABCD);
        parent.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x2083);
        RunPair(parent, child, 100);
        link.SetConnected(false);
        AssertBusy(parent, true);
        Assert.AreEqual(0, parent.InspectHalfWord(IORegs.SIOCNT) & 8);
        RunPair(parent, child, 5_655);
        Assert.AreEqual(0x40, parent.InspectHalfWord(IORegs.SIOCNT) & 0x40);
        Assert.AreEqual(0x40, child.InspectHalfWord(IORegs.SIOCNT) & 0x40);
        Assert.AreEqual(0xFFFF_1234u, parent.InspectWord(IORegs.SIOMULTI0));
        link.SetConnected(true);
        Assert.AreEqual(8, parent.InspectHalfWord(IORegs.SIOCNT) & 8);
        parent.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x2083);
        Assert.AreEqual(0, parent.InspectHalfWord(IORegs.SIOCNT) & 0x40);
        RunPair(parent, child, 5_755);
        Assert.AreEqual(0xABCD_1234u, child.InspectWord(IORegs.SIOMULTI0));
    }

    [TestMethod]
    public void MultiplayerModeChangeCancelsBothWithoutStaleIrqOrStaleWords()
    {
        Device parent = CreateDevice();
        Device child = CreateDevice();
        using var link = new LocalSerialLink(parent.SerialController, child.SerialController);
        ConfigureMultiplayer(parent, 0x1234, irq: true);
        ConfigureMultiplayer(child, 0xABCD, irq: true);
        parent.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x6083);
        RunPair(parent, child, 100);
        child.SerialController.WriteHalfWord(IORegs.RCNT, 0x8000);
        AssertBusy(parent, false);
        AssertBusy(child, false);
        Assert.AreEqual(1L, link.AbortedTransfers);
        RunPair(parent, child, 10_000);
        Assert.AreEqual(0, parent.InspectHalfWord(IORegs.IF) & 0x80);
        Assert.AreEqual(0, child.InspectHalfWord(IORegs.IF) & 0x80);
        Assert.AreEqual(uint.MaxValue, parent.InspectWord(IORegs.SIOMULTI0));
        ConfigureMultiplayer(child, 0x5678);
        parent.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x2083);
        RunPair(parent, child, 5_755);
        Assert.AreEqual(0x5678_1234u, parent.InspectWord(IORegs.SIOMULTI0));
    }

    [TestMethod]
    public void MultiplayerSerialResetKeepsCableAndAllowsBootRegisterReset()
    {
        Device parent = CreateDevice();
        Device child = CreateDevice();
        using var link = new LocalSerialLink(parent.SerialController, child.SerialController);
        ConfigureMultiplayer(parent, 1);
        ConfigureMultiplayer(child, 2);
        parent.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x2083);
        child.SerialController.Reset();
        Assert.IsTrue(child.SerialController.IsLinked);
        Assert.IsTrue(link.Connected);
        AssertBusy(parent, false);
        ConfigureMultiplayer(child, 3);
        parent.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x2083);
        RunPair(parent, child, 5_755);
        Assert.AreEqual(0x0003_0001u, parent.InspectWord(IORegs.SIOMULTI0));
    }

    [TestMethod]
    public void MultiplayerWithoutReadyPeerCompletesWithErrorInsteadOfHanging()
    {
        Device parent = CreateDevice();
        Device child = CreateDevice();
        using var link = new LocalSerialLink(parent.SerialController, child.SerialController);
        ConfigureMultiplayer(parent, 0xBEEF, irq: true);
        parent.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x6083);
        RunPair(parent, child, 3_139);
        AssertBusy(parent, true);
        RunPair(parent, child, 1);
        Assert.AreEqual(0xFFFF_BEEFu, parent.InspectWord(IORegs.SIOMULTI0));
        Assert.AreEqual(0x40, parent.InspectHalfWord(IORegs.SIOCNT) & 0x40);
        Assert.AreEqual(0x80, parent.InspectHalfWord(IORegs.IF) & 0x80);
        AssertBusy(child, false);
    }

    [TestMethod]
    public void MultiplayerExternalClockCanWakeStoppedChild()
    {
        Device parent = CreateDevice();
        Device child = CreateDevice();
        using var link = new LocalSerialLink(parent.SerialController, child.SerialController);
        ConfigureMultiplayer(parent, 1);
        ConfigureMultiplayer(child, 2, irq: true);
        child.InterruptRegisters.WriteHalfWord(IORegs.IE, 0x80);
        child.Bus.HaltMode = HaltMode.Stop;
        parent.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x2083);
        RunPair(parent, child, 5_755);
        Assert.AreEqual(0x0002_0001u, child.InspectWord(IORegs.SIOMULTI0));
        Assert.AreEqual(0x80, child.InspectHalfWord(IORegs.IF) & 0x80);
        Assert.AreNotEqual(HaltMode.Stop, child.Bus.HaltMode);
    }

    [TestMethod]
    [DataRow(false, false, 512)]
    [DataRow(false, true, 64)]
    [DataRow(true, false, 2_048)]
    [DataRow(true, true, 256)]
    public void NormalSerialShiftsAtExactClockAndFinishesWithIrq(bool wordMode, bool fast, int cycles)
    {
        Device first = CreateDevice();
        Device second = CreateDevice();
        using var link = new LocalSerialLink(first.SerialController, second.SerialController);
        WriteNormal(first, wordMode, wordMode ? 0x1234_5678u : 0x35, internalClock: true, fast: fast, start: false);
        WriteNormal(second, wordMode, wordMode ? 0xA1B2_C3D4u : 0xA7, internalClock: false, start: true);
        first.SerialController.WriteHalfWord(IORegs.SIOCNT, (ushort)((wordMode ? 0x1000 : 0) | 0x4081 | (fast ? 2 : 0)));
        RunPair(first, second, cycles - 1);
        AssertBusy(first, true);
        Assert.AreEqual(0, first.InspectHalfWord(IORegs.IF) & 0x80);
        RunPair(first, second, 1);
        Assert.AreEqual(wordMode ? 0xA1B2_C3D4u : 0xA7u, ReadNormal(first, wordMode));
        Assert.AreEqual(wordMode ? 0x1234_5678u : 0x35u, ReadNormal(second, wordMode));
        Assert.AreEqual(0x80, first.InspectHalfWord(IORegs.IF) & 0x80);
        Assert.AreEqual(0x80, second.InspectHalfWord(IORegs.IF) & 0x80);
        Assert.AreEqual(wordMode ? 32L : 8L, link.ClockEdges);
        Assert.AreEqual(1L, link.CompletedTransfers);
    }

    [TestMethod]
    public void NormalSlaveCanArmBeforeFirstClockEdgeAndBothExternalClocksStayPending()
    {
        Device first = CreateDevice();
        Device second = CreateDevice();
        using var link = new LocalSerialLink(first.SerialController, second.SerialController);
        WriteNormal(first, false, 0x35, true, start: true);
        RunPair(first, second, 7);
        WriteNormal(second, false, 0xA7, false, start: true);
        RunPair(first, second, 57);
        Assert.AreEqual(0xA7u, ReadNormal(first, false));
        Assert.AreEqual(0x35u, ReadNormal(second, false));
        WriteNormal(first, false, 0x11, false, start: true);
        WriteNormal(second, false, 0x22, false, start: true);
        RunPair(first, second, 1_000);
        AssertBusy(first, true);
        AssertBusy(second, true);
        Assert.AreEqual(8L, link.ClockEdges);
    }

    [TestMethod]
    public void NormalMidByteDisconnectPreservesReceivedPrefixAndExternalPendingState()
    {
        Device first = CreateDevice();
        Device second = CreateDevice();
        using var link = new LocalSerialLink(first.SerialController, second.SerialController);
        WriteNormal(second, false, 0xA5, false, start: true);
        WriteNormal(first, false, 0x3C, true, start: true);
        RunPair(first, second, 32);
        Assert.AreEqual(0x5, second.InspectByte(IORegs.SIODATA8) >> 4);
        link.SetConnected(false);
        RunPair(first, second, 32);
        Assert.AreEqual(0xAFu, ReadNormal(first, false));
        AssertBusy(first, false);
        AssertBusy(second, true);
        link.SetConnected(true);
        WriteNormal(first, false, 0x12, true, start: true);
        RunPair(first, second, 32);
        AssertBusy(second, false);
        Assert.AreEqual(0x31u, ReadNormal(second, false));
    }

    [TestMethod]
    public void NormalUnmatchedInternalClockCompletesAndDisconnectedInputIsHigh()
    {
        Device first = CreateDevice();
        Device second = CreateDevice();
        using var link = new LocalSerialLink(first.SerialController, second.SerialController);
        WriteNormal(first, false, 0x11, true, start: true);
        RunPair(first, second, 64);
        AssertBusy(first, false);
        Assert.AreEqual(0xFFu, ReadNormal(first, false));
        link.SetConnected(false);
        WriteNormal(second, false, 0x22, true, start: true);
        RunPair(first, second, 64);
        Assert.AreEqual(0xFFu, ReadNormal(second, false));
    }

    [TestMethod]
    public void PendingExternalNormalTransferCanBecomeClockOwnerWithoutBusyRestart()
    {
        Device first = CreateDevice();
        Device second = CreateDevice();
        using var link = new LocalSerialLink(first.SerialController, second.SerialController);
        WriteNormal(first, false, 0x35, false, fast: false, start: true);
        WriteNormal(second, false, 0xA7, false, fast: false, start: true);
        RunPair(first, second, 100);
        first.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x4083);
        RunPair(first, second, 64);
        Assert.AreEqual(0xA7u, ReadNormal(first, false));
        Assert.AreEqual(0x35u, ReadNormal(second, false));
    }

    [TestMethod]
    public void StandaloneNormalTransferStopsClockingWhenSoftwareSelectsExternalClock()
    {
        Device device = CreateDevice();
        WriteNormal(device, false, 0x35, true, start: true);
        RunCycles(device, 7);
        device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x4080);
        RunCycles(device, 1_000);
        AssertBusy(device, true);
        Assert.AreEqual(0, device.InspectHalfWord(IORegs.IF) & 0x80);
    }

    [TestMethod]
    public void NormalBothInternalClocksAreDeterministicAndCountContention()
    {
        Device first = CreateDevice();
        Device second = CreateDevice();
        using var link = new LocalSerialLink(first.SerialController, second.SerialController);
        WriteNormal(first, false, 0x35, true, start: true);
        WriteNormal(second, false, 0xA7, true, start: true);
        RunPair(first, second, 64);
        Assert.AreEqual(0xA7u, ReadNormal(first, false));
        Assert.AreEqual(0x35u, ReadNormal(second, false));
        Assert.AreEqual(8L, link.ClockEdges);
        Assert.AreEqual(8L, link.ContendedClockEdges);
    }

    [TestMethod]
    public void GpioAndUartCannotAccidentallyStartNormalTransfers()
    {
        Device device = CreateDevice();
        device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x4083);
        RunCycles(device, 1_000);
        Assert.IsFalse(device.SerialController._transferActive);
        Assert.AreEqual(0, device.InspectHalfWord(IORegs.IF) & 0x80);
        device.SerialController.WriteHalfWord(IORegs.RCNT, 0);
        device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x7083);
        RunCycles(device, 1_000);
        Assert.IsFalse(device.SerialController._transferActive);
        Assert.AreEqual(0, device.InspectHalfWord(IORegs.IF) & 0x80);
    }

    [TestMethod]
    public void NormalIdleInputReflectsPeerOutputAndModeMismatch()
    {
        Device first = CreateDevice();
        Device second = CreateDevice();
        using var link = new LocalSerialLink(first.SerialController, second.SerialController);
        WriteNormal(first, false, 0, false, start: false);
        WriteNormal(second, false, 0, false, start: false);
        Assert.AreEqual(0, first.InspectHalfWord(IORegs.SIOCNT) & 4);
        second.SerialController.WriteHalfWord(IORegs.SIOCNT, 8);
        Assert.AreEqual(4, first.InspectHalfWord(IORegs.SIOCNT) & 4);
        second.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x2000);
        Assert.AreEqual(4, first.InspectHalfWord(IORegs.SIOCNT) & 4);
    }

    [TestMethod]
    public void AttachedCaptureAndRestoreAreRejectedBeforeMutatingCpuEvenWhenUnplugged()
    {
        Device first = CreateDevice();
        Device second = CreateDevice();
        byte[] state = first.CaptureState();
        using var link = new LocalSerialLink(first.SerialController, second.SerialController);
        long cycles = first.Cpu.Cycles;
        Assert.ThrowsExactly<InvalidOperationException>(() => first.CaptureState());
        Assert.ThrowsExactly<InvalidOperationException>(() => first.RestoreState(state));
        Assert.AreEqual(cycles, first.Cpu.Cycles);
        link.SetConnected(false);
        Assert.ThrowsExactly<InvalidOperationException>(() => first.CaptureState());
        link.Dispose();
        first.RestoreState(state);
        Assert.IsFalse(first.SerialController.IsLinked);
    }

    [TestMethod]
    public void FailedDuplicateAttachmentDoesNotStealExistingCable()
    {
        Device first = CreateDevice();
        Device second = CreateDevice();
        Device third = CreateDevice();
        using var original = new LocalSerialLink(first.SerialController, second.SerialController);
        Assert.ThrowsExactly<InvalidOperationException>(() => new LocalSerialLink(third.SerialController, first.SerialController));
        Assert.IsFalse(third.SerialController.IsLinked);
        ConfigureMultiplayer(first, 1);
        ConfigureMultiplayer(second, 2);
        first.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x2083);
        RunPair(first, second, 5_755);
        Assert.AreEqual(0x0002_0001u, first.InspectWord(IORegs.SIOMULTI0));
    }

    [TestMethod]
    public void StandaloneSchemaSixPreservesAllReceiveRegistersAndPendingMultiplayer()
    {
        Device device = CreateDevice();
        ConfigureMultiplayer(device, 0xBEEF);
        device.SerialController.WriteWord(IORegs.SIOMULTI0, 0x1234_5678);
        device.SerialController.WriteWord(IORegs.SIOMULTI2, 0xFEDC_BA98);
        byte[] state = device.CaptureState();
        Assert.AreEqual((ushort)6, BinaryPrimitives.ReadUInt16LittleEndian(state.AsSpan(4)));
        device.SerialController.WriteWord(IORegs.SIOMULTI2, 0);
        device.RestoreState(state);
        Assert.AreEqual(0xFEDC_BA98u, device.InspectWord(IORegs.SIOMULTI2));
        device.SerialController.WriteHalfWord(IORegs.SIOCNT, 0x6083);
        RunCycles(device, 100);
        byte[] pending = device.CaptureState();
        long started = BinaryPrimitives.ReadInt64LittleEndian(state.AsSpan(40));
        // Snapshot aligns to an instruction boundary; use elapsed CPU time.
        int remaining = checked(3_140 - (int)(device.Cpu.Cycles - started));
        RunCycles(device, remaining);
        Assert.AreEqual(0xFFFF_BEEFu, device.InspectWord(IORegs.SIOMULTI0));
        device.RestoreState(pending);
        RunCycles(device, remaining);
        Assert.AreEqual(0xFFFF_BEEFu, device.InspectWord(IORegs.SIOMULTI0));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SchemaFiveStatesRemainReadableIncludingLegacyGpioFlagDuringTransfer(bool active)
    {
        Device device = CreateDevice();
        WriteNormal(device, false, 0x51, true, fast: false, start: active);
        device.SerialController._sioMultiHigh = 0xFEDC_BA98;
        if (active)
            RunCycles(device, 100);
        byte[] modern = device.CaptureState();
        using var serial = new MemoryStream();
        using (var writer = new BinaryWriter(serial, Encoding.UTF8, leaveOpen: true))
            device.SerialController.WriteState(writer);
        byte[] signature = serial.ToArray();
        int offset = modern.AsSpan(0, modern.Length - 32).IndexOf(signature);
        Assert.IsGreaterThan(0, offset);
        Assert.AreEqual(offset, modern.AsSpan(0, modern.Length - 32).LastIndexOf(signature));
        int removed = offset + signature.Length - 4;
        byte[] legacy = new byte[modern.Length - 4];
        modern.AsSpan(0, removed).CopyTo(legacy);
        modern.AsSpan(removed + 4, modern.Length - 32 - removed - 4).CopyTo(legacy.AsSpan(removed));
        BinaryPrimitives.WriteUInt16LittleEndian(legacy.AsSpan(4), 5);
        if (active)
            BinaryPrimitives.WriteUInt16LittleEndian(legacy.AsSpan(offset + 8), 0x8000);
        SHA256.HashData(legacy.AsSpan(0, legacy.Length - 32)).CopyTo(legacy, legacy.Length - 32);
        long remaining = 512 - device.Cpu.Cycles;
        device.SerialController.Reset();
        device.RestoreState(legacy);
        Assert.AreEqual(uint.MaxValue, device.InspectWord(IORegs.SIOMULTI2));
        Assert.AreEqual((byte)0x51, device.InspectByte(IORegs.SIODATA8));
        if (active)
        {
            RunCycles(device, checked((int)remaining - 1));
            AssertBusy(device, true);
            RunCycles(device, 1);
            AssertBusy(device, false);
            Assert.AreEqual(0xFFu, ReadNormal(device, false));
            Assert.AreEqual(0x80, device.InspectHalfWord(IORegs.IF) & 0x80);
        }
    }

    private static void ConfigureMultiplayer(Device device, ushort send, int baud = 3, bool irq = false)
    {
        device.SerialController.WriteHalfWord(IORegs.RCNT, 0);
        device.SerialController.WriteHalfWord(IORegs.SIOCNT, (ushort)(0x2000 | (irq ? 0x4000 : 0) | baud));
        device.SerialController.WriteHalfWord(IORegs.SIODATA8, send);
    }

    private static void WriteNormal(Device device, bool wordMode, uint data, bool internalClock, bool fast = true, bool start = true)
    {
        device.SerialController.WriteHalfWord(IORegs.RCNT, 0);
        device.SerialController.WriteHalfWord(IORegs.SIOCNT, (ushort)((wordMode ? 0x1000 : 0) | 0x4000));
        if (wordMode)
            device.SerialController.WriteWord(IORegs.SIODATA32, data);
        else
            device.SerialController.WriteByte(IORegs.SIODATA8, (byte)data);
        device.SerialController.WriteHalfWord(IORegs.SIOCNT, (ushort)((wordMode ? 0x1000 : 0) | 0x4000 |
            (internalClock ? 1 : 0) | (fast ? 2 : 0) | (start ? 0x80 : 0)));
    }

    private static uint ReadNormal(Device device, bool wordMode) => wordMode
        ? device.InspectWord(IORegs.SIODATA32) : device.InspectByte(IORegs.SIODATA8);
    private static void AssertBusy(Device device, bool expected) =>
        Assert.AreEqual(expected, (device.InspectHalfWord(IORegs.SIOCNT) & 0x80) != 0);

    private static Device CreateDevice()
    {
        byte[] rom = new byte[0x200];
        BinaryPrimitives.WriteUInt32LittleEndian(rom, 0xEAFF_FFFE); // ARM B to self.
        Encoding.ASCII.GetBytes("LOCAL SERIAL").CopyTo(rom, 0xA0);
        return new Device(Array.Empty<byte>(), new GamePak(rom), new TestDebugger(), skipBios: true);
    }

    private static void RunCycles(Device device, int cycles)
    {
        for (int cycle = 0; cycle < cycles; cycle++)
            device.RunCycle(skipBreakpoints: true);
    }

    private static void RunPair(Device first, Device second, int cycles)
    {
        for (int cycle = 0; cycle < cycles; cycle++)
        {
            first.RunCycle(skipBreakpoints: true);
            second.RunCycle(skipBreakpoints: true);
        }
    }
}
