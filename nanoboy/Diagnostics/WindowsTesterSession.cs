using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using AetherBoy.Runtime;
using nanoboy.Input;
using nanoboy.Storage;
using nanoboy.Platform.Audio;

namespace nanoboy.Diagnostics
{
    internal sealed class WindowsTesterSession : IDisposable
    {
        private const int SchemaVersion = 1;
        private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        private readonly object sync = new();
        private readonly StreamWriter writer;
        private readonly DateTimeOffset startedUtc;
        private DateTimeOffset lastHeartbeatUtc;
        private string? lastGamepadIdentity;
        private bool disposed;

        internal WindowsTesterSession(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new ArgumentException(
                    "A tester-session root directory is required.",
                    nameof(rootDirectory));
            }

            startedUtc = DateTimeOffset.UtcNow;
            string sessionName = $"{startedUtc:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..25];
            SessionDirectory = Path.Combine(Path.GetFullPath(rootDirectory), sessionName);
            Directory.CreateDirectory(SessionDirectory);

            LogFilePath = Path.Combine(SessionDirectory, "session.jsonl");
            writer = new StreamWriter(
                new FileStream(
                    LogFilePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read,
                    16 * 1024,
                    FileOptions.WriteThrough),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = true
            };

            File.WriteAllText(
                Path.Combine(SessionDirectory, "README.txt"),
                "AetherBoy local tester report\r\n" +
                "\r\n" +
                "This folder contains structured runtime events for manual playtesting.\r\n" +
                "It contains no ROM bytes, ROM file paths, save-state data, battery-save data or telemetry.\r\n" +
                "Nothing is uploaded automatically. Exporting or sharing the ZIP is always a manual action.\r\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            Record(
                "application.started",
                new
                {
                    product = ProductInfo.Name,
                    version = ProductInfo.Version,
                    build_channel = ProductInfo.BuildChannel,
                    product_build = GetInformationalVersion(typeof(WindowsTesterSession).Assembly),
                    product_binary_id = typeof(WindowsTesterSession).Assembly.ManifestModule.ModuleVersionId,
                    runtime_version = typeof(EmulationSession).Assembly.GetName().Version?.ToString(),
                    runtime_build = GetInformationalVersion(typeof(EmulationSession).Assembly),
                    runtime_binary_id = typeof(EmulationSession).Assembly.ManifestModule.ModuleVersionId,
                    status = ProductInfo.Status,
                    operating_system = RuntimeInformation.OSDescription,
                    process_architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    framework = RuntimeInformation.FrameworkDescription
                });
        }

        public string SessionDirectory { get; }

        public string LogFilePath { get; }

        internal static bool TryCreateDefault(
            out WindowsTesterSession? session,
            out string? failureReason)
        {
            try
            {
                session = new WindowsTesterSession(WindowsDataPaths.Default.Sessions);
                failureReason = null;
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                session = null;
                failureReason = exception.Message;
                return false;
            }
        }

        internal void RecordWindowReady(NanoboySettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            Record(
                "window.ready",
                new
                {
                    video_scale = settings.VideoScaleFactor,
                    display_filter = settings.DisplayFilterIndex,
                    frameskip = settings.Frameskip,
                    audio_enabled = settings.AudioEnable,
                    audio_volume = settings.AudioVolume
                });
        }

        internal void RecordRomLoadRequested(string path)
        {
            Record(
                "rom.load_requested",
                new
                {
                    extension = SafeExtension(path),
                    file_size = TryGetFileLength(path)
                });
        }

        internal void RecordRomLoadRejected(string path, string reason)
        {
            Record(
                "rom.load_rejected",
                new
                {
                    extension = SafeExtension(path),
                    reason = SafeInline(reason, 80)
                });
        }

