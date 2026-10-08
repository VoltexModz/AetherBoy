param([Parameter(Mandatory=$true)][string]$Path)
$ErrorActionPreference = 'Stop'
# Read-only structural inventory. No ROM, disassembly or Nintendo assets are written.
Add-Type -AssemblyName System.IO.Compression
if ([IO.Path]::GetExtension($Path) -ieq '.zip') {
    $archive = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($Path))
    try {
        $entries = @($archive.Entries | Where-Object { $_.Name.EndsWith('.gba', [StringComparison]::OrdinalIgnoreCase) })
        if ($entries.Count -ne 1 -or $entries[0].Length -gt 33554432) { throw 'Expected one GBA ROM up to 32 MiB.' }
        $inputStream = $entries[0].Open()
        try { $bytes = [byte[]]::new($entries[0].Length); $inputStream.ReadExactly($bytes) } finally { $inputStream.Dispose() }
    } finally { $archive.Dispose() }
} else {
    $file = [IO.FileInfo]::new([IO.Path]::GetFullPath($Path))
    if ($file.Length -lt 192 -or $file.Length -gt 33554432) { throw 'Expected a GBA ROM up to 32 MiB.' }
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
}
if ($bytes.Length -lt 192 -or $bytes.Length -gt 33554432) { throw 'Invalid ROM size.' }
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
public static class ReaderRomSurvey {
    public static object[] Blocks(byte[] bytes) {
        var result = new List<object>();
        for (int start = 0; start < bytes.Length; start += 1048576) {
            int length = Math.Min(1048576, bytes.Length - start), zero = 0, ff = 0;
            var histogram = new int[256];
            for (int i = start; i < start + length; i++) histogram[bytes[i]]++;
            zero = histogram[0]; ff = histogram[255]; double entropy = 0;
            foreach (int n in histogram) if (n > 0) { double p = (double)n / length; entropy -= p * Math.Log2(p); }
            result.Add(new { Offset = start, Length = length, ZeroBytes = zero, FFBytes = ff, EntropyBits = Math.Round(entropy, 4) });
        }
        return result.ToArray();
    }
    public static int Tail(byte[] bytes) {
        int i = bytes.Length - 1;
        while (i >= 0 && bytes[i] == bytes[bytes.Length - 1]) i--;
        return bytes.Length - i - 1;
    }
}
'@
$check = 0
for ($i = 0xA0; $i -le 0xBC; $i++) { $check = ($check - $bytes[$i]) -band 255 }
$check = ($check - 0x19) -band 255
$entry = [BitConverter]::ToUInt32($bytes, 0)
$offset = [int]($entry -band 0xFFFFFF)
if (($offset -band 0x800000) -ne 0) { $offset -= 0x1000000 }
[ordered]@{
    Length = $bytes.Length
    MiB = $bytes.Length / 1048576
    Sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
    Title = [Text.Encoding]::ASCII.GetString($bytes, 0xA0, 12).Trim([char]0)
    GameCode = [Text.Encoding]::ASCII.GetString($bytes, 0xAC, 4)
    MakerCode = [Text.Encoding]::ASCII.GetString($bytes, 0xB0, 2)
    Revision = $bytes[0xBC]
    HeaderChecksumValid = ($check -eq $bytes[0xBD])
    InitialArmBranch = if (($entry -band 0xFF000000L) -eq 0xEA000000L) { '0x{0:X8}' -f (0x08000008 + ($offset * 4)) } else { $null }
    RepeatedTailByte = [int]$bytes[-1]
    RepeatedTailLength = [ReaderRomSurvey]::Tail($bytes)
    Blocks = [ReaderRomSurvey]::Blocks($bytes)
    Note = 'Byte statistics do not distinguish executable code, compressed data, assets or unused space.'
} | ConvertTo-Json -Depth 5
