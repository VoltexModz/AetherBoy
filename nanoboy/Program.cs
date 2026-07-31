using System;
using System.IO;
using System.Windows.Forms;

namespace nanoboy
{
    static class Program
    {
        [STAThread]
        static void Main()
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
                Application.Run(new frmNano());
            }
            catch (Exception ex)
            {
                WriteCrashLog(ex);
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
