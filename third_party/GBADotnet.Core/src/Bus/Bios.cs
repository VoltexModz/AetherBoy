using System.Runtime.CompilerServices;
using System.Buffers.Binary;

namespace GameboyAdvanced.Core.Bus;

public class Bios
{
    public readonly byte[] _bios = new byte[0x4000];

    /// <summary>
    /// Bios reads when R15>=0x4000 don't go through normal open bus but 
    /// instead take the most recently latched value from the BIOS area.
    /// 
    /// No idea how that works on hardware though, is there a dedicated 
    /// line for bios?
    /// </summary>
    public uint _latchedValue;

    public Bios(byte[] bios, bool skipBios)
    {
        if (bios == null || bios.Length > _bios.Length) throw new ArgumentException($"Bios is invalid length {bios?.Length}", nameof(bios));
        Array.Fill<byte>(_bios, 0);
        Array.Copy(bios, 0, _bios, 0, Math.Min(_bios.Length, bios.Length));

        if (skipBios && bios.Length == 0)
        {
            InstallHleInterruptVector();
        }

        // When we're skpping the bios we need to set up the initial latch value
        // for bios open bus
        if (skipBios)
        {
            _latchedValue = 0xE129F000;
        }
    }

    private void InstallHleInterruptVector()
    {
        // Original ARM glue for the documented GBA IRQ calling convention:
        // save the volatile registers on SP_irq, call the cartridge handler
        // at the IWRAM mirror 0x03FFFFFC, then restore CPSR and the interrupted PC.
        // SWI HLE alone is insufficient: the CPU still enters vector 0x18 for
        // hardware IRQs. Leaving it zero made games execute empty BIOS memory.
        // Contract: https://gbadev.net/tonc/interrupts.html#the-interrupt-process
        // These instructions are assembled here, not copied from a BIOS image.
        ReadOnlySpan<uint> instructions =
        [
            0xE92D500F, // 18: stmdb sp!, {r0-r3,r12,lr}
            0xE3A00301, // 1c: mov r0, #0x04000000
            0xE28FE000, // 20: add lr, pc, #0 (return at 0x28)
            0xE510F004, // 24: ldr pc, [r0, #-4]
            0xE8BD500F, // 28: ldmia sp!, {r0-r3,r12,lr}
            0xE25EF004, // 2c: subs pc, lr, #4
        ];
        for (int index = 0; index < instructions.Length; index++)
            BinaryPrimitives.WriteUInt32LittleEndian(_bios.AsSpan(0x18 + index * 4), instructions[index]);
    }

    internal void Reset(bool skipBios)
    {
        _latchedValue = skipBios ? 0xE129F000 : 0x0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal byte ReadByte(uint address, uint r15)
    {
        if (r15 <= 0x3FFF)
        {
            _latchedValue = _bios[address];
            return (byte)_latchedValue;
        }
        
        var rotate = (address & 0b11) * 8;
        return (byte)(_latchedValue >> (int)rotate);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ushort ReadHalfWord(uint address, uint r15)
    {
        if (r15 <= 0x3FFF)
        {
            _latchedValue = Utils.ReadHalfWord(_bios, address & 0x3FFE, 0x3FFF);
            return (ushort)_latchedValue;
        }

        if ((address & 0b11) > 1)
        {
            return (ushort)((_latchedValue >> 16));
        }

        return (ushort)_latchedValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal uint ReadWord(uint address, uint r15)
    {
        if (r15 <= 0x3FFF)
        {
            _latchedValue = Utils.ReadWord(_bios, address & 0x3FFC, 0x3FFF);
            return _latchedValue;
        }

        return _latchedValue;
    }
}
