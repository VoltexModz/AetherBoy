using GameboyAdvanced.Core.Debug;
using GameboyAdvanced.Core.Interrupts;
using static GameboyAdvanced.Core.IORegs;

namespace GameboyAdvanced.Core.Serial;

/// <summary>
/// GBA normal 8/32-bit and two-player 16-bit serial hardware. UART, GPIO
/// accessories and Joybus are not simulated by the local cable.
/// </summary>
public unsafe class SerialController
{
    private readonly Device _device;
    private readonly InterruptInterconnect _interruptInterconnect;

    public uint _sioData32 = uint.MaxValue;
    public uint _sioMultiHigh = uint.MaxValue;
    public ushort _sioCnt;
    public ushort _sioData8 = 0x00FF;
    public ushort _rcnt = 0x8000;
    public ushort _joyCnt;
    public uint _joyReceive = uint.MaxValue;
    public uint _joyTransmit;
    public ushort _joyStat;
    public bool _transferActive;
    private ISerialPeer? _link;
    private bool _completionScheduled;
    private bool _peerReceiveReady;
    private uint _peerReceiveData = uint.MaxValue;
    private int _normalBitsRemaining;
    private int _normalCyclesPerBit;

    public bool IsLinked => _link is not null;
    public int Mode => (_rcnt & 0x8000) == 0 ? (_sioCnt >> 12) & 3 : -1;
    public long EmulatedCycles => _device.Cpu.Cycles;
    internal bool IsNormalClockSource => _transferActive && Mode is 0 or 1 && (_sioCnt & 1) != 0;
    internal bool NormalOutputHigh => _transferActive && Mode is 0 or 1
        ? Mode == 0 ? (_sioData8 & 0x80) != 0 : (_sioData32 & 0x8000_0000) != 0
        : (_sioCnt & 8) != 0;

