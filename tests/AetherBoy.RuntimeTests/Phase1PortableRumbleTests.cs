using AetherBoy.Runtime;
using nanoboy.Core;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class Phase1PortableRumbleTests
{
    [TestMethod]
    public void PortableModeRequiresAnExplicitFlagOrMarker()
    {
        string directory = Path.Combine(Path.GetTempPath(), "aetherboy-portable-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Assert.IsFalse(PortableStorage.IsRequested([], directory));
            Assert.IsTrue(PortableStorage.IsRequested(["--portable"], directory));
            File.WriteAllText(Path.Combine(directory, PortableStorage.MarkerName), "");
            Assert.IsTrue(PortableStorage.IsRequested([], directory));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public void Mbc5MotorStateAppearsInSnapshotsAndSurvivesSnapshotTransforms()
    {
        string directory = Path.Combine(Path.GetTempPath(), "aetherboy-rumble-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string rom = Path.Combine(directory, "rumble.gb");
            byte[] image = new byte[32768];
            image[0x147] = (byte)Mbc.ROM_MBC5_RUMBLE;
            File.WriteAllBytes(rom, image);
            using var machine = new ProductionMachine(rom, Path.Combine(directory, "game.sav"), null,
                new EmulatorConfiguration(0, false, true, true, true, true, 44100), 0);
            Assert.IsFalse(Capture().RumbleActive);
            machine.OnlineMemory.ROM.MBC.WriteByte(0x4000, 0x08);
            Assert.IsTrue(Capture().RumbleActive);
            Assert.IsTrue(Capture().WithState(SessionState.Paused, true).RumbleActive);
            Assert.IsTrue(Capture().WithVideoGeometry(VideoGeometry.GameBoyAdvance).RumbleActive);
            machine.OnlineMemory.ROM.MBC.WriteByte(0x4000, 0x00);
            Assert.IsFalse(Capture().RumbleActive);

            EmulationSnapshot Capture() => machine.CaptureSnapshot(SessionState.Running, false, false, 0, 0);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
