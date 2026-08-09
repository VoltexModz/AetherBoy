using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class SaveStateRoundTripTests
{
    [TestMethod]
    public void CaptureRestoreCapture_IsByteIdenticalAcrossEveryRequiredComponent()
    {
        using var fixture = new EmulatorFixture(romMarker: 0x11);
        MutateState(fixture.Emulator);
        byte[] expected = SaveState.Capture(fixture.Emulator);

        fixture.Emulator.Frame();
        fixture.Emulator.Memory.WriteByte(0xC123, 0x00);
        fixture.Emulator.Memory.Video.WriteVRAMDirect(1, 0x321, 0x00);
        fixture.Emulator.SetButtons(GameBoyButtons.None);

        SaveState.Restore(fixture.Emulator, expected);
        byte[] actual = SaveState.Capture(fixture.Emulator);

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void RestoredState_ProducesTheSameDeterministicFuture()
    {
        using var fixture = new EmulatorFixture(romMarker: 0x22);
        MutateState(fixture.Emulator);
        byte[] origin = SaveState.Capture(fixture.Emulator);

        RunFrames(fixture.Emulator, 3);
        byte[] expectedFuture = SaveState.Capture(fixture.Emulator);

        SaveState.Restore(fixture.Emulator, origin);
        RunFrames(fixture.Emulator, 3);
        byte[] replayedFuture = SaveState.Capture(fixture.Emulator);

        CollectionAssert.AreEqual(expectedFuture, replayedFuture);
    }

    [TestMethod]
    public void LongRunReplay_RemainsByteIdenticalAfterThreeHundredFrames()
    {
        using var fixture = new EmulatorFixture(romMarker: 0x23);
        MutateState(fixture.Emulator);
        byte[] origin = SaveState.Capture(fixture.Emulator);

        RunFrames(fixture.Emulator, 300);
        byte[] expectedFuture = SaveState.Capture(fixture.Emulator);

        SaveState.Restore(fixture.Emulator, origin);
        RunFrames(fixture.Emulator, 300);

        CollectionAssert.AreEqual(expectedFuture, SaveState.Capture(fixture.Emulator));
    }

    [TestMethod]
    public void DifferentRom_IsRejectedWithoutMutatingTheTarget()
    {
        using var source = new EmulatorFixture(romMarker: 0x31);
        using var target = new EmulatorFixture(romMarker: 0x32);
        MutateState(source.Emulator);
        MutateState(target.Emulator);
        byte[] foreignState = SaveState.Capture(source.Emulator);
        byte[] targetBefore = SaveState.Capture(target.Emulator);

        Assert.Throws<InvalidDataException>(() => SaveState.Restore(target.Emulator, foreignState));

        CollectionAssert.AreEqual(targetBefore, SaveState.Capture(target.Emulator));
    }

    [TestMethod]
    public void InvalidComponent_IsRejectedBeforeAnyComponentIsApplied()
    {
        using var fixture = new EmulatorFixture(romMarker: 0x41);
        MutateState(fixture.Emulator);
        byte[] before = SaveState.Capture(fixture.Emulator);
        EmulatorStateDocument document = EmulatorStateCodec.Deserialize(before);
        var sections = new List<EmulatorStateSection>();

        foreach (EmulatorStateSection section in document.Sections) {
            byte[] payload = section.CopyPayload();
            if (section.Id == (ushort)CoreStateSection.Video) {
                BitConverter.GetBytes(61).CopyTo(payload, 2);
            }
            sections.Add(new EmulatorStateSection(
                section.Id,
                section.SchemaVersion,
                section.Required,
                payload));
        }

        byte[] semanticallyInvalid = EmulatorStateCodec.Serialize(
            new EmulatorStateDocument(document.RomSha256, document.HardwareModel, sections));
        Assert.Throws<InvalidDataException>(
            () => SaveState.Restore(fixture.Emulator, semanticallyInvalid));

        CollectionAssert.AreEqual(before, SaveState.Capture(fixture.Emulator));
    }

    [TestMethod]
    public void InvalidMapperRegisters_AreRejectedBeforeAnyComponentIsApplied()
    {
        using var fixture = new EmulatorFixture(romMarker: 0x42);
        MutateState(fixture.Emulator);
        byte[] before = SaveState.Capture(fixture.Emulator);
        EmulatorStateDocument document = EmulatorStateCodec.Deserialize(before);
        var sections = new List<EmulatorStateSection>();

        foreach (EmulatorStateSection section in document.Sections) {
            byte[] payload = section.CopyPayload();
            if (section.Id == (ushort)CoreStateSection.Cartridge) {
                payload[12] = 2;
            }
            sections.Add(new EmulatorStateSection(
                section.Id,
                section.SchemaVersion,
                section.Required,
                payload));
        }

        byte[] semanticallyInvalid = EmulatorStateCodec.Serialize(
            new EmulatorStateDocument(document.RomSha256, document.HardwareModel, sections));
        Assert.Throws<InvalidDataException>(
            () => SaveState.Restore(fixture.Emulator, semanticallyInvalid));

        CollectionAssert.AreEqual(before, SaveState.Capture(fixture.Emulator));
    }

    [TestMethod]
    public void RewindManager_ReturnsToEarlierCapturedTimelines()
    {
        using var fixture = new EmulatorFixture(romMarker: 0x51);
        var rewind = new RewindManager();
        rewind.Initialize(fixture.Emulator);
        byte[] initial = SaveState.Capture(fixture.Emulator);

        RunFrames(fixture.Emulator, 4, rewind);
        byte[] frameFour = SaveState.Capture(fixture.Emulator);
        RunFrames(fixture.Emulator, 4, rewind);

        Assert.IsTrue(rewind.Rewind(fixture.Emulator));
        CollectionAssert.AreEqual(frameFour, SaveState.Capture(fixture.Emulator));
        Assert.IsTrue(rewind.Rewind(fixture.Emulator));
        CollectionAssert.AreEqual(initial, SaveState.Capture(fixture.Emulator));
        Assert.IsFalse(rewind.Rewind(fixture.Emulator));
    }

    [TestMethod]
    public void RewindManager_KeepsItsHistoryStrictlyBounded()
    {
        using var fixture = new EmulatorFixture(romMarker: 0x52);
        var rewind = new RewindManager();
        rewind.Initialize(fixture.Emulator);

        for (int frame = 0; frame < 4 * 151; frame++) {
            rewind.CaptureFrame(fixture.Emulator);
        }

        Assert.AreEqual(150, rewind.HistoryCount);
        rewind.Clear();
        Assert.AreEqual(0, rewind.HistoryCount);
        Assert.AreEqual(0L, rewind.StoredByteCount);
    }

    [TestMethod]
    public void RewindManager_CompressesItsRetainedSnapshots()
    {
        using var fixture = new EmulatorFixture(romMarker: 0x53);
        byte[] rawState = SaveState.Capture(fixture.Emulator);
        var rewind = new RewindManager();

        rewind.Initialize(fixture.Emulator);
        RunFrames(fixture.Emulator, 4, rewind);

        Assert.AreEqual(2, rewind.HistoryCount);
        Assert.IsLessThan(rawState.LongLength * rewind.HistoryCount, rewind.StoredByteCount,
            "Rewind history should retain compressed rather than full raw states.");
    }

    [TestMethod]
    public void FileRoundTrip_UsesTheSameVerifiedStateDocument()
    {
        using var fixture = new EmulatorFixture(romMarker: 0x61);
        MutateState(fixture.Emulator);
        byte[] expected = SaveState.Capture(fixture.Emulator);
        string statePath = Path.Combine(fixture.DirectoryPath, "slot.ss1");

        SaveState.SaveToFile(fixture.Emulator, statePath);
        fixture.Emulator.Frame();
        SaveState.LoadFromFile(fixture.Emulator, statePath);

        CollectionAssert.AreEqual(expected, SaveState.Capture(fixture.Emulator));
        Assert.IsFalse(File.Exists(statePath + ".tmp"));
    }

    private static void MutateState(Nanoboy emulator)
    {
        RunFrames(emulator, 1);
        Memory memory = emulator.Memory;
        memory.WriteByte(0xC123, 0xA5);
        memory.WriteByte(0xD234, 0x5A);
        memory.WriteByte(0xFF80, 0xC3);
        memory.WriteByte(0x0000, 0x0A);
        memory.WriteByte(0x4000, 2);
        memory.WriteByte(0xA123, 0x77);
        memory.WriteByte(0xFF00, 0x10);
        emulator.SetButtons(GameBoyButtons.A | GameBoyButtons.Left);
        memory.WriteByte(0xFF05, 0xFE);
        memory.WriteByte(0xFF06, 0x93);
        memory.WriteByte(0xFF07, 0x05);
        for (int tick = 0; tick < 20; tick++) {
            memory.Timer.Tick();
        }
        memory.Video.WriteVRAMDirect(1, 0x321, 0x9B);
        memory.Video.WriteOAMDirect(0x52, 0x6D);
        memory.WriteByte(0xFF68, 0xBF);
        memory.WriteByte(0xFF69, 0x1F);
        memory.WriteByte(0xFF6A, 0xBF);
        memory.WriteByte(0xFF6B, 0x7C);
        memory.WriteByte(0xFF21, 0xD5);
        memory.WriteByte(0xFF22, 0x6B);
        memory.WriteByte(0xFF23, 0xC0);
        memory.WriteByte(0xFF02, 0x01);
        memory.WriteByte(0xFF51, 0xC0);
        memory.WriteByte(0xFF52, 0x00);
        memory.WriteByte(0xFF53, 0x10);
        memory.WriteByte(0xFF54, 0x00);
        memory.WriteByte(0xFF55, 0x82);
        memory.WriteByte(0xFF46, 0xC0);
    }

    private static void RunFrames(Nanoboy emulator, int count, RewindManager? rewind = null)
    {
        for (int frame = 0; frame < count; frame++) {
            emulator.Frame();
            rewind?.CaptureFrame(emulator);
        }
    }

    private sealed class EmulatorFixture : IDisposable
    {
        public EmulatorFixture(byte romMarker)
        {
            DirectoryPath = Path.Combine(
                Path.GetTempPath(),
                "aetherboy-state-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
            string romPath = Path.Combine(DirectoryPath, "state.gbc");
            byte[] rom = new byte[4 * 0x4000];
            rom[0x134] = romMarker;
            rom[0x143] = 0x80;
            rom[0x147] = (byte)Mbc.ROM_MBC5_RAM;
            rom[0x148] = 0x01;
            rom[0x149] = 0x03;
            rom[0x100] = 0xC3;
            rom[0x101] = 0x50;
            rom[0x102] = 0x01;
            rom[0x150] = 0x00;
            rom[0x151] = 0x18;
            rom[0x152] = 0xFD;
            File.WriteAllBytes(romPath, rom);
            Emulator = new Nanoboy(new ROM(romPath, Path.Combine(DirectoryPath, "state.sav")));
        }

        public string DirectoryPath { get; }
        public Nanoboy Emulator { get; }

        public void Dispose()
        {
            Emulator.Dispose();
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
