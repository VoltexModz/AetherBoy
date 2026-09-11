using System.Buffers.Binary;
using System.Reflection;
using AetherBoy.Runtime;
using GameboyAdvanced.Core;
using GameboyAdvanced.Core.Apu.Channels;
using nanoboy.Core;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class GbaInspectorTelemetryTests
{
    [TestMethod]
    public void SnapshotContainsIndependentDmaChannelsAndUnpackedWaveBanks()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-gba-inspector-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string rom = Path.Combine(root, "generated.gba"); byte[] program = new byte[1024];
            BinaryPrimitives.WriteUInt32LittleEndian(program, 0xEAFFFFFE); File.WriteAllBytes(rom, program);
            using var machine = new GbaProductionMachine(rom, Path.Combine(root, "game.sav"), new EmulatorConfiguration(0, true, true, true, true, true, 44100));
            var device = (Device)typeof(GbaProductionMachine).GetField("device", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(machine)!;
            device.Apu._psgFifoMasterEnable = true;
            var a = device.Apu._dmaChannels[0]; var b = device.Apu._dmaChannels[1];
            a.CurrentValue = -128; a.FifoReadPtr = 4; a.FifoWritePtr = 20; a.FullVolume = true; a.EnableLeft = true; a.SelectTimer1 = true;
            b.CurrentValue = 42; b.FifoReadPtr = 0; b.FifoWritePtr = 32; b.EnableRight = true;
            var wave = (SoundChannel3)device.Apu._channels[2];
            wave._waveRamBanks[0][0] = 0xA3; wave._waveRamBanks[1][0] = 0xF1; wave._force75PctVolume = true;
            AudioSnapshot snapshot = machine.CaptureSnapshot(SessionState.Running, false, false, 1, 1).Audio!;
            Assert.AreEqual(new DirectSoundChannelSnapshot(-128, 16, true, true, false, 1, true), snapshot.DirectSoundA);
            Assert.AreEqual(new DirectSoundChannelSnapshot(42, 32, false, false, true, 0, true), snapshot.DirectSoundB);
            Assert.AreEqual(64, snapshot.Channel3.WaveRamLength); Assert.AreEqual(.75f, snapshot.Channel3.OutputGain);
            byte[] points = snapshot.Channel3.GetWaveRamCopy();
            Assert.AreEqual((byte)10, points[0]); Assert.AreEqual((byte)3, points[1]);
            Assert.AreEqual((byte)15, points[32]); Assert.AreEqual((byte)1, points[33]);
            a.CurrentValue = 0; wave._waveRamBanks[0][0] = 0; points[0] = 0;
            Assert.AreEqual(-128, snapshot.DirectSoundA!.CurrentSample);
            Assert.AreEqual((byte)10, snapshot.Channel3.GetWaveRamCopy()[0]);
        }
        finally { Directory.Delete(root, true); }
    }
}
