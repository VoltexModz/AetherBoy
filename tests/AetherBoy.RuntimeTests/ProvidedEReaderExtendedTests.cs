using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Video;
using nanoboy.Core;

namespace AetherBoy.RuntimeTests;

/// <summary>
/// Opt-in, owner-supplied e-Reader software. Technical smoke/replay checks are
/// intentionally separate from visual gameplay approval and card-scan approval.
/// No ROM, card or flash bytes are committed with these tests.
/// </summary>
[TestClass]
public sealed class ProvidedEReaderExtendedTests
{
    private const string CollectionHash = "A70DD576BFAF96BFC0FF52E0C23E5231EA231D041F6F787DA9EDAB3704E985DD";
    private const string RomHash = "7D2D5CFEDD269FDE0327F8BCBD3C7F9150BFB54541F440C8715A9BC4BF53F933";
    public TestContext TestContext { get; set; } = null!;

    public static IEnumerable<object[]> MarioCards => new[]
    {
        "Big Boo", "Bowser", "Daisy", "Graceful Princess Peach", "Lakitu", "Princess Peach",
        "Super Waluigi", "Super Wario", "Waluigi", "Wario", "Yoshi"
    }.Select(name => new object[] { "Mario Party-e - " + name + " (USA)" });

    public static IEnumerable<object[]> StoredApplications => """
        Aquapolis - Dream Eater (USA) (Mini-Game)
        Aquapolis - Harvest Time (USA) (Mini-Game)
        Aquapolis - Jumping Doduo (USA) (Mini-Game)
        Aquapolis - Mighty Tyranitar (USA) (Mini-Game)
        Aquapolis - Punching Bags (USA) (Mini-Game)
        Aquapolis - Rolling Voltorb (USA) (Mini-Game)
        Aquapolis - Sneak and Snatch (USA) (Mini-Game)
        Expedition - Diving Corsola (USA) (Mini-Game)
        Expedition - Flower Power (USA) (Mini-Game)
        Expedition - Flying Journey (USA) (Cartoon)
        Expedition - Go, Poliwrath! (USA) (Mini-Game)
        Expedition - Gotcha! (USA) (Cartoon)
        Expedition - Here Comes Gloom (USA) (Cartoon)
        Expedition - Hold Down Hoppip (USA) (Mini-Game)
        Expedition - Kingler's Day (USA) (Mini-Game)
        Expedition - Lifesaver (USA) (Cartoon)
        Expedition - Machop At Work (USA) (Mini-Game)
        Expedition - Magby & Magmar (USA) (Cartoon)
        Expedition - Make a Dash! (USA) (Cartoon)
        Expedition - Metronome (USA) (Cartoon)
        Expedition - Sweet Scent (USA) (Cartoon)
        Expedition - TCG Supplement - Coin Flipper 1 (USA)
        Expedition - TCG Supplement - Duel Timer 1 (USA)
        Expedition - TCG Supplement - Duel Timer 2 (USA)
        Skyridge - Berry Tree (USA) (Mini-Game)
        Skyridge - Ditto Leapfrog (USA) (Mini-Game)
        Skyridge - Follow Hoothoot (USA) (Mini-Game)
        Skyridge - Haunter (USA) (Mini-Game)
        Skyridge - Leek Game (USA) (Mini-Game)
        Skyridge - Night Flight (USA) (Mini-Game)
        Skyridge - Pika Pop (USA) (Mini-Game)
        Skyridge - Ride the Tuft (USA) (Mini-Game)
        Skyridge - TCG Supplement - Poke-Power Ability Energy Capture (USA)
        Skyridge - TCG Supplement - Poke-Power Ability Energy Sprinkle (USA)
        Skyridge - TCG Supplement - Poke-Power Ability Psy Capture (USA)
        Skyridge - TCG Supplement - Poke-Power Ability Winding Back (USA)
        Skyridge - Teddiursa (USA) (Mini-Game)
        Skyridge - Watch Out! (USA) (Mini-Game)
        """.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Select(name => new object[] { name });

