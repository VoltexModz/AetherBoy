using GameboyAdvanced.Core.Bus;
using static GameboyAdvanced.Core.IORegs;

namespace GameboyAdvanced.Core.Cpu;

/// <summary>
/// High-level implementations of documented GBA BIOS services. They are used
/// only when no 16 KiB BIOS image was supplied; a real BIOS always keeps the
/// normal exception-vector path. The routines deliberately operate through
/// the emulated bus so RAM mirroring and MMIO side effects remain consistent.
/// </summary>
internal static class HleBios
{
    private const int MaximumDecompressedLength = 0x0100_0000;
    private const uint BiosChecksum = 0xBAAE_187F;

    internal static bool TryHandleSwi(Core core, byte service)
    {
        core.Diagnostics.RecordOnce(
            core.Cycles,
            GameboyAdvanced.Core.Debug.GbaDiagnosticCategory.BiosCall,
            $"HLE BIOS service 0x{service:X2}",
            core.Pipeline.CurrentInstructionAddress);
        int savedWaitStates = core.Bus.WaitStates;
        uint savedPrefetchAddress = core.Bus._prefetcher._internalAddressRegister;
        bool savedPrefetchActive = core.Bus._prefetcher._active;
        uint savedPrefetchBase = core.Bus._prefetcher._currentPreFetchBase;
        long savedPrefetchCycle = core.Bus._prefetcher._cycleNextRequestStart;
        try
        {
            switch (service)
            {
                case 0x00:
                    SoftReset(core);
                    return true;
                case 0x01:
                    RegisterRamReset(core);
                    return true;
                case 0x02:
                    core.Bus.HaltMode = HaltMode.Halt;
                    return true;
                case 0x03:
                    core.Bus.HaltMode = HaltMode.Stop;
                    return true;
                case 0x04:
                    InterruptWait(core, forceVBlank: false);
                    return true;
                case 0x05:
                    InterruptWait(core, forceVBlank: true);
                    return true;
                case 0x06:
                    Divide(core, unchecked((int)core.R[0]), unchecked((int)core.R[1]));
                    return true;
                case 0x07:
                    Divide(core, unchecked((int)core.R[1]), unchecked((int)core.R[0]));
                    return true;
                case 0x08:
                    core.R[0] = IntegerSquareRoot(core.R[0]);
                    return true;
                case 0x09:
                    core.R[0] = unchecked((uint)(short)ArcTan(unchecked((int)core.R[0])));
                    return true;
                case 0x0A:
                    core.R[0] = ArcTan2(unchecked((int)core.R[0]), unchecked((int)core.R[1]));
                    core.R[3] = 0x170;
                    return true;
                case 0x0B:
                    CpuSet(core, fast: false);
                    return true;
                case 0x0C:
                    CpuSet(core, fast: true);
                    return true;
                case 0x0D:
                    core.R[0] = BiosChecksum;
                    core.R[1] = 1;
                    core.R[3] = 0x4000;
                    return true;
                case 0x0E:
                    BackgroundAffineSet(core);
                    return true;
                case 0x0F:
                    ObjectAffineSet(core);
                    return true;
                case 0x10:
                    BitUnpack(core);
                    return true;
                case 0x11:
                    Lz77Uncompress(core, halfWordOutput: false);
                    return true;
                case 0x12:
                    Lz77Uncompress(core, halfWordOutput: true);
                    return true;
                case 0x13:
                    HuffmanUncompress(core);
                    return true;
                case 0x14:
                    RunLengthUncompress(core, halfWordOutput: false);
                    return true;
                case 0x15:
                    RunLengthUncompress(core, halfWordOutput: true);
                    return true;
                case 0x16:
                    DifferentialUnfilter(core, inputWidth: 1, halfWordOutput: false);
                    return true;
                case 0x17:
                    DifferentialUnfilter(core, inputWidth: 1, halfWordOutput: true);
                    return true;
                case 0x18:
                    DifferentialUnfilter(core, inputWidth: 2, halfWordOutput: true);
                    return true;
                case 0x19:
                    core.Bus._apu.WriteHalfWord(SOUNDBIAS, core.R[0] == 0 ? (ushort)0 : (ushort)0x200);
                    return true;
                case 0x1F:
                    MidiKeyToFrequency(core);
                    return true;
                default:
                    return false;
            }
        }
        finally
        {
            // HLE is currently atomic. Do not leak the many bus accesses as a
            // single enormous wait-state stall into the following instruction.
            core.Bus.WaitStates = savedWaitStates;
            if (service != 0x00)
            {
                core.Bus._prefetcher._internalAddressRegister = savedPrefetchAddress;
                core.Bus._prefetcher._active = savedPrefetchActive;
                core.Bus._prefetcher._currentPreFetchBase = savedPrefetchBase;
                core.Bus._prefetcher._cycleNextRequestStart = savedPrefetchCycle;
            }
        }
    }

