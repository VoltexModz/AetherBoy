using AetherBoy.Runtime;

namespace AetherBoy.Desktop;

/// <summary>Pairs bounded audio blocks from the two machines; never queues an unbounded side.</summary>
internal sealed class LinuxLocalLinkAudioMixer
{
    private readonly object sync = new();
    private readonly float[]?[] pending = new float[2][];
    private readonly int[] rates = new int[2], channels = new int[2];
    private readonly long[] generations = new long[2], sessions = new long[2];

    internal (float[] Samples, int Rate, int Channels, long Generation, long Session)? Add(int player, AudioSamplesAvailableEventArgs audio)
    {
        if (player is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(player));
        lock (sync)
        {
            pending[player] = audio.GetInterleavedSamplesCopy();
            rates[player] = audio.SampleRate; channels[player] = audio.Channels;
            generations[player] = audio.PlaybackGeneration; sessions[player] = audio.PlaybackSession;
            int other = 1 - player;
            if (pending[other] is null) return null;
            if (rates[0] != rates[1] || channels[0] != channels[1] ||
                generations[0] != generations[1] || sessions[0] != sessions[1])
            {
                pending[other] = null; // A pause/restore boundary: never mix old and new audio.
                return null;
            }
            int length = Math.Min(pending[0]!.Length, pending[1]!.Length);
            float[] mixed = new float[length];
            for (int i = 0; i < length; i++)
                mixed[i] = Math.Clamp((pending[0]![i] + pending[1]![i]) * 0.5f, -1f, 1f);
            pending[0] = pending[1] = null;
            return (mixed, rates[0], channels[0], generations[0], sessions[0]);
        }
    }

    internal void Clear() { lock (sync) pending[0] = pending[1] = null; }
}
