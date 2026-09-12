namespace GameboyAdvanced.Core.Serial;

/// <summary>
/// A two-port GBA cable driven exclusively by the machines' emulated schedulers.
/// The first port is the multiplayer parent, the second is its child. Normal
/// serial modes instead use the internal clock selected by the software.
/// Both devices and this cable must be accessed on the same emulation thread.
/// </summary>
public sealed class LocalSerialLink : IDisposable, ISerialPeer
{
    private readonly SerialController _first;
    private readonly SerialController _second;
    private bool _connected = true;
    private bool _disposed;
    private bool _multiplayerActive;
    private bool _multiplayerHasChild;
    private bool _multiplayerError;
    private ushort _parentData;
    private ushort _childData;

    public bool Connected => !_disposed && _connected;
    public long ClockEdges { get; private set; }
    public long CompletedTransfers { get; private set; }
    public long AbortedTransfers { get; private set; }
    public long ContendedClockEdges { get; private set; }

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
        RefreshStatus();
    }

    /// <summary>Unplugs the virtual wire without resetting either serial unit.</summary>
    public void SetConnected(bool connected)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_connected == connected)
            return;
        _connected = connected;
        if (_multiplayerActive && !connected)
            _multiplayerError = true;
        RefreshStatus();
    }

    public int PlayerId(SerialController controller) => ReferenceEquals(controller, _first) ? 0 : 1;

    public bool NormalInputHigh(SerialController controller)
    {
        SerialController peer = Peer(controller);
        return !Connected || peer.Mode != controller.Mode || peer.NormalOutputHigh;
    }

    public void RefreshStatus()
    {
        bool ready = Connected && _first.Mode == 2 && _second.Mode == 2;
        _first.UpdateLinkStatus(0, Connected, ready);
        _second.UpdateLinkStatus(1, Connected, ready);
    }

    public void ClockNormal(SerialController source)
    {
        if (_disposed || !source.IsNormalClockSource)
            return;

        SerialController peer = Peer(source);
        bool peerParticipates = Connected && peer.Mode == source.Mode && peer._transferActive;
        // Resolve conflicting internal clocks deterministically instead of
        // clocking both registers twice for the same emulated wire edge.
        if (peerParticipates && peer.IsNormalClockSource && ReferenceEquals(source, _second))
        {
            source.ScheduleNormalClock();
            return;
        }

        bool sourceOut = source.NormalOutputHigh;
        bool peerOut = NormalInputHigh(source);
        if (peerParticipates && peer.IsNormalClockSource)
            ContendedClockEdges++;
        ClockEdges++;
        bool finished = source.ShiftNormalBit(peerOut);
        if (peerParticipates)
            _ = peer.ShiftNormalBit(sourceOut);
        if (finished)
            CompletedTransfers++;
        else
            source.ScheduleNormalClock();
    }

    public void BeginMultiplayer(SerialController controller)
    {
        if (!ReferenceEquals(controller, _first) || _multiplayerActive)
            return;

        _multiplayerActive = true;
        _multiplayerHasChild = Connected && _second.Mode == 2;
        _multiplayerError = !_multiplayerHasChild;
        // Latch both send words before either receive register is reset.
        _parentData = _first._sioData8;
        _childData = _multiplayerHasChild ? _second._sioData8 : ushort.MaxValue;
        _first.ArmMultiplayer();
        if (_multiplayerHasChild)
            _second.ArmMultiplayer();
        _first.ScheduleMultiplayerCompletion(_multiplayerHasChild);
    }

    public void CompleteMultiplayer(SerialController controller)
    {
        if (!_multiplayerActive || !ReferenceEquals(controller, _first))
            return;
        _multiplayerActive = false;
        bool error = _multiplayerError || !Connected;
        ushort childData = error ? ushort.MaxValue : _childData;
        _first.FinishMultiplayer(_parentData, childData, error);
        if (_multiplayerHasChild)
            _second.FinishMultiplayer(error ? ushort.MaxValue : _parentData, childData, error);
        // Counts data bits, excluding multiplayer UART framing/handshake bits.
        ClockEdges += _multiplayerHasChild ? 32 : 16;
        CompletedTransfers++;
        RefreshStatus();
    }

    public void Cancel(SerialController controller)
    {
        if (_multiplayerActive && (ReferenceEquals(controller, _first) || _multiplayerHasChild))
        {
            _multiplayerActive = false;
            AbortedTransfers++;
            _first.AbortTransfer(multiplayerError: true);
            if (_multiplayerHasChild)
                _second.AbortTransfer(multiplayerError: true);
        }
        RefreshStatus();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        Cancel(_first);
        _disposed = true;
        _first.Detach(this);
        _second.Detach(this);
    }

    private SerialController Peer(SerialController controller) =>
        ReferenceEquals(controller, _first) ? _second : _first;
}
