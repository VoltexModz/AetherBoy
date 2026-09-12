namespace AetherBoy.Runtime.Audio;

/// <summary>Use under the output lock. A new session outranks any generation of an older one.</summary>
public sealed class AudioPlaybackCursor
{
    private long session = -1, generation = -1;

    public bool TryAccept(long nextSession, long nextGeneration, out bool changed)
    {
        changed = false;
        if (nextSession < session || (nextSession == session && nextGeneration < generation)) return false;
        changed = nextSession != session || nextGeneration != generation;
        session = nextSession;
        generation = nextGeneration;
        return true;
    }
}
