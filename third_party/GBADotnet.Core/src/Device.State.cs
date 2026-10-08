using System.Security.Cryptography;
using GameboyAdvanced.Core.Bus;
using GameboyAdvanced.Core.Dma;
using GameboyAdvanced.Core.Input;
using GameboyAdvanced.Core.Rom;

namespace GameboyAdvanced.Core;

public unsafe partial class Device
{
    private const uint StateMagic = 0x53414241; // "ABAS" in little endian.
    private const ushort StateSchemaVersion = 7;
    private const int StateDigestLength = 32;
    private const int MaximumBoundaryWaitCycles = CPU_CYCLES_PER_FRAME * 2;

    /// <summary>
    /// Captures a complete, integrity-protected GBA machine state. The CPU is
    /// advanced to the next instruction boundary so implementation-only
    /// function pointers never become part of the persistent format.
    /// </summary>
    public byte[] CaptureState()
    {
        SerialController.EnsureStandaloneState();
        StabilizeForStateCapture();

        using var bodyStream = new MemoryStream(capacity: 768 * 1024);
        using (var writer = new BinaryWriter(bodyStream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(StateMagic);
            // Ordinary cartridges retain byte-for-byte schema 6 layout, which
            // also avoids changing existing link-session state identities.
            writer.Write(Gamepak.EReader is null ? (ushort)6 : StateSchemaVersion);
            writer.Write((ushort)0);
            writer.Write(SHA256.HashData(Bus._bios._bios));
            WriteCpuState(writer);
            WriteBusState(writer);
            Ppu.WriteState(writer);
            Apu.WriteState(writer);
            WriteDmaState(writer);
            WriteTimerState(writer);
            WriteInterruptState(writer);
            WriteGamepadState(writer);
            SerialController.WriteState(writer);
            WriteGamePakState(writer);
            Scheduler.WriteState(writer);
            writer.Write(InstructionBufferPtr);
            foreach (uint address in InstructionBuffer)
                writer.Write(address);
            if (Gamepak.EReader is { } eReader)
            {
                byte[] accessory = eReader.Capture();
                writer.Write(accessory);
                writer.Write(accessory.Length);
            }
        }

        byte[] body = bodyStream.ToArray();
        byte[] result = new byte[body.Length + StateDigestLength];
        body.CopyTo(result, 0);
        SHA256.HashData(body).CopyTo(result, body.Length);
        return result;
    }

    /// <summary>Restores a state produced by <see cref="CaptureState"/>.</summary>
    public void RestoreState(byte[] state)
    {
        SerialController.EnsureStandaloneState();
        ArgumentNullException.ThrowIfNull(state);
        if (state.Length <= StateDigestLength)
            throw new InvalidDataException("The GBA state is truncated.");

        ReadOnlySpan<byte> body = state.AsSpan(0, state.Length - StateDigestLength);
        ReadOnlySpan<byte> expectedDigest = state.AsSpan(state.Length - StateDigestLength);
        Span<byte> actualDigest = stackalloc byte[StateDigestLength];
        SHA256.HashData(body, actualDigest);
        if (!CryptographicOperations.FixedTimeEquals(actualDigest, expectedDigest))
            throw new InvalidDataException("The GBA state failed its integrity check.");

        using var stream = new MemoryStream(body.ToArray(), writable: false);
        using var reader = new BinaryReader(stream);
        if (reader.ReadUInt32() != StateMagic)
            throw new InvalidDataException("The file is not an AetherBoy GBA core state.");
        ushort version = reader.ReadUInt16();
        if (version is not (5 or 6 or StateSchemaVersion))
        {
            throw new NotSupportedException(
                $"GBA state schema {version} is unsupported; expected {StateSchemaVersion}.");
        }
        _ = reader.ReadUInt16();
        byte[] savedBiosDigest = ReadExactly(reader, StateDigestLength);
        byte[] activeBiosDigest = SHA256.HashData(Bus._bios._bios);
        if (!CryptographicOperations.FixedTimeEquals(savedBiosDigest, activeBiosDigest))
            throw new InvalidDataException("The GBA state requires a different BIOS image.");

        // Validate the bounded peripheral tail before changing any live state.
        EReader? nextReader = null;
        int accessoryLength = 0;
        if (version >= 7)
        {
            accessoryLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(body[^4..]);
            if (accessoryLength < 0 || accessoryLength > 150_000 || accessoryLength > body.Length - stream.Position - 4)
                throw new InvalidDataException("Invalid e-Reader state length.");
            if ((accessoryLength != 0) != (Gamepak.EReader is not null))
                throw new InvalidDataException("The GBA state requires different e-Reader hardware.");
            if (accessoryLength != 0) nextReader = EReader.Decode(body.Slice(body.Length - 4 - accessoryLength, accessoryLength).ToArray());
        }
        else if (Gamepak.EReader is not null)
            throw new NotSupportedException("Legacy GBA states do not contain e-Reader hardware. Load the battery save instead.");

        ReadCpuState(reader);
        ReadBusState(reader);
        Ppu.ReadState(reader);
        Apu.ReadState(reader);
        ReadDmaState(reader);
        ReadTimerState(reader);
        ReadInterruptState(reader);
        ReadGamepadState(reader);
        SerialController.ReadState(reader, version);
        ReadGamePakState(reader);
        Scheduler.ReadState(reader);
        InstructionBufferPtr = reader.ReadByte();
        for (int index = 0; index < InstructionBuffer.Length; index++)
            InstructionBuffer[index] = reader.ReadUInt32();

        if (version >= 7)
        {
            if (stream.Position != stream.Length - accessoryLength - 4)
                throw new InvalidDataException("Invalid e-Reader state boundary.");
            stream.Position = stream.Length;
            Gamepak.EReader = nextReader;
            nextReader?.Attach(this);
        }

        if (stream.Position != stream.Length)
            throw new InvalidDataException("The GBA state contains unexpected trailing data.");
    }

    public bool IsKeyPressed(Key key) => Gamepad._keyPressed[key];

    private void StabilizeForStateCapture()
    {
        for (int waited = 0; waited <= MaximumBoundaryWaitCycles; waited++)
        {
            if (Cpu.IsAtInstructionBoundary)
                return;
            RunCycle(skipBreakpoints: true);
        }

        throw new InvalidOperationException(
            "The GBA CPU did not reach a safe save-state boundary.");
    }

    private void WriteCpuState(BinaryWriter writer)
    {
        writer.Write(Cpu.Cycles);
        writer.Write(Cpu.Cpsr.Get());
        foreach (CPSR status in Cpu.Spsr)
            writer.Write(status.Get());
        WriteUInt32Array(writer, Cpu.R);
        WriteUInt32Array(writer, Cpu._spBanks);
        WriteUInt32Array(writer, Cpu._lrBanks);
        WriteUInt32Array(writer, Cpu._fiqHiRegs);
        writer.Write(Cpu.A);
        writer.Write(Cpu.AIncrement);
        writer.Write(Cpu.D);
        writer.Write((byte)Cpu.MAS);
        writer.Write(Cpu.nMREQ);
        writer.Write(Cpu.SEQ);
        writer.Write(Cpu.nOPC);
        writer.Write(Cpu.nRW);
        writer.Write(Cpu.IrqSyncDelay);
        writer.Write(Cpu.Pipeline.ClearedThisCycle);
        WriteNullableUInt32(writer, Cpu.Pipeline.FetchedOpcode);
        WriteNullableUInt32(writer, Cpu.Pipeline.FetchedOpcodeAddress);
        WriteNullableUInt32(writer, Cpu.Pipeline.DecodedOpcode);
        WriteNullableUInt32(writer, Cpu.Pipeline.DecodedOpcodeAddress);
        WriteNullableUInt32(writer, Cpu.Pipeline.CurrentInstruction);
        WriteNullableUInt32(writer, Cpu.Pipeline.CurrentInstructionAddress);
    }

    private void ReadCpuState(BinaryReader reader)
    {
        Cpu.Cycles = reader.ReadInt64();
        Cpu.Cpsr = ReadStatusRegister(reader);
        for (int index = 0; index < Cpu.Spsr.Length; index++)
            Cpu.Spsr[index] = ReadStatusRegister(reader);
        ReadUInt32Array(reader, Cpu.R);
        ReadUInt32Array(reader, Cpu._spBanks);
        ReadUInt32Array(reader, Cpu._lrBanks);
        ReadUInt32Array(reader, Cpu._fiqHiRegs);
        Cpu.A = reader.ReadUInt32();
        Cpu.AIncrement = reader.ReadUInt32();
        Cpu.D = reader.ReadUInt32();
        Cpu.MAS = (BusWidth)reader.ReadByte();
        if (Cpu.MAS is not BusWidth.Byte and not BusWidth.HalfWord and not BusWidth.Word)
            throw new InvalidDataException("The saved GBA bus width is invalid.");
        Cpu.nMREQ = reader.ReadBoolean();
        Cpu.SEQ = reader.ReadInt32();
        Cpu.nOPC = reader.ReadBoolean();
        Cpu.nRW = reader.ReadBoolean();
        Cpu.IrqSyncDelay = reader.ReadInt32();
        Cpu.Pipeline.ClearedThisCycle = reader.ReadBoolean();
        Cpu.Pipeline.FetchedOpcode = ReadNullableUInt32(reader);
        Cpu.Pipeline.FetchedOpcodeAddress = ReadNullableUInt32(reader);
        Cpu.Pipeline.DecodedOpcode = ReadNullableUInt32(reader);
        Cpu.Pipeline.DecodedOpcodeAddress = ReadNullableUInt32(reader);
        Cpu.Pipeline.CurrentInstruction = ReadNullableUInt32(reader);
        Cpu.Pipeline.CurrentInstructionAddress = ReadNullableUInt32(reader);
        Cpu.RestoreInstructionBoundary();
    }

    private void WriteBusState(BinaryWriter writer)
    {
        writer.Write(Bus.OnBoardWRam);
        writer.Write(Bus.OnChipWRam);
        writer.Write(Bus._waitControl.Get());
        writer.Write(Bus._intMemoryControl.Get());
        writer.Write((byte)Bus.HaltMode);
        writer.Write(Bus.PostFlag);
        writer.Write(Bus.WaitStates);
        writer.Write(Bus.InUseByDma);
        writer.Write(Bus._bios._latchedValue);
        writer.Write(Bus._prefetcher._internalAddressRegister);
        writer.Write(Bus._prefetcher._active);
        writer.Write(Bus._prefetcher._currentPreFetchBase);
        writer.Write(Bus._prefetcher._cycleNextRequestStart);
    }

    private void ReadBusState(BinaryReader reader)
    {
        ReadExactly(reader, Bus.OnBoardWRam);
        ReadExactly(reader, Bus.OnChipWRam);
        Bus._waitControl.Set(reader.ReadUInt16());
        Bus._intMemoryControl.Set(reader.ReadUInt32());
        Bus.HaltMode = (HaltMode)reader.ReadByte();
        if (!Enum.IsDefined(Bus.HaltMode))
            throw new InvalidDataException("The saved GBA halt mode is invalid.");
        Bus.PostFlag = reader.ReadByte();
        Bus.WaitStates = reader.ReadInt32();
        Bus.InUseByDma = reader.ReadBoolean();
        Bus._bios._latchedValue = reader.ReadUInt32();
        Bus._prefetcher._internalAddressRegister = reader.ReadUInt32();
        Bus._prefetcher._active = reader.ReadBoolean();
        Bus._prefetcher._currentPreFetchBase = reader.ReadUInt32();
        Bus._prefetcher._cycleNextRequestStart = reader.ReadInt64();
    }

    private void WriteDmaState(BinaryWriter writer)
    {
        writer.Write(DmaCtrl.IsWritePhase);
        writer.Write((byte)DmaCtrl.CurrentChannelIndex);
        foreach (GameboyAdvanced.Core.Dma.DmaChannel channel in DmaData.Channels)
        {
            writer.Write(channel.SourceAddress);
            writer.Write(channel.DestinationAddress);
            writer.Write(channel.WordCount);
            writer.Write(channel.IntSourceAddress);
            writer.Write(channel.IntDestinationAddress);
            WriteNullableUInt32(writer, channel.IntCachedValue);
            writer.Write(channel.IntWordCount);
            writer.Write(channel.IntDestAddressIncrement);
            writer.Write(channel.IntSrcAddressIncrement);
            writer.Write(channel.InternalLatch);
            writer.Write(channel.IntDestSeqAccess);
            writer.Write(channel.IntSrcSeqAccess);
            writer.Write(channel.IsRunning);
            writer.Write(channel.ControlReg.Read());
            writer.Write(channel.ClocksToStart);
            writer.Write(channel.ClocksToStop);
        }
    }

    private void ReadDmaState(BinaryReader reader)
    {
        bool isWritePhase = reader.ReadBoolean();
        int activeChannel = reader.ReadByte();
        foreach (GameboyAdvanced.Core.Dma.DmaChannel channel in DmaData.Channels)
        {
            channel.SourceAddress = reader.ReadUInt32();
            channel.DestinationAddress = reader.ReadUInt32();
            channel.WordCount = reader.ReadUInt16();
            channel.IntSourceAddress = reader.ReadUInt32();
            channel.IntDestinationAddress = reader.ReadUInt32();
            channel.IntCachedValue = ReadNullableUInt32(reader);
            channel.IntWordCount = reader.ReadInt32();
            channel.IntDestAddressIncrement = reader.ReadInt32();
            channel.IntSrcAddressIncrement = reader.ReadInt32();
            channel.InternalLatch = reader.ReadUInt32();
            channel.IntDestSeqAccess = reader.ReadInt32();
            channel.IntSrcSeqAccess = reader.ReadInt32();
            channel.IsRunning = reader.ReadBoolean();
            ushort control = reader.ReadUInt16();
            channel.ControlReg.UpdateB1((byte)control);
            _ = channel.ControlReg.UpdateB2((byte)(control >> 8));
            channel.ClocksToStart = reader.ReadInt32();
            channel.ClocksToStop = reader.ReadInt32();
        }
        DmaCtrl.RestorePipeline(isWritePhase, activeChannel);
    }

    private void WriteTimerState(BinaryWriter writer)
    {
        foreach (Timer.TimerRegister timer in TimerController._timers)
        {
            writer.Write(timer.Reload);
            writer.Write(timer.ReloadLatch);
            writer.Write((byte)timer.PrescalerSelection);
            writer.Write((byte)timer.PrescalerSelectionLatch);
            writer.Write(timer.CountUpTiming);
            writer.Write(timer.CountUpTimingLatch);
            writer.Write(timer.IrqEnabled);
            writer.Write(timer.IrqEnabledLatch);
            writer.Write(timer.OldStart);
            writer.Write(timer.Start);
            writer.Write(timer.StartLatch);
            writer.Write(timer.CounterAtLastLatch);
            writer.Write(timer.CyclesAtLastLatch);
        }
    }

    private void ReadTimerState(BinaryReader reader)
    {
        foreach (Timer.TimerRegister timer in TimerController._timers)
        {
            timer.Reload = reader.ReadUInt16();
            timer.ReloadLatch = reader.ReadUInt16();
            timer.PrescalerSelection = (Timer.TimerPrescaler)reader.ReadByte();
            timer.PrescalerSelectionLatch = (Timer.TimerPrescaler)reader.ReadByte();
            timer.CountUpTiming = reader.ReadBoolean();
            timer.CountUpTimingLatch = reader.ReadBoolean();
            timer.IrqEnabled = reader.ReadBoolean();
            timer.IrqEnabledLatch = reader.ReadBoolean();
            timer.OldStart = reader.ReadBoolean();
            timer.Start = reader.ReadBoolean();
            timer.StartLatch = reader.ReadBoolean();
            timer.CounterAtLastLatch = reader.ReadUInt16();
            timer.CyclesAtLastLatch = reader.ReadInt64();
        }
    }

    private void WriteInterruptState(BinaryWriter writer)
    {
        writer.Write(InterruptRegisters._interruptMasterEnable);
        writer.Write(InterruptRegisters._interruptEnable.Get());
        writer.Write(InterruptRegisters._interruptRequest.Get());
        writer.Write(InterruptRegisters.CpuShouldIrq);
        writer.Write(InterruptRegisters.ShouldBreakHalt);
    }

    private void ReadInterruptState(BinaryReader reader)
    {
        InterruptRegisters._interruptMasterEnable = reader.ReadBoolean();
        ushort enabled = reader.ReadUInt16();
        InterruptRegisters._interruptEnable.SetB1((byte)enabled);
        InterruptRegisters._interruptEnable.SetB2((byte)(enabled >> 8));
        ushort requested = reader.ReadUInt16();
        InterruptRegisters._interruptRequest.SetB1((byte)requested);
        InterruptRegisters._interruptRequest.SetB2((byte)(requested >> 8));
        InterruptRegisters.CpuShouldIrq = reader.ReadBoolean();
        InterruptRegisters.ShouldBreakHalt = reader.ReadBoolean();
    }

    private void WriteGamepadState(BinaryWriter writer)
    {
        foreach (Key key in Enum.GetValues<Key>())
        {
            writer.Write(Gamepad._keyPressed[key]);
            writer.Write(Gamepad._keyIrq[key]);
        }
        writer.Write(Gamepad._irqEnabled);
        writer.Write(Gamepad._irqConditionAnd);
    }

    private void ReadGamepadState(BinaryReader reader)
    {
        foreach (Key key in Enum.GetValues<Key>())
        {
            Gamepad._keyPressed[key] = reader.ReadBoolean();
            Gamepad._keyIrq[key] = reader.ReadBoolean();
        }
        Gamepad._irqEnabled = reader.ReadBoolean();
        Gamepad._irqConditionAnd = reader.ReadBoolean();
    }

    private void WriteGamePakState(BinaryWriter writer)
    {
        writer.Write((byte)Gamepak.RomBackupType);
        writer.Write(Gamepak._sram);
        if (Gamepak._flashBackup is FlashBackup flash)
        {
            writer.Write(flash._data);
            writer.Write(flash._bank);
            writer.Write((byte)flash._state);
            writer.Write((byte)flash._commandState);
        }
        if (Gamepak._eepromBackup is EEPromBackup eeprom)
        {
            writer.Write(eeprom.Data);
            writer.Write((byte)eeprom.Size);
            writer.Write((byte)eeprom.State);
            writer.Write((byte)eeprom.Command);
            writer.Write(eeprom._maxAddressBits);
            writer.Write(eeprom._addressBitsReceived);
            writer.Write(eeprom._dataBitsReceived);
            writer.Write(eeprom._addressBlock);
            writer.Write(eeprom._address);
        }
        writer.Write(Gamepak._rtc is not null);
        Gamepak._rtc?.WriteState(writer);
    }

    private void ReadGamePakState(BinaryReader reader)
    {
        RomBackupType type = (RomBackupType)reader.ReadByte();
        if (type != Gamepak.RomBackupType)
            throw new InvalidDataException("The GBA state uses different save hardware.");
        ReadExactly(reader, Gamepak._sram);
        if (Gamepak._flashBackup is FlashBackup flash)
        {
            ReadExactly(reader, flash._data);
            flash._bank = reader.ReadInt32();
            flash._state = (FlashBackup.FlashChipState)reader.ReadByte();
            flash._commandState = (FlashBackup.FlashCommandState)reader.ReadByte();
        }
        if (Gamepak._eepromBackup is EEPromBackup eeprom)
        {
            ReadExactly(reader, eeprom.Data);
            eeprom.SetSize((EEPromBackup.EEPromSize)reader.ReadByte());
            eeprom.State = (EEPromBackup.EEPromState)reader.ReadByte();
            eeprom.Command = (EEPromBackup.EEPromCommand)reader.ReadByte();
            eeprom._maxAddressBits = reader.ReadInt32();
            eeprom._addressBitsReceived = reader.ReadInt32();
            eeprom._dataBitsReceived = reader.ReadInt32();
            eeprom._addressBlock = reader.ReadInt32();
            eeprom._address = reader.ReadInt32();
        }
        bool hasRtc = reader.ReadBoolean();
        if (hasRtc != (Gamepak._rtc is not null))
            throw new InvalidDataException("The GBA state uses different RTC hardware.");
        Gamepak._rtc?.ReadState(reader);
    }

    private static CPSR ReadStatusRegister(BinaryReader reader)
    {
        uint raw = reader.ReadUInt32();
        var result = new CPSR();
        result.Mode = result.Set(raw);
        result.ThumbMode = (raw & 0x20) != 0;
        return result;
    }

    private static void WriteNullableUInt32(BinaryWriter writer, uint? value)
    {
        writer.Write(value.HasValue);
        if (value.HasValue)
            writer.Write(value.Value);
    }

    private static uint? ReadNullableUInt32(BinaryReader reader) =>
        reader.ReadBoolean() ? reader.ReadUInt32() : null;

    private static void WriteUInt32Array(BinaryWriter writer, uint[] values)
    {
        foreach (uint value in values)
            writer.Write(value);
    }

    private static void ReadUInt32Array(BinaryReader reader, uint[] destination)
    {
        for (int index = 0; index < destination.Length; index++)
            destination[index] = reader.ReadUInt32();
    }

    private static void ReadExactly(BinaryReader reader, byte[] destination)
    {
        int read = reader.Read(destination, 0, destination.Length);
        if (read != destination.Length)
            throw new EndOfStreamException("The GBA machine state ended unexpectedly.");
    }

    private static byte[] ReadExactly(BinaryReader reader, int count)
    {
        byte[] result = new byte[count];
        ReadExactly(reader, result);
        return result;
    }
}
