namespace GameboyAdvanced.Core.Cpu.Shared;

/// <summary>
/// LDM/STM and the PUSH/POP variants on the Thumb instruction set have a
/// _lot_ in common so to avoid duplicating rather complicated code this
/// class contains the common cycle functions; each core owns its state.
/// </summary>
internal static class LdmStmUtils
{
    /// <summary>
    /// Whilst a STM/LDM operation is going on we need to track which register
    /// is going to be stored/loaded on the next cycle.
    ///
    /// Each core records how far through a
    /// stm/ldm instruction we've got.
    /// </summary>

    internal static void Reset(Core core)
    {
        core.InstructionState.LdmStm._storeLoadMultiplePopCount = 0;
        core.InstructionState.LdmStm._storeLoadMultiplePtr = 0;
        core.InstructionState.LdmStm._storeLoadMultipleDoWriteback = false;
        core.InstructionState.LdmStm._useBank0Regs = false;
    }

    /// <summary>
    /// Store multiple registers is a lot simpler than load multiple registers
    /// in cycle timing.
    ///
    /// The first cycle sets up A & D along with configuring the memory unit
    /// for a write cycle. Each following cycle assumes that write has happened
    /// and simply sets A & D again.
    ///
    /// After all writes have happened (the same cycle that the last write has
    /// happened on the memory unit) the memory unit is reset for opcode
    /// fetches.
    /// </summary>
    internal static void StmRegisterWriteCycle(Core core, uint _)
    {
        core.SEQ = 1;

        if (core.InstructionState.LdmStm._storeLoadMultiplePtr >= core.InstructionState.LdmStm._storeLoadMultiplePopCount)
        {
            Core.ResetMemoryUnitForOpcodeFetch(core, _);
        }
        else
        {
            core.A += 4;
            core.D = core.InstructionState.LdmStm._useBank0Regs
                ? core.GetUserModeRegister((int)core.InstructionState.LdmStm._storeLoadMultipleState[core.InstructionState.LdmStm._storeLoadMultiplePtr])
                : core.R[core.InstructionState.LdmStm._storeLoadMultipleState[core.InstructionState.LdmStm._storeLoadMultiplePtr]];

            // This is bit horrible, during an STM if PC is in the source list
            // then the written value would be one cycle later (because the
            // memory unit really uses the register not our cached val)
            // TODO - A much nicer solution here would be for the memory unit
            // to hold references to registers in D instead of uints so the cache
            // isn't required and this special case code would magically go away.
            if (core.InstructionState.LdmStm._storeLoadMultipleState[core.InstructionState.LdmStm._storeLoadMultiplePtr] == 15)
            {
                core.D += core.Cpsr.ThumbMode ? 2u : 4;
            }

            core.InstructionState.LdmStm._storeLoadMultiplePtr++;
        }

        // The writeback happens at the end of the 2nd cycle (after one register write)
        if (core.InstructionState.LdmStm._storeLoadMultipleDoWriteback)
        {
            core.InstructionState.LdmStm._storeLoadMultipleDoWriteback = false;
            if (core.InstructionState.LdmStm._useBank0Regs)
            {
                core.WriteUserModeRegister(core.InstructionState.LdmStm._writebackRegister, core.InstructionState.LdmStm._storeLoadMutipleFinalWritebackValue);
            }
            else
            {
                core.R[core.InstructionState.LdmStm._writebackRegister] = core.InstructionState.LdmStm._storeLoadMutipleFinalWritebackValue;
            }

            if (core.InstructionState.LdmStm._writebackRegister == 15)
            {
                core.ClearPipeline(); // TODO - This is a really bad idea, why would you use PC as writeback, probably needs a special log
            }
        }
    }

