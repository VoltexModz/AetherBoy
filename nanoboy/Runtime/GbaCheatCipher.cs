// CodeBreaker and GameShark cipher behavior derived from mGBA's cheat decoders.
// Copyright (c) 2013-2016 Jeffrey Pfau; C# adaptation (c) 2026 NekoZDevTeam.
// SPDX-License-Identifier: MPL-2.0
// See third_party/mgba-cheats/LICENSE.txt and THIRD_PARTY_NOTICES.md.
using System;
using System.Buffers.Binary;

namespace AetherBoy.Runtime;

internal sealed class GbaCheatCipher
{
    private uint[] shark = [0x09F4FBBD, 0x9681884A, 0x352027E9, 0xF3DEE5A7];
    private uint[] replay = [0x7AA9648F, 0x7FAE6994, 0xC0EFAAD5, 0x42712C57];
    private uint[] breaker = new uint[4];
    private byte[] permutation = new byte[48];
    private uint master;
    private uint random;

    internal GbaCheatCipher Clone() => new()
    {
        shark = (uint[])shark.Clone(), replay = (uint[])replay.Clone(),
        breaker = (uint[])breaker.Clone(), permutation = (byte[])permutation.Clone(),
        master = master, random = random
    };

    internal (uint, uint) DecodeTea(uint first, uint second, bool actionReplay)
    {
        uint[] keys = actionReplay ? replay : shark;
        unchecked
        {
            for (uint sum = 0xC6EF3720, round = 0; round < 32; round++, sum -= 0x9E3779B9)
            {
                second -= ((first << 4) + keys[2]) ^ (first + sum) ^ ((first >> 5) + keys[3]);
                first -= ((second << 4) + keys[0]) ^ (second + sum) ^ ((second >> 5) + keys[1]);
            }
        }
        return (first, second);
    }

    internal void ReseedTea(uint value, bool actionReplay)
    {
        var a = actionReplay ? GbaCheatTables.ActionReplay1 : GbaCheatTables.GameShark1;
        var b = actionReplay ? GbaCheatTables.ActionReplay2 : GbaCheatTables.GameShark2;
        uint[] keys = actionReplay ? replay : shark;
        for (int key = 0; key < 4; key++)
        {
            uint next = 0;
            for (int digit = 0; digit < 4; digit++)
                next = (next << 8) | (byte)(a[((value >> 8) + digit) & 255] + b[(value + key) & 255]);
            keys[key] = next;
        }
    }

    private uint Next()
    {
        unchecked
        {
            uint a = random * 0x41C64E6D + 0x3039;
            uint b = a * 0x41C64E6D + 0x3039;
            random = b * 0x41C64E6D + 0x3039;
            return ((a << 14) & 0xC0000000) | ((b >> 1) & 0x3FFF8000) | ((random >> 16) & 0x7FFF);
        }
    }

    internal void ReseedCodeBreaker(uint first, uint second)
    {
        random = (second & 255) ^ 0x1111;
        for (byte i = 0; i < permutation.Length; i++) permutation[i] = i;
        for (int i = 0; i < 80; i++)
        {
            int x = (int)(Next() % 48), y = (int)(Next() % 48);
            (permutation[x], permutation[y]) = (permutation[y], permutation[x]);
        }
        random = 0x4EFAD1C3;
        for (uint i = 0; i < ((first >> 24) & 15); i++) random = Next();
        breaker[2] = Next(); breaker[3] = Next();
        random = (second >> 8) ^ 0xF254;
        for (uint i = 0; i < (second >> 8); i++) random = Next();
        breaker[0] = Next(); breaker[1] = Next();
        master = first;
    }

    internal (uint, uint) DecodeCodeBreaker(uint first, uint second)
    {
        if (master == 0) return (first, second);
        Span<byte> data = stackalloc byte[6];
        BinaryPrimitives.WriteUInt32BigEndian(data, first);
        BinaryPrimitives.WriteUInt16BigEndian(data[4..], (ushort)second);
        for (int i = 47; i >= 0; i--)
        {
            int j = permutation[i];
            int difference = ((data[i / 8] >> (i % 8)) ^ (data[j / 8] >> (j % 8))) & 1;
            data[i / 8] ^= (byte)(difference << (i % 8));
            data[j / 8] ^= (byte)(difference << (j % 8));
        }
        first = BinaryPrimitives.ReadUInt32BigEndian(data) ^ breaker[0];
        second = (uint)(BinaryPrimitives.ReadUInt16BigEndian(data[4..]) ^ (ushort)breaker[1]);
        BinaryPrimitives.WriteUInt32BigEndian(data, first);
        BinaryPrimitives.WriteUInt16BigEndian(data[4..], (ushort)second);
        for (int i = 0; i < 5; i++) data[i] ^= (byte)((master >> 8) ^ data[i + 1]);
        data[5] ^= (byte)(master >> 8);
        for (int i = 5; i > 0; i--) data[i] ^= (byte)(master ^ data[i - 1]);
        data[0] ^= (byte)master;
        return (BinaryPrimitives.ReadUInt32BigEndian(data) ^ breaker[2],
            (uint)(BinaryPrimitives.ReadUInt16BigEndian(data[4..]) ^ (ushort)breaker[3]));
    }
}
