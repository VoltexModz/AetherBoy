using System;
using System.Numerics;

namespace nanoboy.Core.Advance
{
    /// <summary>
    /// First executing ARM/Thumb interpreter slice for the own GBA core.
    /// Unsupported encodings fault explicitly. Complete instruction coverage,
    /// exceptions and exact bus timing are intentionally not claimed yet.
    /// Provenance: docs/MGBA_REVIEW.md.
    /// </summary>
    public sealed class Arm7Cpu
    {
        private readonly uint[] registers = new uint[16];
        private readonly GbaMemoryBus bus;

        public Arm7Cpu(GbaMemoryBus bus)
        {
            this.bus = bus ?? throw new ArgumentNullException(nameof(bus));
            Reset();
        }

        public uint Cpsr { get; private set; }
        public ulong ExecutedInstructions { get; private set; }
        public uint ProgramCounter => registers[15];

        public uint GetRegister(int index)
        {
            if ((uint)index >= 16) throw new ArgumentOutOfRangeException(nameof(index));
            return registers[index];
        }

        public void Reset()
        {
            Array.Clear(registers);
            registers[15] = GbaMemoryBus.RomBase;
            Cpsr = 0x000000D3; // Supervisor, IRQ/FIQ disabled, ARM state.
            ExecutedInstructions = 0;
        }

        public int Step()
        {
            if ((Cpsr & 0x20) != 0)
                return StepThumb();
            return StepArm();
        }

        private int StepArm()
        {
            uint address = registers[15] & ~3u;
            uint instruction = bus.Read32(address);
            registers[15] = address + 4;
            ExecutedInstructions++;

            if (!Arm7DataPath.ConditionPassed((int)(instruction >> 28), Arm7Flags.FromCpsr(Cpsr)))
                return 1;

            if ((instruction & 0x0FFFFFF0) == 0x012FFF10)
                return ExecuteBranchExchange(instruction, address);
            if ((instruction & 0x0E000000) == 0x0A000000)
                return ExecuteBranch(instruction, address);
            if ((instruction & 0x0C000000) == 0x04000000)
                return ExecuteSingleTransfer(instruction, address);
            if ((instruction & 0x0C000000) == 0 && (instruction & 0x00000090) != 0x00000090)
                return ExecuteDataProcessing(instruction, address);

            throw Unsupported(instruction, address);
        }

        private int ExecuteBranchExchange(uint instruction, uint address)
        {
            uint target = ReadRegister((int)(instruction & 0xF), address, false);
            if ((target & 1) != 0)
            {
                Cpsr |= 0x20;
                registers[15] = target & ~1u;
            }
            else
            {
                Cpsr &= ~0x20u;
                registers[15] = target & ~3u;
            }
            return 3;
        }

        private int ExecuteBranch(uint instruction, uint address)
        {
            if ((instruction & 0x01000000) != 0)
                registers[14] = address + 4;
            int signedOffset = ((int)(instruction << 8)) >> 6;
            registers[15] = unchecked(address + 8u + (uint)signedOffset);
            return 3;
        }

        private int ExecuteSingleTransfer(uint instruction, uint address)
        {
            bool registerOffset = (instruction & 0x02000000) != 0;
            bool preIndex = (instruction & 0x01000000) != 0;
            bool addOffset = (instruction & 0x00800000) != 0;
            bool transferByte = (instruction & 0x00400000) != 0;
            bool writeBack = (instruction & 0x00200000) != 0;
            bool load = (instruction & 0x00100000) != 0;
            int rn = (int)((instruction >> 16) & 0xF);
            int rd = (int)((instruction >> 12) & 0xF);
            uint baseAddress = ReadRegister(rn, address, false);
            uint offset;
            if (registerOffset)
            {
                if ((instruction & 0x10) != 0)
                    throw Unsupported(instruction, address);
                uint rm = ReadRegister((int)(instruction & 0xF), address, false);
                offset = Arm7DataPath.ShiftImmediate(
                    rm, (Arm7ShiftKind)((instruction >> 5) & 3),
                    (int)((instruction >> 7) & 0x1F), Arm7Flags.FromCpsr(Cpsr).Carry).Value;
            }
            else
            {
                offset = instruction & 0xFFF;
            }

            uint adjusted = addOffset ? baseAddress + offset : baseAddress - offset;
            uint transferAddress = preIndex ? adjusted : baseAddress;
            if ((!preIndex || writeBack) && load && rn == rd)
                throw new InvalidOperationException(
                    "LDR with write-back to its destination is unpredictable on ARM7.");

            if (load)
            {
                uint value = transferByte
                    ? bus.Read8(transferAddress)
                    : BitOperations.RotateRight(bus.Read32(transferAddress & ~3u), (int)(transferAddress & 3) * 8);
                WriteRegister(rd, value);
            }
            else
            {
                uint value = rd == 15 ? address + 12 : registers[rd];
                if (transferByte) bus.Write8(transferAddress, (byte)value);
                else bus.Write32(transferAddress & ~3u, value);
            }

            if (!preIndex || writeBack)
            {
                WriteRegister(rn, adjusted);
            }
            return load ? 3 : 2;
        }

