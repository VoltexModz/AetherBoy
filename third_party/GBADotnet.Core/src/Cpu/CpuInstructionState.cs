namespace GameboyAdvanced.Core.Cpu;

/// <summary>
/// In-flight instruction scratch belongs to one ARM7TDMI, never to the process.
/// Save states stabilize at an instruction boundary, so this transient state
/// and its function pointers are reset on restore rather than serialized.
/// </summary>
internal sealed unsafe class CpuInstructionState
{
    internal readonly LdmStmState LdmStm = new();

    internal sealed class LdmStmState
    {
        internal readonly uint[] _storeLoadMultipleState = new uint[16];
        internal int _storeLoadMultiplePopCount;
        internal int _storeLoadMultiplePtr;
        internal uint _storeLoadMutipleFinalWritebackValue;
        internal bool _storeLoadMultipleDoWriteback;
        internal uint _cachedLdmValue;
        internal int _writebackRegister;
        internal bool _useBank0Regs;
    }

    internal readonly LdrStrState LdrStr = new();

    internal sealed class LdrStrState
    {
        internal int _ldrReg;
        internal int _writebackReg;
        internal uint _writebackVal;
        internal bool _doWriteback;
        internal delegate*<uint, uint, uint> _ldrCastFunc;
        internal uint _cachedValue;
    }

    internal readonly MultiplyState Multiply = new();

    internal sealed class MultiplyState
    {
        internal int _requiredCycles;
        internal int _currentCycles;
        internal int _destinationReg;
        internal uint _multiplyResult;
    }

    internal readonly MultiplyLongState MultiplyLong = new();

    internal sealed class MultiplyLongState
    {
        internal int _requiredCycles;
        internal int _currentCycles;
        internal uint _destinationRegHi;
        internal uint _destinationRegLo;
        internal ulong _multiplyResult;
    }

    internal readonly ArmState Arm = new();

    internal sealed class ArmState
    {
        internal uint _swpCachedVal;
        internal uint _swpDestinationReg;
        internal uint _swpSourceReg;
        internal uint _dataMask;
        internal delegate*<uint, uint, uint> _swapCastFunc;
        internal uint BlReturnAddress;
    }

    internal readonly ThumbState Thumb = new();

    internal sealed class ThumbState
    {
        internal uint _blR14;
        internal int _aluDestination;
        internal uint _cachedAluValue;
    }

}
