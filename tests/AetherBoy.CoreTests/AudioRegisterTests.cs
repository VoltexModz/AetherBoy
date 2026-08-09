using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class AudioRegisterTests
{
    [TestMethod]
    public void Reset_LeavesThePostBootAudioRegistersAndChannelStatus()
    {
        using var fixture = new EmulatorFixture();
        Memory memory = fixture.Emulator.Memory;

        Assert.AreEqual(0xF1, memory.ReadByte(0xFF26));
        Assert.AreEqual(0x77, memory.ReadByte(0xFF24));
        Assert.AreEqual(0xF3, memory.ReadByte(0xFF25));
    }

    [TestMethod]
    public void Nr52PowerOff_ClearsRegistersBlocksWritesAndPreservesWaveRam()
    {
        using var fixture = new EmulatorFixture();
        Memory memory = fixture.Emulator.Memory;
        memory.WriteByte(0xFF30, 0xAB);
        memory.WriteByte(0xFF26, 0x00);

        Assert.AreEqual(0x70, memory.ReadByte(0xFF26));
        Assert.AreEqual(0x00, memory.ReadByte(0xFF12));
        Assert.AreEqual(0x00, memory.ReadByte(0xFF24));
        Assert.AreEqual(0x00, memory.ReadByte(0xFF25));
        Assert.AreEqual(0xAB, memory.ReadByte(0xFF30));

        memory.WriteByte(0xFF12, 0xF3);
        memory.WriteByte(0xFF24, 0x77);
        memory.WriteByte(0xFF25, 0xFF);
        Assert.AreEqual(0x00, memory.ReadByte(0xFF12));
        Assert.AreEqual(0x00, memory.ReadByte(0xFF24));
        Assert.AreEqual(0x00, memory.ReadByte(0xFF25));

        memory.WriteByte(0xFF26, 0x80);
        Assert.AreEqual(0xF0, memory.ReadByte(0xFF26));
    }

    [TestMethod]
    public void PulseTrigger_ActivatesOnlyWhileItsDacIsEnabled()
    {
        using var fixture = new EmulatorFixture();
        Memory memory = fixture.Emulator.Memory;
        memory.WriteByte(0xFF26, 0x00);
        memory.WriteByte(0xFF26, 0x80);

        memory.WriteByte(0xFF12, 0x00);
        memory.WriteByte(0xFF14, 0x80);
        Assert.AreEqual(0, memory.ReadByte(0xFF26) & 1);

        memory.WriteByte(0xFF12, 0x08);
        memory.WriteByte(0xFF14, 0x80);
        Assert.AreEqual(1, memory.ReadByte(0xFF26) & 1);

        memory.WriteByte(0xFF12, 0x00);
        Assert.AreEqual(0, memory.ReadByte(0xFF26) & 1);
    }

    [TestMethod]
    public void LengthExpiration_ClearsTheNr52ChannelStatusBit()
    {
        using var fixture = new EmulatorFixture();
        Memory memory = fixture.Emulator.Memory;
        memory.WriteByte(0xFF26, 0x00);
        memory.WriteByte(0xFF26, 0x80);
        memory.WriteByte(0xFF11, 0x3F);
        memory.WriteByte(0xFF12, 0xF0);
        memory.WriteByte(0xFF14, 0xC0);
        Assert.AreEqual(1, memory.ReadByte(0xFF26) & 1);

        for (int dot = 0; dot < 8_192; dot++) {
            memory.Audio.Tick();
        }

        Assert.AreEqual(0, memory.ReadByte(0xFF26) & 1);
    }

    private sealed class EmulatorFixture : IDisposable
    {
        private readonly string directory;

        public EmulatorFixture()
        {
            directory = Path.Combine(
                Path.GetTempPath(),
                "aetherboy-audio-registers-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string romPath = Path.Combine(directory, "audio.gb");
            byte[] data = new byte[0x8000];
            data[0x147] = (byte)Mbc.ROM_NONE;
            File.WriteAllBytes(romPath, data);
            Emulator = new Nanoboy(new ROM(romPath, Path.Combine(directory, "audio.sav")));
        }

        public Nanoboy Emulator { get; }

        public void Dispose()
        {
            Emulator.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }
}
