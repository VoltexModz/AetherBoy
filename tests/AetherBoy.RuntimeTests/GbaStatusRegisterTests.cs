using System.Buffers.Binary;
using GameboyAdvanced.Core;
using GameboyAdvanced.Core.Debug;
using GameboyAdvanced.Core.Rom;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class GbaStatusRegisterTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void FlagsOnlyWritePreservesInterruptMasksAndSavedControl(bool immediate, bool saved)
    {
        // MSR CPSR_f/SPSR_f, r0 or #0xA0000000. No cartridge/BIOS required.
        uint opcode = immediate ? 0xE328F20A : 0xE128F000;
        if (saved) opcode |= 0x00400000;
        var device = CreateDevice(opcode);
        device.Cpu.SwitchMode(CPSRMode.Irq);
        device.Cpu.Cpsr.IrqDisable = true;
        device.Cpu.Cpsr.FiqDisable = true;
        device.Cpu.Spsr[4].Set(0x500000F3);
        device.Cpu.Spsr[4].Mode = CPSRMode.Supervisor;
        device.Cpu.Spsr[4].ThumbMode = true;
        device.Cpu.R[0] = 0xA0000000;
        uint before = saved ? device.Cpu.Spsr[4].Get() : device.Cpu.Cpsr.Get();
        RunFirst(device);
        uint after = saved ? device.Cpu.Spsr[4].Get() : device.Cpu.Cpsr.Get();
        Assert.AreEqual((before & 0x0FFFFFFF) | 0xA0000000, after);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ControlOnlyWritePreservesFlagsAndSwitchesRegisterBank(bool immediate)
    {
        var device = CreateDevice(immediate ? 0xE321F0D2u : 0xE121F000u);
        device.Cpu.Cpsr.Set(0xB000001F);
        device.Cpu.R[13] = 0x03007000;
        device.Cpu.R[14] = 0x08000101;
        device.Cpu.R[0] = 0xD2;
        RunFirst(device);
        Assert.AreEqual(0xB00000D2u, device.Cpu.Cpsr.Get());
        device.Cpu.SwitchMode(CPSRMode.System);
        Assert.AreEqual(0x03007000u, device.Cpu.R[13]);
        Assert.AreEqual(0x08000101u, device.Cpu.R[14]);
    }

    [TestMethod]
    [DataRow(0xE129F000u)] // user-mode CPSR_fc cannot alter control
    [DataRow(0xE128F000u)]
    public void UserModeWriteCannotEnableIrqsOrChangeMode(uint opcode)
    {
        var device = CreateDevice(opcode);
        device.Cpu.SwitchMode(CPSRMode.User);
        device.Cpu.Cpsr.IrqDisable = true;
        device.Cpu.R[0] = 0xA0000012;
        RunFirst(device);
        Assert.AreEqual(0xA0000090u, device.Cpu.Cpsr.Get());
    }

    [TestMethod]
    [DataRow(0xE120F000u)] // no fields
    [DataRow(0xE122F000u)] // reserved extension field
    [DataRow(0xE124F000u)] // reserved status field
    public void UnselectedFieldsAreUnchanged(uint opcode)
    {
        var device = CreateDevice(opcode);
        device.Cpu.Cpsr.Set(0xB00000DF);
        device.Cpu.R[0] = 0;
        RunFirst(device);
        Assert.AreEqual(0xB00000DFu, device.Cpu.Cpsr.Get());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SavedControlWritePreservesFlagsAndRecordsThumbForExceptionReturn(bool immediate)
    {
        var device = CreateDevice(immediate ? 0xE361F0F3u : 0xE161F000u);
        device.Cpu.SwitchMode(CPSRMode.Irq);
        device.Cpu.Spsr[4].Set(0x9000001F);
        device.Cpu.Spsr[4].Mode = CPSRMode.System;
        device.Cpu.R[0] = 0xF3;
        RunFirst(device);
        Assert.AreEqual(0x900000F3u, device.Cpu.Spsr[4].Get());
        Assert.AreEqual(CPSRMode.Irq, device.Cpu.Cpsr.Mode);
        Assert.IsFalse(device.Cpu.Cpsr.ThumbMode);
    }

    [TestMethod]
    public void FlagsWriteInsideIrqDoesNotPermitNestedInterruptOrLoseReturnAddress()
    {
        var device = CreateDevice(0xEAFFFFFE);
        uint[] handler =
        [
            0xE10F2000, // mrs r2, cpsr
            0xE328F20A, // msr cpsr_f, #0xA0000000: must retain IRQ disable
            0xE10F3000, // mrs r3, cpsr
            0xE2033080, // and r3, r3, #0x80
            0xE1844003, // orr r4, r4, r3: publish I bit seen inside IRQ
            0xE2855001, // add r5, r5, #1: handler invocation count
            0xE3A00301, 0xE2800C02, 0xE3A01001, 0xE1C010B2, // ack VBlank
            0xE12FFF1E,
        ];
        for (int i = 0; i < handler.Length; i++)
            device.PokeWord(0x03000100u + (uint)i * 4, handler[i]);
        device.PokeWord(0x03007FFC, 0x03000100);
        device.Bus.WriteHalfWord(IORegs.DISPSTAT, 8, 0, 0);
        device.Bus.WriteHalfWord(IORegs.IE, 1, 0, 0);
        device.Bus.WriteHalfWord(IORegs.IME, 1, 0, 0);
        device.RunFrame(); device.RunFrame();
        Assert.AreEqual(0x80u, device.Cpu.R[4]);
        Assert.AreEqual(2u, device.Cpu.R[5]);
        Assert.AreEqual(CPSRMode.System, device.Cpu.Cpsr.Mode);
        Assert.IsFalse(device.Cpu.Cpsr.IrqDisable);
        Assert.IsTrue(device.Cpu.R[15] is >= 0x08000000 and < 0x08000010);
    }

    private static Device CreateDevice(uint opcode)
    {
        byte[] rom = new byte[0x200];
        BinaryPrimitives.WriteUInt32LittleEndian(rom, opcode);
        BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(4), 0xEAFFFFFE);
        return new Device([], new GamePak(rom), new TestDebugger(), skipBios: true);
    }

    private static void RunFirst(Device device)
    {
        bool complete = false;
        device.Cpu.InstructionStarting = (address, _) => complete |= address == 0x08000004;
        for (int i = 0; i < 100 && !complete; i++) device.RunCycle();
        Assert.IsTrue(complete, "The instruction must return to the following instruction.");
    }
}
