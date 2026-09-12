using System.Buffers.Binary;
using GameboyAdvanced.Core;
using GameboyAdvanced.Core.Debug;
using GameboyAdvanced.Core.Rom;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class GbaCpuInstanceIsolationTests
{
    private const int Cycles = 1_000;

    [TestMethod]
    [DataRow("arm_load_store", false)]
    [DataRow("arm_load_store", true)]
    [DataRow("arm_halfword_signed", false)]
    [DataRow("arm_halfword_signed", true)]
    [DataRow("arm_ldm_stm", false)]
    [DataRow("arm_ldm_stm", true)]
    [DataRow("arm_mul_mla", false)]
    [DataRow("arm_mul_mla", true)]
    [DataRow("arm_long_multiply", false)]
    [DataRow("arm_long_multiply", true)]
    [DataRow("arm_swap", false)]
    [DataRow("arm_swap", true)]
    [DataRow("arm_branch_link", false)]
    [DataRow("arm_branch_link", true)]
    [DataRow("thumb_alu", false)]
    [DataRow("thumb_alu", true)]
    [DataRow("thumb_load_store", false)]
    [DataRow("thumb_load_store", true)]
    [DataRow("thumb_push_pop", false)]
    [DataRow("thumb_push_pop", true)]
    [DataRow("thumb_branch_link", false)]
    [DataRow("thumb_branch_link", true)]
    public void InterleavedCpuFamiliesMatchEachIndependentStandaloneMachine(string family, bool secondFirst)
    {
        // The two CPUs execute actual distinct operand values, not just register
        // writes from a test host. Shared multi-cycle scratch would corrupt the
        // first CPU as soon as the second one decodes or loads its own operand.
        Device expectedFirst = CreateDevice(family, 0);
        Run(expectedFirst, Cycles);
        AssertReachedEnd(expectedFirst, family);
        byte[] firstState = expectedFirst.CaptureState();
        Device expectedSecond = CreateDevice(family, 1);
        Run(expectedSecond, Cycles);
        AssertReachedEnd(expectedSecond, family);
        byte[] secondState = expectedSecond.CaptureState();

        Device first = CreateDevice(family, 0);
        Device second = CreateDevice(family, 1);
        Assert.AreNotSame(first.Cpu.InstructionState, second.Cpu.InstructionState);
        for (int cycle = 0; cycle < Cycles; cycle++)
        {
            (secondFirst ? second : first).RunCycle(skipBreakpoints: true);
            (secondFirst ? first : second).RunCycle(skipBreakpoints: true);
        }
        AssertReachedEnd(first, family);
        AssertReachedEnd(second, family);
        AssertState(firstState, first.CaptureState(), family + " P1");
        AssertState(secondState, second.CaptureState(), family + " P2");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ResettingOrRestoringAnotherCpuCannotAlterAnInFlightMultipleTransfer(bool restore)
    {
        Device expected = CreateDevice("arm_ldm_stm", 0);
        Run(expected, Cycles);
        byte[] expectedState = expected.CaptureState();
        Device actual = CreateDevice("arm_ldm_stm", 0);
        Device peer = CreateDevice("arm_ldm_stm", 1);
        byte[] peerState = peer.CaptureState();
        bool actedDuringTransfer = false;
        for (int cycle = 0; cycle < Cycles; cycle++)
        {
            actual.RunCycle(skipBreakpoints: true);
            var active = actual.Cpu.InstructionState.LdmStm;
            if (!actedDuringTransfer && active._storeLoadMultiplePtr > 0 &&
                active._storeLoadMultiplePtr < active._storeLoadMultiplePopCount)
            {
                if (restore) peer.RestoreState(peerState);
                else peer.Reset(skipBios: true);
                actedDuringTransfer = true;
            }
        }
        Assert.IsTrue(actedDuringTransfer, "The test must reset/restore a peer during an actual multi-register instruction.");
        AssertReachedEnd(actual, "arm_ldm_stm");
        AssertState(expectedState, actual.CaptureState(), restore ? "peer restore" : "peer reset");
    }

    private static Device CreateDevice(string family, int player)
    {
        byte[] rom = new byte[0x200];
        bool thumb = family.StartsWith("thumb_", StringComparison.Ordinal);
        if (thumb)
        {
            uint[] enter = [0xE59FC000, 0xE12FFF1C, 0x08000021]; // LDR r12; BX r12; Thumb address.
            for (int index = 0; index < enter.Length; index++)
                BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(index * 4), enter[index]);
            ushort[] code = ThumbProgram(family);
            for (int index = 0; index < code.Length; index++)
                BinaryPrimitives.WriteUInt16LittleEndian(rom.AsSpan(0x20 + index * 2), code[index]);
        }
        else
        {
            uint[] code = ArmProgram(family, player);
            for (int index = 0; index < code.Length; index++)
                BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(index * 4), code[index]);
        }
        var device = new Device([], new GamePak(rom), new TestDebugger(), skipBios: true);
        uint seed = player == 0 ? 0x9234_5678u : 0xE5A6_B7C8u;
        for (int register = 0; register < 13; register++)
            device.Cpu.R[register] = seed ^ ((uint)register * 0x0101_0101u);
        device.Cpu.R[0] = 0x0200_0000;
        device.Cpu.R[2] = thumb ? 0x0200_0000u : 0x0200_0100u;
        if (thumb) device.Cpu.R[1] = (uint)(player + 3);
        device.Cpu.R[13] = 0x0300_7000;
        for (int index = 0; index < 512; index++)
            device.Bus.OnBoardWRam[index] = (byte)(index * 17 + (player == 0 ? 0x31 : 0xAB));
        return device;
    }

    private static uint[] ArmProgram(string family, int player)
    {
        uint[] operations = family switch
        {
            "arm_load_store" => [0xE5A01004, 0xE4903004, 0xE5D04000, 0xE5C01001],
            "arm_halfword_signed" => [0xE1D030B0, 0xE1D040D1, 0xE1D050F2, 0xE1C010B4],
            "arm_ldm_stm" => [0xE8A0001E, 0xE2400010, 0xE8B001E0, 0xE8A201E0],
            "arm_mul_mla" => [0xE0030291, 0xE0243291],
            "arm_long_multiply" => [0xE0843291, 0xE0A43291, 0xE0C43291, 0xE0E43291],
            "arm_swap" => player == 0 ? [0xE1003091, 0xE1404091] : [0xE1403091, 0xE1004091],
            "arm_branch_link" => [],
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };
        if (family == "arm_branch_link")
            return [0xEB000001, 0xE3A0A05A, 0xEAFFFFFE, 0xE2800001, 0xE12FFF1E];
        return [.. operations, 0xE3A0A05A, 0xEAFFFFFE]; // MOV r10,#0x5A; B .
    }

    private static ushort[] ThumbProgram(string family)
    {
        ushort[] operations = family switch
        {
            "thumb_alu" => [0x4088, 0x40CA, 0x410B, 0x41CC, 0x4375], // Register shifts, ROR, MUL.
            "thumb_load_store" => [0x6813, 0x6054, 0x8855, 0x5656],
            "thumb_push_pop" => [0xB50F, 0x2000, 0x2100, 0xBC0F, 0xBC10],
            "thumb_branch_link" => [],
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };
        if (family == "thumb_branch_link")
            return [0xF000, 0xF802, 0x275A, 0xE7FE, 0x3001, 0x4770];
        return [.. operations, 0x275A, 0xE7FE]; // MOV r7,#0x5A; B .
    }

    private static void Run(Device device, int cycles)
    {
        for (int cycle = 0; cycle < cycles; cycle++) device.RunCycle(skipBreakpoints: true);
    }

    private static void AssertReachedEnd(Device device, string family) =>
        Assert.AreEqual(0x5Au, device.Cpu.R[family.StartsWith("thumb_", StringComparison.Ordinal) ? 7 : 10],
            $"The {family} program must reach its actual CPU completion marker; PC={device.Cpu.R[15]:X8}.");

    private static void AssertState(byte[] expected, byte[] actual, string context)
    {
        if (expected.AsSpan().SequenceEqual(actual)) return;
        int firstDifference = 0;
        while (firstDifference < Math.Min(expected.Length, actual.Length) && expected[firstDifference] == actual[firstDifference])
            firstDifference++;
        Assert.Fail($"{context}: interleaved machine differs from standalone state at byte {firstDifference}; expected length {expected.Length}, actual length {actual.Length}.");
    }
}
