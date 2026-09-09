using System.Security.Cryptography;

namespace GameboyAdvanced.Core.Rom;

/// <summary>
/// Emulates the three-wire GPIO real-time clock used by cartridges such as
/// Pokémon Ruby, Sapphire and Emerald. Commands and payload bytes are shifted
/// least-significant bit first through SCK/SIO/CS.
/// </summary>
public sealed class GpioRtc
{
    private const uint PersistentMagic = 0x54524241; // "ABRT"
    private const ushort PersistentSchema = 1;
    private const int DigestLength = 32;
    public const int PersistentStateLength = 47;
    internal const uint DataOffset = 0xC4;
    internal const uint DirectionOffset = 0xC6;
    internal const uint ControlOffset = 0xC8;

    private readonly Func<DateTime> clock;
    private readonly List<byte> writePayload = new(7);
    private byte[] readPayload = Array.Empty<byte>();
    private ushort dataLatch;
    private ushort direction;
    private ushort control;
    private byte command;
    private byte inputByte;
    private int inputBit;
    private int expectedWriteBytes;
    private int readByteIndex;
    private int readBitIndex;
    private bool receivingCommand;
    private bool outputStarted;
    private byte status = 0x40;
    private long clockOffsetTicks;

    public GpioRtc(Func<DateTime>? clock = null)
    {
        this.clock = clock ?? (() => DateTime.Now);
    }

    public bool ReadEnabled => (control & 1) != 0;
    public byte Status => status;

    public byte[] CapturePersistentState()
    {
        using var body = new MemoryStream(PersistentStateLength - DigestLength);
        using (var writer = new BinaryWriter(body, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(PersistentMagic);
            writer.Write(PersistentSchema);
            writer.Write(status);
            writer.Write(clockOffsetTicks);
        }
        byte[] payload = body.ToArray();
        byte[] result = new byte[PersistentStateLength];
        payload.CopyTo(result, 0);
        SHA256.HashData(payload).CopyTo(result, payload.Length);
        return result;
    }

    public void RestorePersistentState(byte[] state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Length != PersistentStateLength)
            throw new InvalidDataException("The GBA RTC save has an invalid length.");
        ReadOnlySpan<byte> payload = state.AsSpan(0, state.Length - DigestLength);
        ReadOnlySpan<byte> digest = state.AsSpan(state.Length - DigestLength);
        Span<byte> actual = stackalloc byte[DigestLength];
        SHA256.HashData(payload, actual);
        if (!CryptographicOperations.FixedTimeEquals(actual, digest))
            throw new InvalidDataException("The GBA RTC save failed its integrity check.");

        using var stream = new MemoryStream(payload.ToArray(), writable: false);
        using var reader = new BinaryReader(stream);
        if (reader.ReadUInt32() != PersistentMagic)
            throw new InvalidDataException("The file is not an AetherBoy GBA RTC save.");
        ushort schema = reader.ReadUInt16();
        if (schema != PersistentSchema)
            throw new NotSupportedException($"GBA RTC save schema {schema} is unsupported.");
        status = reader.ReadByte();
        clockOffsetTicks = reader.ReadInt64();
    }

    internal ushort Read(uint offset, ushort romValue)
    {
        return offset switch
        {
            DataOffset when ReadEnabled => ReadDataPort(),
            DirectionOffset when ReadEnabled => direction,
            ControlOffset => control,
            _ => romValue,
        };
    }

    internal void Write(uint offset, ushort value)
    {
        switch (offset)
        {
            case DataOffset:
                WriteDataPort((ushort)(value & 7));
                break;
            case DirectionOffset:
                direction = (ushort)(value & 7);
                break;
            case ControlOffset:
                control = (ushort)(value & 1);
                break;
        }
    }

    internal void WriteState(BinaryWriter writer)
    {
        writer.Write(dataLatch);
        writer.Write(direction);
        writer.Write(control);
        writer.Write(command);
        writer.Write(inputByte);
        writer.Write(inputBit);
        writer.Write(expectedWriteBytes);
        writer.Write(readByteIndex);
        writer.Write(readBitIndex);
        writer.Write(receivingCommand);
        writer.Write(outputStarted);
        writer.Write(status);
        writer.Write(clockOffsetTicks);
        writer.Write(writePayload.Count);
        foreach (byte value in writePayload)
            writer.Write(value);
        writer.Write(readPayload.Length);
        writer.Write(readPayload);
    }