        private int ExecuteDataProcessing(uint instruction, uint address)
        {
            int opcode = (int)((instruction >> 21) & 0xF);
            bool setFlags = (instruction & 0x00100000) != 0 || opcode is >= 8 and <= 11;
            int rn = (int)((instruction >> 16) & 0xF);
            int rd = (int)((instruction >> 12) & 0xF);
            Arm7Flags oldFlags = Arm7Flags.FromCpsr(Cpsr);
            Arm7ShiftResult operand2 = DecodeOperand2(instruction, address, oldFlags.Carry);
            uint left = ReadRegister(rn, address, false);
            Arm7ArithmeticResult result = opcode switch
            {
                0 => Arm7DataPath.Logical(left & operand2.Value, operand2.Carry, oldFlags.Overflow),
                1 => Arm7DataPath.Logical(left ^ operand2.Value, operand2.Carry, oldFlags.Overflow),
                2 => Arm7DataPath.Subtract(left, operand2.Value),
                3 => Arm7DataPath.Subtract(operand2.Value, left),
                4 => Arm7DataPath.Add(left, operand2.Value),
                5 => Arm7DataPath.Add(left, operand2.Value, oldFlags.Carry),
                6 => Arm7DataPath.Subtract(left, operand2.Value, oldFlags.Carry),
                7 => Arm7DataPath.Subtract(operand2.Value, left, oldFlags.Carry),
                8 => Arm7DataPath.Logical(left & operand2.Value, operand2.Carry, oldFlags.Overflow),
                9 => Arm7DataPath.Logical(left ^ operand2.Value, operand2.Carry, oldFlags.Overflow),
                10 => Arm7DataPath.Subtract(left, operand2.Value),
                11 => Arm7DataPath.Add(left, operand2.Value),
                12 => Arm7DataPath.Logical(left | operand2.Value, operand2.Carry, oldFlags.Overflow),
                13 => Arm7DataPath.Logical(operand2.Value, operand2.Carry, oldFlags.Overflow),
                14 => Arm7DataPath.Logical(left & ~operand2.Value, operand2.Carry, oldFlags.Overflow),
                15 => Arm7DataPath.Logical(~operand2.Value, operand2.Carry, oldFlags.Overflow),
                _ => throw Unsupported(instruction, address)
            };

            if (setFlags)
            {
                if (rd == 15 && opcode is not (>= 8 and <= 11))
                    throw new NotSupportedException("SPSR restore through a data-processing PC write is not implemented.");
                Cpsr = result.Flags.ApplyToCpsr(Cpsr);
            }
            if (opcode is not (>= 8 and <= 11))
                WriteRegister(rd, result.Value);
            return rd == 15 && opcode is not (>= 8 and <= 11) ? 3 : 1;
        }

