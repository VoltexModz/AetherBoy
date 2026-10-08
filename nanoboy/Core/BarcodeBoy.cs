using System;
using System.IO;

namespace nanoboy.Core;

/// <summary>
/// Namcot Barcode Boy, not the unrelated Bardigun reader or GBA e-Reader.
/// Protocol reference: https://shonumi.github.io/dandocs.html (public domain).
/// All calls belong to the emulation owner thread. No host timers or UI callbacks.
/// </summary>
public sealed class BarcodeBoy : ISerialScheduledDevice
{
    // A conservative normal-speed serial clock, not a measured scanner oscillator.
    public const int ExternalBitPeriodDots = 512;
    // Compatibility pacing between the duplicate packets, not a measured
    // physical-scanner gap. Keep it separate from the serial bit clock.
    public const int PacketGapDots = 14336;
    private readonly Memory memory;
    private int handshake;
    private byte sent, reply;
    private bool internalTransfer;
    private int bit, clock;
    private string pending = "";
    private int packetIndex;
    private int completed;
    private int packetGapHalfDots;

    internal BarcodeBoy(Memory memory) => this.memory = memory;
    public bool Ready => handshake == 4;
    public bool HasPendingScan => pending.Length != 0;
    public int BytesSent => packetIndex;
    public int CompletedScans => completed;