        internal void RecordRomStarted(RomSnapshot? rom, bool externalBootRom)
        {
            if (rom is null)
            {
                Record(
                    "rom.started",
                    new { identity_ready = false, external_boot_rom = externalBootRom });
                return;
            }

            Record(
                "rom.started",
                new
                {
                    identity_ready = true,
                    title = SafeInline(rom.Title, 32),
                    model = GetModelName(rom),
                    cartridge_type = SafeInline(rom.CartridgeType, 64),
                    rom_size = rom.RomSize,
                    ram_size = rom.RamSize,
                    rom_sha256 = rom.RomSha256,
                    battery_save_enabled = rom.BatterySave.IsEnabled,
                    external_boot_rom = externalBootRom
                });
        }

        internal void RecordOperation(
            string operation,
            int? slot,
            bool succeeded,
            string? reason = null)
        {
            Record(
                "operation.completed",
                new
                {
                    operation = SafeInline(operation, 64),
                    slot,
                    succeeded,
                    reason = SafeInline(reason, 80)
                });
        }

        internal void RecordHealthHint(SessionHealthHint hint, SessionHealthSample sample) =>
            Record("session.health_hint", new { code = hint.Code, duration_ms = hint.DurationMs,
                audio_advancing = hint.AudioAdvancing, suspected_only = true, sample = HealthContext(sample) });

        internal void RecordProblemMarker(SessionHealthSample? sample, SessionHealthSample? recentPlaying = null) =>
            Record("session.problem_marked", new { source = "user", sample = HealthContext(sample), recent_playing = HealthContext(recentPlaying) });

        private static object? HealthContext(SessionHealthSample? sample) => sample == null ? null : new
        {
            state = sample.State.ToString(), emulated_frames = sample.EmulatedFrames,
            video_frames = sample.VideoFrames, presented_frames = sample.PresentedFrames,
            audio_frames = sample.AudioFrames, ui_age_ms = sample.UiAgeMs,
            has_video = sample.HasVideo, uniform_frame = sample.UniformRgb.HasValue, suppressed = sample.Suppressed
        };

        internal void RecordException(string eventName, Exception? exception)
        {
            if (exception is null)
            {
                return;
            }

            Record(
                eventName,
                new
                {
                    exception_type = exception.GetType().FullName,
                    hresult = $"0x{exception.HResult:X8}",
                    target = exception.TargetSite is null
                        ? null
                        : $"{exception.TargetSite.DeclaringType?.FullName}.{exception.TargetSite.Name}"
                });
        }

        internal void RecordHeartbeat(EmulationSnapshot snapshot, NanoboySettings settings,
            AudioOutputSnapshot? audio = null, string? renderer = null, long presentedFrames = 0, long supersededFrames = 0)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(settings);

            DateTimeOffset now = DateTimeOffset.UtcNow;
            lock (sync)
            {
                if (disposed || now - lastHeartbeatUtc < HeartbeatInterval)
                {
                    return;
                }

                lastHeartbeatUtc = now;
            }

