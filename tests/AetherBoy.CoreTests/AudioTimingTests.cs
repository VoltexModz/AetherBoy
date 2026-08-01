using Microsoft.VisualStudio.TestTools.UnitTesting;
using nanoboy.Core;
using nanoboy.Core.Audio;

namespace AetherBoy.CoreTests;

[TestClass]
public sealed class AudioTimingTests
{
    [TestMethod]
    public void SampleClock_EmitsExactly44100SamplesPerCpuSecond()
    {
        var clock = new AudioSampleClock();
        int samples = 0;

        for (int tick = 0; tick < EmulationClock.CpuClockHz; tick++)
        {
            if (clock.Tick(44_100))
            {
                samples++;
            }
        }

        Assert.AreEqual(44_100, samples);
    }

    [TestMethod]
    public void AudioEvent_ContainsExactlyConfiguredBufferSize()
    {
        using var audio = new Audio(SoundOutMode.None)
        {
            Enabled = false,
            SampleRate = 192_000,
            BufferSize = 8
        };
        AudioAvailableEventArgs? received = null;
        audio.AudioAvailable += (_, args) => received = args;

        for (int tick = 0; tick < 1_000 && received is null; tick++)
        {
            audio.Tick();
        }

        Assert.IsNotNull(received);
        Assert.AreEqual(8, received.Buffer.Length);
        Assert.AreEqual(192_000, received.SampleRate);
    }

    [TestMethod]
    public void ChannelLengthCounters_UseHardwareClockFractions()
    {
        var pulse = new QuadChannel { SoundLengthRaw = 0 };
        var wave = new WaveChannel { SoundLengthRaw = 0 };
        var noise = new NoiseChannel { SoundLengthRaw = 0 };

        Assert.AreEqual(1_048_576, pulse.SoundLength);
        Assert.AreEqual(16_384, new QuadChannel { SoundLengthRaw = 63 }.SoundLength);
        Assert.AreEqual(4_194_304, wave.SoundLength);
        Assert.AreEqual(16_384, new WaveChannel { SoundLengthRaw = 255 }.SoundLength);
        Assert.AreEqual(1_048_576, noise.SoundLength);
        Assert.AreEqual(16_384, new NoiseChannel { SoundLengthRaw = 63 }.SoundLength);
    }

    [TestMethod]
    public void NoiseLfsr_AdvancesOnlyAfterConfiguredPeriod()
    {
        var noise = new NoiseChannel
        {
            ClockFrequency = 0,
            DividingRatio = 0,
            CounterStep = false,
            Counter = 0x7FFF
        };

        for (int tick = 0; tick < 7; tick++)
        {
            noise.Tick();
        }

        Assert.AreEqual(0x7FFF, noise.Counter);
        noise.Tick();
        Assert.AreNotEqual(0x7FFF, noise.Counter);
    }

    [TestMethod]
    public void SevenBitNoise_CopiesFeedbackToBitsSixAndFourteen()
    {
        var noise = new NoiseChannel
        {
            ClockFrequency = 0,
            DividingRatio = 0,
            CounterStep = true,
            Counter = 1
        };

        for (int tick = 0; tick < 8; tick++)
        {
            noise.Tick();
        }

        Assert.AreEqual(1, (noise.Counter >> 6) & 1);
        Assert.AreEqual(1, (noise.Counter >> 14) & 1);
    }

    [TestMethod]
    public void NoiseRestart_InitializesFullFifteenBitRegister()
    {
        var noise = new NoiseChannel
        {
            CounterStep = true,
            Counter = 0
        };

        noise.Restart();

        Assert.AreEqual(0x7FFF, noise.Counter);
    }
}
