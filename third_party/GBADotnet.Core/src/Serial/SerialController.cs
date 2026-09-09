using GameboyAdvanced.Core.Debug;
using GameboyAdvanced.Core.Interrupts;
using static GameboyAdvanced.Core.IORegs;

namespace GameboyAdvanced.Core.Serial;

/// <summary>
/// GBA serial-register model with deterministic disconnected-peer transfers.
/// Normal 8/32-bit transfers use the internal clock rates; the external-clock
/// path remains pending until a future link transport clocks it.
/// </summary>
public unsafe class SerialController
{
    private readonly Device _device;
    private readonly BaseDebugger _debugger;
    private readonly InterruptInterconnect _interruptInterconnect;

    public uint _sioData32 = 0xFFFF_FFFF;
    public ushort _sioCnt;
    public ushort _sioData8 = 0x00FF;
    public ushort _rcnt = 0x8000;
    public ushort _joyCnt;
    public uint _joyReceive = 0xFFFF_FFFF;
    public uint _joyTransmit;
    public ushort _joyStat;
    public bool _transferActive;
    private LocalSerialLink? _link;
    private bool _completionScheduled;
    private bool _peerReceiveReady;
    private uint _peerReceiveData = 0xFFFF_FFFF;

    internal SerialController(
        Device device,
        BaseDebugger debugger,
        InterruptInterconnect interruptInterconnect)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _debugger = debugger ?? throw new ArgumentNullException(nameof(debugger));
        _interruptInterconnect = interruptInterconnect ?? throw new ArgumentNullException(nameof(interruptInterconnect));
    }

    internal void Reset()
    {
        if (_completionScheduled)
            _device.Scheduler.CancelEvent(EventType.SerialTransfer);
        _link?.Cancel(this);
        _sioData32 = 0xFFFF_FFFF;
        _sioCnt = 0;
        _sioData8 = 0x00FF;
        _rcnt = 0x8000;
        _joyCnt = 0;
        _joyReceive = 0xFFFF_FFFF;
        _joyTransmit = 0;
        _joyStat = 0;
        _transferActive = false;
        _completionScheduled = false;
        _peerReceiveReady = false;
        _peerReceiveData = 0xFFFF_FFFF;
    }

    internal byte ReadByte(uint address) => address switch
    {
        >= SIODATA32 and <= SIODATA32 + 3 =>
            (byte)(_sioData32 >> (int)((address - SIODATA32) * 8)),
        SIOCNT => (byte)_sioCnt,
        SIOCNT + 1 => (byte)(_sioCnt >> 8),
        SIODATA8 => (byte)_sioData8,
        SIODATA8 + 1 => (byte)(_sioData8 >> 8),
        RCNT => (byte)_rcnt,
        RCNT + 1 => (byte)(_rcnt >> 8),
        JOYCNT => (byte)_joyCnt,
        JOYCNT + 1 => (byte)(_joyCnt >> 8),
        >= JOY_RECV and <= JOY_RECV + 3 =>
            (byte)(_joyReceive >> (int)((address - JOY_RECV) * 8)),
        >= JOY_TRANS and <= JOY_TRANS + 3 =>
            (byte)(_joyTransmit >> (int)((address - JOY_TRANS) * 8)),
        JOYSTAT => (byte)_joyStat,
        JOYSTAT + 1 => (byte)(_joyStat >> 8),
        _ => 0,
    };

    internal ushort ReadHalfWord(uint address) =>
        (ushort)(ReadByte(address) | (ReadByte(address + 1) << 8));

    internal uint ReadWord(uint address) =>
        (uint)(ReadHalfWord(address) | (ReadHalfWord(address + 2) << 16));

    internal void WriteByte(uint address, byte value)
    {
        switch (address)
        {
            case >= SIODATA32 and <= SIODATA32 + 3:
                int dataShift = (int)(address - SIODATA32) * 8;
                _sioData32 = (_sioData32 & ~(0xFFu << dataShift)) | ((uint)value << dataShift);
                break;
            case SIOCNT:
                _sioCnt = (ushort)((_sioCnt & 0xFF00) | value);
                UpdateTransferState();
                break;
            case SIOCNT + 1:
                _sioCnt = (ushort)((_sioCnt & 0x00FF) | ((value & 0xF7) << 8));
                UpdateTransferState();
                break;
            case SIODATA8:
                _sioData8 = (ushort)((_sioData8 & 0xFF00) | value);
                break;
            case SIODATA8 + 1:
                _sioData8 = (ushort)((_sioData8 & 0x00FF) | (value << 8));
                break;
            case RCNT:
                _rcnt = (ushort)((_rcnt & 0xFF00) | (value & 0xF0));
                break;
            case RCNT + 1:
                _rcnt = (ushort)((_rcnt & 0x00FF) | ((value & 0xC1) << 8));
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
        {
            _sioCnt = (ushort)((value & 0x00FF) | ((value & 0xF700)));
            UpdateTransferState();
            return;
        }
        WriteByte(address, (byte)value);
        WriteByte(address + 1, (byte)(value >> 8));
    }

    internal void WriteWord(uint address, uint value)
    {
        WriteHalfWord(address, (ushort)value);
        WriteHalfWord(address + 2, (ushort)(value >> 16));
    }

    internal static void CompleteTransferEvent(Device device) =>
        device.SerialController.CompleteTransfer();

    internal void WriteState(BinaryWriter writer)
    {
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
    }

    internal void ReadState(BinaryReader reader)
    {
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
        if (_transferActive && (_sioCnt & 0x80) == 0)
            throw new InvalidDataException("The saved GBA serial transfer state is inconsistent.");
    }

    private void UpdateTransferState()
    {
        bool startRequested = (_sioCnt & 0x80) != 0;
        if (!startRequested)
        {
            if (_completionScheduled)
                _device.Scheduler.CancelEvent(EventType.SerialTransfer);
            _link?.Cancel(this);
            _transferActive = false;
            _completionScheduled = false;
            _peerReceiveReady = false;
            return;
        }
        if (_transferActive)
            return;

        int mode = (_sioCnt >> 12) & 3;
        if (mode is 0 or 1 && _link is not null)
        {
            _transferActive = true;
            _completionScheduled = false;
            _peerReceiveReady = false;
            uint data = mode == 0 ? _sioData8 & 0xFFu : _sioData32;
            _link.Begin(this, mode, data, (_sioCnt & 1) != 0, TransferCycles(mode));
            return;
        }
        if ((mode is 0 or 1) && (_sioCnt & 1) == 0)
            return;

        _transferActive = true;
        _device.Scheduler.ScheduleEvent(
            EventType.SerialTransfer,
            &CompleteTransferEvent,
            TransferCycles(mode));
        _completionScheduled = true;
    }

    private int TransferCycles(int mode)
    {
        if (mode is 0 or 1)
        {
            int bits = mode == 0 ? 8 : 32;
            int cyclesPerBit = (_sioCnt & 2) != 0 ? 8 : 64;
            return bits * cyclesPerBit;
        }

        int bitsPerTransfer = mode == 2 ? 16 : 8;
        int multiplayerCyclesPerBit = (_sioCnt & 3) switch
        {
            0 => 1_748,
            1 => 437,
            2 => 291,
            _ => 146,
        };
        return bitsPerTransfer * multiplayerCyclesPerBit;
    }

    private void CompleteTransfer()
    {
        if (!_transferActive)
            return;
        int mode = (_sioCnt >> 12) & 3;
        if (mode == 0)
            _sioData8 = _peerReceiveReady ? (ushort)(_peerReceiveData & 0xFF) : (ushort)0x00FF;
        else if (mode == 1)
            _sioData32 = _peerReceiveReady ? _peerReceiveData : 0xFFFF_FFFF;
        else if (mode == 2)
        {
            _sioData32 = 0xFFFF_FFFF;
            _sioCnt |= 0x40;
        }
        else
            _sioData8 = 0x00FF;

        _sioCnt = (ushort)(_sioCnt & ~0x80);
        _transferActive = false;
        _completionScheduled = false;
        _peerReceiveReady = false;
        _link?.Complete(this);
        if ((_sioCnt & 0x4000) != 0)
            _interruptInterconnect.RaiseInterrupt(Interrupt.SerialCommunication);
    }

    internal void Attach(LocalSerialLink link)
    {
        if (_link is not null && !ReferenceEquals(_link, link))
            throw new InvalidOperationException("This GBA serial controller is already linked.");
        _link = link;
    }

    internal void Detach(LocalSerialLink link)
    {
        if (!ReferenceEquals(_link, link))
            return;
        _link = null;
        _peerReceiveReady = false;
    }

    internal void ArmLinkedCompletion(uint receivedData, int cycles)
    {
        if (!_transferActive)
            return;
        _device.Diagnostics.Record(
            _device.Cpu.Cycles,
            GbaDiagnosticCategory.SerialLink,
            ((_sioCnt >> 12) & 3) == 0
                ? "Local 8-bit serial transfer matched"
                : "Local 32-bit serial transfer matched");
        _peerReceiveData = receivedData;
        _peerReceiveReady = true;
        if (_completionScheduled)
            return;
        _device.Scheduler.ScheduleEvent(EventType.SerialTransfer, &CompleteTransferEvent, cycles);
        _completionScheduled = true;
    }
}
