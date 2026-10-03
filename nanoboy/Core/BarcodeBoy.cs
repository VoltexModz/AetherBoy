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
    private readonly Memory memory;
    private int handshake;
    private byte sent, reply;
    private bool internalTransfer;
    private int bit, clock;
    private string pending = "";
    private int packetIndex;
    private int completed;

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
        handshake = bit = clock = packetIndex = completed = 0;
        sent = 0; reply = 0xFF; internalTransfer = false; pending = "";
    }

    void ISerialDevice.Write(byte value) => sent = value;
    byte ISerialDevice.Read() => reply;
    void ISerialDevice.Start()
    {
        bit = clock = 0;
        internalTransfer = memory.HasInternalSerialClock;
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
                if (!Ready)
                {
                    byte expected = (handshake & 1) == 0 ? (byte)0x10 : (byte)0x07;
                    handshake = sent == expected ? handshake + 1 : sent == 0x10 ? 1 : 0;
                }
            }
            else if (Ready && HasPendingScan && ++packetIndex == 30)
            {
                pending = ""; packetIndex = 0; handshake = 0;
                if (completed < int.MaxValue) completed++;
            }
        }
        bit = clock = 0;
    }

    void ISerialScheduledDevice.SerialDataWritten() { }
    void ISerialScheduledDevice.TickSerialCycle()
    {
        if (memory.SerialBitsRemaining == 0) return;
        bool master = memory.HasInternalSerialClock;
        if (!master && (!Ready || !HasPendingScan)) return;
        int period = master ? memory.SerialClockPeriodDots * (memory.SerialCpuDoubleSpeed ? 2 : 1)
            : ExternalBitPeriodDots * (memory.SerialCpuDoubleSpeed ? 2 : 1);
        if (++clock < period) return;
        clock = 0;
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
        writer.Write(0x31424342); // BCB1, tagged extension of the formerly empty serial section.
        writer.Write(handshake); writer.Write(sent); writer.Write(reply); writer.Write(internalTransfer);
        writer.Write(bit); writer.Write(clock); writer.Write(packetIndex); writer.Write(completed);
        writer.Write((byte)pending.Length);
        foreach (char digit in pending) writer.Write((byte)digit);
    });

    internal static BarcodeBoy Decode(Memory memory, byte[] payload) => StatePayload.Read(payload, reader =>
    {
        if (reader.ReadInt32() != 0x31424342) throw new InvalidDataException("Unknown serial accessory state.");
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
        StatePayload.RequireRange(device.handshake, 0, 4, "barcode handshake");
        StatePayload.RequireRange(device.bit, 0, 7, "barcode bit");
        StatePayload.RequireRange(device.clock, 0, 1023, "barcode clock");
        StatePayload.RequireRange(device.packetIndex, 0, 29, "barcode packet");
        StatePayload.RequireRange(device.completed, 0, int.MaxValue, "barcode count");
        if (length == 0 && device.packetIndex != 0) throw new InvalidDataException("Barcode packet has no data.");
        return device;
    });
}