    internal SerialController(Device device, BaseDebugger debugger, InterruptInterconnect interruptInterconnect)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        ArgumentNullException.ThrowIfNull(debugger);
        _interruptInterconnect = interruptInterconnect ?? throw new ArgumentNullException(nameof(interruptInterconnect));
    }

    internal void Reset()
    {
        _link?.Cancel(this);
        AbortTransfer();
        _sioData32 = uint.MaxValue;
        _sioMultiHigh = uint.MaxValue;
        _sioCnt = 0;
        _sioData8 = 0x00FF;
        _rcnt = 0x8000;
        _joyCnt = 0;
        _joyReceive = uint.MaxValue;
        _joyTransmit = 0;
        _joyStat = 0;
        _peerReceiveData = uint.MaxValue;
        _link?.RefreshStatus();
    }

    internal byte ReadByte(uint address)
    {
        if (address is SIOCNT or SIOCNT + 1 or RCNT or RCNT + 1)
            RefreshStatus();
        return address switch
        {
            >= SIODATA32 and <= SIODATA32 + 3 => (byte)(_sioData32 >> (int)((address - SIODATA32) * 8)),
            >= SIOMULTI2 and <= SIOMULTI3 + 1 => (byte)(_sioMultiHigh >> (int)((address - SIOMULTI2) * 8)),
            SIOCNT => (byte)_sioCnt,
            SIOCNT + 1 => (byte)(_sioCnt >> 8),
            SIODATA8 => (byte)_sioData8,
            SIODATA8 + 1 => (byte)(_sioData8 >> 8),
            RCNT => (byte)_rcnt,
            RCNT + 1 => (byte)(_rcnt >> 8),
            JOYCNT => (byte)_joyCnt,
            JOYCNT + 1 => (byte)(_joyCnt >> 8),
            >= JOY_RECV and <= JOY_RECV + 3 => (byte)(_joyReceive >> (int)((address - JOY_RECV) * 8)),
            >= JOY_TRANS and <= JOY_TRANS + 3 => (byte)(_joyTransmit >> (int)((address - JOY_TRANS) * 8)),
            JOYSTAT => (byte)_joyStat,
            JOYSTAT + 1 => (byte)(_joyStat >> 8),
            _ => 0,
        };
    }

    internal ushort ReadHalfWord(uint address) => (ushort)(ReadByte(address) | (ReadByte(address + 1) << 8));
    internal uint ReadWord(uint address) => (uint)(ReadHalfWord(address) | (ReadHalfWord(address + 2) << 16));

    internal void WriteByte(uint address, byte value)
    {
        switch (address)
        {
            case >= SIODATA32 and <= SIODATA32 + 3:
                int dataShift = (int)(address - SIODATA32) * 8;
                _sioData32 = (_sioData32 & ~(0xFFu << dataShift)) | ((uint)value << dataShift);
                break;
            case >= SIOMULTI2 and <= SIOMULTI3 + 1:
                int highShift = (int)(address - SIOMULTI2) * 8;
                _sioMultiHigh = (_sioMultiHigh & ~(0xFFu << highShift)) | ((uint)value << highShift);
                break;
            case SIOCNT:
                WriteControl((ushort)((_sioCnt & 0xFF00) | value));
                break;
            case SIOCNT + 1:
                WriteControl((ushort)((_sioCnt & 0x00FF) | (value << 8)));
                break;
            case SIODATA8:
                _sioData8 = (ushort)((_sioData8 & 0xFF00) | value);
                break;
            case SIODATA8 + 1:
                _sioData8 = (ushort)((_sioData8 & 0x00FF) | (value << 8));
                break;
            case RCNT:
                WriteRcnt((ushort)((_rcnt & 0xFF00) | value));
                break;
            case RCNT + 1:
                WriteRcnt((ushort)((_rcnt & 0x00FF) | (value << 8)));
                break;
            case JOYCNT:
                _joyCnt = (ushort)((_joyCnt & 0xFF00) | (value & 0x47));
                break;
            case JOYCNT + 1:
                _joyCnt = (ushort)((_joyCnt & 0x00FF) | ((value & 0x40) << 8));
                break;
            case >= JOY_TRANS and <= JOY_TRANS + 3:
                int joyShift = (int)(address - JOY_TRANS) * 8;
                _joyTransmit = (_joyTransmit & ~(0xFFu << joyShift)) | ((uint)value << joyShift);
                break;
            case JOYSTAT:
                _joyStat = (ushort)((_joyStat & 0xFF00) | (value & 0x30));
                break;
        }
    }

    internal void WriteHalfWord(uint address, ushort value)
    {
        if (address == SIOCNT)
            WriteControl(value);
        else if (address == RCNT)
            WriteRcnt(value);
        else
        {
            WriteByte(address, (byte)value);
            WriteByte(address + 1, (byte)(value >> 8));
        }
    }

    internal void WriteWord(uint address, uint value)
    {
        WriteHalfWord(address, (ushort)value);
        WriteHalfWord(address + 2, (ushort)(value >> 16));
    }

    private void WriteRcnt(ushort value)
    {
        int previousMode = Mode;
        _rcnt = (ushort)((value & 0xC1F0) | (_rcnt & 0xF));
        if (previousMode != Mode)
        {
            _link?.Cancel(this);
            AbortTransfer();
        }
        RefreshStatus();
    }

    private void WriteControl(ushort value)
    {
        int previousMode = Mode;
        bool wasActive = _transferActive;
        int newMode = (_rcnt & 0x8000) == 0 ? (value >> 12) & 3 : -1;
        if (newMode != previousMode)
        {
            _link?.Cancel(this);
            AbortTransfer();
            wasActive = false;
        }

        if (newMode == 2)
        {
            bool slave = _link?.PlayerId(this) == 1;
            // Hardware owns ready, identity and error; slaves also cannot write busy.
            ushort writable = (ushort)(value & 0x7F83);
            if (slave)
                writable = (ushort)((writable & ~0x80) | (_sioCnt & 0x80));
            _sioCnt = (ushort)(writable | (_sioCnt & 0x7C));
        }
        else if (newMode is 0 or 1)
            _sioCnt = (ushort)((value & 0x7F8B) | (_sioCnt & 4));
        else
            _sioCnt = (ushort)(value & 0x7FFF);

        RefreshStatus();
        if (newMode is not (0 or 1 or 2))
            return;
        if ((_sioCnt & 0x80) == 0)
        {
            _link?.Cancel(this);
            AbortTransfer();
            return;
        }
        if (wasActive)
        {
            // Software can switch an armed external-clock transfer to its own
            // clock without clearing busy. Do not leave that transfer stranded.
            if (newMode is 0 or 1)
            {
                int newCyclesPerBit = (_sioCnt & 2) != 0 ? 8 : 64;
                bool internalClock = (_sioCnt & 1) != 0;
                if (_completionScheduled && (!internalClock || newCyclesPerBit != _normalCyclesPerBit))
                {
                    _device.Scheduler.CancelEvent(EventType.SerialTransfer);
                    _completionScheduled = false;
                }
                _normalCyclesPerBit = newCyclesPerBit;
                if (internalClock && !_completionScheduled)
                {
                    if (_link is not null)
                        ScheduleNormalClock();
                    else
                        ScheduleCompletion(_normalBitsRemaining * _normalCyclesPerBit);
                }
            }
            return;
        }
        if (newMode == 2)
        {
            if (_link is not null)
                _link.BeginMultiplayer(this);
            else
            {
                _peerReceiveData = _sioData8;
                ArmMultiplayer();
                ScheduleMultiplayerCompletion(hasChild: false);
            }
        }
        else
        {
            _transferActive = true;
            _peerReceiveReady = false;
            _normalBitsRemaining = newMode == 0 ? 8 : 32;
            _normalCyclesPerBit = (_sioCnt & 2) != 0 ? 8 : 64;
            if ((_sioCnt & 1) != 0)
            {
                if (_link is not null)
                    ScheduleNormalClock();
                else
                    ScheduleCompletion(_normalBitsRemaining * _normalCyclesPerBit);
            }
        }
    }

    private void RefreshStatus()
    {
        if (_link is not null)
            _link.RefreshStatus();
        else
            UpdateLinkStatus(0, connected: false, ready: false);
    }

    public void UpdateLinkStatus(int playerId, bool connected, bool ready)
    {
        if (Mode == 2)
        {
            _sioCnt = (ushort)((_sioCnt & ~0x3C) | (playerId << 4) |
                (playerId != 0 || !connected ? 4 : 0) | (ready ? 8 : 0));
            _rcnt = (ushort)((_rcnt & ~7) | (_transferActive ? 0 : 1) |
                (ready ? 2 : 0) | (playerId != 0 || !connected ? 4 : 0));
        }
        else if (Mode is 0 or 1)
        {
            bool inputHigh = _link?.NormalInputHigh(this) ?? true;
            _sioCnt = (ushort)((_sioCnt & ~4) | (inputHigh ? 4 : 0));
            _rcnt = (ushort)((_rcnt & ~0xD) | ((_sioCnt & 1) != 0 ? 1 : 0) |
                (inputHigh ? 4 : 0) | (NormalOutputHigh ? 8 : 0));
        }
    }

    public void ScheduleNormalClock()
    {
        if (IsNormalClockSource && !_completionScheduled)
            ScheduleCompletion(_normalCyclesPerBit);
    }

    private void ScheduleCompletion(int cycles)
    {
        _device.Scheduler.ScheduleEvent(EventType.SerialTransfer, &CompleteTransferEvent, cycles);
        _completionScheduled = true;
    }

    internal static void CompleteTransferEvent(Device device) => device.SerialController.CompleteTransfer();

    private void CompleteTransfer()
    {
        _completionScheduled = false;
        if (!_transferActive)
            return;
        if (Mode is 0 or 1 && _link is not null)
        {
            _link.ClockNormal(this);
            return;
        }
        if (Mode == 2)
        {
            if (_link is not null)
                _link.CompleteMultiplayer(this);
            else
                FinishMultiplayer((ushort)_peerReceiveData, ushort.MaxValue, error: true);
            return;
        }
        if (Mode == 0)
            _sioData8 = _peerReceiveReady ? (ushort)(_peerReceiveData & 0xFF) : (ushort)0x00FF;
        else if (Mode == 1)
            _sioData32 = _peerReceiveReady ? _peerReceiveData : uint.MaxValue;
        FinishTransfer();
    }

    public bool ShiftNormalBit(bool inputHigh)
    {
        if (!_transferActive || Mode is not (0 or 1))
            return false;
        if (Mode == 0)
            _sioData8 = (byte)((_sioData8 << 1) | (inputHigh ? 1 : 0));
        else
            _sioData32 = (_sioData32 << 1) | (inputHigh ? 1u : 0u);
        if (--_normalBitsRemaining != 0)
            return false;
        FinishTransfer();
        return true;
    }

    public void ArmMultiplayer()
    {
        AbortTransfer();
        _transferActive = true;
        _sioCnt = (ushort)((_sioCnt | 0x80) & ~0x40);
        _sioData32 = uint.MaxValue;
        _sioMultiHigh = uint.MaxValue;
        RefreshStatus();
    }

    public void ScheduleMultiplayerCompletion(bool hasChild)
    {
        // Whole-transfer CPU-cycle totals include framing, not merely 16 / baud.
        // Timing reference: https://github.com/mgba-emu/mgba/blob/master/src/gba/sio.c
        // Only the one- and two-port hardware cases belong to this cable.
        int cycles = ((_sioCnt & 3), hasChild) switch
        {
            (0, false) => 31_976, (0, true) => 63_427,
            (1, false) => 8_378, (1, true) => 16_241,
            (2, false) => 5_750, (2, true) => 10_998,
            (3, false) => 3_140, _ => 5_755,
        };
        ScheduleCompletion(cycles);
    }

    public void FinishMultiplayer(ushort parent, ushort child, bool error)
    {
        if (!_transferActive || Mode != 2)
            return;
        _sioData32 = (uint)(parent | (child << 16));
        _sioMultiHigh = uint.MaxValue;
        _sioCnt = (ushort)((_sioCnt & ~0x40) | (error ? 0x40 : 0));
        _device.Diagnostics.Record(_device.Cpu.Cycles, GbaDiagnosticCategory.SerialLink,
            error ? "Multiplayer transfer ended with a connection error" : "16-bit multiplayer transfer completed");
        FinishTransfer();
    }

    private void FinishTransfer()
    {
        AbortTransfer();
        RefreshStatus();
        if ((_sioCnt & 0x4000) != 0)
            _interruptInterconnect.RaiseInterrupt(Interrupt.SerialCommunication);
    }

    public void AbortTransfer(bool multiplayerError = false)
    {
        if (_completionScheduled)
            _device.Scheduler.CancelEvent(EventType.SerialTransfer);
        _completionScheduled = false;
        _transferActive = false;
        _peerReceiveReady = false;
        _normalBitsRemaining = 0;
        _sioCnt = (ushort)((_sioCnt & ~0x80) | (multiplayerError ? 0x40 : 0));
    }

    public void Attach(ISerialPeer link)
    {
        ArgumentNullException.ThrowIfNull(link);
        if (_link is not null)
            throw new InvalidOperationException("This GBA serial controller is already linked.");
        if (_transferActive)
            throw new InvalidOperationException("Attach the GBA cable before starting a serial transfer.");
        _link = link;
    }

    public void Detach(ISerialPeer link)
    {
        if (!ReferenceEquals(_link, link))
            return;
        AbortTransfer();
        _link = null;
        RefreshStatus();
    }

    internal void EnsureStandaloneState()
    {
        if (IsLinked)
            throw new InvalidOperationException("A linked GBA machine cannot capture or restore a unilateral save state.");
    }

    internal void WriteState(BinaryWriter writer)
    {
        EnsureStandaloneState();
        writer.Write(_sioData32);
        writer.Write(_sioCnt);
        writer.Write(_sioData8);
        writer.Write(_rcnt);
        writer.Write(_joyCnt);
        writer.Write(_joyReceive);
        writer.Write(_joyTransmit);
        writer.Write(_joyStat);
        writer.Write(_transferActive);
        writer.Write(_completionScheduled);
        writer.Write(_peerReceiveReady);
        writer.Write(_peerReceiveData);
        writer.Write(_sioMultiHigh);
    }

    internal void ReadState(BinaryReader reader, ushort schemaVersion)
    {
        EnsureStandaloneState();
        _sioData32 = reader.ReadUInt32();
        _sioCnt = reader.ReadUInt16();
        _sioData8 = reader.ReadUInt16();
        _rcnt = reader.ReadUInt16();
        _joyCnt = reader.ReadUInt16();
        _joyReceive = reader.ReadUInt32();
        _joyTransmit = reader.ReadUInt32();
        _joyStat = reader.ReadUInt16();
        _transferActive = reader.ReadBoolean();
        _completionScheduled = reader.ReadBoolean();
        _peerReceiveReady = reader.ReadBoolean();
        _peerReceiveData = reader.ReadUInt32();
        _sioMultiHigh = schemaVersion >= 6 ? reader.ReadUInt32() : uint.MaxValue;
        if (_transferActive && (_sioCnt & 0x80) == 0)
            throw new InvalidDataException("The saved GBA serial transfer state is inconsistent.");
        // Schema 5 started serial transfers even with the reset GPIO bit set.
        // Preserve an already-running legacy transfer when migrating it; new
        // writes correctly require software to select SIO through RCNT.
        if (schemaVersion == 5 && _transferActive)
            _rcnt = (ushort)(_rcnt & ~0x8000);
        _normalBitsRemaining = Mode == 1 ? 32 : 8;
        _normalCyclesPerBit = (_sioCnt & 2) != 0 ? 8 : 64;
    }
}
