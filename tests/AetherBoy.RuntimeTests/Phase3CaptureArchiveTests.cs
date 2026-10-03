using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Cartridges;
using SharpCompress.Writers.SevenZip;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class Phase3CaptureArchiveTests
{
    private string root = null!;
    [TestInitialize] public void Setup() { root = Path.Combine(Path.GetTempPath(), "aetherboy-phase3-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); }
    [TestCleanup] public void Cleanup() => Directory.Delete(root, true);

    [TestMethod]
    [DataRow("gb", "zip")][DataRow("gbc", "zip")][DataRow("gba", "zip")]
    [DataRow("gb", "7z")][DataRow("gbc", "7z")][DataRow("gba", "7z")]
    public void SingleRomExtractsWithoutSaveAndRemovesOnlyItsOwnStaging(string system, string archiveType)
    {
        byte[] rom = Enumerable.Range(0, 32768).Select(i => (byte)i).ToArray();
        string input = Path.Combine(root, "game." + archiveType), stage = Path.Combine(root, "stage");
        if (archiveType == "zip") WriteZip(input, ("folder/game." + system, rom), ("folder/game.sav", [1, 2, 3]));
        else
        {
            using var output = File.Create(input); using var seven = new SevenZipWriter(output, new SevenZipWriterOptions());
            using var source = new MemoryStream(rom); seven.Write("folder/game." + system, source, null);
            using var save = new MemoryStream(new byte[] { 1, 2, 3 }); seven.Write("folder/game.sav", save, null);
            using var empty = new MemoryStream(); seven.Write("empty.txt", empty, null);
        }
        byte[] original = File.ReadAllBytes(input);
        string extracted;
        using (var prepared = RomArchiveSource.Open(input, stage))
        {
            extracted = prepared.Path;
            Assert.IsTrue(prepared.WasArchive);
            CollectionAssert.AreEqual(rom, File.ReadAllBytes(extracted));
            Assert.AreEqual(1, Directory.GetFiles(stage, "*", SearchOption.AllDirectories).Length);
        }
        Assert.IsFalse(File.Exists(extracted)); Assert.AreEqual(0, Directory.GetDirectories(stage).Length);
        CollectionAssert.AreEqual(original, File.ReadAllBytes(input));
    }

    [TestMethod]
    public void RawRomIsNotDeletedAndCancellationCreatesNoStaging()
    {
        string path = Path.Combine(root, "game.GBC"), stage = Path.Combine(root, "stage");
        File.WriteAllBytes(path, new byte[32768]);
        using (var source = RomArchiveSource.Open(path, stage)) { Assert.IsFalse(source.WasArchive); Assert.AreEqual(path, source.Path); }
        Assert.IsTrue(File.Exists(path));
        Assert.Throws<OperationCanceledException>(() => RomArchiveSource.Open(path, stage, new CancellationToken(true)));
        Assert.IsFalse(Directory.Exists(stage));
    }

    [TestMethod]
    [DataRow("../game.gb")][DataRow("folder/../../game.gba")][DataRow("/game.gb")][DataRow("C:/game.gb")][DataRow("..\\game.gbc")]
    public void RejectsUnsafeArchivePathsBeforeExtracting(string entry)
    {
        string archive = Path.Combine(root, "game.zip"), stage = Path.Combine(root, "stage");
        WriteZip(archive, (entry, new byte[32768]));
        Assert.Throws<InvalidDataException>(() => RomArchiveSource.Open(archive, stage));
        Assert.IsFalse(Directory.Exists(stage));
    }

    [TestMethod]
    public void RejectsAmbiguousEmptyAndTooManyEntries()
    {
        string archive = Path.Combine(root, "game.zip"), stage = Path.Combine(root, "stage");
        WriteZip(archive, ("one.gb", new byte[32768]), ("two.gba", new byte[32768]));
        Assert.Throws<RomArchiveSelectionRequiredException>(() => RomArchiveSource.Open(archive, stage));
        WriteZip(archive, ("readme.txt", [1]));
        Assert.Throws<InvalidDataException>(() => RomArchiveSource.Open(archive, stage));
        WriteZip(archive, Enumerable.Range(0, 513).Select(i => ($"{i}.txt", new byte[] { 1 })).ToArray());
        Assert.Throws<InvalidDataException>(() => RomArchiveSource.Open(archive, stage));
        Assert.IsFalse(Directory.Exists(stage));
    }

    [TestMethod]
    [DataRow("zip")][DataRow("7z")]
    public void ArchiveSelectionIsExplicitExtractsOnlyChosenEntryAndBindsExactContent(string extension)
    {
        string path = Path.Combine(root, "collection." + extension), stage = Path.Combine(root, "stage");
        byte[] first = new byte[32768], second = Enumerable.Repeat((byte)27, 32768).ToArray();
        if (extension == "zip") WriteZip(path, ("one/game.gb", first), ("two/game.gba", second), ("two/game.sav", [9]));
        else
        {
            using var output = File.Create(path); using var seven = new SevenZipWriter(output, new SevenZipWriterOptions());
            using var a = new MemoryStream(first); seven.Write("one/game.gb", a, null);
            using var b = new MemoryStream(second); seven.Write("two/game.gba", b, null);
            using var save = new MemoryStream(new byte[] { 9 }); seven.Write("two/game.sav", save, null);
        }
        var request = Assert.Throws<RomArchiveSelectionRequiredException>(() => RomArchiveSource.Open(path, stage));
        Assert.IsFalse(Directory.Exists(stage)); Assert.HasCount(2, request.Choices);
        Assert.AreEqual("GBA", request.Choices[1].System); Assert.AreEqual(32768L, request.Choices[1].Size);
        Assert.Throws<OperationCanceledException>(() => RomArchiveSource.Open(path, stage, new CancellationToken(true), request.Choices[1]));
        Assert.IsFalse(Directory.Exists(stage));
        using (var source = RomArchiveSource.Open(path, stage, choice: request.Choices[1]))
        {
            CollectionAssert.AreEqual(second, File.ReadAllBytes(source.Path));
            Assert.AreEqual(1, Directory.GetFiles(stage, "*", SearchOption.AllDirectories).Length);
        }
        // Even a valid new archive at the same path cannot reuse the previous choice.
        File.Delete(path);
        if (extension == "zip") WriteZip(path, ("game.gb", first));
        else
        {
            using var output = File.Create(path); using var seven = new SevenZipWriter(output, new SevenZipWriterOptions());
            using var data = new MemoryStream(first); seven.Write("game.gb", data, null);
        }
        StringAssert.Contains(Assert.Throws<InvalidDataException>(() => RomArchiveSource.Open(path, stage, choice: request.Choices[1])).Message, "changed");
        Assert.IsEmpty(Directory.GetDirectories(stage));
    }

    [TestMethod]
    public void DuplicateArchiveNamesRemainDistinctAndDisplayNamesCannotInjectControls()
    {
        string path = Path.Combine(root, "duplicates.zip"), stage = Path.Combine(root, "stage");
        byte[] first = new byte[32768], second = Enumerable.Repeat((byte)22, 32768).ToArray();
        WriteZip(path, ("same\u202E\n.gb", first), ("same\u202E\n.gb", second));
        var choices = Assert.Throws<RomArchiveSelectionRequiredException>(() => RomArchiveSource.Open(path, stage)).Choices;
        Assert.AreEqual("same  .gb", choices[0].DisplayName);
        using var selected = RomArchiveSource.Open(path, stage, choice: choices[1]);
        CollectionAssert.AreEqual(second, File.ReadAllBytes(selected.Path));
        string raw = Path.Combine(root, "raw.gb"); File.WriteAllBytes(raw, first);
        Assert.Throws<InvalidDataException>(() => RomArchiveSource.Open(raw, stage, choice: choices[1]));
    }

    [TestMethod]
    public void OverlongArchiveNamesAreRejectedBeforeSelectionOrExtraction()
    {
        string path = Path.Combine(root, "long.zip"), stage = Path.Combine(root, "stage");
        WriteZip(path, (new string('x', RomArchiveSource.MaximumEntryNameLength) + ".gb", new byte[32768]));
        Assert.Throws<InvalidDataException>(() => RomArchiveSource.Open(path, stage));
        Assert.IsFalse(Directory.Exists(stage));
    }

    [TestMethod]
    [DataRow("zip")][DataRow("7z")]
    public void BrokenArchiveHeadersAreRecoverableAndDoNotCreateStaging(string extension)
    {
        string archive = Path.Combine(root, "broken." + extension), stage = Path.Combine(root, "stage");
        File.WriteAllBytes(archive, [0x50, 0x4B, 0x03, 0x04, 0xFF]);
        Exception error = Assert.Throws<Exception>(() => RomArchiveSource.Open(archive, stage));
        Assert.IsTrue(error is InvalidDataException or IOException);
        Assert.IsFalse(Directory.Exists(stage));
    }

    [TestMethod]
    public void EncryptedZipEntryIsRejectedBeforePayloadExtraction()
    {
        string archive = Path.Combine(root, "encrypted.zip"), stage = Path.Combine(root, "stage");
        WriteZip(archive, ("game.gb", new byte[32768]));
        byte[] bytes = File.ReadAllBytes(archive); bytes[6] |= 1;
        int central = Find(bytes, "PK\x01\x02"); bytes[central + 8] |= 1; File.WriteAllBytes(archive, bytes);
        Assert.Throws<InvalidDataException>(() => RomArchiveSource.Open(archive, stage));
        Assert.IsFalse(Directory.Exists(stage));
    }

    [TestMethod]
    public void ChecksumFailureCleansUpStagingAndKeepsArchive()
    {
        string archive = Path.Combine(root, "game.zip"), stage = Path.Combine(root, "stage");
        WriteZip(archive, ("game.gb", new byte[32768]));
        byte[] bytes = File.ReadAllBytes(archive);
        int payload = 30 + BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(26)) + BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(28));
        bytes[payload + 100] ^= 1; File.WriteAllBytes(archive, bytes);
        Assert.Throws<InvalidDataException>(() => RomArchiveSource.Open(archive, stage));
        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(archive));
        Assert.AreEqual(0, Directory.GetFiles(stage, "*", SearchOption.AllDirectories).Length);
    }

    [TestMethod]
    public void RejectsOversizedRomAndArchiveWithoutExpanding()
    {
        string archive = Path.Combine(root, "game.zip"), stage = Path.Combine(root, "stage");
        using (var file = File.Create(archive)) file.SetLength(RomArchiveSource.MaximumArchiveBytes + 1);
        Assert.Throws<InvalidDataException>(() => RomArchiveSource.Open(archive, stage));
        WriteZip(archive, ("game.gb", new byte[512]));
        byte[] bytes = File.ReadAllBytes(archive); int central = Find(bytes, "PK\x01\x02");
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(central + 24), 33 * 1024 * 1024); File.WriteAllBytes(archive, bytes);
        Assert.Throws<InvalidDataException>(() => RomArchiveSource.Open(archive, stage));
        Assert.IsFalse(Directory.Exists(stage));
    }

    [TestMethod]
    [DataRow(160, 144, 44100)][DataRow(240, 160, 65536)][DataRow(3, 2, 48000)]
    public async Task VideoHasValidHeadersIndexNativeCadenceStereoAndBottomUpPixels(int width, int height, int rate)
    {
        string path = Path.Combine(root, "video.avi"); var geometry = new VideoGeometry(width, height);
        var capture = new GameplayRecorder(path, geometry, rate);
        int[] pixels = Enumerable.Repeat(0x112233, width * height).ToArray(); pixels[^1] = 0xFFCC55;
        for (int frame = 0; frame < 4; frame++)
        {
            float[] sound = Enumerable.Range(0, rate / 50 * 2).Select(i => i % 2 == 0 ? 0.5f : -0.25f).ToArray();
            capture.SubmitAudio(new(sound, rate, 2)); capture.SubmitFrame(pixels);
        }
        capture.Stop(); await capture.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsNull(capture.Error); Assert.AreEqual(4L, capture.FramesWritten); Assert.IsFalse(File.Exists(capture.PartialPath));
        byte[] bytes = File.ReadAllBytes(path);
        Assert.AreEqual("RIFF", Encoding.ASCII.GetString(bytes, 0, 4)); Assert.AreEqual((uint)bytes.Length - 8, U(bytes, 4));
        int main = Find(bytes, "avih") + 8; Assert.AreEqual(4u, U(bytes, main + 16)); Assert.AreEqual(2u, U(bytes, main + 24));
        int v = Find(bytes, "strh") + 8; Assert.AreEqual(4389u, U(bytes, v + 20)); Assert.AreEqual(262144u, U(bytes, v + 24));
        Assert.AreEqual(4u, U(bytes, v + 32));
        int a = Find(bytes, "strh", v) + 8; Assert.AreEqual(4u, U(bytes, a + 20)); Assert.AreEqual((uint)rate * 4, U(bytes, a + 24));
        Assert.AreEqual((uint)(4L * rate * 4389 / 262144), U(bytes, a + 32));
        int movi = Find(bytes, "movi"), idx = Find(bytes, "idx1"); Assert.AreEqual(8u * 16, U(bytes, idx + 4));
        int totalPcm = 0;
        for (int i = 0; i < 8; i++)
        {
            int entry = idx + 8 + i * 16, chunk = movi + (int)U(bytes, entry + 8);
            Assert.AreEqual(Encoding.ASCII.GetString(bytes, entry, 4), Encoding.ASCII.GetString(bytes, chunk, 4));
            Assert.AreEqual(U(bytes, entry + 12), U(bytes, chunk + 4));
            if (i % 2 == 1) totalPcm += (int)U(bytes, chunk + 4) / 4;
        }
        Assert.AreEqual((int)U(bytes, a + 32), totalPcm);
        int firstPixels = movi + 12;
        CollectionAssert.AreEqual(new byte[] { 0x55, 0xCC, 0xFF }, bytes.AsSpan(firstPixels + (width - 1) * 3, 3).ToArray());
        int firstAudio = Find(bytes, "01wb", movi) + 8;
        Assert.AreEqual((short)16384, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(firstAudio)));
        Assert.AreEqual((short)-8192, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(firstAudio + 2)));
    }

    [TestMethod]
    public async Task SilenceMonoAndFormatChangeAreExplicitAndBounded()
    {
        var capture = new GameplayRecorder(Path.Combine(root, "silence.avi"), new(1, 1), 44100);
        capture.SubmitFrame([0]); Assert.IsTrue(capture.SilenceSamples > 0);
        capture.SubmitAudio(new(new float[] { 0.5f, float.NaN }, 44100)); capture.SubmitFrame([0]);
        capture.SubmitAudio(new(new float[] { 0 }, 48000));
        await capture.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(GameplayRecordingStop.AudioChanged, capture.StopReason); Assert.IsNull(capture.Error);
    }

    [TestMethod]
    public async Task BackpressureStopsInsteadOfDroppingFramesAndLimitKeepsValidEarlierClip()
    {
        using var gate = new ManualResetEventSlim();
        var capture = new GameplayRecorder(Path.Combine(root, "bounded.avi"), new(1, 1), 44100, GameplayRecorder.MaximumFileBytes, 1,
            () => { if (!gate.Wait(TimeSpan.FromSeconds(5))) throw new IOException("Test writer was not released."); });
        try
        {
            capture.SubmitFrame([0]); capture.SubmitFrame([0]);
            Assert.IsFalse(capture.IsRecording); Assert.AreEqual(GameplayRecordingStop.QueueFull, capture.StopReason);
        }
        finally { gate.Set(); }
        await capture.Completion.WaitAsync(TimeSpan.FromSeconds(5)); Assert.AreEqual(1L, capture.FramesWritten);
        var limited = new GameplayRecorder(Path.Combine(root, "limited.avi"), new(1, 1), 44100, 4096, 16);
        for (int i = 0; i < 6; i++) limited.SubmitFrame([0]);
        limited.Stop(); await limited.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(GameplayRecordingStop.SizeLimit, limited.StopReason); Assert.IsTrue(new FileInfo(limited.Path).Length <= 4096);
        Assert.AreEqual(1L, limited.FramesWritten); Assert.IsNull(limited.Error);
    }

    [TestMethod]
    public async Task ExistingOutputAndPartialFilesAreNeverOverwritten()
    {
        foreach (bool partial in new[] { false, true })
        {
            string path = Path.Combine(root, partial ? "partial.avi" : "exists.avi"), existing = path + (partial ? ".partial" : "");
            File.WriteAllBytes(existing, [42]);
            var capture = new GameplayRecorder(path, new(1, 1), 44100); capture.SubmitFrame([0]); capture.Stop();
            await capture.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual(GameplayRecordingStop.WriteError, capture.StopReason); Assert.IsNotNull(capture.Error);
            CollectionAssert.AreEqual(new byte[] { 42 }, File.ReadAllBytes(existing));
        }
    }

    [TestMethod]
    [DataRow("turbo")][DataRow("reset")][DataRow("shutdown")][DataRow("rewind")][DataRow("restore")]
    public async Task SessionEndsCaptureOnTimelineChangesWithoutFaulting(string operation)
    {
        using var pacer = new ManualFramePacer();
        await using var session = new EmulationSession(() => new RecordingMachine(null), pacer);
        var timeout = TimeSpan.FromSeconds(5); pacer.WaitForWaitCount(1, timeout);
        var start = session.StartGameplayRecordingAsync(Path.Combine(root, "session.avi")); pacer.ReleaseOneFrame();
        var capture = await start.WaitAsync(timeout); pacer.WaitForWaitCount(2, timeout);
        Task change = operation switch { "turbo" => session.SetTurboAsync(true), "reset" => session.ResetAsync(),
            "rewind" => session.RewindAsync(), "restore" => session.RestoreStateAsync([1]), _ => session.ShutdownAsync() };
        pacer.ReleaseOneFrame(); await change.WaitAsync(timeout); await capture.Completion.WaitAsync(timeout);
        Assert.AreEqual(1L, capture.FramesWritten); Assert.IsNull(session.Fault); Assert.IsNull(capture.Error);
        Assert.AreEqual(operation == "turbo" ? GameplayRecordingStop.Turbo : operation == "shutdown" ? GameplayRecordingStop.SessionEnded : GameplayRecordingStop.TimelineChanged, capture.StopReason);
    }

    [TestMethod]
    public async Task PauseDoesNotAddFramesAndStartWhileTurboIsRejected()
    {
        using var pacer = new ManualFramePacer(); await using var session = new EmulationSession(() => new RecordingMachine(null), pacer);
        var timeout = TimeSpan.FromSeconds(5); pacer.WaitForWaitCount(1, timeout);
        var start = session.StartGameplayRecordingAsync(Path.Combine(root, "pause.avi")); pacer.ReleaseOneFrame();
        var capture = await start.WaitAsync(timeout); pacer.WaitForWaitCount(2, timeout);
        var pause = session.SetPausedAsync(true); pacer.ReleaseOneFrame(); await pause.WaitAsync(timeout);
        await session.SetButtonsAsync(nanoboy.Core.GameBoyButtons.A).WaitAsync(timeout);
        Assert.IsTrue(capture.IsRecording);
        capture.Stop(); await capture.Completion.WaitAsync(timeout); Assert.AreEqual(1L, capture.FramesWritten);
        await session.SetTurboAsync(true).WaitAsync(timeout);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartGameplayRecordingAsync(Path.Combine(root, "turbo.avi")));
        Assert.IsFalse(File.Exists(Path.Combine(root, "turbo.avi")));
    }

    private static uint U(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));

    [TestMethod]
    [DoNotParallelize]
    [DataRow("gb")][DataRow("gbc")][DataRow("gba")]
    public async Task RealCoreSyntheticRomRecordsNativeGeometryAndAudioRate(string system)
    {
        string romPath = Path.Combine(root, "synthetic." + system), savePath = Path.Combine(root, "synthetic.sav");
        byte[] rom = new byte[32768];
        if (system == "gba")
        {
            BinaryPrimitives.WriteUInt32LittleEndian(rom, 0xEAFFFFFE);
            Encoding.ASCII.GetBytes("CAPTURE TEST").CopyTo(rom, 0xA0); rom[0xB2] = 0x96;
        }
        else { rom[0x100] = 0x18; rom[0x101] = 0xFE; if (system == "gbc") rom[0x143] = 0x80; }
        File.WriteAllBytes(romPath, rom);
        var configuration = new nanoboy.Core.EmulatorConfiguration(0, true, true, true, true, true, 44100);
        using var pacer = new ManualFramePacer();
        await using var session = new EmulationSession(() => system == "gba"
            ? new GbaProductionMachine(romPath, savePath, configuration)
            : new ProductionMachine(romPath, savePath, null, configuration, 0), pacer);
        var timeout = TimeSpan.FromSeconds(10); pacer.WaitForWaitCount(1, timeout);
        var start = session.StartGameplayRecordingAsync(Path.Combine(root, "native.avi")); pacer.ReleaseOneFrame();
        var capture = await start.WaitAsync(timeout); pacer.WaitForWaitCount(2, timeout);
        for (int frame = 3; frame <= 12; frame++) { pacer.ReleaseOneFrame(); pacer.WaitForWaitCount(frame, timeout); }
        await session.ShutdownAsync().WaitAsync(timeout);
        Assert.AreEqual(11L, capture.FramesWritten); Assert.IsNull(capture.Error); Assert.IsNull(session.Fault);
        byte[] bytes = File.ReadAllBytes(capture.Path); int main = Find(bytes, "avih") + 8;
        Assert.AreEqual(system == "gba" ? 240u : 160u, U(bytes, main + 32));
        Assert.AreEqual(system == "gba" ? 160u : 144u, U(bytes, main + 36));
        int firstFormat = Find(bytes, "strf") + 8, audioFormat = Find(bytes, "strf", firstFormat) + 8;
        Assert.AreEqual(system == "gba" ? 65536u : 44100u, U(bytes, audioFormat + 4));
        Assert.IsTrue(capture.SilenceSamples < (system == "gba" ? 65536 : 44100) / 10,
            "The live audio tap must deliver samples, not pad the entire capture with silence.");
    }
    private static int Find(byte[] bytes, string value, int start = 0)
    {
        int relative = bytes.AsSpan(start).IndexOf(Encoding.ASCII.GetBytes(value)); Assert.IsTrue(relative >= 0, value); return start + relative;
    }
    private static void WriteZip(string path, params (string Name, byte[] Bytes)[] entries)
    {
        using var output = File.Create(path); using var archive = new ZipArchive(output, ZipArchiveMode.Create);
        foreach (var entry in entries) { using var stream = archive.CreateEntry(entry.Name, CompressionLevel.NoCompression).Open(); stream.Write(entry.Bytes); }
    }
}
