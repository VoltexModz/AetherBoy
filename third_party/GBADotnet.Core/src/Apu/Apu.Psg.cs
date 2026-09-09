using GameboyAdvanced.Core.Apu.Channels;

namespace GameboyAdvanced.Core.Apu;

/// <summary>
/// Cycle-based implementation of the four DMG-compatible sound channels in
/// the GBA. The PSG is clocked at 4.194304 MHz (one quarter of the ARM7 clock)
/// and shares the familiar 512 Hz eight-step frame sequencer with the GB.
/// </summary>
public unsafe partial class Apu
{
    private const int PsgCyclesPerSample = CyclesPerSample / 4;
    private const int PsgCyclesPerFrameSequencerStep = 8_192;
    private static readonly byte[] DutyPatterns = { 0x01, 0x81, 0x87, 0x7E };
    private static readonly int[] NoiseDivisors = { 8, 16, 32, 48, 64, 80, 96, 112 };

    private readonly bool[] _hostPsgChannelEnabled = { true, true, true, true };
    private readonly bool[] _psgChannelActive = new bool[4];
    private readonly int[] _psgFrequencyTimer = new int[4];
    private readonly int[] _psgPosition = new int[4];
    private readonly int[] _psgLengthCounter = new int[4];
    private readonly int[] _psgCurrentVolume = new int[4];
    private readonly int[] _psgEnvelopeTimer = new int[4];

    private int _psgFrameSequencerStep;
    private int _psgCyclesUntilFrameSequencer = PsgCyclesPerFrameSequencerStep;
    private int _sweepTimer;
    private int _sweepShadowFrequency;
    private bool _sweepEnabled;
    private bool _sweepNegateUsed;
    private int _noiseLfsr = 0x7FFF;

    /// <summary>
    /// Applies host-side mute switches without changing emulated registers or
    /// channel timing. This mirrors the channel controls of AetherBoy's GB core.
    /// </summary>
    public void ConfigurePsgChannels(bool channel1, bool channel2, bool channel3, bool channel4)
    {
        _hostPsgChannelEnabled[0] = channel1;
        _hostPsgChannelEnabled[1] = channel2;
        _hostPsgChannelEnabled[2] = channel3;
        _hostPsgChannelEnabled[3] = channel4;
    }

    public bool IsPsgChannelHostEnabled(int index) =>
        _hostPsgChannelEnabled[ValidateChannelIndex(index)];

    public bool IsPsgChannelActive(int index) =>
        _psgChannelActive[ValidateChannelIndex(index)];

    public int GetPsgCurrentVolume(int index) =>
        _psgCurrentVolume[ValidateChannelIndex(index)];

    public int GetPsgLengthCounter(int index) =>
        _psgLengthCounter[ValidateChannelIndex(index)];

    public int NoiseLfsr => _noiseLfsr;

    public float GetPsgFrequencyHz(int index)
    {
        index = ValidateChannelIndex(index);
        if (index < 2)
        {
            int frequency = ((ToneChannel)_channels[index])._frequency & 0x7FF;
            return 131_072f / Math.Max(1, 2_048 - frequency);
        }
        if (index == 2)
        {
            int frequency = ((SoundChannel3)_channels[2])._sampleRate & 0x7FF;
            return 65_536f / Math.Max(1, 2_048 - frequency);
        }

        SoundChannel4 noise = (SoundChannel4)_channels[3];
        return 4_194_304f /
            (NoiseDivisors[noise._ratio & 7] << Math.Clamp(noise._shiftClockFrequency, 0, 15));
    }

    private static int ValidateChannelIndex(int index)
    {
        if ((uint)index >= 4)
            throw new ArgumentOutOfRangeException(nameof(index));
        return index;
    }

    private void ResetPsgRuntime()
    {
        Array.Clear(_psgChannelActive);
        Array.Clear(_psgFrequencyTimer);
        Array.Clear(_psgPosition);
        Array.Clear(_psgLengthCounter);
        Array.Clear(_psgCurrentVolume);
        Array.Clear(_psgEnvelopeTimer);
        _psgFrameSequencerStep = 0;
        _psgCyclesUntilFrameSequencer = PsgCyclesPerFrameSequencerStep;
        _sweepTimer = 0;
        _sweepShadowFrequency = 0;
        _sweepEnabled = false;
        _sweepNegateUsed = false;
        _noiseLfsr = 0x7FFF;
    }

