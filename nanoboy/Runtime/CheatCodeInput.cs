using System;

namespace AetherBoy.Runtime;

public enum CheatCodeFormat { Automatic, CodeBreaker, GameShark, GameSharkRaw, ActionReplayV3, ActionReplayV3Raw }

public static partial class CheatCodeInput
{
    public static string Prepare(string code, CheatCodeFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        string prefix = format switch
        {
            CheatCodeFormat.Automatic => "", CheatCodeFormat.CodeBreaker => "CB:",
            CheatCodeFormat.GameShark => "GS:", CheatCodeFormat.GameSharkRaw => "GSRAW:",
            CheatCodeFormat.ActionReplayV3 => "AR3:", CheatCodeFormat.ActionReplayV3Raw => "AR3RAW:",
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        // Existing explicit prefixes win; selection is a convenience for bare device codes.
        string text = code.Trim();
        int colon = text.IndexOf(':');
        int separator = text.IndexOfAny(['\r', '\n', '+', ';']);
        return prefix.Length == 0 || (colon >= 0 && (separator < 0 || colon < separator)) ? text : prefix + text;
    }
}
