using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace AetherBoy.Runtime.Netplay;

public enum OnlineProbePhase { Connecting, Testing, Passed, Cancelled, Failed }
public enum OnlineProbeFailure { None, AccessKey, RoomMissing, RoomMismatch, ServerProfile, RateLimit, Timeout, Integrity, Connection }

public sealed record OnlineProbeConnectionState(string DisplayCode, bool RoomAdmitted, bool PeerPresent, bool Connected, string Stage);
public sealed record OnlineProbeSnapshot(OnlineProbePhase Phase, bool Active, bool Stopping,
    OnlineProbeConnectionState Connection, OnlineTransportProbeProgress Progress,
    OnlineTransportProbeResult? Result, OnlineProbeFailure Failure, string? DiagnosticPath, bool ReportWriteFailed);

/// <summary>Room metadata for the isolated probe; never exposes credentials or SDP.</summary>
public interface IOnlineProbeConnection : IOnlineLinkTransport
{
    string WireProfile { get; }
    OnlineProbeConnectionState ConnectionState { get; }
    OnlineRoomDiagnostics Diagnostics { get; }
    string? DiagnosticPath { get; }
    string? DiagnosticWriteError { get; }
}

public interface IOnlineProbeSession : IDisposable, IAsyncDisposable
{
    OnlineProbeSnapshot Snapshot { get; }
    Task Completion { get; }
    Task StopAsync();
    string GetDiagnosticReport();
}

/// <summary>A ROM-free owner shared by frontends. Poll snapshots on the UI thread; never block it on cleanup.</summary>
public sealed class OnlineProbeSession : IOnlineProbeSession
{
    private readonly IOnlineProbeConnection connection;
    private readonly CancellationTokenSource lifetime;
    private readonly object sync = new();
    private readonly TimeSpan responseTimeout;
    private readonly int samples;
    private bool stopRequested, lifetimeDisposed;
    private OnlineProbePhase phase = OnlineProbePhase.Connecting;
    private OnlineProbeFailure failure;
    private OnlineTransportProbeProgress progress;
    private OnlineTransportProbeResult? result;
    public Task Completion { get; }

    public OnlineProbeSession(OnlineRoomSettings settings, bool host, string code, string? diagnosticDirectory)
        : this(new OnlineRoomTransport(settings, host, code, OnlineTransportProbe.Profile, diagnosticDirectory)) { }

    internal OnlineProbeSession(IOnlineProbeConnection connection, int samples = 8, TimeSpan? responseTimeout = null)
    {
        if (connection.WireProfile != OnlineTransportProbe.Profile)
            throw new ArgumentException("A probe session requires its own transport-probe-v1 room.", nameof(connection));
        if (samples is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(samples));
        if (responseTimeout is { } timeout && timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(responseTimeout));
        this.connection = connection; this.samples = samples;
        this.responseTimeout = responseTimeout ?? TimeSpan.FromSeconds(20);
        lifetime = new(TimeSpan.FromMinutes(12));
        progress = new(0, 0, samples * 4);
        Completion = Task.Run(RunAsync);
    }

    public OnlineProbeSnapshot Snapshot
    {
        get
        {
            lock (sync) return new(phase, !Completion.IsCompleted, stopRequested && !Completion.IsCompleted,
                connection.ConnectionState, progress, result, failure, connection.DiagnosticPath,
                connection.DiagnosticWriteError is not null);
        }
    }

    private async Task RunAsync()
    {
        try
        {
            await connection.Ready.WaitAsync(lifetime.Token).ConfigureAwait(false);
            lock (sync) phase = OnlineProbePhase.Testing;
            var measured = await OnlineTransportProbe.RunCoreAsync(connection, samples, connection.Diagnostics,
                responseTimeout, lifetime.Token, value => { lock (sync) progress = value; }).ConfigureAwait(false);
            lock (sync) { result = measured; phase = OnlineProbePhase.Passed; }
            // Keep servicing outgoing echoes until the user closes OR the peer closes.
            // A local PASS is not confirmation that the other screen already reports PASS.
            await connection.Completion.WaitAsync(lifetime.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            lock (sync)
            {
                if (phase != OnlineProbePhase.Passed)
                {
                    phase = stopRequested ? OnlineProbePhase.Cancelled : OnlineProbePhase.Failed;
                    failure = stopRequested ? OnlineProbeFailure.None : OnlineProbeFailure.Timeout;
                }
            }
        }
        catch (Exception error)
        {
            lock (sync)
            {
                if (phase != OnlineProbePhase.Passed)
                {
                    phase = stopRequested ? OnlineProbePhase.Cancelled : OnlineProbePhase.Failed;
                    failure = stopRequested ? OnlineProbeFailure.None : Classify(connection.Fault ?? error);
                }
            }
        }
        finally
        {
            try { await connection.DisposeAsync().ConfigureAwait(false); }
            catch (Exception)
            {
                // Only a fixed message reaches the report/UI; arbitrary native/HTTP exceptions may contain secrets.
                connection.Diagnostics.Record("probe-cleanup", "Connection cleanup failed.");
            }
            lock (sync) { lifetimeDisposed = true; lifetime.Dispose(); }
        }
    }

    internal static OnlineProbeFailure Classify(Exception error) => error switch
    {
        OnlineRoomRequestException { StatusCode: HttpStatusCode.Unauthorized } => OnlineProbeFailure.AccessKey,
        OnlineRoomRequestException { StatusCode: HttpStatusCode.NotFound } => OnlineProbeFailure.RoomMissing,
        OnlineRoomRequestException { StatusCode: HttpStatusCode.Conflict } => OnlineProbeFailure.RoomMismatch,
        OnlineRoomRequestException { StatusCode: HttpStatusCode.BadRequest } => OnlineProbeFailure.ServerProfile,
        OnlineRoomRequestException { StatusCode: HttpStatusCode.TooManyRequests } => OnlineProbeFailure.RateLimit,
        TimeoutException or OperationCanceledException => OnlineProbeFailure.Timeout,
        InvalidDataException => OnlineProbeFailure.Integrity,
        _ => OnlineProbeFailure.Connection,
    };

    public string GetDiagnosticReport() => connection.Diagnostics.ToJson();
    public void Dispose()
    {
        lock (sync)
        {
            if (stopRequested || lifetimeDisposed) return;
            stopRequested = true; lifetime.Cancel();
        }
        connection.Dispose();
    }
    public async Task StopAsync() { Dispose(); await Completion.ConfigureAwait(false); }
    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}

public sealed class OnlineRoomRequestException(HttpStatusCode statusCode, string message) : IOException(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
