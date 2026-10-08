using System.Buffers.Binary;
using GameboyAdvanced.Core;
using GameboyAdvanced.Core.Debug;
using GameboyAdvanced.Core.Rom;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class GbaSoundDmaTests
{
    [TestMethod]
    [DataRow(1, false)]
    [DataRow(1, true)]
    [DataRow(2, false)]
    [DataRow(2, true)]
    public void FifoBurstForcesFourWordsAndWordAlignmentRegardlessOfSizeBit(int channel, bool sizeBit)
    {
        var device = CreateDevice();
        byte[] samples = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
        samples.CopyTo(device.Bus.OnBoardWRam, 0);
        uint dma = IORegs.DMA0SAD + (uint)channel * 12;
        uint fifoAddress = channel == 1 ? IORegs.FIFO_A : IORegs.FIFO_B;
        device.Bus.WriteWord(dma, 0x02000002, 0, 0);
        device.Bus.WriteWord(dma + 4, fifoAddress, 0, 0);
        device.Bus.WriteHalfWord(dma + 8, 123, 0, 0); // ignored in FIFO mode
        device.Bus.WriteHalfWord(dma + 10, (ushort)(sizeBit ? 0xB600 : 0xB200), 0, 0);
        var fifo = device.Apu._dmaChannels[channel - 1];
        fifo.StepFifo();
        for (int i = 0; i < 200; i++) device.RunCycle();
        Assert.AreEqual(16, fifo.FifoWritePtr);
        CollectionAssert.AreEqual(samples[..16], fifo.Fifo[..16]);
        Assert.AreEqual(0x02000010u, device.DmaData.Channels[channel].IntSourceAddress);
        Assert.AreEqual(fifoAddress, device.DmaData.Channels[channel].IntDestinationAddress);
        Assert.AreEqual(sizeBit, device.DmaData.Channels[channel].ControlReg.Is32Bit, "Do not change the software-visible size bit.");
    }

    [TestMethod]
    public void FifoRequestsRefillAtSixteenRemainingBytesWithoutRestartingActiveBurst()
    {
        var device = CreateDevice();
        device.Bus.WriteWord(IORegs.DMA1SAD, 0x02000000, 0, 0);
        device.Bus.WriteWord(IORegs.DMA1DAD, IORegs.FIFO_A, 0, 0);
        device.Bus.WriteHalfWord(IORegs.DMA1CNT_H, 0xB600, 0, 0);
        var fifo = device.Apu._dmaChannels[0];
        for (int i = 0; i < 18; i++) fifo.InsertSampleByte((byte)i);
        fifo.StepFifo();
        Assert.IsFalse(device.DmaData.Channels[1].IsRunning);
        fifo.StepFifo();
        var channel = device.DmaData.Channels[1];
        Assert.IsTrue(channel.IsRunning);
        channel.ClocksToStart = 1;
        fifo.StepFifo();
        Assert.AreEqual(1, channel.ClocksToStart, "A pending FIFO request must not re-arm a running DMA.");
    }

    [TestMethod]
    public void OrdinaryHalfwordDmaStillTransfersHalfwords()
    {
        var device = CreateDevice();
        device.PokeWord(0x02000000, 0x44332211);
        device.Bus.WriteWord(IORegs.DMA1SAD, 0x02000002, 0, 0);
        device.Bus.WriteWord(IORegs.DMA1DAD, 0x03000002, 0, 0);
        device.Bus.WriteHalfWord(IORegs.DMA1CNT_L, 1, 0, 0);
        device.Bus.WriteHalfWord(IORegs.DMA1CNT_H, 0x8000, 0, 0);
        for (int i = 0; i < 100; i++) device.RunCycle();
        Assert.AreEqual(0x44330000u, device.InspectWord(0x03000000));
    }

    private static Device CreateDevice()
    {
        byte[] rom = new byte[0x200]; BinaryPrimitives.WriteUInt32LittleEndian(rom, 0xEAFFFFFE);
        return new Device([], new GamePak(rom), new TestDebugger(), skipBios: true);
    }
}
