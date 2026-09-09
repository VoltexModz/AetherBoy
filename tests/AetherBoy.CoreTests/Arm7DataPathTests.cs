using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core.Advance;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class Arm7DataPathTests
{
    [TestMethod]
    public void ArithmeticMatchesWideIntegerOracleIncludingCarryAndBorrowEdges()
    {
        uint[] values = { 0, 1, 2, 0x7FFFFFFF, 0x80000000, 0x80000001, 0xFFFFFFFE, uint.MaxValue };
        foreach (uint left in values)
        foreach (uint right in values)
        foreach (bool carry in new[] { false, true })
        {
            VerifyArithmetic(left, right, carry);
        }

        var random = new Random(7319);
        for (int index = 0; index < 1_000; index++)
            VerifyArithmetic((uint)random.NextInt64(1L << 32), (uint)random.NextInt64(1L << 32), index % 2 == 0);
    }

    [TestMethod]
    public void SubtractionCarryMeansNoBorrowAndLogicalOperationsKeepOverflow()
    {
        Assert.AreEqual(new Arm7ArithmeticResult(0, new(false, true, true, false)), Arm7DataPath.Subtract(7, 7));
        Assert.AreEqual(uint.MaxValue, Arm7DataPath.Subtract(7, 7, carryIn: false).Value);
        Assert.IsFalse(Arm7DataPath.Subtract(7, 7, carryIn: false).Flags.Carry);
        Assert.AreEqual(new Arm7Flags(false, true, true, true), Arm7DataPath.Logical(0, true, true).Flags);
        Assert.AreEqual(new Arm7Flags(true, false, false, false), Arm7DataPath.Logical(0x80000000, false, false).Flags);
    }

    [TestMethod]
    public void RegisterShiftsMatchSingleBitReferenceForEveryEightBitCount()
    {
        uint[] values = { 0, 1, 0x80000000, 0x80000001, uint.MaxValue, 0x617CA253 };
        foreach (uint value in values)
        foreach (Arm7ShiftKind kind in Enum.GetValues<Arm7ShiftKind>())
        foreach (bool carry in new[] { false, true })
        for (uint count = 0; count <= 255; count++)
        {
            var expected = RepeatedShift(value, kind, (int)count, carry);
            Assert.AreEqual(expected, Arm7DataPath.ShiftRegister(value, kind, count, carry),
                $"{kind}, value={value:X8}, count={count}, carry={carry}");
            Assert.AreEqual(expected, Arm7DataPath.ShiftRegister(value, kind, count | 0xA5FF0000, carry));
        }
    }

    [TestMethod]
    public void ImmediateZeroHasDistinctLslLsrAsrAndRrxSemantics()
    {
        Assert.AreEqual(new Arm7ShiftResult(0x80000001, false),
            Arm7DataPath.ShiftImmediate(0x80000001, Arm7ShiftKind.LogicalLeft, 0, false));
        Assert.AreEqual(new Arm7ShiftResult(0, true),
            Arm7DataPath.ShiftImmediate(0x80000001, Arm7ShiftKind.LogicalRight, 0, false));
        Assert.AreEqual(new Arm7ShiftResult(uint.MaxValue, true),
            Arm7DataPath.ShiftImmediate(0x80000001, Arm7ShiftKind.ArithmeticRight, 0, false));
        Assert.AreEqual(new Arm7ShiftResult(0, false),
            Arm7DataPath.ShiftImmediate(0x7FFFFFFF, Arm7ShiftKind.ArithmeticRight, 0, true));
        Assert.AreEqual(new Arm7ShiftResult(0x80000001, true),
            Arm7DataPath.ShiftImmediate(3, Arm7ShiftKind.RotateRight, 0, true));
        Assert.AreEqual(new Arm7ShiftResult(1, true),
            Arm7DataPath.ShiftImmediate(3, Arm7ShiftKind.RotateRight, 0, false));
        Assert.AreEqual(new Arm7ShiftResult(3, false),
            Arm7DataPath.ShiftRegister(3, Arm7ShiftKind.RotateRight, 0x100, false));
    }

    [TestMethod]
    public void AllNonzeroImmediateShiftsMatchSingleBitReference()
    {
        foreach (Arm7ShiftKind kind in Enum.GetValues<Arm7ShiftKind>())
        for (int count = 1; count < 32; count++)
            Assert.AreEqual(RepeatedShift(0x815A2311, kind, count, true),
                Arm7DataPath.ShiftImmediate(0x815A2311, kind, count, true));
    }

    [TestMethod]
    public void RotatedImmediateUsesEvenRotationsAndPreservesCarryOnlyAtZero()
    {
        for (int immediate = 0; immediate < 256; immediate++)
        for (int rotation = 0; rotation < 16; rotation++)
        foreach (bool carry in new[] { false, true })
        {
            Assert.AreEqual(RepeatedShift((uint)immediate, Arm7ShiftKind.RotateRight, rotation * 2, carry),
                Arm7DataPath.ExpandImmediate((byte)immediate, rotation, carry));
        }
    }

    [TestMethod]
    public void AllConditionsMatchSignedAndUnsignedComparisons()
    {
        // Every N/Z/C/V combination is checked against the architectural truth table.
        for (uint bits = 0; bits < 16; bits++)
        {
            Arm7Flags flags = Arm7Flags.FromCpsr(bits << 28);
            bool[] pairs = { flags.Zero, flags.Carry, flags.Negative, flags.Overflow,
                flags.Carry && !flags.Zero, flags.Negative == flags.Overflow,
                !flags.Zero && flags.Negative == flags.Overflow, true };
            for (int group = 0; group < 8; group++)
            {
                Assert.AreEqual(pairs[group], Arm7DataPath.ConditionPassed(group * 2, flags));
                Assert.AreEqual(!pairs[group], Arm7DataPath.ConditionPassed(group * 2 + 1, flags));
            }
        }

        foreach (uint left in new uint[] { 0, 1, 0x7FFFFFFF, 0x80000000, uint.MaxValue })
        foreach (uint right in new uint[] { 0, 1, 0x7FFFFFFF, 0x80000000, uint.MaxValue })
        {
            Arm7Flags comparison = Arm7DataPath.Subtract(left, right).Flags;
            Assert.AreEqual(left > right, Arm7DataPath.ConditionPassed(8, comparison));
            Assert.AreEqual(unchecked((int)left) > unchecked((int)right), Arm7DataPath.ConditionPassed(12, comparison));
        }
    }

    [TestMethod]
    public void FlagsRoundTripWithoutChangingModeInterruptOrThumbBits()
    {
        for (uint bits = 0; bits < 16; bits++)
        {
            var flags = Arm7Flags.FromCpsr(bits << 28);
            uint updated = flags.ApplyToCpsr(0xFA1234F3);
            Assert.AreEqual(0x0A1234F3u, updated & 0x0FFFFFFF);
            Assert.AreEqual(flags, Arm7Flags.FromCpsr(updated));
        }
    }

    [TestMethod]
    public void RejectsInvalidDecodedFieldsInsteadOfMaskingThemSilently()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Arm7DataPath.ConditionPassed(16, default));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Arm7DataPath.ConditionPassed(-1, default));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Arm7DataPath.ExpandImmediate(1, 16, false));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Arm7DataPath.ShiftImmediate(1, Arm7ShiftKind.LogicalLeft, 32, false));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Arm7DataPath.ShiftRegister(1, (Arm7ShiftKind)4, 0, false));
    }

    private static void VerifyArithmetic(uint left, uint right, bool carry)
    {
        BigInteger modulus = BigInteger.One << 32;
        BigInteger sum = (BigInteger)left + right + (carry ? 1 : 0);
        BigInteger signedSum = (BigInteger)unchecked((int)left) + unchecked((int)right) + (carry ? 1 : 0);
        var add = Arm7DataPath.Add(left, right, carry);
        Check(add, sum, signedSum, sum >= modulus);

        BigInteger difference = (BigInteger)left - right - (carry ? 0 : 1);
        BigInteger signedDifference = (BigInteger)unchecked((int)left) - unchecked((int)right) - (carry ? 0 : 1);
        Check(Arm7DataPath.Subtract(left, right, carry), difference, signedDifference, difference >= 0);

        void Check(Arm7ArithmeticResult actual, BigInteger wide, BigInteger signed, bool expectedCarry)
        {
            uint expected = (uint)((wide % modulus + modulus) % modulus);
            Assert.AreEqual(expected, actual.Value);
            Assert.AreEqual(expected == 0, actual.Flags.Zero);
            Assert.AreEqual(expected >= 0x80000000, actual.Flags.Negative);
            Assert.AreEqual(expectedCarry, actual.Flags.Carry);
            Assert.AreEqual(signed < int.MinValue || signed > int.MaxValue, actual.Flags.Overflow);
        }
    }

    private static Arm7ShiftResult RepeatedShift(uint value, Arm7ShiftKind kind, int count, bool carry)
    {
        for (int step = 0; step < count; step++)
        {
            bool high = (value & 0x80000000) != 0;
            bool low = (value & 1) != 0;
            if (kind == Arm7ShiftKind.LogicalLeft)
            {
                carry = high;
                value = unchecked(value * 2);
            }
            else
            {
                carry = low;
                value /= 2;
                if ((kind == Arm7ShiftKind.ArithmeticRight && high) ||
                    (kind == Arm7ShiftKind.RotateRight && low))
                    value += 0x80000000;
            }
        }
        return new(value, carry);
    }
}
