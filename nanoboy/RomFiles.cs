using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace nanoboy
{
    internal static class RomFiles
    {
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
                    extension.Equals(".gbc", StringComparison.OrdinalIgnoreCase);
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
                !IsSupportedPath(files[0]))
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
                {
                    return Array.Empty<string>();
                }

                return File.ReadAllLines(path)
                    .Where(RomFiles.IsSupportedPath)
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
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ProductInfo.Name,
                "recent-roms.txt");
        }
    }
}
