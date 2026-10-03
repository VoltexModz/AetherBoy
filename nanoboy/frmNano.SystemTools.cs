using System;
using System.Linq;
using System.Windows.Forms;
using nanoboy.Controls;
using nanoboy.Diagnostics;
using nanoboy.Storage;

namespace nanoboy;

public partial class frmNano
{
    private string? firmwareLoadWarning;
    private bool testerExportInProgress;

    private void InitializeSystemTools()
    {
        var firmware = new nanoboy.Controls.AetherCommand("Firmware Station") { Name = "menuFirmwareManager" };
        firmware.Click += (_, _) => OpenFirmwareManager();
        menuItem21.DropDownItems.Add(firmware);
    }

    private void OpenFirmwareManager()
    {
        using var manager = new frmFirmwareManager(new WindowsFirmwareStore(WindowsDataPaths.Default), settings);
        manager.ShowDialog(controlCenter is { IsDisposed: false } ? controlCenter : this);
    }

    private static string DescribeFirmware()
    {
        var store = new WindowsFirmwareStore(WindowsDataPaths.Default);
        return string.Join("  ·  ", Enum.GetValues<WindowsFirmwareKind>().Select(kind =>
        {
            var status = store.GetStatus(kind);
            string state = status.State switch
            {
                WindowsFirmwareState.Available => global::AetherBoy.Runtime.Localization.UiText.Get("GRÖSSE OK"),
                WindowsFirmwareState.Invalid => global::AetherBoy.Runtime.Localization.UiText.Get("UNGÜLTIG"),
                WindowsFirmwareState.Unreadable => global::AetherBoy.Runtime.Localization.UiText.Get("NICHT LESBAR"),
                _ => global::AetherBoy.Runtime.Localization.UiText.Get("INTEGRIERT")
            };
            return $"{kind.ToString().ToUpperInvariant()} {state}";
        }));
    }

    private static string DescribeDiagnosticsPreference()
    {
        var decision = WindowsDiagnosticsPreferences.Default.GetStatus();
        if (decision.PreferenceReadError is not null)
            return global::AetherBoy.Runtime.Localization.UiText.Get("Einstellung nicht lesbar · Aufzeichnung bleibt aus. Einstellungen prüfen.");
        if (decision.EnvironmentDisabled)
            return global::AetherBoy.Runtime.Localization.UiText.Get("AETHERBOY_DIAGNOSTICS=0 erzwingt AUS, auch bei gespeicherter Auswahl AN.");
        string choice = decision.RecordNextSession ? global::AetherBoy.Runtime.Localization.UiText.Get("AN") : global::AetherBoy.Runtime.Localization.UiText.Get("AUS");
        string source = decision.SavedPreference.HasValue ? global::AetherBoy.Runtime.Localization.UiText.Get("gespeichert") : global::AetherBoy.Runtime.Localization.UiText.Get("Build-Standard");
        return global::AetherBoy.Runtime.Localization.UiText.Format("Normaler Neustart: {0} ({1}). --tester-mode kann einmalig einschalten.", choice, source);
    }

    private bool SetRecordNextSession(bool enabled)
    {
        bool saved = WindowsDiagnosticsPreferences.Default.TrySetRecordNextSession(enabled, out string? failure);
        if (!saved)
            AetherSignal.Show(controlCenter is { IsDisposed: false } ? controlCenter : this,
                global::AetherBoy.Runtime.Localization.UiText.Get("Die Diagnose-Einstellung konnte nicht gespeichert werden. Die bisherige Auswahl bleibt bestehen.\n\n") + failure,
                global::AetherBoy.Runtime.Localization.UiText.Get("Aufzeichnungswahl nicht gespeichert"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return saved;
    }

    private string DescribeActiveRecording()
    {
        if (testerExportInProgress) return global::AetherBoy.Runtime.Localization.UiText.Get("Bericht wird im Hintergrund exportiert · bitte kurz warten.");
        if (testerSession is null)
            return Program.StartupDiagnosticsDecision?.EnvironmentDisabled == true
                ? global::AetherBoy.Runtime.Localization.UiText.Get("AETHERBOY_DIAGNOSTICS=0 hat die Aufzeichnung für diesen Start deaktiviert.")
                : global::AetherBoy.Runtime.Localization.UiText.Get("Live-Ansicht und Kopieren verfügbar. Änderungen am Schalter gelten erst ab Neustart.");
        if (testerSession.ErrorCode is string error)
            return global::AetherBoy.Runtime.Localization.UiText.Get("Aufzeichnung wegen Schreibfehler gestoppt · ") + error;
        if (testerSession.SizeLimitReached)
            return global::AetherBoy.Runtime.Localization.UiText.Get("8-MiB-Limit erreicht. Vorhandene Sitzungsdaten bleiben exportierbar.");
        return global::AetherBoy.Runtime.Localization.UiText.Format("Puffer {0}/256 · verworfene Ereignisse {1}. Auswahl gilt ab Neustart.", testerSession.PendingEvents, testerSession.DroppedEvents);
    }
}
