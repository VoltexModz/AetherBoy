using System;
using System.Threading.Tasks;

namespace nanoboy.Storage;

// One background writer owns each destination. New snapshots supersede older, not-yet-written
// snapshots; a completed older write can never be mistaken for the latest generation.
internal sealed class OrderedSnapshotWriter<T>
{
    private readonly object sync = new();
    private readonly Action<T> write;
    private T? pending;
    private long requestedGeneration;
    private long persistedGeneration;
    private bool running;
    private bool failureReported;
    private Exception? lastError;
    private Task worker = Task.CompletedTask;

    internal event Action<Exception>? SaveFailed;

    internal OrderedSnapshotWriter(Action<T> write) => this.write = write;

    internal bool HasPending
    {
        get { lock (sync) return persistedGeneration < requestedGeneration; }
    }

    internal long Enqueue(T snapshot)
    {
        lock (sync)
        {
            pending = snapshot;
            long generation = ++requestedGeneration;
            if (!running) StartWorker();
            return generation;
        }
    }

    internal async Task FlushAsync()
    {
        Task current;
        long target;
        lock (sync)
        {
            target = requestedGeneration;
            if (persistedGeneration >= target) return;
            if (!running) StartWorker();
            current = worker;
        }
        await current.ConfigureAwait(false);
        lock (sync)
        {
            if (persistedGeneration < target)
                throw new InvalidOperationException("Die letzte Änderung konnte nicht gespeichert werden.", lastError);
        }
    }

    private void StartWorker()
    {
        running = true;
        worker = Task.Run(WritePending);
    }

    private void WritePending()
    {
        while (true)
        {
            T snapshot;
            long generation;
            lock (sync)
            {
                if (pending is null) { running = false; return; }
                snapshot = pending;
                generation = requestedGeneration;
                pending = default;
            }

            try
            {
                write(snapshot);
                lock (sync)
                {
                    persistedGeneration = generation;
                    lastError = null;
                    failureReported = false;
                    if (pending is null) { running = false; return; }
                }
            }
            catch (Exception exception)
            {
                bool report;
                lock (sync)
                {
                    if (pending is null) pending = snapshot;
                    lastError = exception;
                    running = false;
                    report = !failureReported;
                    failureReported = true;
                }
                if (report)
                {
                    try { SaveFailed?.Invoke(exception); }
                    catch { /* A notification handler cannot discard the pending snapshot. */ }
                }
                return;
            }
        }
    }
}
