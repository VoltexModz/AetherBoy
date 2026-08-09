using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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
        MooneyeRegisters,
        SmokeFrames
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
                    string output = serialCapture.Output;
                    if (serialOutcome == ConformanceOutcome.Failed &&
                        serialCapture.Protocol == ConformanceProtocol.MooneyeRegisters) {
                        output += FormatMooneyeSavedRegisters(emulator.Memory);
                    }
                    return new ConformanceResult(
                        serialOutcome,
                        output,
                        frame,
                        serialCapture.Protocol);
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
                if (HasMooneyeFailSignature(emulator.Cpu)) {
                    return new ConformanceResult(
                        ConformanceOutcome.Failed,
                        "Mooneye failure register signature",
                        frame,
                        ConformanceProtocol.MooneyeRegisters);
                }
            }

            return new ConformanceResult(
                ConformanceOutcome.TimedOut,
                FormatTimeoutState(emulator, serialCapture.Output),
                maximumFrames,
                ConformanceProtocol.None);
        }

        public ConformanceResult RunSmoke(ROM rom, int framesToExecute = DefaultMaximumFrames)
        {
            if (rom == null) {
                throw new ArgumentNullException(nameof(rom));
            }
            if (framesToExecute <= 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(framesToExecute),
                    framesToExecute,
                    "At least one frame must be executed.");
            }

            using var emulator = new Nanoboy(rom);
            emulator.Configure(new EmulatorConfiguration(
                Frameskip: 0,
                AudioEnabled: false,
                Channel1Enabled: false,
                Channel2Enabled: false,
                Channel3Enabled: false,
                Channel4Enabled: false,
                SampleRate: 44_100));

            for (int frame = 0; frame < framesToExecute; frame++) {
                emulator.Frame();
            }

            var pixels = new int[Video.FramePixelCount];
            long sequence = 0;
            bool hasPublishedFrame = emulator.Memory.Video.TryCopyPublishedFrame(pixels, ref sequence);
            string frameDigest = hasPublishedFrame
                ? Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(pixels.AsSpan())))
                : "unavailable";
            string output =
                $"Completed {framesToExecute} frame(s); PC={emulator.Cpu.PC:X4}; " +
                $"frame-sha256={frameDigest}";
            return new ConformanceResult(
                ConformanceOutcome.Passed,
                output,
                framesToExecute,
                ConformanceProtocol.SmokeFrames);
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

        private static string FormatTimeoutState(Nanoboy emulator, string serialOutput)
        {
            var output = new StringBuilder(serialOutput);
            if (TryReadBlarggProgress(emulator.Memory, out string progress)) {
                if (output.Length > 0) {
                    output.Append("; ");
                }
                output.Append("Blargg progress: ").Append(progress);
            }
            if (output.Length > 0) {
                output.Append("; ");
            }
            output.Append($"timeout PC={emulator.Cpu.PC:X4} SP={emulator.Cpu.SP:X4} ")
                .Append($"AF={emulator.Cpu.A:X2}{emulator.Cpu.F:X2} ")
                .Append($"BC={emulator.Cpu.B:X2}{emulator.Cpu.C:X2} ")
                .Append($"DE={emulator.Cpu.D:X2}{emulator.Cpu.E:X2} ")
                .Append($"HL={emulator.Cpu.H:X2}{emulator.Cpu.L:X2} ")
                .Append($"DIV={emulator.Memory.Timer.DIV:X2} NR52={emulator.Memory.Audio.ReadStatus():X2} ")
                .Append($"opcode={emulator.Memory.ReadByte(emulator.Cpu.PC):X2}");
            return output.ToString();
        }

        private static bool TryReadBlarggProgress(Memory memory, out string output)
        {
            output = string.Empty;
            if (memory.ReadByte(0xA001) != 0xDE ||
                memory.ReadByte(0xA002) != 0xB0 ||
                memory.ReadByte(0xA003) != 0x61) {
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
            output = message.ToString();
            return output.Length > 0;
        }

        private static bool HasMooneyePassSignature(CPU cpu) =>
            cpu.B == 3 &&
            cpu.C == 5 &&
            cpu.D == 8 &&
            cpu.E == 13 &&
            cpu.H == 21 &&
            cpu.L == 34;

        private static bool HasMooneyeFailSignature(CPU cpu) =>
            cpu.B == 0x42 &&
            cpu.C == 0x42 &&
            cpu.D == 0x42 &&
            cpu.E == 0x42 &&
            cpu.H == 0x42 &&
            cpu.L == 0x42;

        private static string FormatMooneyeSavedRegisters(Memory memory) =>
            $"; saved AF={memory.ReadHRAMDirect(1):X2}{memory.ReadHRAMDirect(0):X2}" +
            $" BC={memory.ReadHRAMDirect(3):X2}{memory.ReadHRAMDirect(2):X2}" +
            $" DE={memory.ReadHRAMDirect(5):X2}{memory.ReadHRAMDirect(4):X2}" +
            $" HL={memory.ReadHRAMDirect(7):X2}{memory.ReadHRAMDirect(6):X2}";

        private sealed class ConformanceSerialDevice : ISerialDevice
        {
            private readonly StringBuilder output = new StringBuilder();
            private byte transferByte;

            public string Output => output.ToString();
            public ConformanceOutcome? Outcome { get; private set; }
            public ConformanceProtocol Protocol { get; private set; } = ConformanceProtocol.SerialText;

            public void Start()
            {
                output.Append((char)transferByte);
                string current = output.ToString();
                if (current.Contains("Failed", StringComparison.OrdinalIgnoreCase)) {
                    Outcome = ConformanceOutcome.Failed;
                } else if (current.Contains("Passed", StringComparison.OrdinalIgnoreCase)) {
                    Outcome = ConformanceOutcome.Passed;
                } else if (EndsWithMooneyeSignature(current, 0x42, 0x42, 0x42, 0x42, 0x42, 0x42)) {
                    Protocol = ConformanceProtocol.MooneyeRegisters;
                    Outcome = ConformanceOutcome.Failed;
                } else if (EndsWithMooneyeSignature(current, 3, 5, 8, 13, 21, 34)) {
                    Protocol = ConformanceProtocol.MooneyeRegisters;
                    Outcome = ConformanceOutcome.Passed;
                }
            }

            private static bool EndsWithMooneyeSignature(string value, params byte[] signature)
            {
                if (value.Length < signature.Length) {
                    return false;
                }

                int offset = value.Length - signature.Length;
                for (int index = 0; index < signature.Length; index++) {
                    if (value[offset + index] != (char)signature[index]) {
                        return false;
                    }
                }
                return true;
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
