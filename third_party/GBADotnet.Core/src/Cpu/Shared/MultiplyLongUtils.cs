namespace GameboyAdvanced.Core.Cpu.Shared;

/// <summary>
/// Multiply long is obviously very similar to multiply but is split into its
/// own utils class here because the way that register writeback works is
/// sufficiently different as to get clearer code with duplication than
/// branching.
/// </summary>
internal static unsafe class MultiplyLongUtils
{

    internal static void SetupForSignedMultiplyAccumulateLongFlags(Core core, uint rdHi, uint rdLo, uint rs, uint rm)
    {
        SetupForMultiplyLongCommon(core, rdHi, rdLo);
        var longAccumulator = (long)(((ulong)core.R[rdHi] << 32) | core.R[rdLo]);
        core.InstructionState.MultiplyLong._requiredCycles = MultiplyUtils.CyclesForMultiplySigned(core.R[rs]) + 2;
        core.InstructionState.MultiplyLong._multiplyResult = (ulong)(((long)(int)core.R[rs] * (long)(int)core.R[rm]) + longAccumulator);
        core.NextExecuteAction = &MultiplyCycleWFlags;
    }

    internal static void SetupForSignedMultiplyLongFlags(Core core, uint rdHi, uint rdLo, uint rs, uint rm)
    {
        SetupForMultiplyLongCommon(core, rdHi, rdLo);
        core.InstructionState.MultiplyLong._requiredCycles = MultiplyUtils.CyclesForMultiplySigned(core.R[rs]) + 1;
        core.InstructionState.MultiplyLong._multiplyResult = (ulong)((long)(int)core.R[rs] * (long)(int)core.R[rm]);
        core.NextExecuteAction = &MultiplyCycleWFlags;
    }

    internal static void SetupForSignedMultiplyAccumulateLong(Core core, uint rdHi, uint rdLo, uint rs, uint rm)
    {
        SetupForMultiplyLongCommon(core, rdHi, rdLo);
        var longAccumulator = (long)(((ulong)core.R[rdHi] << 32) | core.R[rdLo]);
        core.InstructionState.MultiplyLong._requiredCycles = MultiplyUtils.CyclesForMultiplySigned(core.R[rs]) + 2;
        core.InstructionState.MultiplyLong._multiplyResult = (ulong)(((long)(int)core.R[rs] * (long)(int)core.R[rm]) + longAccumulator);
        core.NextExecuteAction = &MultiplyCycle;
    }

    internal static void SetupForSignedMultiplyLong(Core core, uint rdHi, uint rdLo, uint rs, uint rm)
    {
        SetupForMultiplyLongCommon(core, rdHi, rdLo);
        core.InstructionState.MultiplyLong._requiredCycles = MultiplyUtils.CyclesForMultiplySigned(core.R[rs]) + 1;
        core.InstructionState.MultiplyLong._multiplyResult = (ulong)((long)(int)core.R[rs] * (long)(int)core.R[rm]);
        core.NextExecuteAction = &MultiplyCycle;
    }

    internal static void SetupForMultiplyLongAccumulateFlags(Core core, uint rdHi, uint rdLo, uint rs, uint rm)
    {
        SetupForMultiplyLongCommon(core, rdHi, rdLo);
        var longAccumulator = ((ulong)core.R[rdHi] << 32) | core.R[rdLo];
        core.InstructionState.MultiplyLong._multiplyResult = (core.R[rs] * (ulong)core.R[rm]) + longAccumulator;
        core.InstructionState.MultiplyLong._requiredCycles = MultiplyUtils.CyclesForMultiplyUnsigned(core.R[rs]) + 2;
        core.NextExecuteAction = &MultiplyCycleWFlags;
    }

