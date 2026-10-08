using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AetherBoy.Runtime;

internal enum GbaCheatOperation
{
    Assign, Indirect, Add, And, Or, Equal, NotEqual, Less, Greater,
    LessOrEqual, GreaterOrEqual, UnsignedLess, UnsignedGreater, SignedLess, SignedGreater, BitsSet, BitsClear, Button, Never
}

internal sealed record GbaCheatInstruction(GbaCheatOperation Operation, uint Address, uint Value, int Width)
{
    internal int Count { get; set; } = 1;
    internal uint AddressStep { get; set; }
    internal uint ValueStep { get; set; }
    internal int ElseCount { get; set; }
    internal bool IsCondition => Operation >= GbaCheatOperation.Equal;
}

internal readonly record struct GbaCheatRomPatch(uint Address, uint Value, int Width);

/// <summary>Compiles a whole device-code set before publishing any executable state.</summary>
internal sealed class GbaCheatProgram
{
    internal const int MaximumOperations = 65_536;
    internal readonly List<GbaCheatInstruction> Instructions = new();
    internal readonly List<GbaCheatRomPatch> Patches = new();
    internal uint? Hook;
    internal GbaCheatCipher Cipher = new();
    internal string Code = "";
    private int block = -1;
    private int otherwise = -1;
    private readonly List<string> normalized = new();
    private string[] lines = [];
    private int cursor;
    private Format format;
    private bool automaticFormat = true;
    private enum Format { Auto, CodeBreaker, CodeBreakerRaw, GameShark, GameSharkRaw, ActionReplay, ActionReplayRaw }
    private static readonly (string Prefix, Format Format)[] Prefixes =
    [
        ("CODEBREAKER:", Format.CodeBreaker), ("CB:", Format.CodeBreaker), ("CBRAW:", Format.CodeBreakerRaw),
        ("GAMESHARK:", Format.GameShark), ("GS:", Format.GameShark),
        ("GAMESHARKRAW:", Format.GameSharkRaw), ("GSRAW:", Format.GameSharkRaw),
        ("AR3:", Format.ActionReplay), ("PAR3:", Format.ActionReplay),
        ("AR3RAW:", Format.ActionReplayRaw), ("PAR3RAW:", Format.ActionReplayRaw)
    ];

