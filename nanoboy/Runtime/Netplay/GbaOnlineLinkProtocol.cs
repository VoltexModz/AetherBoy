using System;
using System.Buffers.Binary;
using System.IO;
using GameboyAdvanced.Core.Serial;

namespace AetherBoy.Runtime.Netplay;

/// <summary>GBA profile envelopes are deliberately incompatible with GB/GBC v1.</summary>
internal static class GbaOnlineLinkProtocol
{
    internal const int HelloLength = 32, HeaderLength = 32, MessageLength = 72;
    internal const byte Hello = 1, Message = 2, Heartbeat = 3, Pause = 4, CloseRequest = 5, CloseAck = 6, CloseReceipt = 7;
    private static ReadOnlySpan<byte> Magic => "ABLK"u8;

    internal static byte[] CreateHello(bool host, ReadOnlySpan<byte> nonce)
    {
        if (nonce.Length != 16) throw new ArgumentException("A connection nonce needs 16 bytes.");
        byte[] data = new byte[HelloLength];
        Magic.CopyTo(data); data[4] = 2; data[5] = Hello; data[6] = host ? (byte)1 : (byte)0;
        data[7] = 2; nonce.CopyTo(data.AsSpan(8));
        data[24] = 1; data[26] = 1; data[28] = 1; // Gen3 profile 1 / revision 1 / explicit unverified-development consent.
        return data;
    }

    internal static byte[] ReadHello(ReadOnlySpan<byte> data, bool localHost)
    {
        ValidatePrefix(data);
        if (data.Length != HelloLength || data[5] != Hello || data[6] > 1 || data[7] != 2 ||
            BinaryPrimitives.ReadUInt16LittleEndian(data[24..]) != 1 || BinaryPrimitives.ReadUInt16LittleEndian(data[26..]) != 1 ||
            data[28] != 1 || data[29] != 0 || data[30] != 0 || data[31] != 0)
            throw new InvalidDataException("Both peers need the same GBA Pokémon Gen3 development profile and explicit consent.");
        if ((data[6] == 1) == localHost) throw new InvalidDataException("Choose one host and one guest.");
        return data.Slice(8, 16).ToArray();
    }

    internal static byte[] Encode(byte kind, ReadOnlySpan<byte> nonce, ulong sequence, PokemonGen3Message message = default, bool paused = false)
    {
        if (nonce.Length != 16 || sequence == 0 || kind is < Message or > CloseReceipt)
            throw new ArgumentException("Invalid GBA online envelope.");
        byte[] data = new byte[kind == Message ? MessageLength : kind == Pause ? HeaderLength + 1 : HeaderLength];
        Magic.CopyTo(data); data[4] = 2; data[5] = kind;
        nonce.CopyTo(data.AsSpan(8)); BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(24), sequence);
        if (kind == Pause) data[32] = paused ? (byte)1 : (byte)0;
        if (kind == Message)
        {
            data[32] = (byte)message.Kind;
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(34), message.HandshakeWord);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(36), message.Phase);
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(40), message.Sequence);
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(48), message.WordsLow);
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(56), message.WordsHigh);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(64), message.Checksum);
        }
        return data;
    }

    internal static (byte Kind, PokemonGen3Message Message, bool Paused) Decode(ReadOnlySpan<byte> data, ReadOnlySpan<byte> nonce, ulong sequence)
    {
        ValidatePrefix(data);
        if (data.Length < HeaderLength || data[6] != 0 || data[7] != 0 || nonce.Length != 16 || sequence == 0 ||
            !data.Slice(8, 16).SequenceEqual(nonce) || BinaryPrimitives.ReadUInt64LittleEndian(data[24..]) != sequence)
            throw new InvalidDataException("Stale, reordered or malformed GBA online packet.");
        if (data[5] is Heartbeat or CloseRequest or CloseAck or CloseReceipt && data.Length == HeaderLength) return (data[5], default, false);
        if (data[5] == Pause && data.Length == HeaderLength + 1 && data[32] <= 1) return (Pause, default, data[32] == 1);
        if (data[5] != Message || data.Length != MessageLength || data[32] is < 1 or > 3 || data[33] != 0 ||
            data[66] != 0 || data[67] != 0 || data[68] != 0 || data[69] != 0 || data[70] != 0 || data[71] != 0)
            throw new InvalidDataException("Unknown GBA online message or reserved fields.");
        return (Message, new((PokemonGen3MessageKind)data[32], BinaryPrimitives.ReadUInt32LittleEndian(data[36..]),
            BinaryPrimitives.ReadUInt64LittleEndian(data[40..]), BinaryPrimitives.ReadUInt16LittleEndian(data[34..]),
            BinaryPrimitives.ReadUInt64LittleEndian(data[48..]), BinaryPrimitives.ReadUInt64LittleEndian(data[56..]),
            BinaryPrimitives.ReadUInt16LittleEndian(data[64..])), false);
    }

    private static void ValidatePrefix(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8 || !data[..4].SequenceEqual(Magic) || data[4] != 2)
            throw new InvalidDataException("GBA Gen3 needs AetherBoy protocol v2; GB/GBC and other profiles cannot be mixed.");
    }
}
