# UI-Farben und Logo-Abgleich — lokaler Arbeitsstand vom 28. September 2026

Basis: `development` auf `4b56039` (Voltex' Diagnose, ROM-freier Windows-Verbindungstest und Linux-Handoff). Die unten beschriebenen UI-Änderungen liegen derzeit **uncommittet** im Linux-Arbeitsbaum; sie sind noch nicht Teil dieses Upstream-Commits. Bestehende lokale Änderungen am neuen Windows-/Linux-Layout und an den README-Screenshots bleiben erhalten.

## Verhalten

- Die Standard-UI nutzt die Markenfarben `#8B38FF` (primärer Akzent), `#29E2ED` (zweiter Akzent) und `#050712` (Hintergrund) aus `branding/BRAND.md`. Das Logo selbst wird nicht umgefärbt.
- Beide Akzente und der Hintergrund sind global frei wählbar. Die Spielbild-/DMG-Palette bleibt eine getrennte Einstellung. Text, Flächen und die Schrift auf primären Schaltflächen werden für hellere und dunklere Hintergrundfarben abgeleitet.
- Windows: **Settings → Appearance**, drei native Farbdialoge mit Vorschau und „Restore logo colors“. Änderungen erscheinen sofort und werden über den bestehenden Windows-Einstellungsspeicher gesichert.
- Linux/Wayland: **Settings → System → Appearance colors**, drei `#RRGGBB`-Felder mit Vorschau, „Apply colors“ und „Restore logo colors“. Die Werte werden im globalen `settings.json` gespeichert, nicht im Spielprofil.
- Die gemeinsame, plattformneutrale Farblogik liegt in `nanoboy/Core/UiThemePalette.cs`. Die Linux-Testoberfläche für den ROM-freien Verbindungstest aus Voltex' Handoff ist weiterhin eine separate offene Aufgabe.

## Prüfung

- Linux- und Windows-Cross-Build: jeweils 0 Warnungen, 0 Fehler.
- Core-Tests: 234 bestanden, darunter Logo-Defaults, freie Hex-Farben und Textkontrast bei hellen, mittleren und dunklen Hintergründen.
- Linux-Desktop-Tests: 123 erfasst, 83 bestanden, 40 Tests ohne native Sitzung/Opt-in übersprungen. Der gezielte native Wayland-Shelltest wurde zusätzlich mit Opt-in ausgeführt: 1 bestanden; er hat die drei Farben über die Textfelder geändert und nach dem Speichern wieder geladen.
- Screenshots der Standard- und frei gewählten hellen Variante: `artifacts/theme-ui-preview/appearance.png` und `appearance-custom.png` (lokale, ignorierte Testartefakte).

Eine visuelle Windows-Abnahme auf einem Windows-Rechner steht noch aus. Die Windows-Oberfläche wurde unter Linux mit aktiviertem Windows-Targeting kompiliert, aber dort nicht gestartet. Kein Commit, Push oder Server-Deployment durch diese UI-Arbeit.
