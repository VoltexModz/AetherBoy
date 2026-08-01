using System;

namespace nanoboy.Core
{
    public sealed class Nanoboy : IDisposable
    {
        public CPU Cpu;
        public Memory Memory;
        private int dotOvershoot;
        private int doubleSpeedCpuPhase;

        public Nanoboy(ROM rom, byte[] bootRom = null)
        {
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
            int dotBudget = EmulationClock.DotsPerFrame - dotOvershoot;
            int dotsExecuted = 0;

            while (dotsExecuted < dotBudget)
            {
                // A speed switch takes effect after STOP. The instruction itself still
                // belongs to the clock domain that was active when it started.
                bool doubleSpeed = Cpu.IsDoubleSpeed;
                int cpuCycles = Memory.Interrupt.ServicePending();
                if (cpuCycles == 0)
                {
                    cpuCycles = Cpu.Tick();
                }

                if (cpuCycles <= 0)
                {
                    throw new InvalidOperationException("The CPU returned a non-positive cycle count.");
                }

                for (int cpuCycle = 0; cpuCycle < cpuCycles; cpuCycle++)
                {
                    // DIV/TIMA are driven by the CPU clock and therefore continue to
                    // receive every T-cycle in CGB double-speed mode.
                    Memory.Timer.Tick();

                    if (doubleSpeed)
                    {
                        doubleSpeedCpuPhase++;
                        if (doubleSpeedCpuPhase < 2)
                        {
                            continue;
                        }

                        doubleSpeedCpuPhase = 0;
                    }
                    else
                    {
                        doubleSpeedCpuPhase = 0;
                    }

                    Memory.Video.Tick();
                    Memory.Audio.Tick();
                    dotsExecuted++;
                }
            }

            // Instructions are atomic in the current CPU. Carry their small dot
            // overshoot into the next call instead of accumulating frame-rate drift.
            dotOvershoot = dotsExecuted - dotBudget;

            Memory.Video.FrameReady = false;
        }

        public void Reset()
        {
            dotOvershoot = 0;
            doubleSpeedCpuPhase = 0;
            Cpu.ResetExecutionState();
            Memory.Interrupt.IE = 0;
            Memory.Interrupt.IF = 0;
            Memory.Timer.Reset();
            Memory.Video.ResetTiming();
            Memory.Audio.ResetTiming();

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

        public void Dispose()
        {
            Memory.Dispose();
        }
    }
}
