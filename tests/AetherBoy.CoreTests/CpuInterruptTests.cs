using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class CpuInterruptTests
{
    [TestMethod]
    public void OpcodeFetch_ReadsMemoryExactlyOnce()
    {
        var memory = new TestMemory();
        var cpu = CreateCpu(memory);
        memory[0] = 0x00;

        cpu.Tick();

        Assert.AreEqual(1, memory.ReadCounts[0]);
    }

    [TestMethod]
    public void ExecutionBreakpoint_StopsBeforeOpcodeFetch()
    {
        var memory = new TestMemory();
        var cpu = CreateCpu(memory);
        memory[0] = 0x00;
        using IDisposable subscription = cpu.Subscribe(
            new PauseOnReasonObserver(CPUStatusUpdate.UpdateReason.Execution));

        int cycles = cpu.Tick();

        Assert.AreEqual(4, cycles);
        Assert.IsFalse(cpu.Running);
        Assert.AreEqual((ushort)0, cpu.PC);
        Assert.AreEqual(0, memory.ReadCounts[0]);
    }

    [TestMethod]
    public void MemoryReadBreakpoint_StopsBeforeOpcodeExecution()
    {
        var memory = new TestMemory();
        var cpu = CreateCpu(memory);
        memory[0] = 0x00;
        using IDisposable subscription = cpu.Subscribe(
            new PauseOnReasonObserver(CPUStatusUpdate.UpdateReason.MemoryRead));

        int cycles = cpu.Tick();

        Assert.AreEqual(4, cycles);
        Assert.IsFalse(cpu.Running);
        Assert.AreEqual((ushort)0, cpu.PC);
        Assert.AreEqual(1, memory.ReadCounts[0]);
    }

    [TestMethod]
    public void Ei_EnablesInterruptsAfterFollowingInstruction()
    {
        var memory = new TestMemory();
        var cpu = CreateCpu(memory);
        memory[0] = 0xFB;
        memory[1] = 0x00;

        cpu.Tick();
        Assert.IsFalse(cpu.IME);

        cpu.Tick();
        Assert.IsTrue(cpu.IME);
    }

    [TestMethod]
    public void Di_CancelsPendingEiEnable()
    {
        var memory = new TestMemory();
        var cpu = CreateCpu(memory);
        memory[0] = 0xFB;
        memory[1] = 0xF3;
        memory[2] = 0x00;

        cpu.Tick();
        cpu.Tick();
        cpu.Tick();

        Assert.IsFalse(cpu.IME);
    }

    [TestMethod]
    public void Stop_TogglesDoubleSpeedAndClearsPreparation()
    {
        var memory = new TestMemory();
        var cpu = CreateCpu(memory);
        memory[0] = 0x10;
        memory[1] = 0x00;
        memory[2] = 0x10;
        memory[3] = 0x00;

        cpu.PrepareSpeedSwitch = true;
        cpu.Tick();

        Assert.IsTrue(cpu.IsDoubleSpeed);
        Assert.IsFalse(cpu.PrepareSpeedSwitch);
        Assert.AreEqual((ushort)2, cpu.PC);

        cpu.PrepareSpeedSwitch = true;
        cpu.Tick();

        Assert.IsFalse(cpu.IsDoubleSpeed);
        Assert.IsFalse(cpu.PrepareSpeedSwitch);
        Assert.AreEqual((ushort)4, cpu.PC);
    }

    [TestMethod]
    public void Stop_WithoutSpeedSwitchSleepsUntilAButtonTransition()
    {
        var memory = new TestMemory();
        var cpu = CreateCpu(memory);
        memory[0] = 0x10;
        memory[1] = 0x00;
        memory[2] = 0x00;

        cpu.Tick();

        Assert.IsTrue(cpu.IsStopped);
        Assert.AreEqual((ushort)2, cpu.PC);
        cpu.Tick();
        Assert.AreEqual((ushort)2, cpu.PC);

        var joypad = new Joypad(new Interrupt(cpu), cpu.WakeFromStop);
        joypad.SetButtons(GameBoyButtons.A);
        Assert.IsFalse(cpu.IsStopped);
        cpu.Tick();
        Assert.AreEqual((ushort)3, cpu.PC);
    }

    [TestMethod]
    public void InterruptService_UsesPriorityAndConsumesTwentyCpuTicks()
    {
        var memory = new TestMemory();
        var cpu = CreateCpu(memory);
        cpu.PC = 0x1234;
        cpu.SP = 0xFFFE;
        cpu.IME = true;
        var interrupt = new Interrupt(cpu)
        {
            IE = 0x1F,
            IF = 0x1F
        };

        int cycles = interrupt.ServicePending();

        Assert.AreEqual(20, cycles);
        Assert.AreEqual((ushort)0x0040, cpu.PC);
        Assert.AreEqual((ushort)0xFFFC, cpu.SP);
        Assert.AreEqual(0x34, memory[0xFFFC]);
        Assert.AreEqual(0x12, memory[0xFFFD]);
        Assert.AreEqual(0x1E, interrupt.IF);
        Assert.IsFalse(cpu.IME);
    }

    [TestMethod]
    public void InterruptService_WrapsStackWritesAtTheAddressSpaceBoundary()
    {
        var memory = new TestMemory();
        var cpu = CreateCpu(memory);
        cpu.PC = 0x1234;
        cpu.SP = 0x0001;
        cpu.IME = true;
        var interrupt = new Interrupt(cpu) { IE = 1, IF = 1 };

        interrupt.ServicePending();

        Assert.AreEqual((ushort)0xFFFF, cpu.SP);
        Assert.AreEqual(0x34, memory[0xFFFF]);
        Assert.AreEqual(0x12, memory[0x0000]);
    }

    [TestMethod]
    public void InterruptDispatch_CanBeCancelledWhenTheHighPushClearsIe()
    {
        var memory = new TestMemory();
        var cpu = CreateCpu(memory);
        cpu.PC = 0x0235;
        cpu.SP = 0x0000;
        cpu.IME = true;
        var interrupt = new Interrupt(cpu) { IE = 0x04, IF = 0x04 };
        memory.ByteWritten = (address, value) =>
        {
            if (address == 0xFFFF) {
                interrupt.IE = value;
            }
        };

        int cycles = interrupt.ServicePending();

        Assert.AreEqual(20, cycles);
        Assert.AreEqual((ushort)0x0000, cpu.PC);
        Assert.AreEqual((ushort)0xFFFF, cpu.SP);
        Assert.AreEqual(0x04, interrupt.IF);
        Assert.IsFalse(cpu.IME);
    }

    [TestMethod]
    public void InterruptDispatch_ReselectsPriorityAfterTheHighPushWritesIe()
    {
        var memory = new TestMemory();
        var cpu = CreateCpu(memory);
        cpu.PC = 0x0235;
        cpu.SP = 0x0000;
        cpu.IME = true;
        var interrupt = new Interrupt(cpu) { IE = 0x03, IF = 0x03 };
        memory.ByteWritten = (address, value) =>
        {
            if (address == 0xFFFF) {
                interrupt.IE = value;
            }
        };

        interrupt.ServicePending();

        Assert.AreEqual((ushort)0x0048, cpu.PC);
        Assert.AreEqual(0x01, interrupt.IF);
    }

    [TestMethod]
    public void InterruptDispatch_DoesNotCancelAfterTheLowPushWritesIe()
    {
        var memory = new TestMemory();
        var cpu = CreateCpu(memory);
        cpu.PC = 0x0235;
        cpu.SP = 0x0001;
        cpu.IME = true;
        var interrupt = new Interrupt(cpu) { IE = 0x08, IF = 0x08 };
        memory.ByteWritten = (address, value) =>
        {
            if (address == 0xFFFF) {
                interrupt.IE = value;
            }
        };

        interrupt.ServicePending();

        Assert.AreEqual((ushort)0x0058, cpu.PC);
        Assert.AreEqual(0, interrupt.IF);
        Assert.AreEqual(0x35, interrupt.IE);
    }

    [TestMethod]
    public void PendingInterrupt_WakesHaltEvenWhenImeIsClear()
    {
        var memory = new TestMemory();
        var cpu = CreateCpu(memory);
        cpu.WaitForInterrupt = true;
        cpu.IME = false;
        var interrupt = new Interrupt(cpu)
        {
            IE = 0x04,
            IF = 0x04
        };

        int cycles = interrupt.ServicePending();

        Assert.AreEqual(0, cycles);
        Assert.IsFalse(cpu.WaitForInterrupt);
        Assert.AreEqual(0x04, interrupt.IF);
    }

    [TestMethod]
    public void HaltBug_SuppressesNextOpcodeFetchIncrement()
    {
        var memory = new TestMemory();
        var cpu = CreateCpu(memory);
        memory[0] = 0x76;
        memory[1] = 0x3E;
        memory[2] = 0x42;
        memory[0xFFFF] = 0x01;
        memory[0xFF0F] = 0x01;

        cpu.Tick();
        cpu.Tick();

        Assert.IsFalse(cpu.WaitForInterrupt);
        Assert.AreEqual(0x3E, cpu.A);
        Assert.AreEqual((ushort)2, cpu.PC);
    }

    [TestMethod]
    public void IllegalOpcode_LocksTheCpuUntilResetAndIgnoresInterrupts()
    {
        var memory = new TestMemory();
        var cpu = CreateCpu(memory);
        memory[0] = 0xD3;
        memory[1] = 0x00;

        Assert.AreEqual(4, cpu.Tick());
        Assert.IsTrue(cpu.IsLockedUp);
        Assert.AreEqual((ushort)1, cpu.PC);
        Assert.AreEqual(4, cpu.Tick());
        Assert.AreEqual((ushort)1, cpu.PC);

        cpu.IME = true;
        var interrupt = new Interrupt(cpu) { IE = 1, IF = 1 };
        Assert.AreEqual(0, interrupt.ServicePending());
        Assert.AreEqual((ushort)1, cpu.PC);

        cpu.ResetExecutionState();
        Assert.IsFalse(cpu.IsLockedUp);
        cpu.PC = 1;
        cpu.Tick();
        Assert.AreEqual((ushort)2, cpu.PC);
    }

    [TestMethod]
    public void DecimalAdjust_HandlesAdditionAndSubtractionFlags()
    {
        var memory = new TestMemory();
        var cpu = CreateCpu(memory);
        memory[0] = 0x27;

        cpu.A = 0x9A;
        Assert.AreEqual(4, cpu.Tick());
        Assert.AreEqual(0x00, cpu.A);
        Assert.IsTrue(cpu.FlagZ);
        Assert.IsFalse(cpu.FlagN);
        Assert.IsFalse(cpu.FlagH);
        Assert.IsTrue(cpu.FlagC);

        cpu.PC = 0;
        cpu.A = 0x13;
        cpu.FlagZ = false;
        cpu.FlagN = true;
        cpu.FlagH = true;
        cpu.FlagC = false;
        cpu.Tick();
        Assert.AreEqual(0x0D, cpu.A);
        Assert.IsFalse(cpu.FlagZ);
        Assert.IsTrue(cpu.FlagN);
        Assert.IsFalse(cpu.FlagH);
        Assert.IsFalse(cpu.FlagC);

        cpu.PC = 0;
        cpu.A = 0x73;
        cpu.FlagN = true;
        cpu.FlagH = false;
        cpu.FlagC = true;
        cpu.Tick();
        Assert.AreEqual(0x13, cpu.A);
        Assert.IsTrue(cpu.FlagN);
        Assert.IsTrue(cpu.FlagC);
    }

    [TestMethod]
    public void SignedStackPointerArithmetic_UsesUnsignedLowByteFlagsAndClearsZeroAndSubtract()
    {
        var memory = new TestMemory();
        var cpu = CreateCpu(memory);
        memory[0] = 0xE8;
        memory[1] = 0xFF;
        cpu.SP = 0x0000;
        cpu.FlagZ = true;
        cpu.FlagN = true;

        Assert.AreEqual(16, cpu.Tick());
        Assert.AreEqual((ushort)0xFFFF, cpu.SP);
        Assert.IsFalse(cpu.FlagZ);
        Assert.IsFalse(cpu.FlagN);
        Assert.IsFalse(cpu.FlagH);
        Assert.IsFalse(cpu.FlagC);

        cpu.PC = 0;
        cpu.SP = 0x0001;
        cpu.Tick();
        Assert.AreEqual((ushort)0x0000, cpu.SP);
        Assert.IsTrue(cpu.FlagH);
        Assert.IsTrue(cpu.FlagC);

        memory[0] = 0xF8;
        memory[1] = 0x08;
        cpu.PC = 0;
        cpu.SP = 0xFFF8;
        cpu.FlagZ = true;
        cpu.FlagN = true;
        Assert.AreEqual(12, cpu.Tick());
        Assert.AreEqual((ushort)0xFFF8, cpu.SP);
        Assert.AreEqual(0x00, cpu.H);
        Assert.AreEqual(0x00, cpu.L);
        Assert.IsFalse(cpu.FlagZ);
        Assert.IsFalse(cpu.FlagN);
        Assert.IsTrue(cpu.FlagH);
        Assert.IsTrue(cpu.FlagC);
    }

    [TestMethod]
    public void MemoryStoreAndBitInstructions_ReportHardwareCycleCounts()
    {
        var memory = new TestMemory();
        var cpu = CreateCpu(memory);
        memory[0] = 0x70;
        cpu.H = 0xC0;
        cpu.L = 0x00;
        cpu.B = 0xA5;

        Assert.AreEqual(8, cpu.Tick());
        Assert.AreEqual(0xA5, memory[0xC000]);

        memory[1] = 0xCB;
        memory[2] = 0x46;
        cpu.PC = 1;
        Assert.AreEqual(12, cpu.Tick());
    }

    [TestMethod]
    public void CycleSink_AdvancesThroughTheIoWriteMachineCycleBeforeTheWriteOccurs()
    {
        var memory = new TestMemory();
        var cpu = CreateCpu(memory);
        memory[0] = 0xE0; // LDH ($05),A
        memory[1] = 0x05;
        cpu.A = 0x77;
        int elapsedCycles = 0;
        int writeCycle = 0;
        cpu.CycleSink = cycles => elapsedCycles += cycles;
        memory.ByteWritten = (address, _) =>
        {
            if (address == 0xFF05) {
                writeCycle = elapsedCycles;
            }
        };

        int totalCycles = cpu.Tick();

        Assert.AreEqual(12, totalCycles);
        Assert.AreEqual(12, elapsedCycles);
        Assert.AreEqual(9, writeCycle);
        Assert.AreEqual(0x77, memory[0xFF05]);
    }

    [TestMethod]
    public void CycleSink_WritesTimerControlAtTheBusCycleBoundary()
    {
        var memory = new TestMemory();
        var cpu = CreateCpu(memory);
        memory[0] = 0xE0; // LDH ($07),A
        memory[1] = 0x07;
        int elapsedCycles = 0;
        int writeCycle = 0;
        cpu.CycleSink = cycles => elapsedCycles += cycles;
        memory.ByteWritten = (address, _) =>
        {
            if (address == 0xFF07) {
                writeCycle = elapsedCycles;
            }
        };

        cpu.Tick();

        Assert.AreEqual(8, writeCycle);
        Assert.AreEqual(12, elapsedCycles);
    }

    [TestMethod]
    public void Push_UsesAnInternalCycleAndWritesHighByteBeforeLowByte()
    {
        var memory = new TestMemory();
        var cpu = CreateCpu(memory);
        memory[0] = 0xC5; // PUSH BC
        cpu.SP = 0xC002;
        cpu.B = 0x12;
        cpu.C = 0x34;
        int elapsedCycles = 0;
        var writes = new List<(int Address, byte Value, int Cycle)>();
        cpu.CycleSink = cycles => elapsedCycles += cycles;
        memory.ByteWritten = (address, value) => writes.Add((address, value, elapsedCycles));

        int totalCycles = cpu.Tick();

        Assert.AreEqual(16, totalCycles);
        Assert.AreEqual(16, elapsedCycles);
        CollectionAssert.AreEqual(
            new[] { (0xC001, (byte)0x12, 9), (0xC000, (byte)0x34, 13) },
            writes.ToArray());
        Assert.AreEqual((ushort)0xC000, cpu.SP);
    }

    private static CPU CreateCpu(TestMemory memory)
    {
        var cpu = new CPU
        {
            Memory = memory,
            PC = 0,
            SP = 0xFFFE
        };
        return cpu;
    }

    private sealed class TestMemory : IMemoryDevice
    {
        private readonly byte[] bytes = new byte[0x10000];

        public int[] ReadCounts { get; } = new int[0x10000];
        public Action<int, byte>? ByteWritten { get; set; }

        public byte this[int address]
        {
            get => bytes[address & 0xFFFF];
            set => bytes[address & 0xFFFF] = value;
        }

        public byte ReadByte(int address)
        {
            int normalized = address & 0xFFFF;
            ReadCounts[normalized]++;
            return bytes[normalized];
        }

        public void WriteByte(int address, byte value)
        {
            int normalized = address & 0xFFFF;
            bytes[normalized] = value;
            ByteWritten?.Invoke(normalized, value);
        }
    }

    private sealed class PauseOnReasonObserver : IObserver<CPUStatusUpdate>
    {
        private readonly CPUStatusUpdate.UpdateReason reason;

        public PauseOnReasonObserver(CPUStatusUpdate.UpdateReason reason)
        {
            this.reason = reason;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(CPUStatusUpdate value)
        {
            if (value.Reason == reason)
            {
                value.CPU.Running = false;
            }
        }
    }
}
