using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace AetherBoy.Runtime;

/// <summary>SemVer precedence, independent of dates, commit IDs and the machine's culture.</summary>
internal sealed class ReleaseVersion : IComparable<ReleaseVersion>
{
    private static readonly Regex Pattern = new(@"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$", RegexOptions.CultureInvariant);
    private readonly string[] core;
    private readonly string[] prerelease;
    public string Text { get; }
    public bool IsPrerelease => prerelease.Length != 0;

    private ReleaseVersion(string text, Match match)
    {
        Text = text;
        core = [match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value];
        prerelease = match.Groups[4].Success ? match.Groups[4].Value.Split('.') : [];
    }

    public static bool TryParse(string? value, out ReleaseVersion version)
    {
        version = null!;
        if (string.IsNullOrEmpty(value) || value.Length > 128) return false;
        if (value[0] == 'v') value = value[1..];
        Match match = Pattern.Match(value);
        if (!match.Success || match.Length != value.Length) return false;
        string[] pre = match.Groups[4].Value.Split('.');
        if (pre.Any(p => Numeric(p) && p.Length > 1 && p[0] == '0')) return false;
        version = new(value, match);
        return true;
    }

    private static bool Numeric(string value) => value.Length > 0 && value.All(c => c is >= '0' and <= '9');
    private static int CompareNumber(string a, string b) => a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b);

    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null) return 1;
        for (int i = 0; i < 3; i++)
        { int result = CompareNumber(core[i], other.core[i]); if (result != 0) return result; }
        if (!IsPrerelease || !other.IsPrerelease)
            return IsPrerelease == other.IsPrerelease ? 0 : IsPrerelease ? -1 : 1;
        for (int i = 0; i < Math.Min(prerelease.Length, other.prerelease.Length); i++)
        {
            string a = prerelease[i], b = other.prerelease[i];
            bool an = Numeric(a), bn = Numeric(b);
            int result = an && bn ? CompareNumber(a, b) : an != bn ? (an ? -1 : 1) : string.CompareOrdinal(a, b);
            if (result != 0) return result;
        }
        return prerelease.Length.CompareTo(other.prerelease.Length);
    }
}
