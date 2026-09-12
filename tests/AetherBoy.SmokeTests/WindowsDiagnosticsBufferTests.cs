using System.IO.Compression;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Diagnostics;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class WindowsDiagnosticsBufferTests
{
    [TestMethod]
    public void BoundedQueueDropsIncomingEventsWithoutBlockingProducerOrLosingExportCheckpoint()
    {
        WithDirectory(root =>
        {
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            int first = 1;
            using var session = new WindowsTesterSession(root, queueCapacity: 3, beforeWrite: () =>
            {
                if (Interlocked.Exchange(ref first, 0) != 1) return;
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new IOException("Test worker was not released.");
            });
            try
            {
                Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(3)));
                Task producer = Task.Run(() =>
                {
                    for (int index = 0; index < 20; index++) session.RecordOperation("ordered", index, true);
                });
                Assert.IsTrue(producer.Wait(TimeSpan.FromSeconds(1)), "Blocked disk writes must not block emulation events.");
                Assert.AreEqual(3, session.PendingEvents);
                Assert.AreEqual(17L, session.DroppedEvents);
                Task<string> export = Task.Run(() => session.CreateBundle(Path.Combine(root, "report.zip")));
                Assert.IsTrue(SpinWait.SpinUntil(() => session.DroppedEvents == 18, TimeSpan.FromSeconds(3)),
                    "The export event must encounter the full queue; its separate checkpoint must survive.");
                release.Set();
                Assert.IsTrue(export.Wait(TimeSpan.FromSeconds(5)));
                string text = ReadEntry(export.Result, "session.jsonl");
                int[] slots = ParseLines(text).Where(item => item.Event == "operation.completed")
                    .Select(item => item.Slot!.Value).ToArray();
                CollectionAssert.AreEqual(new[] { 0, 1, 2 }, slots);
                StringAssert.Contains(ReadEntry(export.Result, "README.txt"), "dropped events: 18");
                Assert.IsTrue(session.IsRecording);
                Assert.IsNull(session.ErrorCode);
            }
            finally { release.Set(); }
        });
    }

    [TestMethod]
    public void SizeLimitKeepsWholeJsonLinesAndStillExportsPriorEvidence()
    {
        WithDirectory(root =>
        {
            const long maximum = 2048;
            using var session = new WindowsTesterSession(root, maximumLogBytes: maximum);
            for (int index = 0; index < 100; index++) session.RecordOperation("limit_probe", index, true);
            string report = session.CreateBundle(Path.Combine(root, "report.zip"));
            Assert.IsTrue(session.SizeLimitReached);
            Assert.IsFalse(session.IsRecording);
            Assert.IsNull(session.ErrorCode, "A storage budget is not an I/O failure.");
            Assert.IsTrue(session.DroppedEvents > 0);
            Assert.IsTrue(new FileInfo(session.LogFilePath).Length <= maximum);
            string log = ReadEntry(report, "session.jsonl");
            Assert.IsTrue(log.EndsWith('\n'));
            Assert.IsTrue(ParseLines(log).Length > 0, "Every saved line must be complete JSON.");
            StringAssert.Contains(ReadEntry(report, "README.txt"), "size limit reached: True");
        });
    }

    [TestMethod]
    public void WriteFailureStopsRecordingAndReleasesExporterWithoutLeakingExceptionMessage()
    {
        WithDirectory(root =>
        {
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            using var session = new WindowsTesterSession(root, beforeWrite: () =>
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new IOException("Test worker was not released.");
                throw new IOException(@"PRIVATE C:\Users\Secret\my-rom.gba");
            });
            try
            {
                Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(3)));
                Task<Exception?> export = Task.Run(() =>
                {
                    try { session.CreateBundle(Path.Combine(root, "report.zip")); return null; }
                    catch (Exception exception) { return exception; }
                });
                release.Set();
                Assert.IsTrue(export.Wait(TimeSpan.FromSeconds(3)));
                Assert.IsInstanceOfType<IOException>(export.Result);
                Assert.IsFalse(session.IsRecording);
                StringAssert.Contains(session.ErrorCode!, "IOException:0x");
                Assert.IsFalse(export.Result!.ToString().Contains("PRIVATE", StringComparison.Ordinal));
                Assert.IsFalse(session.ErrorCode!.Contains("Secret", StringComparison.Ordinal));
                Assert.IsFalse(File.Exists(Path.Combine(root, "report.zip")));
                Assert.IsTrue(session.WriterCompletion.Wait(TimeSpan.FromSeconds(3)));
            }
            finally { release.Set(); }
        });
    }

    [TestMethod]
    public void DisposeDrainsAcceptedEventsAndClosesExactlyOnce()
    {
        WithDirectory(root =>
        {
            var session = new WindowsTesterSession(root);
            for (int index = 0; index < 20; index++) session.RecordOperation("before_close", index, true);
            session.Dispose();
            session.Dispose();
            Assert.IsFalse(session.IsRecording);
            Assert.IsNull(session.ErrorCode);
            Assert.IsTrue(session.WriterCompletion.IsCompletedSuccessfully);
            session.RecordOperation("after_close", null, true);
            string log = File.ReadAllText(session.LogFilePath);
            var entries = ParseLines(log);
            Assert.AreEqual("application.closed", entries[^1].Event);
            Assert.AreEqual(1, entries.Count(item => item.Event == "application.closed"));
            Assert.AreEqual(20, entries.Count(item => item.Event == "operation.completed"));
            Assert.IsFalse(log.Contains("after_close", StringComparison.Ordinal));
            Assert.ThrowsExactly<ObjectDisposedException>(() => session.CreateBundle(Path.Combine(root, "closed.zip")));
        });
    }

    [TestMethod]
    public void ExportTimeoutCreatesNoPartialArchiveAndWorkerCanFinishAfterwards()
    {
        WithDirectory(root =>
        {
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            int first = 1;
            var session = new WindowsTesterSession(root, flushTimeout: TimeSpan.FromMilliseconds(50), beforeWrite: () =>
            {
                if (Interlocked.Exchange(ref first, 0) != 1) return;
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new IOException("Test worker was not released.");
            });
            try
            {
                Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(3)));
                string destination = Path.Combine(root, "timeout.zip");
                Assert.ThrowsExactly<IOException>(() => session.CreateBundle(destination));
                Assert.IsFalse(File.Exists(destination));
                Assert.IsTrue(session.IsRecording, "An export timeout is not yet a confirmed write failure.");
                Assert.IsNull(session.ErrorCode);
            }
            finally { release.Set(); session.Dispose(); }
            Assert.IsTrue(session.WriterCompletion.IsCompletedSuccessfully);
            StringAssert.Contains(File.ReadAllText(session.LogFilePath), "application.closed");
        });
    }

    [TestMethod]
    public void ShutdownTimeoutDoesNotDisposeStreamWhileWorkerIsStillWriting()
    {
        WithDirectory(root =>
        {
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            int first = 1;
            var session = new WindowsTesterSession(root, shutdownTimeout: TimeSpan.FromMilliseconds(50), beforeWrite: () =>
            {
                if (Interlocked.Exchange(ref first, 0) != 1) return;
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new IOException("Test worker was not released.");
            });
            try
            {
                Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(3)));
                session.Dispose();
                Assert.AreEqual("shutdown_timeout", session.ErrorCode);
                Assert.IsFalse(session.WriterCompletion.IsCompleted);
            }
            finally { release.Set(); Assert.IsTrue(session.WriterCompletion.Wait(TimeSpan.FromSeconds(3))); }
            Assert.IsTrue(ParseLines(File.ReadAllText(session.LogFilePath)).Length >= 1);
        });
    }

    private static (string Event, int? Slot)[] ParseLines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line =>
        {
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement entry = document.RootElement;
            int? slot = entry.GetProperty("details").TryGetProperty("slot", out JsonElement value) && value.ValueKind == JsonValueKind.Number
                ? value.GetInt32() : null;
            return (entry.GetProperty("event").GetString()!, slot);
        }).ToArray();

    private static string ReadEntry(string archivePath, string name)
    {
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        using var input = new StreamReader(archive.GetEntry(name)!.Open());
        return input.ReadToEnd();
    }

    private static void WithDirectory(Action<string> test)
    {
        string directory = Path.Combine(Path.GetTempPath(), "aetherboy-diag-buffer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { test(directory); }
        finally { Directory.Delete(directory, true); }
    }
}
