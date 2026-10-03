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
        var pipe = new MemoryPipe();
        using var client = new DiscordIpcClient("123456789012345678", pipe);
        Assert.IsTrue(SpinWait.SpinUntil(() => client.Status == DiscordPresenceStatus.Connected, 3000), "Fake READY not processed.");
        client.SetActivity(new("TEST GAME", "GBC · Paused"));
        Assert.IsTrue(SpinWait.SpinUntil(() => pipe.Written.Any(HasTestActivity), 3000));
        var packet = JObject.Parse(pipe.Written.First(HasTestActivity));
        var activity = (JObject)packet["args"]!["activity"]!;
        Assert.AreEqual("GBC · Paused", (string?)activity["state"]);
        Assert.IsNull(activity["secrets"]); Assert.IsNull(activity["party"]); Assert.IsNull(activity["buttons"]);
        Assert.IsFalse(packet.ToString().Contains("fake-user"));
        int clears = pipe.Written.Count(IsClear);
        client.Dispose();
        Assert.IsTrue(SpinWait.SpinUntil(() => pipe.Written.Count(IsClear) > clears, 3000), "Shutdown must clear activity: " + string.Join("; ", pipe.Written));
    }

    private static bool HasTestActivity(string json) => JObject.Parse(json)["args"]?["activity"] is JObject activity && (string?)activity["details"] == "TEST GAME";
    [TestMethod]
    public void ReconnectionRepublishesLatestActivityAndExplicitClearIsSent()
    {
        var pipe = new MemoryPipe(); using var client = new DiscordIpcClient("123456789012345678", pipe);
        Assert.IsTrue(SpinWait.SpinUntil(() => client.Status == DiscordPresenceStatus.Connected, 3000));
        client.SetActivity(new("TEST GAME", "GB · Playing"));
        Assert.IsTrue(SpinWait.SpinUntil(() => pipe.Written.Any(HasTestActivity), 3000));
        int sent = pipe.Written.Count(HasTestActivity);
        pipe.Close();
        Assert.IsTrue(SpinWait.SpinUntil(() => pipe.Written.Count(HasTestActivity) > sent, 6000), "READY after reconnect must replay current activity.");
        int clears = pipe.Written.Count(IsClear);
        client.SetActivity(null);
        Assert.IsTrue(SpinWait.SpinUntil(() => pipe.Written.Count(IsClear) > clears, 3000));
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

    private sealed class MemoryPipe : INamedPipeClient
    {
        private readonly ConcurrentQueue<PipeFrame> incoming = new();
        public readonly ConcurrentQueue<string> Written = new();
        public ILogger Logger { get; set; } = new NullLogger();
        private int connected;
        public bool IsConnected => Volatile.Read(ref connected) != 0;
        public int ConnectedPipe => IsConnected ? 0 : -1;
        public bool Connect(int pipe) { Volatile.Write(ref connected, 1); return true; }
        public bool ReadFrame(out PipeFrame frame) => incoming.TryDequeue(out frame);
        public bool WriteFrame(PipeFrame frame)
        {
            if (frame.Opcode == Opcode.Handshake)
            {
                incoming.Enqueue(new PipeFrame(Opcode.Frame, new
                {
                    cmd = "DISPATCH", evt = "READY", data = new
                    {
                        v = 1, config = new { cdn_host = "example.invalid", api_endpoint = "//example.invalid", environment = "test" },
                        user = new { id = "123456789012345678", username = "fake-user", discriminator = "0000", avatar = (string?)null }
                    }
                }));
            }
            if (frame.Opcode == Opcode.Frame) Written.Enqueue(frame.Message);
            return true;
        }
        public void Close() => Volatile.Write(ref connected, 0);
        public void Dispose() => Close();
    }
}
