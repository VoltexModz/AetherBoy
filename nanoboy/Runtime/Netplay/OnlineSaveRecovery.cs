using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using nanoboy.Core;

namespace AetherBoy.Runtime.Netplay;

public enum OnlineSaveRecoveryState { Active, CleanStopped, Interrupted, Faulted, Promoting, Promoted, Invalid }

public sealed record OnlineSaveRecoveryInfo(string SessionDirectory, string OriginalSavePath,
    string WorkingSavePath, OnlineSaveRecoveryState State, bool CanPromote, bool IsUncertain, string Reason);

/// <summary>Explicit, local-only recovery. A clean program exit is NOT proof of a successful trade.</summary>
public static class OnlineSaveRecovery
{
    public static OnlineSaveRecoveryInfo Inspect(string sessionDirectory)
    {
        string directory = Path.GetFullPath(sessionDirectory);
        try
        {
            OnlineSaveJournal journal = OnlineSaveFiles.ReadJournal(directory);
            try
            {
                using var marker = OnlineSaveFiles.LockMarker(directory, journal);
                using var source = OnlineSaveFiles.LockOriginal(journal.OriginalSavePath);
                return Evaluate(directory, journal);
            }
            catch (IOException)
            {
                return Info(directory, journal, false, true, "The session or original save is in use or cannot be locked.");
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return new(directory, "", Path.Combine(directory, "game.sav"), OnlineSaveRecoveryState.Invalid,
                false, true, "The session journal or its files could not be safely validated.");
        }
    }

    /// <summary>
    /// Requires explicit user confirmation. expectedOriginalSavePath must come from the selected cartridge,
    /// never blindly from a journal. Returns the retained backup directory.
    /// </summary>
    public static string Promote(string sessionDirectory, string expectedOriginalSavePath, Action? beforeImport = null)
    {
        string directory = Path.GetFullPath(sessionDirectory);
        OnlineSaveJournal journal = OnlineSaveFiles.ReadJournal(directory);
        if (!string.Equals(Path.GetFullPath(expectedOriginalSavePath), journal.OriginalSavePath, OnlineSaveFiles.PathComparison))
            throw new InvalidOperationException("This session belongs to another cartridge's save path.");
        using var marker = OnlineSaveFiles.LockMarker(directory, journal);
        using var source = OnlineSaveFiles.LockOriginal(journal.OriginalSavePath);
        OnlineSaveRecoveryInfo status = Evaluate(directory, journal);
        if (!status.CanPromote) throw new InvalidOperationException(status.Reason);
        string working = Path.Combine(directory, "game.sav");
        byte[] battery = OnlineSaveFiles.ReadChecked(working, journal.Working![""])!;
        byte[]? rtc = OnlineSaveFiles.ReadChecked(working + ".rtc", journal.Working[".rtc"]);
        string backup = Path.Combine(directory, "original-before-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backup);
        foreach (string suffix in OnlineSaveFiles.Suffixes)
            if (journal.Original[suffix] is not null)
                OnlineSaveFiles.CopyNew(journal.OriginalSavePath + suffix, Path.Combine(backup, "game.sav") + suffix);
        if (!OnlineSaveFiles.SameFamily(journal.Original, OnlineSaveFiles.FingerprintFamily(Path.Combine(backup, "game.sav"))) ||
            !OnlineSaveFiles.SameFamily(journal.Original, OnlineSaveFiles.FingerprintFamily(journal.OriginalSavePath)))
            throw new IOException("The original changed while its import backup was prepared. Nothing was overwritten.");
        // Frontends archive stale auto-resume files here, under the same cartridge lifetime lock.
        // No callback is invoked for a rejected, active, uncertain or changed session.
        beforeImport?.Invoke();
        journal.State = OnlineSaveRecoveryState.Promoting;
        journal.BackupDirectoryName = Path.GetFileName(backup);
        journal.Reason = "Import started. An interrupted import requires manual review of the retained backup and working copies.";
        OnlineSaveFiles.WriteJournal(directory, journal);
        // Each file uses the existing durable atomic writer. Battery+RTC is NOT one filesystem
        // transaction: the Promoting journal and complete old family cover that crash window.
        BatterySaveStore.Restore(journal.OriginalSavePath, battery.Length, battery);
        if (rtc is not null) BatterySaveStore.Restore(journal.OriginalSavePath + ".rtc", rtc.Length, rtc);
        journal.State = OnlineSaveRecoveryState.Promoted;
        journal.Reason = "The user explicitly imported this locally reviewed working copy. This does not attest the peer's outcome.";
        OnlineSaveFiles.WriteJournal(directory, journal);
        return backup;
    }

    private static OnlineSaveRecoveryInfo Evaluate(string directory, OnlineSaveJournal journal)
    {
        if (journal.State != OnlineSaveRecoveryState.CleanStopped)
            return Info(directory, journal, false, journal.State != OnlineSaveRecoveryState.Promoted,
                journal.State == OnlineSaveRecoveryState.Active
                    ? "The owner did not record a clean end; this session was interrupted."
                    : journal.Reason ?? "Only a cleanly stopped session can be reviewed for import.");
        string working = Path.Combine(directory, "game.sav");
        if (!OnlineSaveFiles.SameFamily(journal.Original, OnlineSaveFiles.FingerprintFamily(journal.OriginalSavePath)))
            return Info(directory, journal, false, true, "The original save family changed after this session began. Nothing will be overwritten.");
        if (journal.Working is null || !OnlineSaveFiles.SameFamily(journal.Working, OnlineSaveFiles.FingerprintFamily(working)))
            return Info(directory, journal, false, true, "The working files changed after the clean session ended. Review them manually.");
        OnlineSaveFingerprint? primary = journal.Working[""];
        if (primary is null || primary.Length == 0 || !BatterySaveStore.Inspect(working, checked((int)primary.Length))[0].IsValid)
            return Info(directory, journal, false, true, "The working battery save is missing, empty or fails its integrity check.");
        if (journal.Original[""] is { } original && original.Length != 0 && original.Length != primary.Length)
            return Info(directory, journal, false, true, "The battery save size changed. Cartridge-specific manual review is required.");
        if (journal.Original[".rtc"] is not null && journal.Working[".rtc"] is null)
            return Info(directory, journal, false, true, "The working RTC companion is missing.");
        if (journal.Working[".rtc"] is { } rtc && (rtc.Length == 0 ||
            !BatterySaveStore.Inspect(working + ".rtc", checked((int)rtc.Length))[0].IsValid))
            return Info(directory, journal, false, true, "The working RTC companion fails its integrity check.");
        return Info(directory, journal, true, false,
            "Clean local exit and intact files. Verify the saved result on BOTH peers before explicitly importing; a clean exit does not prove a successful trade.");
    }

    private static OnlineSaveRecoveryInfo Info(string directory, OnlineSaveJournal journal, bool canPromote, bool uncertain, string reason) =>
        new(directory, journal.OriginalSavePath, Path.Combine(directory, "game.sav"), journal.State, canPromote, uncertain, reason);
}

internal sealed record OnlineSaveFingerprint(long Length, string Sha256);

internal sealed class OnlineSaveJournal
{
    public int Version { get; set; } = 1;
    public string SessionId { get; set; } = "";
    public string OriginalSavePath { get; set; } = "";
    public string? ProfileId { get; set; }
    public DateTimeOffset StartedUtc { get; set; }
    public DateTimeOffset? EndedUtc { get; set; }
    public OnlineSaveRecoveryState State { get; set; }
    public string? Reason { get; set; }
    public string? BackupDirectoryName { get; set; }
    public Dictionary<string, OnlineSaveFingerprint?> Original { get; set; } = new();
    public Dictionary<string, OnlineSaveFingerprint?>? Working { get; set; }
}

internal static class OnlineSaveFiles
{
    internal const string MarkerName = ".online-session";
    internal const string JournalName = "online-session.json";
    private const long MaximumFileSize = 4 * 1024 * 1024;
    internal static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    internal static readonly string[] Suffixes = (from rtc in new[] { "", ".rtc" }
        from generation in Enumerable.Range(0, 4) from guard in new[] { "", ".guard", ".guard.next" }
        select rtc + (generation == 0 ? "" : ".bak" + generation) + guard).ToArray();

    internal static Dictionary<string, OnlineSaveFingerprint?> FingerprintFamily(string path)
    {
        var result = new Dictionary<string, OnlineSaveFingerprint?>(StringComparer.Ordinal);
        EnsurePlainPath(Path.GetDirectoryName(Path.GetFullPath(path))!);
        foreach (string suffix in Suffixes)
        {
            string candidate = path + suffix;
            EnsurePlainEntry(candidate);
            if (!File.Exists(candidate)) { result.Add(suffix, null); continue; }
            using var stream = new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaximumFileSize) throw new InvalidDataException("Battery/RTC file exceeds the safe copy limit.");
            result.Add(suffix, new(stream.Length, Convert.ToHexString(SHA256.HashData(stream))));
        }
        return result;
    }

    internal static bool SameFamily(Dictionary<string, OnlineSaveFingerprint?> a, Dictionary<string, OnlineSaveFingerprint?> b) =>
        Suffixes.All(suffix => a.TryGetValue(suffix, out var left) && b.TryGetValue(suffix, out var right) && left == right);

    internal static void CopyNew(string source, string destination)
    {
        EnsurePlainPath(source); EnsurePlainPath(destination);
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > MaximumFileSize) throw new InvalidDataException("Battery/RTC file exceeds the safe copy limit.");
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        input.CopyTo(output); output.Flush(flushToDisk: true);
    }