    internal void ReadState(BinaryReader reader)
    {
        dataLatch = reader.ReadUInt16();
        direction = reader.ReadUInt16();
        control = reader.ReadUInt16();
        command = reader.ReadByte();
        inputByte = reader.ReadByte();
        inputBit = reader.ReadInt32();
        expectedWriteBytes = reader.ReadInt32();
        readByteIndex = reader.ReadInt32();
        readBitIndex = reader.ReadInt32();
        receivingCommand = reader.ReadBoolean();
        outputStarted = reader.ReadBoolean();
        status = reader.ReadByte();
        clockOffsetTicks = reader.ReadInt64();
        int writeCount = reader.ReadInt32();
        if ((dataLatch & ~7) != 0 || (direction & ~7) != 0 || (control & ~1) != 0 ||
            inputBit is < 0 or > 7 || expectedWriteBytes is < 0 or > 7 ||
            readByteIndex < 0 || readBitIndex is < 0 or > 7 || writeCount is < 0 or > 7)
        {
            throw new InvalidDataException("The saved GBA RTC protocol state is invalid.");
        }
        writePayload.Clear();
        for (int index = 0; index < writeCount; index++)
            writePayload.Add(reader.ReadByte());
        int readCount = reader.ReadInt32();
        if (readCount is < 0 or > 7 || readByteIndex > readCount)
            throw new InvalidDataException("The saved GBA RTC response is invalid.");
        readPayload = reader.ReadBytes(readCount);
        if (readPayload.Length != readCount)
            throw new EndOfStreamException("The GBA RTC state ended unexpectedly.");
    }

    private ushort ReadDataPort()
    {
        int value = dataLatch & direction;
        if ((direction & 2) == 0 && CurrentOutputBit())
            value |= 2;
        return (ushort)value;
    }

    private void WriteDataPort(ushort value)
    {
        bool oldClock = (dataLatch & 1) != 0;
        bool oldChipSelect = (dataLatch & 4) != 0;
        bool newClock = (value & 1) != 0;
        bool newChipSelect = (value & 4) != 0;
        dataLatch = value;

        if (!oldChipSelect && newChipSelect)
            BeginTransaction();
        if (oldChipSelect && !newChipSelect)
        {
            EndTransaction();
            return;
        }
        if (!newChipSelect)
            return;

        if (!oldClock && newClock)
        {
            if ((direction & 2) != 0)
                ReceiveBit((value & 2) != 0);
            else if (readPayload.Length != 0)
                outputStarted = true;
        }
        else if (oldClock && !newClock && (direction & 2) == 0 && outputStarted)
        {
            AdvanceOutputBit();
        }
    }

    private void BeginTransaction()
    {
        receivingCommand = true;
        command = 0;
        inputByte = 0;
        inputBit = 0;
        expectedWriteBytes = 0;
        writePayload.Clear();
        readPayload = Array.Empty<byte>();
        readByteIndex = 0;
        readBitIndex = 0;
        outputStarted = false;
    }

    private void EndTransaction()
    {
        if (!receivingCommand && expectedWriteBytes > 0 &&
            writePayload.Count == expectedWriteBytes)
        {
            ApplyWriteCommand();
        }
        receivingCommand = false;
        expectedWriteBytes = 0;
        outputStarted = false;
    }

    private void ReceiveBit(bool high)
    {
        if (high)
            inputByte |= (byte)(1 << inputBit);
        inputBit++;
        if (inputBit != 8)
            return;

        byte complete = inputByte;
        inputByte = 0;
        inputBit = 0;
        if (receivingCommand)
        {
            command = NormalizeCommand(complete);
            receivingCommand = false;
            PrepareCommand();
        }
        else if (expectedWriteBytes > 0 && writePayload.Count < expectedWriteBytes)
        {
            writePayload.Add(complete);
            if (writePayload.Count == expectedWriteBytes)
                ApplyWriteCommand();
        }
    }

    private static byte NormalizeCommand(byte value)
    {
        if ((value & 0xF0) == 0x60)
            return value;
        byte reversed = 0;
        for (int bit = 0; bit < 8; bit++)
            reversed |= (byte)(((value >> bit) & 1) << (7 - bit));
        return reversed;
    }

