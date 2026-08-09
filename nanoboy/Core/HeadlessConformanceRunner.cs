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

    public enum ConformanceProtocol
    {
        None,
        SerialText,
        BlarggMemory,
        MooneyeRegisters
    }

    public readonly record struct ConformanceResult(
        ConformanceOutcome Outcome,
        string Output,
        int FramesExecuted,
        ConformanceProtocol Protocol)
    {
        public string SerialOutput => Output;
    }

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
                if (serialCapture.Outcome is ConformanceOutcome serialOutcome) {
                    return new ConformanceResult(
                        serialOutcome,
                        serialCapture.Output,
                        frame,
                        ConformanceProtocol.SerialText);
                }
                if (TryReadBlarggResult(emulator.Memory, out ConformanceOutcome blarggOutcome, out string blarggOutput)) {
                    return new ConformanceResult(
                        blarggOutcome,
                        blarggOutput,
                        frame,
                        ConformanceProtocol.BlarggMemory);
                }
                if (HasMooneyePassSignature(emulator.Cpu)) {
                    return new ConformanceResult(
                        ConformanceOutcome.Passed,
                        "Mooneye Fibonacci register signature",
                        frame,
                        ConformanceProtocol.MooneyeRegisters);
                }
            }

            return new ConformanceResult(
                ConformanceOutcome.TimedOut,
                serialCapture.Output,
                maximumFrames,
                ConformanceProtocol.None);
        }

        private static bool TryReadBlarggResult(
            Memory memory,
            out ConformanceOutcome outcome,
            out string output)
        {
            outcome = ConformanceOutcome.TimedOut;
            output = string.Empty;
            if (memory.ReadByte(0xA001) != 0xDE ||
                memory.ReadByte(0xA002) != 0xB0 ||
                memory.ReadByte(0xA003) != 0x61) {
                return false;
            }

            int status = memory.ReadByte(0xA000);
            if (status == 0x80) {
                return false;
            }

            var message = new StringBuilder();
            for (int address = 0xA004; address <= 0xAFFF; address++) {
                int value = memory.ReadByte(address);
                if (value == 0 || value == 0xFF) {
                    break;
                }
                message.Append((char)value);
            }

            outcome = status == 0 ? ConformanceOutcome.Passed : ConformanceOutcome.Failed;
            output = message.ToString();
            return true;
        }

        private static bool HasMooneyePassSignature(CPU cpu) =>
            cpu.B == 3 &&
            cpu.C == 5 &&
            cpu.D == 8 &&
            cpu.E == 13 &&
            cpu.H == 21 &&
            cpu.L == 34;

        private sealed class ConformanceSerialDevice : ISerialDevice
        {
            private readonly StringBuilder output = new StringBuilder();
            private byte transferByte;

            public string Output => output.ToString();
            public ConformanceOutcome? Outcome { get; private set; }

            public void Start()
            {
                output.Append((char)transferByte);
                string current = output.ToString();
                if (current.Contains("Failed", StringComparison.OrdinalIgnoreCase)) {
                    Outcome = ConformanceOutcome.Failed;
                } else if (current.Contains("Passed", StringComparison.OrdinalIgnoreCase)) {
                    Outcome = ConformanceOutcome.Passed;
                }
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
