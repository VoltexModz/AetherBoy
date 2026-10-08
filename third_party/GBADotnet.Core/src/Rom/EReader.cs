// Copyright (c) 2013-2020 Jeffrey Pfau
// C# adaptation, bounded FIFO and complete accessory state for AetherBoy, 2026.
// SPDX-License-Identifier: MPL-2.0
// Derived from mGBA src/gba/cart/ereader.c. See THIRD_PARTY_NOTICES.md.
using System.Buffers.Binary;
using GameboyAdvanced.Core.Interrupts;

namespace GameboyAdvanced.Core.Rom;

/// <summary>Owner-thread cartridge peripheral. No game-RAM edits or host timers.</summary>
public sealed unsafe class EReader
{
    public const int QueueLimit = 16;
    private Device? device;
    private readonly byte[] data = new byte[88], serial = new byte[92];
    private readonly Queue<byte[]> cards = new();
    private byte[]? dots;
    private ushort unknown, reset = 4, led;
    private byte control0, control1 = 0x80, index, shiftByte, command;
    private int state, scanX, scanY, cardsStarted;
    public int QueuedCards => cards.Count;
    public int CardsStarted => cardsStarted;
    public bool HasCard => dots is not null;
    internal void Attach(Device owner) => device = owner;
    internal static bool IsRegisterAddress(uint address) => address is >= 0x0DF80000 and <= 0x0DFFFFFF;
    internal static bool IsFlashRegister(uint address) => (address & 0xFFFF) >= 0xFF80;

    public void QueueCard(ReadOnlySpan<byte> card)
    {
        if (!EReaderDotCode.IsSupportedLength(card.Length)) throw new InvalidDataException("Unsupported e-Reader card size.");
        if (cards.Count >= QueueLimit) throw new InvalidOperationException("The e-Reader card queue is full.");
        cards.Enqueue(card.ToArray());
    }

    public void ClearCards()
    {
        cards.Clear(); dots = null; scanX = scanY = 0; Array.Clear(data);
        device?.Scheduler.CancelEvent(EventType.EReaderIrq, -1);
    }

    internal void Reset()
    {
        ClearCards(); ResetRegisters(); Array.Clear(serial); cardsStarted = 0;
    }

    private void ResetRegisters()
    {
        Array.Clear(data); unknown = 0; reset = 4; led = 0; control0 = 0; control1 = 0x80;
        state = 0; index = shiftByte = command = 0;
        device?.Scheduler.CancelEvent(EventType.EReaderIrq, -1);
    }

