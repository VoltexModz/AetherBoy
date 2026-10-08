using System.IO.Compression;
using System.Security.Cryptography;
using GameboyAdvanced.Core;
using GameboyAdvanced.Core.Debug;
using GameboyAdvanced.Core.Rom;

namespace AetherBoy.RuntimeTests;

/// <summary>
/// Optional checks against user-provided card archives. No card payload or
/// commercial firmware is bundled. Passing is not proof of firmware acceptance.
/// </summary>
[TestClass]
public sealed class ProvidedEReaderCardTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("AETHERBOY_DONKEY_KONG_CARD1_ZIP", "A28D6916536B754A67A2DC5C525182CB3FD3577AC2772B4DCA8325B5B36526F3")]
    [DataRow("AETHERBOY_BALLOON_FIGHT_CARD1_ZIP", "6A8AFEAD6927EDD9AB6BB178F770557E157DD252D96BD8AEE644E9ABBE8721CB")]
    public void ProvidedRawStripsDecodeAndSurviveQueuedStateRoundTrip(string variable, string archiveSha256)
    {
        string? path = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(path))
        { Assert.Inconclusive("Set " + variable + " to the user-provided card archive."); return; }
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.IsTrue(file.Length is > 0 and < 65536);
        Assert.AreEqual(archiveSha256, Convert.ToHexString(SHA256.HashData(file))); file.Position = 0;
        using var zip = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: true);
        Assert.AreEqual(2, zip.Entries.Count, "These fixtures contain card 1 only, not the full NES game.");
        var device = new Device([], new GamePak(EReaderTests.Rom()), new TestDebugger(), true);
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in zip.Entries.OrderBy(entry => entry.FullName, StringComparer.Ordinal))
        {
            Assert.IsTrue(entry.FullName.EndsWith(".raw", StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(2912L, entry.Length);
            byte[] card = new byte[2912];
            using (var source = entry.Open()) { source.ReadExactly(card); Assert.AreEqual(-1, source.ReadByte()); }
            byte[] dots = EReaderDotCode.Decode(card);
            Assert.AreEqual(EReaderDotCode.Stride * EReaderDotCode.Height, dots.Length);
            Assert.IsTrue(dots.All(value => value <= 1)); Assert.IsTrue(dots.Any(value => value == 1));
            CollectionAssert.AreEqual(dots, EReaderDotCode.Decode(card));
            string hash = Convert.ToHexString(SHA256.HashData(dots)); Assert.IsTrue(hashes.Add(hash));
            TestContext.WriteLine($"{entry.FullName}: RAW SHA256={Convert.ToHexString(SHA256.HashData(card))}; dots SHA256={hash}");
            device.Gamepak.EReader!.QueueCard(card);
        }
        Assert.AreEqual(2, device.Gamepak.EReader!.QueuedCards);
        byte[] queuedState = device.CaptureState();
        device.Gamepak.EReader.ClearCards(); Assert.AreEqual(0, device.Gamepak.EReader.QueuedCards);
        device.RestoreState(queuedState); Assert.AreEqual(2, device.Gamepak.EReader!.QueuedCards);
        CollectionAssert.AreEqual(queuedState, device.CaptureState());
        // The fixture stays read-only, including when assertions above fail.
        file.Position = 0; Assert.AreEqual(archiveSha256, Convert.ToHexString(SHA256.HashData(file)));
    }
}