    internal static void SetupForMultiplyLongFlags(Core core, uint rdHi, uint rdLo, uint rs, uint rm)
    {
        SetupForMultiplyLongCommon(core, rdHi, rdLo);
        core.InstructionState.MultiplyLong._requiredCycles = MultiplyUtils.CyclesForMultiplyUnsigned(core.R[rs]) + 1;
        core.InstructionState.MultiplyLong._multiplyResult = core.R[rs] * (ulong)core.R[rm];
        core.NextExecuteAction = &MultiplyCycleWFlags;
    }

    internal static void SetupForMultiplyLongAccumulate(Core core, uint rdHi, uint rdLo, uint rs, uint rm)
    {
        SetupForMultiplyLongCommon(core, rdHi, rdLo);
        var longAccumulator = ((ulong)core.R[rdHi] << 32) | core.R[rdLo];
        core.InstructionState.MultiplyLong._multiplyResult = (core.R[rs] * (ulong)core.R[rm]) + longAccumulator;
        core.InstructionState.MultiplyLong._requiredCycles = MultiplyUtils.CyclesForMultiplyUnsigned(core.R[rs]) + 2;
        core.NextExecuteAction = &MultiplyCycle;
    }

    internal static void SetupForMultiplyLong(Core core, uint rdHi, uint rdLo, uint rs, uint rm)
    {
        SetupForMultiplyLongCommon(core, rdHi, rdLo);
        core.InstructionState.MultiplyLong._requiredCycles = MultiplyUtils.CyclesForMultiplyUnsigned(core.R[rs]) + 1;
        core.InstructionState.MultiplyLong._multiplyResult = core.R[rs] * (ulong)core.R[rm];
        core.NextExecuteAction = &MultiplyCycle;
    }

    private static void SetupForMultiplyLongCommon(Core core, uint rdHi, uint rdLo)
    {
        core.SEQ = 0;
        core.nOPC = true;
        core.nMREQ = true;
        core.AIncrement = 0;
        core.InstructionState.MultiplyLong._destinationRegHi = rdHi;
        core.InstructionState.MultiplyLong._destinationRegLo = rdLo;
        core.InstructionState.MultiplyLong._currentCycles = 0;
    }

    internal static void MultiplyCycle(Core core, uint instruction)
    {
        core.InstructionState.MultiplyLong._currentCycles++;
        core.SEQ = 0;

        if (core.InstructionState.MultiplyLong._currentCycles == core.InstructionState.MultiplyLong._requiredCycles)
        {
            core.R[core.InstructionState.MultiplyLong._destinationRegHi] = (uint)(core.InstructionState.MultiplyLong._multiplyResult >> 32);
            core.R[core.InstructionState.MultiplyLong._destinationRegLo] = (uint)core.InstructionState.MultiplyLong._multiplyResult;
            Core.ResetMemoryUnitForOpcodeFetch(core, instruction);

            if (core.InstructionState.MultiplyLong._destinationRegHi == 15 || core.InstructionState.MultiplyLong._destinationRegLo == 15)
            {
                core.ClearPipeline();
            }
        }
    }

    internal static void MultiplyCycleWFlags(Core core, uint instruction)
    {
        core.InstructionState.MultiplyLong._currentCycles++;
        core.SEQ = 0;

        if (core.InstructionState.MultiplyLong._currentCycles == core.InstructionState.MultiplyLong._requiredCycles)
        {
            core.R[core.InstructionState.MultiplyLong._destinationRegHi] = (uint)(core.InstructionState.MultiplyLong._multiplyResult >> 32);
            core.R[core.InstructionState.MultiplyLong._destinationRegLo] = (uint)core.InstructionState.MultiplyLong._multiplyResult;
            ALU.SetZeroSignFlags(ref core.Cpsr, core.InstructionState.MultiplyLong._multiplyResult);
            // TODO - The carry/overflow flags are set to a meaningless value. Ok, but what.
            Core.ResetMemoryUnitForOpcodeFetch(core, instruction);

            if (core.InstructionState.MultiplyLong._destinationRegHi == 15 || core.InstructionState.MultiplyLong._destinationRegLo == 15)
            {
                core.ClearPipeline();
            }
        }
    }
}
