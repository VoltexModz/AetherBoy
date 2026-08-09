using System;
using System.Text;

namespace nanoboy.Core
{
    public enum ConformanceOutcome
    {
        Passed,
        Failed,
        TimedOut
    }

    public readonly record struct ConformanceResult(
        ConformanceOutcome Outcome,
        string SerialOutput,
        int FramesExecuted);

    public sealed class HeadlessConformanceRunner
    {
        public const int DefaultMaximumFrames = 600;

        public ConformanceResult Run(ROM rom, int maximumFrames = DefaultMaximumFrames)
        {
            if (rom == null) {
                throw new ArgumentNullException(nameof(rom));
            }
            if (maximumFrames <= 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumFrames),
                    maximumFrames,
                    "At least one frame must be executed.");
            }

            var serialCapture = new ConformanceSerialDevice();
            using var emulator = new Nanoboy(rom);
            emulator.Memory.AttachSerialDevice(serialCapture);
            emulator.Configure(new EmulatorConfiguration(
                Frameskip: 60,
                AudioEnabled: false,
                Channel1Enabled: false,
                Channel2Enabled: false,
                Channel3Enabled: false,
                Channel4Enabled: false,
                SampleRate: 44_100));

            for (int frame = 1; frame <= maximumFrames; frame++) {
                emulator.Frame();
                string output = serialCapture.Output;
                if (output.Contains("Failed", StringComparison.OrdinalIgnoreCase)) {
                    return new ConformanceResult(ConformanceOutcome.Failed, output, frame);
                }
                if (output.Contains("Passed", StringComparison.OrdinalIgnoreCase)) {
                    return new ConformanceResult(ConformanceOutcome.Passed, output, frame);
                }
            }

            return new ConformanceResult(
                ConformanceOutcome.TimedOut,
                serialCapture.Output,
                maximumFrames);
        }

        private sealed class ConformanceSerialDevice : ISerialDevice
        {
            private readonly StringBuilder output = new StringBuilder();
            private byte transferByte;

            public string Output => output.ToString();

            public void Start()
            {
                output.Append((char)transferByte);
            }

            public void Stop()
            {
            }

            public void Write(byte value)
            {
                transferByte = value;
            }

            public byte Read()
            {
                return 0xFF;
            }
        }
    }
}