    private void HandlePsgRegisterWrite(uint address)
    {
        switch (address)
        {
            case IORegs.SOUND1CNT_H:
                LoadLength(0, 64 - (((ToneChannel)_channels[0])._length & 0x3F));
                break;
            case IORegs.SOUND1CNT_H + 1:
                ApplyDacState(0);
                break;
            case IORegs.SOUND1CNT_X + 1:
                TriggerIfRequested(0);
                break;
            case IORegs.SOUND2CNT_L:
                LoadLength(1, 64 - (((ToneChannel)_channels[1])._length & 0x3F));
                break;
            case IORegs.SOUND2CNT_L + 1:
                ApplyDacState(1);
                break;
            case IORegs.SOUND2CNT_H + 1:
                TriggerIfRequested(1);
                break;
            case IORegs.SOUND3CNT_L:
                ApplyDacState(2);
                break;
            case IORegs.SOUND3CNT_H:
                LoadLength(2, 256 - (((SoundChannel3)_channels[2])._length & 0xFF));
                break;
            case IORegs.SOUND3CNT_X + 1:
                TriggerIfRequested(2);
                break;
            case IORegs.SOUND4CNT_L:
                LoadLength(3, 64 - (((SoundChannel4)_channels[3])._length & 0x3F));
                break;
            case IORegs.SOUND4CNT_L + 1:
                ApplyDacState(3);
                break;
            case IORegs.SOUND4CNT_H + 1:
                TriggerIfRequested(3);
                break;
        }
    }

    private void LoadLength(int channel, int length)
    {
        _psgLengthCounter[channel] = length;
    }

    private void ApplyDacState(int channel)
    {
        if (!DacEnabled(channel))
            _psgChannelActive[channel] = false;
    }

    private bool DacEnabled(int channel) => channel switch
    {
        0 or 1 =>
            ((ToneChannel)_channels[channel])._envelope.InitialVolume != 0 ||
            ((ToneChannel)_channels[channel])._envelope.IsIncrease,
        2 => ((SoundChannel3)_channels[2])._on,
        3 =>
            ((SoundChannel4)_channels[3])._envelope.InitialVolume != 0 ||
            ((SoundChannel4)_channels[3])._envelope.IsIncrease,
        _ => false,
    };

    private void TriggerIfRequested(int channel)
    {
        if (!_channels[channel]._restartScheduled)
            return;
        _channels[channel]._restartScheduled = false;

        int maximumLength = channel == 2 ? 256 : 64;
        if (_psgLengthCounter[channel] == 0)
            _psgLengthCounter[channel] = maximumLength;

        _psgChannelActive[channel] = DacEnabled(channel);
        _psgPosition[channel] = 0;
        _psgFrequencyTimer[channel] = FrequencyPeriod(channel);

        if (channel is 0 or 1)
        {
            ToneChannel tone = (ToneChannel)_channels[channel];
            _psgCurrentVolume[channel] = tone._envelope.InitialVolume;
            _psgEnvelopeTimer[channel] = EnvelopePeriod(tone._envelope.EnvelopeStepTime);
        }
        else if (channel == 2)
        {
            _psgCurrentVolume[channel] = 0;
        }
        else
        {
            SoundChannel4 noise = (SoundChannel4)_channels[3];
            _psgCurrentVolume[channel] = noise._envelope.InitialVolume;
            _psgEnvelopeTimer[channel] = EnvelopePeriod(noise._envelope.EnvelopeStepTime);
            _noiseLfsr = 0x7FFF;
        }

        if (channel == 0)
        {
            SoundChannel1 pulse = (SoundChannel1)_channels[0];
            _sweepShadowFrequency = pulse._frequency & 0x7FF;
            _sweepTimer = EnvelopePeriod(pulse._sweepUnit._sweepTime);
            _sweepEnabled = pulse._sweepUnit._sweepTime != 0 ||
                pulse._sweepUnit.NumberOfSweepShift != 0;
            _sweepNegateUsed = false;
            if (pulse._sweepUnit.NumberOfSweepShift != 0)
                _ = CalculateSweepFrequency(disableOnOverflow: true);
        }
    }

