using System.Buffers.Binary;
using GameboyAdvanced.Core;
using GameboyAdvanced.Core.Bus;
using GameboyAdvanced.Core.Cpu;
using GameboyAdvanced.Core.Debug;
using GameboyAdvanced.Core.Rom;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class GbaHleBootRegressionTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void HardwareIrqCallsCartridgeHandlerAndRestoresInterruptedState(bool thumb)
    {
        Device device = CreateDevice(thumb);
        uint[] handler =
        [
            0xE3A00301, // mov r0, #IO base
            0xE2800C02, // add r0, r0, #0x200
            0xE3A01001, // mov r1, #VBlank
            0xE1C010B2, // strh r1, [r0, #2]: acknowledge IF
            0xE2844001, // add r4, r4, #1: observable handler count
            0xE3A00063, 0xE3A02062, 0xE3A03061, 0xE3A0C060,
            0xE12FFF1E, // bx lr: return through the BIOS glue
        ];
        for (int i = 0; i < handler.Length; i++)
            device.PokeWord(0x03000100u + (uint)i * 4, handler[i]);
        device.PokeWord(0x03007FFC, 0x03000100);
        int[] saved = [0, 1, 2, 3, 12, 13, 14];
        uint[] expected = saved.Select(i => device.Cpu.R[i]).ToArray();
        device.Bus.WriteHalfWord(IORegs.DISPSTAT, 8, 0, 0);
        device.Bus.WriteHalfWord(IORegs.IE, 1, 0, 0);
        device.Bus.WriteHalfWord(IORegs.IME, 1, 0, 0);

        device.RunFrame();
        device.RunFrame();

        Assert.AreEqual(2u, device.Cpu.R[4]);
        CollectionAssert.AreEqual(expected, saved.Select(i => device.Cpu.R[i]).ToArray());
        Assert.AreEqual(CPSRMode.System, device.Cpu.Cpsr.Mode);
        Assert.AreEqual(thumb, device.Cpu.Cpsr.ThumbMode);
        Assert.IsFalse(device.Cpu.Cpsr.IrqDisable);
        Assert.IsTrue(device.Cpu.R[15] is >= 0x08000000 and < 0x08000020);
    }

    [TestMethod]
    public void SuppliedBiosIsNeverOverwrittenByHleGlue()
    {
        byte[] firmware = Enumerable.Repeat((byte)0x5A, 0x4000).ToArray();
        CollectionAssert.AreEqual(firmware, new Bios(firmware, skipBios: false)._bios);
        CollectionAssert.AreEqual(firmware, new Bios(firmware, skipBios: true)._bios);
    }

    [TestMethod]
    [DataRow(0x00000002u, false, false)]
    [DataRow(0x01000002u, true, false)]
    [DataRow(0x04000002u, false, true)]
    [DataRow(0x05000002u, true, true)]
    public void CpuSetDecodesSdkCopyFillAndUnitWidthFlags(uint control, bool fill, bool word)
    {
        Device device = CreateDevice();
        byte[] source = [1, 2, 3, 4, 5, 6, 7, 8];
        source.CopyTo(device.Bus.OnBoardWRam, 0);
        Array.Fill(device.Bus.OnChipWRam, (byte)0xCC, 0, 16);
        device.Cpu.R[0] = 0x02000000;
        device.Cpu.R[1] = 0x03000000;
        device.Cpu.R[2] = control;
        HleBios.TryHandleSwi(device.Cpu, 0x0B);

        int width = word ? 4 : 2;
        byte[] expected = fill ? source[..width].Concat(source[..width]).ToArray() : source[..(width * 2)];
        CollectionAssert.AreEqual(expected, device.Bus.OnChipWRam[..(width * 2)]);
        Assert.AreEqual((byte)0xCC, device.Bus.OnChipWRam[width * 2]);
    }

    [TestMethod]
    public void CpuFastSetUsesBit24ForFixedSourceAndRoundsToEightWords()
    {
        Device device = CreateDevice();
        device.PokeWord(0x02000000, 0x12345678);
        device.PokeWord(0x02000004, 0xDEADBEEF);
        device.Cpu.R[0] = 0x02000000;
        device.Cpu.R[1] = 0x03000000;
        device.Cpu.R[2] = 0x01000001;
        HleBios.TryHandleSwi(device.Cpu, 0x0C);
        for (uint i = 0; i < 8; i++)
            Assert.AreEqual(0x12345678u, device.InspectWord(0x03000000 + i * 4));
        Assert.AreEqual(0u, device.InspectWord(0x03000020));
    }

    [TestMethod]
    [DataRow((byte)0x11)]
    [DataRow((byte)0x12)]
    public void EmptyLz77AssetReturnsWithoutWritingDestination(byte service)
    {
        Device device = CreateDevice();
        device.PokeWord(0x02000000, 0x00000010);
        device.PokeWord(0x03000000, 0x12345678);
        device.Cpu.R[0] = 0x02000000;
        device.Cpu.R[1] = 0x03000000;
        Assert.IsTrue(HleBios.TryHandleSwi(device.Cpu, service));
        Assert.AreEqual(0x12345678u, device.InspectWord(0x03000000));
        Assert.AreEqual(0x03000000u, device.Cpu.R[1]);
    }

    [TestMethod]
    [DataRow((byte)0x11)]
    [DataRow((byte)0x12)]
    public void Lz77UsesSwiAlgorithmEvenWhenAssetTypeTagIsNonstandard(byte service)
    {
        Device device = CreateDevice();
        // The upper 24 bits still declare three literal output bytes.
        device.PokeWord(0x02000000, 0x0000032F);
        device.PokeWord(0x02000004, 0x43424100); // flags=0, A, B, C
        device.Cpu.R[0] = 0x02000000;
        device.Cpu.R[1] = 0x03000000;
        Assert.IsTrue(HleBios.TryHandleSwi(device.Cpu, service));
        CollectionAssert.AreEqual(new byte[] { 65, 66, 67 }, device.Bus.OnChipWRam[..3]);
    }

    [TestMethod]
    public void Lz77StillRejectsBackReferencesBeforeTheOutputBuffer()
    {
        Device device = CreateDevice();
        device.PokeWord(0x02000000, 0x0000032F);
        device.PokeWord(0x02000004, 0x00000080); // back-reference before any literal
        device.Cpu.R[0] = 0x02000000;
        device.Cpu.R[1] = 0x03000000;
        Assert.ThrowsExactly<InvalidDataException>(() => HleBios.TryHandleSwi(device.Cpu, 0x11));
    }

    private static Device CreateDevice(bool thumb = false)
    {
        byte[] rom = new byte[0x200];
        BinaryPrimitives.WriteUInt32LittleEndian(rom, 0xEAFFFFFEu);
        if (thumb)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(rom, 0xE59F0000); // ldr r0, [pc]
            BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(4), 0xE12FFF10); // bx r0
            BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(8), 0x08000011);
            BinaryPrimitives.WriteUInt16LittleEndian(rom.AsSpan(0x10), 0xE7FE); // b .
        }
        var device = new Device([], new GamePak(rom), new TestDebugger(), skipBios: true);
        for (int cycle = 0; cycle < 100; cycle++) device.RunCycle();
        return device;
    }
}
