using System;
using System.IO;
using AetherBoy.Runtime.Storage;

namespace AetherBoy.Runtime.Netplay;

/// <summary>Work on a private battery/RTC family; a network failure never writes the original.</summary>
internal sealed class OnlineSaveWorkspace : IDisposable
{
    private readonly RomWriteLease sourceLease;
    private readonly FileStream marker;
    private readonly string directory;
    private readonly OnlineSaveJournal journal;
    private bool disposed, completed;
    internal string SavePath { get; }

    internal OnlineSaveWorkspace(string originalSavePath, string sessionDirectory, string? profileId = null)
    {
        string source = Path.GetFullPath(originalSavePath);
        directory = Path.GetFullPath(sessionDirectory);
        SavePath = Path.Combine(directory, "game.sav");
        if (string.Equals(source, directory, OnlineSaveFiles.PathComparison) ||
            source.StartsWith(Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar, OnlineSaveFiles.PathComparison))
            throw new ArgumentException("The original save cannot be inside its online working directory.", nameof(sessionDirectory));
        if (Directory.Exists(directory) || File.Exists(directory))
            throw new IOException("An online session needs a new, unused save directory.");
        OnlineSaveFiles.EnsurePlainPath(source + ".lock");
        OnlineSaveFiles.EnsurePlainPath(directory);
        sourceLease = RomWriteLease.Acquire(source + ".lock");
        FileStream? claimed = null;
        try
        {
            Directory.CreateDirectory(directory);
            // Hold the same inode through the owner's final battery flush.
            claimed = new FileStream(Path.Combine(directory, OnlineSaveFiles.MarkerName), FileMode.CreateNew,
                FileAccess.ReadWrite, FileShare.None);
            journal = new OnlineSaveJournal
            {
                SessionId = Guid.NewGuid().ToString("N"), OriginalSavePath = source,
                ProfileId = profileId, StartedUtc = DateTimeOffset.UtcNow, State = OnlineSaveRecoveryState.Active,
                Original = OnlineSaveFiles.FingerprintFamily(source)
            };
            claimed.Write(System.Text.Encoding.ASCII.GetBytes(journal.SessionId)); claimed.Flush(flushToDisk: true);
            foreach (string suffix in OnlineSaveFiles.Suffixes)
                if (journal.Original[suffix] is not null) OnlineSaveFiles.CopyNew(source + suffix, SavePath + suffix);
            if (!OnlineSaveFiles.SameFamily(journal.Original, OnlineSaveFiles.FingerprintFamily(SavePath)) ||
                !OnlineSaveFiles.SameFamily(journal.Original, OnlineSaveFiles.FingerprintFamily(source)))
                throw new IOException("The source changed while its online working copy was created.");
            OnlineSaveFiles.WriteJournal(directory, journal);
            marker = claimed;
        }
        catch { claimed?.Dispose(); sourceLease.Dispose(); throw; }
    }

    /// <summary>Called only after the owner has flushed and disposed its private core.</summary>
    internal void Complete(bool success, string? reason = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (completed) return;
        journal.Working = OnlineSaveFiles.FingerprintFamily(SavePath);
        journal.EndedUtc = DateTimeOffset.UtcNow;
        journal.State = success ? OnlineSaveRecoveryState.CleanStopped : OnlineSaveRecoveryState.Faulted;
        // Do not persist arbitrary exception text; it may contain connection secrets.
        journal.Reason = success ? "Owner stopped and flushed its private files. Trade outcome still requires manual verification."
            : "The owner reported a failure; the trade outcome is uncertain.";
        OnlineSaveFiles.WriteJournal(directory, journal);
        completed = true;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try
        {
            if (!completed)
            {
                journal.State = OnlineSaveRecoveryState.Interrupted;
                journal.EndedUtc = DateTimeOffset.UtcNow;
                journal.Reason = "The owner did not confirm a clean final flush; the trade outcome is uncertain.";
                OnlineSaveFiles.WriteJournal(directory, journal);
            }
        }
        finally { try { marker.Dispose(); } finally { sourceLease.Dispose(); } }
    }
}