    private static int EnvelopePeriod(int period) => period == 0 ? 8 : period;

    private int FrequencyPeriod(int channel) => channel switch
    {
        0 or 1 => Math.Max(4, (2_048 - (((ToneChannel)_channels[channel])._frequency & 0x7FF)) * 4),
        2 => Math.Max(2, (2_048 - (((SoundChannel3)_channels[2])._sampleRate & 0x7FF)) * 2),
        3 => NoisePeriod(),
        _ => throw new ArgumentOutOfRangeException(nameof(channel)),
    };

    private int NoisePeriod()
    {
        SoundChannel4 noise = (SoundChannel4)_channels[3];
        return NoiseDivisors[noise._ratio & 7] << Math.Clamp(noise._shiftClockFrequency, 0, 15);
    }

    private void AdvancePsg()
    {
        for (int channel = 0; channel < 4; channel++)
        {
            if (!_psgChannelActive[channel])
                continue;

            _psgFrequencyTimer[channel] -= PsgCyclesPerSample;
            int period = FrequencyPeriod(channel);
            while (_psgFrequencyTimer[channel] <= 0)
            {
                _psgFrequencyTimer[channel] += period;
                if (channel < 2)
                {
                    _psgPosition[channel] = (_psgPosition[channel] + 1) & 7;
                }
                else if (channel == 2)
                {
                    SoundChannel3 wave = (SoundChannel3)_channels[2];
                    int sampleCount = wave._isTwoBankRam ? 64 : 32;
                    _psgPosition[channel] = (_psgPosition[channel] + 1) % sampleCount;
                }
                else
                {
                    int feedback = (_noiseLfsr & 1) ^ ((_noiseLfsr >> 1) & 1);
                    _noiseLfsr = (_noiseLfsr >> 1) | (feedback << 14);
                    if (((SoundChannel4)_channels[3])._isShortWidth)
                        _noiseLfsr = (_noiseLfsr & ~(1 << 6)) | (feedback << 6);
                }
            }
        }

        _psgCyclesUntilFrameSequencer -= PsgCyclesPerSample;
        while (_psgCyclesUntilFrameSequencer <= 0)
        {
            _psgCyclesUntilFrameSequencer += PsgCyclesPerFrameSequencerStep;
            ClockFrameSequencer();
        }
    }

    private void ClockFrameSequencer()
    {
        if ((_psgFrameSequencerStep & 1) == 0)
            ClockLengths();
        if (_psgFrameSequencerStep is 2 or 6)
            ClockSweep();
        if (_psgFrameSequencerStep == 7)
            ClockEnvelopes();
        _psgFrameSequencerStep = (_psgFrameSequencerStep + 1) & 7;
    }

    private void ClockLengths()
    {
        for (int channel = 0; channel < 4; channel++)
        {
            if (!_psgChannelActive[channel] || !_channels[channel]._lengthFlag ||
                _psgLengthCounter[channel] <= 0)
            {
                continue;
            }

            _psgLengthCounter[channel]--;
            if (_psgLengthCounter[channel] == 0)
                _psgChannelActive[channel] = false;
        }
    }

    private void ClockEnvelopes()
    {
        ClockEnvelope(0, ((ToneChannel)_channels[0])._envelope);
        ClockEnvelope(1, ((ToneChannel)_channels[1])._envelope);
        ClockEnvelope(3, ((SoundChannel4)_channels[3])._envelope);
    }

    private void ClockEnvelope(int channel, Units.Envelope envelope)
    {
        if (!_psgChannelActive[channel] || envelope.EnvelopeStepTime == 0)
            return;
        if (--_psgEnvelopeTimer[channel] > 0)
            return;

        _psgEnvelopeTimer[channel] = EnvelopePeriod(envelope.EnvelopeStepTime);
        int next = _psgCurrentVolume[channel] + (envelope.IsIncrease ? 1 : -1);
        if ((uint)next <= 15)
            _psgCurrentVolume[channel] = next;
    }

