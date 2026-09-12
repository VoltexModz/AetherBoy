using System;
using System.IO;

namespace nanoboy.Storage;

internal enum WindowsFirmwareKind { Dmg, Cgb, Gba }
internal enum WindowsFirmwareSource { None, Managed, WorkingDirectory, Executable }
internal enum WindowsFirmwareState { Missing, Available, Invalid, Unreadable }

internal sealed record WindowsFirmwareStatus(
    WindowsFirmwareKind Kind, WindowsFirmwareState State, WindowsFirmwareSource Source, string? Path,
    string? Problem)
{
    internal bool IsAvailable => State == WindowsFirmwareState.Available;
    internal string SourceLabel => Source switch
    {
        WindowsFirmwareSource.Managed => "AppData",
        WindowsFirmwareSource.WorkingDirectory => "Arbeitsordner",
        WindowsFirmwareSource.Executable => "Programmordner",
        _ => "Keine Datei"
    };
}

/// <summary>A bounded, validated snapshot; changing the selected file after confirmation cannot change the import.</summary>
internal sealed class WindowsFirmwareImport
{
    private readonly byte[] bytes;
    internal WindowsFirmwareImport(WindowsFirmwareKind kind, byte[] bytes)
    {
        Kind = kind;
        this.bytes = (byte[])bytes.Clone();
    }
    internal WindowsFirmwareKind Kind { get; }
    internal void WriteTo(Stream destination) => destination.Write(bytes);
    internal int Length => bytes.Length;
}

/// <summary>Local firmware only. Exact sizes check structure, not authenticity, provenance or compatibility.</summary>
internal sealed class WindowsFirmwareStore
{
    private readonly string executableDirectory;
    private readonly string workingDirectory;

    internal WindowsFirmwareStore(WindowsDataPaths paths, string? executableDirectory = null,
        string? workingDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        DirectoryPath = paths.Firmware;
        this.executableDirectory = System.IO.Path.GetFullPath(executableDirectory ?? AppContext.BaseDirectory);
        this.workingDirectory = System.IO.Path.GetFullPath(workingDirectory ?? Environment.CurrentDirectory);
    }

    internal string DirectoryPath { get; }
    internal string PathFor(WindowsFirmwareKind kind) => System.IO.Path.Combine(DirectoryPath, FileName(kind));
    internal static string FileName(WindowsFirmwareKind kind) => kind switch
    {
        WindowsFirmwareKind.Dmg => "dmg_boot.bin",
        WindowsFirmwareKind.Cgb => "gbc_boot.bin", // Preserve the existing Windows filename.
        WindowsFirmwareKind.Gba => "gba_bios.bin",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    internal static int ExpectedLength(WindowsFirmwareKind kind) => kind switch
    {
        WindowsFirmwareKind.Dmg => 256,
        WindowsFirmwareKind.Cgb => 2304,
        WindowsFirmwareKind.Gba => 16384,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    internal static string ModelName(WindowsFirmwareKind kind) => kind switch
    {
        WindowsFirmwareKind.Dmg => "GAME BOY · DMG",
        WindowsFirmwareKind.Cgb => "GAME BOY COLOR · CGB",
        WindowsFirmwareKind.Gba => "GAME BOY ADVANCE · GBA",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    internal WindowsFirmwareStatus GetStatus(WindowsFirmwareKind kind)
    {
        foreach (var candidate in Candidates(kind))
        {
            try
            {
                _ = ReadValidated(candidate.Path, kind);
                return new(kind, WindowsFirmwareState.Available, candidate.Source, candidate.Path, null);
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException) { }
            catch (InvalidDataException)
            {
                return new(kind, WindowsFirmwareState.Invalid, candidate.Source, candidate.Path,
                    $"Falsche Größe; erwartet: {ExpectedLength(kind):N0} Bytes.");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return new(kind, WindowsFirmwareState.Unreadable, candidate.Source, candidate.Path,
                    "Datei ist gesperrt oder nicht lesbar.");
            }
        }
        return new(kind, WindowsFirmwareState.Missing, WindowsFirmwareSource.None, null, null);
    }

    // A present but invalid higher-priority file must not silently select some other external BIOS.
    internal byte[]? Load(WindowsFirmwareKind kind)
    {
        foreach (var candidate in Candidates(kind))
        {
            try { return ReadValidated(candidate.Path, kind); }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException) { }
        }
        return null;
    }

    internal WindowsFirmwareImport PrepareImport(string path, WindowsFirmwareKind kind) =>
        new(kind, ReadValidated(System.IO.Path.GetFullPath(path), kind));

    internal string Import(WindowsFirmwareImport import, bool replaceExisting = false)
    {
        ArgumentNullException.ThrowIfNull(import);
        if (import.Length != ExpectedLength(import.Kind))
            throw new InvalidDataException("Ungültiger Firmware-Import; keine Datei wurde geändert.");
        string destination = PathFor(import.Kind);
        Directory.CreateDirectory(DirectoryPath);
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                import.WriteTo(output);
                output.Flush(flushToDisk: true);
            }
            if (replaceExisting && File.Exists(destination))
                File.Replace(temporary, destination, destinationBackupFileName: null);
            else
                File.Move(temporary, destination); // No overwrite, including a file created during confirmation.
            return destination;
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }

    private (string Path, WindowsFirmwareSource Source)[] Candidates(WindowsFirmwareKind kind) =>
    [
        (PathFor(kind), WindowsFirmwareSource.Managed),
        (System.IO.Path.Combine(workingDirectory, FileName(kind)), WindowsFirmwareSource.WorkingDirectory),
        (System.IO.Path.Combine(executableDirectory, FileName(kind)), WindowsFirmwareSource.Executable)
    ];

    private static byte[] ReadValidated(string path, WindowsFirmwareKind kind)
    {
        int expected = ExpectedLength(kind);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length != expected)
            throw new InvalidDataException($"Für {ModelName(kind)} werden exakt {expected:N0} Bytes benötigt. " +
                "Diese Datei hat eine andere Größe; es wurde nichts importiert.");
        var bytes = new byte[expected];
        input.ReadExactly(bytes);
        if (input.ReadByte() != -1)
            throw new InvalidDataException("Die Firmware-Datei hat sich beim Lesen geändert.");
        return bytes;
    }
}
