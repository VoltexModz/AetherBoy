using System;

namespace nanoboy.Core
{
    public sealed class Nanoboy : IDisposable
    {
        public CPU Cpu;
        public Memory Memory;
        private int dotOvershoot;
        private int doubleSpeedCpuPhase;
        private readonly Action<int> advanceInstructionCycles;
        private bool instructionDoubleSpeed;
        private int instructionDots;

        public Nanoboy(ROM rom, byte[] bootRom = null)
        {
            advanceInstructionCycles = AdvanceInstructionCycles;
            Cpu = new CPU();
            Memory = new Memory(Cpu, rom);
            Cpu.Memory = Memory;
            if (bootRom != null && bootRom.Length > 0)
            {
                Memory.BootROM = bootRom;
                Memory.BootROMEnabled = true;
            }
            Reset();
        }

        public void Frame()
        {
            if (Cpu.IsStopped)
            {
                Memory.Video.FrameReady = false;
                return;
            }

            int dotBudget = EmulationClock.DotsPerFrame - dotOvershoot;
            int dotsExecuted = 0;

            while (dotsExecuted < dotBudget)
            {
                dotsExecuted += StepClockedInstruction();
            }

            // Instructions are atomic in the current CPU. Carry their small dot
            // overshoot into the next call instead of accumulating frame-rate drift.
            dotOvershoot = dotsExecuted - dotBudget;

            Memory.Video.FrameReady = false;
        }

        /// <summary>
        /// Advances one instruction, interrupt service, or HDMA stall dot on the owner
        /// thread. Returns elapsed 4 MHz base dots (not double-speed CPU T-cycles).
        /// A stopped CPU returns zero without advancing its hardware; a paired scheduler
        /// must allow its peer to advance and keep processing input to wake it.
        /// Do not interleave this stepping mode with Frame's frame-overshoot accounting.
        /// </summary>
        public int StepInstruction() => Cpu.IsStopped ? 0 : StepClockedInstruction();

        private int StepClockedInstruction()
        {
            if (Memory.HDMA.ConsumeCpuStallDot())
            {
                int stalledCpuCycles = Cpu.IsDoubleSpeed ? 2 : 1;
                for (int cycle = 0; cycle < stalledCpuCycles; cycle++)
                {
                    Memory.Timer.Tick();
                    Memory.HDMA.TickOamDma();
                    Memory.TickSerial();
                }
                Memory.Video.Tick();
                Memory.Audio.Tick();
                return 1;
            }

            // STOP's speed switch belongs to the clock domain active on instruction entry.
            instructionDoubleSpeed = Cpu.IsDoubleSpeed;
            instructionDots = 0;
            Cpu.CycleSink = advanceInstructionCycles;
            int cpuCycles;
            try
            {
                cpuCycles = Memory.Interrupt.ServicePending();
                if (cpuCycles == 0)
                    cpuCycles = Cpu.Tick();
            }
            finally
            {
                Cpu.CycleSink = null;
            }

            if (cpuCycles <= 0 || instructionDots <= 0)
                throw new InvalidOperationException("The CPU returned a non-positive cycle count.");
            return instructionDots;
        }

        private void AdvanceInstructionCycles(int cycles)
        {
            for (int cpuCycle = 0; cpuCycle < cycles; cpuCycle++)
            {
                Memory.Timer.Tick();
                Memory.HDMA.TickOamDma();
                Memory.TickSerial();
                if (instructionDoubleSpeed)
                {
                    if (++doubleSpeedCpuPhase < 2)
                        continue;
                    doubleSpeedCpuPhase = 0;
                }
                else
                {
                    doubleSpeedCpuPhase = 0;
                }
                Memory.Video.Tick();
                Memory.Audio.Tick();
                instructionDots++;
            }
        }