    private void PrepareCommand()
    {
        int register = (command >> 1) & 7;
        bool read = (command & 1) != 0;
        if (register == 0)
        {
            status = 0x40;
            clockOffsetTicks = 0;
            return;
        }

        if (read)
        {
            readPayload = register switch
            {
                1 => new[] { status },
                2 => EncodeDateTime(CurrentTime()),
                3 => EncodeTime(CurrentTime()),
                _ => Array.Empty<byte>(),
            };
            readByteIndex = 0;
            readBitIndex = 0;
            outputStarted = false;
        }
        else
        {
            expectedWriteBytes = register switch
            {
                1 => 1,
                2 => 7,
                3 => 3,
                _ => 0,
            };
        }
    }

    private void ApplyWriteCommand()
    {
        int register = (command >> 1) & 7;
        if (register == 1)

            status = (byte)(writePayload[0] & 0x6A);
        else if (register == 2)
            SetCurrentTime(DecodeDateTime(writePayload));
        else if (register == 3)
        {
            DateTime now = CurrentTime();
            (int hour, int minute, int second) = DecodeTime(writePayload);
            SetCurrentTime(new DateTime(now.Year, now.Month, now.Day, hour, minute, second));
        }
        expectedWriteBytes = 0;
    }

    private bool CurrentOutputBit()
    {
        if (readByteIndex >= readPayload.Length)
            return false;
        return ((readPayload[readByteIndex] >> readBitIndex) & 1) != 0;
    }

    private void AdvanceOutputBit()
    {
        readBitIndex++;
        if (readBitIndex < 8)
            return;
        readBitIndex = 0;
        readByteIndex++;
    }

    private DateTime CurrentTime()
    {
        DateTime hostTime = clock();
        try
        {
            return hostTime.AddTicks(clockOffsetTicks);
        }
        catch (ArgumentOutOfRangeException)
        {
            return hostTime;
        }
    }

    private void SetCurrentTime(DateTime value)
    {
        clockOffsetTicks = value.Ticks - clock().Ticks;
    }

    private byte[] EncodeDateTime(DateTime value)
    {
        byte[] time = EncodeTime(value);
        return
        [
            ToBcd(value.Year % 100),
            ToBcd(value.Month),
            ToBcd(value.Day),
            ToBcd((int)value.DayOfWeek),
            time[0],
            time[1],
            time[2],
        ];
    }

    private byte[] EncodeTime(DateTime value)
    {
        int hour = value.Hour;
        byte encodedHour;
        if ((status & 0x40) != 0)
        {
            encodedHour = ToBcd(hour);
        }
        else
        {
            bool afternoon = hour >= 12;
            int hour12 = hour % 12;
            if (hour12 == 0)
                hour12 = 12;
            encodedHour = (byte)(ToBcd(hour12) | (afternoon ? 0x80 : 0));
        }
        return [encodedHour, ToBcd(value.Minute), ToBcd(value.Second)];
    }

    private DateTime DecodeDateTime(IReadOnlyList<byte> value)
    {
        int year = 2_000 + FromBcd(value[0]);
        int month = Math.Clamp(FromBcd(value[1]), 1, 12);
        int day = Math.Clamp(FromBcd(value[2]), 1, DateTime.DaysInMonth(year, month));
        (int hour, int minute, int second) = DecodeTime(value.Skip(4).Take(3).ToArray());
        return new DateTime(year, month, day, hour, minute, second);
    }

    private (int Hour, int Minute, int Second) DecodeTime(IReadOnlyList<byte> value)
    {
        int hour;
        if ((status & 0x40) != 0)
        {
            hour = Math.Clamp(FromBcd((byte)(value[0] & 0x3F)), 0, 23);
        }
        else
        {
            int hour12 = Math.Clamp(FromBcd((byte)(value[0] & 0x3F)), 1, 12);
            hour = hour12 % 12 + ((value[0] & 0x80) != 0 ? 12 : 0);
        }
        return (
            hour,
            Math.Clamp(FromBcd(value[1]), 0, 59),
            Math.Clamp(FromBcd(value[2]), 0, 59));
    }

    private static byte ToBcd(int value) => (byte)(((value / 10) << 4) | value % 10);
    private static int FromBcd(byte value) => ((value >> 4) * 10) + (value & 0xF);
}
