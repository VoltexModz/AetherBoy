using System.Reflection;
using System.Runtime.InteropServices;
using AetherBoy.Runtime;
using SDL3;

namespace AetherBoy.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LinuxPatchLabTests
{
    private string root = null!;
    private LinuxDataPaths paths = null!;
    [TestInitialize] public void Setup()
    {
        root = Path.Combine(Path.GetTempPath(), "aetherboy-patch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); paths = LinuxDataPaths.Isolated(Path.Combine(root, "storage"));
    }
    [TestCleanup] public void Cleanup() => Directory.Delete(root, true);

    [TestMethod]
    [DataRow("ips")]
    [DataRow("bps")]
    [DataRow("ups")]
    public void ImportPreservesOriginalAndSavesAndReusesIdenticalResults(string format)
    {
        var (source, patch, original, target) = Files(format);
        File.WriteAllBytes(Path.ChangeExtension(source, ".sav"), [42, 43]);
        var service = new LinuxRomPatchService(paths);
        var result = service.ApplyAndImport(source, patch);
        CollectionAssert.AreEqual(original, File.ReadAllBytes(source));
        CollectionAssert.AreEqual(target, File.ReadAllBytes(result.Path));
        CollectionAssert.AreEqual(new byte[] { 42, 43 }, File.ReadAllBytes(Path.ChangeExtension(source, ".sav")));
        Assert.AreEqual(format != "ips", result.ChecksumsVerified);
        Assert.IsFalse(result.Reversed); Assert.IsFalse(result.Reused);
        using var storage = LinuxRomStorage.Open(paths, result.Path);
        Assert.IsFalse(File.Exists(storage.SavePath), "Never migrate the original cartridge's save into a hack.");
        Assert.AreEqual(result.Path, new LinuxLibrary(paths).Read().Single().Path);
        var repeated = service.ApplyAndImport(source, patch);
        Assert.IsTrue(repeated.Reused); Assert.AreEqual(result.Path, repeated.Path);
        Assert.AreEqual(1, Directory.GetDirectories(Path.Combine(paths.Data, "roms")).Length);
        string manifest = File.ReadAllText(Path.Combine(Path.GetDirectoryName(result.Path)!, "patch.json"));
        Assert.IsFalse(manifest.Contains(root), "Provenance must not contain private source paths.");
    }

    [TestMethod]
    public void ExplicitUpsUndoKeepsKnownOriginalMetadataAndSave()
    {
        var (source, patch, _, _) = Files("ups");
        string identity = LinuxRomStorage.Identify(source);
        new LinuxLibrary(paths).Remember(identity, source);
        string metadata = Path.Combine(paths.Data, "library", identity + ".json");
        byte[] before = File.ReadAllBytes(metadata);
        using var originalStorage = LinuxRomStorage.Open(paths, source);
        File.WriteAllBytes(originalStorage.SavePath, [7, 8, 9]);
        var service = new LinuxRomPatchService(paths);
        var modified = service.ApplyAndImport(source, patch);
        Assert.Throws<InvalidDataException>(() => service.ApplyAndImport(modified.Path, patch));
        var restored = service.ApplyAndImport(modified.Path, patch, reverseUps: true);
        Assert.IsTrue(restored.Reversed); Assert.IsTrue(restored.Reused);
        Assert.AreEqual(source, restored.Path);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(metadata));
        CollectionAssert.AreEqual(new byte[] { 7, 8, 9 }, File.ReadAllBytes(originalStorage.SavePath));
    }

