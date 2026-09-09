using System;
using System.Collections.Generic;
using System.Globalization;
using GameboyAdvanced.Core;

namespace AetherBoy.Runtime
{
    internal sealed class GbaCheatEngine
    {
        private enum Operation
        {
            Assign,
            Or,
            And,
            Add,
            IfEqual,
            IfNotEqual,
            IfGreater,
            IfLess,
            IfAnd
        }

        private readonly record struct Instruction(Operation Operation, uint Address, uint Value, int Width);

        private sealed class Entry
        {
            public required Guid Id { get; init; }
            public required string Name { get; init; }
            public required string Code { get; init; }
            public required Instruction[] Instructions { get; init; }
            public bool Enabled { get; set; } = true;
        }

        private readonly List<Entry> entries = new();

        public CheatSnapshot Add(string name, string code)
        {
            (Instruction[] instructions, string normalized) = ParseProgram(code);
            var entry = new Entry
            {
                Id = Guid.NewGuid(),
                Name = string.IsNullOrWhiteSpace(name) ? "Cheat" : name.Trim(),
                Code = normalized,
                Instructions = instructions
            };
            entries.Add(entry);
            return ToSnapshot(entry);
        }

        public bool Remove(Guid id)
        {
            int index = entries.FindIndex(entry => entry.Id == id);
            if (index < 0)
                return false;
            entries.RemoveAt(index);
            return true;
        }

        public bool Toggle(Guid id)
        {
            Entry? entry = entries.Find(entry => entry.Id == id);
            if (entry is null)
                return false;
            entry.Enabled = !entry.Enabled;
            return entry.Enabled;
        }

        public void Apply(Device device)
        {
            ArgumentNullException.ThrowIfNull(device);
            foreach (Entry entry in entries)
            {
                if (!entry.Enabled)
                    continue;

                bool executeNext = true;
                foreach (Instruction instruction in entry.Instructions)
                {
                    if (IsCondition(instruction.Operation))
                    {
                        if (!executeNext)
                        {
                            executeNext = true;
                            continue;
                        }
                        executeNext = EvaluateCondition(device, instruction);
                        continue;
                    }

                    if (executeNext)
                        ApplyInstruction(device, instruction);
                    executeNext = true;
                }
            }
        }

        public CheatSnapshot[] CaptureSnapshots()
        {
            var result = new CheatSnapshot[entries.Count];
            for (int index = 0; index < entries.Count; index++)
                result[index] = ToSnapshot(entries[index]);
            return result;
        }

        // Compatibility helper for callers which expect one direct RAM patch.
        internal static (uint Address, uint Value, int Width, string Normalized) Parse(string code)
        {
            (Instruction[] instructions, string normalized) = ParseProgram(code);
            if (instructions.Length != 1 || instructions[0].Operation != Operation.Assign)
                throw new FormatException("The code is a program rather than one direct GBA RAM patch.");
            Instruction instruction = instructions[0];
            return (instruction.Address, instruction.Value, instruction.Width, normalized);
        }

        private static (Instruction[] Instructions, string Normalized) ParseProgram(string code)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(code);
            string[] lines = code.Replace("\r", string.Empty, StringComparison.Ordinal)
                .Split(new[] { '\n', ';', '+' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length is 0 or > 32)
                throw new FormatException("A GBA cheat may contain between 1 and 32 code lines.");

            var instructions = new List<Instruction>(lines.Length);
            var normalized = new List<string>(lines.Length);
            foreach (string sourceLine in lines)
                ParseLine(sourceLine, instructions, normalized);

            if (instructions.Count == 0)
                throw new FormatException("The cheat does not contain an executable GBA code.");
            if (IsCondition(instructions[^1].Operation))
                throw new FormatException("A conditional GBA code must be followed by a write code.");
            return (instructions.ToArray(), string.Join(" + ", normalized));
        }