            Record(
                "session.heartbeat",
                new
                {
                    state = snapshot.State.ToString(),
                    emulated_frames = snapshot.EmulatedFrameCount,
                    model = snapshot.Rom is null ? "NONE" : GetModelName(snapshot.Rom),
                    diagnostic_event_count = snapshot.DiagnosticEvents.Count,
                    audio_enabled = settings.AudioEnable,
                    frameskip = settings.Frameskip,
                    audio_backend = audio?.Backend,
                    audio_target_latency_ms = audio?.TargetLatencyMs,
                    audio_buffered_ms = audio?.BufferedMs,
                    audio_underruns = audio?.Underruns,
                    audio_dropped_samples = audio?.DroppedSamples,
                    audio_reconnects = audio?.Reconnects,
                    audio_error = audio?.ErrorCode,
                    video_renderer = renderer,
                    video_presented_frames = presentedFrames,
                    video_superseded_frames = supersededFrames,
                    video_vsync_requested = settings.VideoVSync
                });
        }

        internal void RecordGamepadIfChanged(HostGamepadState gamepad)
        {
            string identity = gamepad.IsConnected
                ? $"{gamepad.Source}:{gamepad.VendorId:X4}:{gamepad.ProductId:X4}:{gamepad.DeviceName}"
                : "disconnected";
            lock (sync)
            {
                if (disposed || string.Equals(lastGamepadIdentity, identity, StringComparison.Ordinal))
                {
                    return;
                }

                lastGamepadIdentity = identity;
            }

            Record(
                "input.gamepad_changed",
                new
                {
                    connected = gamepad.IsConnected,
                    source = gamepad.Source.ToString(),
                    vendor_id = gamepad.VendorId == 0 ? null : $"{gamepad.VendorId:X4}",
                    product_id = gamepad.ProductId == 0 ? null : $"{gamepad.ProductId:X4}",
                    device_name = SafeInline(gamepad.DeviceName, 120)
                });
        }

        internal string CreateBundle(string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                throw new ArgumentException("A destination path is required.", nameof(destinationPath));
            }

            string fullDestination = Path.GetFullPath(destinationPath);
            string sessionPrefix =
                SessionDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (fullDestination.StartsWith(sessionPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The report archive cannot be created inside its source folder.");
            }

            string? destinationDirectory = Path.GetDirectoryName(fullDestination);
            if (string.IsNullOrEmpty(destinationDirectory))
            {
                throw new InvalidOperationException(
                    "The report destination has no parent directory.");
            }

            Directory.CreateDirectory(destinationDirectory);
            Record("report.exported", new { format = "zip", automatic_upload = false });
            lock (sync)
            {
                ThrowIfDisposed();
                writer.Flush();
            }

            string temporaryPath = fullDestination + $".{Guid.NewGuid():N}.tmp";
            try
            {
                using (ZipArchive archive = ZipFile.Open(temporaryPath, ZipArchiveMode.Create))
                {
                    foreach (string name in new[] { "README.txt", "session.jsonl" })
                    {
                        string file = Path.Combine(SessionDirectory, name);
                        ZipArchiveEntry entry = archive.CreateEntry(
                            Path.GetFileName(file),
                            CompressionLevel.Optimal);
                        using Stream target = entry.Open();
                        using var source = new FileStream(
                            file,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.ReadWrite | FileShare.Delete,
                            16 * 1024,
                            FileOptions.SequentialScan);
                        source.CopyTo(target);
                    }
                }

                File.Move(temporaryPath, fullDestination, overwrite: true);
                return fullDestination;
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                try
                {
                    WriteEventUnsafe(
                        "application.closed",
                        new
                        {
                            duration_seconds = Math.Max(
                                0,
                                (long)(DateTimeOffset.UtcNow - startedUtc).TotalSeconds)
                        });
                    writer.Dispose();
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException or ObjectDisposedException)
                {
                    // Closing diagnostics must never prevent application shutdown.
                }
            }
        }

        private void Record(string eventName, object? details)
        {
            try
            {
                lock (sync)
                {
                    if (disposed)
                    {
                        return;
                    }

                    WriteEventUnsafe(eventName, details);
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or ObjectDisposedException or JsonException)
            {
                // Tester diagnostics must never interrupt emulation.
            }
        }

        private void WriteEventUnsafe(string eventName, object? details)
        {
            var entry = new Dictionary<string, object?>
            {
                ["schema_version"] = SchemaVersion,
                ["timestamp_utc"] = DateTimeOffset.UtcNow.ToString("O"),
                ["event"] = eventName,
                ["details"] = details
            };
            writer.WriteLine(JsonSerializer.Serialize(entry, JsonOptions));
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(WindowsTesterSession));
            }
        }

        private static string GetModelName(RomSnapshot rom) =>
            rom.IsGameBoyAdvance ? "GBA" : rom.HasColorFeatures ? "CGB" : "DMG";

        private static string? GetInformationalVersion(Assembly assembly) =>
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

        private static string SafeExtension(string? path)
        {
            try
            {
                return Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
        }

        private static long? TryGetFileLength(string path)
        {
            try
            {
                return new FileInfo(path).Length;
            }
            catch (Exception exception) when (
                exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return null;
            }
        }

        private static string? SafeInline(string? value, int maximumLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string sanitized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return sanitized.Length <= maximumLength
                ? sanitized
                : sanitized[..maximumLength];
        }
    }
}
