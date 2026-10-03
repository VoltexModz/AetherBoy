using System;
using System.Collections.Generic;
using System.Linq;
using GameboyAdvanced.Core;

namespace AetherBoy.Runtime;

/// <summary>Owner-thread cheat execution, device-button state and reversible in-memory ROM patches.</summary>
internal sealed class GbaCheatEngine
{
    private sealed class Entry
    {
        internal Guid Id = Guid.NewGuid();
        internal required string Name;
        internal required GbaCheatProgram Program;
        internal bool Enabled = true;
    }

    private readonly List<Entry> entries = new();
    private readonly Dictionary<int, byte> originalRom = new();
    private readonly HashSet<uint> hookAddresses = new();
    private Device? attached;
    internal bool ButtonPressed { get; set; }

    internal void Attach(Device device)
    {
        if (attached is not null && attached != device) throw new InvalidOperationException("A cheat engine belongs to one GBA device.");
        attached = device;
        RefreshRomAndHooks();
    }

    public CheatSnapshot Add(string name, string code)
    {
        if (entries.Count >= 128) throw new FormatException("At most 128 cheat sets can be active in one session.");
        GbaCheatProgram program = GbaCheatProgram.Compile(code, entries.LastOrDefault()?.Program);
        if (attached is not null)
            foreach (var patch in program.Patches)
                if ((ulong)(patch.Address & 0x01FFFFFF) + (uint)patch.Width > (uint)attached.Gamepak.Data.Length)
                    throw new FormatException("The ROM patch lies outside the loaded cartridge.");
        var entry = new Entry { Name = string.IsNullOrWhiteSpace(name) ? "Cheat" : name.Trim(), Program = program };
        entries.Add(entry);
        RefreshRomAndHooks();
        return Snapshot(entry);
    }

    public bool Remove(Guid id)
    {
        int index = entries.FindIndex(entry => entry.Id == id);
        if (index < 0) return false;
        entries.RemoveAt(index);
        RefreshRomAndHooks();
        return true;
    }

    public bool Toggle(Guid id)
    {
        Entry? entry = entries.Find(entry => entry.Id == id);
        if (entry is null) return false;
        entry.Enabled = !entry.Enabled;
        RefreshRomAndHooks();
        return entry.Enabled;
    }

    private void RefreshRomAndHooks()
    {
        if (attached is null) return;
        foreach (var original in originalRom) attached.Gamepak.Data[original.Key] = original.Value;
        originalRom.Clear();
        hookAddresses.Clear();
        foreach (Entry entry in entries)
        {
            if (!entry.Enabled) continue;
            if (entry.Program.Hook is uint hook) hookAddresses.Add(hook);
            foreach (var patch in entry.Program.Patches)
            {
                int offset = (int)(patch.Address & 0x01FFFFFF);
                for (int i = 0; i < patch.Width; i++)
                {
                    originalRom.TryAdd(offset + i, attached.Gamepak.Data[offset + i]);
                    attached.Gamepak.Data[offset + i] = (byte)(patch.Value >> (i * 8));
                }
            }
        }
        attached.Cpu.InstructionStarting = hookAddresses.Count == 0 ? null : OnInstruction;
    }

    private void OnInstruction(uint address, bool thumb)
    {
        if (!thumb || !hookAddresses.Contains(address) || attached is null) return;
        foreach (Entry entry in entries)
            if (entry.Enabled && entry.Program.Hook == address) Execute(attached, entry.Program);
    }

    public void Apply(Device device)
    {
        if (attached is null) Attach(device);
        foreach (Entry entry in entries)
            if (entry.Enabled && entry.Program.Hook is null) Execute(device, entry.Program);
    }