        private int StepThumb()
        {
            uint address = registers[15] & ~1u;
            ushort instruction = bus.Read16(address);
            registers[15] = address + 2;
            ExecutedInstructions++;

            if ((instruction & 0xF800) == 0x1800)
                return ExecuteThumbAddSubtract(instruction);
            if ((instruction & 0xE000) == 0x0000)
                return ExecuteThumbShift(instruction);
            if ((instruction & 0xE000) == 0x2000)
                return ExecuteThumbImmediate(instruction);
            if ((instruction & 0xFC00) == 0x4000)
                return ExecuteThumbAlu(instruction);
            if ((instruction & 0xFC00) == 0x4400)
                return ExecuteThumbHighRegister(instruction, address);
            if ((instruction & 0xF800) == 0x4800)
                return ExecuteThumbPcRelativeLoad(instruction, address);
            if ((instruction & 0xF000) == 0x5000)
                return ExecuteThumbRegisterTransfer(instruction);
            if ((instruction & 0xE000) == 0x6000)
                return ExecuteThumbImmediateTransfer(instruction);
            if ((instruction & 0xF000) == 0x8000)
                return ExecuteThumbHalfwordTransfer(instruction);
            if ((instruction & 0xF000) == 0x9000)
                return ExecuteThumbStackRelativeTransfer(instruction);
            if ((instruction & 0xF000) == 0xA000)
                return ExecuteThumbLoadAddress(instruction, address);
            if ((instruction & 0xFF00) == 0xB000)
                return ExecuteThumbAdjustStack(instruction);
            if ((instruction & 0xF600) == 0xB400)
                return ExecuteThumbPushPop(instruction);
            if ((instruction & 0xF000) == 0xC000)
                return ExecuteThumbMultipleTransfer(instruction);
            if ((instruction & 0xF000) == 0xD000)
                return ExecuteThumbConditionalBranch(instruction, address);
            if ((instruction & 0xF800) == 0xE000)
                return ExecuteThumbBranch(instruction, address);
            if ((instruction & 0xF800) is 0xF000 or 0xF800)
                return ExecuteThumbLongBranch(instruction, address);

            throw UnsupportedThumb(instruction, address);
        }

        private int ExecuteThumbShift(ushort instruction)
        {
            Arm7Flags oldFlags = Arm7Flags.FromCpsr(Cpsr);
            int operation = (instruction >> 11) & 3;
            if (operation == 3)
                throw new InvalidOperationException("Thumb add/subtract reached the shift decoder.");
            Arm7ShiftResult shifted = Arm7DataPath.ShiftImmediate(
                registers[(instruction >> 3) & 7],
                (Arm7ShiftKind)operation,
                (instruction >> 6) & 0x1F,
                oldFlags.Carry);
            registers[instruction & 7] = shifted.Value;
            ApplyFlags(Arm7DataPath.Logical(shifted.Value, shifted.Carry, oldFlags.Overflow));
            return 1;
        }

        private int ExecuteThumbAddSubtract(ushort instruction)
        {
            bool immediate = (instruction & 0x0400) != 0;
            bool subtract = (instruction & 0x0200) != 0;
            uint right = immediate
                ? (uint)((instruction >> 6) & 7)
                : registers[(instruction >> 6) & 7];
            int source = (instruction >> 3) & 7;
            int destination = instruction & 7;
            Arm7ArithmeticResult result = subtract
                ? Arm7DataPath.Subtract(registers[source], right)
                : Arm7DataPath.Add(registers[source], right);
            registers[destination] = result.Value;
            ApplyFlags(result);
            return 1;
        }

        private int ExecuteThumbImmediate(ushort instruction)
        {
            int operation = (instruction >> 11) & 3;
            int destination = (instruction >> 8) & 7;
            uint immediate = (byte)instruction;
            Arm7Flags oldFlags = Arm7Flags.FromCpsr(Cpsr);
            Arm7ArithmeticResult result = operation switch
            {
                0 => Arm7DataPath.Logical(immediate, oldFlags.Carry, oldFlags.Overflow),
                1 => Arm7DataPath.Subtract(registers[destination], immediate),
                2 => Arm7DataPath.Add(registers[destination], immediate),
                3 => Arm7DataPath.Subtract(registers[destination], immediate),
                _ => throw new InvalidOperationException()
            };
            if (operation != 1)
                registers[destination] = result.Value;
            ApplyFlags(result);
            return 1;
        }

