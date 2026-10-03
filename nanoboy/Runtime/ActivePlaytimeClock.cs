namespace AetherBoy.Runtime;

/// <summary>Active wall time, never emulated/turbo time. Call from the frontend owner thread.</summary>
public sealed class ActivePlaytimeClock
{
    private long previous;
    private bool wasActive;

    public double Sample(long milliseconds, SessionState state, bool suppressed = false)
    {
        bool active = state == SessionState.Running && !suppressed;
        long elapsed = milliseconds - previous;
        double seconds = active && wasActive && elapsed is > 0 and < 2000 ? elapsed / 1000d : 0;
        previous = milliseconds;
        wasActive = active;
        return seconds;
    }

    public void Reset() => wasActive = false;
}
