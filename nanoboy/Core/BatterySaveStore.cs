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

            RotateValidFiles(savePath, data.Length);
            WriteAtomic(savePath, data);
        }

        private static void RotateValidFiles(string savePath, int expectedLength)
        {
            var generations = new List<byte[]>(BackupCount);
            for (int generation = 0; generation < BackupCount; generation++)
            {
                if (TryReadExact(GetPath(savePath, generation), expectedLength, out byte[] data))
                {
                    generations.Add(data);
                }
            }

            for (int index = generations.Count - 1; index >= 0; index--)
            {
                WriteAtomic(GetPath(savePath, index + 1), generations[index]);
            }

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

        private static void WriteAtomic(string destinationPath, ReadOnlySpan<byte> data)
        {
            string temporaryPath = destinationPath + ".tmp." + Guid.NewGuid().ToString("N");
            string nextGuardPath = GetNextGuardPath(destinationPath);
            try
            {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    FileOptions.WriteThrough))
                {
                    stream.Write(data);
                    stream.Flush(flushToDisk: true);
                }

                byte[] guard = CreateGuard(data);
                WriteDurableFile(nextGuardPath, guard);
                File.Move(temporaryPath, destinationPath, overwrite: true);
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

        private static void WriteDurableFile(string destinationPath, ReadOnlySpan<byte> data)
        {
            string temporaryPath = destinationPath + ".tmp." + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4 * 1024,
                    FileOptions.WriteThrough))
                {
                    stream.Write(data);
                    stream.Flush(flushToDisk: true);
                }

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