    private static void SoftReset(Core core)
    {
        bool bootFromWorkRam = core.Bus.OnChipWRam[0x7FFA] != 0;
        Array.Clear(core.Bus.OnChipWRam, 0x7E00, 0x200);
        core.Reset(skipBios: true);
        core.R[15] = bootFromWorkRam ? 0x0200_0000u : 0x0800_0000u;
        core.ClearPipeline();
    }

    private static void RegisterRamReset(Core core)
    {
        byte areas = (byte)core.R[0];
        core.Bus._ppu.WriteRegisterHalfWord(DISPCNT, 0x0080);
        if ((areas & 0x01) != 0)
            Array.Clear(core.Bus.OnBoardWRam);
        if ((areas & 0x02) != 0)
            Array.Clear(core.Bus.OnChipWRam, 0, core.Bus.OnChipWRam.Length - 0x200);
        if ((areas & 0x04) != 0)
        {
            for (uint address = 0x0500_0000; address < 0x0500_0400; address += 2)
                core.Bus._ppu.WriteHalfWord(address, 0);
        }
        if ((areas & 0x08) != 0)
            Array.Clear(core.Bus._ppu.Vram);
        if ((areas & 0x10) != 0)
        {
            for (uint address = 0x0700_0000; address < 0x0700_0400; address += 2)
                core.Bus._ppu.WriteHalfWord(address, 0);
        }
        if ((areas & 0x20) != 0)
            core.Bus._serialController.Reset();
        if ((areas & 0x40) != 0)
            core.Bus._apu.Reset();
        if ((areas & 0x80) != 0)
        {
            for (uint address = 0x0400_0000; address <= 0x0400_0056; address += 2)
                core.Bus._ppu.WriteRegisterHalfWord(address, 0);
            core.Bus._ppu.WriteRegisterHalfWord(DISPCNT, 0x0080);
            core.Bus._ppu.WriteRegisterHalfWord(BG2PA, 0x0100);
            core.Bus._ppu.WriteRegisterHalfWord(BG2PD, 0x0100);
            core.Bus._ppu.WriteRegisterHalfWord(BG3PA, 0x0100);
            core.Bus._ppu.WriteRegisterHalfWord(BG3PD, 0x0100);
            core.Bus._dma.Reset();
            core.Bus._timerController.Reset();
            core.Bus._interruptRegisters.Reset();
            core.Bus._waitControl.Reset();
            core.Bus._prefetcher.Reset();
        }
    }

    private static void InterruptWait(Core core, bool forceVBlank)
    {
        ushort mask = forceVBlank ? (ushort)1 : (ushort)core.R[1];
        bool discardPending = forceVBlank || core.R[0] != 0;
        ushort pending = (ushort)(core.Bus._interruptRegisters._interruptRequest.Get() & mask);
        if (discardPending && pending != 0)
        {
            core.Bus._interruptRegisters.WriteHalfWord(IF, pending);
            pending = 0;
        }
        if (pending == 0)
            core.Bus.HaltMode = HaltMode.Halt;
    }

    private static void Divide(Core core, int numerator, int denominator)
    {
        int quotient;
        int remainder;
        if (denominator == 0)
        {
            quotient = numerator < 0 ? -1 : 1;
            remainder = numerator;
        }
        else if (numerator == int.MinValue && denominator == -1)
        {
            quotient = int.MinValue;
            remainder = 0;
        }
        else
        {
            quotient = numerator / denominator;
            remainder = numerator % denominator;
        }
        core.R[0] = unchecked((uint)quotient);
        core.R[1] = unchecked((uint)remainder);
        core.R[3] = quotient == int.MinValue ? 0x8000_0000u : (uint)Math.Abs(quotient);
    }

    private static uint IntegerSquareRoot(uint value)
    {
        uint result = (uint)Math.Sqrt(value);
        while ((ulong)(result + 1) * (result + 1) <= value)
            result++;
        while ((ulong)result * result > value)
            result--;
        return result;
    }

    private static short ArcTan(int tangent) =>
        (short)Math.Round(Math.Atan(tangent / 16384.0) * 32768.0 / Math.PI);

