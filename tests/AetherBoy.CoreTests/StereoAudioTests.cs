using System.Buffers.Binary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core.Audio;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class StereoAudioTests
{
    [TestMethod]
    [DataRow(false, 0x10, 0)]
    [DataRow(false, 0x01, 1)]
    [DataRow(true, 0x10, 0)]
    [DataRow(true, 0x01, 1)]
    public void Nr51SeparatesLeftAndRightOnDmgAndCgb(bool dmg, int routing, int activeChannel)
    {
        using var audio = Tone(dmg, (byte)routing);
        AudioAvailableEventArgs frame = Next(audio);
        Assert.IsNotNull(frame.StereoBuffer);
        Assert.AreEqual(frame.Buffer.Length * 2, frame.StereoBuffer.Length);
        Assert.IsTrue(frame.StereoBuffer.Where((_, n) => n % 2 == activeChannel).Any(value => Math.Abs(value) > .001));
        Assert.IsTrue(frame.StereoBuffer.Where((_, n) => n % 2 != activeChannel).All(value => value == 0));
        for (int n = 0; n < frame.Buffer.Length; n++)
            Assert.AreEqual(frame.Buffer[n], (frame.StereoBuffer[n * 2] + frame.StereoBuffer[n * 2 + 1]) * .5f, .000001f);
    }
    [TestMethod]
    public void StereoStateRestoresBothCapacitorsAndPendingFramesExactly()
    {
        using var original = Tone(true, 0x10);
        Next(original);
        for (int n = 0; n < 357; n++) original.Tick();
        byte[] payload = original.CaptureStatePayload();
        using var restored = new Audio(dmgMode: true); restored.PrepareStateRestore(payload)();
        CollectionAssert.AreEqual(Next(original).StereoBuffer!, Next(restored).StereoBuffer!);
        CollectionAssert.AreEqual(original.CaptureStatePayload(), restored.CaptureStatePayload());
    }
    [TestMethod]
    public void LegacyMonoPayloadLoadsAndMalformedStereoExtensionIsTransactional()
    {
        using var original = Tone(false, 0x11);
        for (int n = 0; n < 357; n++) original.Tick();
        byte[] modern = original.CaptureStatePayload();
        int count = BinaryPrimitives.ReadInt32LittleEndian(modern.AsSpan(33, 4));
        byte[] legacy = modern[..^(12 + count * 8)];
        using var restored = new Audio(); restored.PrepareStateRestore(legacy)();
        Assert.AreEqual(original.OutputRouting, restored.OutputRouting);
        Assert.AreEqual(2 * Next(original).Buffer.Length, Next(restored).StereoBuffer!.Length);
        byte[] before = restored.CaptureStatePayload();
        byte[] invalid = (byte[])modern.Clone();
        BinaryPrimitives.WriteSingleLittleEndian(invalid.AsSpan(legacy.Length + 4, 4), float.NaN);
        Assert.Throws<InvalidOperationException>(() => restored.PrepareStateRestore(invalid));
        CollectionAssert.AreEqual(before, restored.CaptureStatePayload());
    }
    private static Audio Tone(bool dmg, byte routing)
    {
        var audio = new Audio(dmg) { MasterVolume = 0x77, OutputRouting = routing, BufferSize = 64 };
        audio.Channel1.Volume = 15;
        audio.Channel1.Frequency = 1000;
        audio.Channel1.WavePatternDuty = 2;
        audio.Channel1.Restart();
        return audio;
    }
    private static AudioAvailableEventArgs Next(Audio audio)
    {
        AudioAvailableEventArgs? received = null;
        void Receive(object? sender, AudioAvailableEventArgs args) => received = args;
        audio.AudioAvailable += Receive;
        try
        {
            for (int n = 0; n < 100000 && received == null; n++) audio.Tick();
            Assert.IsNotNull(received); return received;
        }
        finally { audio.AudioAvailable -= Receive; }
    }
}