        private int ExecuteThumbAlu(ushort instruction)
        {
            int operation = (instruction >> 6) & 0xF;
            int source = (instruction >> 3) & 7;
            int destination = instruction & 7;
            uint left = registers[destination];
            uint right = registers[source];
            Arm7Flags oldFlags = Arm7Flags.FromCpsr(Cpsr);
            Arm7ArithmeticResult result;
            bool writeResult = operation is not (8 or 10 or 11);
            switch (operation)
            {
                case 0: result = Arm7DataPath.Logical(left & right, oldFlags.Carry, oldFlags.Overflow); break;
                case 1: result = Arm7DataPath.Logical(left ^ right, oldFlags.Carry, oldFlags.Overflow); break;
                case 2: result = LogicalShift(Arm7DataPath.ShiftRegister(left, Arm7ShiftKind.LogicalLeft, right, oldFlags.Carry), oldFlags); break;
                case 3: result = LogicalShift(Arm7DataPath.ShiftRegister(left, Arm7ShiftKind.LogicalRight, right, oldFlags.Carry), oldFlags); break;
                case 4: result = LogicalShift(Arm7DataPath.ShiftRegister(left, Arm7ShiftKind.ArithmeticRight, right, oldFlags.Carry), oldFlags); break;
                case 5: result = Arm7DataPath.Add(left, right, oldFlags.Carry); break;
                case 6: result = Arm7DataPath.Subtract(left, right, oldFlags.Carry); break;
                case 7: result = LogicalShift(Arm7DataPath.ShiftRegister(left, Arm7ShiftKind.RotateRight, right, oldFlags.Carry), oldFlags); break;
                case 8: result = Arm7DataPath.Logical(left & right, oldFlags.Carry, oldFlags.Overflow); break;
                case 9: result = Arm7DataPath.Subtract(0, right); break;
                case 10: result = Arm7DataPath.Subtract(left, right); break;
                case 11: result = Arm7DataPath.Add(left, right); break;
                case 12: result = Arm7DataPath.Logical(left | right, oldFlags.Carry, oldFlags.Overflow); break;
                case 13:
                    result = Arm7DataPath.Logical(unchecked(left * right), oldFlags.Carry, oldFlags.Overflow);
                    break;
                case 14: result = Arm7DataPath.Logical(left & ~right, oldFlags.Carry, oldFlags.Overflow); break;
                case 15: result = Arm7DataPath.Logical(~right, oldFlags.Carry, oldFlags.Overflow); break;
                default: throw new InvalidOperationException();
            }
            if (writeResult)
                registers[destination] = result.Value;
            ApplyFlags(result);
            return operation == 13 ? 2 : 1;
        }

        private int ExecuteThumbHighRegister(ushort instruction, uint address)
        {
            int operation = (instruction >> 8) & 3;
            int source = (instruction >> 3) & 0xF;
            int destination = (instruction & 7) | ((instruction >> 4) & 8);
            uint right = ReadThumbRegister(source, address);
            switch (operation)
            {
                case 0:
                    WriteThumbRegister(destination, ReadThumbRegister(destination, address) + right);
                    return destination == 15 ? 3 : 1;
                case 1:
                    ApplyFlags(Arm7DataPath.Subtract(ReadThumbRegister(destination, address), right));
                    return 1;
                case 2:
                    WriteThumbRegister(destination, right);
                    return destination == 15 ? 3 : 1;
                case 3:
                    if ((instruction & 0x0080) != 0)
                        throw UnsupportedThumb(instruction, address);
                    SetBranchExchangeTarget(right);
                    return 3;
                default:
                    throw new InvalidOperationException();
            }
        }

        private int ExecuteThumbPcRelativeLoad(ushort instruction, uint address)
        {
            uint loadAddress = ((address + 4) & ~3u) + ((uint)(byte)instruction << 2);
            registers[(instruction >> 8) & 7] = LoadWord(loadAddress);
            return 3;
        }

        private int ExecuteThumbRegisterTransfer(ushort instruction)
        {
            uint address = registers[(instruction >> 3) & 7] + registers[(instruction >> 6) & 7];
            int destination = instruction & 7;
            int operation = (instruction >> 10) & 3;
            if ((instruction & 0x0200) == 0)
            {
                switch (operation)
                {
                    case 0: bus.Write32(address & ~3u, registers[destination]); return 2;
                    case 1: bus.Write8(address, (byte)registers[destination]); return 2;
                    case 2: registers[destination] = LoadWord(address); return 3;
                    case 3: registers[destination] = bus.Read8(address); return 3;
                }
            }
            else
            {
                switch (operation)
                {
                    case 0: bus.Write16(address & ~1u, (ushort)registers[destination]); return 2;
                    case 1: registers[destination] = unchecked((uint)(int)(sbyte)bus.Read8(address)); return 3;
                    case 2: registers[destination] = LoadHalfword(address); return 3;
                    case 3:
                        registers[destination] = (address & 1) != 0
                            ? unchecked((uint)(int)(sbyte)bus.Read8(address))
                            : unchecked((uint)(int)(short)bus.Read16(address));
                        return 3;
                }
            }
            throw new InvalidOperationException();
        }

