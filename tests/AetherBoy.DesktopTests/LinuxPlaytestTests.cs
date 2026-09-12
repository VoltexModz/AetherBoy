using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using AetherBoy.Runtime;
using SDL3;

namespace AetherBoy.Desktop.Tests;

/// <summary>Opt-in synthetic native playtest. No downloaded ROMs, personal data, or screenshots.</summary>
[TestClass]
[DoNotParallelize]
public sealed class LinuxPlaytestTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void SyntheticAdvanceGameReachesNativeVideoAudioAndTimeline()
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_PLAYTEST") != "1")
        {
            Assert.Inconclusive("Set AETHERBOY_PLAYTEST=1 in a native Wayland session with an audio device.");
            return;
        }
        string directory = Path.Combine(Path.GetTempPath(), "aetherboy-gba-playtest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string settings = Path.Combine(directory, "settings.json");
        string rom = Path.Combine(directory, "synthetic.gba");
        byte[] bytes = new byte[0x200];
        uint[] program =
        [
            0xE59F0018, 0xE59F1018, 0xE5801000, // set Mode 3 + BG2
            0xE59F0014, 0xE59F1014, 0xE5801000, // two red pixels on second scanline
            0xEAFFFFFE, 0xEAFFFFFE,
            0x04000000, 0x00000403, 0x060001E0, 0x001F001F
        ];
        for (int i = 0; i < program.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), program[i]);
        "AETHER GBA"u8.CopyTo(bytes.AsSpan(0xA0));
        "ABCE00"u8.CopyTo(bytes.AsSpan(0xAC));
        bytes[0xB2] = 0x96;
        "SRAM_V113"u8.CopyTo(bytes.AsSpan(0xC0));
        File.WriteAllBytes(rom, bytes);
        LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = true, AudioVolume = 0 });
        SDL.SetHint("SDL_VIDEO_DRIVER", "wayland");
        try
        {
            Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events | SDL.InitFlags.Gamepad), SDL.GetError());
            using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true);
            VerifyInputFocusSwitch(host);
            VerifySearchCloses(host);
            VerifyFirmwareImport(host, directory, settings);
            var session = Load(host, rom);
            long audioSamples = 0;
            session.AudioSamplesAvailable += (_, e) => Interlocked.Add(ref audioSamples, e.SampleCount);
            int seconds = int.TryParse(Environment.GetEnvironmentVariable("AETHERBOY_GBA_PLAYTEST_SECONDS"), out int duration)
                ? Math.Clamp(duration, 10, 3600) : 10;
            var timer = Stopwatch.StartNew();
            long startFrame = session.LatestSnapshot.EmulatedFrameCount;
            long lastFrame = startFrame;
            int nextSample = 1;
            while (timer.Elapsed.TotalSeconds < seconds)
            {
                Call(host, "UpdateEmulation");
                SDL.PumpEvents();
                Assert.IsNull(session.Fault);
                Assert.IsNull(Field<string?>(host, "audioError"));
                if (timer.Elapsed.TotalSeconds >= nextSample)
                {
                    long frame = session.LatestSnapshot.EmulatedFrameCount;
                    Assert.IsTrue(frame > lastFrame, "GBA emulation stalled for one second.");
                    lastFrame = frame;
                    nextSample++;
                }
                Thread.Sleep(10);
            }
            Assert.IsTrue(session.LatestSnapshot.Rom?.IsGameBoyAdvance);
            Assert.AreEqual(VideoGeometry.GameBoyAdvance, session.LatestSnapshot.VideoGeometry);
            Assert.IsTrue(Field<int[]>(host, "framePixels").Any(pixel => (pixel & 0x00FF0000) != 0));
            Assert.IsTrue(Interlocked.Read(ref audioSamples) > seconds * 20_000L);
            Assert.AreEqual(2, Field<SdlAudioOutput>(host, "audioOutput").Channels,
                "Native GBA output must retain both interleaved source channels.");
            double fps = (session.LatestSnapshot.EmulatedFrameCount - startFrame) / timer.Elapsed.TotalSeconds;
            Assert.IsTrue(fps is > 45 and < 70, $"GBA observed {fps:F2} fps.");
            TestContext.WriteLine($"GBA: {timer.Elapsed.TotalSeconds:F2}s, {fps:F2} fps, audio " +
                $"{Field<SdlAudioOutput>(host, "audioOutput").DriverName}, {audioSamples} samples.");
            session.SetGameBoyAdvanceButtonsAsync(GameBoyAdvanceButtons.L | GameBoyAdvanceButtons.R)
                .GetAwaiter().GetResult();
            session.SetGameBoyAdvanceButtonsAsync(GameBoyAdvanceButtons.None).GetAwaiter().GetResult();
            Call(host, "TogglePause");
            Call(host, "QuickSave");
            Assert.IsNull(Field<string?>(host, "loadError"));
            Call(host, "QuickLoad");
            Assert.IsNull(Field<string?>(host, "loadError"));
            session.SetPausedAsync(false).GetAwaiter().GetResult();
            Thread.Sleep(1500);
            Assert.IsTrue(session.RewindAsync().GetAwaiter().GetResult());
            Call(host, "CloseSession");
        }
        finally
        {
            SDL.Quit();
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SyntheticBatteryGameSurvivesNativeAudioControlsAndRestart(bool color)
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_PLAYTEST") != "1")
        {
            Assert.Inconclusive("Set AETHERBOY_PLAYTEST=1 in a native Wayland session with an audio device.");
            return;
        }

        int seconds = int.TryParse(Environment.GetEnvironmentVariable("AETHERBOY_PLAYTEST_SECONDS"), out int duration)
            ? Math.Clamp(duration, 10, 3600) : 10;
        string directory = Path.Combine(Path.GetTempPath(), "aetherboy-playtest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string[] variables = ["XDG_DATA_HOME", "XDG_CONFIG_HOME", "XDG_STATE_HOME", "XDG_CACHE_HOME"];
        string?[] originals = variables.Select(Environment.GetEnvironmentVariable).ToArray();
        for (int i = 0; i < variables.Length; i++)
            Environment.SetEnvironmentVariable(variables[i], Path.Combine(directory, variables[i]));
        string settings = Path.Combine(directory, "settings.json");
        string rom = Path.Combine(directory, color ? "synthetic.gbc" : "synthetic.gb");
        File.WriteAllBytes(rom, MakeBatteryRom(color));
        // Muted output still exercises the actual stream and its sample submissions.
        LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = true, AudioVolume = 0 });
        SDL.SetHint("SDL_VIDEO_DRIVER", "wayland");
        try
        {
            Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events | SDL.InitFlags.Gamepad), SDL.GetError());
            using (var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true))
            {
                var session = Load(host, rom);
                string movedRom = Path.Combine(directory, color ? "relocated.gbc" : "relocated.gb");
                File.Move(rom, movedRom);
                Call(host, "TryLoadRom", movedRom);
                Assert.IsTrue(SpinWait.SpinUntil(() => { Call(host, "CompletePendingLoad"); return Field<Task?>(host, "romPreparation") is null; }, TimeSpan.FromSeconds(10)));
                Assert.AreSame(session, Field<EmulationSession>(host, "session"), "Relocating identical content must keep the running session.");
                Assert.AreEqual(movedRom, Field<LinuxLibrary>(host, "library").Read().Single().Path,
                    "Opening relocated content must repair the cartridge library path.");
                rom = movedRom;
                long audioSamples = 0;
                long nonzeroBatches = 0;
                session.AudioSamplesAvailable += (_, e) =>
                {
                    Interlocked.Add(ref audioSamples, e.SampleCount);
                    if (e.GetSamplesCopy().Any(sample => sample != 0)) Interlocked.Increment(ref nonzeroBatches);
                };
                Assert.IsNotNull(Field<SdlAudioOutput?>(host, "audioOutput"), Field<string?>(host, "audioError"));
                var audio = Field<SdlAudioOutput>(host, "audioOutput");
                long startFrame = session.LatestSnapshot.EmulatedFrameCount;
                var timer = Stopwatch.StartNew();
                var process = Process.GetCurrentProcess();
                TimeSpan cpuStart = process.TotalProcessorTime;
                long lastFrame = startFrame;
                int sampledWindows = 0;
                int nextSample = 1;
                while (timer.Elapsed.TotalSeconds < seconds)
                {
                    Call(host, "UpdateEmulation");
                    SDL.PumpEvents();
                    Assert.IsNull(session.Fault);
                    Assert.IsNull(Field<string?>(host, "audioError"));
                    if (timer.Elapsed.TotalSeconds >= nextSample)
                    {
                        long frame = session.LatestSnapshot.EmulatedFrameCount;
                        Assert.IsTrue(frame > lastFrame, "Emulation stalled for one second.");
                        lastFrame = frame;
                        sampledWindows++;
                        nextSample++;
                    }
                    Thread.Sleep(10);
                }
                double fps = (session.LatestSnapshot.EmulatedFrameCount - startFrame) / timer.Elapsed.TotalSeconds;
                Assert.IsTrue(fps is > 45 and < 70, $"Expected real-time GB pacing; observed {fps:F2} fps.");
                Assert.IsTrue(Interlocked.Read(ref audioSamples) > seconds * 20_000L, "Too few audio samples delivered.");
                Assert.IsTrue(Interlocked.Read(ref nonzeroBatches) > 0, "The generated APU tone was never delivered.");
                TestContext.WriteLine($"{(color ? "GBC" : "GB")}: {timer.Elapsed.TotalSeconds:F2}s, {fps:F2} fps, " +
                    $"CPU {(process.TotalProcessorTime - cpuStart).TotalSeconds:F2}s, " +
                    $"audio {audio.DriverName}, {audioSamples} samples, {sampledWindows} advancing one-second windows.");

                Call(host, "TogglePause");
                Assert.IsTrue(session.LatestSnapshot.IsPaused);
                long pausedFrame = session.LatestSnapshot.EmulatedFrameCount;
                Thread.Sleep(100);
                Assert.AreEqual(pausedFrame, session.LatestSnapshot.EmulatedFrameCount);
                Call(host, "QuickSave");
                Assert.IsNull(Field<string?>(host, "loadError"));
                Call(host, "TogglePause");
                session.SetTurboAsync(true).GetAwaiter().GetResult();
                Assert.IsTrue(session.LatestSnapshot.IsTurboEnabled);
                Thread.Sleep(250);
                session.SetTurboAsync(false).GetAwaiter().GetResult();
                Call(host, "QuickLoad");
                Assert.IsNull(Field<string?>(host, "loadError"));
                Assert.IsTrue(Field<string>(host, "statusMessage").Contains("LOADED", StringComparison.OrdinalIgnoreCase));
                session.SetPausedAsync(false).GetAwaiter().GetResult();
                Thread.Sleep(1500);
                Assert.IsTrue(session.RewindAsync().GetAwaiter().GetResult(), "Rewind had no usable history.");
                session.SetPausedAsync(false).GetAwaiter().GetResult();
                var options = Field<LinuxFrontendOptions>(host, "options");
                Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = options.Keys[LinuxInputAction.A] }, true);
                Call(host, "UpdateEmulation");
                long inputFrame = session.LatestSnapshot.EmulatedFrameCount;
                Assert.IsTrue(SpinWait.SpinUntil(() => session.LatestSnapshot.EmulatedFrameCount >= inputFrame + 6,
                    TimeSpan.FromSeconds(5)), "Input verification did not advance.");
                Call(host, "CloseSession");
            }

            string save = Directory.EnumerateFiles(directory, "*.sav", SearchOption.AllDirectories).Single();
            Assert.AreEqual((byte)0x42, File.ReadAllBytes(save)[0], "In-game battery write was not persisted.");
            Assert.AreEqual(0, File.ReadAllBytes(save)[2] & 1, "The ROM did not observe the A key routed through the host.");
            using (var restarted = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true))
            {
                var session = Load(restarted, rom);
                Assert.IsTrue(SpinWait.SpinUntil(() => session.LatestSnapshot.EmulatedFrameCount >= 6,
                    TimeSpan.FromSeconds(10)));
                Call(restarted, "CloseSession");
            }
            Assert.AreEqual((byte)0x42, File.ReadAllBytes(save)[1],
                "Restart did not expose the previous battery value to the generated ROM.");
            Assert.AreEqual(1, File.ReadAllBytes(save)[2] & 1, "The restarted ROM retained a stuck A button.");
            VerifyBatteryRestore(rom, settings, directory, save);
        }
        finally
        {
            SDL.Quit();
            for (int i = 0; i < variables.Length; i++) Environment.SetEnvironmentVariable(variables[i], originals[i]);
            Directory.Delete(directory, recursive: true);
        }
    }

    private static EmulationSession Load(WaylandEmulatorHost host, string rom)
    {
        Call(host, "TryLoadRom", rom);
        Assert.IsTrue(SpinWait.SpinUntil(() =>
        {
            Call(host, "CompletePendingLoad");
            return Field<EmulationSession?>(host, "session") is not null;
        }, TimeSpan.FromSeconds(10)), Field<string?>(host, "loadError"));
        return Field<EmulationSession>(host, "session");
    }

    private static void VerifyInputFocusSwitch(WaylandEmulatorHost host)
    {
        Call(host, "ToggleControlCenter");
        Type pageType = typeof(WaylandEmulatorHost).GetNestedType("ControlCenterPage", BindingFlags.NonPublic)!;
        Call(host, "SelectControlCenterPage", Enum.Parse(pageType, "Input"));
        Call(host, "DrawShell");
        void Key(SDL.Scancode key) => Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = key }, true);
        Key(SDL.Scancode.Tab);
        Assert.IsTrue(Field<int>(host, "focusedControl") >= 0);
        Key(SDL.Scancode.Down);
        Assert.AreEqual(-1, Field<int>(host, "focusedControl"), "Arrow navigation must leave generic button focus.");
        Key(SDL.Scancode.Return);
        Assert.AreEqual(LinuxInputAction.B, Field<LinuxInputAction?>(host, "rebindingAction"),
            "Enter must rebind the arrow-selected action rather than activate stale Tab focus.");
        Key(SDL.Scancode.Escape);
        Assert.IsNull(Field<LinuxInputAction?>(host, "rebindingAction"));
        Call(host, "CloseControlCenter");
    }

    private static void VerifySearchCloses(WaylandEmulatorHost host)
    {
        Call(host, "ToggleControlCenter");
        Type pageType = typeof(WaylandEmulatorHost).GetNestedType("ControlCenterPage", BindingFlags.NonPublic)!;
        Call(host, "SelectControlCenterPage", Enum.Parse(pageType, "Library"));
        Call(host, "DrawShell");
        Call(host, "HandleMouseClick", 560f, 220f);
        Assert.IsTrue(Field<bool>(host, "editingSearch"));
        Call(host, "CloseControlCenter");
        Assert.IsFalse(Field<bool>(host, "editingSearch"), "Hidden library search must not consume game keyboard input.");
        Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = SDL.Scancode.X }, true);
        Assert.IsTrue(Field<HashSet<SDL.Scancode>>(host, "pressedKeys").Contains(SDL.Scancode.X));
        Call(host, "HandleKeyboard", new SDL.KeyboardEvent { Scancode = SDL.Scancode.X }, false);
    }

    private static void VerifyFirmwareImport(WaylandEmulatorHost host, string directory, string settings)
    {
        string source = Path.Combine(directory, "firmware.bin");
        File.WriteAllBytes(source, new byte[123]);
        var error = Assert.ThrowsExactly<TargetInvocationException>(() => Call(host, "ImportFirmware", source));
        Assert.IsTrue(error.GetBaseException() is InvalidDataException);
        byte[] syntheticFirmware = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        File.WriteAllBytes(source, syntheticFirmware);
        Call(host, "ImportFirmware", source);
        string cartridge = Path.Combine(directory, "firmware-selection.gb");
        File.WriteAllBytes(cartridge, MakeBatteryRom(false));
        byte[] selected = (byte[])typeof(WaylandEmulatorHost).GetMethod("FirmwareFor", Private)!.Invoke(host, [cartridge])!;
        CollectionAssert.AreEqual(syntheticFirmware, selected);
        Call(host, "FlushSettingsIfDue", true);
        Assert.IsTrue(LinuxSettingsStore.Load(settings, out _).UseFirmware);
        // This checks import/selection only. The synthetic bytes are not executable BIOS firmware.
    }

    private static void VerifyBatteryRestore(string rom, string settings, string directory, string save)
    {
        using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true);
        _ = Load(host, rom);
        string import = Path.Combine(directory, "import.sav");
        File.WriteAllBytes(import, new byte[1]);
        var error = Assert.ThrowsExactly<TargetInvocationException>(() => Call(host, "SelectBatteryImport", import));
        Assert.IsTrue(error.GetBaseException() is InvalidDataException);
        byte[] replacement = new byte[File.ReadAllBytes(save).Length];
        replacement[0] = 0x66;
        File.WriteAllBytes(import, replacement);
        Call(host, "SelectBatteryImport", import);
        Call(host, "RestoreBattery");
        Assert.IsNull(Field<string?>(host, "loadError"));
        var restored = Field<EmulationSession>(host, "session");
        Assert.IsTrue(SpinWait.SpinUntil(() => restored.LatestSnapshot.EmulatedFrameCount >= 6,
            TimeSpan.FromSeconds(10)), "Restored battery did not restart the cartridge.");
        Call(host, "CloseSession");
        Assert.AreEqual((byte)0x66, File.ReadAllBytes(save)[1], "The restarted ROM did not read the imported battery value.");
        string archivePath = Directory.EnumerateFiles(directory, "saves-*.zip", SearchOption.AllDirectories).Single();
        using var archive = ZipFile.OpenRead(archivePath);
        var oldBattery = archive.GetEntry("battery/game.sav");
        Assert.IsNotNull(oldBattery, "Restore must archive the current battery before replacement.");
        using var previous = oldBattery.Open();
        Assert.AreEqual(0x42, previous.ReadByte());
        Assert.AreEqual(0x42, previous.ReadByte(), "Export did not preserve the pre-restore battery image.");
    }

    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, Private)!.GetValue(instance)!;
    private static void Call(object instance, string method, params object[] args)
    {
        instance.GetType().GetMethod(method, Private)!.Invoke(instance, args);
        FieldInfo? operation = instance.GetType().GetField("stateOperation", Private);
        if (operation?.GetValue(instance) is Task)
        {
            Assert.IsTrue(SpinWait.SpinUntil(() =>
            {
                instance.GetType().GetMethod("CompleteStateOperation", Private)!.Invoke(instance, null);
                return operation.GetValue(instance) is null;
            }, TimeSpan.FromSeconds(10)), "The save-state action did not finish.");
            // Save completion and the subsequent UI metadata refresh are separate asynchronous steps.
            // The next pointer click must see the newly enabled Load action, including on slow runners.
            Assert.IsTrue(SpinWait.SpinUntil(() =>
            {
                instance.GetType().GetMethod("PollDiskRefresh", Private)!.Invoke(instance, null);
                return instance.GetType().GetField("diskRefresh", Private)!.GetValue(instance) is null;
            }, TimeSpan.FromSeconds(10)), "Updated save information did not reach the UI.");
        }
    }

    internal static byte[] MakeBatteryRom(bool color)
    {
        byte[] bytes = new byte[32768];
        bytes[0x100] = 0xC3; bytes[0x101] = 0x50; bytes[0x102] = 0x01;
        "AETHER SOAK"u8.CopyTo(bytes.AsSpan(0x134));
        bytes[0x143] = color ? (byte)0x80 : (byte)0;
        bytes[0x147] = 0x03; // MBC1 + RAM + battery
        bytes[0x149] = 0x02; // 8 KiB external RAM
        byte[] program =
        [
            0xF3, 0x3E, 0x0A, 0xEA, 0x00, 0x00, // enable cartridge RAM
            0xFA, 0x00, 0xA0, 0xEA, 0x01, 0xA0, // retain value observed at boot
            0x3E, 0x42, 0xEA, 0x00, 0xA0, // write battery marker
            0x3E, 0x00, 0xE0, 0x40, // LCD off
            0x21, 0x00, 0x80, 0x06, 0x10,
            0x3E, 0xAA, 0x22, 0x05, 0x20, 0xFC, // striped tile
            0x3E, 0xE4, 0xE0, 0x47, 0x3E, 0x91, 0xE0, 0x40,
            0x3E, 0x80, 0xE0, 0x26, // APU power
            0x3E, 0x77, 0xE0, 0x24, 0x3E, 0x11, 0xE0, 0x25, // volume/routing
            0x3E, 0x80, 0xE0, 0x11, 0x3E, 0xF0, 0xE0, 0x12, // duty/envelope
            0x3E, 0x00, 0xE0, 0x13, 0x3E, 0x87, 0xE0, 0x14, // trigger tone
            0x3E, 0x10, 0xE0, 0x00, // select action buttons in JOYP
            0xF0, 0x00, 0xEA, 0x02, 0xA0, // retain current input in cartridge RAM
            0x18, 0xF5 // repeat JOYP polling
        ];
        program.CopyTo(bytes, 0x150);
        byte checksum = 0;
        for (int i = 0x134; i <= 0x14C; i++) checksum = unchecked((byte)(checksum - bytes[i] - 1));
        bytes[0x14D] = checksum;
        return bytes;
    }
}