    [TestMethod]
    public void InvalidInputsAndOversizedSourceCreateNoImportedRom()
    {
        var (source, patch, original, _) = Files("bps");
        var service = new LinuxRomPatchService(paths);
        byte[] corrupt = File.ReadAllBytes(patch); corrupt[^1] ^= 1; File.WriteAllBytes(patch, corrupt);
        Assert.Throws<InvalidDataException>(() => service.ApplyAndImport(source, patch));
        string unchanged = Path.Combine(root, "no-op.ips"); File.WriteAllBytes(unchanged, "PATCHEOF"u8.ToArray());
        Assert.Throws<InvalidDataException>(() => service.ApplyAndImport(source, unchanged));
        Assert.Throws<InvalidDataException>(() => service.ApplyAndImport(source, "archive.zip"));
        string oversized = Path.Combine(root, "oversized.gba");
        using (var file = File.Create(oversized)) file.SetLength(32 * 1024 * 1024 + 1);
        Assert.Throws<InvalidDataException>(() => service.ApplyAndImport(oversized, patch));
        Assert.IsFalse(Directory.Exists(Path.Combine(paths.Data, "roms")));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(source));
    }

    [TestMethod]
    public void CorruptExistingResultIsNeverOverwritten()
    {
        var (source, patch, _, _) = Files("ips");
        var service = new LinuxRomPatchService(paths);
        var result = service.ApplyAndImport(source, patch);
        File.WriteAllBytes(result.Path, [99]);
        Assert.Throws<InvalidDataException>(() => service.ApplyAndImport(source, patch));
        CollectionAssert.AreEqual(new byte[] { 99 }, File.ReadAllBytes(result.Path));
        Assert.IsFalse(Directory.EnumerateDirectories(Path.Combine(paths.Data, "roms"), ".patch-*").Any());
    }

    [TestMethod]
    public void CatalogFailureKeepsUsableResultAndReportsWarning()
    {
        var (source, patch, _, target) = Files("ips");
        Directory.CreateDirectory(paths.Data);
        File.WriteAllText(Path.Combine(paths.Data, "library"), "blocking file");
        var result = new LinuxRomPatchService(paths).ApplyAndImport(source, patch);
        Assert.IsNotNull(result.Warning);
        CollectionAssert.AreEqual(target, File.ReadAllBytes(result.Path));
        Assert.IsFalse(Directory.EnumerateDirectories(Path.Combine(paths.Data, "roms"), ".patch-*").Any());
    }

    [TestMethod]
    public void NativePatchLabRoutesDropCancelApplyErrorAndOpenResult()
    {
        if (Environment.GetEnvironmentVariable("AETHERBOY_UI_TESTS") != "1")
        { Assert.Inconclusive("Set AETHERBOY_UI_TESTS=1 in a native Wayland session."); return; }
        var (source, patch, _, target) = Files("ips");
        string settings = Path.Combine(root, "settings.json");
        LinuxSettingsStore.Save(settings, new LinuxFrontendOptions { AudioEnabled = false });
        SDL.SetHint("SDL_VIDEO_DRIVER", "wayland");
        Assert.IsTrue(SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Events | SDL.InitFlags.Gamepad), SDL.GetError());
        try
        {
            using var host = new WaylandEmulatorHost(LinuxDesktopProfile.Detect(), settings, hidden: true);
            void Draw() => Call(host, "DrawShell");
            void Click(float x, float y) { Draw(); Call(host, "HandleMouseClick", x, y); Draw(); }
            void Drop(string path)
            {
                IntPtr text = Marshal.StringToCoTaskMemUTF8(path);
                try { Call(host, "HandleEvent", new SDL.Event { Drop = new SDL.DropEvent { Type = SDL.EventType.DropFile, Data = text } }); }
                finally { Marshal.FreeCoTaskMem(text); }
            }
            Call(host, "TryLoadRom", source); WaitForSession(host);
            var originalSession = Field<EmulationSession>(host, "session");
            Click(270, 724); Click(120, 595); Click(990, 570);
            Assert.IsTrue(Field<bool>(host, "showPatchLab"));
            Drop(source); Drop(patch);
            Assert.AreEqual(source, Field<string>(host, "patchSourcePath"));
            Assert.AreEqual(patch, Field<string>(host, "patchFilePath"));
            Assert.AreSame(originalSession, Field<EmulationSession>(host, "session"));

            // Exercise the actual callback/drain route without opening an external portal dialog.
            FieldInfo purpose = host.GetType().GetField("pickingPatch", Private)!;
            purpose.SetValue(host, Enum.Parse(purpose.FieldType, "Source"));
            host.GetType().GetField("fileDialogOpen", Private)!.SetValue(host, 1);
            IntPtr cancelled = Marshal.AllocHGlobal(IntPtr.Size);
            try { Marshal.WriteIntPtr(cancelled, IntPtr.Zero); Call(host, "OnFileDialogCompleted", IntPtr.Zero, cancelled, 0); }
            finally { Marshal.FreeHGlobal(cancelled); }
            Assert.AreEqual(1, Field<int>(host, "fileDialogOpen"));
            Call(host, "DrainDialogSelections");
            Assert.AreEqual(0, Field<int>(host, "fileDialogOpen"));
            Assert.AreEqual(source, Field<string>(host, "patchSourcePath"));
            purpose.SetValue(host, Enum.Parse(purpose.FieldType, "Patch"));
            host.GetType().GetField("fileDialogOpen", Private)!.SetValue(host, 1);
            IntPtr selected = Marshal.StringToCoTaskMemUTF8(patch), list = Marshal.AllocHGlobal(IntPtr.Size * 2);
            try
            {
                Marshal.WriteIntPtr(list, selected); Marshal.WriteIntPtr(list, IntPtr.Size, IntPtr.Zero);
                Call(host, "OnFileDialogCompleted", IntPtr.Zero, list, 0);
            }
            finally { Marshal.FreeCoTaskMem(selected); Marshal.FreeHGlobal(list); }
            Call(host, "DrainDialogSelections");
            Assert.AreEqual(patch, Field<string>(host, "patchFilePath"));
            Assert.AreSame(originalSession, Field<EmulationSession>(host, "session"));

            Click(415, 480);
            Assert.IsTrue(SpinWait.SpinUntil(() => { Call(host, "CompletePendingPatch"); return Field<Task?>(host, "pendingPatch") is null; }, TimeSpan.FromSeconds(10)));
            Assert.IsFalse(Field<bool>(host, "patchFailed"), Field<string>(host, "patchMessage"));
            var result = Field<LinuxPatchedRom>(host, "patchResult");
            CollectionAssert.AreEqual(target, File.ReadAllBytes(result.Path));
            Assert.AreSame(originalSession, Field<EmulationSession>(host, "session"));
            Draw(); Capture(host, "patch-result");
            SDL.SetWindowSize(Field<IntPtr>(host, "window"), 900, 650);
            Draw(); Capture(host, "patch-compact");
            Click(425, 600); WaitForSession(host);
            Assert.AreEqual(result.Path, Field<string>(host, "romPath"));
            Assert.AreNotSame(originalSession, Field<EmulationSession>(host, "session"));

            File.WriteAllBytes(patch, [1, 2, 3]);
            Click(415, 480);
            Assert.IsTrue(SpinWait.SpinUntil(() => { Call(host, "CompletePendingPatch"); return Field<Task?>(host, "pendingPatch") is null; }, TimeSpan.FromSeconds(10)));
            Assert.IsTrue(Field<bool>(host, "patchFailed"));
            Assert.IsNull(Field<LinuxPatchedRom?>(host, "patchResult"));
            Assert.IsTrue(Field<string>(host, "patchMessage").StartsWith("Patch failed:"));
        }
        finally { SDL.Quit(); }
    }

    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static T Field<T>(object host, string name) => (T)host.GetType().GetField(name, Private)!.GetValue(host)!;
    private static void Call(object host, string method, params object[] args) => host.GetType().GetMethod(method, Private)!.Invoke(host, args);
    private static void WaitForSession(object host) => Assert.IsTrue(SpinWait.SpinUntil(() =>
    { Call(host, "CompletePendingLoad"); return Field<object?>(host, "pendingSession") is null && Field<EmulationSession?>(host, "session") is not null; }, TimeSpan.FromSeconds(10)));
    private static void Capture(object host, string name)
    {
        string? folder = Environment.GetEnvironmentVariable("AETHERBOY_PATCH_CAPTURE_DIR");
        if (folder is null) return;
        Directory.CreateDirectory(folder);
        IntPtr surface = SDL.RenderReadPixels(Field<IntPtr>(host, "renderer"), null);
        Assert.AreNotEqual(IntPtr.Zero, surface);
        try { Assert.IsTrue(SDL.SavePNG(surface, Path.Combine(folder, name + ".png"))); }
        finally { SDL.DestroySurface(surface); }
    }

    private (string Source, string Patch, byte[] Original, byte[] Target) Files(string format)
    {
        byte[] original = LinuxPlaytestTests.MakeBatteryRom(false), target = (byte[])original.Clone();
        target[0x200] ^= 0x42;
        string source = Path.Combine(root, "original.gb"), patch = Path.Combine(root, "example." + format);
        File.WriteAllBytes(source, original);
        List<byte> bytes;
        if (format == "ips") bytes = [.. "PATCH"u8.ToArray(), 0, 2, 0, 0, 1, target[0x200], .. "EOF"u8.ToArray()];
        else
        {
            bytes = [.. (format == "bps" ? "BPS1"u8.ToArray() : "UPS1"u8.ToArray())];
            Number(bytes, original.Length); Number(bytes, target.Length);
            if (format == "bps") { Number(bytes, 0); Number(bytes, ((target.Length - 1) << 2) | 1); bytes.AddRange(target); }
            else { Number(bytes, 0x200); bytes.Add(0x42); bytes.Add(0); }
            bytes.AddRange(BitConverter.GetBytes(Crc(original))); bytes.AddRange(BitConverter.GetBytes(Crc(target)));
            bytes.AddRange(BitConverter.GetBytes(Crc(bytes.ToArray())));
        }
        File.WriteAllBytes(patch, bytes.ToArray()); return (source, patch, original, target);
    }
    private static void Number(List<byte> bytes, int value)
    {
        while (true) { byte part = (byte)(value & 127); value >>= 7; if (value == 0) { bytes.Add((byte)(part | 128)); return; } bytes.Add(part); value--; }
    }
    private static uint Crc(byte[] data)
    {
        uint value = uint.MaxValue;
        foreach (byte b in data) { value ^= b; for (int bit = 0; bit < 8; bit++) value = (value >> 1) ^ ((value & 1) == 0 ? 0 : 0xEDB88320u); }
        return ~value;
    }
}