        private int ExecuteThumbImmediateTransfer(ushort instruction)
        {
            bool transferByte = (instruction & 0x1000) != 0;
            bool load = (instruction & 0x0800) != 0;
            uint offset = (uint)((instruction >> 6) & 0x1F);
            if (!transferByte)
                offset <<= 2;
            uint address = registers[(instruction >> 3) & 7] + offset;
            int destination = instruction & 7;
            if (load)
                registers[destination] = transferByte ? bus.Read8(address) : LoadWord(address);
            else if (transferByte)
                bus.Write8(address, (byte)registers[destination]);
            else
                bus.Write32(address & ~3u, registers[destination]);
            return load ? 3 : 2;
        }

        private int ExecuteThumbHalfwordTransfer(ushort instruction)
        {
            bool load = (instruction & 0x0800) != 0;
            uint address = registers[(instruction >> 3) & 7] +
                (uint)(((instruction >> 6) & 0x1F) << 1);
            int destination = instruction & 7;
            if (load)
                registers[destination] = LoadHalfword(address);
            else
                bus.Write16(address & ~1u, (ushort)registers[destination]);
            return load ? 3 : 2;
        }

        private int ExecuteThumbStackRelativeTransfer(ushort instruction)
        {
            bool load = (instruction & 0x0800) != 0;
            uint address = registers[13] + ((uint)(byte)instruction << 2);
            int destination = (instruction >> 8) & 7;
            if (load)
                registers[destination] = LoadWord(address);
            else
                bus.Write32(address & ~3u, registers[destination]);
            return load ? 3 : 2;
        }

        private int ExecuteThumbLoadAddress(ushort instruction, uint address)
        {
            uint source = (instruction & 0x0800) != 0
                ? registers[13]
                : (address + 4) & ~3u;
            registers[(instruction >> 8) & 7] = source + ((uint)(byte)instruction << 2);
            return 1;
        }

        private int ExecuteThumbAdjustStack(ushort instruction)
        {
            uint offset = (uint)((instruction & 0x7F) << 2);
            registers[13] = (instruction & 0x0080) != 0
                ? registers[13] - offset
                : registers[13] + offset;
            return 1;
        }

        private int ExecuteThumbPushPop(ushort instruction)
        {
            bool pop = (instruction & 0x0800) != 0;
            bool extraRegister = (instruction & 0x0100) != 0;
            int registerCount = BitOperations.PopCount((uint)(instruction & 0xFF)) +
                (extraRegister ? 1 : 0);
            if (registerCount == 0)
                throw new NotSupportedException("Empty Thumb PUSH/POP lists are not implemented.");

            if (!pop)
            {
                uint cursor = registers[13] - (uint)(registerCount * 4);
                uint start = cursor;
                for (int register = 0; register < 8; register++)
                {
                    if ((instruction & (1 << register)) == 0) continue;
                    bus.Write32(cursor, registers[register]);
                    cursor += 4;
                }
                if (extraRegister)
                    bus.Write32(cursor, registers[14]);
                registers[13] = start;
                return registerCount + 1;
            }

            uint loadCursor = registers[13];
            for (int register = 0; register < 8; register++)
            {
                if ((instruction & (1 << register)) == 0) continue;
                registers[register] = bus.Read32(loadCursor);
                loadCursor += 4;
            }
            if (extraRegister)
            {
                registers[15] = bus.Read32(loadCursor) & ~1u;
                loadCursor += 4;
            }
            registers[13] = loadCursor;
            return registerCount + (extraRegister ? 3 : 2);
        }

        private int ExecuteThumbMultipleTransfer(ushort instruction)
        {
            bool load = (instruction & 0x0800) != 0;
            int baseRegister = (instruction >> 8) & 7;
            int list = instruction & 0xFF;
            if (list == 0 || (load && (list & (1 << baseRegister)) != 0))
                throw new NotSupportedException(
                    "Empty or base-in-list Thumb multiple transfers are not implemented.");
            uint cursor = registers[baseRegister];
            for (int register = 0; register < 8; register++)
            {
                if ((list & (1 << register)) == 0) continue;
                if (load)
                    registers[register] = bus.Read32(cursor);
                else
                    bus.Write32(cursor, registers[register]);
                cursor += 4;
            }
            registers[baseRegister] = cursor;
            return BitOperations.PopCount((uint)list) + (load ? 2 : 1);
        }

