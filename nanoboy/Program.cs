using System;
using System.IO;
using System.Windows.Forms;

namespace nanoboy
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                WriteCrashLog(e.ExceptionObject as Exception);
            };
            Application.ThreadException += (s, e) =>
            {
                WriteCrashLog(e.Exception);
                MessageBox.Show(
                    "AetherBoy wurde wegen eines unerwarteten Fehlers beendet. " +
                    "Ein Diagnoseprotokoll wurde im lokalen Anwendungsordner gespeichert.",
                    ProductInfo.Name,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Application.Exit();
            };
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            try
            {
                ApplicationConfiguration.Initialize();
                var mainWindow = new frmNano();
                if (TryGetStartupRom(args, out string? startupRom))
                {
                    mainWindow.Shown += (_, _) => mainWindow.LoadRomFile(startupRom);
                }

                Application.Run(mainWindow);
            }
            catch (Exception ex)
            {
                WriteCrashLog(ex);
            }
        }

        private static bool TryGetStartupRom(string[] args, out string? path)
        {
            path = null;
            if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                return false;
            }

            try
            {
                string candidate = Path.GetFullPath(args[0]);
                string extension = Path.GetExtension(candidate);
                if (!extension.Equals(".gb", StringComparison.OrdinalIgnoreCase) &&
                    !extension.Equals(".gbc", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                path = candidate;
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        private static void WriteCrashLog(Exception exception)
        {
            if (exception == null)
            {
                return;
            }

            try
            {
                string logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    ProductInfo.Name,
                    "Logs");
                Directory.CreateDirectory(logDirectory);

                string fileName = $"crash-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.log";
                File.WriteAllText(Path.Combine(logDirectory, fileName), exception.ToString());
            }
            catch
            {
                // Crash reporting must never replace the original failure.
            }
        }
    }
}
