namespace GameboyAdvanced.Core;

public unsafe partial class Device
{
    // Device codes use the emulated bus, but must not add CPU wait cycles simply
    // because the host inspected or patched memory between emulated instructions.
    public uint ReadCheatMemory(uint address, int width)
    {
        int wait = Bus.WaitStates;
        try
        {
            if (address is >= 0x08000000 and < 0x0E000000)
            {
                uint offset = address & Gamepak.RomMask;
                if ((ulong)offset + (uint)width > (uint)Gamepak.Data.Length) return 0;
                uint value = 0;
                for (int i = 0; i < width; i++) value |= (uint)Gamepak.Data[offset + i] << (i * 8);
                return value;
            }
            return width switch { 1 => InspectByte(address), 2 => InspectHalfWord(address), 4 => InspectWord(address), _ => 0 };
        }
        finally { Bus.WaitStates = wait; }
    }

    public void WriteCheatMemory(uint address, uint value, int width)
    {
        int wait = Bus.WaitStates;
        try
        {
            switch (width)
            {
                case 1: Bus.WriteByte(address, (byte)value, 0, Cpu.R[15]); break;
                case 2: Bus.WriteHalfWord(address, (ushort)value, 0, Cpu.R[15]); break;
                case 4: Bus.WriteWord(address, value, 0, Cpu.R[15]); break;
                default: throw new ArgumentOutOfRangeException(nameof(width));
            }
        }
        finally { Bus.WaitStates = wait; }
    }
}