    internal static byte[]? ReadChecked(string path, OnlineSaveFingerprint? expected)
    {
        EnsurePlainPath(path);
        if (expected is null)
        {
            if (File.Exists(path)) throw new IOException("Working files changed during import preparation.");
            return null;
        }
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length != expected.Length || stream.Length > MaximumFileSize)
            throw new InvalidDataException("Working file size changed during import preparation.");
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(expected.Sha256, StringComparison.Ordinal))
            throw new InvalidDataException("Working file content changed during import preparation.");
        return bytes;
    }

    internal static void EnsurePlainPath(string path)
    {
        string? current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            EnsurePlainEntry(current);
            current = Path.GetDirectoryName(current);
        }
    }

    private static void EnsurePlainEntry(string path)
    {
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Online save recovery does not follow symbolic links or junctions.");
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
    }

    internal static FileStream LockOriginal(string source)
    {
        EnsurePlainPath(source + ".lock");
        // Inspect must never create directories or lock files named by an untrusted journal.
        // Every genuine workspace already created this persistent lock inode at startup.
        return new FileStream(source + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    internal static FileStream LockMarker(string directory, OnlineSaveJournal journal)
    {
        string path = Path.Combine(directory, MarkerName);
        EnsurePlainPath(path);
        var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        try
        {
            if (stream.Length != 32) throw new InvalidDataException("Session identity is invalid.");
            Span<byte> bytes = stackalloc byte[32]; stream.ReadExactly(bytes);
            if (!Encoding.ASCII.GetString(bytes).Equals(journal.SessionId, StringComparison.Ordinal))
                throw new InvalidDataException("Session identity does not match its journal.");
            return stream;
        }
        catch { stream.Dispose(); throw; }
    }

    internal static OnlineSaveJournal ReadJournal(string directory)
    {
        string path = Path.Combine(directory, JournalName);
        EnsurePlainPath(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 64 * 1024) throw new InvalidDataException("Session journal exceeds its safe size limit.");
        OnlineSaveJournal value = JsonSerializer.Deserialize<OnlineSaveJournal>(stream)
            ?? throw new InvalidDataException("Session journal is empty.");
        if (value.Version != 1 || !Guid.TryParseExact(value.SessionId, "N", out _) ||
            !Enum.IsDefined(value.State) || !Path.IsPathFullyQualified(value.OriginalSavePath) ||
            !string.Equals(Path.GetFullPath(value.OriginalSavePath), value.OriginalSavePath, PathComparison) ||
            value.OriginalSavePath.StartsWith(Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar, PathComparison) ||
            value.ProfileId?.Length > 128 || value.Reason?.Length > 1024)
            throw new InvalidDataException("Session journal metadata is invalid.");
        ValidateFamily(value.Original);
        if (value.Working is not null) ValidateFamily(value.Working);
        EnsurePlainPath(value.OriginalSavePath);
        return value;
    }

    private static void ValidateFamily(Dictionary<string, OnlineSaveFingerprint?>? family)
    {
        if (family is null || family.Count != Suffixes.Length || Suffixes.Any(suffix => !family.ContainsKey(suffix)))
            throw new InvalidDataException("Session file family is invalid.");
        foreach (var item in family.Values)
            if (item is not null && (item.Length < 0 || item.Length > MaximumFileSize ||
                item.Sha256 is null || item.Sha256.Length != 64 || item.Sha256.Any(c => !Uri.IsHexDigit(c))))
                throw new InvalidDataException("Session file fingerprint is invalid.");
    }

    internal static void WriteJournal(string directory, OnlineSaveJournal value)
    {
        string path = Path.Combine(directory, JournalName);
        EnsurePlainPath(path);
        string temporary = Path.Combine(directory, ".journal-" + Guid.NewGuid().ToString("N"));
        byte[] data = JsonSerializer.SerializeToUtf8Bytes(value);
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        { stream.Write(data); stream.Flush(flushToDisk: true); }
        File.Move(temporary, path, overwrite: true);
    }
}
