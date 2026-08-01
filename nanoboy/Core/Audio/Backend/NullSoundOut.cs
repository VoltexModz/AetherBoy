namespace nanoboy.Core.Audio.Backend
{
    internal sealed class NullSoundOut : SoundOut
    {
        public NullSoundOut(Audio audio)
            : base(audio)
        {
        }

        public override void Dispose()
        {
        }
    }
}