    private static ushort ArcTan2(int x, int y)
    {
        double turns = Math.Atan2(y, x) / (Math.PI * 2.0);
        if (turns < 0)
            turns += 1.0;
        return (ushort)Math.Round(turns * 65536.0);
    }

    private static void CpuSet(Core core, bool fast)
    {
        uint source = core.R[0];
        uint destination = core.R[1];
        uint control = core.R[2];
        int count = (int)(control & 0x1F_FFFF);
        // BIOS control: bit 24 selects fixed-source fill; bit 26 selects
        // 32-bit units (CpuFastSet always transfers words).
        bool fill = (control & (1u << 24)) != 0;
        bool word = fast || (control & (1u << 26)) != 0;
        int width = word ? 4 : 2;
        source &= word ? 0xFFFF_FFFCu : 0xFFFF_FFFEu;
        destination &= word ? 0xFFFF_FFFCu : 0xFFFF_FFFEu;
        if (fast)
            count = (count + 7) & ~7;
        if (count <= 0)
            return;

        uint fillValue = word ? Read32(core, source) : Read16(core, source);
        for (int unit = 0; unit < count; unit++)
        {
            uint value = fill ? fillValue : word
                ? Read32(core, source + (uint)(unit * width))
                : Read16(core, source + (uint)(unit * width));
            if (word)
                Write32(core, destination + (uint)(unit * width), value);
            else
                Write16(core, destination + (uint)(unit * width), (ushort)value);
        }
    }

    private static void BackgroundAffineSet(Core core)
    {
        uint source = core.R[0];
        uint destination = core.R[1];
        int count = checked((int)Math.Min(core.R[2], 0x10000));
        for (int item = 0; item < count; item++, source += 20, destination += 16)
        {
            double originX = unchecked((int)Read32(core, source)) / 256.0;
            double originY = unchecked((int)Read32(core, source + 4)) / 256.0;
            short centerX = unchecked((short)Read16(core, source + 8));
            short centerY = unchecked((short)Read16(core, source + 10));
            double scaleX = unchecked((short)Read16(core, source + 12)) / 256.0;
            double scaleY = unchecked((short)Read16(core, source + 14)) / 256.0;
            double angle = (Read16(core, source + 16) >> 8) * Math.PI / 128.0;
            double cosine = Math.Cos(angle);
            double sine = Math.Sin(angle);
            double a = cosine * scaleX;
            double b = -sine * scaleX;
            double c = sine * scaleY;
            double d = cosine * scaleY;
            Write16(core, destination, unchecked((ushort)(short)(a * 256.0)));
            Write16(core, destination + 2, unchecked((ushort)(short)(b * 256.0)));
            Write16(core, destination + 4, unchecked((ushort)(short)(c * 256.0)));
            Write16(core, destination + 6, unchecked((ushort)(short)(d * 256.0)));
            Write32(core, destination + 8, unchecked((uint)(int)((originX - a * centerX - b * centerY) * 256.0)));
            Write32(core, destination + 12, unchecked((uint)(int)((originY - c * centerX - d * centerY) * 256.0)));
        }
    }

    private static void ObjectAffineSet(Core core)
    {
        uint source = core.R[0];
        uint destination = core.R[1];
        int count = checked((int)Math.Min(core.R[2], 0x10000));
        uint stride = core.R[3];
        for (int item = 0; item < count; item++, source += 8, destination += stride * 4)
        {
            double scaleX = unchecked((short)Read16(core, source)) / 256.0;
            double scaleY = unchecked((short)Read16(core, source + 2)) / 256.0;
            double angle = (Read16(core, source + 4) >> 8) * Math.PI / 128.0;
            double cosine = Math.Cos(angle);
            double sine = Math.Sin(angle);
            Write16(core, destination, unchecked((ushort)(short)(cosine * scaleX * 256.0)));
            Write16(core, destination + stride, unchecked((ushort)(short)(-sine * scaleX * 256.0)));
            Write16(core, destination + stride * 2, unchecked((ushort)(short)(sine * scaleY * 256.0)));
            Write16(core, destination + stride * 3, unchecked((ushort)(short)(cosine * scaleY * 256.0)));
        }
    }

