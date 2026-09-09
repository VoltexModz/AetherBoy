using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core.Advance;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class GbaExecutionPrototypeTests
{
    [TestMethod]
    public void GeneratedArmRomExecutesThroughBusAndPublishesMode3Frame()
    {
        var gba = new GbaSystem(CreateSolidRedProgram());
        gba.Frame();

        Assert.AreEqual(1L, gba.FrameCount);
        Assert.IsTrue(gba.Cpu.ExecutedInstructions > 20_000);
        Assert.AreEqual((ushort)0x0403, gba.Bus.Read16(GbaMemoryBus.IoBase));
        Assert.AreEqual((uint)0x001F001F, gba.Bus.Read32(GbaMemoryBus.VramBase));

        int[] frame = new int[GbaVideo.FramePixelCount];
        long sequence = 0;
        Assert.IsTrue(gba.Video.TryCopyPublishedFrame(frame, ref sequence));
        Assert.IsTrue(frame.All(pixel => pixel == unchecked((int)0xFFFF0000)));
        Assert.IsFalse(gba.Video.TryCopyPublishedFrame(frame, ref sequence));
    }

    [TestMethod]
    public void ResetReturnsToRomEntryWithoutClearingCartridgeOrVideoMemory()
    {
        var gba = new GbaSystem(CreateSolidRedProgram());
        gba.Frame();
        gba.Reset();
        Assert.AreEqual(GbaMemoryBus.RomBase, gba.Cpu.ProgramCounter);
        Assert.AreEqual(0ul, gba.Cpu.ExecutedInstructions);
        Assert.AreEqual(0L, gba.FrameCount);
        Assert.AreEqual((uint)0x001F001F, gba.Bus.Read32(GbaMemoryBus.VramBase));
    }

    [TestMethod]
    public void BusImplementsCoreRegionsAndDocumentedVramMirror()
    {
        var bus = new GbaMemoryBus(new byte[0xC0]);
        bus.Write32(0x02000000, 0x12345678);
        Assert.AreEqual((uint)0x12345678, bus.Read32(0x02040000));
        bus.Write16(0x03000000, 0xABCD);
        Assert.AreEqual((ushort)0xABCD, bus.Read16(0x03008000));
        bus.Write16(0x06010000, 0x35A7);
        Assert.AreEqual((ushort)0x35A7, bus.Read16(0x06018000));
        bus.Write8(0x0E000000, 0x5A);
        Assert.AreEqual((byte)0x5A, bus.Read8(0x0F010000));
        Assert.AreEqual((byte)0, bus.Read8(0x01000000));
    }

    [TestMethod]
    public void UnsupportedInstructionReportsOpcodeAndAddress()
    {
        byte[] rom = new byte[0xC0];
        Write32(rom, 0, 0xEF000000); // SWI 0; BIOS HLE/exception entry not implemented yet.
        var gba = new GbaSystem(rom);
        NotSupportedException exception = Assert.ThrowsExactly<NotSupportedException>(() => gba.Cpu.Step());
        StringAssert.Contains(exception.Message, "0xEF000000");
        StringAssert.Contains(exception.Message, "0x08000000");
    }

    [TestMethod]
    public void BranchLinkAndConditionExecutionUseArmPipelineAddress()
    {
        byte[] rom = new byte[0xC0];
        Write32(rom, 0, 0xEB000000); // BL to current instruction + 8.
        Write32(rom, 8, 0xE3A0202A); // MOV r2,#42.
        var gba = new GbaSystem(rom);
        Assert.AreEqual(3, gba.Cpu.Step());
        Assert.AreEqual(GbaMemoryBus.RomBase + 8, gba.Cpu.ProgramCounter);
        Assert.AreEqual(GbaMemoryBus.RomBase + 4, gba.Cpu.GetRegister(14));
        gba.Cpu.Step();
        Assert.AreEqual((uint)42, gba.Cpu.GetRegister(2));
    }

    [TestMethod]
    public void ArmBranchExchangeEntersThumbAndExecutesImmediateArithmetic()
    {
        byte[] rom = CreateThumbProgram(0x212A, 0x3101, 0x292B); // MOV r1,#42; ADD #1; CMP #43.
        var gba = new GbaSystem(rom);

        for (int index = 0; index < 5; index++)
            gba.Cpu.Step();

        Assert.AreEqual((uint)43, gba.Cpu.GetRegister(1));
        Assert.AreNotEqual(0u, gba.Cpu.Cpsr & 0x20); // Thumb state.
        Assert.AreNotEqual(0u, gba.Cpu.Cpsr & 0x40000000); // Zero after CMP.
        Assert.AreEqual(GbaMemoryBus.RomBase + 0x26, gba.Cpu.ProgramCounter);
    }

    [TestMethod]
    public void ThumbPcRelativeLoadsAndHalfwordStoreReachIo()
    {
        byte[] rom = CreateThumbProgram(
            0x4807, // LDR r0,[pc,#28] -> 0x08000040.
            0x4908, // LDR r1,[pc,#32] -> 0x08000044.
            0x8001, // STRH r1,[r0].
            0xE7FE); // B to itself.
        Write32(rom, 0x40, GbaMemoryBus.IoBase);
        Write32(rom, 0x44, 0x00000403);
        var gba = new GbaSystem(rom);

        for (int index = 0; index < 5; index++)
            gba.Cpu.Step();

        Assert.AreEqual((ushort)0x0403, gba.Bus.DisplayControl);
    }

    [TestMethod]
    public void ThumbPushAndPopRoundTripRegistersAndStackPointer()
    {
        byte[] rom = CreateThumbProgram(
            0x4807, // LDR r0,[pc,#28] -> stack top.
            0x4685, // MOV sp,r0.
            0x2112, // MOV r1,#0x12.
            0xB502, // PUSH {r1,lr}.
            0x2100, // MOV r1,#0.
            0xBC06); // POP {r1,r2}.
        Write32(rom, 0x40, 0x03008000);
        var gba = new GbaSystem(rom);

        for (int index = 0; index < 8; index++)
            gba.Cpu.Step();

        Assert.AreEqual((uint)0x12, gba.Cpu.GetRegister(1));
        Assert.AreEqual(0u, gba.Cpu.GetRegister(2));
        Assert.AreEqual((uint)0x03008000, gba.Cpu.GetRegister(13));
    }

    [TestMethod]
    public void ThumbConditionalBranchUsesCpsrFlags()
    {
        byte[] rom = CreateThumbProgram(
            0x2001, // MOV r0,#1.
            0x2801, // CMP r0,#1.
            0xD001, // BEQ 0x0800002A.
            0x2111, // skipped.
            0x46C0, // NOP (MOV r8,r8).
            0x2122); // MOV r1,#0x22.
        var gba = new GbaSystem(rom);

        for (int index = 0; index < 6; index++)
            gba.Cpu.Step();

        Assert.AreEqual((uint)0x22, gba.Cpu.GetRegister(1));
        Assert.AreEqual(GbaMemoryBus.RomBase + 0x2C, gba.Cpu.ProgramCounter);
    }

    [TestMethod]
    public void ThumbByteTransfersUseMappedMemory()
    {
        byte[] rom = CreateThumbProgram(
            0x4807, // LDR r0,[pc,#28] -> EWRAM.
            0x217F, // MOV r1,#0x7F.
            0x70C1, // STRB r1,[r0,#3].
            0x78C2); // LDRB r2,[r0,#3].
        Write32(rom, 0x40, 0x02000000);
        var gba = new GbaSystem(rom);

        for (int index = 0; index < 6; index++)
            gba.Cpu.Step();

        Assert.AreEqual((byte)0x7F, gba.Bus.Read8(0x02000003));
        Assert.AreEqual((uint)0x7F, gba.Cpu.GetRegister(2));
    }

    [TestMethod]
    public void UnsupportedThumbSoftwareInterruptReportsOpcodeAndAddress()
    {
        var gba = new GbaSystem(CreateThumbProgram(0xDF00)); // SWI 0; BIOS HLE is still absent.
        gba.Cpu.Step();
        gba.Cpu.Step();

        NotSupportedException exception =
            Assert.ThrowsExactly<NotSupportedException>(() => gba.Cpu.Step());
        StringAssert.Contains(exception.Message, "0xDF00");
        StringAssert.Contains(exception.Message, "0x08000020");
    }

    private static byte[] CreateSolidRedProgram()
    {
        byte[] rom = new byte[0xC0];
        uint[] program =
        {
            0xE59F0018, // LDR r0,[pc,#24] -> DISPCNT
            0xE59F1018, // LDR r1,[pc,#24] -> mode 3 + BG2
            0xE5801000, // STR r1,[r0]
            0xE59F0014, // LDR r0,[pc,#20] -> VRAM
            0xE59F1014, // LDR r1,[pc,#20] -> two red pixels
            0xE5801000, // loop: STR r1,[r0]
            0xE2800004, // ADD r0,r0,#4
            0xEAFFFFFC, // B loop
            GbaMemoryBus.IoBase,
            0x00000403,
            GbaMemoryBus.VramBase,
            0x001F001F
        };
        for (int index = 0; index < program.Length; index++)
            Write32(rom, index * 4, program[index]);

        // Synthetic metadata only. No Nintendo logo, BIOS, or game content.
        "AETHER ARM"u8.CopyTo(rom.AsSpan(0xA0));
        "AABE00"u8.CopyTo(rom.AsSpan(0xAC));
        rom[0xB2] = 0x96;
        byte checksum = 0;
        for (int index = 0xA0; index <= 0xBC; index++)
            checksum = unchecked((byte)(checksum - rom[index]));
        rom[0xBD] = unchecked((byte)(checksum - 0x19));
        return rom;
    }

    private static byte[] CreateThumbProgram(params ushort[] instructions)
    {
        byte[] rom = new byte[0xC0];
        Write32(rom, 0, 0xE59F0008); // LDR r0,[pc,#8].
        Write32(rom, 4, 0xE12FFF10); // BX r0.
        Write32(rom, 0x10, GbaMemoryBus.RomBase + 0x21);
        for (int index = 0; index < instructions.Length; index++)
            Write16(rom, 0x20 + index * 2, instructions[index]);
        return rom;
    }

    private static void Write16(byte[] destination, int offset, ushort value)
    {
        destination[offset] = (byte)value;
        destination[offset + 1] = (byte)(value >> 8);
    }

    private static void Write32(byte[] destination, int offset, uint value)
    {
        destination[offset] = (byte)value;
        destination[offset + 1] = (byte)(value >> 8);
        destination[offset + 2] = (byte)(value >> 16);
        destination[offset + 3] = (byte)(value >> 24);
    }
}
