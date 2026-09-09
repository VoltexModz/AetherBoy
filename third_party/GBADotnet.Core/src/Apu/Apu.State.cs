using GameboyAdvanced.Core.Apu.Channels;

namespace GameboyAdvanced.Core.Apu;

public unsafe partial class Apu
{
    internal void WriteState(BinaryWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.Write(InternalSampleBufferPtr);
        writer.Write(InternalSampleBuffer);
        writer.Write(_psgFifoMasterEnable);
        writer.Write(_biasLevel);
        writer.Write(_samplingCycle);
        writer.Write(_soundControlRegister.GetSoundCntL());
        writer.Write(_soundControlRegister.GetSoundCntH());
        writer.Write(_psgFrameSequencerStep);
        writer.Write(_psgCyclesUntilFrameSequencer);
        WriteIntArray(writer, _psgFrequencyTimer);
        WriteIntArray(writer, _psgPosition);
        WriteIntArray(writer, _psgLengthCounter);
        WriteIntArray(writer, _psgCurrentVolume);
        WriteIntArray(writer, _psgEnvelopeTimer);
        foreach (bool active in _psgChannelActive)
            writer.Write(active);
        writer.Write(_sweepTimer);
        writer.Write(_sweepShadowFrequency);
        writer.Write(_sweepEnabled);
        writer.Write(_sweepNegateUsed);
        writer.Write(_noiseLfsr);

        foreach (DmaChannel channel in _dmaChannels)
        {
            writer.Write(channel.CurrentValue);
            writer.Write(channel.FifoReadPtr);
            writer.Write(channel.FifoWritePtr);
            writer.Write(channel.Fifo);
            writer.Write(channel.FullVolume);
            writer.Write(channel.EnableRight);
            writer.Write(channel.EnableLeft);
            writer.Write(channel.SelectTimer1);
        }

        WriteCommonChannel(writer, _channels[0]);
        SoundChannel1 channel1 = (SoundChannel1)_channels[0];
        WriteToneChannel(writer, channel1);
        writer.Write(channel1._sweepUnit.NumberOfSweepShift);
        writer.Write(channel1._sweepUnit.IsDecrease);
        writer.Write(channel1._sweepUnit._sweepTime);

        WriteCommonChannel(writer, _channels[1]);
        WriteToneChannel(writer, (SoundChannel2)_channels[1]);

        WriteCommonChannel(writer, _channels[2]);
        SoundChannel3 channel3 = (SoundChannel3)_channels[2];
        writer.Write(channel3._length);
        writer.Write(channel3._isTwoBankRam);
        writer.Write(channel3._waveRamBankIndex);
        writer.Write(channel3._on);
        writer.Write(channel3._volume);
        writer.Write(channel3._force75PctVolume);
        writer.Write(channel3._sampleRate);
        writer.Write(channel3._waveRamBanks[0]);
        writer.Write(channel3._waveRamBanks[1]);

        WriteCommonChannel(writer, _channels[3]);
        SoundChannel4 channel4 = (SoundChannel4)_channels[3];
        writer.Write(channel4._length);
        writer.Write(channel4._ratio);
        writer.Write(channel4._isShortWidth);
        writer.Write(channel4._shiftClockFrequency);
        WriteEnvelope(writer, channel4._envelope);
    }

    internal void ReadState(BinaryReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        InternalSampleBufferPtr = reader.ReadInt32();
        if (InternalSampleBufferPtr < 0 ||
            InternalSampleBufferPtr >= InternalSampleBuffer.Length ||
            (InternalSampleBufferPtr & 3) != 0)
        {
            throw new InvalidDataException("The saved GBA audio buffer position is invalid.");
        }
        ReadExactly(reader, InternalSampleBuffer);
        _psgFifoMasterEnable = reader.ReadBoolean();
        _biasLevel = reader.ReadInt32();
        _samplingCycle = reader.ReadInt32();
        ushort soundControlL = reader.ReadUInt16();
        _soundControlRegister.SetSoundCntL((byte)soundControlL, 0);
        _soundControlRegister.SetSoundCntL((byte)(soundControlL >> 8), 1);
        ushort soundControlH = reader.ReadUInt16();
        _soundControlRegister.SetSoundCntH((byte)soundControlH, 0);
        _soundControlRegister.SetSoundCntH((byte)(soundControlH >> 8), 1);
        _psgFrameSequencerStep = reader.ReadInt32();
        _psgCyclesUntilFrameSequencer = reader.ReadInt32();
        ReadIntArray(reader, _psgFrequencyTimer);
        ReadIntArray(reader, _psgPosition);
        ReadIntArray(reader, _psgLengthCounter);
        ReadIntArray(reader, _psgCurrentVolume);
        ReadIntArray(reader, _psgEnvelopeTimer);
        for (int index = 0; index < _psgChannelActive.Length; index++)
            _psgChannelActive[index] = reader.ReadBoolean();
        _sweepTimer = reader.ReadInt32();
        _sweepShadowFrequency = reader.ReadInt32();
        _sweepEnabled = reader.ReadBoolean();
        _sweepNegateUsed = reader.ReadBoolean();
        _noiseLfsr = reader.ReadInt32();

        foreach (DmaChannel channel in _dmaChannels)
        {
            channel.CurrentValue = reader.ReadInt16();
            channel.FifoReadPtr = reader.ReadInt32();
            channel.FifoWritePtr = reader.ReadInt32();
            if (channel.FifoReadPtr is < 0 or > 32 ||
                channel.FifoWritePtr is < 0 or > 32 ||
                channel.FifoReadPtr > channel.FifoWritePtr)
            {
                throw new InvalidDataException("The saved GBA Direct Sound FIFO position is invalid.");
            }
            ReadExactly(reader, channel.Fifo);
            channel.FullVolume = reader.ReadBoolean();
            channel.EnableRight = reader.ReadBoolean();
            channel.EnableLeft = reader.ReadBoolean();
            channel.SelectTimer1 = reader.ReadBoolean();
        }

        ReadCommonChannel(reader, _channels[0]);
        SoundChannel1 channel1 = (SoundChannel1)_channels[0];
        ReadToneChannel(reader, channel1);
        channel1._sweepUnit.NumberOfSweepShift = reader.ReadInt32();
        channel1._sweepUnit.IsDecrease = reader.ReadBoolean();
        channel1._sweepUnit._sweepTime = reader.ReadInt32();

        ReadCommonChannel(reader, _channels[1]);
        ReadToneChannel(reader, (SoundChannel2)_channels[1]);

        ReadCommonChannel(reader, _channels[2]);
        SoundChannel3 channel3 = (SoundChannel3)_channels[2];
        channel3._length = reader.ReadInt32();
        channel3._isTwoBankRam = reader.ReadBoolean();
        channel3._waveRamBankIndex = reader.ReadInt32();
        channel3._on = reader.ReadBoolean();
        channel3._volume = reader.ReadInt32();
        channel3._force75PctVolume = reader.ReadBoolean();
        channel3._sampleRate = reader.ReadInt32();
        ReadExactly(reader, channel3._waveRamBanks[0]);
        ReadExactly(reader, channel3._waveRamBanks[1]);

        ReadCommonChannel(reader, _channels[3]);
        SoundChannel4 channel4 = (SoundChannel4)_channels[3];
        channel4._length = reader.ReadInt32();
        channel4._ratio = reader.ReadInt32();
        channel4._isShortWidth = reader.ReadBoolean();
        channel4._shiftClockFrequency = reader.ReadInt32();
        ReadEnvelope(reader, channel4._envelope);

        ValidatePsgState();
    }

