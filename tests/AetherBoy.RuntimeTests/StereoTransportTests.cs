using System.Buffers.Binary;
using System.Reflection;
using AetherBoy.Runtime;
using AetherBoy.Runtime.Audio;
using nanoboy.Core;

namespace AetherBoy.RuntimeTests;

[TestClass]
public sealed class StereoTransportTests
{
    [TestMethod]
    public void StereoTransportKeepsLegacyMonoContractAndOwnsItsBuffers()
    {
        float[] stereo = [1, -1, .75f, .25f];
        var frames = new AudioSamplesAvailableEventArgs(stereo, 65536, 2);
        stereo[0] = 0;
        Assert.AreEqual(2, frames.Channels); Assert.AreEqual(2, frames.SampleCount);
        Assert.AreEqual(4, frames.InterleavedSampleCount);
        CollectionAssert.AreEqual(new float[] { 0, .5f }, frames.GetSamplesCopy());
        CollectionAssert.AreEqual(new float[] { 1, -1, .75f, .25f }, frames.GetInterleavedSamplesCopy());
        float[] copy = frames.GetInterleavedSamplesCopy(); copy[0] = 0;
        Assert.AreEqual(1f, frames.GetInterleavedSamplesCopy()[0]);
        Assert.Throws<ArgumentException>(() => new AudioSamplesAvailableEventArgs(new float[3], 44100, 2));
        Assert.Throws<ArgumentException>(() => frames.CopySamplesTo(new float[1]));
    }
    [TestMethod]
    public void DispatcherPreservesStereoChannels()
    {
        using var delivered = new ManualResetEventSlim();
        AudioSamplesAvailableEventArgs? received = null;
        var dispatcher = new BoundedAudioDispatcher(block => { received = block; delivered.Set(); });
        try
        {
            dispatcher.TryPost(new(new float[] { .5f, -.75f }, 44100, 2));
            Assert.IsTrue(delivered.Wait(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(2, received!.Channels);
            CollectionAssert.AreEqual(new float[] { .5f, -.75f }, received.GetInterleavedSamplesCopy());
        }
        finally { dispatcher.StopWithoutWaiting(); }
    }
    [TestMethod]
    public void GbaAdapterPreservesSignedLittleEndianLeftAndRight()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-stereo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string rom = Path.Combine(root, "audio.gba");
            byte[] program = new byte[1024]; BinaryPrimitives.WriteUInt32LittleEndian(program, 0xEAFFFFFE);
            File.WriteAllBytes(rom, program);
            using var machine = new GbaProductionMachine(rom, Path.Combine(root, "audio.sav"), new EmulatorConfiguration(0, true, true, true, true, true, 44100));
            AudioSamplesAvailableEventArgs? received = null; machine.AudioSamplesAvailable += (_, data) => received = data;
            byte[] pcm = new byte[8];
            BinaryPrimitives.WriteInt16LittleEndian(pcm, 16384); BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(2), -24576);
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(4), -32768); BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(6), 32767);
            typeof(GbaProductionMachine).GetMethod("OnCoreAudio", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(machine, new object[] { pcm });
            Assert.IsNotNull(received); Assert.AreEqual(2, received.Channels);
            CollectionAssert.AreEqual(new float[] { .5f, -.75f, -1, 32767f / 32768 }, received.GetInterleavedSamplesCopy());
        }
        finally { Directory.Delete(root, true); }
    }
    [TestMethod]
    public void StereoWavHasCorrectChannelsByteRateAlignmentAndOrdering()
    {
        string root = Path.Combine(Path.GetTempPath(), "aetherboy-stereo-wav-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "capture.wav");
            using (var recorder = new WavRecorder())
            {
                recorder.Start(path, 65536, 2);
                recorder.AddFrames(new(new float[] { .5f, -.75f, 1, -1 }, 65536, 2));
                recorder.AddFrames(new(new float[] { .25f }, 65536)); // Mono fallback duplicates, never changes speed.
            }
            byte[] bytes = File.ReadAllBytes(path);
            Assert.AreEqual(2, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(22)));
            Assert.AreEqual(65536 * 4, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(28)));
            Assert.AreEqual(4, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(32)));
            Assert.AreEqual(12, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(40)));
            short[] expected = [16383, -24575, 32767, -32767, 8191, 8191];
            for (int n = 0; n < expected.Length; n++) Assert.AreEqual(expected[n], BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(44 + n * 2)));
        }
        finally { Directory.Delete(root, true); }
    }
}