        public void Reset()
        {
            dotOvershoot = 0;
            doubleSpeedCpuPhase = 0;
            Cpu.ResetExecutionState();
            Memory.Interrupt.IE = 0;
            Memory.Interrupt.IF = 0;
            Memory.Timer.Reset();
            Memory.HDMA.Reset();
            Memory.ResetSerial();
            Memory.Video.ResetTiming();
            Memory.Audio.ResetHardware();

            if (Memory.BootROMEnabled && Memory.BootROM != null)
            {
                Cpu.A = 0x00;
                Cpu.B = 0x00;
                Cpu.C = 0x00;
                Cpu.D = 0x00;
                Cpu.E = 0x00;
                Cpu.H = 0x00;
                Cpu.L = 0x00;
                Cpu.SP = 0x0000;
                Cpu.PC = 0x0000;
                Cpu.FlagZ = false;
                Cpu.FlagN = false;
                Cpu.FlagH = false;
                Cpu.FlagC = false;
            }
            else
            {
                Cpu.A = 0x11;
                Cpu.B = 0x00;
                Cpu.C = 0x13;
                Cpu.D = 0x00;
                Cpu.E = 0xD8;
                Cpu.H = 0x01;
                Cpu.L = 0x4D;
                Cpu.SP = 0xFFFE;
                Cpu.PC = 0x0100;
                Cpu.FlagZ = true;
                Cpu.FlagN = false;
                Cpu.FlagH = true;
                Cpu.FlagC = true;
                Cpu.Memory.WriteByte(0xFF05, 0x00);
                Cpu.Memory.WriteByte(0xFF06, 0x00);
                Cpu.Memory.WriteByte(0xFF07, 0x00);
                Cpu.Memory.WriteByte(0xFF10, 0x80);
                Cpu.Memory.WriteByte(0xFF11, 0xBF);
                Cpu.Memory.WriteByte(0xFF12, 0xF3);
                Cpu.Memory.WriteByte(0xFF14, 0xBF);
                Cpu.Memory.WriteByte(0xFF16, 0x3F);
                Cpu.Memory.WriteByte(0xFF17, 0x00);
                Cpu.Memory.WriteByte(0xFF19, 0xBF);
                Cpu.Memory.WriteByte(0xFF1A, 0x7F);
                Cpu.Memory.WriteByte(0xFF1B, 0xFF);
                Cpu.Memory.WriteByte(0xFF1C, 0x9F);
                Cpu.Memory.WriteByte(0xFF1E, 0xBF);
                Cpu.Memory.WriteByte(0xFF20, 0xFF);
                Cpu.Memory.WriteByte(0xFF21, 0x00);
                Cpu.Memory.WriteByte(0xFF22, 0x00);
                Cpu.Memory.WriteByte(0xFF23, 0xBF);
                Cpu.Memory.WriteByte(0xFF24, 0x77);
                Cpu.Memory.WriteByte(0xFF25, 0xF3);
                Cpu.Memory.WriteByte(0xFF26, 0xF1);
                Cpu.Memory.WriteByte(0xFF40, 0x91);
                Cpu.Memory.WriteByte(0xFF42, 0x00);
                Cpu.Memory.WriteByte(0xFF43, 0x00);
                Cpu.Memory.WriteByte(0xFF45, 0x00);
                Cpu.Memory.WriteByte(0xFF47, 0xFC);
                Cpu.Memory.WriteByte(0xFF48, 0xFF);
                Cpu.Memory.WriteByte(0xFF49, 0xFF);
                Cpu.Memory.WriteByte(0xFF4A, 0x00);
                Cpu.Memory.WriteByte(0xFF4B, 0x00);
                Cpu.Memory.WriteByte(0xFFFF, 0x00);
            }
        }

        public void Configure(EmulatorConfiguration configuration)
        {
            if (configuration.Frameskip < 0 || configuration.Frameskip > 60)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuration),
                    configuration.Frameskip,
                    "Frameskip must be between 0 and 60.");
            }

            if (configuration.SampleRate < 8_000 || configuration.SampleRate > 192_000)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuration),
                    configuration.SampleRate,
                    "The sample rate must be between 8 kHz and 192 kHz.");
            }

            Memory.Audio.SampleRate = configuration.SampleRate;
            Memory.Audio.Enabled = configuration.AudioEnabled;
            Memory.Audio.Channel1.Enabled = configuration.Channel1Enabled;
            Memory.Audio.Channel2.Enabled = configuration.Channel2Enabled;
            Memory.Audio.Channel3.Enabled = configuration.Channel3Enabled;
            Memory.Audio.Channel4.Enabled = configuration.Channel4Enabled;
            Memory.Video.Frameskip = configuration.Frameskip;
        }

        public Subscription<CPUStatusUpdate> Debug(IObserver<CPUStatusUpdate> debugger)
        {
            return (Subscription<CPUStatusUpdate>)Cpu.Subscribe(debugger);
        }

        public void SetButtons(GameBoyButtons buttons, bool active)
        {
            Memory.Joypad.SetButtons(buttons, active);
        }

        public void SetButtons(GameBoyButtons pressedButtons)
        {
            Memory.Joypad.SetButtons(pressedButtons);
        }

        internal byte[] CaptureClockStatePayload()
        {
            return StatePayload.Write(writer => {
                writer.Write(dotOvershoot);
                writer.Write(doubleSpeedCpuPhase);
            });
        }

        internal Action PrepareClockStateRestore(byte[] payload)
        {
            return StatePayload.Read(payload, reader => {
                int nextDotOvershoot = reader.ReadInt32();
                int nextDoubleSpeedCpuPhase = reader.ReadInt32();
                StatePayload.RequireRange(nextDotOvershoot, 0, 23, nameof(dotOvershoot));
                StatePayload.RequireRange(nextDoubleSpeedCpuPhase, 0, 1, nameof(doubleSpeedCpuPhase));
                return (Action)(() => {
                    dotOvershoot = nextDotOvershoot;
                    doubleSpeedCpuPhase = nextDoubleSpeedCpuPhase;
                });
            });
        }

        public void Dispose()
        {
            Memory.Dispose();
        }
    }
}
