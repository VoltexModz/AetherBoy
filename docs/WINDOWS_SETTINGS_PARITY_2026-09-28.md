# Windows-Einstellungen nach dem Linux-Update / Settings handoff

## Übernommener Stand

`development` wurde ohne Merge-Commit von `2a91c9f` auf **`9f50dfe`** gebracht
(xJessyX: „Add searchable Linux settings subpages and picture scaling“).
Die vorher offenen Windows-/CI-Korrekturen blieben erhalten. Die nachfolgende
Windows-Erweiterung ist lokal und wurde noch nicht committet oder gepusht.
Fremde Projekte und Dateien im Arbeitsordner wurden nicht verändert.

Grundlage: [Linux-Einstellungsdesign](LINUX_SETTINGS_DESIGN.md).
Die frühere [Windows-UI-Abnahme](WINDOWS_UI_INTEGRATION_2026-09-28.md) bleibt
als Bericht über den vorangegangenen Integrationsschritt erhalten.

## Funktionsabgleich dieses Updates

| Bereich | Windows-Umsetzung |
| --- | --- |
| Suche | Strg+K auch aus dem Hauptfenster; deutsche und englische Suchwörter; Enter öffnet einen Treffer, Esc kehrt zur vorherigen Seite zurück. Die Suche ändert keine Einstellungen. |
| Grafik | Unterseiten Bild, GB-Farben und Leistung. GPU, VSync und Leistungsanzeige bleiben verfügbar. |
| Skalierung | Automatisch, Ganze Pixel und Fenster ausfüllen. Automatisch verwendet bei scharfem Bild/LCD ganze Pixel, bei weichen Kanten eine passende Skalierung. Das Seitenverhältnis bleibt erhalten. GDI und Direct2D erhalten dieselbe Entscheidung. |
| Spielprofile | Neue Skalierungswahl ist pro Spiel überschreibbar. Alte globale und spielbezogene `IntegerScaling`-Werte bleiben beim Umstieg erhalten. |
| Eingabe | Tastatur, Controller, Stick und feste App-Tastenkürzel sind getrennt auffindbar. Tastatur- und Controller-Belegung haben eigene Rücksetzaktionen. |
| Controller | Gerätewechsel, positionsbezogene Tastenbeschriftung, bekannte Xbox-/PlayStation-Beschriftungen, Capture-Abbruch mit Esc; Änderungen bei fehlendem Gerät gesperrt. |
| Stick | Live-Darstellung und Richtungsanzeige, Totzone in 5-%-Schritten sowie Abschaltung des linken Sticks. Auch die Freigabe nach Fokus-/Gerätewechsel berücksichtigt die gewählte Totzone. Das Steuerkreuz bleibt aktiv. |
| App | Bedienung, Spielprofile und Firmware sind getrennt. Farben und Dateipfade bleiben direkt erreichbar. |
| Lesbarkeit | Globale Vergrößerung von Text **und** Bedienelementen auf 100/125/150 %. Neue Dialoge übernehmen sie; das Hauptfenster nach Neustart. Zu große Dialoge sind scrollbar. |
| Fensterwechsel | Optionale Pause für laufende Einzelspiele. Bereits manuell pausierte Spiele bleiben pausiert. Online-Sitzungen werden dadurch nicht automatisch pausiert. |
| Werkzeuge | Bibliothek, Patcher, Audioaufnahme, Cheats und Online Link sind über die Suche und eine Werkzeugseite erreichbar. Vorhandene Aktions- und Sicherheitsprüfungen bleiben zuständig. |

Dies ist der Abgleich des neuen Einstellungs-Updates, **keine pauschale
1:1-Abnahme aller Funktionen beider Frontends**. Linux behält SDL/Wayland und
GTK/AT-SPI; Windows verwendet WinForms mit seinen nativen Zugänglichkeitsrollen.
Linux kann Textgrößen unmittelbar anwenden; Windows verlangt für das Hauptfenster
einen Neustart. Der Windows-Controllerpfad nutzt weiterhin Windows Gaming Input
mit XInput-Fallback. Steuerkreuz-Richtungen bleiben in Windows fest zugeordnet.

## Speicherung und Sicherheit

- `VideoScalingMode`: `0` automatisch, `1` ganzzahlig, `2` passend. Der interne
  Standardwert `-1` liest die bisherige Windows-Wahl, statt sie still zu ändern.
- `UiScalePercent` und `PauseOnFocusLoss` sind global, nicht pro ROM.
- Controller-Einstellungen liegen unter
  `%LOCALAPPDATA%\AetherBoy\Settings\Controllers\<SHA-256>.json` mit atomarem
  Austausch und Sicherung. Der Hash identifiziert Backend, Modell und Gerätename.
  Identische Modelle teilen die Belegung; XInput hat mangels Modellkennung ein
  gemeinsames Profil. Die Auswahl des konkreten Gerätes gilt für die Sitzung.