    /// <summary>
    /// LDM* functions take several cycles depending on the number of memory
    /// reads required.
    /// The first cycle (handled by ldm*_* partials) calculates the first
    /// address and stores it on A along with setting the memory unit up for
    /// non-opcode reads.
    ///
    /// The second cycle expects that the memory unit will have loaded that
    /// address value into core.D but doesn't do _anything with it_ (here we
    /// have to cache it off).
    ///
    /// The third cycle (and all following) load the value we cached into the
    /// relevant register.
    ///
    /// So an LDM which reads R0 and R1 will take 4 cycles
    /// - Set up A (ldm* partial function - not this function)
    /// - Start reading (non-SEQ) - cache off core.D into local variable and
    ///   increment A for next read
    /// - Set R0 with value read, cache core.D into local variable and since
    ///   this the last value has been read drive nMREQ high so that the next
    ///   cycle is internal with no memory accesses
    /// - Set R1 to cached value, set writeback value, reset memory unit for
    ///   opcode fetch.
    /// </summary>
    internal static void LdmRegisterReadCycle(Core core, uint _)
    {
        // Writeback happens at the end of the second cycle (before any
        // registers are loaded)
        if (core.InstructionState.LdmStm._storeLoadMultipleDoWriteback)
        {
            core.InstructionState.LdmStm._storeLoadMultipleDoWriteback = false;
            if (core.InstructionState.LdmStm._useBank0Regs)
            {
                core.WriteUserModeRegister(core.InstructionState.LdmStm._writebackRegister, core.InstructionState.LdmStm._storeLoadMutipleFinalWritebackValue);
            }
            else
            {
                core.R[core.InstructionState.LdmStm._writebackRegister] = core.InstructionState.LdmStm._storeLoadMutipleFinalWritebackValue;
            }

            if (core.InstructionState.LdmStm._writebackRegister == 15)
            {
                core.R[core.InstructionState.LdmStm._writebackRegister] &= core.Cpsr.ThumbMode ? 0xFFFF_FFFE : 0xFFFF_FFFC;
                core.ClearPipeline();
            }
        }

        if (core.InstructionState.LdmStm._storeLoadMultiplePtr > 0)
        {
            if (core.InstructionState.LdmStm._useBank0Regs)
            {
                core.WriteUserModeRegister((int)core.InstructionState.LdmStm._storeLoadMultipleState[core.InstructionState.LdmStm._storeLoadMultiplePtr - 1], core.InstructionState.LdmStm._cachedLdmValue);
            }
            else
            {
                core.R[core.InstructionState.LdmStm._storeLoadMultipleState[core.InstructionState.LdmStm._storeLoadMultiplePtr - 1]] = core.InstructionState.LdmStm._cachedLdmValue;
            }

            if (core.InstructionState.LdmStm._storeLoadMultipleState[core.InstructionState.LdmStm._storeLoadMultiplePtr - 1] == 15)
            {
                core.R[core.InstructionState.LdmStm._storeLoadMultipleState[core.InstructionState.LdmStm._storeLoadMultiplePtr - 1]] &= core.Cpsr.ThumbMode ? 0xFFFF_FFFE : 0xFFFF_FFFC;
                core.ClearPipeline();
            }
        }

        core.InstructionState.LdmStm._cachedLdmValue = core.D;
        core.A += 4;
        core.SEQ = 1;

        if (core.InstructionState.LdmStm._storeLoadMultiplePtr == core.InstructionState.LdmStm._storeLoadMultiplePopCount - 1)
        {
            core.A = core.R[15];
            core.AIncrement = 0;
            core.SEQ = 0;
            core.MAS = core.Cpsr.ThumbMode ? BusWidth.HalfWord : BusWidth.Word;
            core.nMREQ = true;
        }

        if (core.InstructionState.LdmStm._storeLoadMultiplePtr == core.InstructionState.LdmStm._storeLoadMultiplePopCount)
        {
            Core.ResetMemoryUnitForOpcodeFetch(core, _);
        }

        core.InstructionState.LdmStm._storeLoadMultiplePtr++;
    }
}