        private static void ParseLine(string sourceLine, ICollection<Instruction> instructions, ICollection<string> normalized)
        {
            string line = sourceLine.Trim();
            if (line.Contains(':') || line.Contains('='))
            {
                string compactRaw = line.Replace(" ", string.Empty, StringComparison.Ordinal);
                string[] rawParts = compactRaw.Split(new[] { ':', '=' }, StringSplitOptions.RemoveEmptyEntries);
                if (rawParts.Length == 2 && rawParts[0].Length == 8 && rawParts[1].Length is 2 or 4 or 8 &&
                    TryHex(rawParts[0], out uint rawAddress) && TryHex(rawParts[1], out uint rawValue))
                {
                    int width = rawParts[1].Length / 2;
                    ValidateWritableRam(rawAddress, width);
                    instructions.Add(new Instruction(Operation.Assign, rawAddress, rawValue, width));
                    normalized.Add($"{rawAddress:X8}:{rawValue.ToString($"X{width * 2}", CultureInfo.InvariantCulture)}");
                    return;
                }
            }

            string upper = line.ToUpperInvariant();
            bool forceCodeBreaker = RemovePrefix(ref upper, "CB:") || RemovePrefix(ref upper, "CODEBREAKER:");
            bool forceEncryptedGameShark = RemovePrefix(ref upper, "GS:") || RemovePrefix(ref upper, "GAMESHARK:");
            bool forceRawGameShark = RemovePrefix(ref upper, "GSRAW:") || RemovePrefix(ref upper, "GAMESHARKRAW:");
            string compact = upper.Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal);

            if (forceCodeBreaker || (!forceEncryptedGameShark && !forceRawGameShark && compact.Length == 12))
            {
                if (compact.Length != 12 || !TryHex(compact[..8], out uint op1) || !TryHex(compact[8..], out uint op2))
                    throw UnsupportedFormat();
                instructions.Add(ParseCodeBreaker(op1, op2));
                normalized.Add($"CB:{op1:X8} {op2:X4}");
                return;
            }

            if (compact.Length != 16 || !TryHex(compact[..8], out uint gameSharkOp1) ||
                !TryHex(compact[8..], out uint gameSharkOp2))
                throw UnsupportedFormat();

            Instruction? raw = TryParseGameShark(gameSharkOp1, gameSharkOp2);
            (uint decryptedOp1, uint decryptedOp2) = DecryptGameShark(gameSharkOp1, gameSharkOp2);
            Instruction? decrypted = TryParseGameShark(decryptedOp1, decryptedOp2);
            Instruction selected;
            string prefix;

            if (forceRawGameShark)
            {
                selected = raw ?? throw new FormatException("The raw GameShark code is not a supported EWRAM/IWRAM write.");
                prefix = "GSRAW:";
            }
            else if (forceEncryptedGameShark)
            {
                selected = decrypted ?? throw new FormatException("The encrypted GameShark code does not decode to a supported EWRAM/IWRAM write.");
                prefix = "GS:";
            }
            else if (raw.HasValue && !decrypted.HasValue)
            {
                selected = raw.Value;
                prefix = "GSRAW:";
            }
            else if (decrypted.HasValue && !raw.HasValue)
            {
                selected = decrypted.Value;
                prefix = "GS:";
            }
            else if (raw.HasValue && decrypted.HasValue)
            {
                throw new FormatException("This GameShark code is ambiguous. Prefix it with GS: (encrypted) or GSRAW: (decoded).");
            }
            else
            {
                throw new FormatException("Only GameShark v1/v2 RAM writes to EWRAM/IWRAM are supported.");
            }

            instructions.Add(selected);
            normalized.Add($"{prefix}{gameSharkOp1:X8} {gameSharkOp2:X8}");
        }

        private static Instruction ParseCodeBreaker(uint op1, uint op2)
        {
            uint address = op1 & 0x0FFF_FFFF;
            Operation operation;
            int width;
            switch (op1 >> 28)
            {
                case 2: operation = Operation.Or; width = 2; break;
                case 3: operation = Operation.Assign; width = 1; break;
                case 6: operation = Operation.And; width = 2; break;
                case 7: operation = Operation.IfEqual; width = 2; break;
                case 8: operation = Operation.Assign; width = 2; break;
                case 0xA: operation = Operation.IfNotEqual; width = 2; break;
                case 0xB: operation = Operation.IfGreater; width = 2; break;
                case 0xC: operation = Operation.IfLess; width = 2; break;
                case 0xE: operation = Operation.Add; width = 2; break;
                case 0xF: operation = Operation.IfAnd; width = 2; break;
                default:
                    throw new FormatException("This CodeBreaker command type is not supported yet. Direct, logical and conditional RAM codes are supported.");
            }

            ValidateWritableRam(address, width);
            return new Instruction(operation, address, width == 1 ? op2 & 0xFFu : op2 & 0xFFFFu, width);
        }

