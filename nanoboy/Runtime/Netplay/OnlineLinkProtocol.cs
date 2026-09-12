using System;
using System.Buffers.Binary;
using System.IO;
using nanoboy.Core;

namespace AetherBoy.Runtime.Netplay;

/// <summary>Our versioned wire format; never contains ROMs, save files, paths or executable data.</summary>
internal static class OnlineLinkProtocol
{
    internal const int HelloLength = 24;
    internal const int HeaderLength = 32;
    internal const int SerialLength = 56;
    internal const byte Hello = 1, Serial = 2, Heartbeat = 3;
    private static ReadOnlySpan<byte> Magic => "ABLK"u8;

    internal static byte[] CreateHello(bool host, ReadOnlySpan<byte> nonce)
    {
        if (nonce.Length != 16) throw new ArgumentException("A connection nonce needs 16 bytes.");
        byte[] data = new byte[HelloLength];
        Magic.CopyTo(data);
        data[4] = 1; data[5] = Hello; data[6] = host ? (byte)1 : (byte)0;
        data[7] = 1; // GB/GBC paired serial protocol. GBA is NOT this protocol.
        nonce.CopyTo(data.AsSpan(8));
        return data;
    }

    internal static byte[] ReadHello(ReadOnlySpan<byte> data, bool localHost)
    {
        ValidatePrefix(data);
        if (data.Length != HelloLength || data[5] != Hello || data[6] > 1 || data[7] != 1)
            throw new InvalidDataException("Incompatible online-link handshake (GB/GBC protocol v1 required).");
        if ((data[6] == 1) == localHost)
            throw new InvalidDataException("Choose one host and one guest, not two identical roles.");
        return data.Slice(8, 16).ToArray();
    }

    internal static byte[] Encode(byte kind, ReadOnlySpan<byte> nonce, ulong sequence, NetworkSerialPacket packet = default)
    {
        if (nonce.Length != 16 || sequence == 0 || (kind != Serial && kind != Heartbeat))
            throw new ArgumentException("Invalid online-link envelope.");
        byte[] data = new byte[kind == Serial ? SerialLength : HeaderLength];
        Magic.CopyTo(data); data[4] = 1; data[5] = kind;
        nonce.CopyTo(data.AsSpan(8));
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(24), sequence);
        if (kind == Serial)
        {
            data[32] = (byte)packet.Kind; data[34] = packet.Data; data[35] = packet.Control;
            BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(36), packet.TransferId);
            BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(44), packet.PeerTransferId);
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(52), packet.ClockPeriodDots);
        }
        return data;
    }

    internal static (byte Kind, NetworkSerialPacket Packet) Decode(ReadOnlySpan<byte> data, ReadOnlySpan<byte> nonce, ulong sequence)
    {
        ValidatePrefix(data);
        if (data.Length < HeaderLength || data[6] != 0 || data[7] != 0 || nonce.Length != 16 ||
            !data.Slice(8, 16).SequenceEqual(nonce) || sequence == 0 ||
            BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(24, 8)) != sequence)
            throw new InvalidDataException("Stale, reordered or malformed online-link packet.");
        if (data[5] == Heartbeat && data.Length == HeaderLength) return (Heartbeat, default);
        if (data[5] != Serial || data.Length != SerialLength || data[33] != 0 || data[32] is < 1 or > 4)
            throw new InvalidDataException("Unknown online-link packet type or length.");
        return (Serial, new((NetworkSerialPacketKind)data[32],
            BinaryPrimitives.ReadInt64LittleEndian(data.Slice(36, 8)),
            BinaryPrimitives.ReadInt64LittleEndian(data.Slice(44, 8)),
            data[34], data[35], BinaryPrimitives.ReadInt32LittleEndian(data.Slice(52, 4))));
    }

    private static void ValidatePrefix(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8 || !data[..4].SequenceEqual(Magic) || data[4] != 1)
            throw new InvalidDataException("Unsupported AetherBoy online-link protocol.");
    }
}
