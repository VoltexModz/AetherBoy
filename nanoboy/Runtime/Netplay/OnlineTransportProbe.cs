using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace AetherBoy.Runtime.Netplay;

/// <summary>ROM-free, bidirectional transport measurement. Never run on a game session/channel.</summary>
public static class OnlineTransportProbe
{
    public const string Profile = "transport-probe-v1";
    private const uint Magic = 0x50544241; // ABTP, little endian
    private static readonly int[] Sizes = { 32, 256, 1024, 4096 };

    public static Task<OnlineTransportProbeResult> RunAsync(OnlineRoomTransport transport, int samplesPerSize = 8,
        CancellationToken cancellation = default)
    {
        if (transport.WireProfile != Profile) throw new ArgumentException("Use a dedicated transport-probe-v1 room, never a game room.");
        return RunCoreAsync(transport, samplesPerSize, transport.Diagnostics, TimeSpan.FromSeconds(20), cancellation);
    }

    internal static async Task<OnlineTransportProbeResult> RunCoreAsync(IOnlineLinkTransport transport, int samplesPerSize,
        OnlineRoomDiagnostics diagnostics, TimeSpan responseTimeout, CancellationToken cancellation)
    {
        if (samplesPerSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(samplesPerSize));
        if (responseTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(responseTimeout));
        await transport.Ready.WaitAsync(cancellation).ConfigureAwait(false);
        diagnostics.Record("probe", "started sizes=32,256,1024,4096 samples-per-size=" + samplesPerSize);
        var samples = new List<OnlineTransportProbeSample>();
        int total = Sizes.Length * samplesPerSize, localSequence = 0, remoteSequence = 0;
        byte[]? expected = null;
        long sentAt = 0, lastProgress = Stopwatch.GetTimestamp();
        try
        {
            while (samples.Count != total || remoteSequence != total)
            {
                cancellation.ThrowIfCancellationRequested();
                if (!transport.Connected) throw new IOException("The transport disconnected before both directions completed.");
                if (expected is null && localSequence < total)
                {
                    int length = Sizes[localSequence / samplesPerSize];
                    byte[] request = CreatePacket(localSequence, samplesPerSize, length);
                    sentAt = Stopwatch.GetTimestamp();
                    transport.Send(request);
                    expected = (byte[])request.Clone(); expected[5] = 2;
                    localSequence++;
                }
                // Bounded drain: a malicious peer must not starve timeout/cancellation processing.
                int processed = 0;
                while (processed++ < 64 && transport.TryReceive(out byte[] packet))
                {
                    ValidateHeader(packet, samplesPerSize);
                    int sequence = BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(8));
                    if (packet[5] == 1)
                    {
                        if (sequence != remoteSequence || remoteSequence >= total)
                            throw new InvalidDataException("Probe request sequence is duplicated or out of order.");
                        remoteSequence++;
                        byte[] reply = (byte[])packet.Clone(); reply[5] = 2;
                        transport.Send(reply);
                    }
                    else
                    {
                        if (expected is null || !packet.AsSpan().SequenceEqual(expected))
                            throw new InvalidDataException("Probe echo is corrupted, duplicated or out of order.");
                        double rtt = Stopwatch.GetElapsedTime(sentAt).TotalMilliseconds;
                        samples.Add(new(packet.Length, rtt)); expected = null;
                        diagnostics.Record("probe-sample", FormattableString.Invariant($"bytes={packet.Length} rtt-ms={rtt:F3}"));
                    }
                    lastProgress = Stopwatch.GetTimestamp();
                }
                if ((expected is not null && Stopwatch.GetElapsedTime(sentAt) > responseTimeout) ||
                    Stopwatch.GetElapsedTime(lastProgress) > responseTimeout)
                    throw new TimeoutException("Probe response deadline exceeded; delivery was not verified. This is not proof of UDP packet loss.");
                await Task.Delay(2, cancellation).ConfigureAwait(false);
            }
            var groups = samples.GroupBy(s => s.Bytes).Select(group =>
            {
                double[] sorted = group.Select(s => s.RoundTripMs).Order().ToArray();
                double median = (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2;
                return new OnlineTransportProbeSize(group.Key, sorted.Length, sorted[0], median,
                    sorted[(int)Math.Ceiling(sorted.Length * .95) - 1], sorted[^1]);
            }).ToArray();
            diagnostics.Record("probe", "passed own-echoes=" + samples.Count + " peer-requests-echoed=" + remoteSequence);
            return new(samples.Count, remoteSequence, groups);
        }
        catch (Exception e)
        {
            diagnostics.Record("probe-failed", "type=" + e.GetType().Name + " verified=" + samples.Count + " expected=" + total);
            throw;
        }
    }

    internal static byte[] CreatePacket(int sequence, int samplesPerSize, int size)
    {
        byte[] packet = RandomNumberGenerator.GetBytes(size);
        BinaryPrimitives.WriteUInt32LittleEndian(packet, Magic);
        packet[4] = 1; packet[5] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(6), (ushort)size);
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(8), sequence);
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(12), samplesPerSize);
        return packet;
    }
    private static void ValidateHeader(byte[] packet, int samplesPerSize)
    {
        if (packet.Length < 32 || packet.Length > 4096 || BinaryPrimitives.ReadUInt32LittleEndian(packet) != Magic ||
            packet[4] != 1 || packet[5] is not (1 or 2) || BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(6)) != packet.Length ||
            BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(12)) != samplesPerSize)
            throw new InvalidDataException("Incompatible probe packet or sample count. Both peers need the same test settings.");
        int sequence = BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(8));
        if (sequence < 0 || sequence >= Sizes.Length * samplesPerSize || packet.Length != Sizes[sequence / samplesPerSize])
            throw new InvalidDataException("Invalid probe sequence or payload size.");
    }
}

public sealed record OnlineTransportProbeSample(int Bytes, double RoundTripMs);
public sealed record OnlineTransportProbeSize(int Bytes, int Verified, double MinMs, double MedianMs, double P95Ms, double MaxMs);
public sealed record OnlineTransportProbeResult(int VerifiedEchoes, int PeerRequestsEchoed, OnlineTransportProbeSize[] Sizes);
