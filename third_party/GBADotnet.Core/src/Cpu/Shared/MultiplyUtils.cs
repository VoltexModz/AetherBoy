namespace GameboyAdvanced.Core.Cpu.Shared;

/// <summary>
/// All the multiply operations across Arm/Thumb have aspects in common which
/// are stored here.
/// </summary>
internal static unsafe class MultiplyUtils
{

    internal static void SetupForMultiplyAccumulateFlags(Core core, int rd, int rs, int rm, int rn)
    {
        SetupForMultiplyAccumulate(core, rd, rs, rm, rn);
        core.NextExecuteAction = &MultiplyCycleWFlags;
    }

    internal static void SetupForMultiplyFlags(Core core, int rd, int rs, int rm)
    {
        SetupForMultiply(core, rd, rs, rm);
        core.NextExecuteAction = &MultiplyCycleWFlags;
    }

    internal static void SetupForMultiplyAccumulate(Core core, int rd, int rs, int rm, int rn)
    {
        SetupForMultiply(core, rd, rs, rm);
        core.InstructionState.Multiply._multiplyResult += core.R[rn];
        core.InstructionState.Multiply._requiredCycles++; // 1 extra I cycle for MLA operation
    }

    internal static int CyclesForMultiplySigned(uint operand)
    {
        if ((operand & 0xFFFF_FF00) is 0 or 0xFFFF_FF00)
        {
            return 1;
        }
        else if ((operand & 0xFFFF_0000) is 0 or 0xFFFF_0000)
        {
            return 2;
        }
        else if ((operand & 0xFF00_0000) is 0 or 0xFF00_0000)
        {
            return 3;
        }
        else
        {
            return 4;
        }
    }

    internal static int CyclesForMultiplyUnsigned(uint operand)
    {
        if ((operand & 0xFFFF_FF00) is 0)
        {
            return 1;
        }
        else if ((operand & 0xFFFF_0000) is 0)
        {
            return 2;
        }
        else if ((operand & 0xFF00_0000) is 0)
        {
            return 3;
        }
        else
        {
            return 4;
        }
    }

    internal static void SetupForMultiply(Core core, int rd, int rs, int rm)
    {
        core.SEQ = 0;
        core.nOPC = true;
        core.nMREQ = true;
        core.AIncrement = 0;
        core.InstructionState.Multiply._destinationReg = rd;
        core.InstructionState.Multiply._currentCycles = 0;
        core.InstructionState.Multiply._requiredCycles = CyclesForMultiplySigned(core.R[rs]);
        core.InstructionState.Multiply._multiplyResult = (uint)((int)core.R[rs] * (int)core.R[rm]);
        core.NextExecuteAction = &MultiplyCycle;
    }

    internal static void MultiplyCycle(Core core, uint instruction)
    {
        core.InstructionState.Multiply._currentCycles++;
        core.SEQ = 0;

        if (core.InstructionState.Multiply._currentCycles == core.InstructionState.Multiply._requiredCycles)
        {
            core.R[core.InstructionState.Multiply._destinationReg] = core.InstructionState.Multiply._multiplyResult;
            Core.ResetMemoryUnitForOpcodeFetch(core, instruction);

            if (core.InstructionState.Multiply._destinationReg == 15)
            {
                core.ClearPipeline();
            }
        }
    }

    internal static void MultiplyCycleWFlags(Core core, uint instruction)
    {
        core.InstructionState.Multiply._currentCycles++;
        core.SEQ = 0;

        if (core.InstructionState.Multiply._currentCycles == core.InstructionState.Multiply._requiredCycles)
        {
            core.R[core.InstructionState.Multiply._destinationReg] = core.InstructionState.Multiply._multiplyResult;
            ALU.SetZeroSignFlags(ref core.Cpsr, core.InstructionState.Multiply._multiplyResult);
            // TODO - The carry flag is set to a meaningless value. Ok, but what.
            Core.ResetMemoryUnitForOpcodeFetch(core, instruction);

            if (core.InstructionState.Multiply._destinationReg == 15)
            {
                core.ClearPipeline();
            }
        }
    }
}
