using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Security;
using System.Text.Json;
using nanoboy.Storage;

namespace nanoboy.Diagnostics;

internal sealed record WindowsDiagnosticsDecision(
    bool RecordNextSession,
    bool RecordingRequested,
    bool EnvironmentDisabled,
    bool TesterModeRequested,
    bool? SavedPreference,
    string? PreferenceReadError = null);

internal static class WindowsDiagnosticsPolicy
{
    internal const string TesterModeArgument = "--tester-mode";
    internal const string EnvironmentVariable = "AETHERBOY_DIAGNOSTICS";

    internal static bool HasTesterModeArgument(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return Array.Exists(args, argument => string.Equals(
            argument, TesterModeArgument, StringComparison.OrdinalIgnoreCase));
    }

    // An explicit tester launch overrides the saved next-session choice for that process only.
    // The environment's privacy switch always wins; other values do not opt a stable build in.
    internal static WindowsDiagnosticsDecision Resolve(
        bool developmentBuild,
        bool? savedPreference,
        string? environmentValue,
        bool testerModeRequested)
    {
        bool nextSession = savedPreference ?? developmentBuild;
        bool environmentDisabled = string.Equals(environmentValue?.Trim(), "0", StringComparison.Ordinal);
        return new WindowsDiagnosticsDecision(nextSession,
            !environmentDisabled && (testerModeRequested || nextSession),
            environmentDisabled, testerModeRequested, savedPreference);
    }
}

internal sealed class WindowsDiagnosticsPreferences
{
    internal static WindowsDiagnosticsPreferences Default { get; } = new(
        ReadDefaultPreference,
        value => Properties.Settings.Default.DiagnosticsRecording = value,
        () => Properties.Settings.Default.Save(),
        ProductInfo.IsDevelopmentBuild,
        () => Environment.GetEnvironmentVariable(WindowsDiagnosticsPolicy.EnvironmentVariable));

    private readonly Func<string> read;
    private readonly Action<string> write;
    private readonly Action save;
    private readonly bool developmentBuild;
    private readonly Func<string?> environment;

    private static string ReadDefaultPreference()
    {
        ValidateReadableStorage(WindowsDataPaths.Default.SettingsFile);
        return Properties.Settings.Default.DiagnosticsRecording;
    }

    internal static void ValidateReadableStorage(string settingsPath)
    {
        bool malformed = false;
        foreach (string path in new[] { settingsPath, settingsPath + ".bak" })
        {
            try
            {
                using FileStream source = File.OpenRead(path);
                if (JsonSerializer.Deserialize<Dictionary<string, string?>>(source) is not null) return;
                malformed = true;
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            catch (JsonException) { malformed = true; }
        }
        // The general settings provider recovers defaults after two malformed files. For
        // recording this must not silently replace a potentially explicit privacy opt-out.
        if (malformed)
            throw new ConfigurationErrorsException("Die Diagnoseeinstellung ist nicht lesbar; auch die Einstellungs-Sicherung konnte nicht gelesen werden.");
    }

    // Storage and environment access are injected so policy tests never alter player settings.
    internal WindowsDiagnosticsPreferences(Func<string> read, Action<string> write, Action save,
        bool developmentBuild, Func<string?> environment)
    {
        this.read = read ?? throw new ArgumentNullException(nameof(read));
        this.write = write ?? throw new ArgumentNullException(nameof(write));
        this.save = save ?? throw new ArgumentNullException(nameof(save));
        this.developmentBuild = developmentBuild;
        this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    // With no arguments this describes an ordinary next launch, not the active recorder.
    internal WindowsDiagnosticsDecision GetStatus(string[]? args = null)
    {
        bool testerMode = WindowsDiagnosticsPolicy.HasTesterModeArgument(args ?? Array.Empty<string>());
        string? environmentValue = null;
        try
        {
            environmentValue = environment();
            string value = read();
            bool? savedPreference = string.IsNullOrWhiteSpace(value) ? null
                : bool.TryParse(value, out bool parsed) ? parsed
                : throw new FormatException("Die gespeicherte Diagnoseeinstellung ist ungültig.");
            return WindowsDiagnosticsPolicy.Resolve(developmentBuild, savedPreference, environmentValue, testerMode);
        }
        catch (Exception exception) when (IsPreferenceFailure(exception))
        {
            // Never silently re-enable recording when an earlier privacy choice cannot be read,
            // including an explicit tester launch. Minimal crash logs remain independent.
            return WindowsDiagnosticsPolicy.Resolve(developmentBuild, false, environmentValue, testerMode) with
            {
                RecordingRequested = false,
                SavedPreference = null,
                PreferenceReadError = exception.Message
            };
        }
    }

    internal bool TrySetRecordNextSession(bool enabled, out string? failureReason)
    {
        string? previous = null;
        bool originalRead = false;
        try
        {
            previous = read();
            originalRead = true;
            write(enabled ? "True" : "False");
            // NanoboySettings' general PropertyChanged handler can swallow an auto-save error.
            // Require this explicit save to succeed before the UI reports persistence.
            save();
            failureReason = null;
            return true;
        }
        catch (Exception exception) when (IsPreferenceFailure(exception))
        {
            if (originalRead)
            {
                try { write(previous ?? string.Empty); }
                catch (Exception rollbackException) when (IsPreferenceFailure(rollbackException)) { }
            }
            failureReason = exception.Message;
            return false;
        }
    }

    private static bool IsPreferenceFailure(Exception exception) => exception is
        IOException or UnauthorizedAccessException or ConfigurationErrorsException or
        FormatException or InvalidCastException or ArgumentException or NotSupportedException or SecurityException;
}