    private static void BitUnpack(Core core)
    {
        uint source = core.R[0];
        uint destination = core.R[1] & 0xFFFF_FFFCu;
        uint info = core.R[2];
        int sourceLength = Read16(core, info);
        int sourceWidth = Read8(core, info + 2);
        int destinationWidth = Read8(core, info + 3);
        if (sourceWidth is not (1 or 2 or 4 or 8) ||
            destinationWidth is not (1 or 2 or 4 or 8 or 16 or 32))
            return;
        uint biasControl = Read32(core, info + 4);
        uint bias = biasControl & 0x7FFF_FFFF;
        bool biasZero = (biasControl & 0x8000_0000) != 0;
        uint sourceMask = (1u << sourceWidth) - 1;
        uint destinationMask = destinationWidth == 32
            ? uint.MaxValue
            : (1u << destinationWidth) - 1;
        uint output = 0;
        int outputBits = 0;
        for (int byteIndex = 0; byteIndex < sourceLength; byteIndex++)
        {
            uint packed = Read8(core, source++);
            for (int inputBits = 0; inputBits < 8; inputBits += sourceWidth)
            {
                uint value = packed & sourceMask;
                packed >>= sourceWidth;
                if (value != 0 || biasZero)
                    value += bias;
                output |= (value & destinationMask) << outputBits;
                outputBits += destinationWidth;
                if (outputBits == 32)
                {
                    Write32(core, destination, output);
                    destination += 4;
                    output = 0;
                    outputBits = 0;
                }
            }
        }
        if (outputBits != 0)
            Write32(core, destination, output);
        core.R[0] = source;
        core.R[1] = destination;
    }

    private static void Lz77Uncompress(Core core, bool halfWordOutput)
    {
        uint source = core.R[0] & 0xFFFF_FFFCu;
        uint header = Read32(core, source);
        // SWI 0x11/0x12 select LZ77 themselves. Hardware consumes the upper
        // 24-bit length and does not require the advisory low-byte type tag.
        // Some ROM hacks use a nonstandard tag for otherwise valid streams.
        int length = GetDecompressedLength(header);
        source += 4;
        byte[] output = new byte[length];
        int written = 0;
        while (written < length)
        {
            byte flags = Read8(core, source++);
            for (int bit = 7; bit >= 0 && written < length; bit--)
            {
                if ((flags & (1 << bit)) == 0)
                {
                    output[written++] = Read8(core, source++);
                    continue;
                }
                int pair = (Read8(core, source) << 8) | Read8(core, source + 1);
                source += 2;
                int count = (pair >> 12) + 3;
                int displacement = (pair & 0x0FFF) + 1;
                if (displacement > written)
                    throw new InvalidDataException("The GBA LZ77 stream references data before its output buffer.");
                for (int copy = 0; copy < count && written < length; copy++)
                {
                    output[written] = output[written - displacement];
                    written++;
                }
            }
        }
        WriteOutput(core, core.R[1], output, halfWordOutput);
        core.R[0] = source;
        core.R[1] += (uint)length;
        core.R[3] = 0;
    }

    private static void HuffmanUncompress(Core core)
    {
        uint source = core.R[0] & 0xFFFF_FFFCu;
        uint header = Read32(core, source);
        int length = ValidateCompressedHeader(header, 0x20);
        int symbolBits = (int)(header & 0xF);
        if (symbolBits is not (4 or 8))
            return;
        int treeLength = Read8(core, source + 4) * 2 + 1;
        uint treeBase = source + 5;
        source = treeBase + (uint)treeLength;
        byte[] output = new byte[length];
        int outputByte = 0;
        int outputBit = 0;
        uint nodeAddress = treeBase;
        uint bitWord = 0;
        int bitsRemaining = 0;
        int guard = checked(length * (symbolBits == 4 ? 16 : 8) + 1024);
        while (outputByte < length && guard-- > 0)
        {
            if (bitsRemaining == 0)
            {
                bitWord = Read32(core, source);
                source += 4;
                bitsRemaining = 32;
            }
            bool right = (bitWord & 0x8000_0000) != 0;
            bitWord <<= 1;
            bitsRemaining--;
            byte node = Read8(core, nodeAddress);
            uint child = (nodeAddress & 0xFFFF_FFFEu) + (uint)((node & 0x3F) * 2 + 2);
            bool terminal = right ? (node & 0x40) != 0 : (node & 0x80) != 0;
            child += right ? 1u : 0u;
            if (!terminal)
            {
                nodeAddress = child;
                continue;
            }
            byte symbol = Read8(core, child);
            output[outputByte] |= (byte)((symbol & ((1 << symbolBits) - 1)) << outputBit);
            outputBit += symbolBits;
            if (outputBit == 8)
            {
                outputBit = 0;
                outputByte++;
            }
            nodeAddress = treeBase;
        }
        if (outputByte != length)
            throw new InvalidDataException("The GBA Huffman stream did not produce its declared output.");
        WriteOutput(core, core.R[1], output, halfWordOutput: true);
        core.R[0] = source;
        core.R[1] += (uint)length;
    }

