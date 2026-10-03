using System;
using System.IO;
using System.Text;
using nanoboy.Core;

namespace AetherBoy.Runtime;

public sealed record BarcodeBoySnapshot(bool Ready, bool Pending, int BytesSent, int CompletedScans);

public static class BarcodeBoyInput
{
    public const string BattleSpaceBerserker = "4907981000301";
    public const string BattleSpaceValkyrie = "4908052808369";
    public static string Normalize(string code) => BarcodeBoy.ValidateCode(code);

    // One bounded UTF-8 text file, never a path extracted from the file contents.
    public static string ReadFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 128) throw new InvalidDataException("Barcode text file exceeds 128 bytes.");
        Span<byte> buffer = stackalloc byte[129];
        int count = 0, read;
        while (count < buffer.Length && (read = stream.Read(buffer[count..])) > 0) count += read;
        if (count > 128) throw new InvalidDataException("Barcode text file exceeds 128 bytes.");
        ReadOnlySpan<byte> text = buffer[..count];
        if (text.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) text = text[3..];
        return Normalize(new UTF8Encoding(false, true).GetString(text));
    }
}

internal sealed class SetBarcodeBoyCommand(bool enabled) : EmulationCommand
{
    public override void Apply(SessionOwnerContext context)
    {
        if (context.Machine is not ProductionMachine machine)
            throw new NotSupportedException("Barcode Boy requires a single-player GB/GBC session.");
        machine.SetBarcodeBoyEnabled(enabled);
    }
}

internal sealed class ScanBarcodeBoyCommand(string code) : EmulationCommand
{
    public override void Apply(SessionOwnerContext context)
    {
        if (context.Machine is not ProductionMachine machine)
            throw new NotSupportedException("Barcode Boy requires a single-player GB/GBC session.");
        machine.ScanBarcodeBoy(code);
    }
}
