using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Threading.Channels;

namespace AetherBoy.Desktop;

internal static class LinuxBuildInfo
{
    private static readonly Assembly Assembly = typeof(LinuxBuildInfo).Assembly;
    public static string Version => Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
    public static string Channel => Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(item => item.Key == "AetherBoyChannel")?.Value ?? "development";
    public static bool RecordByDefault => Channel == "development" && Environment.GetEnvironmentVariable("AETHERBOY_DIAGNOSTICS") != "0";
}

/// <summary>Bounded, local-only diagnostic events. Free-form exception messages and ROM paths are excluded.</summary>
internal sealed class LinuxDiagnostics : IDisposable
{
    private readonly Channel<(string? Line, TaskCompletionSource? Completion)> queue = Channel.CreateBounded<(string? Line, TaskCompletionSource? Completion)>(new BoundedChannelOptions(256)
        { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    private readonly Task writer;
    private readonly object fileSync = new();
    private bool disposed;
    public string? ReportPath { get; }
    public string? Error { get; private set; }
    public bool Enabled => ReportPath is not null && Error is null && !disposed;

    public LinuxDiagnostics(LinuxDataPaths paths, bool enabled)
    {
        if (!enabled) { writer = Task.CompletedTask; return; }
        try
        {
            string directory = Path.Combine(paths.State, "sessions");
            Directory.CreateDirectory(directory);
            foreach (string old in Directory.GetFiles(directory, "*.jsonl").OrderByDescending(File.GetLastWriteTimeUtc).Skip(19))
                try { File.Delete(old); } catch (IOException) { }
            ReportPath = Path.Combine(directory, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.jsonl");
            File.WriteAllText(ReportPath, "");
            writer = Task.Run(WriteLoop);
            Record("started", new { schema = 1, build = LinuxBuildInfo.Version, channel = LinuxBuildInfo.Channel,
                platform = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString() });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { Error = exception.GetType().Name; writer = Task.CompletedTask; }
    }

    public void Record(string kind, object? detail = null)
    {
        if (Enabled) queue.Writer.TryWrite((JsonSerializer.Serialize(new { utc = DateTime.UtcNow, kind, detail }), null));
    }

    public void Failure(string action, Exception exception) =>
        Record("error", new { action, type = exception.GetBaseException().GetType().Name, code = exception.HResult });

    private async Task WriteLoop()
    {
        try
        {
            await foreach (var item in queue.Reader.ReadAllAsync())
                lock (fileSync)
                {
                    if (item.Line is not null && new FileInfo(ReportPath!).Length < 8 * 1024 * 1024)
                        File.AppendAllText(ReportPath!, item.Line + "\n");
                    item.Completion?.TrySetResult();
                }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { Error = exception.GetType().Name; }
    }

    public string Export(string directory)
    {
        if (ReportPath is null) throw new InvalidOperationException("Session recording is disabled.");
        var checkpoint = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!queue.Writer.TryWrite((null, checkpoint)) || !checkpoint.Task.Wait(TimeSpan.FromSeconds(5)))
            throw new IOException("The diagnostic writer could not finish the export. Try again.");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"aetherboy-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.zip");
        lock (fileSync)
        {
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            zip.CreateEntryFromFile(ReportPath, "session.jsonl");
        }
        return path;
    }

    public void Dispose()
    {
        if (disposed) return;
        Record("stopped");
        disposed = true;
        queue.Writer.TryComplete();
        writer.Wait(TimeSpan.FromSeconds(2));
    }
}
