using System;

namespace nanoboy.Core
{
    public static class EmulationClock
    {
        public const int CpuClockHz = 4_194_304;
        public const int DotsPerScanline = 456;
        public const int ScanlinesPerFrame = 154;
        public const int DotsPerFrame = 70_224;
        public const double FrameSeconds = (double)DotsPerFrame / CpuClockHz;
        public const double FramesPerSecond = (double)CpuClockHz / DotsPerFrame;
        public static TimeSpan FrameDuration { get; } = TimeSpan.FromSeconds(FrameSeconds);
    }
}
