namespace GameboyAdvanced.Core.Serial;

/// <summary>
/// Deterministic, in-process cable for exactly two GBA serial controllers.
/// It performs no networking and never owns a timing thread; both emulators
/// remain driven by their normal schedulers.
/// </summary>
public sealed class LocalSerialLink : IDisposable
{
    private sealed record Pending(int Mode, uint Data, bool InternalClock, int Cycles);

    private readonly SerialController _first;
    private readonly SerialController _second;
    private Pending? _firstPending;
    private Pending? _secondPending;
    private bool _disposed;

    public LocalSerialLink(SerialController first, SerialController second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        if (ReferenceEquals(first, second))
            throw new ArgumentException("A local serial link needs two different GBA instances.", nameof(second));

        _first = first;
        _second = second;
        first.Attach(this);
        try
        {
            second.Attach(this);
        }
        catch
        {
            first.Detach(this);
            throw;
        }
    }

    internal void Begin(SerialController controller, int mode, uint data, bool internalClock, int cycles)
    {
        ThrowIfDisposed();
        var pending = new Pending(mode, data, internalClock, cycles);
        if (ReferenceEquals(controller, _first))
            _firstPending = pending;
        else if (ReferenceEquals(controller, _second))
            _secondPending = pending;
        else
            throw new InvalidOperationException("The serial controller is not attached to this link.");

        TryMatch();
    }

    internal void Cancel(SerialController controller)
    {
        if (ReferenceEquals(controller, _first))
            _firstPending = null;
        else if (ReferenceEquals(controller, _second))
            _secondPending = null;
    }

    internal void Complete(SerialController controller) => Cancel(controller);

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _firstPending = null;
        _secondPending = null;
        _first.Detach(this);
        _second.Detach(this);
    }

    private void TryMatch()
    {
        if (_firstPending is not { } first || _secondPending is not { } second)
            return;
        if (first.Mode != second.Mode || first.Mode is not (0 or 1))
            return;
        if (!first.InternalClock && !second.InternalClock)
            return;

        int cycles = first.InternalClock && second.InternalClock
            ? Math.Min(first.Cycles, second.Cycles)
            : first.InternalClock ? first.Cycles : second.Cycles;
        _first.ArmLinkedCompletion(second.Data, cycles);
        _second.ArmLinkedCompletion(first.Data, cycles);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
