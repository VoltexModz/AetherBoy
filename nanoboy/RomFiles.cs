using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Forms;
using nanoboy.Storage;

namespace nanoboy
{
    internal static class RomFiles
    {
        public static bool IsOpenablePath(string? path) => IsSupportedPath(path) ||
            (!string.IsNullOrWhiteSpace(path) && AetherBoy.Runtime.Cartridges.RomArchiveSource.IsArchive(path));
        public static bool IsSupportedPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                string extension = Path.GetExtension(path);
                return extension.Equals(".gb", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".gbc", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".gba", StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public static bool TryGetSingleDrop(IDataObject? data, out string? path)
        {
            path = null;
            if (data?.GetData(DataFormats.FileDrop) is not string[] files ||
                files.Length != 1 ||
                !IsOpenablePath(files[0]))
            {
                return false;
            }

            path = files[0];
            return true;
        }
    }

    internal static class RecentRomStore
    {
        private const int MaximumEntries = 8;

        public static IReadOnlyList<string> Load()
        {
            try
            {
                string path = GetStorePath();
                if (!File.Exists(path))
                    path = Path.Combine(WindowsDataPaths.Default.Root, "recent-roms.txt");
                if (!File.Exists(path))
                {
                    return Array.Empty<string>();
                }

                return File.ReadAllLines(path)
                    .Select(Resolve)
                    .Where(RomFiles.IsSupportedPath)
                    .OfType<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(MaximumEntries)
                    .ToArray();
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Debug.WriteLine($"Could not read recent ROM history: {exception}");
                return Array.Empty<string>();
            }
        }

        public static void Save(IEnumerable<string> paths)
        {
            try
            {
                string storePath = GetStorePath();
                string? directory = Path.GetDirectoryName(storePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string[] entries = paths
                    .Where(RomFiles.IsSupportedPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(MaximumEntries)
                    .Select(Encode)
                    .ToArray();
                string temporaryPath = storePath + ".tmp";
                File.WriteAllLines(temporaryPath, entries);
                File.Move(temporaryPath, storePath, overwrite: true);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Debug.WriteLine($"Could not persist recent ROM history: {exception}");
            }
        }

        private static string GetStorePath()
        {
            return WindowsDataPaths.Default.RecentRoms;
        }

        private const string ManagedPrefix = "managed:";

        private static string Encode(string path)
        {
            try { return ManagedPrefix + new WindowsRomLibrary(WindowsDataPaths.Default).GetIdentity(path); }
            catch (InvalidOperationException) { return Path.GetFullPath(path); }
        }

        private static string? Resolve(string entry)
        {
            try
            {
                if (entry.StartsWith(ManagedPrefix, StringComparison.Ordinal))
                    return FindManaged(entry[ManagedPrefix.Length..]);
                if (!Path.IsPathFullyQualified(entry) || !RomFiles.IsSupportedPath(entry)) return null;
                // Legacy absolute references can survive a folder/drive change. Only a
                // matching content hash in the current managed library may replace them.
                string? parent = Path.GetDirectoryName(entry);
                if (string.Equals(Path.GetFileName(Path.GetDirectoryName(parent)), "Roms", StringComparison.OrdinalIgnoreCase))
                    return FindManaged(Path.GetFileName(parent)) ?? entry;
                return entry;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            { return null; }
        }

        private static string? FindManaged(string? identity)
        {
            if (identity is not { Length: 64 } || !identity.All(Uri.IsHexDigit)) return null;
            string directory = Path.Combine(WindowsDataPaths.Default.Roms, identity);
            if (!Directory.Exists(directory)) return null;
            foreach (string file in Directory.EnumerateFiles(directory).Where(RomFiles.IsSupportedPath))
            {
                using var input = File.OpenRead(file);
                if (input.Length <= 32 * 1024 * 1024 &&
                    Convert.ToHexString(SHA256.HashData(input)).Equals(identity, StringComparison.OrdinalIgnoreCase)) return file;
            }
            return null;
        }
    }
}
