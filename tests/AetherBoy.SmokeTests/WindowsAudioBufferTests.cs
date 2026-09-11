using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Platform.Audio;

namespace AetherBoy.SmokeTests;

[TestClass]
public sealed class WindowsAudioBufferTests
{
    [TestMethod]
    [DataRow(44100)]
    [DataRow(65536)]
    public void StereoRingPreservesChannelOrderAcrossOverflowAndKeepsLatencyInFrames(int rate)
    {
        var wave = new GameBoyWaveProvider(rate, 40, 2) { Volume = .5f };
        float[] input = Enumerable.Range(0, rate / 5 * 2).Select(n => n % 2 == 0 ? .8f : -.4f).ToArray();
        wave.Enqueue(input);
        Assert.AreEqual(2, wave.WaveFormat.Channels);
        Assert.IsTrue(wave.Metrics.BufferedMs is > 79 and <= 80);
        Assert.AreEqual(0L, wave.Metrics.DroppedSamples % 2);
        var output = new float[400]; wave.Read(output, 0, 400);
        for (int n = 0; n < output.Length; n++) Assert.AreEqual(n % 2 == 0 ? .4f : -.2f, output[n]);
        Assert.Throws<ArgumentException>(() => wave.Enqueue(new float[3]));
        Assert.Throws<ArgumentException>(() => wave.Read(new float[3], 0, 3));
    }

    [TestMethod]
    [DataRow(44100)]
    [DataRow(65536)]
    public void RingPreservesSamplesAndOffsetsAtBothCoreRates(int rate)
    {
        var wave = new GameBoyWaveProvider(rate) { Volume = 0.5f };
        wave.Enqueue(Enumerable.Repeat(0.5f, rate / 25).ToArray());
        float[] output = Enumerable.Repeat(-1f, 104).ToArray();
        Assert.AreEqual(100, wave.Read(output, 2, 100));
        Assert.AreEqual(-1f, output[0]);
        Assert.AreEqual(-1f, output[103]);
        Assert.IsTrue(output.Skip(2).Take(100).All(sample => sample == 0.25f));
        Assert.AreEqual(0L, wave.Metrics.Underruns);
    }

    [TestMethod]
    public void OverflowDropsOldestAndBoundsLatency()
    {
        var wave = new GameBoyWaveProvider(10000, 40);
        wave.Enqueue(Enumerable.Repeat(-0.5f, 800).ToArray());
        wave.Enqueue(Enumerable.Repeat(0.5f, 1000).ToArray());
        Assert.AreEqual(80d, wave.Metrics.BufferedMs);
        Assert.AreEqual(1000L, wave.Metrics.DroppedSamples);
        float[] output = new float[800];
        wave.Read(output, 0, 800);
        Assert.IsTrue(output.All(sample => sample == 0.5f));
    }

    [TestMethod]
    public void PauseAndClearRemoveStaleAudioWithoutCountingSilenceAsStarvation()
    {
        var wave = new GameBoyWaveProvider(10000);
        wave.Enqueue(Enumerable.Repeat(1f, 400).ToArray());
        wave.SetSuspended(true);
        wave.Enqueue(Enumerable.Repeat(1f, 400).ToArray());
        float[] output = new float[500];
        wave.Read(output, 0, output.Length);
        Assert.IsTrue(output.All(sample => sample == 0));
        Assert.AreEqual(0L, wave.Metrics.Underruns);
        wave.SetSuspended(false);
        wave.Read(output, 0, output.Length);
        Assert.AreEqual(0L, wave.Metrics.Underruns);
        wave.Enqueue(Enumerable.Repeat(0.5f, 400).ToArray());
        wave.Read(output, 0, output.Length);
        Assert.AreEqual(1L, wave.Metrics.Underruns);
        Assert.AreEqual(0.5f, output[399]);
        Assert.AreEqual(0f, output[400]);
        wave.Enqueue(Enumerable.Repeat(1f, 400).ToArray());
        wave.Clear();
        wave.Read(output, 0, output.Length);
        Assert.IsTrue(output.All(sample => sample == 0));
    }

    [TestMethod]
    public void InvalidSamplesCannotReachTheDevice()
    {
        var wave = new GameBoyWaveProvider(10000);
        float[] source = Enumerable.Repeat(float.NaN, 400).ToArray();
        source[0] = 3; source[1] = -3; source[2] = float.PositiveInfinity;
        wave.Enqueue(source);
        float[] output = new float[400];
        wave.Read(output, 0, output.Length);
        Assert.AreEqual(1f, output[0]);
        Assert.AreEqual(-1f, output[1]);
        Assert.IsTrue(output.Skip(2).All(sample => sample == 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => wave.Read(output, -1, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new GameBoyWaveProvider(0));
    }

    [TestMethod]
    public void ConcurrentProducerConsumerKeepsTheRingBounded()
    {
        var wave = new GameBoyWaveProvider(65536);
        Parallel.Invoke(
            () => { for (int n = 0; n < 1000; n++) wave.Enqueue(new float[1098]); },
            () => { float[] output = new float[800]; for (int n = 0; n < 1000; n++) wave.Read(output, 0, 800); });
        Assert.IsTrue(wave.Metrics.BufferedMs is >= 0 and <= 80);
    }
}