    private static void RunLengthUncompress(Core core, bool halfWordOutput)
    {
        uint source = core.R[0] & 0xFFFF_FFFCu;
        int length = ValidateCompressedHeader(Read32(core, source), 0x30);
        source += 4;
        byte[] output = new byte[length];
        int written = 0;
        while (written < length)
        {
            byte control = Read8(core, source++);
            if ((control & 0x80) != 0)
            {
                int count = (control & 0x7F) + 3;
                byte value = Read8(core, source++);
                while (count-- > 0 && written < length)
                    output[written++] = value;
            }
            else
            {
                int count = control + 1;
                while (count-- > 0 && written < length)
                    output[written++] = Read8(core, source++);
            }
        }
        WriteOutput(core, core.R[1], output, halfWordOutput);
        core.R[0] = source;
        core.R[1] += (uint)length;
    }

    private static void DifferentialUnfilter(Core core, int inputWidth, bool halfWordOutput)
    {
        uint source = core.R[0] & 0xFFFF_FFFCu;
        int length = ValidateCompressedHeader(Read32(core, source), 0x80);
        source += 4;
        byte[] output = new byte[length];
        if (inputWidth == 1)
        {
            byte previous = 0;
            for (int index = 0; index < length; index++)
            {
                previous = unchecked((byte)(previous + Read8(core, source++)));
                output[index] = previous;
            }
        }
        else
        {
            ushort previous = 0;
            for (int index = 0; index + 1 < length; index += 2, source += 2)
            {
                previous = unchecked((ushort)(previous + Read16(core, source)));
                output[index] = (byte)previous;
                output[index + 1] = (byte)(previous >> 8);
            }
        }
        WriteOutput(core, core.R[1], output, halfWordOutput);
        core.R[0] = source;
        core.R[1] += (uint)length;
    }

    private static int ValidateCompressedHeader(uint header, byte expectedType)
    {
        if ((header & 0xF0) != expectedType)
            throw new InvalidDataException($"The GBA BIOS stream has an invalid 0x{expectedType:X2} header.");
        return GetDecompressedLength(header);
    }

    private static int GetDecompressedLength(uint header)
    {
        int length = (int)(header >> 8);
        // Empty assets (header 0x00000010, for example) are legal no-ops.
        if (length > MaximumDecompressedLength)
            throw new InvalidDataException("The GBA BIOS stream has an invalid output length.");
        return length;
    }

    private static void WriteOutput(Core core, uint destination, ReadOnlySpan<byte> output, bool halfWordOutput)
    {
        if (!halfWordOutput)
        {
            for (int index = 0; index < output.Length; index++)
                Write8(core, destination + (uint)index, output[index]);
            return;
        }
        destination &= 0xFFFF_FFFEu;
        for (int index = 0; index < output.Length; index += 2)
        {
            ushort value = output[index];
            if (index + 1 < output.Length)
                value |= (ushort)(output[index + 1] << 8);
            Write16(core, destination + (uint)index, value);
        }
    }

    private static void MidiKeyToFrequency(Core core)
    {
        uint baseFrequency = Read32(core, core.R[0] + 4);
        double exponent = (180.0 - core.R[1] - core.R[2] / 256.0) / 12.0;
        core.R[0] = (uint)(baseFrequency / Math.Pow(2.0, exponent));
    }

    private static byte Read8(Core core, uint address) =>
        core.Bus.ReadByte(address, 0, core.R[15], core.D, core.Cycles, false);

    private static ushort Read16(Core core, uint address) =>
        core.Bus.ReadHalfWord(address, 0, core.R[15], core.D, core.Cycles, false);

    private static uint Read32(Core core, uint address) =>
        core.Bus.ReadWord(address, 0, core.R[15], core.D, core.Cycles, false);

    private static void Write8(Core core, uint address, byte value) =>
        core.Bus.WriteByte(address, value, 0, core.R[15]);

    private static void Write16(Core core, uint address, ushort value) =>
        core.Bus.WriteHalfWord(address, value, 0, core.R[15]);

    private static void Write32(Core core, uint address, uint value) =>
        core.Bus.WriteWord(address, value, 0, core.R[15]);
}