    private void ClockSweep()
    {
        if (!_sweepEnabled || !_psgChannelActive[0])
            return;
        if (--_sweepTimer > 0)
            return;

        SoundChannel1 pulse = (SoundChannel1)_channels[0];
        _sweepTimer = EnvelopePeriod(pulse._sweepUnit._sweepTime);
        if (pulse._sweepUnit._sweepTime == 0)
            return;

        int next = CalculateSweepFrequency(disableOnOverflow: true);
        if (!_psgChannelActive[0] || pulse._sweepUnit.NumberOfSweepShift == 0)
            return;

        _sweepShadowFrequency = next;
        pulse._frequency = next;
        _ = CalculateSweepFrequency(disableOnOverflow: true);
    }

    private int CalculateSweepFrequency(bool disableOnOverflow)
    {
        SoundChannel1 pulse = (SoundChannel1)_channels[0];
        int delta = _sweepShadowFrequency >> pulse._sweepUnit.NumberOfSweepShift;
        int next;
        if (pulse._sweepUnit.IsDecrease)
        {
            _sweepNegateUsed = true;
            next = _sweepShadowFrequency - delta;
        }
        else
        {
            next = _sweepShadowFrequency + delta;
        }

        if (disableOnOverflow && (uint)next > 0x7FF)
            _psgChannelActive[0] = false;
        return next;
    }

    private void RenderMixedSample(out short left, out short right)
    {
        AdvancePsg();
        int leftMix = 0;
        int rightMix = 0;

        if (_psgFifoMasterEnable)
        {
            for (int channel = 0; channel < 4; channel++)
            {
                if (!_psgChannelActive[channel] || !_hostPsgChannelEnabled[channel])
                    continue;
                int sample = PsgSample(channel);
                if (_soundControlRegister.GbChannelEnableFlagsLeft[channel])
                    leftMix += sample * (_soundControlRegister.Sound1to4MasterVolumeLeft + 1);
                if (_soundControlRegister.GbChannelEnableFlagsRight[channel])
                    rightMix += sample * (_soundControlRegister.Sound1to4MasterVolumeRight + 1);
            }

            int psgShift = _soundControlRegister.Sound1to4Volume switch
            {
                0 => 2,
                1 => 1,
                _ => 0,
            };
            leftMix = (leftMix >> psgShift) * 32;
            rightMix = (rightMix >> psgShift) * 32;

            foreach (DmaChannel channel in _dmaChannels)
            {
                if (channel.EnableLeft)
                    leftMix += channel.CurrentValue * 64;
                if (channel.EnableRight)
                    rightMix += channel.CurrentValue * 64;
            }
        }

        left = (short)Math.Clamp(leftMix, short.MinValue, short.MaxValue);
        right = (short)Math.Clamp(rightMix, short.MinValue, short.MaxValue);
    }

    private int PsgSample(int channel)
    {
        if (channel < 2)
        {
            ToneChannel tone = (ToneChannel)_channels[channel];
            int bit = (DutyPatterns[tone._dutyPattern & 3] >> (7 - _psgPosition[channel])) & 1;
            return bit == 0 ? -_psgCurrentVolume[channel] : _psgCurrentVolume[channel];
        }
        if (channel == 2)
        {
            SoundChannel3 wave = (SoundChannel3)_channels[2];
            int position = _psgPosition[2];
            int bank = wave._waveRamBankIndex;
            if (wave._isTwoBankRam && position >= 32)
            {
                bank ^= 1;
                position -= 32;
            }
            int packed = wave._waveRamBanks[bank][position >> 1];
            int sample = (position & 1) == 0 ? packed >> 4 : packed & 0xF;
            int centeredSample = (sample * 2) - 15;
            if (wave._force75PctVolume)
                return centeredSample * 3 / 4;

            return wave._volume switch
            {
                0 => 0,
                1 => centeredSample,
                2 => centeredSample / 2,
                _ => centeredSample / 4,
            };
        }

        return (_noiseLfsr & 1) == 0
            ? _psgCurrentVolume[3]
            : -_psgCurrentVolume[3];
    }
}
