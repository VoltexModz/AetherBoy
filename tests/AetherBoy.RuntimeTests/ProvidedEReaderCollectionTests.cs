using System.IO.Compression;
using System.Security.Cryptography;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Video;
using nanoboy.Core;

namespace AetherBoy.RuntimeTests;

/// <summary>Opt-in real firmware checks. No Nintendo firmware or card bytes are bundled.</summary>
[TestClass]
public sealed class ProvidedEReaderCollectionTests
{
    private const string CollectionHash = "A70DD576BFAF96BFC0FF52E0C23E5231EA231D041F6F787DA9EDAB3704E985DD";
    private const string RomZipHash = "7D2D5CFEDD269FDE0327F8BCBD3C7F9150BFB54541F440C8715A9BC4BF53F933";
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Timeout(240_000)]
    [DataRow("Donkey Kong", "93C6C5B782A305B3D98AAA707130439ACC600D6EAAA8E229FC384A8CBFB93BA3",
        "5DB0436BF9D19E243ED6D5F80AA227DCC9982BDD6E04B74068751C0D9D41501C",
        "B32520C0DB655B1C3FAB40C8DD1A3AEB91778D58D47CBA86871ED4F92627EA94")]
    [DataRow("Balloon Fight", "68BC966489213439BAA32E9972B3D98A5BDAB7A372639E4A1CCDD3D2961EB148",
        "BB569E5AFB37D5D349D669BF9F61160BEA0D61A8102A6EE6F17C2821F44F5360",
        "3EEA1AAD7663574A933DF51641BF5ECC9170A849F685D19DB02C2B90066C35A4")]
    public void CompleteNesSetBootsPlaysProducesAudioAndReplays(string title, string titleImage, string playImage, string reopenImage)
        => AuditNesSet(title, titleImage, playImage, reopenImage, fromLibrary: false);

    [TestMethod, Timeout(240_000)]
    // Unlike the manual-scanner baseline, the library supplies cards before the
    // scan screen opens. Balloon Fight's timing-sensitive play scene therefore
    // has its own visually inspected reference; do not change the manual one.
    [DataRow("Donkey Kong", "93C6C5B782A305B3D98AAA707130439ACC600D6EAAA8E229FC384A8CBFB93BA3",
        "5DB0436BF9D19E243ED6D5F80AA227DCC9982BDD6E04B74068751C0D9D41501C",
        "B32520C0DB655B1C3FAB40C8DD1A3AEB91778D58D47CBA86871ED4F92627EA94")]
    [DataRow("Balloon Fight", "68BC966489213439BAA32E9972B3D98A5BDAB7A372639E4A1CCDD3D2961EB148",
        "2D40ACA28B35F0B72688380CD4537EF1741DB141A6F606A4CB012813D4FF2DF4",
        "3EEA1AAD7663574A933DF51641BF5ECC9170A849F685D19DB02C2B90066C35A4")]
    public void LibrarySetQueuesBeforeBootPlaysAndPreservesSeparateFlash(string title, string titleImage, string playImage, string reopenImage)
        => AuditNesSet(title, titleImage, playImage, reopenImage, fromLibrary: true);

    private void AuditNesSet(string title, string titleImage, string playImage, string reopenImage, bool fromLibrary)
    {
        string romZip = RequireFile("AETHERBOY_EREADER_ROM_ZIP");
        string collection = RequireFile("AETHERBOY_EREADER_COLLECTION_ZIP");
        Assert.AreEqual(RomZipHash, HashFile(romZip));
        Assert.AreEqual(CollectionHash, HashFile(collection));
        byte[] rom;
        using (var zip = ZipFile.OpenRead(romZip)) rom = Read(zip.Entries.Single(), 8388608);
        Assert.AreEqual("72BF37F887E896ADD1342BF95A7CFE3494A689199F878E5E1AA3072639B1B948", Hash(rom));
        var cards = new List<byte[]>();
        using (var zip = ZipFile.OpenRead(collection))
        {
            var entries = zip.Entries.Where(e => Path.GetFileName(e.FullName).StartsWith($"NES-e - {title}-e (USA) (Card ", StringComparison.Ordinal))
                .OrderBy(e => e.FullName, StringComparer.Ordinal).ToArray();
            Assert.HasCount(5, entries);
            foreach (var entry in entries)
            {
                using var buffer = new MemoryStream(Read(entry, 64 * 1024), false);
                using var nested = new ZipArchive(buffer, ZipArchiveMode.Read);
                Assert.IsTrue(nested.Entries.Count is 1 or 2);
                foreach (var strip in nested.Entries.OrderBy(e => e.FullName, StringComparer.Ordinal))
                {
                    Assert.AreEqual(2912L, strip.Length);
                    cards.Add(Read(strip, 2912));
                }
            }
        }
        Assert.HasCount(9, cards);
        string root = Path.Combine(Path.GetTempPath(), "aether-ereader-set-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string romPath = Path.Combine(root, "reader.gba"), savePath = Path.Combine(root, "reader.sav");
            File.WriteAllBytes(romPath, rom);
            if (fromLibrary)
            {
                var library = new EReaderLibrary(Path.Combine(root, "library")); library.SetFirmware(romPath);
                string? setId = null;
                for (int index = 0; index < cards.Count; index++)
                {
                    string strip = Path.Combine(root, $"strip-{index:D2}.raw"); File.WriteAllBytes(strip, cards[index]);
                    setId = library.Import(strip, setId).Id;
                }
                romPath = library.PrepareLaunch(setId!);
                cards = library.ReadLaunchCards(romPath).ToList();
                savePath = Path.Combine(root, library.IdentifyLaunch(romPath)!, "game.sav");
                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
            }
            using (var machine = new GbaProductionMachine(romPath, savePath, new(0, true, true, true, true, true, 44100)))
            {
                if (fromLibrary) foreach (byte[] card in cards) machine.QueueEReaderCard(card);
                float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
                long audioSamples = 0;
                machine.AudioSamplesAvailable += (_, audio) =>
                {
                    foreach (float sample in audio.GetInterleavedSamplesCopy())
                    {
                        Assert.IsTrue(float.IsFinite(sample));
                        minimum = Math.Min(minimum, sample); maximum = Math.Max(maximum, sample); audioSamples++;
                    }
                };
                void Run(int frames, GameBoyButtons buttons = GameBoyButtons.None)
                {
                    machine.SetButtons(buttons);
                    for (int i = 0; i < frames; i++) machine.RunFrame();
                    machine.SetButtons(GameBoyButtons.None);
                }
                // Match the inspected probe's instruction-boundary capture cadence.
                void Step(int frames, GameBoyButtons buttons = GameBoyButtons.None)
                {
                    Run(frames, buttons); _ = machine.CaptureState();
                }
                string Picture(string label)
                {
                    int[] pixels = new int[VideoGeometry.GameBoyAdvance.PixelCount]; long sequence = -1;
                    Assert.IsTrue(machine.TryCopyVideoFrame(pixels, ref sequence));
                    byte[] png = NativeScreenshot.Encode(VideoGeometry.GameBoyAdvance, pixels);
                    string? output = Environment.GetEnvironmentVariable("AETHERBOY_EREADER_TEST_OUTPUT");
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        Directory.CreateDirectory(output);
                        File.WriteAllBytes(Path.Combine(output, title.Replace(' ', '-') + "-" + label + ".png"), png);
                    }
                    return Hash(png);
                }
                Step(180); Step(8, GameBoyButtons.A); Step(120); Step(8, GameBoyButtons.A); Step(120);
                if (!fromLibrary) foreach (byte[] card in cards) machine.QueueEReaderCard(card);
                _ = machine.CaptureState();
                for (int i = 0; i < cards.Count; i++)
                {
                    if (i > 0) Step(1, GameBoyButtons.A);
                    Step(334);
                }
                var reader = machine.CaptureSnapshot(SessionState.Running, false, false, 0, 0).EReader!;
                Assert.AreEqual(9, reader.CardsStarted); Assert.AreEqual(0, reader.QueuedCards);
                Step(600);
                byte[] savePrompt = machine.CaptureState();
                Step(1, GameBoyButtons.A); Step(600);
                Assert.AreEqual(titleImage, Picture("title"));
                minimum = float.PositiveInfinity; maximum = float.NegativeInfinity; audioSamples = 0;
                Step(1, GameBoyButtons.Start); Step(600);
                Assert.AreEqual(playImage, Picture("play"));
                Step(60, GameBoyButtons.Right); Step(15, GameBoyButtons.A); Step(3600);
                TestContext.WriteLine("After 3600 additional frames: " + Picture("soak"));
                Assert.IsGreaterThan(0L, audioSamples);
                Assert.IsTrue(maximum - minimum > 0.001f, "Gameplay audio must vary, not remain a constant sample.");

                byte[] checkpoint = machine.CaptureState();
                machine.RestoreState(checkpoint); Run(120, GameBoyButtons.Left);
                byte[] replay = machine.CaptureState();
                for (int repeat = 0; repeat < 5; repeat++)
                {
                    machine.RestoreState(checkpoint); Run(120, GameBoyButtons.Left);
                    CollectionAssert.AreEqual(replay, machine.CaptureState(), "Replay " + repeat);
                }
                Assert.IsFalse(machine.CaptureSnapshot(SessionState.Running, false, false, 0, 0).DiagnosticEvents
                    .Any(e => e.Category == "UnknownOpcode" || e.Message == "HLE BIOS service 0x7F"));

                // The firmware defaults to NO (its blinking text may be invisible
                // in a screenshot). Explicitly select YES before testing flash.
                machine.RestoreState(savePrompt);
                Step(8, GameBoyButtons.Left); Step(20); Step(8, GameBoyButtons.A); Step(660);
                TestContext.WriteLine("Saved application confirmation: " + Picture("saved"));
            }
            Assert.AreEqual(131072L, new FileInfo(savePath).Length);
            byte[] persisted = File.ReadAllBytes(savePath);
            Assert.IsTrue(persisted.AsSpan(0x10000).ContainsAnyExcept((byte)0xFF),
                "The application bank must contain data, not just factory calibration.");
            using (var reopened = new GbaProductionMachine(romPath, savePath, new(0, true, true, true, true, true, 44100)))
            {
                void Step(int frames, GameBoyButtons buttons = GameBoyButtons.None)
                {
                    reopened.SetButtons(buttons);
                    for (int i = 0; i < frames; i++) reopened.RunFrame();
                    reopened.SetButtons(GameBoyButtons.None); _ = reopened.CaptureState();
                }
                string Picture(string label)
                {
                    int[] pixels = new int[VideoGeometry.GameBoyAdvance.PixelCount]; long sequence = -1;
                    Assert.IsTrue(reopened.TryCopyVideoFrame(pixels, ref sequence));
                    byte[] png = NativeScreenshot.Encode(VideoGeometry.GameBoyAdvance, pixels);
                    string? output = Environment.GetEnvironmentVariable("AETHERBOY_EREADER_TEST_OUTPUT");
                    if (!string.IsNullOrWhiteSpace(output))
                        File.WriteAllBytes(Path.Combine(output, title.Replace(' ', '-') + "-" + label + ".png"), png);
                    return Hash(png);
                }
                Step(180); Step(8, GameBoyButtons.A); Step(120);
                string menu = Picture("reopen-menu");
                // Access saved data is the third menu entry; no cards are queued.
                Step(8, GameBoyButtons.Down); Step(20); Step(8, GameBoyButtons.Down); Step(20);
                Step(8, GameBoyButtons.A); Step(600);
                string loaded = Picture("reopen-play");
                Assert.AreNotEqual(menu, loaded);
                Assert.AreEqual(reopenImage, loaded, "Visually inspected game loaded from flash, without rescanning.");
                Step(1, GameBoyButtons.Start); Step(600);
                Assert.AreNotEqual(loaded, Picture("reopen-later"));
                Assert.AreEqual(0, reopened.CaptureSnapshot(SessionState.Running, false, false, 0, 0).EReader!.CardsStarted);
                Assert.IsFalse(reopened.CaptureSnapshot(SessionState.Running, false, false, 0, 0).DiagnosticEvents
                    .Any(e => e.Category == "UnknownOpcode" || e.Message == "HLE BIOS service 0x7F"));
            }
            CollectionAssert.AreEqual(persisted, File.ReadAllBytes(savePath), "Starting the stored program must preserve its flash data.");
            TestContext.WriteLine("Complete nine-strip scan, inspected title/play frames, 60-second emulated soak, variable audio, five full-state replays, application flash save and new-machine reopen passed.");
        }
        finally
        {
            Directory.Delete(root, recursive: true); // only this test's unique directory
            Assert.AreEqual(RomZipHash, HashFile(romZip));
            Assert.AreEqual(CollectionHash, HashFile(collection));
        }
    }

    [TestMethod]
    [Timeout(120_000)]
    [DataRow("Promotional - Kirby Slide Puzzle (USA)", "A7303EDE9C4E3A9766D8DE915F29EF53C3C340123A82A10A88D690217166DCCB")]
    [DataRow("Promotional - Manhole Old-e (USA) (E3)", "7BD06768FDFD7D68AC19020FBE24BB8B6CFE2246F47C377B6A2A1572D90A721B")]
    [DataRow("Promotional - Air Hockey-e (USA)", "9304A52751467B3CA1D11B1129359EBDBF5935B4FACB214F51F6B24511D55B1D")]
    public void StandalonePromotionalGameRunsWithoutAnotherCartridge(string title, string gameImage)
    {
        string romZip = RequireFile("AETHERBOY_EREADER_ROM_ZIP");
        string collection = RequireFile("AETHERBOY_EREADER_COLLECTION_ZIP");
        Assert.AreEqual(RomZipHash, HashFile(romZip));
        Assert.AreEqual(CollectionHash, HashFile(collection));
        byte[] rom;
        using (var zip = ZipFile.OpenRead(romZip)) rom = Read(zip.Entries.Single(), 8388608);
        byte[][] cards;
        using (var zip = ZipFile.OpenRead(collection))
        {
            var entry = zip.Entries.Single(e => Path.GetFileName(e.FullName) == title + ".zip");
            using var buffer = new MemoryStream(Read(entry, 64 * 1024), false);
            using var nested = new ZipArchive(buffer, ZipArchiveMode.Read);
            Assert.HasCount(2, nested.Entries);
            cards = nested.Entries.OrderBy(e => e.FullName, StringComparer.Ordinal).Select(e => Read(e, 2912)).ToArray();
        }
        string root = Path.Combine(Path.GetTempPath(), "aether-ereader-promo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string romPath = Path.Combine(root, "reader.gba"); File.WriteAllBytes(romPath, rom);
            using var machine = new GbaProductionMachine(romPath, Path.Combine(root, "reader.sav"), new(0, true, true, true, true, true, 44100));
            void Step(int frames, GameBoyButtons buttons = GameBoyButtons.None)
            {
                machine.SetButtons(buttons);
                for (int i = 0; i < frames; i++) machine.RunFrame();
                machine.SetButtons(GameBoyButtons.None); _ = machine.CaptureState();
            }
            Step(180); Step(8, GameBoyButtons.A); Step(120); Step(8, GameBoyButtons.A); Step(120);
            foreach (byte[] card in cards) machine.QueueEReaderCard(card);
            _ = machine.CaptureState();
            Step(334); Step(1, GameBoyButtons.A); Step(334);
            Step(600); Step(1, GameBoyButtons.A); Step(600); Step(1, GameBoyButtons.Start); Step(600);
            Step(60, GameBoyButtons.Right); Step(15, GameBoyButtons.A); Step(600);
            int[] pixels = new int[VideoGeometry.GameBoyAdvance.PixelCount]; long sequence = -1;
            Assert.IsTrue(machine.TryCopyVideoFrame(pixels, ref sequence));
            byte[] png = NativeScreenshot.Encode(VideoGeometry.GameBoyAdvance, pixels);
            string? output = Environment.GetEnvironmentVariable("AETHERBOY_EREADER_TEST_OUTPUT");
            if (!string.IsNullOrWhiteSpace(output))
            {
                Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output, title + ".png"), png);
            }
            Assert.AreEqual(gameImage, Hash(png), "Visually inspected running program.");
            var reader = machine.CaptureSnapshot(SessionState.Running, false, false, 0, 0).EReader!;
            Assert.AreEqual(2, reader.CardsStarted); Assert.AreEqual(0, reader.QueuedCards);
            byte[] checkpoint = machine.CaptureState();
            machine.RestoreState(checkpoint); Step(120, GameBoyButtons.Left);
            byte[] replay = machine.CaptureState();
            for (int i = 0; i < 3; i++)
            {
                machine.RestoreState(checkpoint); Step(120, GameBoyButtons.Left);
                CollectionAssert.AreEqual(replay, machine.CaptureState(), "Standalone program replay " + i);
            }
            Assert.IsFalse(machine.CaptureSnapshot(SessionState.Running, false, false, 0, 0).DiagnosticEvents
                .Any(e => e.Category == "UnknownOpcode" || e.Message == "HLE BIOS service 0x7F"));
        }
        finally
        {
            Directory.Delete(root, recursive: true); // only this test's unique directory
            Assert.AreEqual(RomZipHash, HashFile(romZip));
            Assert.AreEqual(CollectionHash, HashFile(collection));
        }
    }

    private static byte[] Read(ZipArchiveEntry entry, int maximum)
    {
        Assert.IsTrue(entry.Length > 0 && entry.Length <= maximum);
        byte[] bytes = new byte[(int)entry.Length]; using var stream = entry.Open();
        stream.ReadExactly(bytes); Assert.AreEqual(-1, stream.ReadByte()); return bytes;
    }
    private static string RequireFile(string variable)
    {
        string? path = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(path)) Assert.Inconclusive("Set " + variable + " to the locally supplied archive.");
        return path!;
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string HashFile(string path)
    {
        using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file));
    }
}
