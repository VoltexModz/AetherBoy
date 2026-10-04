using System.Collections.Concurrent;
using AetherBoy.Runtime;
using DiscordRPC.IO;
using DiscordRPC.Logging;
using Newtonsoft.Json.Linq;

namespace AetherBoy.RuntimeTests;

// Exercise the real library and adapter with an in-memory pipe. No installed Discord is contacted.
[TestClass]
public sealed class DiscordIpcTests
{
    [TestMethod]
    public void LibraryHandshakePublishesOnlyAllowedFieldsAndClearsOnShutdown()
    {
        var pipe = new MemoryPipe(automaticReady: false);
        using var client = new DiscordIpcClient("123456789012345678", pipe);
        pipe.ReplyReadyAfterHandshake(1);
        Assert.IsTrue(SpinWait.SpinUntil(() => client.Status == DiscordPresenceStatus.Connected, 3000), "Fake READY not processed.");
        client.SetActivity(new("TEST GAME", "GBC · Paused"));
        pipe.WaitForWritten(frames => frames.Any(HasTestActivity), "Activity not published.");
        var packet = JObject.Parse(pipe.Written.First(HasTestActivity));
        var activity = (JObject)packet["args"]!["activity"]!;
        Assert.AreEqual("GBC · Paused", (string?)activity["state"]);
        Assert.IsNull(activity["secrets"]); Assert.IsNull(activity["party"]); Assert.IsNull(activity["buttons"]);
        Assert.IsFalse(packet.ToString().Contains("fake-user"));
        int clears = pipe.Written.Count(IsClear);
        client.Dispose();
        pipe.WaitForWritten(frames => frames.Count(IsClear) > clears, "Shutdown must clear activity.");
    }

    private static bool HasTestActivity(string json) => JObject.Parse(json)["args"]?["activity"] is JObject activity && (string?)activity["details"] == "TEST GAME";
    [TestMethod]
    public void ReconnectionRepublishesLatestActivityAndExplicitClearIsSent()
    {
        var pipe = new MemoryPipe(automaticReady: false); using var client = new DiscordIpcClient("123456789012345678", pipe);
        pipe.ReplyReadyAfterHandshake(1);
        Assert.IsTrue(SpinWait.SpinUntil(() => client.Status == DiscordPresenceStatus.Connected, 3000), "First READY not processed.");
        client.SetActivity(new("TEST GAME", "GB · Playing"));
        pipe.WaitForWritten(frames => frames.Any(HasTestActivity), "Initial activity not published.");
        pipe.Close();
        pipe.ReplyReadyAfterHandshake(2);
        pipe.WaitForWritten(frames => frames.Any(HasTestActivity), "READY after reconnect must replay current activity.", handshake: 2);
        int clears = pipe.Written.Count(IsClear);
        client.SetActivity(null);
        pipe.WaitForWritten(frames => frames.Count(IsClear) > clears, "Explicit clear not sent.");
    }

    [TestMethod]
    public void ImmediateReadyIsProcessedWhenClientsStartTogether()
    {
        // Keep the fastest possible server response covered as well as the explicitly stepped
        // handshakes above. Starting workers together exercises Initialize's IPC-thread race.
        Parallel.For(0, 32, new ParallelOptions { MaxDegreeOfParallelism = 16 }, _ =>
        {
            var pipe = new MemoryPipe();
            using var client = new DiscordIpcClient("123456789012345678", pipe);
            Assert.IsTrue(SpinWait.SpinUntil(() => client.Status == DiscordPresenceStatus.Connected, 3000), "Immediate READY not processed.");
        });
    }

    private static bool IsClear(string json)
    {
        var packet = JObject.Parse(json);
        return (string?)packet["cmd"] == "SET_ACTIVITY" && packet["args"]?["activity"]?.Type == JTokenType.Null;
    }

