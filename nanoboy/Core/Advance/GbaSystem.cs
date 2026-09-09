using System;

namespace nanoboy.Core.Advance
{
    public sealed class GbaSystem
    {
        public const int ClockHz = 16_777_216;
        public const int CyclesPerScanline = 1_232;
        public const int ScanlinesPerFrame = 228;
        public const int CyclesPerFrame = CyclesPerScanline * ScanlinesPerFrame;

        public GbaSystem(ReadOnlySpan<byte> rom)
        {
            Bus = new GbaMemoryBus(rom);
            Cpu = new Arm7Cpu(Bus);
            Video = new GbaVideo();
        }

        public GbaMemoryBus Bus { get; }
        public Arm7Cpu Cpu { get; }
        public GbaVideo Video { get; }
        public long FrameCount { get; private set; }

        public void Frame()
        {
            int cycles = 0;
            while (cycles < CyclesPerFrame)
                cycles += Cpu.Step();
            Video.RenderFrame(Bus);
            FrameCount++;
        }

        public void Reset()
        {
            Cpu.Reset();
            FrameCount = 0;
        }
    }
}
