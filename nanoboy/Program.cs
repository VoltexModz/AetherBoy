using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
using nanoboy.Controls;
using nanoboy.Diagnostics;
using nanoboy.Storage;
using AetherBoy.Runtime;

namespace nanoboy
{
    static class Program
    {
        private const string TesterModeArgument = WindowsDiagnosticsPolicy.TesterModeArgument;
        private static WindowsTesterSession? activeTesterSession;

        internal static WindowsDiagnosticsDecision? StartupDiagnosticsDecision { get; private set; }

        [STAThread]
        static void Main(string[] args)
        {
            args = PortableStorage.Configure(args);
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
                    global::AetherBoy.Runtime.Localization.UiText.Get("AetherBoy wurde wegen eines unerwarteten Fehlers beendet. ") +
                    global::AetherBoy.Runtime.Localization.UiText.Get("Ein Diagnoseprotokoll wurde im lokalen Anwendungsordner gespeichert."),
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
                AetherBoy.Runtime.Localization.UiText.Initialize(Properties.Settings.Default.DisplayLanguage);
                try { PortableStorage.EnsureWritable(); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    AetherSignal.Show(global::AetherBoy.Runtime.Localization.UiText.Get("Der portable Datenordner ist nicht beschreibbar. Starte AetherBoy aus einem beschreibbaren Ordner oder ohne Portable Mode.\n\n") + global::AetherBoy.Runtime.Localization.UiText.TechnicalDetails(exception.Message),
                        global::AetherBoy.Runtime.Localization.UiText.Get("Portable Mode nicht gestartet"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                StartupDiagnosticsDecision = WindowsDiagnosticsPreferences.Default.GetStatus(args);
                if (StartupDiagnosticsDecision.PreferenceReadError is string preferenceError)
                {
                    AetherSignal.Show(
                        global::AetherBoy.Runtime.Localization.UiText.Get("Die Diagnoseeinstellung konnte nicht gelesen werden. ") +
                        global::AetherBoy.Runtime.Localization.UiText.Get("AetherBoy läuft vorsichtshalber ohne Sitzungsaufzeichnung weiter. ") +
                        global::AetherBoy.Runtime.Localization.UiText.Format("Lokale Fehlerberichte bleiben aktiv.\n\n{0}", preferenceError),
                        global::AetherBoy.Runtime.Localization.UiText.Get("Diagnoseeinstellung nicht verfügbar"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                if (StartupDiagnosticsDecision.RecordingRequested &&
                    !WindowsTesterSession.TryCreateDefault(
                        out testerSession,
                        out string? failureReason))
                {
                    AetherSignal.Show(
                        global::AetherBoy.Runtime.Localization.UiText.Get("Die lokale Entwicklungsdiagnose konnte nicht gestartet werden. ") +
                        global::AetherBoy.Runtime.Localization.UiText.Format("AetherBoy läuft ohne Aufzeichnung weiter.\n\n{0}", failureReason),
                        global::AetherBoy.Runtime.Localization.UiText.Get("Entwicklungsdiagnose nicht verfügbar"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

                activeTesterSession = testerSession;
                var mainWindow = new frmNano(testerSession);
                if (TryGetStartupRom(args, out string? startupRom))
                {
                    mainWindow.Shown += async (_, _) => await mainWindow.PrepareStartupRomAsync(startupRom);
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
            // Compatibility helper intentionally excludes player settings and the environment.
            // Application startup uses the persisted policy above instead.
            WindowsDiagnosticsPolicy.Resolve(ProductInfo.IsDevelopmentBuild, null, null,
                WindowsDiagnosticsPolicy.HasTesterModeArgument(args)).RecordingRequested;

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
                if (!RomFiles.IsOpenablePath(candidate))
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
            report.AppendLine(global::AetherBoy.Runtime.Localization.UiText.Get("Local diagnostic log · no ROM bytes, ROM paths or telemetry"));
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