- Alte Belegungen dienen als Ausgangswert; neue Controlleränderungen verändern
  weder Tastatur- noch ROM-Profilwerte. Ohne eigenes Controllerprofil wird die
  bisherige Belegung weiter verwendet. Fehler beim Speichern behalten den alten
  Wert und werden in der Controllerseite angezeigt.
- Bei Verlust eines ausdrücklich gewählten Controllers wird nicht ungefragt
  auf den Controller eines anderen Spielers umgeschaltet. Lokales Link-Spielen
  behält seine getrennte Zwei-Geräte-Zuordnung.
- Keine Änderung an ROM-/Saveformaten, Link-Drahtprotokollen, TURN-Konfiguration,
  Raumdienst oder Originalspielstandschutz. Keine echten Spielstände für Tests
  eingesetzt und kein WAN-/Pokémon-Tausch bestätigt.

## Zusätzliche Darstellungsfehler behoben

Die Größenprüfung fand einen Fehler in `AetherButton`: Eine vorübergehend
zusammengedrückte Tabellenzeile konnte einen Bogen mit Radius null zeichnen und
einen blockierenden Windows-Fehlerdialog öffnen. Kleine Zeichenflächen sind nun
abgesichert. „Spielstände verwalten“ hat außerdem eine mitskalierende Mindesthöhe,
damit der Button in der flexiblen Tabellenzeile nicht verschwindet.

## Nachweise und verbleibende Abnahme

Release-Build: **0 Warnungen, 0 Fehler**. Testartefakte unter
`artifacts/windows-settings-parity-20260928/`; Windows-Build unter
`artifacts/windows-theme-integration-20260928/bin/nanoboy/release/`.
Die Verzeichnisbezeichnung stammt vom vorherigen Integrationslauf; die Dateien
werden mit dem neuen Stand frisch gebaut. Die gesamte Ausgabe gehört zusammen.

- Core: 234 bestanden.
- Desktop unter Windows: 91 bestanden, 42 plattform-/hardwarebedingt ausgelassen
  (133 insgesamt). Keine native Wayland-Abnahme auf diesem Rechner behauptet.
- Windows Smoke: 228 bestanden, 4 ausdrücklich ausgelassen (232 insgesamt).
  Enthält 18 neue Testfälle für Suche, Skalierung, Migration, Controllerprofile,
  Eingabe-UI, kleine Zeichenflächen und Fokuspausen.
- Runtime: 391 bestanden, 1 POSIX-Fall ausgelassen (392 insgesamt) im separaten
  Abschlusslauf. Der vorherige parallel ausgeführte Lauf hatte einen Timeout in
  `AutonomousArmProgramsExchangeGen3CommandsThroughIndependentOwners(100, false)`
  beim Shutdown. Alle Befehle waren zugestellt; der Owner meldete `Stopping`.
  **Nicht als behobener Corefehler ausgeben:** Ohne Parallelbelastung ließ es sich
  im anschließenden vollständigen Lauf nicht reproduzieren. Beide TRX-Berichte
  bleiben zur Nachprüfung erhalten; keine Timeouts erhöht und keine Tests entfernt.
- Browser-/Raumdienst: 8 bestanden, ausschließlich lokale Testdienste.
- Visuell geprüft: Grafik, Controllerbelegung, Stick-Test sowie Hauptfenster und
  Einstellungen in vergrößerter Darstellung. Screenshots liegen im Unterordner
  `screenshots`. Der Controller in den Aufnahmen ist ein synthetisches Testgerät.
- UI-Copy-Review durchgeführt. Die verbleibenden Hinweise betreffen bestehende
  kompakte Status-/Diagnosezeilen und ältere englische Beschriftungen. Keine
  Protokollkennung für eine Formulierungsänderung umbenannt.

Vor Veröffentlichung noch mit einem echten Controller testen: Gerätewechsel,
Abziehen/Wiederanstecken, GBA-Schultertasten und gewünschte Totzone. Zusätzlich
gemischte Monitor-DPI und große Oberfläche auf kleinen Bildschirmen manuell
prüfen. Ein synthetischer Eingabetest ersetzt diese Geräteabnahme nicht.

## English handoff

Fast-forwarded the shared branch to `9f50dfe` and preserved pending Windows/CI
fixes. The local Windows implementation adds searchable, categorized preferences,
Auto/Integer/Fit picture scaling with legacy profile migration, controller
selection and per-model mappings, live deadzone feedback, optional focus pause,
global interface enlargement and searchable access to existing tools.

No network protocol, ROM/save format, original-save protection or Linux source
was changed by this Windows adaptation. It is not a claim of complete platform
parity or verified WAN trading. Windows interface enlargement takes effect in new
dialogs and after restarting the main window; Linux can change its text size live.
Windows directional D-pad mappings remain fixed. Native device testing and mixed
monitor DPI review are still required. One GBA shutdown test timed out under a
parallel test load; the subsequent full standalone Runtime suite passed unchanged.
Keep that observation in future stability reviews rather than treating it as a
fixed core issue. Commit and push require the user's approval.
