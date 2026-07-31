using System;
using System.Collections.Generic;

namespace nanoboy.Core
{
    public class CheatItem
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public bool Enabled { get; set; }
        public ushort Address { get; set; }
        public byte Value { get; set; }
        public byte CompareValue { get; set; }
        public bool IsGameGenie { get; set; }
    }

    public class CheatEngine
    {
        public List<CheatItem> Cheats { get; } = new List<CheatItem>();

        public bool AddCheat(string name, string code)
        {
            string cleanCode = code.Replace("-", "").Replace(" ", "").Trim().ToUpper();

            // GameShark: 8 Hex Chars e.g. 01XXYYZZ -> Value XX at Address ZZYY
            if (cleanCode.Length == 8 && cleanCode.StartsWith("01"))
            {
                try
                {
                    byte val = Convert.ToByte(cleanCode.Substring(2, 2), 16);
                    byte addrLsb = Convert.ToByte(cleanCode.Substring(4, 2), 16);
                    byte addrMsb = Convert.ToByte(cleanCode.Substring(6, 2), 16);
                    ushort addr = (ushort)((addrMsb << 8) | addrLsb);

                    Cheats.Add(new CheatItem
                    {
                        Name = name,
                        Code = code,
                        Enabled = true,
                        Address = addr,
                        Value = val,
                        IsGameGenie = false
                    });
                    return true;
                }
                catch { }
            }

            // Game Genie: 9 Hex Chars e.g. ABC-DEF-GHI -> Address (D E F C)
            if (cleanCode.Length == 9)
            {
                try
                {
                    byte val = Convert.ToByte(cleanCode.Substring(0, 2), 16);
                    ushort addr = (ushort)(
                        (Convert.ToByte(cleanCode.Substring(6, 1), 16) << 12) |
                        (Convert.ToByte(cleanCode.Substring(2, 1), 16) << 8) |
                        (Convert.ToByte(cleanCode.Substring(4, 1), 16) << 4) |
                        (Convert.ToByte(cleanCode.Substring(5, 1), 16))
                    );
                    byte cmp = Convert.ToByte(cleanCode.Substring(7, 2), 16);

                    Cheats.Add(new CheatItem
                    {
                        Name = name,
                        Code = code,
                        Enabled = true,
                        Address = addr,
                        Value = val,
                        CompareValue = cmp,
                        IsGameGenie = true
                    });
                    return true;
                }
                catch { }
            }

            return false;
        }

        public void ApplyCheats(Memory memory)
        {
            if (memory == null) return;

            foreach (var cheat in Cheats)
            {
                if (!cheat.Enabled) continue;

                if (!cheat.IsGameGenie)
                {
                    // GameShark RAM patch
                    try
                    {
                        memory.WriteByte(cheat.Address, cheat.Value);
                    }
                    catch { }
                }
            }
        }
    }
}
