namespace GameboyAdvanced.Core.Debug;

public enum GbaDiagnosticCategory
{
    BiosCall,
    UnknownOpcode,
    RegisterAccess,
    SerialLink
}

public readonly record struct GbaDiagnosticEvent(
    long Cycle,
    GbaDiagnosticCategory Category,
    string Message,
    uint? Address = null);

/// <summary>
/// Bounded local event buffer. It stores no ROM bytes, register values,
/// file paths or network identifiers and performs no telemetry or I/O.
/// </summary>
public sealed class GbaDiagnosticLog
{
    private const int Capacity = 128;
    private const int OnceKeyCapacity = 512;
    private readonly object _gate = new();
    private readonly Queue<GbaDiagnosticEvent> _events = new(Capacity);
    private readonly HashSet<string> _onceKeys = new(StringComparer.Ordinal);

    public void Record(long cycle, GbaDiagnosticCategory category, string message, uint? address = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        lock (_gate)
        {
            if (_events.Count == Capacity)
                _events.Dequeue();
            _events.Enqueue(new GbaDiagnosticEvent(cycle, category, message, address));
        }
    }

    public void RecordOnce(long cycle, GbaDiagnosticCategory category, string message, uint? address = null)
    {
        string key = $"{category}:{address:X8}:{message}";
        lock (_gate)
        {
            if (_onceKeys.Count >= OnceKeyCapacity)
                _onceKeys.Clear();
            if (!_onceKeys.Add(key))
                return;
            if (_events.Count == Capacity)
                _events.Dequeue();
            _events.Enqueue(new GbaDiagnosticEvent(cycle, category, message, address));
        }
    }

    public GbaDiagnosticEvent[] Snapshot()
    {
        lock (_gate)
            return _events.ToArray();
    }

    public void Clear()
    {
        lock (_gate)
        {
            _events.Clear();
            _onceKeys.Clear();
        }
    }
}