    public static string ValidateCode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string code = value.Trim();
        if (code.Length != 13) throw new ArgumentException("A Barcode Boy code needs exactly 13 ASCII digits.", nameof(value));
        foreach (char digit in code)
            if (digit < '0' || digit > '9') throw new ArgumentException("A Barcode Boy code needs exactly 13 ASCII digits.", nameof(value));
        // Preserve card data verbatim; do not invent/fix an EAN check digit.
        return code;
    }

    public void QueueScan(string code)
    {
        string validated = ValidateCode(code);
        if (HasPendingScan) throw new InvalidOperationException("A barcode scan is already pending.");
        pending = validated;
        packetIndex = 0;
    }

    internal void Reset()
    {
        handshake = bit = clock = packetIndex = completed = packetGapHalfDots = 0;
        sent = 0; reply = 0xFF; internalTransfer = false; pending = "";
    }

    void ISerialDevice.Write(byte value) => sent = value;
    byte ISerialDevice.Read() => reply;
    void ISerialDevice.Start()
    {
        bit = clock = 0;
        internalTransfer = memory.HasInternalSerialClock;
        // SC starts a fresh serial clock low phase, not a fresh DIV prescaler.
        // Two divider edges form one bit clock (normal: 256 + 256 T-cycles).
        // Restarting a private 512-cycle timer here can move the serial IRQ
        // into a nested game timer handler after repeated scans.
        if (internalTransfer) clock = memory.Timer.DividerCounter & (InternalBitPeriod / 2 - 1);
        reply = handshake switch
        {
            2 when sent == 0x10 => 0x10,
            3 when sent == 0x07 => 0x07,
            _ => 0xFF
        };
    }

    void ISerialDevice.Stop()
    {
        // Memory calls Stop for both completion and abort. Only eight real edges commit.
        if (bit == 8 && memory.SerialBitsRemaining == 0)
        {
            if (internalTransfer)
            {
                // Compatibility recovery: games can discard the final reply
                // (Battle Space has a nested timer/serial IRQ race) and retry
                // detection. Accept a new 10,07,10,07 handshake before any card
                // bytes have left the scanner. Real hardware is documented to
                // return FF until power-cycled; this deliberately avoids that
                // manual recovery, without resetting the console or editing RAM.
                // Commit only after all eight edges; interrupted polls do nothing.
                if (Ready && packetIndex == 0 && sent == 0x10)
                    handshake = 1;
                else if (!Ready)
                {
                    byte expected = (handshake & 1) == 0 ? (byte)0x10 : (byte)0x07;
                    handshake = sent == expected ? handshake + 1 : sent == 0x10 ? 1 : 0;
                }
            }
            else if (Ready && HasPendingScan)
            {
                packetIndex++;
                if (packetIndex == 15) packetGapHalfDots = PacketGapDots * 2;
                else if (packetIndex == 30)
                {
                    pending = ""; packetIndex = 0; handshake = 0;
                    if (completed < int.MaxValue) completed++;
                }
            }
        }
        bit = clock = 0;
    }

    void ISerialScheduledDevice.SerialDataWritten() { }
    private int InternalBitPeriod => memory.SerialClockPeriodDots * (memory.SerialCpuDoubleSpeed ? 2 : 1);

    internal void DividerReset()
    {
        if (!memory.HasInternalSerialClock) return;
        int half = InternalBitPeriod / 2;
        bool fallingPrescalerEdge = (memory.Timer.DividerCounter & (half / 2)) != 0;
        // DIV reset clears the prescaler phase. A falling source edge toggles
        // the clock; only the second half-edge actually shifts a data bit.
        clock = clock >= half ? half : 0;
        if (!fallingPrescalerEdge) return;
        clock += half;
        if (clock < InternalBitPeriod) return;
        clock = 0;
        ClockBit(true);
    }

    void ISerialScheduledDevice.TickSerialCycle()
    {
        if (packetGapHalfDots > 0)
        {
            packetGapHalfDots = Math.Max(0, packetGapHalfDots - (memory.SerialCpuDoubleSpeed ? 1 : 2));
            if (!memory.HasInternalSerialClock) return;
        }
        if (memory.SerialBitsRemaining == 0) return;
        bool master = memory.HasInternalSerialClock;
        if (!master && (!Ready || !HasPendingScan)) return;
        int period = master ? InternalBitPeriod
            : ExternalBitPeriodDots * (memory.SerialCpuDoubleSpeed ? 2 : 1);
        if (++clock < period) return;
        clock = 0;
        ClockBit(master);
    }

    private void ClockBit(bool master)
    {
        byte incoming = reply;
        if (!master)
        {
            int position = packetIndex % 15;
            incoming = position == 0 ? (byte)2 : position == 14 ? (byte)3 : (byte)pending[position - 1];
        }
        bool input = (incoming & (0x80 >> bit)) != 0;
        bit++; // Stop is called synchronously by the eighth edge.
        if (master) memory.TryClockLinkedSerialBit(input, out _);
        else memory.TryClockSerialBit(input, out _);
    }

    internal byte[] Capture() => StatePayload.Write(writer =>
    {
        writer.Write(0x32424342); // BCB2 adds the emulated inter-packet delay.
        writer.Write(handshake); writer.Write(sent); writer.Write(reply); writer.Write(internalTransfer);
        writer.Write(bit); writer.Write(clock); writer.Write(packetIndex); writer.Write(completed);
        writer.Write((byte)pending.Length);
        foreach (char digit in pending) writer.Write((byte)digit);
        writer.Write(packetGapHalfDots);
    });

    internal static BarcodeBoy Decode(Memory memory, byte[] payload) => StatePayload.Read(payload, reader =>
    {
        int version = reader.ReadInt32();
        if (version != 0x31424342 && version != 0x32424342) throw new InvalidDataException("Unknown serial accessory state.");
        var device = new BarcodeBoy(memory)
        {
            handshake = reader.ReadInt32(), sent = reader.ReadByte(), reply = reader.ReadByte(),
            internalTransfer = StatePayload.ReadBoolean(reader), bit = reader.ReadInt32(), clock = reader.ReadInt32(),
            packetIndex = reader.ReadInt32(), completed = reader.ReadInt32()
        };
        int length = reader.ReadByte();
        if (length != 0 && length != 13) throw new InvalidDataException("Invalid barcode length in state.");
        byte[] digits = StatePayload.ReadBytes(reader, length, "barcode");
        foreach (byte digit in digits)
            if (digit < '0' || digit > '9') throw new InvalidDataException("Invalid barcode digit in state.");
        device.pending = System.Text.Encoding.ASCII.GetString(digits);
        device.packetGapHalfDots = version == 0x32424342 ? reader.ReadInt32() : 0;
        StatePayload.RequireRange(device.handshake, 0, 4, "barcode handshake");
        StatePayload.RequireRange(device.bit, 0, 7, "barcode bit");
        StatePayload.RequireRange(device.clock, 0, 1023, "barcode clock");
        StatePayload.RequireRange(device.packetIndex, 0, 29, "barcode packet");
        StatePayload.RequireRange(device.completed, 0, int.MaxValue, "barcode count");
        StatePayload.RequireRange(device.packetGapHalfDots, 0, PacketGapDots * 2, "barcode packet delay");
        if (device.packetGapHalfDots > 0 && (length == 0 || device.packetIndex != 15 || device.handshake != 4))
            throw new InvalidDataException("Barcode packet delay has no pending duplicate.");
        if (length == 0 && device.packetIndex != 0) throw new InvalidDataException("Barcode packet has no data.");
        return device;
    });
}
