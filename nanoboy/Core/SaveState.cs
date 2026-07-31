using System;
using System.IO;

namespace nanoboy.Core
{
    public static class SaveState
    {
        private const uint SAVE_STATE_MAGIC = 0x4E414E4F; // "NANO"
        private const ushort SAVE_STATE_VERSION = 1;

        public static byte[] SaveToBuffer(Nanoboy nano)
        {
            if (nano == null) return null;
            try
            {
                using (var ms = new MemoryStream())
                {
                    if (SaveToStream(nano, ms))
                        return ms.ToArray();
                }
            }
            catch { }
            return null;
        }

        public static bool LoadFromBuffer(Nanoboy nano, byte[] buffer)
        {
            if (nano == null || buffer == null) return false;
            try
            {
                using (var ms = new MemoryStream(buffer))
                {
                    return LoadFromStream(nano, ms);
                }
            }
            catch { }
            return false;
        }

        public static bool Save(Nanoboy nano, string filePath)
        {
            if (nano == null || nano.Memory == null || nano.Cpu == null)
                return false;

            try
            {
                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    return SaveToStream(nano, fs);
                }
            }
            catch { return false; }
        }

        public static bool Load(Nanoboy nano, string filePath)
        {
            if (nano == null || nano.Memory == null || nano.Cpu == null || !File.Exists(filePath))
                return false;

            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    return LoadFromStream(nano, fs);
                }
            }
            catch { return false; }
        }

        public static bool SaveToStream(Nanoboy nano, Stream stream)
        {
            if (nano == null || nano.Memory == null || nano.Cpu == null || stream == null)
                return false;

            try
            {
                var writer = new BinaryWriter(stream);

                // Header
                writer.Write(SAVE_STATE_MAGIC);
                writer.Write(SAVE_STATE_VERSION);

                // CPU State
                var cpu = nano.Cpu;
                writer.Write(cpu.A);
                writer.Write(cpu.B);
                writer.Write(cpu.C);
                writer.Write(cpu.D);
                writer.Write(cpu.E);
                writer.Write(cpu.H);
                writer.Write(cpu.L);
                writer.Write(cpu.SP);
                writer.Write(cpu.PC);
                writer.Write(cpu.FlagZ);
                writer.Write(cpu.FlagN);
                writer.Write(cpu.FlagH);
                writer.Write(cpu.FlagC);
                writer.Write(cpu.IME);
                writer.Write(cpu.Halt);
                writer.Write(cpu.IsDoubleSpeed);
                writer.Write(cpu.PrepareSpeedSwitch);

                // Memory & Interrupt State
                var mem = nano.Memory;
                writer.Write(mem.Interrupt.IF);
                writer.Write(mem.Interrupt.IE);
                writer.Write(mem.Timer.DIV);
                writer.Write(mem.Timer.TIMA);
                writer.Write(mem.Timer.TMA);
                writer.Write(mem.Timer.TAC);
                writer.Write(mem.BootROMEnabled);

                // WRAM & HRAM
                for (int b = 0; b < 8; b++)
                {
                    for (int i = 0; i < 0x1000; i++)
                    {
                        writer.Write(mem.ReadWRAMDirect(b, i));
                    }
                }
                writer.Write(mem.WRAMBank);

                for (int i = 0; i < 0x7F; i++)
                {
                    writer.Write(mem.ReadHRAMDirect(i));
                }

                // Video State
                var video = mem.Video;
                writer.Write(video.SCY);
                writer.Write(video.SCX);
                writer.Write(video.LY);
                writer.Write(video.LYC);
                writer.Write(video.WY);
                writer.Write(video.WX);
                writer.Write((byte)video.BGP);
                writer.Write((byte)video.OBP0);
                writer.Write((byte)video.OBP1);
                writer.Write(video.LCDEnable);
                writer.Write(video.WindowTileMapSelect);
                writer.Write(video.WindowEnable);
                writer.Write(video.TileDataSelect);
                writer.Write(video.BackgroundTileMapSelect);
                writer.Write(video.ObjectSize);
                writer.Write(video.ObjectEnable);
                writer.Write(video.BackgroundEnable);
                writer.Write(video.CoincidenceInterrupt);
                writer.Write(video.OAMInterrupt);
                writer.Write(video.VBlankInterrupt);
                writer.Write(video.HBlankInterrupt);
                writer.Write(video.CoincidenceFlag);
                writer.Write(video.ModeFlag);
                writer.Write(video.VRAMBank);

                for (int b = 0; b < 2; b++)
                {
                    for (int i = 0; i < 0x2000; i++)
                    {
                        writer.Write(video.ReadVRAMDirect(b, i));
                    }
                }

                for (int i = 0; i < 0xA0; i++)
                {
                    writer.Write(video.ReadOAMDirect(i));
                }

                // MBC State
                var mbc = mem.MBC;
                if (mbc is Mbc1 mbc1)
                {
                    writer.Write((byte)1);
                    writer.Write(mbc1.CurrentROMBank);
                    writer.Write(mbc1.CurrentRAMBank);
                    writer.Write(mbc1.RAMEnable);
                    writer.Write(mbc1.Mode);
                }
                else if (mbc is Mbc3 mbc3)
                {
                    writer.Write((byte)3);
                    writer.Write(mbc3.CurrentROMBank);
                    writer.Write(mbc3.CurrentRAMBank);
                    writer.Write(mbc3.RAMEnable);
                }
                else
                {
                    writer.Write((byte)0);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool LoadFromStream(Nanoboy nano, Stream stream)
        {
            if (nano == null || nano.Memory == null || nano.Cpu == null || stream == null)
                return false;

            try
            {
                var reader = new BinaryReader(stream);
                uint magic = reader.ReadUInt32();
                ushort version = reader.ReadUInt16();

                if (magic != SAVE_STATE_MAGIC || version != SAVE_STATE_VERSION)
                    return false;

                // CPU State
                var cpu = nano.Cpu;
                cpu.A = reader.ReadByte();
                cpu.B = reader.ReadByte();
                cpu.C = reader.ReadByte();
                cpu.D = reader.ReadByte();
                cpu.E = reader.ReadByte();
                cpu.H = reader.ReadByte();
                cpu.L = reader.ReadByte();
                cpu.SP = reader.ReadUInt16();
                cpu.PC = reader.ReadUInt16();
                cpu.FlagZ = reader.ReadBoolean();
                cpu.FlagN = reader.ReadBoolean();
                cpu.FlagH = reader.ReadBoolean();
                cpu.FlagC = reader.ReadBoolean();
                cpu.IME = reader.ReadBoolean();
                cpu.Halt = reader.ReadBoolean();
                cpu.IsDoubleSpeed = reader.ReadBoolean();
                cpu.PrepareSpeedSwitch = reader.ReadBoolean();

                // Memory & Interrupt State
                var mem = nano.Memory;
                mem.Interrupt.IF = reader.ReadByte();
                mem.Interrupt.IE = reader.ReadByte();
                mem.Timer.DIV = reader.ReadInt32();
                mem.Timer.TIMA = reader.ReadInt32();
                mem.Timer.TMA = reader.ReadInt32();
                mem.Timer.TAC = reader.ReadInt32();
                mem.BootROMEnabled = reader.ReadBoolean();

                // WRAM & HRAM
                for (int b = 0; b < 8; b++)
                {
                    for (int i = 0; i < 0x1000; i++)
                    {
                        mem.WriteWRAMDirect(b, i, reader.ReadByte());
                    }
                }
                mem.WRAMBank = reader.ReadInt32();

                for (int i = 0; i < 0x7F; i++)
                {
                    mem.WriteHRAMDirect(i, reader.ReadByte());
                }

                // Video State
                var video = mem.Video;
                video.SCY = reader.ReadInt32();
                video.SCX = reader.ReadInt32();
                video.LY = reader.ReadInt32();
                video.LYC = reader.ReadInt32();
                video.WY = reader.ReadInt32();
                video.WX = reader.ReadInt32();
                video.BGP = reader.ReadByte();
                video.OBP0 = reader.ReadByte();
                video.OBP1 = reader.ReadByte();
                video.LCDEnable = reader.ReadBoolean();
                video.WindowTileMapSelect = reader.ReadBoolean();
                video.WindowEnable = reader.ReadBoolean();
                video.TileDataSelect = reader.ReadBoolean();
                video.BackgroundTileMapSelect = reader.ReadBoolean();
                video.ObjectSize = reader.ReadBoolean();
                video.ObjectEnable = reader.ReadBoolean();
                video.BackgroundEnable = reader.ReadBoolean();
                video.CoincidenceInterrupt = reader.ReadBoolean();
                video.OAMInterrupt = reader.ReadBoolean();
                video.VBlankInterrupt = reader.ReadBoolean();
                video.HBlankInterrupt = reader.ReadBoolean();
                video.CoincidenceFlag = reader.ReadBoolean();
                video.ModeFlag = reader.ReadInt32();
                video.VRAMBank = reader.ReadInt32();

                for (int b = 0; b < 2; b++)
                {
                    for (int i = 0; i < 0x2000; i++)
                    {
                        video.WriteVRAMDirect(b, i, reader.ReadByte());
                    }
                }

                for (int i = 0; i < 0xA0; i++)
                {
                    video.WriteOAMDirect(i, reader.ReadByte());
                }

                // MBC State
                byte mbcType = reader.ReadByte();
                var mbc = mem.MBC;
                if (mbcType == 1 && mbc is Mbc1 mbc1)
                {
                    mbc1.CurrentROMBank = reader.ReadInt32();
                    mbc1.CurrentRAMBank = reader.ReadInt32();
                    mbc1.RAMEnable = reader.ReadBoolean();
                    mbc1.Mode = reader.ReadInt32();
                }
                else if (mbcType == 3 && mbc is Mbc3 mbc3)
                {
                    mbc3.CurrentROMBank = reader.ReadInt32();
                    mbc3.CurrentRAMBank = reader.ReadInt32();
                    mbc3.RAMEnable = reader.ReadBoolean();
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