    [TestMethod]
    public void QueuedOrReplayedOldTitleCannotOverrideLatestPrivacyChoice()
    {
        var memory = new MemoryPipe(); using var pipe = new ClosingDiscordPipe(memory);
        pipe.Connect(0);
        var stale = new PipeFrame(Opcode.Frame, new JObject
        {
            ["cmd"] = "SET_ACTIVITY", ["args"] = new JObject
            { ["pid"] = Environment.ProcessId, ["activity"] = new JObject { ["details"] = "OLD PRIVATE TITLE" } }
        });
        pipe.SetDesiredActivity(new("Playing a Game Boy game", "GB · Playing"));
        Assert.IsTrue(pipe.WriteFrame(stale)); Assert.IsFalse(memory.Written.Last().Contains("PRIVATE"));
        pipe.SetDesiredActivity(null); Assert.IsTrue(pipe.WriteFrame(stale)); Assert.IsTrue(IsClear(memory.Written.Last()));
        pipe.RequestStop(); Assert.IsFalse(pipe.WriteFrame(stale));
    }

    private sealed class MemoryPipe(bool automaticReady = true) : INamedPipeClient
    {
        private readonly object framesChanged = new();
        private readonly ConcurrentQueue<PipeFrame> incoming = new();
        private readonly ConcurrentQueue<(int Handshake, string Message)> writtenByHandshake = new();
        private int handshakes, readyReplies;
        public readonly ConcurrentQueue<string> Written = new();
        public ILogger Logger { get; set; } = new NullLogger();
        private int connected;
        public bool IsConnected => Volatile.Read(ref connected) != 0;
        public int ConnectedPipe => IsConnected ? 0 : -1;
        public bool Connect(int pipe) { Volatile.Write(ref connected, 1); return true; }
        public bool ReadFrame(out PipeFrame frame) => incoming.TryDequeue(out frame);
        public bool WriteFrame(PipeFrame frame)
        {
            lock (framesChanged)
            {
                if (frame.Opcode == Opcode.Handshake)
                {
                    handshakes++;
                    if (automaticReady) EnqueueReady();
                    else
                    {
                        // The test acts as the server. Do not let the worker poll an empty pipe
                        // between sending the handshake and the test supplying its READY reply.
                        Monitor.PulseAll(framesChanged);
                        WaitFor(() => readyReplies >= handshakes, "Server did not reply to handshake.");
                    }
                }
                if (frame.Opcode == Opcode.Frame)
                {
                    Written.Enqueue(frame.Message);
                    writtenByHandshake.Enqueue((handshakes, frame.Message));
                }
                Monitor.PulseAll(framesChanged);
            }
            return true;
        }
        public void ReplyReadyAfterHandshake(int number)
        {
            lock (framesChanged)
            {
                WaitFor(() => handshakes >= number, $"Handshake {number} not sent.");
                Assert.AreEqual(number, handshakes, "Unexpected extra handshake.");
                EnqueueReady();
                readyReplies = number;
                Monitor.PulseAll(framesChanged);
            }
        }
        public void WaitForWritten(Func<IEnumerable<string>, bool> predicate, string failure, int handshake = 0)
        {
            lock (framesChanged) WaitFor(() => predicate(handshake == 0 ? Written
                : writtenByHandshake.Where(frame => frame.Handshake == handshake).Select(frame => frame.Message)), failure);
        }
        private void WaitFor(Func<bool> predicate, string failure)
        {
            long deadline = Environment.TickCount64 + 3000;
            while (!predicate())
            {
                int remaining = (int)Math.Max(0, deadline - Environment.TickCount64);
                Assert.IsTrue(remaining > 0, failure + " Frames: " + string.Join("; ", Written));
                Monitor.Wait(framesChanged, remaining);
            }
        }
        private void EnqueueReady() => incoming.Enqueue(new PipeFrame(Opcode.Frame, new
        {
            cmd = "DISPATCH", evt = "READY", data = new
            {
                v = 1, config = new { cdn_host = "example.invalid", api_endpoint = "//example.invalid", environment = "test" },
                user = new { id = "123456789012345678", username = "fake-user", discriminator = "0000", avatar = (string?)null }
            }
        }));
        public void Close() => Volatile.Write(ref connected, 0);
        public void Dispose() => Close();
    }
}