    internal ushort Read(uint address)
    {
        uint mapped = address & 0x700FF;
        return (mapped >> 17) switch
        {
            0 => unknown,
            1 => reset,
            2 when (mapped & 0xFE) + 2 <= data.Length => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan((int)(mapped & 0xFE))),
            _ => 0
        };
    }

    internal void Write(uint address, byte value)
    {
        switch ((address & 0x700FF) >> 17)
        {
            case 0: unknown = (ushort)(value & 15); break;
            case 1:
                reset = (ushort)((value & 0x8A) | 4);
                if ((value & 2) != 0) ResetRegisters();
                break;
        }
    }

    internal byte ReadFlash(uint address) => (address & 0xFFFF) switch
    { 0xFFB0 => control0, 0xFFB1 => control1, _ => 0 };

    internal void WriteFlash(uint address, byte value)
    {
        switch (address & 0xFFFF)
        {
            case 0xFFB0: WriteControl0(value); break;
            case 0xFFB1:
                control1 = (byte)((value & 0x32) | 0x80);
                if ((control0 & 0x10) != 0 && (control1 & 2) == 0)
                {
                    scanY = Math.Min(scanY + 1, 65535);
                    if (scanY == (serial[0x15] | serial[0x14] << 8))
                    { scanY = 0; if (scanX < 4050) scanX += 210; }
                    ReadScanline();
                }
                break;
            case 0xFFB2: led = (ushort)((led & 0xFF00) | value); break;
            case 0xFFB3: led = (ushort)((led & 0xFF) | value << 8); break;
        }
    }

    private void WriteControl0(byte value)
    {
        byte next = (byte)(value & 0x7F), old = control0;
        if (state == 0)
        {
            if ((old & 3) == 3 && (next & 1) == 0) state = 1;
        }
        else if ((old & 3) == 2 && (next & 1) != 0) state = 0;
        else if (state == 1)
        {
            if ((old & 3) == 2 && (next & 2) == 0)
            { state = 2; command = shiftByte = 0; }
        }
        else if ((old & 2) != 0 && (next & 2) == 0)
        {
            if ((next & 4) != 0)
            {
                shiftByte |= (byte)((next & 1) << (9 - state));
                if (++state == 10)
                {
                    if (command == 0) command = shiftByte;
                    else if (command == 0x22) { index = shiftByte; command = 1; }
                    else if (command == 1)
                    {
                        int register = index & 0x7F;
                        if (register is > 0 and < 0x57) serial[register] = shiftByte;
                        index++;
                    }
                    state = 2; shiftByte = 0;
                }
            }
            else if (command == 0x23)
            {
                int incoming = (index & 0x7F) < 0x5A ? serial[index & 0x7F] >> (9 - state) : 0;
                next = (byte)((next & ~1) | (incoming & 1));
                if (++state == 10) { index++; state = 2; }
            }
        }
        else if ((next & 4) == 0) next &= 0xFE;
        control0 = next;
        if ((old & 0x10) == 0 && (next & 0x10) != 0)
        { if (scanX > 0) NextCard(); scanX = scanY = 0; }
        else if ((next & 0x18) == 0x18 && (control1 & 2) == 0) ReadScanline();
    }

    private void NextCard()
    {
        // An empty scan must keep polling for a later insertion. Retaining a
        // cleared, non-null dot buffer stranded cards queued while firmware
        // already displayed its "Start" prompt after the first strip.
        dots = null;
        if (!cards.TryDequeue(out byte[]? card)) return;
        dots = EReaderDotCode.Decode(card); scanX = -24; scanY = 0;
        if (cardsStarted < int.MaxValue) cardsStarted++;
    }

    private void ReadScanline()
    {
        Array.Clear(data);
        if (dots is null) NextCard();
        int y = scanY - 10;
        if (dots is not null && y is >= 0 and < 120)
            for (int i = 0; i < 20; i++)
            {
                ushort word = 0;
                for (int bit = 0; bit < 16; bit++)
                {
                    int x = 16 + (scanX + i * 16 + bit) / 3;
                    if ((uint)x < EReaderDotCode.Stride)
                        word |= (ushort)(dots[(y / 3) * EReaderDotCode.Stride + x] << ((bit + 8) % 16));
                }
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan((19 - i) * 2), word);
            }
        control1 |= 2;
        if ((control0 & 8) != 0 && device is not null)
        {
            device.Scheduler.CancelEvent(EventType.EReaderIrq, -1);
            device.Scheduler.ScheduleEvent(EventType.EReaderIrq, &IrqEvent, Math.Max(1, Math.Min((int)(ushort)(led * 2), 0x4000)));
        }
    }

    internal static void IrqEvent(Device owner) => owner.InterruptRegisters.RaiseInterrupt(Interrupt.GamePak);

    // Calibration applies only to completely erased sectors; never replace a
    // player's existing calibration/save. Called after loading persistent data.
    internal void InitializeCalibration(byte[] flash)
    {
        ReadOnlySpan<byte> template = [0x43,0x61,0x72,0x64,0x2D,0x45,0x20,0x52,0x65,0x61,0x64,0x65,0x72,0x20,0x32,0x30,
            0x30,0x31,0,0,0xCF,0x72,0x2F,0x37,0x3A,0x3A,0x3A,0x38,0x33,0x30,0x30,0x37,0x3A,0x39,0x37,0x35,
            0x33,0x2F,0x2F,0x34,0x36,0x36,0x37,0x36,0x34,0x31,0x2D,0x30,0x32,0x34,0x35,0x35,0x34,0x30,
            0x2A,0x2D,0x2D,0x2F,0x31,0x32,0x31,0x2F,0x29,0x2A,0x2C,0x2B,0x2C,0x2E,0x2E,0x2D,0x18,0x2D,
            0x8F,3,0,0,0xC0,0xFD,0x77,0,0,0,1];
        foreach (int offset in new int[] { 0xD000, 0xE000 })
        {
            Span<byte> sector = flash.AsSpan(offset, 0x1000);
            if (sector.IndexOfAnyExcept((byte)255) >= 0) continue;
            sector.Clear(); template.CopyTo(sector);
        }
    }

    internal byte[] Capture()
    {
        using var stream = new MemoryStream(); using var w = new BinaryWriter(stream);
        w.Write(1); w.Write(unknown); w.Write(reset); w.Write(led); w.Write(control0); w.Write(control1);
        w.Write(index); w.Write(shiftByte); w.Write(command); w.Write(state); w.Write(scanX); w.Write(scanY); w.Write(cardsStarted);
        w.Write(data); w.Write(serial); w.Write(dots is not null);
        if (dots is not null) w.Write(dots);
        w.Write(cards.Count); foreach (var card in cards) { w.Write(card.Length); w.Write(card); }
        return stream.ToArray();
    }

    internal static EReader Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, false); using var r = new BinaryReader(stream);
        if (r.ReadInt32() != 1) throw new InvalidDataException("Unknown e-Reader state version.");
        var result = new EReader { unknown = r.ReadUInt16(), reset = r.ReadUInt16(), led = r.ReadUInt16(),
            control0 = r.ReadByte(), control1 = r.ReadByte(), index = r.ReadByte(), shiftByte = r.ReadByte(), command = r.ReadByte(),
            state = r.ReadInt32(), scanX = r.ReadInt32(), scanY = r.ReadInt32(), cardsStarted = r.ReadInt32() };
        if (result.unknown > 15 || (result.reset & ~0x8E) != 0 || (result.reset & 4) == 0 || result.control0 > 127 ||
            (result.control1 & ~0x32) != 0x80 || result.state is < 0 or > 9 || result.scanX is < -24 or > 4236 ||
            result.scanY is < 0 or > 65535 || result.cardsStarted < 0)
            throw new InvalidDataException("Invalid e-Reader state.");
        r.BaseStream.ReadExactly(result.data); r.BaseStream.ReadExactly(result.serial);
        byte hasDots = r.ReadByte();
        if (hasDots > 1) throw new InvalidDataException("Invalid e-Reader dot buffer flag.");
        if (hasDots == 1)
        {
            result.dots = new byte[EReaderDotCode.Stride * EReaderDotCode.Height]; r.BaseStream.ReadExactly(result.dots);
            if (result.dots.Any(b => b > 1)) throw new InvalidDataException("Invalid e-Reader dot value.");
        }
        int count = r.ReadInt32();
        if (count is < 0 or > QueueLimit) throw new InvalidDataException("Invalid e-Reader queue length.");
        for (int i = 0; i < count; i++)
        {
            int length = r.ReadInt32();
            if (!EReaderDotCode.IsSupportedLength(length)) throw new InvalidDataException("Invalid e-Reader card length.");
            byte[] card = new byte[length]; r.BaseStream.ReadExactly(card); result.cards.Enqueue(card);
        }
        if (stream.Position != stream.Length) throw new InvalidDataException("Trailing e-Reader state data.");
        return result;
    }
}