    internal static GbaCheatProgram Compile(string code, GbaCheatProgram? previous = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (code.Length > 32_768) throw new FormatException("The cheat set is too large.");
        var result = new GbaCheatProgram { Cipher = previous?.Cipher.Clone() ?? new(), Hook = previous?.Hook };
        result.lines = code.Replace("\r", "", StringComparison.Ordinal).Split(['\n', ';', '+'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (result.lines.Length is 0 or > 512) throw new FormatException("Use between 1 and 512 code lines per set.");
        while (result.cursor < result.lines.Length)
        {
            int commandStart = result.cursor;
            try
            {
                string line = result.lines[result.cursor];
                // Explicit raw addresses retain their deliberately narrow RAM contract.
                string[] raw = line.Split([':', '=']);
                if (raw.Length == 2 && raw[0].Trim().Length == 8 && raw[1].Trim().Length is 2 or 4 or 8 &&
                    Hex(raw[0].Trim(), out uint address) && Hex(raw[1].Trim(), out uint value))
                {
                    int width = raw[1].Trim().Length / 2;
                    ValidateRam(address, width);
                    result.Write(address, value, width);
                    result.normalized.Add($"{address:X8}:{value.ToString($"X{width * 2}", CultureInfo.InvariantCulture)}");
                    result.cursor++;
                    continue;
                }
                (uint first, uint second) = result.NextLine();
                switch (result.format)
                {
                    case Format.CodeBreaker: case Format.CodeBreakerRaw: result.CodeBreaker(first, second); break;
                    case Format.ActionReplay: case Format.ActionReplayRaw: result.ActionReplay(first, second); break;
                    default: result.GameShark(first, second); break;
                }
            }
            catch (FormatException error)
            {
                if (!error.Data.Contains("CheatLine")) error.Data["CheatLine"] = Math.Max(commandStart + 1, result.cursor);
                throw;
            }
        }
        result.CloseBlock();
        long operations = result.Instructions.Sum(i => i.IsCondition ? 1L : i.Count);
        if (operations > MaximumOperations) throw new FormatException("The cheat set exceeds 65536 operations per pass.");
        for (int i = 0; i < result.Instructions.Count; i++)
            if (result.Instructions[i].IsCondition && i + result.Instructions[i].Count + result.Instructions[i].ElseCount >= result.Instructions.Count)
                throw new FormatException("A conditional code is missing its following commands.");
        result.Code = string.Join(" + ", result.normalized);
        return result;
    }

    private (uint, uint) NextLine(bool continuation = false)
    {
        if (cursor >= lines.Length) throw new FormatException("A multi-line code is incomplete.");
        string text = lines[cursor++].ToUpperInvariant();
        Format selected = format;
        foreach (var item in Prefixes)
            if (text.StartsWith(item.Prefix, StringComparison.Ordinal))
            { selected = item.Format; automaticFormat = false; text = text[item.Prefix.Length..]; break; }
        if (continuation && selected != format) throw new FormatException("A continuation must use the same code format.");
        string compact = string.Concat(text.Where(c => !char.IsWhiteSpace(c) && c != '-'));
        if (compact.Length is not (12 or 16) || !Hex(compact[..8], out uint first) || !Hex(compact[8..], out uint second))
            throw new FormatException("Use an eight-digit address and a four- or eight-digit value.");
        // Auto may cross device families between complete commands (for example an
        // AR master followed by plain CodeBreaker money writes), never mid-command.
        if (automaticFormat && !continuation &&
            (selected == Format.Auto || (compact.Length == 12) != (selected == Format.CodeBreaker)))
            selected = Format.Auto;
        if (selected == Format.Auto)
        {
            if (compact.Length == 12) selected = Format.CodeBreaker;
            else selected = DetectWideFormat(cursor - 1);
        }
        bool breaker = selected is Format.CodeBreaker or Format.CodeBreakerRaw;
        if (compact.Length != (breaker ? 12 : 16)) throw new FormatException("The code length does not match the selected format.");
        format = selected;
        string prefix = selected switch
        {
            Format.CodeBreaker => "CB", Format.CodeBreakerRaw => "CBRAW", Format.GameShark => "GS",
            Format.GameSharkRaw => "GSRAW", Format.ActionReplay => "AR3", _ => "AR3RAW"
        };
        normalized.Add($"{prefix}:{first:X8} {second.ToString(breaker ? "X4" : "X8", CultureInfo.InvariantCulture)}");
        return selected switch
        {
            Format.CodeBreaker => Cipher.DecodeCodeBreaker(first, second),
            Format.GameShark => Cipher.DecodeTea(first, second, false),
            Format.ActionReplay => Cipher.DecodeTea(first, second, true),
            _ => (first, second)
        };
    }

    private Format DetectWideFormat(int start)
    {
        // Validate an entire same-width run on isolated compiler/cipher state.
        // A plausible first opcode alone cannot identify an encrypted device code.
        var run = new List<string>();
        for (int index = start; index < lines.Length; index++)
        {
            string line = lines[index];
            if (line.Contains(':') || line.Contains('=')) break;
            string compact = string.Concat(line.Where(c => !char.IsWhiteSpace(c) && c != '-'));
            if (compact.Length != 16) break;
            run.Add(line);
        }
        string code = string.Join(" + ", run);
        var matches = new List<Format>();
        // RAW is deliberately opt-in. Ciphertext can also look like valid raw
        // instructions, which is not evidence that the user intended RAW.
        foreach (var candidate in new[] { ("GS:", Format.GameShark), ("AR3:", Format.ActionReplay) })
        {
            try { _ = Compile(candidate.Item1 + code, this); }
            catch (FormatException) { continue; }
            matches.Add(candidate.Item2);
        }
        if (matches.Count > 1)
        {
            var error = new FormatException("Both GameShark v1/v2 and Action Replay v3 match this set. Select the format stated by your source; they can change different memory addresses.");
            error.Data["CheatCandidates"] = new[] { CheatCodeFormat.GameShark, CheatCodeFormat.ActionReplayV3 };
            error.Data["CheatLine"] = start + 1;
            throw error;
        }
        return matches.Count == 1 ? matches[0] : throw new FormatException("No supported format matches the complete code set. Select the source format and check every line.");
    }

    private void CodeBreaker(uint first, uint second)
    {
        uint address = first & 0x0FFFFFFF;
        switch (first >> 28)
        {
            case 0: return; // Device ID/checksum metadata, not a compatibility assertion.
            case 1: SetHook(0x08000000 | (first & 0x01FFFFFE)); return;
            case 9: Cipher.ReseedCodeBreaker(first, second); return;
            case 4:
                var fill = NextLine(true);
                Write(address, second, 2, (int)(fill.Item1 & 0xFFFF), fill.Item2, fill.Item1 >> 16);
                return;
            case 5:
                if (second == 0) throw new FormatException("A CodeBreaker list must contain at least one halfword.");
                int remaining = (int)second;
                while (remaining > 0)
                {
                    var data = NextLine(true);
                    uint[] values = [data.Item1 >> 16, data.Item1 & 0xFFFF, data.Item2];
                    foreach (uint item in values)
                    {
                        if (remaining-- <= 0) break;
                        Write(address, ((item & 255) << 8) | (item >> 8), 2);
                        address += 2;
                    }
                }
                return;
            case 0xD:
                if (address != 0x20) throw new FormatException("This CodeBreaker D command is undefined.");
                Condition(GbaCheatOperation.BitsClear, 0x04000130, second, 2); return;
        }
        GbaCheatOperation operation = (first >> 28) switch
        {
            2 => GbaCheatOperation.Or, 3 or 8 => GbaCheatOperation.Assign, 6 => GbaCheatOperation.And,
            7 => GbaCheatOperation.Equal, 10 => GbaCheatOperation.NotEqual, 11 => GbaCheatOperation.Greater,
            12 => GbaCheatOperation.Less, 14 => GbaCheatOperation.Add, 15 => GbaCheatOperation.BitsSet,
            _ => throw new FormatException("Unknown CodeBreaker command.")
        };
        int width = first >> 28 == 3 ? 1 : 2;
        if (operation >= GbaCheatOperation.Equal) Condition(operation, address, second, width);
        else Write(address, second, width, operation: operation);
    }

    private void GameShark(uint first, uint second)
    {
        if (second == 0x001DC0DE) return;
        if (first == 0xDEADFACE) { Cipher.ReseedTea(second, false); return; }
        uint address = first & 0x0FFFFFFF;
        switch (first >> 28)
        {
            case 0: case 1: case 2: Write(address, second, 1 << (int)(first >> 28)); return;
            case 3:
                int count = (int)(first & 0xFFFF);
                if (count < 1) throw new FormatException("A GameShark address list must not be empty.");
                // The original v1 device also writes the value to itself as an address.
                // Invalid bus targets of that hardware quirk are a no-op.
                if (IsBusAddress(second, 4, true)) Write(second, second, 4);
                else Instructions.Add(new(GbaCheatOperation.Assign, 0, 0, 0) { Count = 0 });
                count--;
                while (count > 0)
                {
                    var pair = NextLine(true);
                    Write(pair.Item1, second, 4); count--;
                    if (count > 0) { Write(pair.Item2, second, 4); count--; }
                }
                return;
            case 6: Patch(0x08000000 | ((first & 0xFFFFFF) << 1), second, 2); return;
            case 8:
                int size = (int)((first >> 20) & 15);
                if (size is not (1 or 2)) throw new FormatException("Unknown GameShark button command.");
                Instructions.Add(new(GbaCheatOperation.Button, 0, 0, 0));
                Write(first & 0x0F0FFFFF, second, size); return;
            case 0xD:
                var comparison = (second >> 20) switch
                {
                    0 => GbaCheatOperation.Equal, 1 => GbaCheatOperation.NotEqual,
                    2 => GbaCheatOperation.LessOrEqual, 3 => GbaCheatOperation.GreaterOrEqual,
                    _ => throw new FormatException("Unknown GameShark comparison.")
                };
                Condition(comparison, address, second & 0xFFFF, 2); return;
            case 0xE: Condition(GbaCheatOperation.Equal, second & 0x0FFFFFFF, first & 0xFFFF, 2, (int)((first >> 16) & 255)); return;
            case 0xF: SetHook(0x08000000 | (first & 0x01FFFFFE)); return;
            default: throw new FormatException("Unknown GameShark v1/v2 command.");
        }
    }

    private void ActionReplay(uint first, uint second)
    {
        if (second == 0x001DC0DE) return;
        if (first == 0xDEADFACE) { Cipher.ReseedTea(second, true); return; }
        if ((first >> 24) == 0xC4) { SetHook(0x08000000 | (first & 0x01FFFFFE)); return; }
        if (first == 0) { ReplaySpecial(second); return; }
        uint address = ReplayAddress(first);
        int width = 1 << (int)((first >> 25) & 3);
        uint condition = (first >> 27) & 7;
        if (condition != 0)
        {
            uint action = first >> 30;
            if (action == 3) throw new FormatException("Action Replay disable-code conditions are not implemented by this profile.");
            if (action == 2) CloseBlock();
            var operation = condition switch
            {
                1 => GbaCheatOperation.Equal, 2 => GbaCheatOperation.NotEqual,
                3 => GbaCheatOperation.SignedLess, 4 => GbaCheatOperation.SignedGreater,
                5 => GbaCheatOperation.UnsignedLess, 6 => GbaCheatOperation.UnsignedGreater,
                _ => GbaCheatOperation.BitsSet
            };
            if (width == 8) { operation = GbaCheatOperation.Never; width = 0; }
            Condition(operation, address, second, width, action == 1 ? 2 : 1);
            if (action == 2) block = Instructions.Count - 1;
            return;
        }
        if ((first & 0x01000000) != 0 && (first & 0xFE000000) != 0xC6000000)
            throw new FormatException("Invalid Action Replay operation bit.");
        if (first >> 30 == 3)
        {
            if ((first & 0xFE000000) != 0xC6000000) throw new FormatException("Unknown Action Replay I/O command.");
            Write(0x04000000 | (first & 0x00FFFFFF), second, 2 << (int)((first >> 24) & 1)); return;
        }
        if (width > 4) throw new FormatException("Invalid Action Replay width.");
        switch (first >> 30)
        {
            case 0:
                uint repetitions = width < 4 ? (second >> (width * 8)) + 1 : 1;
                if (repetitions > MaximumOperations) throw new FormatException("The repeated write is too large.");
                Write(address, second, width, (int)repetitions, (uint)width); return;
            case 1:
                ValidateBus(address, 4, false);
                Instructions.Add(new(GbaCheatOperation.Indirect, address, Mask(second, width), width)
                    { AddressStep = width < 4 ? (second >> (width * 8)) * (uint)width : 0 }); return;
            case 2: Write(address, second, width, operation: GbaCheatOperation.Add); return;
        }
    }

    private void ReplaySpecial(uint value)
    {
        uint kind = value >> 24;
        if (value == 0) { CloseBlock(); return; }
        if (kind == 0x40) { if (block < 0) throw new FormatException("ENDIF has no matching block."); CloseBlock(); return; }
        if (kind == 0x60)
        {
            if (block < 0 || otherwise >= 0) throw new FormatException("ELSE has no matching block.");
            otherwise = Instructions.Count; return;
        }
        if (kind is 0x18 or 0x1A or 0x1C or 0x1E)
        {
            var next = NextLine(true); Patch(0x08000000 | ((value & 0xFFFFFF) << 1), next.Item1, 2); return;
        }
        bool button = kind is 0x10 or 0x12 or 0x14;
        bool fill = kind is 0x80 or 0x82 or 0x84;
        if (!button && !fill) throw new FormatException("Unknown or unsupported Action Replay special command (including slowdown).");
        int width = 1 << (int)((value >> 25) & 3);
        var data = NextLine(true);
        if (button) Instructions.Add(new(GbaCheatOperation.Button, 0, 0, 0));
        Write(ReplayAddress(value), data.Item1, width, button ? 1 : (int)((data.Item2 >> 16) & 255),
            button ? 0 : (data.Item2 & 0xFFFF) * (uint)width, button ? 0 : data.Item2 >> 24);
    }

    private void CloseBlock()
    {
        if (block < 0) return;
        int end = Instructions.Count;
        Instructions[block].Count = (otherwise >= 0 ? otherwise : end) - block - 1;
        Instructions[block].ElseCount = otherwise >= 0 ? end - otherwise : 0;
        block = otherwise = -1;
    }

    private void SetHook(uint address)
    {
        // A newly supplied master may replace a master inherited from the previous set.
        Hook = address;
    }

    private void Condition(GbaCheatOperation op, uint address, uint value, int width, int count = 1)
    {
        if (width != 0) ValidateBus(address, width, false);
        if (count < 1) throw new FormatException("A condition must control at least one command.");
        Instructions.Add(new(op, address, width == 0 ? 0 : Mask(value, width), width) { Count = count });
    }

    private void Write(uint address, uint value, int width, int count = 1, uint step = 0, uint valueStep = 0,
        GbaCheatOperation operation = GbaCheatOperation.Assign)
    {
        if (count is < 0 or > MaximumOperations) throw new FormatException("The repeated write is too large.");
        ValidateBus(address, width, true);
        for (int index = 1; index < count; index++)
        {
            ulong next = (ulong)address + (ulong)(uint)index * step;
            if (next > uint.MaxValue) throw new FormatException("The write crosses the address space.");
            ValidateBus((uint)next, width, true);
        }
        Instructions.Add(new(operation, address, Mask(value, width), width) { Count = count, AddressStep = step, ValueStep = valueStep });
    }

    private void Patch(uint address, uint value, int width) => Patches.Add(new(address, Mask(value, width), width));
    internal static uint Mask(uint value, int width) => width switch { 1 => value & 255, 2 => value & 65535, _ => value };
    private static uint ReplayAddress(uint value) => (value & 0xFFFFF) | ((value << 4) & 0x0F000000);
    private static bool Hex(string value, out uint result) => uint.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out result);
    internal static bool IsRam(uint address, int width) => Aligned(address, width) &&
        ((address >= 0x02000000 && (ulong)address + (uint)width <= 0x02040000) ||
         (address >= 0x03000000 && (ulong)address + (uint)width <= 0x03008000));
    private static bool Aligned(uint address, int width) => width is 1 or 2 or 4 && (address & (uint)(width - 1)) == 0;
    private static void ValidateRam(uint address, int width)
    { if (!IsRam(address, width)) throw new FormatException("Raw patches require aligned EWRAM or IWRAM addresses."); }

    internal static bool IsBusAddress(uint address, int width, bool write)
    {
        if (!Aligned(address, width)) return false;
        ulong end = (ulong)address + (uint)width;
        return IsRam(address, width) ||
            (address >= 0x04000000 && end <= 0x04000400) || (address >= 0x05000000 && end <= 0x05000400) ||
            (address >= 0x06000000 && end <= 0x06018000) || (address >= 0x07000000 && end <= 0x07000400) ||
            (address >= 0x0E000000 && end <= 0x0E010000) || (!write && address >= 0x08000000 && end <= 0x0E000000);
    }
    private static void ValidateBus(uint address, int width, bool write)
    { if (!IsBusAddress(address, width, write)) throw new FormatException("The code targets an invalid or unaligned GBA memory address."); }
}