        private int ExecuteThumbConditionalBranch(ushort instruction, uint address)
        {
            int condition = (instruction >> 8) & 0xF;
            if (condition >= 0xE)
                throw UnsupportedThumb(instruction, address);
            if (Arm7DataPath.ConditionPassed(condition, Arm7Flags.FromCpsr(Cpsr)))
            {
                int offset = unchecked((sbyte)instruction) << 1;
                registers[15] = unchecked(address + 4u + (uint)offset);
                return 3;
            }
            return 1;
        }

        private int ExecuteThumbBranch(ushort instruction, uint address)
        {
            int offset = ((short)(instruction << 5)) >> 4;
            registers[15] = unchecked(address + 4u + (uint)offset);
            return 3;
        }

        private int ExecuteThumbLongBranch(ushort instruction, uint address)
        {
            if ((instruction & 0x0800) == 0)
            {
                int highOffset = ((short)(instruction << 5)) << 7;
                registers[14] = unchecked(address + 4u + (uint)highOffset);
                return 1;
            }

            uint target = registers[14] + ((uint)(instruction & 0x7FF) << 1);
            registers[14] = (address + 2) | 1u;
            registers[15] = target & ~1u;
            return 3;
        }

        private Arm7ShiftResult DecodeOperand2(uint instruction, uint address, bool carry)
        {
            if ((instruction & 0x02000000) != 0)
                return Arm7DataPath.ExpandImmediate((byte)instruction, (int)((instruction >> 8) & 0xF), carry);

            bool registerShift = (instruction & 0x10) != 0;
            uint rm = ReadRegister((int)(instruction & 0xF), address, registerShift);
            Arm7ShiftKind kind = (Arm7ShiftKind)((instruction >> 5) & 3);
            return registerShift
                ? Arm7DataPath.ShiftRegister(rm, kind,
                    ReadRegister((int)((instruction >> 8) & 0xF), address, false), carry)
                : Arm7DataPath.ShiftImmediate(rm, kind, (int)((instruction >> 7) & 0x1F), carry);
        }

        private uint ReadRegister(int index, uint instructionAddress, bool registerShift) =>
            index == 15 ? instructionAddress + (registerShift ? 12u : 8u) : registers[index];

        private uint ReadThumbRegister(int index, uint instructionAddress) =>
            index == 15 ? instructionAddress + 4 : registers[index];

        private void WriteRegister(int index, uint value) =>
            registers[index] = index == 15 ? value & ~3u : value;

        private void WriteThumbRegister(int index, uint value) =>
            registers[index] = index == 15 ? value & ~1u : value;

        private void SetBranchExchangeTarget(uint target)
        {
            if ((target & 1) != 0)
            {
                Cpsr |= 0x20;
                registers[15] = target & ~1u;
            }
            else
            {
                Cpsr &= ~0x20u;
                registers[15] = target & ~3u;
            }
        }

        private uint LoadWord(uint address) =>
            BitOperations.RotateRight(bus.Read32(address & ~3u), (int)(address & 3) * 8);

        private uint LoadHalfword(uint address)
        {
            ushort value = bus.Read16(address & ~1u);
            return (address & 1) != 0
                ? (uint)((value >> 8) | (value << 8))
                : value;
        }

        private void ApplyFlags(Arm7ArithmeticResult result) =>
            Cpsr = result.Flags.ApplyToCpsr(Cpsr);

        private static Arm7ArithmeticResult LogicalShift(Arm7ShiftResult result, Arm7Flags oldFlags) =>
            Arm7DataPath.Logical(result.Value, result.Carry, oldFlags.Overflow);

        private static Exception Unsupported(uint instruction, uint address) =>
            new NotSupportedException($"Unsupported ARM instruction 0x{instruction:X8} at 0x{address:X8}.");

        private static Exception UnsupportedThumb(ushort instruction, uint address) =>
            new NotSupportedException($"Unsupported Thumb instruction 0x{instruction:X4} at 0x{address:X8}.");
    }
}