    private static void WriteCommonChannel(BinaryWriter writer, GBSoundChannel channel)
    {
        writer.Write(channel._lengthFlag);
        writer.Write(channel._restartScheduled);
    }

    private static void ReadCommonChannel(BinaryReader reader, GBSoundChannel channel)
    {
        channel._lengthFlag = reader.ReadBoolean();
        channel._restartScheduled = reader.ReadBoolean();
    }

    private static void WriteToneChannel(BinaryWriter writer, ToneChannel channel)
    {
        writer.Write(channel._frequency);
        writer.Write(channel._length);
        writer.Write(channel._dutyPattern);
        WriteEnvelope(writer, channel._envelope);
    }

    private static void ReadToneChannel(BinaryReader reader, ToneChannel channel)
    {
        channel._frequency = reader.ReadInt32();
        channel._length = reader.ReadInt32();
        channel._dutyPattern = reader.ReadInt32();
        ReadEnvelope(reader, channel._envelope);
    }

    private static void WriteEnvelope(BinaryWriter writer, Units.Envelope envelope)
    {
        writer.Write(envelope.EnvelopeStepTime);
        writer.Write(envelope.IsIncrease);
        writer.Write(envelope.InitialVolume);
    }

    private static void ReadEnvelope(BinaryReader reader, Units.Envelope envelope)
    {
        envelope.EnvelopeStepTime = reader.ReadInt32();
        envelope.IsIncrease = reader.ReadBoolean();
        envelope.InitialVolume = reader.ReadInt32();
    }

    private void ValidatePsgState()
    {
        if ((uint)_psgFrameSequencerStep > 7 ||
            _psgCyclesUntilFrameSequencer is < 1 or > PsgCyclesPerFrameSequencerStep)
        {
            throw new InvalidDataException("The saved GBA PSG frame sequencer is invalid.");
        }
        if (_sweepTimer is < 0 or > 8 || (uint)_sweepShadowFrequency > 0x7FF ||
            (uint)_noiseLfsr > 0x7FFF)
        {
            throw new InvalidDataException("The saved GBA PSG sweep/noise state is invalid.");
        }

        for (int channel = 0; channel < 4; channel++)
        {
            int maximumLength = channel == 2 ? 256 : 64;
            int maximumPosition = channel switch { 0 or 1 => 7, 2 => 63, _ => 0 };
            if (_psgFrequencyTimer[channel] < 0 ||
                _psgFrequencyTimer[channel] > FrequencyPeriod(channel) ||
                _psgPosition[channel] < 0 || _psgPosition[channel] > maximumPosition ||
                _psgLengthCounter[channel] < 0 || _psgLengthCounter[channel] > maximumLength ||
                (uint)_psgCurrentVolume[channel] > 15 ||
                _psgEnvelopeTimer[channel] is < 0 or > 8 ||
                (_psgChannelActive[channel] && !DacEnabled(channel)))
            {
                throw new InvalidDataException($"The saved GBA PSG channel {channel + 1} state is invalid.");
            }
        }
    }

    private static void WriteIntArray(BinaryWriter writer, int[] values)
    {
        foreach (int value in values)
            writer.Write(value);
    }

    private static void ReadIntArray(BinaryReader reader, int[] destination)
    {
        for (int index = 0; index < destination.Length; index++)
            destination[index] = reader.ReadInt32();
    }

    private static void ReadExactly(BinaryReader reader, byte[] destination)
    {
        int read = reader.Read(destination, 0, destination.Length);
        if (read != destination.Length)
            throw new EndOfStreamException("The GBA APU state ended unexpectedly.");
    }
}
