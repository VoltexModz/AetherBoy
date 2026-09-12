using System;

namespace AetherBoy.Runtime.Audio;

/// <summary>Keep queued audio contiguous under turbo instead of replacing it mid-playback.</summary>
public static class AudioQueuePolicy
{
    public static int AcceptedFrames(int queuedFrames, int incomingFrames, int capacityFrames)
    {
        if (queuedFrames < 0 || incomingFrames < 0 || capacityFrames <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacityFrames));
        // An oversized block may seed an empty queue, but can never exceed the bound.
        int accepted = Math.Min(incomingFrames, capacityFrames);
        return queuedFrames <= capacityFrames - accepted ? accepted : 0;
    }
}
