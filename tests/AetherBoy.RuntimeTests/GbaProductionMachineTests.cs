using System.Buffers.Binary;
using System.Text;
using AetherBoy.Runtime;
using GameboyAdvanced.Core;
using GameboyAdvanced.Core.Input;
using nanoboy.Core;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class GbaProductionMachineTests
{
    [TestMethod]
    public async Task ProductionSessionRoutesGbaExtensionToAdvanceBackend()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"aetherboy-gba-session-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string romPath = Path.Combine(directory, "synthetic.GBA");
        string savePath = Path.Combine(directory, "synthetic.sav");

        try
        {
            File.WriteAllBytes(romPath, CreateMode3Rom());
            var configuration = new EmulatorConfiguration(
                Frameskip: 0,
                AudioEnabled: false,
                Channel1Enabled: true,
                Channel2Enabled: true,
                Channel3Enabled: true,
                Channel4Enabled: true,
                SampleRate: 44_100);
            await using var session = new EmulationSession(
                romPath,
                savePath,
                bootRom: null,
                configuration);

            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (session.LatestSnapshot.EmulatedFrameCount < 1 &&
                session.State != SessionState.Faulted &&
                DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }

            Assert.IsNull(session.Fault);
            Assert.AreEqual(SessionState.Running, session.State);
            Assert.IsTrue(session.LatestSnapshot.Rom?.IsGameBoyAdvance);
            Assert.AreEqual(VideoGeometry.GameBoyAdvance, session.LatestSnapshot.VideoGeometry);
            Assert.IsTrue(session.LatestSnapshot.HasVideoFrame);
            await session.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ExecutesArmRomAndPublishesGbaFrameThroughAetherBoyAdapter()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"aetherboy-gba-adapter-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string romPath = Path.Combine(directory, "synthetic.gba");
        string savePath = Path.Combine(directory, "synthetic.sav");

        try
        {
            File.WriteAllBytes(romPath, CreateMode3Rom());
            var configuration = new EmulatorConfiguration(
                Frameskip: 0,
                AudioEnabled: false,
                Channel1Enabled: true,
                Channel2Enabled: true,
                Channel3Enabled: true,
                Channel4Enabled: true,
                SampleRate: 44_100);

            using var machine = new GbaProductionMachine(romPath, savePath, configuration);
            Assert.AreEqual(VideoGeometry.GameBoyAdvance, machine.VideoGeometry);

            machine.RunFrame();
            var frame = new int[VideoGeometry.GameBoyAdvance.PixelCount];
            long sequence = 0;
            Assert.IsTrue(machine.TryCopyVideoFrame(frame, ref sequence));
            Assert.IsGreaterThan(0L, sequence);
            object device = typeof(GbaProductionMachine)
                .GetField("device", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(machine)!;
            ushort dispcnt = (ushort)device.GetType().GetMethod("InspectHalfWord")!
                .Invoke(device, new object[] { 0x04000000u })!;
            ushort firstVram = (ushort)device.GetType().GetMethod("InspectHalfWord")!
                .Invoke(device, new object[] { 0x060001E0u })!;
            Assert.AreEqual((ushort)0x0403, dispcnt);
            Assert.AreEqual((ushort)0x001F, firstVram);
            Assert.IsTrue(
                frame.Any(pixel => (pixel & 0x00FF0000) != 0),
                $"Expected red output; distinct pixels: {string.Join(", ", frame.Distinct().Take(8).Select(pixel => $"0x{pixel:X8}"))}");

            machine.SetGameBoyAdvanceButtons(
                GameBoyAdvanceButtons.L | GameBoyAdvanceButtons.R);
            ushort pressedKeys = (ushort)device.GetType().GetMethod("InspectHalfWord")!
                .Invoke(device, new object[] { 0x04000130u })!;
            Assert.AreEqual(0, pressedKeys & 0x0300);
            machine.SetGameBoyAdvanceButtons(GameBoyAdvanceButtons.None);
            ushort releasedKeys = (ushort)device.GetType().GetMethod("InspectHalfWord")!
                .Invoke(device, new object[] { 0x04000130u })!;
            Assert.AreEqual(0x0300, releasedKeys & 0x0300);

            EmulationSnapshot snapshot = machine.CaptureSnapshot(
                SessionState.Running,
                isPaused: false,
                isTurboEnabled: false,
                emulatedFrameCount: 1,
                videoFrameSequence: sequence);
            Assert.IsNotNull(snapshot.Rom);
            Assert.IsTrue(snapshot.Rom.IsGameBoyAdvance);
            Assert.AreEqual("AETHER TEST", snapshot.Rom.Title);
            Assert.AreEqual(240, snapshot.VideoGeometry.Width);
            Assert.AreEqual(160, snapshot.VideoGeometry.Height);
            Assert.IsTrue(snapshot.Supports(EmulationFeature.SaveStates));
            Assert.IsTrue(snapshot.Supports(EmulationFeature.Rewind));
            Assert.IsTrue(snapshot.Supports(EmulationFeature.Frameskip));
            Assert.IsTrue(snapshot.Supports(EmulationFeature.ShoulderButtons));
            Assert.IsTrue(snapshot.Supports(EmulationFeature.AudioChannelControls));
            Assert.IsTrue(snapshot.Supports(EmulationFeature.AudioInspector));
            Assert.IsNotNull(snapshot.Audio);
            Assert.AreEqual(65_536, snapshot.Audio.SampleRate);
            Assert.IsTrue(snapshot.Supports(EmulationFeature.Cheats));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void SaveStateRestoresAndReplaysDeterministically()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"aetherboy-gba-state-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string romPath = Path.Combine(directory, "synthetic.gba");
        string savePath = Path.Combine(directory, "synthetic.sav");

        try
        {
            File.WriteAllBytes(romPath, CreateMode3Rom());
            using var machine = new GbaProductionMachine(
                romPath,
                savePath,
                CreateConfiguration());

            machine.RunFrame();
            machine.SetButtons(GameBoyButtons.A | GameBoyButtons.Right);
            machine.SetGameBoyAdvanceButtons(GameBoyAdvanceButtons.L);
            byte[] checkpoint = machine.CaptureState();

            machine.RunFrame();
            byte[] expected = machine.CaptureState();
            machine.RestoreState(checkpoint);
            machine.RunFrame();
            byte[] actual = machine.CaptureState();

            CollectionAssert.AreEqual(expected, actual);
            Device device = GetDevice(machine);
            Assert.IsTrue(device.IsKeyPressed(Key.A));
            Assert.IsTrue(device.IsKeyPressed(Key.Right));
            Assert.IsTrue(device.IsKeyPressed(Key.L));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void SaveStateRejectsCorruptionAndAnotherRom()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"aetherboy-gba-state-contract-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string firstRomPath = Path.Combine(directory, "first.gba");
        string secondRomPath = Path.Combine(directory, "second.gba");

        try
        {
            byte[] firstRom = CreateMode3Rom();
            byte[] secondRom = CreateMode3Rom();
            secondRom[^1] = 0x5A;
            File.WriteAllBytes(firstRomPath, firstRom);
            File.WriteAllBytes(secondRomPath, secondRom);

            using var first = new GbaProductionMachine(
                firstRomPath,
                Path.Combine(directory, "first.sav"),
                CreateConfiguration());
            using var second = new GbaProductionMachine(
                secondRomPath,
                Path.Combine(directory, "second.sav"),
                CreateConfiguration());
            byte[] state = first.CaptureState();
            byte[] corrupted = (byte[])state.Clone();
            corrupted[corrupted.Length / 2] ^= 0x80;

            Assert.ThrowsExactly<InvalidDataException>(() => first.RestoreState(corrupted));
            Assert.ThrowsExactly<InvalidDataException>(() => second.RestoreState(state));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void RewindReturnsToAnEarlierGbaMachineCycle()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"aetherboy-gba-rewind-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string romPath = Path.Combine(directory, "synthetic.gba");

        try
        {
            File.WriteAllBytes(romPath, CreateMode3Rom());
            using var machine = new GbaProductionMachine(
                romPath,
                Path.Combine(directory, "synthetic.sav"),
                CreateConfiguration());
            for (int frame = 0; frame < 9; frame++)
                machine.RunFrame();

            long beforeRewind = GetDevice(machine).Cpu.Cycles;
            Assert.IsTrue(machine.Rewind());
            long afterRewind = GetDevice(machine).Cpu.Cycles;

            Assert.IsLessThan(beforeRewind, afterRewind);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void FrameskipSkipsPresentationButContinuesGbaExecution()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"aetherboy-gba-frameskip-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string romPath = Path.Combine(directory, "synthetic.gba");

        try
        {
            File.WriteAllBytes(romPath, CreateMode3Rom());
            EmulatorConfiguration configuration = CreateConfiguration() with { Frameskip = 1 };
            using var machine = new GbaProductionMachine(
                romPath,
                Path.Combine(directory, "synthetic.sav"),
                configuration);
            var frame = new int[VideoGeometry.GameBoyAdvance.PixelCount];
            long sequence = 0;

            machine.RunFrame();
            long firstCycle = GetDevice(machine).Cpu.Cycles;
            Assert.IsFalse(machine.TryCopyVideoFrame(frame, ref sequence));
            machine.RunFrame();

            Assert.IsGreaterThan(firstCycle, GetDevice(machine).Cpu.Cycles);
            Assert.IsTrue(machine.TryCopyVideoFrame(frame, ref sequence));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void GbaBiosMustHaveHardwareSize()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"aetherboy-gba-bios-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string romPath = Path.Combine(directory, "synthetic.gba");

        try
        {
            File.WriteAllBytes(romPath, CreateMode3Rom());
            Assert.ThrowsExactly<InvalidDataException>(() => new GbaProductionMachine(
                romPath,
                Path.Combine(directory, "synthetic.sav"),
                CreateConfiguration(),
                new byte[16_383]));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void GbaSnapshotReportsRtcAndActiveBiosMode()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"aetherboy-gba-rtc-label-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string romPath = Path.Combine(directory, "synthetic.gba");

        try
        {
            byte[] rom = CreateMode3Rom();
            Encoding.ASCII.GetBytes("SIIRTC_V001").CopyTo(rom, 0x100);
            File.WriteAllBytes(romPath, rom);
            using var machine = new GbaProductionMachine(
                romPath,
                Path.Combine(directory, "synthetic.sav"),
                CreateConfiguration());

            EmulationSnapshot snapshot = machine.CaptureSnapshot(
                SessionState.Running, false, false, 0, 0);

            Assert.IsNotNull(snapshot.Rom);
            StringAssert.Contains(snapshot.Rom.CartridgeType, "+ RTC");
            StringAssert.Contains(snapshot.Rom.CartridgeType, "HLE BIOS");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void GbaRamCheatsPatchEightSixteenAndThirtyTwoBitWorkRam()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"aetherboy-gba-cheats-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string romPath = Path.Combine(directory, "synthetic.gba");

        try
        {
            File.WriteAllBytes(romPath, CreateMode3Rom());
            using var machine = new GbaProductionMachine(
                romPath,
                Path.Combine(directory, "synthetic.sav"),
                CreateConfiguration());
            CheatSnapshot byteCheat = machine.AddCheat("byte", "02000000:5A");
            CheatSnapshot halfCheat = machine.AddCheat("half", "02000002:BEEF");
            CheatSnapshot wordCheat = machine.AddCheat("word", "03000004:12345678");

            machine.RunFrame();
            Device device = GetDevice(machine);
            Assert.AreEqual(0x5A, device.InspectByte(0x0200_0000));
            Assert.AreEqual(0xBEEF, device.InspectHalfWord(0x0200_0002));
            Assert.AreEqual(0x1234_5678u, device.InspectWord(0x0300_0004));

            Assert.IsFalse(machine.ToggleCheat(byteCheat.Id));
            device.PokeByte(0x0200_0000, 0);
            machine.RunFrame();
            Assert.AreEqual(0, device.InspectByte(0x0200_0000));
            Assert.IsTrue(machine.RemoveCheat(halfCheat.Id));
            Assert.IsFalse(machine.RemoveCheat(Guid.NewGuid()));

            EmulationSnapshot snapshot = machine.CaptureSnapshot(
                SessionState.Running, false, false, 2, 0);
            Assert.HasCount(2, snapshot.Cheats);
            Assert.IsTrue(snapshot.Cheats.Any(cheat => cheat.Id == wordCheat.Id));
            Assert.IsFalse(snapshot.Cheats.Single(cheat => cheat.Id == byteCheat.Id).Enabled);
            Assert.ThrowsExactly<FormatException>(
                () => machine.AddCheat("unsafe", "08000000:FF"));
            Assert.ThrowsExactly<FormatException>(
                () => machine.AddCheat("unaligned", "02000001:FFFF"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void GbaCodeBreakerSupportsDirectLogicalAndConditionalRamCodes()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"aetherboy-gba-cb-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string romPath = Path.Combine(directory, "synthetic.gba");
        try
        {
            File.WriteAllBytes(romPath, CreateMode3Rom());
            using var machine = new GbaProductionMachine(
                romPath, Path.Combine(directory, "synthetic.sav"), CreateConfiguration());
            Device device = GetDevice(machine);
            device.PokeHalfWord(0x0200_0000, 0x00AA);
            machine.AddCheat("direct", "82000004 BEEF");
            machine.AddCheat("logical", "22000004 0010");
            machine.AddCheat("conditional", "72000000 00AA + 32000006 0055");

            machine.RunFrame();

            Assert.AreEqual(0xBEFF, device.InspectHalfWord(0x0200_0004));
            Assert.AreEqual(0x55, device.InspectByte(0x0200_0006));
            CollectionAssert.Contains(
                machine.CaptureSnapshot(SessionState.Running, false, false, 1, 0)
                    .Cheats.Select(cheat => cheat.Code).ToArray(),
                "CB:72000000 00AA + CB:32000006 0055");

            device.PokeHalfWord(0x0200_0000, 0);
            device.PokeByte(0x0200_0006, 0);
            machine.RunFrame();
            Assert.AreEqual(0, device.InspectByte(0x0200_0006));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void GbaGameSharkSupportsDecodedAndEncryptedVersionOneWrites()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"aetherboy-gba-gs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string romPath = Path.Combine(directory, "synthetic.gba");
        try
        {
            File.WriteAllBytes(romPath, CreateMode3Rom());
            using var machine = new GbaProductionMachine(
                romPath, Path.Combine(directory, "synthetic.sav"), CreateConfiguration());
            machine.AddCheat("decoded", "GSRAW:02000008 0000007A");
            (uint op1, uint op2) = EncryptGameShark(0x1200_000A, 0x0000_CAFE);
            machine.AddCheat("encrypted", $"GS:{op1:X8} {op2:X8}");

            machine.RunFrame();

            Device device = GetDevice(machine);
            Assert.AreEqual(0x7A, device.InspectByte(0x0200_0008));
            Assert.AreEqual(0xCAFE, device.InspectHalfWord(0x0200_000A));
            Assert.ThrowsExactly<FormatException>(
                () => machine.AddCheat("rom", "GSRAW:08000000 00000001"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void EepromSaveExpandsToEightKiBAndReloadsDetectedCapacity()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"aetherboy-gba-eeprom-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string romPath = Path.Combine(directory, "synthetic.gba");
        string savePath = Path.Combine(directory, "synthetic.sav");

        try
        {
            byte[] rom = CreateMode3Rom();
            Encoding.ASCII.GetBytes("EEPROM_V124").CopyTo(rom, 0xD0);
            File.WriteAllBytes(romPath, rom);

            using (var machine = new GbaProductionMachine(
                romPath, savePath, CreateConfiguration()))
            {
                Device device = GetDevice(machine);
                device.Gamepak._eepromBackup!.SetSize(
                    GameboyAdvanced.Core.Rom.EEPromBackup.EEPromSize.Small4Kb);
                byte[] smallProtocolState = machine.CaptureState();
                device.Gamepak._eepromBackup!.SetSize(
                    GameboyAdvanced.Core.Rom.EEPromBackup.EEPromSize.Large64Kb);
                device.Gamepak._eepromBackup.Data[^1] = 0x7A;
                machine.RunFrame();

                EmulationSnapshot snapshot = machine.CaptureSnapshot(
                    SessionState.Running, false, false, 1, 0);
                Assert.AreEqual(8 * 1024, snapshot.Rom!.RamSize);
                Assert.AreEqual(8 * 1024, snapshot.Rom.BatterySave!.ExpectedLength);

                machine.RestoreState(smallProtocolState);
                snapshot = machine.CaptureSnapshot(
                    SessionState.Running, false, false, 1, 0);
                Assert.AreEqual(8 * 1024, snapshot.Rom!.RamSize);
                device.Gamepak._eepromBackup!.SetSize(
                    GameboyAdvanced.Core.Rom.EEPromBackup.EEPromSize.Large64Kb);
                device.Gamepak._eepromBackup.Data[^1] = 0x7A;
            }

            Assert.AreEqual(8 * 1024, new FileInfo(savePath).Length);
            using var reloaded = new GbaProductionMachine(
                romPath, savePath, CreateConfiguration());
            Assert.AreEqual(
                GameboyAdvanced.Core.Rom.EEPromBackup.EEPromSize.Large64Kb,
                GetDevice(reloaded).Gamepak._eepromBackup!.Size);
            Assert.AreEqual(0x7A, GetDevice(reloaded).Gamepak._eepromBackup!.Data[^1]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static EmulatorConfiguration CreateConfiguration() => new(
        Frameskip: 0,
        AudioEnabled: false,
        Channel1Enabled: true,
        Channel2Enabled: true,
        Channel3Enabled: true,
        Channel4Enabled: true,
        SampleRate: 44_100);

    private static Device GetDevice(GbaProductionMachine machine) =>
        (Device)typeof(GbaProductionMachine)
            .GetField("device", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(machine)!;

    private static (uint Op1, uint Op2) EncryptGameShark(uint op1, uint op2)
    {
        uint sum = 0;
        const uint delta = 0x9E37_79B9;
        ReadOnlySpan<uint> seed = [0x09F4_FBBD, 0x9681_884A, 0x3520_27E9, 0xF3DE_E5A7];
        unchecked
        {
            for (int round = 0; round < 32; round++)
            {
                sum += delta;
                op1 += ((op2 << 4) + seed[0]) ^ (op2 + sum) ^ ((op2 >> 5) + seed[1]);
                op2 += ((op1 << 4) + seed[2]) ^ (op1 + sum) ^ ((op1 >> 5) + seed[3]);
            }
        }
        return (op1, op2);
    }

    private static byte[] CreateMode3Rom()
    {
        byte[] rom = new byte[0x200];
        uint[] program =
        {
            0xE59F0018, // LDR r0, [pc, #0x18] -> DISPCNT
            0xE59F1018, // LDR r1, [pc, #0x18] -> Mode 3 + BG2
            0xE5801000, // STR r1, [r0]
            0xE59F0014, // LDR r0, [pc, #0x14] -> VRAM
            0xE59F1014, // LDR r1, [pc, #0x14] -> two red pixels
            0xE5801000, // STR r1, [r0]
            0xE2800004, // ADD r0, r0, #4
            0xEAFFFFFC, // B back to STR
            0x04000000,
            0x00000403,
            0x060001E0, // First two pixels of the second scanline.
            0x001F001F
        };
        for (int index = 0; index < program.Length; index++)
            BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(index * 4), program[index]);

        Encoding.ASCII.GetBytes("AETHER TEST").CopyTo(rom, 0xA0);
        Encoding.ASCII.GetBytes("ABCE00").CopyTo(rom, 0xAC);
        rom[0xB2] = 0x96;
        rom[0xBC] = 1;
        Encoding.ASCII.GetBytes("SRAM_V113").CopyTo(rom, 0xC0);
        return rom;
    }
}
