using System;

namespace nanoboy.Core
{
    /// <summary>Conservative header heuristic for the documented 1 MiB MBC1M layout.</summary>
    internal static class Mbc1MulticartDetector
    {
        internal static bool LooksLikeMulticart(ReadOnlySpan<byte> rom)
        {
            if (rom.Length != 0x100000)
                return false;

            const int secondGame = 0x10 * 0x4000;
            ReadOnlySpan<byte> firstLogo = rom.Slice(0x104, 48);
            ReadOnlySpan<byte> secondLogo = rom.Slice(secondGame + 0x104, 48);
            // No copyrighted logo bytes are embedded. Matching empty/fill data is
            // not evidence of a second cartridge header.
            bool hasVariation = false;
            foreach (byte value in firstLogo)
                hasVariation |= value != firstLogo[0];

            return hasVariation && firstLogo.SequenceEqual(secondLogo) &&
                HasValidHeaderChecksum(rom) && HasValidHeaderChecksum(rom[secondGame..]);
        }

        private static bool HasValidHeaderChecksum(ReadOnlySpan<byte> cartridge)
        {
            int sum = 0;
            for (int index = 0x134; index <= 0x14C; index++)
                sum += cartridge[index] + 1;
            return unchecked((byte)-sum) == cartridge[0x14D];
        }
    }
}
