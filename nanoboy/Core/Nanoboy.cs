using System;
using System.Drawing;
using System.Windows.Forms;

namespace nanoboy.Core
{
    public sealed class Nanoboy : IDisposable
    {
        public CPU Cpu;
        public Memory Memory;
        private static int[] samplerates = new int[] {
            8192, 16384, 32768, 44100
        };

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
            bool doublespeed = Cpu.IsDoubleSpeed;
            int cycles_per_frame = doublespeed ? 140448 : 70224;

            for (int i = 0; i < cycles_per_frame; i++) {
                int cycles = Cpu.Tick();

                if (doublespeed)
                    cycles >>= 1;

                for (int j = 0; j < cycles; j++) {
                    Memory.Interrupt.Tick();
                    Memory.Video.Tick();
                    Memory.Audio.Tick();
                    Memory.Timer.Tick(doublespeed);
                }

                i += cycles - 1;
            }

            Memory.Video.FrameReady = false;
        }

        public void Reset()
        {
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

        public void SetSettings(IEmulatorSettings settings)
        {
            Memory.Audio.SampleRate = samplerates[settings.SampleRate];
            Memory.Audio.Channel1.Enabled = settings.Channel1Enable;
            Memory.Audio.Channel2.Enabled = settings.Channel2Enable;
            Memory.Audio.Channel3.Enabled = settings.Channel3Enable;
            Memory.Audio.Channel4.Enabled = settings.Channel4Enable;
            Memory.Audio.Enabled = settings.AudioEnable;
            Memory.Video.Frameskip = settings.Frameskip;
            Memory.Joypad.Settings = settings;
        }

        public Subscription<CPUStatusUpdate> Debug(IObserver<CPUStatusUpdate> debugger)
        {
            return (Subscription<CPUStatusUpdate>)Cpu.Subscribe(debugger);
        }

        public void SetKey(Keys key)
        {
            Memory.Joypad.Set(key, false);
        }

        public void UnsetKey(Keys key)
        {
            Memory.Joypad.Set(key, true);
        }

        public void Dispose()
        {
            Memory.Dispose();
        }
    }
}
