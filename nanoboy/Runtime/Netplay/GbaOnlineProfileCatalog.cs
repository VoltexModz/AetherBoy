using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AetherBoy.Runtime.Cartridges;

namespace AetherBoy.Runtime.Netplay;

/// <summary>Identification is not a successful-trade compatibility claim.</summary>
public sealed record GbaOnlineCompatibility(bool IsDevelopmentCandidate, string ProfileId,
    string DisplayName, string GameCode, string RomSha256, string Reason)
{
    public bool IsVerified => false; // No real-game/real-WAN acceptance matrix has been completed yet.
}

public static class GbaOnlineProfileCatalog
{
    public const string PokemonGen3Profile = "gba-pokemon-gen3-v1";
    private sealed record Candidate(string Code, byte Revision, string Name);
    // Exact full-image SHA-1 identifiers from pinned primary pret checksum files, not filenames/headers.
    // SHA-1 is used only for published cartridge identification, never network authentication.
    // pokeruby: 63a8cbf0016b351a4e68f7036fa0b77e23d2f2c1, ruby*.sha1 / sapphire*.sha1
    // pokefirered: c75f352304d529f6ba92d4f74b9cf8b5c3810788, firered*.sha1 / leafgreen*.sha1
    // pokeemerald: 5eff78649e7170a877b961ef0b3da13b81a16038, rom.sha1
    private static readonly IReadOnlyDictionary<string, Candidate> Candidates = new Dictionary<string, Candidate>(StringComparer.OrdinalIgnoreCase)
    {
        ["f28b6ffc97847e94a6c21a63cacf633ee5c8df1e"] = new("AXVE", 0, "Pokémon Ruby · English · rev 0"),
        ["610b96a9c9a7d03d2bafb655e7560ccff1a6d894"] = new("AXVE", 1, "Pokémon Ruby · English · rev 1"),
        ["5b64eacf892920518db4ec664e62a086dd5f5bc8"] = new("AXVE", 2, "Pokémon Ruby · English · rev 2"),
        ["3ccbbd45f8553c36463f13b938e833f652b793e4"] = new("AXPE", 0, "Pokémon Sapphire · English · rev 0"),
        ["4722efb8cd45772ca32555b98fd3b9719f8e60a9"] = new("AXPE", 1, "Pokémon Sapphire · English · rev 1"),
        ["89b45fb172e6b55d51fc0e61989775187f6fe63c"] = new("AXPE", 2, "Pokémon Sapphire · English · rev 2"),
        ["1c2a53332382e14dab8815e3a6dd81ad89534050"] = new("AXVD", 0, "Pokémon Rubin · Deutsch · rev 0"),
        ["424740be1fc67a5ddb954794443646e6aeee2c1b"] = new("AXVD", 1, "Pokémon Rubin · Deutsch · rev 1"),
        ["fa0bd1abe04fea17016f585454d0f1392f342a21"] = new("AXPD", 0, "Pokémon Saphir · Deutsch · rev 0"),
        ["7e6e034f9cdca6d2c4a270fdb50a94def5883d17"] = new("AXPD", 1, "Pokémon Saphir · Deutsch · rev 1"),
        ["41cb23d8dccc8ebd7c649cd8fbb58eeace6e2fdc"] = new("BPRE", 0, "Pokémon FireRed · English · rev 0"),
        ["dd5945db9b930750cb39d00c84da8571feebf417"] = new("BPRE", 1, "Pokémon FireRed · English · rev 1"),
        ["574fa542ffebb14be69902d1d36f1ec0a4afd71e"] = new("BPGE", 0, "Pokémon LeafGreen · English · rev 0"),
        ["7862c67bdecbe21d1d69ce082ce34327e1c6ed5e"] = new("BPGE", 1, "Pokémon LeafGreen · English · rev 1"),
        ["f3ae088181bf583e55daf962a92bb46f4f1d07b7"] = new("BPEE", 0, "Pokémon Emerald · English · rev 0"),
    };

    public static GbaOnlineCompatibility InspectRom(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length is < GbaRomInfo.HeaderLength or > GbaRomInfo.MaximumRomLength)
            throw new InvalidDataException("Invalid GBA cartridge length.");
        byte[] data = new byte[checked((int)input.Length)];
        input.ReadExactly(data);
        string code = Encoding.ASCII.GetString(data, 0xAC, 4);
        return MatchFingerprint(Convert.ToHexString(SHA1.HashData(data)), code, data[0xBC],
            Convert.ToHexString(SHA256.HashData(data)), data[0xB2] == 0x96);
    }

    internal static GbaOnlineCompatibility MatchFingerprint(string sha1, string code, byte revision, string sha256, bool validHeader)
    {
        if (validHeader && Candidates.TryGetValue(sha1, out Candidate? candidate) && candidate.Code == code && candidate.Revision == revision)
            return new(true, PokemonGen3Profile, candidate.Name, code, sha256,
                "Recognized original build; DEVELOPMENT ONLY. Pokémon trades and WAN interoperability are not verified. Explicit consent and private save copies are required.");
        return new(false, PokemonGen3Profile, "Unrecognized GBA build", code, sha256,
            "This exact ROM build has no development profile. Headers and names do not prove an unchanged link protocol; hacks and unknown revisions are not automatically enabled.");
    }

    // Friend-test seam: generated ARM programs exercise the real owner without making an unknown public ROM eligible.
    internal static GbaOnlineCompatibility CreateSyntheticTestProfile(string path) => new(true, PokemonGen3Profile,
        "Synthetic Gen3 protocol test", "TEST", Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), "Own test cartridge, not a released profile.");
}
