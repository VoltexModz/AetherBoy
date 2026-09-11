using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
using nanoboy.Controls;
using nanoboy.Diagnostics;
using nanoboy.Storage;

namespace nanoboy
{
    static class Program
    {
        private const string TesterModeArgument = "--tester-mode";
        private static WindowsTesterSession? activeTesterSession;

        [STAThread]
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Exception? exception = e.ExceptionObject as Exception;
                activeTesterSession?.RecordException(
                    "application.unhandled_exception",
                    exception);
                WriteCrashLog(exception);
            };
            Application.ThreadException += (s, e) =>
            {
                activeTesterSession?.RecordException(
                    "application.ui_thread_exception",
                    e.Exception);
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

            WindowsTesterSession? testerSession = null;
            try
            {
                ApplicationConfiguration.Initialize();
                if (IsTesterModeRequested(args) &&
                    !WindowsTesterSession.TryCreateDefault(
                        out testerSession,
                        out string? failureReason))
                {
                    AetherSignal.Show(
                        "Die lokale Entwicklungsdiagnose konnte nicht gestartet werden. " +
                        $"AetherBoy läuft ohne Aufzeichnung weiter.\n\n{failureReason}",
                        "Entwicklungsdiagnose nicht verfügbar",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

                activeTesterSession = testerSession;
                var mainWindow = new frmNano(testerSession);
                if (TryGetStartupRom(args, out string? startupRom))
                {
                    mainWindow.Shown += (_, _) => mainWindow.LoadRomFile(startupRom);
                }

                Application.Run(mainWindow);
            }
            catch (Exception ex)
            {
                activeTesterSession?.RecordException("application.startup_failed", ex);
                WriteCrashLog(ex);
            }
            finally
            {
                activeTesterSession = null;
                testerSession?.Dispose();
            }
        }

        internal static bool IsTesterModeRequested(string[] args) =>
            ProductInfo.IsDevelopmentBuild || Array.Exists(
                args,
                argument => string.Equals(
                    argument,
                    TesterModeArgument,
                    StringComparison.OrdinalIgnoreCase));

        private static bool TryGetStartupRom(string[] args, out string? path)
        {
            path = null;
            string[] positionalArguments = Array.FindAll(
                args,
                argument => !string.Equals(
                    argument,
                    TesterModeArgument,
                    StringComparison.OrdinalIgnoreCase));
            if (positionalArguments.Length != 1 ||
                string.IsNullOrWhiteSpace(positionalArguments[0]))
            {
                return false;
            }

            try
            {
                string candidate = Path.GetFullPath(positionalArguments[0]);
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
                string logDirectory = WindowsDataPaths.Default.CrashLogs;
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