    private void Execute(Device device, GbaCheatProgram program)
    {
        // A branch controls decoded commands, not the number of writes in a fill.
        var elseRanges = new List<(int Start, int End)>();
        for (int pc = 0; pc < program.Instructions.Count;)
        {
            for (int n = elseRanges.Count - 1; n >= 0; n--)
            {
                var range = elseRanges[n];
                if (pc < range.Start) continue;
                if (pc < range.End) pc = range.End;
                elseRanges.RemoveAt(n);
            }
            if (pc >= program.Instructions.Count) break;
            GbaCheatInstruction instruction = program.Instructions[pc];
            if (instruction.IsCondition)
            {
                if (!Test(device, instruction)) { pc += instruction.Count + 1; continue; }
                if (instruction.ElseCount > 0)
                    elseRanges.Add((pc + instruction.Count + 1, pc + instruction.Count + instruction.ElseCount + 1));
            }
            else
            {
                for (int repeat = 0; repeat < instruction.Count; repeat++)
                {
                    uint address = instruction.Address + (uint)repeat * instruction.AddressStep;
                    uint value = unchecked(instruction.Value + (uint)repeat * instruction.ValueStep);
                    if (instruction.Operation == GbaCheatOperation.Indirect)
                    {
                        ulong target = (ulong)Read(device, instruction.Address, 4) + instruction.AddressStep;
                        if (target > uint.MaxValue || !GbaCheatProgram.IsBusAddress((uint)target, instruction.Width, true)) continue;
                        address = (uint)target;
                    }
                    uint current = instruction.Operation is GbaCheatOperation.Add or GbaCheatOperation.And or GbaCheatOperation.Or
                        ? Read(device, address, instruction.Width) : 0;
                    value = instruction.Operation switch
                    {
                        GbaCheatOperation.Add => unchecked(current + value),
                        GbaCheatOperation.And => current & value,
                        GbaCheatOperation.Or => current | value,
                        _ => value
                    };
                    if (instruction.Width != 0) device.WriteCheatMemory(address, value, instruction.Width);
                }
            }
            pc++;
        }
    }

    private bool Test(Device device, GbaCheatInstruction instruction)
    {
        if (instruction.Operation == GbaCheatOperation.Button) return ButtonPressed;
        if (instruction.Operation == GbaCheatOperation.Never) return false;
        uint value = Read(device, instruction.Address, instruction.Width), operand = instruction.Value;
        return instruction.Operation switch
        {
            GbaCheatOperation.Equal => value == operand,
            GbaCheatOperation.NotEqual => value != operand,
            GbaCheatOperation.Less or GbaCheatOperation.UnsignedLess => value < operand,
            GbaCheatOperation.Greater or GbaCheatOperation.UnsignedGreater => value > operand,
            GbaCheatOperation.SignedLess => Signed(value, instruction.Width) < Signed(operand, instruction.Width),
            GbaCheatOperation.SignedGreater => Signed(value, instruction.Width) > Signed(operand, instruction.Width),
            GbaCheatOperation.LessOrEqual => value <= operand,
            GbaCheatOperation.GreaterOrEqual => value >= operand,
            GbaCheatOperation.BitsSet => (value & operand) != 0,
            GbaCheatOperation.BitsClear => (value & operand) == 0,
            _ => false
        };
    }

    private static int Signed(uint value, int width) => width switch { 1 => (sbyte)value, 2 => (short)value, _ => (int)value };
    private static uint Read(Device device, uint address, int width) => device.ReadCheatMemory(address, width);
    public CheatSnapshot[] CaptureSnapshots() => entries.Select(Snapshot).ToArray();
    private static CheatSnapshot Snapshot(Entry entry) => new(entry.Id, entry.Name, entry.Program.Code, entry.Enabled);

    internal static (uint Address, uint Value, int Width, string Normalized) Parse(string code)
    {
        GbaCheatProgram program = GbaCheatProgram.Compile(code);
        if (program.Instructions.Count != 1 || program.Instructions[0].Operation != GbaCheatOperation.Assign ||
            program.Instructions[0].Count != 1 || program.Patches.Count != 0 || program.Hook is not null)
            throw new FormatException("The code is a program rather than one direct GBA RAM patch.");
        var instruction = program.Instructions[0];
        return (instruction.Address, instruction.Value, instruction.Width, program.Code);
    }
}
