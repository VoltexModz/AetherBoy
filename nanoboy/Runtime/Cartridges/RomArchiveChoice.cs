using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AetherBoy.Runtime.Cartridges;

/// <summary>A choice is bound to the exact archive bytes, not merely a filename.</summary>
public sealed class RomArchiveChoice
{
    internal string ArchiveSha256 { get; }
    internal int EntryIndex { get; }
    public string DisplayName { get; }
    public long Size { get; }
    public string System { get; }
    internal RomArchiveChoice(string hash, int index, string name, long size)
    {
        ArchiveSha256 = hash; EntryIndex = index; Size = size;
        DisplayName = string.Concat(name.Select(c => char.IsControl(c) ||
            char.GetUnicodeCategory(c) == UnicodeCategory.Format ? ' ' : c));
        System = Path.GetExtension(name).TrimStart('.').ToUpperInvariant();
    }
}

/// <summary>No payload or saves have been extracted when a selection is requested.</summary>
public sealed class RomArchiveSelectionRequiredException : IOException
{
    public IReadOnlyList<RomArchiveChoice> Choices { get; }
    internal RomArchiveSelectionRequiredException(RomArchiveChoice[] choices)
        : base("Choose one game from this archive.") => Choices = Array.AsReadOnly(choices);
}
