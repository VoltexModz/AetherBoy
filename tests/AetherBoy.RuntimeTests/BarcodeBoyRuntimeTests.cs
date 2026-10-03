using AetherBoy.Runtime;
using nanoboy.Core;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class BarcodeBoyRuntimeTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SyntheticCartridgeCompletesHandshakeAndReadsBothPacketsOnOwnerThread(bool color)
    {
        string root = Path.Combine(Path.GetTempPath(), "aether-barcode-runtime-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "scanner.gb"); File.WriteAllBytes(path, TestCartridge(color));
            var config = new EmulatorConfiguration(0, false, true, true, true, true, 44100);
            ProductionMachine? machine = null;
            await using var session = new EmulationSession(() =>
            {
                machine = new ProductionMachine(path, Path.Combine(root, "scanner.sav"), null, config, 0);
                machine.SetBarcodeBoyEnabled(true);
                machine.ScanBarcodeBoy(BarcodeBoyInput.BattleSpaceBerserker);
                return machine;
            }, new RealTimeFramePacer());
            Assert.IsTrue(SpinWait.SpinUntil(() => session.LatestSnapshot.BarcodeBoy?.CompletedScans == 1 || session.Fault is not null, TimeSpan.FromSeconds(8)));
            await session.SetPausedAsync(true); Assert.IsNull(session.Fault);
            Assert.AreEqual(1, session.LatestSnapshot.BarcodeBoy?.CompletedScans);
            Assert.AreEqual(0xA5, machine!.OnlineMemory.ReadByte(0xC100), "Synthetic CPU program reached its completion marker.");
            byte[] expected = [255, 255, 0x10, 7, 2, .. System.Text.Encoding.ASCII.GetBytes(BarcodeBoyInput.BattleSpaceBerserker), 3,
                2, .. System.Text.Encoding.ASCII.GetBytes(BarcodeBoyInput.BattleSpaceBerserker), 3];
            for (int i = 0; i < expected.Length; i++) Assert.AreEqual(expected[i], machine.OnlineMemory.ReadByte((ushort)(0xC000 + i)));
            Assert.AreEqual(session.LatestSnapshot.BarcodeBoy, session.LatestSnapshot.WithState(SessionState.Running, true).BarcodeBoy);
            await session.ScanBarcodeBoyAsync(BarcodeBoyInput.BattleSpaceValkyrie);
            Assert.IsTrue(session.LatestSnapshot.BarcodeBoy!.Pending);
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => session.ScanBarcodeBoyAsync(BarcodeBoyInput.BattleSpaceBerserker));
            await session.ResetAsync(); Assert.IsFalse(session.LatestSnapshot.BarcodeBoy!.Pending);
            await session.SetBarcodeBoyEnabledAsync(false); Assert.IsNull(session.LatestSnapshot.BarcodeBoy);
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => session.ScanBarcodeBoyAsync(BarcodeBoyInput.BattleSpaceBerserker));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void TextFileImportIsBoundedStrictAndDoesNotRewriteDigits()
    {
        string path = Path.Combine(Path.GetTempPath(), "aether-barcode-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            File.WriteAllText(path, "\uFEFF4907981000301\r\n"); Assert.AreEqual(BarcodeBoyInput.BattleSpaceBerserker, BarcodeBoyInput.ReadFile(path));
            File.WriteAllText(path, "0000000000009"); Assert.AreEqual("0000000000009", BarcodeBoyInput.ReadFile(path));
            File.WriteAllText(path, "4907981000301\n4908052808369"); Assert.ThrowsExactly<ArgumentException>(() => BarcodeBoyInput.ReadFile(path));
            File.WriteAllBytes(path, [0xFF]); Assert.ThrowsExactly<System.Text.DecoderFallbackException>(() => BarcodeBoyInput.ReadFile(path));
            File.WriteAllBytes(path, new byte[129]); Assert.ThrowsExactly<InvalidDataException>(() => BarcodeBoyInput.ReadFile(path));
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public async Task GbaSessionRejectsScannerCommandsWithoutFaulting()
    {
        string root = Path.Combine(Path.GetTempPath(), "aether-barcode-gba-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "idle.gba"); byte[] rom = new byte[512];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(rom, 0xEAFFFFFE); // ARM branch to self
            File.WriteAllBytes(path, rom);
            await using var session = new EmulationSession(path, Path.Combine(root, "idle.sav"), null, new EmulatorConfiguration(0, false, true, true, true, true, 44100));
            Assert.IsTrue(SpinWait.SpinUntil(() => session.State != SessionState.Starting, TimeSpan.FromSeconds(5)));
            await session.SetPausedAsync(true);
            Assert.IsFalse(session.LatestSnapshot.Supports(EmulationFeature.BarcodeBoy));
            await Assert.ThrowsExactlyAsync<NotSupportedException>(() => session.SetBarcodeBoyEnabledAsync(true));
            await Assert.ThrowsExactlyAsync<NotSupportedException>(() => session.ScanBarcodeBoyAsync(BarcodeBoyInput.BattleSpaceBerserker));
            Assert.IsNull(session.Fault); Assert.IsNull(session.LatestSnapshot.BarcodeBoy);
        }
        finally { Directory.Delete(root, true); }
    }

    // Original test program; no commercial ROM bytes, logo or game logic.
    private static byte[] TestCartridge(bool color)
    {
        byte[] rom = new byte[32768]; rom[0x100] = 0xC3; rom[0x101] = 0x50; rom[0x102] = 1; rom[0x143] = color ? (byte)0x80 : (byte)0;
        var code = new List<byte> { 0xF3, 0x31, 0xFE, 0xFF, 0x21, 0, 0xC0 }; // DI; SP; output pointer
        void Transfer(byte data, byte control)
        {
            code.AddRange([0x3E, data, 0xE0, 1, 0x3E, control, 0xE0, 2]);
            code.AddRange([0xF0, 2, 0xCB, 0x7F, 0x20, 0xFA, 0xF0, 1, 0x22]); // poll SC then store SB
        }
        foreach (byte b in new byte[] { 0x10, 7, 0x10, 7 }) Transfer(b, 0x81);
        for (int i = 0; i < 30; i++) Transfer(0, 0x80);
        code.AddRange([0x3E, 0xA5, 0xEA, 0, 0xC1, 0x18, 0xFE]); code.CopyTo(rom, 0x150); return rom;
    }
}
