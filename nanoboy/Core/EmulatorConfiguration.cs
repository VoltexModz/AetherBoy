namespace nanoboy.Core
{
    public readonly record struct EmulatorConfiguration(
        int Frameskip,
        bool AudioEnabled,
        bool Channel1Enabled,
        bool Channel2Enabled,
        bool Channel3Enabled,
        bool Channel4Enabled,
        int SampleRate);
}
