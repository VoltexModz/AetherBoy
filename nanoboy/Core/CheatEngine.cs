using System;
using System.Collections.Generic;
using System.Globalization;

namespace nanoboy.Core
{
    public class CheatItem
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public bool Enabled { get; set; }
        public ushort Address { get; set; }
        public byte Value { get; set; }
        public byte CompareValue { get; set; }
        public bool HasCompareValue { get; set; }
        public bool IsGameGenie { get; set; }
    }

    public class CheatEngine
    {
        public List<CheatItem> Cheats { get; } = new List<CheatItem>();

        public bool AddCheat(string name, string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            string formatted = code.Replace(" ", "", StringComparison.Ordinal).Trim().ToUpperInvariant();
            // GB CodeBreaker has an explicit separator before its byte value;
            // distinguish it from GameShark before removing separators.
            if (formatted.Length == 9 && formatted.StartsWith("00", StringComparison.Ordinal) && formatted[6] == '-')
                return AddMemoryWrite(name, code, formatted.AsSpan(2, 4), formatted.AsSpan(7, 2));
            if (formatted.Length == 7 && formatted[4] == ':')
                return AddMemoryWrite(name, code, formatted.AsSpan(0, 4), formatted.AsSpan(5, 2));
            string cleanCode = code.Replace("-", "", StringComparison.Ordinal)
                .Replace(" ", "", StringComparison.Ordinal).Trim().ToUpperInvariant();

            // GameShark: 8 Hex Chars e.g. 01XXYYZZ -> Value XX at Address ZZYY
            if (cleanCode.Length == 8 && cleanCode.StartsWith("01", StringComparison.Ordinal))
            {
                if (byte.TryParse(cleanCode.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte val) &&
                    byte.TryParse(cleanCode.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte addrLsb) &&
                    byte.TryParse(cleanCode.AsSpan(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte addrMsb))
                {
                    ushort addr = (ushort)((addrMsb << 8) | addrLsb);
                    Cheats.Add(new CheatItem
                    {
                        Name = name,
                        Code = code,
                        Enabled = true,
                        Address = addr,
                        Value = val,
                        IsGameGenie = false
                    });
                    return true;
                }
            }

            // Game Genie changes cartridge ROM reads. Both six- and nine-digit codes are valid.
            if (cleanCode.Length is 6 or 9 &&
                ushort.TryParse(cleanCode.AsSpan(0, 3), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort first) &&
                ushort.TryParse(cleanCode.AsSpan(3, 3), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort second))
            {
                ushort address = (ushort)(((second & 0xF) ^ 0xF) << 12 |
                    (first & 0xF) << 8 | (second >> 4));
                byte compare = 0;
                bool hasCompare = cleanCode.Length == 9;
                if (hasCompare)
                {
                    if (!ushort.TryParse(cleanCode.AsSpan(6, 3), NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture, out ushort third)) return false;
                    uint scrambled = ((uint)(third & 0xF00) << 20) | (uint)(third & 0xF);
                    uint rotated = (scrambled >> 2) | (scrambled << 30);
                    compare = (byte)((rotated | (rotated >> 24)) ^ 0xBA);
                }

                Cheats.Add(new CheatItem
                {
                    Name = name,
                    Code = code,
                    Enabled = true,
                    Address = address,
                    Value = (byte)(first >> 4),
                    CompareValue = compare,
                    HasCompareValue = hasCompare,
                    IsGameGenie = true
                });
                return true;
            }

            return false;
        }

        private bool AddMemoryWrite(string name, string code, ReadOnlySpan<char> addressText, ReadOnlySpan<char> valueText)
        {
            if (!ushort.TryParse(addressText, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort address) ||
                !byte.TryParse(valueText, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte value)) return false;
            Cheats.Add(new CheatItem { Name = name, Code = code, Enabled = true, Address = address, Value = value });
            return true;
        }

        public byte ApplyRomRead(int address, byte original)
        {
            foreach (CheatItem cheat in Cheats)
                if (cheat.Enabled && cheat.IsGameGenie && cheat.Address == address &&
                    (!cheat.HasCompareValue || cheat.CompareValue == original))
                    return cheat.Value;
            return original;
        }

        public void ApplyCheats(Memory memory)
        {
            if (memory == null) return;

            foreach (var cheat in Cheats)
            {
                if (!cheat.Enabled) continue;

                if (!cheat.IsGameGenie)
                {
                    // GameShark RAM patch
                    try
                    {
                        memory.WriteByte(cheat.Address, cheat.Value);
                    }
                    catch { }
                }
            }
        }
    }
}
