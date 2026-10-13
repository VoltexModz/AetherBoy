using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using nanoboy.Core;
using nanoboy.Storage;

namespace nanoboy.Controls;

internal enum AetherSaveSlotState { NoGame, Reading, Empty, Saved, Unreadable }

/// <summary>Owner-thread snapshot; disk work never runs in the UI loop.</summary>
internal sealed class AetherSaveSlotInventory
{
    private sealed record ReadResult(long Generation, AetherSaveSlotState[] States);
    private object? owner;
    private string? rom;
    private long generation;
    private long nextRead;
    private Task<ReadResult>? pending;
    private CancellationTokenSource? cancellation;
    private AetherSaveSlotState[] states = Enumerable.Repeat(AetherSaveSlotState.NoGame, 5).ToArray();
    internal Func<string, int, AetherSaveSlotState> Inspect { get; init; } =
        (rom, slot) => InspectFile(WindowsSaveStateStore.Default.PathFor(rom, slot));
    internal bool IsReading => pending is not null;
    internal AetherSaveSlotState this[int slot] => slot is >= 1 and <= 5 ? states[slot - 1] : AetherSaveSlotState.NoGame;
    internal void Invalidate()
    {
        generation++; nextRead = 0; CancelPending();
        states = Enumerable.Repeat(owner is null || rom is null ? AetherSaveSlotState.NoGame : AetherSaveSlotState.Reading, 5).ToArray();
    }
    internal void Update(object? sessionOwner, string? romPath)
    {
        if (!ReferenceEquals(owner, sessionOwner) || rom != romPath)
        {
            owner = sessionOwner; rom = romPath; Invalidate();
        }
        if (pending is { IsCompleted: true })
        {
            ReadResult result = pending.GetAwaiter().GetResult(); pending = null;
            cancellation?.Dispose(); cancellation = null;
            if (generation == result.Generation) states = result.States;
        }
        if (owner is null || rom is null || pending is not null || Environment.TickCount64 < nextRead) return;
        string requestedRom = rom; long requestedGeneration = generation;
        var source = cancellation = new CancellationTokenSource(); var inspect = Inspect;
        nextRead = Environment.TickCount64 + 2000;
        pending = Task.Run(() =>
        {
            var result = new AetherSaveSlotState[5];
            for (int slot = 1; slot <= 5; slot++)
            {
                // Filesystem calls can be uncancellable. Do not delay a new session on an old call.
                if (source.IsCancellationRequested) break;
                try { result[slot - 1] = inspect(requestedRom, slot); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
                { result[slot - 1] = AetherSaveSlotState.Unreadable; }
            }
            return new ReadResult(requestedGeneration, result);
        });
    }
    private void CancelPending()
    {
        cancellation?.Cancel();
        if (cancellation is { } source)
        {
            if (pending is { } task) _ = task.ContinueWith(_ => source.Dispose(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            else source.Dispose();
        }
        cancellation = null; pending = null;
    }
    internal static AetherSaveSlotState InspectFile(string path)
    {
        try
        {
            // Exists masks access errors. Opening the state distinguishes absence from unreadability.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is < 82 or > EmulatorStateCodec.MaximumDocumentLength) return AetherSaveSlotState.Unreadable;
            Span<byte> header = stackalloc byte[8]; stream.ReadExactly(header);
            // This is an occupancy/readability hint; restore still checks integrity, ROM and schema.
            return header.SequenceEqual("AETHSTAT"u8) ||
                (stream.Length >= 84 && header.SequenceEqual("AETHGBA\0"u8))
                ? AetherSaveSlotState.Saved : AetherSaveSlotState.Unreadable;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return AetherSaveSlotState.Empty; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return AetherSaveSlotState.Unreadable; }
    }
}