    [TestMethod, Timeout(240_000)]
    [DynamicData(nameof(MarioCards))]
    public void MarioPartyRawCardAndIndependentFlashSmokeReplay(string title) => Audit(title, raw: true);

    [TestMethod, Timeout(240_000)]
    [DynamicData(nameof(StoredApplications))]
    public void StoredPokemonApplicationSmokeReplayAndFlashReopen(string title) => Audit(title, raw: false);

    [TestMethod, Timeout(240_000)]
    public void MachopThreeRawCardsCompleteCollectionAndReplayWithoutInjectedSave()
    {
        string romZip = Require("AETHERBOY_EREADER_ROM_ZIP");
        string collection = Require("AETHERBOY_EREADER_COLLECTION_ZIP");
        Assert.AreEqual(RomHash, HashFile(romZip)); Assert.AreEqual(CollectionHash, HashFile(collection));
        byte[] rom;
        using (var zip = ZipFile.OpenRead(romZip)) rom = Read(zip.Entries.Single(), 8_388_608);
        List<byte[]> cards = [];
        using (var outer = ZipFile.OpenRead(collection))
            foreach (string name in new[] { "Machop", "Machoke", "Machamp" })
            {
                string archive = $"Pokemon-e TCG - Expedition - {name} (USA).zip";
                using var bytes = new MemoryStream(Read(outer.Entries.Single(e => Path.GetFileName(e.FullName) == archive), 64 * 1024), false);
                using var nested = new ZipArchive(bytes, ZipArchiveMode.Read);
                cards.Add(Read(nested.Entries.Single(e => e.FullName.Contains("Long Strip", StringComparison.Ordinal)), 2912));
            }
        string root = Path.Combine(Path.GetTempPath(), "aether-ereader-machop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string? outputRoot = Environment.GetEnvironmentVariable("AETHERBOY_EREADER_EXTENDED_OUTPUT");
        string? output = string.IsNullOrWhiteSpace(outputRoot) ? null : Path.Combine(outputRoot, "machop-raw-three-cards");
        if (output is not null) Directory.CreateDirectory(output);
        List<Observation> observations = [];
        int total = 0;
        try
        {
            string romPath = Path.Combine(root, "reader.gba"); File.WriteAllBytes(romPath, rom);
            using var machine = Machine(romPath, Path.Combine(root, "reader.sav"));
            using var audio = new AudioMeter(machine);
            void Step(int frames, GameBoyButtons buttons = GameBoyButtons.None) => Run(machine, frames, buttons, ref total);
            void Observe(string label) => observations.Add(Capture(machine, audio, label, output, total));
            Step(180); Step(8, GameBoyButtons.A); Step(120); Step(8, GameBoyButtons.A); Step(120);
            for (int i = 0; i < cards.Count; i++)
            {
                machine.QueueEReaderCard(cards[i]); _ = machine.CaptureState();
                if (i > 0) Step(1, GameBoyButtons.A);
                Step(334); Observe($"card-{i + 1}-accepted");
                Step(600); Observe($"card-{i + 1}-later");
                Assert.AreEqual(i + 1, Snapshot(machine).EReader!.CardsStarted);
                Assert.AreEqual(0, Snapshot(machine).EReader!.QueuedCards);
            }
            Assert.AreEqual("BFFFC07D02D6A5717339B0F5B1156A29C49BCC334BF13C7657FE7E6951F8C540", observations[^1].Image,
                "Visually inspected Machop game countdown after all three scanned cards, not a saved application.");
            byte[] start = machine.CaptureState();
            machine.RestoreState(start); Step(120, GameBoyButtons.Right);
            byte[] expected = machine.CaptureState(); Observe("moving");
            for (int replay = 0; replay < 5; replay++)
            {
                machine.RestoreState(start); Step(120, GameBoyButtons.Right);
                CollectionAssert.AreEqual(expected, machine.CaptureState());
            }
            Step(1800); Observe("after-1800-frames");
            Assert.IsFalse(Snapshot(machine).DiagnosticEvents.Any(e => e.Category == "UnknownOpcode" || e.Message == "HLE BIOS service 0x7F"));
        }
        finally
        {
            if (output is not null) File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new
                { Title = "Machop At Work - three actual raw scans", Frames = total, Observations = observations }, new JsonSerializerOptions { WriteIndented = true }));
            Directory.Delete(root, true);
            Assert.AreEqual(RomHash, HashFile(romZip)); Assert.AreEqual(CollectionHash, HashFile(collection));
        }
    }

    private void Audit(string title, bool raw)
    {
        string romZip = Require("AETHERBOY_EREADER_ROM_ZIP");
        string collection = Require("AETHERBOY_EREADER_COLLECTION_ZIP");
        Assert.AreEqual(RomHash, HashFile(romZip)); Assert.AreEqual(CollectionHash, HashFile(collection));
        byte[] rom;
        using (var zip = ZipFile.OpenRead(romZip)) rom = Read(zip.Entries.Single(), 8_388_608);
        byte[][] cards = [];
        byte[]? seed = null;
        using (var zip = ZipFile.OpenRead(collection))
        {
            string archive = raw ? title + ".zip" : "Memory Dumps - Pokemon-e TCG (USA).zip";
            using var nestedBytes = new MemoryStream(Read(zip.Entries.Single(e => Path.GetFileName(e.FullName) == archive), 8 * 1024 * 1024), false);
            using var nested = new ZipArchive(nestedBytes, ZipArchiveMode.Read);
            if (raw)
            {
                Assert.HasCount(2, nested.Entries);
                cards = nested.Entries.OrderBy(e => e.FullName, StringComparer.Ordinal).Select(e => Read(e, 2912)).ToArray();
            }
            else seed = Read(nested.Entries.Single(e => e.FullName == title + ".sav"), 131072);
            if (raw)
            {
                using var savedBytes = new MemoryStream(Read(zip.Entries.Single(e => Path.GetFileName(e.FullName) == "Memory Dumps - Mario Party-e (USA).zip"), 2 * 1024 * 1024), false);
                using var savedArchive = new ZipArchive(savedBytes, ZipArchiveMode.Read);
                seed = Read(savedArchive.Entries.Single(e => e.FullName == title + ".sav"), 131072);
            }
        }
        string root = Path.Combine(Path.GetTempPath(), "aether-ereader-extended-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string? outputRoot = Environment.GetEnvironmentVariable("AETHERBOY_EREADER_EXTENDED_OUTPUT");
        // Only use a generated fixed-length directory name, never an archive path.
        string? output = string.IsNullOrWhiteSpace(outputRoot) ? null : Path.Combine(outputRoot, Hash(System.Text.Encoding.UTF8.GetBytes(title))[..16]);
        if (output is not null) Directory.CreateDirectory(output);
        List<Observation> observations = [];
        int totalFrames = 0, replays = 0;
        bool inputChangesImage = false;
        int soak = 1800;
        if (int.TryParse(Environment.GetEnvironmentVariable("AETHERBOY_EREADER_SOAK_FRAMES"), out int requested))
            soak = Math.Clamp(requested, 1800, 18000);
        string romPath = Path.Combine(root, "reader.gba"), savePath = Path.Combine(root, "reader.sav");
        try
        {
            File.WriteAllBytes(romPath, rom);
            if (!raw && seed is not null) File.WriteAllBytes(savePath, seed);
            using (var machine = Machine(romPath, savePath))
            using (var audio = new AudioMeter(machine))
            {
                void Step(int frames, GameBoyButtons buttons = GameBoyButtons.None) => Run(machine, frames, buttons, ref totalFrames);
                void Observe(string label) => observations.Add(Capture(machine, audio, label, output, totalFrames));
                Step(180); Step(8, GameBoyButtons.A); Step(120);
                if (raw)
                {
                    Step(8, GameBoyButtons.A); Step(120);
                    foreach (byte[] card in cards) machine.QueueEReaderCard(card);
                    Step(334); Observe("01-first-strip");
                    Step(1, GameBoyButtons.A); Step(334); Step(600); Observe("02-scan-complete");
                    Assert.AreEqual(2, Snapshot(machine).EReader!.CardsStarted);
                    Assert.AreEqual(0, Snapshot(machine).EReader!.QueuedCards);
                    // Mario Party cards auto-execute. There is no NES-style
                    // "save application" prompt here. Do not manufacture one.
                    Step(8, GameBoyButtons.A); Step(600); Observe("03-first-mode");
                }
                else
                {
                    Step(8, GameBoyButtons.Down); Step(20); Step(8, GameBoyButtons.Down); Step(20);
                    Step(8, GameBoyButtons.A); Step(600);
                    Assert.AreEqual(0, Snapshot(machine).EReader!.CardsStarted);
                }
                Observe("04-launched");
                Step(1, GameBoyButtons.Start); Step(120); Observe("05-after-start");
                Step(1, GameBoyButtons.A); Step(120); Observe("06-after-a");
                byte[] branch = machine.CaptureState();
                machine.RestoreState(branch); Step(120);
                string idle = Capture(machine, audio, "07-idle-branch", output, totalFrames).Image;
                machine.RestoreState(branch); Step(120, GameBoyButtons.Right | GameBoyButtons.A);
                string controlled = Capture(machine, audio, "08-input-branch", output, totalFrames).Image;
                inputChangesImage = idle != controlled;
                // Distinct screenshots are evidence of an input response, not a
                // requirement: cartoons and chance/time-based games may ignore it.
                Observe("09-before-soak");
                GameBoyButtons[] sequence = [GameBoyButtons.Left, GameBoyButtons.A, GameBoyButtons.Right,
                    GameBoyButtons.None, GameBoyButtons.Up, GameBoyButtons.A, GameBoyButtons.Down, GameBoyButtons.None];
                for (int elapsed = 0; elapsed < soak; elapsed += 60)
                {
                    Step(Math.Min(60, soak - elapsed), sequence[(elapsed / 60) % sequence.Length]);
                    if ((elapsed + 60) % 600 == 0) Observe("soak-" + (elapsed + 60));
                }
                Observe("10-after-soak");
                byte[] checkpoint = machine.CaptureState();
                machine.RestoreState(checkpoint); Step(120, GameBoyButtons.Left);
                byte[] expected = machine.CaptureState();
                for (int i = 0; i < 5; i++)
                {
                    machine.RestoreState(checkpoint); Step(120, GameBoyButtons.Left);
                    CollectionAssert.AreEqual(expected, machine.CaptureState(), "Full state replay " + i);
                    replays++;
                }
                Observe("11-replayed");
                Assert.IsFalse(Snapshot(machine).DiagnosticEvents.Any(e => e.Category == "UnknownOpcode" || e.Message == "HLE BIOS service 0x7F"));
            }
            string reopenPath = savePath;
            if (raw)
            {
                // An independent supplied flash image tests the load route,
                // not a save action that this card's UI did not offer.
                reopenPath = Path.Combine(root, "provided.sav");
                File.WriteAllBytes(reopenPath, seed!);
            }
            byte[] saved = File.ReadAllBytes(reopenPath);
            Assert.HasCount(131072, saved);
            Assert.IsTrue(saved.AsSpan(0x10000).ContainsAnyExcept((byte)0xFF), "Application bank cannot be blank.");
            using (var reopened = Machine(romPath, reopenPath))
            using (var audio = new AudioMeter(reopened))
            {
                Run(reopened, 180, GameBoyButtons.None, ref totalFrames);
                Run(reopened, 8, GameBoyButtons.A, ref totalFrames); Run(reopened, 120, GameBoyButtons.None, ref totalFrames);
                observations.Add(Capture(reopened, audio, "12-reopened-menu", output, totalFrames));
                Run(reopened, 8, GameBoyButtons.Down, ref totalFrames); Run(reopened, 20, GameBoyButtons.None, ref totalFrames);
                Run(reopened, 8, GameBoyButtons.Down, ref totalFrames); Run(reopened, 20, GameBoyButtons.None, ref totalFrames);
                Run(reopened, 8, GameBoyButtons.A, ref totalFrames); Run(reopened, 600, GameBoyButtons.None, ref totalFrames);
                observations.Add(Capture(reopened, audio, "13-reopened-application", output, totalFrames));
                Assert.AreEqual(0, Snapshot(reopened).EReader!.CardsStarted);
            }
            // Reading/playing an application is not permission to overwrite its source dump.
            CollectionAssert.AreEqual(saved, File.ReadAllBytes(reopenPath));
            TestContext.WriteLine($"{title}: {totalFrames} emulated frames, {replays} identical replays, visual input difference={inputChangesImage}.");
        }
        finally
        {
            if (output is not null)
                File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new
                { Title = title, Mode = raw ? "raw-two-strip-and-independent-provided-flash" : "provided-flash-no-scan", Frames = totalFrames,
                    SoakFrames = soak, Replays = replays, InputChangesImage = inputChangesImage, Observations = observations }, new JsonSerializerOptions { WriteIndented = true }));
            Directory.Delete(root, true); // a unique test-owned directory, never a user save path
            Assert.AreEqual(RomHash, HashFile(romZip)); Assert.AreEqual(CollectionHash, HashFile(collection));
        }
    }

    private static GbaProductionMachine Machine(string rom, string save) => new(rom, save, new(0, true, true, true, true, true, 44100));
    private static EmulationSnapshot Snapshot(GbaProductionMachine machine) => machine.CaptureSnapshot(SessionState.Running, false, false, 0, 0);
    private static void Run(GbaProductionMachine machine, int frames, GameBoyButtons buttons, ref int total)
    {
        machine.SetButtons(buttons);
        for (int i = 0; i < frames; i++) { machine.RunFrame(); total++; }
        machine.SetButtons(GameBoyButtons.None); _ = machine.CaptureState();
    }
    private static Observation Capture(GbaProductionMachine machine, AudioMeter audio, string label, string? output, int frames)
    {
        int[] pixels = new int[VideoGeometry.GameBoyAdvance.PixelCount]; long sequence = -1;
        Assert.IsTrue(machine.TryCopyVideoFrame(pixels, ref sequence));
        byte[] image = NativeScreenshot.Encode(VideoGeometry.GameBoyAdvance, pixels);
        if (output is not null) File.WriteAllBytes(Path.Combine(output, label + ".png"), image);
        var result = new Observation(label, frames, Hash(image), Hash(machine.CaptureState()), audio.Samples, audio.Minimum, audio.Maximum,
            Convert.ToHexString(audio.Hash.GetHashAndReset()), Snapshot(machine).DiagnosticEvents.ToArray());
        audio.Reset(); return result;
    }
    private sealed record Observation(string Label, int Frames, string Image, string State, long Samples, float Minimum, float Maximum, string AudioHash, GbaDiagnosticEventSnapshot[] Diagnostics);
    private sealed class AudioMeter : IDisposable
    {
        internal long Samples; internal float Minimum, Maximum;
        internal readonly IncrementalHash Hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        internal AudioMeter(GbaProductionMachine machine) => machine.AudioSamplesAvailable += (_, e) =>
        {
            float[] samples = e.GetInterleavedSamplesCopy();
            foreach (float sample in samples)
            {
                Assert.IsTrue(float.IsFinite(sample)); Minimum = Math.Min(Minimum, sample); Maximum = Math.Max(Maximum, sample); Samples++;
            }
            Hash.AppendData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(samples.AsSpan()));
        };
        internal void Reset() { Samples = 0; Minimum = Maximum = 0; }
        public void Dispose() => Hash.Dispose();
    }
    private static byte[] Read(ZipArchiveEntry entry, int maximum)
    {
        Assert.IsTrue(entry.Length > 0 && entry.Length <= maximum);
        byte[] bytes = new byte[entry.Length]; using var stream = entry.Open(); stream.ReadExactly(bytes);
        Assert.AreEqual(-1, stream.ReadByte()); return bytes;
    }
    private static string Require(string key)
    {
        string? path = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(path)) Assert.Inconclusive("Set " + key + " to the owner-provided archive.");
        return path!;
    }
    private static string HashFile(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
