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
            bytes[address & 0xFFFF] = value;
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
