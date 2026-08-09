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
        using var audio = new Audio()
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
    public void AnalogHighPass_RemovesDcWithoutChangingTheInitialEdge()
    {
        using var audio = new Audio(dmgMode: true) { Enabled = false };

        float first = audio.ApplyHighPass(0.75f);
        float second = audio.ApplyHighPass(0.75f);
        float settled = second;
        for (int sample = 0; sample < 4_096; sample++) {
            settled = audio.ApplyHighPass(0.75f);
        }

        Assert.AreEqual(0.75f, first, 0.0001f);
        Assert.IsGreaterThan(0f, second);
        Assert.IsTrue(second < first);
        Assert.AreEqual(0f, settled, 0.0001f);
    }

    [TestMethod]
    public void AnalogHighPass_HoldsItsChargeWhileEveryDacIsDisconnected()
    {
        using var audio = new Audio(dmgMode: true) { Enabled = false };
        for (int sample = 0; sample < 4_096; sample++) {
            audio.ApplyHighPass(0.75f);
        }

        for (int sample = 0; sample < 4_096; sample++) {
            Assert.AreEqual(0f, audio.ApplyHighPass(0f, capacitorConnected: false));
        }

        Assert.AreEqual(0f, audio.ApplyHighPass(0.75f), 0.0001f);
    }

    [TestMethod]
    public void CgbWaveRam_UsesTheCurrentlyPlayingByteForEveryAddress()
    {
        var channel = new WaveChannel(dmgMode: false) {
            On = true,
            FrequencyRaw = 0x7FF
        };
        channel.WriteWaveRam(0, 0x12);
        channel.WriteWaveRam(1, 0x34);
        channel.Restart();

        Assert.AreEqual(0x12, channel.ReadWaveRam(0x0F));
        for (int dot = 0; dot < 12; dot++) {
            channel.Tick();
        }
        Assert.AreEqual(2, channel.WavePosition);
        Assert.AreEqual(0x34, channel.ReadWaveRam(0));

        channel.WriteWaveRam(0x0F, 0xAB);
        channel.On = false;
        channel.ApplyDacState();
        Assert.AreEqual(0xAB, channel.ReadWaveRam(1));
    }

    [TestMethod]
    public void DmgWaveRam_IsAccessibleOnlyOnTheFetchTick()
    {
        var channel = new WaveChannel(dmgMode: true) {
            On = true,
            FrequencyRaw = 0x7FE
        };
        channel.WriteWaveRam(0, 0x12);
        channel.Restart();

        Assert.AreEqual(0xFF, channel.ReadWaveRam(0));
        for (int dot = 0; dot < 9; dot++) {
            channel.Tick();
            Assert.AreEqual(0xFF, channel.ReadWaveRam(0));
        }
        channel.Tick();
        Assert.AreEqual(0x12, channel.ReadWaveRam(0x0F));
        channel.Tick();
        Assert.AreEqual(0xFF, channel.ReadWaveRam(0));
    }

    [TestMethod]
    public void DmgWaveRetrigger_CopiesTheCurrentAlignedBlockIntoTheFirstFourBytes()
    {
        var channel = new WaveChannel(dmgMode: true) {
            On = true,
            FrequencyRaw = 0x7FF
        };
        for (int index = 0; index < 16; index++) {
            channel.WriteWaveRam(index, (byte)(index * 0x11));
        }
        channel.Restart();
        for (int dot = 0; dot < 26; dot++) {
            channel.Tick();
        }

        channel.Restart();
        channel.On = false;
        channel.ApplyDacState();

        Assert.AreEqual(0x44, channel.ReadWaveRam(0));
        Assert.AreEqual(0x55, channel.ReadWaveRam(1));
        Assert.AreEqual(0x66, channel.ReadWaveRam(2));
        Assert.AreEqual(0x77, channel.ReadWaveRam(3));
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

    [TestMethod]
    public void FrameSequencer_ClocksLengthOnTheFirst256HzBoundary()
    {
        using var audio = new Audio { Enabled = false };
        audio.Channel1.Volume = 15;
        audio.Channel1.WavePatternDuty = 2;
        audio.Channel1.SoundLengthRaw = 63;
        audio.Channel1.StopOnLengthExpired = true;
        audio.Channel1.Restart();

        Tick(audio, 8_191);
        Assert.IsGreaterThan(0f, audio.Channel1.Next(44_100));
        audio.Tick();
        Assert.AreEqual(0f, audio.Channel1.Next(44_100));
    }

    [TestMethod]
    public void EnablingLengthBeforeANonLengthStep_ClocksItImmediately()
    {
        using var audio = new Audio { Enabled = false };
        audio.Channel1.SoundLengthRaw = 63;
        audio.Channel1.Volume = 15;
        audio.Channel1.Restart();
        Tick(audio, 8_192);
        audio.Channel1.SoundLengthRaw = 63;

        audio.Channel1.WriteControl(
            lengthEnabled: true,
            trigger: false,
            audio.ShouldClockLengthOnWrite);

        Assert.IsFalse(audio.Channel1.IsActive);
    }

    [TestMethod]
    public void FrameSequencer_ClocksSweepOnlyOnStepsTwoAndSix()
    {
        using var audio = new Audio { Enabled = false };
        audio.Channel1.Frequency = 1_000;
        audio.Channel1.Volume = 1;
        audio.Channel1.SweepTime = 1;
        audio.Channel1.SweepShift = 1;
        audio.Channel1.SweepDirection = SweepMode.Addition;
        audio.Channel1.Restart();

        Tick(audio, 24_575);
        Assert.AreEqual(1_000, audio.Channel1.CurrentFrequency);
        audio.Tick();
        Assert.AreEqual(1_500, audio.Channel1.CurrentFrequency);
    }

    [TestMethod]
    public void SweepShiftZero_DoesNotChangeTheChannelFrequency()
    {
        using var audio = new Audio { Enabled = false };
        audio.Channel1.Frequency = 1_000;
        audio.Channel1.SweepTime = 1;
        audio.Channel1.SweepShift = 0;

        Tick(audio, 24_576);

        Assert.AreEqual(1_000, audio.Channel1.CurrentFrequency);
    }

    [TestMethod]
    public void SweepShiftZero_ChecksOverflowOnlyWhenTheSweepClockRuns()
    {
        var pulse = new QuadChannel {
            Frequency = 0x7FF,
            Volume = 1,
            SweepTime = 1,
            SweepShift = 0
        };

        pulse.Restart();
        Assert.IsTrue(pulse.IsActive);

        pulse.ClockSweep();

        Assert.IsFalse(pulse.IsActive);
        Assert.AreEqual(0x7FF, pulse.CurrentFrequency);
    }

    [TestMethod]
    public void SweepTrigger_DisablesChannelWhenTheInitialCalculationOverflows()
    {
        var pulse = new QuadChannel
        {
            Frequency = 0x7FF,
            Volume = 1,
            SweepShift = 1
        };

        pulse.Restart();

        Assert.IsFalse(pulse.IsActive);
    }

    [TestMethod]
    public void FrameSequencer_ClocksEnvelopeAt64Hz()
    {
        using var audio = new Audio { Enabled = false };
        audio.Channel4.Volume = 1;
        audio.Channel4.EnvelopeSweep = 1;
        audio.Channel4.EnvelopeDirection = EnvelopeMode.Increase;

        Tick(audio, 65_535);
        Assert.AreEqual(1, audio.Channel4.CurrentVolume);
        audio.Tick();
        Assert.AreEqual(2, audio.Channel4.CurrentVolume);
    }

    [TestMethod]
    public void PulseFrequencyTimer_AdvancesDutyAfterFourDotsAtPeriod2047()
    {
        var pulse = new QuadChannel { Frequency = 0x7FF, Volume = 15 };
        pulse.Restart();

        for (int dot = 0; dot < 3; dot++) {
            pulse.Tick();
        }
        Assert.AreEqual(0, pulse.DutyStep);
        pulse.Tick();
        Assert.AreEqual(1, pulse.DutyStep);
    }

    [TestMethod]
    public void WaveFrequencyTimer_AppliesTheModelSpecificTriggerStartupDelay()
    {
        var dmgWave = new WaveChannel(dmgMode: true) { FrequencyRaw = 0x7FF, On = true };
        dmgWave.Restart();

        for (int dot = 0; dot < 7; dot++) {
            dmgWave.Tick();
        }
        Assert.AreEqual(0, dmgWave.WavePosition);
        dmgWave.Tick();
        Assert.AreEqual(1, dmgWave.WavePosition);

        var cgbWave = new WaveChannel(dmgMode: false) { FrequencyRaw = 0x7FF, On = true };
        cgbWave.Restart();

        for (int dot = 0; dot < 9; dot++) {
            cgbWave.Tick();
        }
        Assert.AreEqual(0, cgbWave.WavePosition);
        cgbWave.Tick();
        Assert.AreEqual(1, cgbWave.WavePosition);
    }

    [TestMethod]
    public void HardwareAudioTimers_DoNotAllocateAcrossACompleteFrame()
    {
        using var audio = new Audio { Enabled = false };
        audio.Channel1.Frequency = 0x700;
        audio.Channel1.Volume = 15;
        audio.Channel1.Restart();
        Tick(audio, EmulationClock.DotsPerFrame);

        long before = GC.GetAllocatedBytesForCurrentThread();
        Tick(audio, EmulationClock.DotsPerFrame);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.IsLessThan(1_024L, allocated,
            $"A complete hardware-audio frame allocated {allocated} bytes.");
    }

    private static void Tick(Audio audio, int count)
    {
        for (int tick = 0; tick < count; tick++)
        {
            audio.Tick();
        }
    }
}
