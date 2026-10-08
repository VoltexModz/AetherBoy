// Copyright (c) 2013-2020 Jeffrey Pfau
// C# adaptation and bounds checks for AetherBoy, 2026.
// SPDX-License-Identifier: MPL-2.0
// Derived from mGBA src/gba/cart/ereader.c. See THIRD_PARTY_NOTICES.md.
namespace GameboyAdvanced.Core.Rom;

/// <summary>Digital dot strips, not photographs, JAN numbers or save injections.</summary>
public static class EReaderDotCode
{
    public const int Stride = 1420, Height = 40, MaximumFileLength = 5456;
    private static readonly int[] Nibbles = [0, 1, 2, 18, 4, 5, 6, 22, 8, 9, 10, 20, 12, 13, 17, 16];
    private static readonly ushort[] Addresses = [1023,1174,2628,3373,4233,6112,6450,7771,8826,9491,11201,11432,
        12556,13925,14519,16350,16629,18332,18766,20007,21379,21738,23096,23889,24944,26137,26827,28578,
        29190,30063,31677,31956,33410,34283,35641,35920,37364,38557,38991,40742,41735,42094,43708,44501,
        45169,46872,47562,48803,49544,50913,51251,53082,54014,54679];

    public static bool IsSupportedLength(int length) => length is 1872 or 2912 or 3520 or 5456;

    public static byte[] Decode(ReadOnlySpan<byte> card)
    {
        if (!IsSupportedLength(card.Length))
            throw new InvalidDataException("Use a raw e-Reader strip (1872/2912 bytes) or packed dot bitmap (3520/5456 bytes). Images and decoded BIN cards are not supported yet.");
        byte[] dots = new byte[Stride * Height];
        if (card.Length is 3520 or 5456)
        {
            int rowBytes = card.Length / 44;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < rowBytes * 8; x++)
                    if (rowBytes != 124 || x < 987)
                        dots[y * Stride + 200 + x] = (byte)((card[(y + 2) * rowBytes + x / 8] >> (7 - x % 8)) & 1);
            return dots;
        }
        int blocks = card.Length / 104, addressBase = blocks == 18 ? 1 : 25;
        for (int b = 0; b <= blocks; b++)
        {
            int left = 200 + b * 35;
            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 5; x++)
                    if (y is not (0 or 4) || x is >= 1 and <= 3)
                    { dots[y * Stride + left + x] = 1; dots[(35 + y) * Stride + left + x] = 1; }
            dots[7 * Stride + left + 2] = 1;
            for (int y = 0; y < 16; y++)
                dots[(16 + y) * Stride + left + 2] = (byte)((Addresses[addressBase + b] >> (15 - y)) & 1);
        }
        Span<byte> encoded = stackalloc byte[1040];
        for (int b = 0; b < blocks; b++)
        {
            int left = 200 + b * 35;
            foreach (int x in new int[] { 8,10,12,14,16,18,21,23,25,27,29,31 })
            { dots[2 * Stride + left + x] = 1; dots[37 * Stride + left + x] = 1; }
            for (int i = 0; i < 104; i++)
            {
                byte value = card[b * 104 + i];
                for (int bit = 0; bit < 5; bit++)
                {
                    encoded[i * 10 + bit] = (byte)((Nibbles[value >> 4] >> (4 - bit)) & 1);
                    encoded[i * 10 + bit + 5] = (byte)((Nibbles[value & 15] >> (4 - bit)) & 1);
                }
            }
            int offset = 0;
            for (int y = 4; y < 36; y++)
            {
                bool narrow = y < 7 || y >= 33;
                int length = narrow ? 26 : 34;
                encoded.Slice(offset, length).CopyTo(dots.AsSpan(y * Stride + left + (narrow ? 7 : 3), length));
                offset += length;
            }
        }
        return dots;
    }
}
