using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AetherBoy.Runtime;

/// <summary>Retains per-game deltas on failed writes and while a background checkpoint is in flight.</summary>
public sealed class PlaytimeJournal
{
    private readonly object sync = new(), writer = new();
    private readonly Dictionary<string, double> pending = new(StringComparer.Ordinal);
    public void Add(string identity, double seconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (seconds == 0) return;
        lock (sync) pending[identity] = pending.GetValueOrDefault(identity) + seconds;
    }

    public IReadOnlyList<Exception> Flush(Action<string, double> persist)
    {
        lock (writer)
        {
            KeyValuePair<string, double>[] batch;
            lock (sync) batch = pending.ToArray();
            var failures = new List<Exception>();
            foreach (var item in batch)
            {
                try
                {
                    persist(item.Key, item.Value);
                    lock (sync)
                    {
                        double remainder = pending[item.Key] - item.Value;
                        if (remainder > 0) pending[item.Key] = remainder; else pending.Remove(item.Key);
                    }
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { failures.Add(ex); }
            }
            return failures;
        }
    }
}
