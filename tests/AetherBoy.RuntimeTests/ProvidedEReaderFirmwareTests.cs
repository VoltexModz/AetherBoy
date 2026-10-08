using System.IO.Compression;
using System.Security.Cryptography;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Video;
using nanoboy.Core;

namespace AetherBoy.RuntimeTests;

/// <summary>
/// Opt-in integration checks using the owner's archives; no firmware/card data
/// is bundled. Screenshots were visually checked: named application and 8, then
/// 7 missing dot codes out of 9. This does not assert a complete NES game boots.
/// </summary>
[TestClass]
public sealed class ProvidedEReaderFirmwareTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Timeout(180_000)]
    [DataRow("AETHERBOY_DONKEY_KONG_CARD1_ZIP", "A28D6916536B754A67A2DC5C525182CB3FD3577AC2772B4DCA8325B5B36526F3",
        "5E9F2B92FD545FBD72D5016BE96E3E5802E0DA2CE68F96C264D30A8AD3899B4D", "84C5A6D435EDE0702B5A9B66F4EF8E15D8BBFC202D9078C04863868E101940D9")]
    [DataRow("AETHERBOY_BALLOON_FIGHT_CARD1_ZIP", "6A8AFEAD6927EDD9AB6BB178F770557E157DD252D96BD8AEE644E9ABBE8721CB",
        "2C1E2F2183EF5F360359D8AD5F3FBF6B19F9F05B41AA4004FCAB183EDF1A698B", "D3C846FE60F66AE47C9A94E3CBF9F1792326CF339CDD2C580B11836456469C6E")]
    public void UsaFirmwareAcceptsBothStripsAndLateInsertionWithDeterministicReplay(
        string cardVariable, string archiveHash, string firstImage, string secondImage)
    {
        string romZip = RequireFile("AETHERBOY_EREADER_ROM_ZIP"), cardZip = RequireFile(cardVariable);
        byte[][] romEntries = ReadArchive(romZip, "7D2D5CFEDD269FDE0327F8BCBD3C7F9150BFB54541F440C8715A9BC4BF53F933", 1, 8388608);
        byte[][] cards = ReadArchive(cardZip, archiveHash, 2, 2912);
        Assert.AreEqual("72BF37F887E896ADD1342BF95A7CFE3494A689199F878E5E1AA3072639B1B948", Hash(romEntries[0]));
        string root = Path.Combine(Path.GetTempPath(), "aether-ereader-firmware-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string rom = Path.Combine(root, "reader.gba"); File.WriteAllBytes(rom, romEntries[0]);
            using var machine = new GbaProductionMachine(rom, Path.Combine(root, "reader.sav"), new(0, false, true, true, true, true, 44100));
            void Run(int frames, GameBoyButtons buttons = GameBoyButtons.None)
            {
                machine.SetButtons(buttons);
                for (int i = 0; i < frames; i++) machine.RunFrame();
                machine.SetButtons(GameBoyButtons.None);
            }
            void CheckImage(string expected, string label)
            {
                int[] pixels = new int[VideoGeometry.GameBoyAdvance.PixelCount]; long sequence = -1;
                Assert.IsTrue(machine.TryCopyVideoFrame(pixels, ref sequence));
                byte[] png = NativeScreenshot.Encode(VideoGeometry.GameBoyAdvance, pixels);
                string? output = Environment.GetEnvironmentVariable("AETHERBOY_EREADER_TEST_OUTPUT");
                if (!string.IsNullOrWhiteSpace(output))
                {
                    Directory.CreateDirectory(output);
                    File.WriteAllBytes(Path.Combine(output, cardVariable + "-" + label + ".png"), png);
                }
                Assert.AreEqual(expected, Hash(png), "Firmware screen: " + label);
                TestContext.WriteLine(label + " PNG SHA256=" + Hash(png));
            }
            EReaderSnapshot Snapshot() => machine.CaptureSnapshot(SessionState.Running, false, false, 0, 0).EReader!;

            // Actual firmware, emulated input only; no RAM patches/skip-to-result.
            Run(180); Run(8, GameBoyButtons.A); Run(120); Run(8, GameBoyButtons.A); Run(120);
            machine.QueueEReaderCard(cards[0]); Run(334);
            CheckImage(firstImage, "first-accepted"); Assert.AreEqual(1, Snapshot().CardsStarted);
            byte[] firstAccepted = machine.CaptureState();

            // A second scan is already waiting when the user chooses a file.
            Run(1, GameBoyButtons.A); Run(120);
            Assert.IsFalse(Snapshot().HasCard); Assert.AreEqual(0, Snapshot().QueuedCards);
            machine.QueueEReaderCard(cards[1]); Run(4);
            Assert.AreEqual(2, Snapshot().CardsStarted); Assert.AreEqual(0, Snapshot().QueuedCards);
            byte[] duringScan = machine.CaptureState();
            Run(330); CheckImage(secondImage, "late-second-accepted");
            // Restoring resets the host rewind-capture cadence. Compare repeated
            // restores with the same cadence, not uninterrupted host RunFrame
            // calls (automatic state capture advances to instruction boundaries).
            machine.RestoreState(duringScan); Run(330);
            CheckImage(secondImage, "restored-second-accepted");
            byte[] completed = machine.CaptureState();

            // Repeated restore inside a real scan must preserve IRQ/sensor order,
            // CPU/memory/flash and queue state, not merely the visible image.
            for (int repeat = 0; repeat < 20; repeat++)
            {
                machine.RestoreState(duringScan); Run(330);
                CollectionAssert.AreEqual(completed, machine.CaptureState(), "Mid-scan replay " + repeat);
            }
            CheckImage(secondImage, "replayed-second-accepted");

            // Also retain the normal path: enqueue before starting the scan.
            machine.RestoreState(firstAccepted); machine.QueueEReaderCard(cards[1]);
            Run(1, GameBoyButtons.A); Run(334); CheckImage(secondImage, "queued-second-accepted");
            Assert.AreEqual(2, Snapshot().CardsStarted);
            // Rescanning strip 1 must not falsely complete another part of the game.
            machine.QueueEReaderCard(cards[0]); Run(1, GameBoyButtons.A); Run(334);
            CheckImage(secondImage, "duplicate-does-not-increase-progress");
            Assert.AreEqual(3, Snapshot().CardsStarted);
            TestContext.WriteLine("Two real RAW strips accepted; 20 complete-state mid-scan replays and duplicate scan passed. Seven dot codes still missing; no full-game claim.");
        }
        finally
        {
            // Only the unique directory created above; user archives stay read-only.
            Directory.Delete(root, recursive: true);
            using var source = File.OpenRead(romZip);
            Assert.AreEqual("7D2D5CFEDD269FDE0327F8BCBD3C7F9150BFB54541F440C8715A9BC4BF53F933", Convert.ToHexString(SHA256.HashData(source)));
            using var cardSource = File.OpenRead(cardZip);
            Assert.AreEqual(archiveHash, Convert.ToHexString(SHA256.HashData(cardSource)));
        }
    }

    private static string RequireFile(string variable)
    {
        string? path = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(path)) Assert.Inconclusive("Set " + variable + " to the user-provided archive.");
        return path!;
    }

    private static byte[][] ReadArchive(string path, string expectedHash, int count, int size)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.IsTrue(file.Length > 0 && file.Length <= 16 * 1024 * 1024);
        Assert.AreEqual(expectedHash, Convert.ToHexString(SHA256.HashData(file))); file.Position = 0;
        using var zip = new ZipArchive(file, ZipArchiveMode.Read);
        Assert.AreEqual(count, zip.Entries.Count);
        return zip.Entries.OrderBy(e => e.FullName, StringComparer.Ordinal).Select(e =>
        {
            Assert.AreEqual((long)size, e.Length);
            byte[] bytes = new byte[size]; using var stream = e.Open();
            stream.ReadExactly(bytes); Assert.AreEqual(-1, stream.ReadByte()); return bytes;
        }).ToArray();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
