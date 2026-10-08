using GameboyAdvanced.Core.Timer;
using GameboyAdvanced.Core.Serial;

namespace GameboyAdvanced.Core;

public unsafe partial class Scheduler
{
    internal void WriteState(BinaryWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        int count = Math.Max(0, _lastEventPtr - _nextEventPtr + 1);
        writer.Write(count);
        for (int index = _nextEventPtr; index <= _lastEventPtr; index++)
        {
            writer.Write((byte)_events[index].Type);
            writer.Write(_events[index].Cycles);
        }
    }

    internal void ReadState(BinaryReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        int count = reader.ReadInt32();
        if (count is < 0 or > MaxEvents)
            throw new InvalidDataException("The saved GBA event count is invalid.");

        Reset();
        for (int index = 0; index < count; index++)
        {
            EventType type = (EventType)reader.ReadByte();
            long cycles = reader.ReadInt64();
            if (!Enum.IsDefined(type) || type == EventType.Generic)
                throw new InvalidDataException($"The saved GBA event type {type} is unsupported.");
            if (cycles < _device.Cpu.Cycles)
                throw new InvalidDataException("A saved GBA event is in the past.");

            _events[index].Type = type;
            _events[index].Cycles = cycles;
            _events[index].Callback = CallbackFor(type);
        }

        _nextEventPtr = 0;
        _lastEventPtr = count - 1;
    }

    private static delegate*<Device, void> CallbackFor(EventType type) => type switch
    {
        EventType.Timer0Irq => &TimerRegister.Timer0IrqSet,
        EventType.Timer0Overflow => &TimerRegister.Timer0OverflowEvent,
        EventType.Timer0Latch => &TimerRegister.LatchTimer0ValuesEvent,
        EventType.Timer0LatchReload => &TimerRegister.LatchTimer0ReloadEvent,
        EventType.Timer1Irq => &TimerRegister.Timer1IrqSet,
        EventType.Timer1Overflow => &TimerRegister.Timer1OverflowEvent,
        EventType.Timer1Latch => &TimerRegister.LatchTimer1ValuesEvent,
        EventType.Timer1LatchReload => &TimerRegister.LatchTimer1ReloadEvent,
        EventType.Timer2Irq => &TimerRegister.Timer2IrqSet,
        EventType.Timer2Overflow => &TimerRegister.Timer2OverflowEvent,
        EventType.Timer2Latch => &TimerRegister.LatchTimer2ValuesEvent,
        EventType.Timer2LatchReload => &TimerRegister.LatchTimer2ReloadEvent,
        EventType.Timer3Irq => &TimerRegister.Timer3IrqSet,
        EventType.Timer3Overflow => &TimerRegister.Timer3OverflowEvent,
        EventType.Timer3Latch => &TimerRegister.LatchTimer3ValuesEvent,
        EventType.Timer3LatchReload => &TimerRegister.LatchTimer3ReloadEvent,
        EventType.HBlankStart => &Ppu.Ppu.HBlankStartEvent,
        EventType.HBlankEnd => &Ppu.Ppu.HBlankEndEvent,
        EventType.ApuSample => &Apu.Apu.SampleEvent,
        EventType.SerialTransfer => &SerialController.CompleteTransferEvent,
        EventType.EReaderIrq => &Rom.EReader.IrqEvent,
        _ => throw new InvalidDataException($"The saved GBA event type {type} is unsupported.")
    };
}
