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
        var firmware = new ToolStripMenuItem("Firmware Station") { Name = "menuFirmwareManager" };
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
                WindowsFirmwareState.Available => "GRÖSSE OK",
                WindowsFirmwareState.Invalid => "UNGÜLTIG",
                WindowsFirmwareState.Unreadable => "NICHT LESBAR",
                _ => "INTEGRIERT"
            };
            return $"{kind.ToString().ToUpperInvariant()} {state}";
        }));
    }

    private static string DescribeDiagnosticsPreference()
    {
        var decision = WindowsDiagnosticsPreferences.Default.GetStatus();
        if (decision.PreferenceReadError is not null)
            return "Einstellung nicht lesbar · Aufzeichnung bleibt aus. Einstellungen prüfen.";
        if (decision.EnvironmentDisabled)
            return "AETHERBOY_DIAGNOSTICS=0 erzwingt AUS, auch bei gespeicherter Auswahl AN.";
        string choice = decision.RecordNextSession ? "AN" : "AUS";
        string source = decision.SavedPreference.HasValue ? "gespeichert" : "Build-Standard";
        return $"Normaler Neustart: {choice} ({source}). --tester-mode kann einmalig einschalten.";
    }

    private bool SetRecordNextSession(bool enabled)
    {
        bool saved = WindowsDiagnosticsPreferences.Default.TrySetRecordNextSession(enabled, out string? failure);
        if (!saved)
            AetherSignal.Show(controlCenter is { IsDisposed: false } ? controlCenter : this,
                "Die Diagnose-Einstellung konnte nicht gespeichert werden. Die bisherige Auswahl bleibt bestehen.\n\n" + failure,
                "Aufzeichnungswahl nicht gespeichert", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return saved;
    }

    private string DescribeActiveRecording()
    {
        if (testerExportInProgress) return "Bericht wird im Hintergrund exportiert · bitte kurz warten.";
        if (testerSession is null)
            return Program.StartupDiagnosticsDecision?.EnvironmentDisabled == true
                ? "AETHERBOY_DIAGNOSTICS=0 hat die Aufzeichnung für diesen Start deaktiviert."
                : "Live-Ansicht und Kopieren verfügbar. Änderungen am Schalter gelten erst ab Neustart.";
        if (testerSession.ErrorCode is string error)
            return "Aufzeichnung wegen Schreibfehler gestoppt · " + error;
        if (testerSession.SizeLimitReached)
            return "8-MiB-Limit erreicht. Vorhandene Sitzungsdaten bleiben exportierbar.";
        return $"Puffer {testerSession.PendingEvents}/256 · verworfene Ereignisse {testerSession.DroppedEvents}. Auswahl gilt ab Neustart.";
    }
}
