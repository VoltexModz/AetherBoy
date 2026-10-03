using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace nanoboy.Core
{
    public enum BatterySaveGeneration
    {
        None = -1,
        Current = 0,
        Backup1 = 1,
        Backup2 = 2,
        Backup3 = 3
    }

    public sealed record BatterySaveFile(
        BatterySaveGeneration Generation,
        bool Exists,
        bool IsValid,
        bool HasIntegrityMetadata,
        long Length,
        DateTime LastWriteTimeUtc);

    public sealed record BatterySaveLoadStatus(
        bool IsEnabled,
        int ExpectedLength,
        BatterySaveGeneration LoadedFrom,
        bool InvalidPrimaryDetected)
    {
        public bool RecoveredFromBackup => LoadedFrom >= BatterySaveGeneration.Backup1;

        internal static BatterySaveLoadStatus Disabled(int expectedLength) =>
            new(false, expectedLength, BatterySaveGeneration.None, false);
    }

    public static class BatterySaveStore
    {
        public const int BackupCount = 3;

        public static IReadOnlyList<BatterySaveActivity> GetActiveWrites() => BatterySaveOperation.GetActive();

        internal sealed record LoadResult(byte[] Data, BatterySaveLoadStatus Status);

        internal static LoadResult Load(string savePath, int expectedLength)
        {
            ValidateArguments(savePath, expectedLength);

            bool primaryExists = File.Exists(savePath);
            bool invalidPrimary = primaryExists && !TryReadExact(savePath, expectedLength, out _);
            bool anyCandidateExists = false;

            for (int generation = 0; generation <= BackupCount; generation++)
            {
                string candidatePath = GetPath(savePath, generation);
                if (!File.Exists(candidatePath))
                {
                    continue;
                }

                anyCandidateExists = true;
                if (!TryReadExact(candidatePath, expectedLength, out byte[] data))
                {
                    continue;
                }

                BatterySaveGeneration loadedFrom = (BatterySaveGeneration)generation;
                return new LoadResult(
                    data,
                    new BatterySaveLoadStatus(
                        true,
                        expectedLength,
                        loadedFrom,
                        invalidPrimary));
            }

            if (anyCandidateExists)
            {
                throw new InvalidDataException(
                    $"No valid battery save was found for '{Path.GetFileName(savePath)}'. " +
                    $"Expected exactly {expectedLength} bytes in the current save or one of its {BackupCount} backups.");
            }

            return new LoadResult(
                new byte[expectedLength],
                new BatterySaveLoadStatus(
                    true,
                    expectedLength,
                    BatterySaveGeneration.None,
                    false));
        }

        public static IReadOnlyList<BatterySaveFile> Inspect(string savePath, int expectedLength)
        {
            ValidateArguments(savePath, expectedLength);
            var files = new List<BatterySaveFile>(BackupCount + 1);

            for (int generation = 0; generation <= BackupCount; generation++)
            {
                string candidatePath = GetPath(savePath, generation);
                var info = new FileInfo(candidatePath);
                bool exists = info.Exists;
                bool valid = exists && TryReadExact(candidatePath, expectedLength, out _);
                files.Add(new BatterySaveFile(
                    (BatterySaveGeneration)generation,
                    exists,
                    valid,
                    valid && File.Exists(GetGuardPath(candidatePath)),
                    exists ? info.Length : 0,
                    exists ? info.LastWriteTimeUtc : DateTime.MinValue));
            }

            return files.AsReadOnly();
        }

        public static byte[] ReadBackup(
            string savePath,
            int expectedLength,
            BatterySaveGeneration generation)
        {
            ValidateArguments(savePath, expectedLength);
            if (generation < BatterySaveGeneration.Backup1 ||
                generation > BatterySaveGeneration.Backup3)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(generation),
                    generation,
                    "Only rotating backup generations can be restored.");
            }

            string backupPath = GetPath(savePath, (int)generation);
            if (!TryReadExact(backupPath, expectedLength, out byte[] data))
            {
                throw new InvalidDataException(
                    $"Backup {GetGenerationNumber(generation)} is missing or does not contain exactly {expectedLength} bytes.");
            }

            return data;
        }

        public static void Restore(string savePath, int expectedLength, ReadOnlySpan<byte> data)
        {
            ValidateArguments(savePath, expectedLength);
            if (data.Length != expectedLength)
            {
                throw new InvalidDataException(
                    $"Battery save size mismatch: expected {expectedLength}, got {data.Length}.");
            }

            Write(savePath, data);
        }

        internal static void Write(string savePath, ReadOnlySpan<byte> data)
        {
            if (string.IsNullOrWhiteSpace(savePath))
            {
                throw new ArgumentException("A battery save path is required.", nameof(savePath));
            }

            string? directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var operation = new BatterySaveOperation();
            try
            {
                RotateValidFiles(savePath, data.Length, operation);
                WriteAtomic(savePath, data, operation, 0);
            }
            catch (Exception error)
            {
                operation.Annotate(error);
                throw;
            }
        }

        private static void RotateValidFiles(string savePath, int expectedLength, BatterySaveOperation operation)
        {
            var generations = new List<(string Path, byte[] Data)>(BackupCount);
            for (int generation = 0; generation < BackupCount; generation++)
            {
                operation.SetStage("inspect-backup", generation);
                string path = GetPath(savePath, generation);
                if (TryReadExact(path, expectedLength, out byte[] data))
                {
                    generations.Add((path, data));
                }
            }

            for (int index = generations.Count - 1; index >= 0; index--)
            {
                var source = generations[index];
                string target = GetPath(savePath, index + 1);
                // Keep the current save in place until its replacement is durable.
                // Older guarded generations are already durable: rename, don't
                // rewrite and fsync every byte again on each shutdown.
                string? guard = GuardMatches(GetGuardPath(source.Path), source.Data) ? GetGuardPath(source.Path)
                    : GuardMatches(GetNextGuardPath(source.Path), source.Data) ? GetNextGuardPath(source.Path) : null;
                if (source.Path != savePath && guard is not null)
                {
                    if (source.Path == target) continue;
                    operation.SetStage("rotate-guard", index + 1);
                    File.Move(guard, GetNextGuardPath(target), overwrite: true);
                    operation.SetStage("rotate-data", index + 1);
                    File.Move(source.Path, target, overwrite: true);
                    operation.SetStage("publish-backup-guard", index + 1);
                    File.Move(GetNextGuardPath(target), GetGuardPath(target), overwrite: true);
                    DeleteIfExists(GetGuardPath(source.Path));
                    DeleteIfExists(GetNextGuardPath(source.Path));
                }
                else WriteAtomic(target, source.Data, operation, index + 1);
            }

            operation.SetStage("remove-stale-backups", -1);
            for (int target = generations.Count; target < BackupCount; target++)
            {
                string stalePath = GetPath(savePath, target + 1);
                if (File.Exists(stalePath))
                {
                    File.Delete(stalePath);
                }
                DeleteIfExists(GetGuardPath(stalePath));
                DeleteIfExists(GetNextGuardPath(stalePath));
            }
        }

        private static void WriteAtomic(string destinationPath, ReadOnlySpan<byte> data, BatterySaveOperation operation, int generation)
        {
            string temporaryPath = destinationPath + ".tmp." + Guid.NewGuid().ToString("N");
            string nextGuardPath = GetNextGuardPath(destinationPath);
            try
            {
                operation.SetStage("write-data", generation);
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    FileOptions.None))
                {
                    stream.Write(data);
                    // Commit the complete temporary file once, before publishing it.
                    // WriteThrough on every write plus Flush(true) made shutdown
                    // pay for both barriers, multiplied by both linked players
                    // and their backup generations. The explicit durable flush
                    // remains mandatory; Dispose/Flush() alone is not sufficient.
                    operation.SetStage("flush-data", generation);
                    stream.Flush(flushToDisk: true);
                    operation.SetStage("close-data", generation);
                }

                byte[] guard = CreateGuard(data);
                WriteDurableFile(nextGuardPath, guard, operation, generation);
                operation.SetStage("publish-data", generation);
                File.Move(temporaryPath, destinationPath, overwrite: true);
                operation.SetStage("publish-guard", generation);
                File.Move(nextGuardPath, GetGuardPath(destinationPath), overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static bool TryReadExact(string path, int expectedLength, out byte[] data)
        {
            data = Array.Empty<byte>();
            if (!File.Exists(path) || !HasExpectedLength(path, expectedLength))
            {
                return false;
            }

            byte[] candidate = File.ReadAllBytes(path);
            if (candidate.Length != expectedLength)
            {
                return false;
            }

            string guardPath = GetGuardPath(path);
            if (File.Exists(guardPath) &&
                !GuardMatches(guardPath, candidate) &&
                !GuardMatches(GetNextGuardPath(path), candidate))
            {
                return false;
            }

            data = candidate;
            return true;
        }

        private static byte[] CreateGuard(ReadOnlySpan<byte> data)
        {
            string hash = Convert.ToHexString(SHA256.HashData(data));
            return Encoding.ASCII.GetBytes(
                $"AETHERBOY-BATTERY-SAVE-V1\n{data.Length}\n{hash}\n");
        }

        private static bool GuardMatches(string guardPath, ReadOnlySpan<byte> data)
        {
            if (!File.Exists(guardPath))
            {
                return false;
            }

            try
            {
                string[] lines = File.ReadAllLines(guardPath);
                if (lines.Length < 3 ||
                    !string.Equals(lines[0], "AETHERBOY-BATTERY-SAVE-V1", StringComparison.Ordinal) ||
                    !int.TryParse(lines[1], out int recordedLength) ||
                    recordedLength != data.Length)
                {
                    return false;
                }

                string actualHash = Convert.ToHexString(SHA256.HashData(data));
                return string.Equals(lines[2], actualHash, StringComparison.OrdinalIgnoreCase);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static void WriteDurableFile(string destinationPath, ReadOnlySpan<byte> data, BatterySaveOperation operation, int generation)
        {
            string temporaryPath = destinationPath + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                operation.SetStage("write-guard", generation);
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4 * 1024,
                    FileOptions.None))
                {
                    stream.Write(data);
                    // The guard must be durable before the data file is replaced.
                    operation.SetStage("flush-guard", generation);
                    stream.Flush(flushToDisk: true);
                    operation.SetStage("close-guard", generation);
                }

                operation.SetStage("stage-guard", generation);
                File.Move(temporaryPath, destinationPath, overwrite: true);
            }
            finally
            {
                DeleteIfExists(temporaryPath);
            }
        }

        private static string GetGuardPath(string savePath) => savePath + ".guard";

        private static string GetNextGuardPath(string savePath) => savePath + ".guard.next";

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static bool HasExpectedLength(string path, int expectedLength) =>
            new FileInfo(path).Length == expectedLength;

        private static string GetPath(string savePath, int generation) =>
            generation == 0 ? savePath : savePath + $".bak{generation}";

        private static int GetGenerationNumber(BatterySaveGeneration generation) =>
            (int)generation;

        private static void ValidateArguments(string savePath, int expectedLength)
        {
            if (string.IsNullOrWhiteSpace(savePath))
            {
                throw new ArgumentException("A battery save path is required.", nameof(savePath));
            }
            if (expectedLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedLength));
            }
        }
    }
}
