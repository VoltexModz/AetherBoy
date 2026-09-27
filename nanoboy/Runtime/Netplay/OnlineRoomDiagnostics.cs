using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AetherBoy.Runtime.Netplay;

/// <summary>Bounded, local-only connection evidence. Never stores SDP, endpoints or credentials.</summary>
public sealed class OnlineRoomDiagnostics
{
    private const int Capacity = 512;
    private readonly object sync = new();
    private readonly Queue<OnlineRoomDiagnosticEvent> events = new();
    private readonly long started = Stopwatch.GetTimestamp();
    private int discarded;
    public string SessionId { get; } = Guid.NewGuid().ToString("N");

    internal void Record(string kind, string detail)
    {
        lock (sync)
        {
            if (events.Count == Capacity) { events.Dequeue(); discarded++; }
            events.Enqueue(new(DateTimeOffset.UtcNow, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                kind, detail.Length <= 800 ? detail : detail[..800]));
        }
    }

    internal void Native(int level, string message) => Record("native-process",
        "level=" + Math.Clamp(level, 0, 6) + " " + SanitizeNativeMessage(message));

    // Native logs are free-form and process-global. Keep ONLY a diagnostic vocabulary;
    // regex-based credential removal alone cannot guarantee privacy for future native versions.
    // Unknown tokens are removed BEFORE any in-memory event or file is created.
    private static readonly HashSet<string> NativeWords = new((
        "a an the to of for from with without and or in on by is was are not no has have be been " +
        "failed failure error warning success successful succeeded complete completed closed closing open opened " +
        "connecting connected disconnected disconnect connection state changed changing new checking gathering " +
        "started starting finished waiting timeout timed out expired invalid unexpected unavailable unsupported " +
        "refused rejected denied forbidden unauthorized permissions permission credentials authentication authenticated " +
        "allocate allocation allocations createpermission channelbind bind binding refresh request response send sent " +
        "receive received receiving sending retry retrying retries retransmit retransmission exhausted stale nonce realm " +
        "socket sockets udp tcp tls dtls sctp ice stun turn relay relayed host srflx prflx candidate candidates pair " +
        "selected selecting nominated nomination handshake certificate fingerprint verification verify alert fatal " +
        "buffer buffered buffering queue full empty too small large size maximum message packet packets channel " +
        "datachannel transport network connectivity check checks address family mismatch unsupported protocol " +
        "local remote peer client server role controlling controlled negotiation negotiated blocked dropped " +
        "disposed destroyed shutdown cancelled canceled reset aborted reset limit quota capacity reached " +
        "400 401 403 420 437 438 440 441 442 443 486 487 500 508").Split(' '), StringComparer.OrdinalIgnoreCase);

    internal static string SanitizeNativeMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return "[no native details]";
        if (message.Length > 4096) message = message[..4096];
        // Do not partially expose SDP, candidate strings, credentials or certificate material.
        if (Regex.IsMatch(message, @"(?:ice-pwd|ice-ufrag|fingerprint\s*:|a=candidate|candidate:|authorization|password|credential\s*[:=]|-----BEGIN)", RegexOptions.IgnoreCase))
            return "[sensitive native details redacted]";
        var result = new List<string>();
        bool hidden = false;
        foreach (string piece in message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            string word = piece.Trim('.', ',', ':', ';', '!', '?', '[', ']', '(', ')', '"', '\'');
            if (NativeWords.Contains(word)) { result.Add(word); hidden = false; }
            else if (!hidden) { result.Add("[redacted]"); hidden = true; }
        }
        return string.Join(" ", result);
    }

    internal static string CandidateType(string candidate)
    {
        string[] words = candidate.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i + 1 < words.Length; i++)
            if (words[i] == "typ") return words[i + 1] is "host" or "srflx" or "prflx" or "relay" ? words[i + 1] : "unknown";
        return "unknown";
    }

    public OnlineRoomDiagnosticEvent[] Snapshot() { lock (sync) return events.ToArray(); }

    public string ToJson()
    {
        lock (sync)
            return JsonSerializer.Serialize(new
            {
                schema = 1, sessionId = SessionId,
                build = typeof(OnlineRoomDiagnostics).Assembly.GetName().Version?.ToString(),
                platform = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsLinux() ? "linux" : "other",
                architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                nativeLogScope = "process-wide; not attributable to one peer",
                privacy = "No SDP, room codes, endpoints, credentials, ROMs or saves. Unknown native tokens are redacted.",
                discardedEvents = discarded, events = events.ToArray()
            }, new JsonSerializerOptions { WriteIndented = true });
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        // The caller supplies a unique session filename. Write atomically for concurrent readers.
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (var stream = new FileStream(temporary, options))
            using (var writer = new StreamWriter(stream)) writer.Write(ToJson());
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

public sealed record OnlineRoomDiagnosticEvent(DateTimeOffset Utc, double ElapsedMs, string Kind, string Detail);
