using System;
using System.Numerics;

namespace nanoboy.Core.Advance
{
    public readonly record struct Arm7Flags(bool Negative, bool Zero, bool Carry, bool Overflow)
    {
        public static Arm7Flags FromCpsr(uint cpsr) => new(
            (cpsr & 0x80000000) != 0, (cpsr & 0x40000000) != 0,
            (cpsr & 0x20000000) != 0, (cpsr & 0x10000000) != 0);

        public uint ApplyToCpsr(uint cpsr) => (cpsr & 0x0FFFFFFF) |
            (Negative ? 0x80000000u : 0) | (Zero ? 0x40000000u : 0) |
            (Carry ? 0x20000000u : 0) | (Overflow ? 0x10000000u : 0);
    }

    public readonly record struct Arm7ArithmeticResult(uint Value, Arm7Flags Flags);
    public readonly record struct Arm7ShiftResult(uint Value, bool Carry);
    public enum Arm7ShiftKind { LogicalLeft, LogicalRight, ArithmeticRight, RotateRight }

    /// <summary>
    /// Pure ARMv4T arithmetic/operand primitives for the future GBA CPU.
    /// Newly written from documented hardware semantics, not an mGBA C translation.
    /// No instruction decoder, timing, banked registers or memory bus is implemented here.
    /// Sources and provenance: docs/MGBA_REVIEW.md.
    /// </summary>
    public static class Arm7DataPath
    {
        public static Arm7ArithmeticResult Add(uint left, uint right, bool carryIn = false)
        {
            ulong unsignedTotal = (ulong)left + right + (carryIn ? 1ul : 0ul);
            long signedTotal = (long)unchecked((int)left) + unchecked((int)right) + (carryIn ? 1 : 0);
            uint value = unchecked((uint)unsignedTotal);
            return Result(value, unsignedTotal > uint.MaxValue,
                signedTotal < int.MinValue || signedTotal > int.MaxValue);
        }

        // ARM subtraction uses C=1 for NO borrow: SBC subtracts right + (1-C).
        public static Arm7ArithmeticResult Subtract(uint left, uint right, bool carryIn = true)
        {
            ulong subtrahend = (ulong)right + (carryIn ? 0ul : 1ul);
            long signedTotal = (long)unchecked((int)left) - unchecked((int)right) - (carryIn ? 0 : 1);
            uint value = unchecked((uint)((ulong)left - subtrahend));
            return Result(value, (ulong)left >= subtrahend,
                signedTotal < int.MinValue || signedTotal > int.MaxValue);
        }

        public static Arm7ArithmeticResult Logical(uint value, bool shifterCarry, bool previousOverflow) =>
            Result(value, shifterCarry, previousOverflow);

        public static bool ConditionPassed(int conditionField, Arm7Flags flags) => conditionField switch
        {
            0x0 => flags.Zero,
            0x1 => !flags.Zero,
            0x2 => flags.Carry,
            0x3 => !flags.Carry,
            0x4 => flags.Negative,
            0x5 => !flags.Negative,
            0x6 => flags.Overflow,
            0x7 => !flags.Overflow,
            0x8 => flags.Carry && !flags.Zero,
            0x9 => !flags.Carry || flags.Zero,
            0xA => flags.Negative == flags.Overflow,
            0xB => flags.Negative != flags.Overflow,
            0xC => !flags.Zero && flags.Negative == flags.Overflow,
            0xD => flags.Zero || flags.Negative != flags.Overflow,
            0xE => true,
            0xF => false, // ARM7 NV; not the unconditional encodings of later ARM CPUs.
            _ => throw new ArgumentOutOfRangeException(nameof(conditionField))
        };

        public static Arm7ShiftResult ExpandImmediate(byte immediate, int rotationField, bool carryIn)
        {
            if (rotationField is < 0 or > 15)
                throw new ArgumentOutOfRangeException(nameof(rotationField));
            uint value = BitOperations.RotateRight((uint)immediate, rotationField * 2);
            return new(value, rotationField == 0 ? carryIn : IsNegative(value));
        }

        public static Arm7ShiftResult ShiftImmediate(
            uint value, Arm7ShiftKind kind, int amountField, bool carryIn)
        {
            ValidateKind(kind);
            if (amountField is < 0 or > 31)
                throw new ArgumentOutOfRangeException(nameof(amountField));
            if (amountField == 0)
            {
                if (kind == Arm7ShiftKind.RotateRight)
                    return new((value >> 1) | (carryIn ? 0x80000000u : 0), (value & 1) != 0);
                if (kind != Arm7ShiftKind.LogicalLeft)
                    amountField = 32;
            }
            return Shift(value, kind, amountField, carryIn);
        }

        public static Arm7ShiftResult ShiftRegister(
            uint value, Arm7ShiftKind kind, uint amountRegister, bool carryIn)
        {
            ValidateKind(kind);
            return Shift(value, kind, (int)(amountRegister & 0xFF), carryIn);
        }

        private static Arm7ShiftResult Shift(uint value, Arm7ShiftKind kind, int count, bool carryIn)
        {
            if (count == 0)
                return new(value, carryIn);

            switch (kind)
            {
                case Arm7ShiftKind.LogicalLeft:
                    return count >= 32
                        ? new(0, count == 32 && (value & 1) != 0)
                        : new(value << count, ((value >> (32 - count)) & 1) != 0);
                case Arm7ShiftKind.LogicalRight:
                    return count >= 32
                        ? new(0, count == 32 && IsNegative(value))
                        : new(value >> count, ((value >> (count - 1)) & 1) != 0);
                case Arm7ShiftKind.ArithmeticRight:
                    return new(unchecked((uint)(unchecked((int)value) >> Math.Min(count, 31))),
                        count >= 32 ? IsNegative(value) : ((value >> (count - 1)) & 1) != 0);
                case Arm7ShiftKind.RotateRight:
                    uint rotated = BitOperations.RotateRight(value, count & 31);
                    return new(rotated, IsNegative(rotated));
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static void ValidateKind(Arm7ShiftKind kind)
        {
            if (kind is < Arm7ShiftKind.LogicalLeft or > Arm7ShiftKind.RotateRight)
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        private static bool IsNegative(uint value) => (value & 0x80000000) != 0;
        private static Arm7ArithmeticResult Result(uint value, bool carry, bool overflow) =>
            new(value, new Arm7Flags(IsNegative(value), value == 0, carry, overflow));
    }
}
