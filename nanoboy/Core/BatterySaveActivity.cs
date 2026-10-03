using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace nanoboy.Core
{
    // Deliberately contains no paths, game titles or save contents.
    public sealed record BatterySaveActivity(int ThreadId, string Stage, int Generation,
        long ElapsedMilliseconds, long StageMilliseconds);

    internal sealed class BatterySaveOperation : IDisposable
    {
        private static readonly ConcurrentDictionary<int, BatterySaveOperation> active = new();
        private readonly long started = Stopwatch.GetTimestamp();
        private readonly int threadId = Environment.CurrentManagedThreadId;
        private readonly object sync = new();
        private long stageStarted = Stopwatch.GetTimestamp();
        private string stage = "prepare";
        private int generation;

        internal BatterySaveOperation() => active[threadId] = this;
        internal void SetStage(string value, int targetGeneration)
        {
            lock (sync)
            {
                stage = value;
                generation = targetGeneration;
                stageStarted = Stopwatch.GetTimestamp();
            }
        }
        private BatterySaveActivity Snapshot()
        {
            lock (sync)
                return new(threadId, stage, generation,
                    (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    (long)Stopwatch.GetElapsedTime(stageStarted).TotalMilliseconds);
        }
        internal void Annotate(Exception error)
        {
            BatterySaveActivity snapshot = Snapshot();
            error.Data["battery_save_stage"] = snapshot.Stage;
            error.Data["battery_save_generation"] = snapshot.Generation;
            error.Data["battery_save_elapsed_ms"] = snapshot.ElapsedMilliseconds;
        }
        internal static IReadOnlyList<BatterySaveActivity> GetActive() =>
            active.Values.Select(value => value.Snapshot()).ToArray();
        public void Dispose() => active.TryRemove(threadId, out _);
    }
}