        private static Instruction? TryParseGameShark(uint op1, uint op2)
        {
            int width = (op1 >> 28) switch { 0 => 1, 1 => 2, 2 => 4, _ => 0 };
            if (width == 0)
                return null;
            uint address = op1 & 0x0FFF_FFFF;
            if (!IsWritableRam(address, width))
                return null;
            uint value = width switch { 1 => op2 & 0xFFu, 2 => op2 & 0xFFFFu, _ => op2 };
            return new Instruction(Operation.Assign, address, value, width);
        }

        private static (uint Op1, uint Op2) DecryptGameShark(uint op1, uint op2)
        {
            uint sum = 0xC6EF_3720;
            const uint delta = 0x9E37_79B9;
            ReadOnlySpan<uint> seed = [0x09F4_FBBD, 0x9681_884A, 0x3520_27E9, 0xF3DE_E5A7];
            unchecked
            {
                for (int round = 0; round < 32; round++)
                {
                    op2 -= ((op1 << 4) + seed[2]) ^ (op1 + sum) ^ ((op1 >> 5) + seed[3]);
                    op1 -= ((op2 << 4) + seed[0]) ^ (op2 + sum) ^ ((op2 >> 5) + seed[1]);
                    sum -= delta;
                }
            }
            return (op1, op2);
        }

        private static void ApplyInstruction(Device device, Instruction instruction)
        {
            uint current = Read(device, instruction.Address, instruction.Width);
            uint value = instruction.Operation switch
            {
                Operation.Assign => instruction.Value,
                Operation.Or => current | instruction.Value,
                Operation.And => current & instruction.Value,
                Operation.Add => current + instruction.Value,
                _ => current
            };
            Write(device, instruction.Address, value, instruction.Width);
        }

        private static bool EvaluateCondition(Device device, Instruction instruction)
        {
            uint current = Read(device, instruction.Address, instruction.Width);
            return instruction.Operation switch
            {
                Operation.IfEqual => current == instruction.Value,
                Operation.IfNotEqual => current != instruction.Value,
                Operation.IfGreater => current > instruction.Value,
                Operation.IfLess => current < instruction.Value,
                Operation.IfAnd => (current & instruction.Value) != 0,
                _ => true
            };
        }

        private static uint Read(Device device, uint address, int width) => width switch
        {
            1 => device.InspectByte(address),
            2 => device.InspectHalfWord(address),
            _ => device.InspectWord(address)
        };

        private static void Write(Device device, uint address, uint value, int width)
        {
            if (width == 1)
                device.PokeByte(address, (byte)value);
            else if (width == 2)
                device.PokeHalfWord(address, (ushort)value);
            else
                device.PokeWord(address, value);
        }

        private static bool IsCondition(Operation operation) => operation is
            Operation.IfEqual or Operation.IfNotEqual or Operation.IfGreater or Operation.IfLess or Operation.IfAnd;

        private static bool RemovePrefix(ref string text, string prefix)
        {
            if (!text.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            text = text[prefix.Length..].Trim();
            return true;
        }

        private static bool TryHex(string text, out uint value) =>
            uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

        private static FormatException UnsupportedFormat() => new(
            "Use ADDRESS:VALUE, CodeBreaker XXXXXXXX XXXX, or GameShark XXXXXXXX XXXXXXXX. Separate multi-line codes with + or ;.");

        private static void ValidateWritableRam(uint address, int width)
        {
            if (!IsWritableRam(address, width))
                throw new FormatException("GBA cheats may only target aligned EWRAM or IWRAM addresses.");
        }

        private static bool IsWritableRam(uint address, int width)
        {
            if ((address & (uint)(width - 1)) != 0)
                return false;
            ulong end = (ulong)address + (uint)width - 1;
            return (address >= 0x0200_0000 && end <= 0x0203_FFFF) ||
                (address >= 0x0300_0000 && end <= 0x0300_7FFF);
        }

        private static CheatSnapshot ToSnapshot(Entry entry) =>
            new(entry.Id, entry.Name, entry.Code, entry.Enabled);
    }
}
