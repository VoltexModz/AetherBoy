using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
using nanoboy.Controls;

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
                AetherSignal.Show(
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
                if (!RomFiles.IsSupportedPath(candidate))
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

        internal static void WriteCrashLog(Exception exception)
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
                File.WriteAllText(
                    Path.Combine(logDirectory, fileName),
                    BuildPrivacySafeExceptionReport(exception));
            }
            catch
            {
                // Crash reporting must never replace the original failure.
            }
        }

        private static string BuildPrivacySafeExceptionReport(Exception exception)
        {
            var report = new StringBuilder();
            report.AppendLine(ProductInfo.DisplayName);
            report.AppendLine(DateTimeOffset.Now.ToString("O"));
            report.AppendLine("Local diagnostic log · no ROM bytes, ROM paths or telemetry");
            int depth = 0;
            for (Exception? current = exception; current is not null && depth < 8; current = current.InnerException, depth++)
            {
                report.AppendLine();
                report.AppendLine($"Exception[{depth}] {current.GetType().FullName}");
                report.AppendLine($"HResult      0x{current.HResult:X8}");
                if (current.TargetSite is not null)
                    report.AppendLine($"Target       {current.TargetSite.DeclaringType?.FullName}.{current.TargetSite.Name}");
                string stack = new StackTrace(current, fNeedFileInfo: false).ToString();
                if (!string.IsNullOrWhiteSpace(stack))
                    report.Append(stack);
            }
            return report.ToString();
        }
    }
}
