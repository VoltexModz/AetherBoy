using System;

namespace AetherBoy.Runtime
{
    [Flags]
    public enum GameBoyAdvanceButtons : byte
    {
        None = 0,
        L = 1 << 0,
        R = 1 << 1,
        All = L | R
    }
}
