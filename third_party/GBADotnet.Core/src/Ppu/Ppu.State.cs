using GameboyAdvanced.Core.Ppu.Registers;

namespace GameboyAdvanced.Core.Ppu;

public partial class Ppu
{
    internal void WriteState(BinaryWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.Write(Dispcnt.Read());
        writer.Write(GreenSwap);
        writer.Write(Dispstat.Read());
        writer.Write(CurrentLine);
        writer.Write(CurrentLineCycles);

        foreach (Background background in Backgrounds)
        {
            writer.Write(background.Control.Read());
            writer.Write(background.XOffset);
            writer.Write(background.YOffset);
            writer.Write(background.RefPointX);
            writer.Write(background.RefPointXLatched);
            writer.Write(background.RefPointY);
            writer.Write(background.RefPointYLatched);
            writer.Write(background.Dx);
            writer.Write(background.Dmx);
            writer.Write(background.Dy);
            writer.Write(background.Dmy);
        }

        writer.Write(_windows.X1);
        writer.Write(_windows.X2);
        writer.Write(_windows.Y1);
        writer.Write(_windows.Y2);
        writer.Write(_windows.GetWinIn());
        writer.Write(_windows.GetWinOut());
        writer.Write(Mosaic.Get());
        writer.Write(ColorEffects.BldCnt());
        writer.Write(ColorEffects.BldAlpha());
        writer.Write((byte)ColorEffects.EVYCoefficient);

        foreach (ushort color in _paletteRam)
            writer.Write(color);
        writer.Write(Vram);
        foreach (ushort value in _oam)
            writer.Write(value);
        writer.Write(FrameBuffer);

        foreach (int window in _windowState)
            writer.Write(window);
        foreach (SpritePixelProperties pixel in _objBuffer)
        {
            writer.Write(pixel.PaletteColor);
            writer.Write((byte)pixel.PixelMode);
            writer.Write(pixel.Priority);
            writer.Write(pixel.IsBackdrop);
        }
    }

    internal void ReadState(BinaryReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        Dispcnt.Update(reader.ReadUInt16());
        GreenSwap = reader.ReadUInt16();
        ushort displayStatus = reader.ReadUInt16();
        Dispstat.VBlankFlag = (displayStatus & 0x0001) != 0;
        Dispstat.HBlankFlag = (displayStatus & 0x0002) != 0;
        Dispstat.VCounterFlag = (displayStatus & 0x0004) != 0;
        Dispstat.VBlankIrqEnable = (displayStatus & 0x0008) != 0;
        Dispstat.HBlankIrqEnable = (displayStatus & 0x0010) != 0;
        Dispstat.VCounterIrqEnable = (displayStatus & 0x0020) != 0;
        Dispstat.VCountSetting = (ushort)(displayStatus >> 8);
        CurrentLine = reader.ReadUInt16();
        CurrentLineCycles = reader.ReadInt32();
        if (CurrentLine > 228)
            throw new InvalidDataException("The saved GBA scanline is invalid.");

        foreach (Background background in Backgrounds)
        {
            ushort control = reader.ReadUInt16();
            background.Control.UpdateB1((byte)control);
            background.Control.UpdateB2((byte)(control >> 8));
            background.XOffset = reader.ReadInt32();
            background.YOffset = reader.ReadInt32();
            background.RefPointX = reader.ReadInt32();
            background.RefPointXLatched = reader.ReadInt32();
            background.RefPointY = reader.ReadInt32();
            background.RefPointYLatched = reader.ReadInt32();
            background.Dx = reader.ReadInt16();
            background.Dmx = reader.ReadInt16();
            background.Dy = reader.ReadInt16();
            background.Dmy = reader.ReadInt16();
        }

        ReadExactly(reader, _windows.X1);
        ReadExactly(reader, _windows.X2);
        ReadExactly(reader, _windows.Y1);
        ReadExactly(reader, _windows.Y2);
        ushort windowIn = reader.ReadUInt16();
        _windows.UpdateWinIn(0, (byte)windowIn);
        _windows.UpdateWinIn(1, (byte)(windowIn >> 8));
        ushort windowOut = reader.ReadUInt16();
        _windows.UpdateWinOutB1((byte)windowOut);
        _windows.UpdateWinOutB2((byte)(windowOut >> 8));
        ushort mosaic = reader.ReadUInt16();
        Mosaic.UpdateB1((byte)mosaic);
        Mosaic.UpdateB2((byte)(mosaic >> 8));
        ushort blendControl = reader.ReadUInt16();
        ColorEffects.UpdateBldCntB1((byte)blendControl);
        ColorEffects.UpdateBldCntB2((byte)(blendControl >> 8));
        ushort blendAlpha = reader.ReadUInt16();
        ColorEffects.UpdateBldAlphaB1((byte)blendAlpha);
        ColorEffects.UpdateBldAlphaB2((byte)(blendAlpha >> 8));
        ColorEffects.UpdateBldy(reader.ReadByte());

        for (int index = 0; index < _paletteRam.Length; index++)
            WritePaletteHalfWord((uint)(index * 2), reader.ReadUInt16());
        ReadExactly(reader, Vram);
        for (int index = 0; index < _oam.Length; index++)
            WriteOamHalfWord((uint)(index * 2), reader.ReadUInt16());
        ReadExactly(reader, FrameBuffer);

        for (int index = 0; index < _windowState.Length; index++)
            _windowState[index] = reader.ReadInt32();
        for (int index = 0; index < _objBuffer.Length; index++)
        {
            _objBuffer[index].PaletteColor = reader.ReadInt32();
            byte mode = reader.ReadByte();
            if (mode > (byte)SpriteMode.Prohibited)
                throw new InvalidDataException("The saved GBA sprite mode is invalid.");
            _objBuffer[index].PixelMode = (SpriteMode)mode;
            _objBuffer[index].Priority = reader.ReadInt32();
            _objBuffer[index].IsBackdrop = reader.ReadBoolean();
        }
    }

    private static void ReadExactly(BinaryReader reader, byte[] destination)
    {
        int read = reader.Read(destination, 0, destination.Length);
        if (read != destination.Length)
            throw new EndOfStreamException("The GBA PPU state ended unexpectedly.");
    }
}
