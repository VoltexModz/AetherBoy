# Linux-Fixliste – Restkritik nach Komfortrunde 2

Stand: **12. September 2026**, Basis **`22a77ef`** plus lokale Änderungen.
Diese Liste dokumentiert den Folgeauftrag „alles andere fixen“. Die historische
Kritik in [Runde 2](LINUX_CRITIQUE_ROUND2.md) bleibt als damaliger Befund erhalten;
sie beschreibt nicht automatisch den aktuellen Fehlerstand.

## Änderungen mit nachvollziehbarer Zuordnung

| ID | Vorheriges Problem | Änderung | Hauptdateien / gezielter Nachweis |
| --- | --- | --- | --- |
| R3-01 | Titel nur am Ende editierbar; fehlender Cursor, Teilmarkierung und Zwischenablage | Gemeinsamer Singleline-Editor für Titel, Suche und Cheats; Home/End, Pfeile, Shift-Auswahl, Click/Shift-Click/Drag, Ctrl+A/C/X/V, horizontaler Caret-Scroll. Grapheme werden nicht beim Löschen oder Kürzen zerteilt. Clipboardfehler dürfen keine Auswahl löschen. | `LinuxTextEditor.cs`, `WaylandEmulatorHost.TextEditing.cs`, `LinuxTextEditorTests.cs` |
| R3-02 | Keine sichtbare IME-Komposition; unsichtbare Felder konnten weiter Eingaben abfangen | SDL-Preedit bleibt bis TextInput-Commit getrennt; Auswahl/Caret/Unterstreichung und Kandidatenposition. Escape, Fokusverlust und Feldwechsel räumen Komposition auf. Tab/F6 verlassen den Eingabefokus, ohne den Titel zu verwerfen; erneuter Feldklick setzt fort. | TextEditing-Partial; native Tests für Unicode, IME, Blur, Pointer und AltGr |
| R3-03 | SDL-Zeichenbaum ohne semantische Assistenztechnik | Optionales GTK3-Bedienfenster mit echten Buttons, Toggle-Zuständen, Texten und GtkEntry. Öffnen mit **Ctrl+F7**, System → **Accessible UI** oder `--accessible`. Dieselben Anwendungsaktionen werden anhand aktueller Identitäten validiert; alte/disabled Aktionen werden verworfen. F7 bleibt Rewind. | `LinuxAccessibleControls.cs`, `WaylandEmulatorHost.Accessibility.cs`, `LinuxAccessibilityReviewTests.cs`, `LinuxAccessibilityBusTests.cs` |
| R3-04 | ROM-Pfad, Hash, Migration, Firmware und Profil blockierten die Oberfläche; Continue hashte doppelt | Ein begrenzter Vorbereitungstask, Ladeoverlay auf Hauptseite und Control Center, Cancel/Escape; Continue prüft seine erwartete Identität im selben Auftrag. Verspätete Ergebnisse geben Ressourcen/Locks frei. | `WaylandEmulatorHost.Loading.cs`, `LinuxRomStorage.cs`, `LinuxAsyncOpenTests.cs` |
| R3-05 | Fokuswechsel konnte alten GBA-Owner während Vorbereitung des neuen Owners fortsetzen | Fokus-Resume wird während Laden aufgeschoben; Pause/Capture-Barriere bleibt vor dem neuen GBA-Owner bestehen. Cancel/Fehler/Erfolg wenden den tatsächlichen Fokuszustand anschließend an. | Loading-Partial und FocusLost/FocusGained; GBA→GBA-Fokus-/LateCancel-Regression |
| R3-06 | Regelmäßiger Settings-Flush konnte UI bei langsamer Ablage blockieren | UI serialisiert unveränderliche Byte-Snapshots; ein Schreibworker plus Generationsvergleich. Neuere Änderungen bleiben offen, Fehler retryfähig. Maus-Up, Fokus-, Seiten- und CC-Wechsel blockieren nicht auf Datei-I/O. | `WaylandEmulatorHost.Settings.cs`, SettingsStore/ProfileStore; `LinuxAsyncSettingsTests.cs` |
| R3-07 | Hinter dem Ladeoverlay waren alte Koordinaten-/Tastatur-/Controlleraktionen erreichbar | Busy-Guards gelten für gezeichnete und zwischengespeicherte Aktionen, harte Koordinaten, Keyboard und Gamepad. Cancel bleibt erreichbar. | Host/AetherUi/Controller und AsyncOpen-Tests |
| R3-08 | Headeruntertitel lag bei größter Schrift an der Trennlinie | Acht logische Pixel höher gesetzt; bereits am Ende von Runde 2 anhand eines versteckten Captures bestätigt. | AetherUi; `artifacts/comfort-review/input-large-text.png` |

## Was „Zugänglichkeit“ hier konkret bedeutet

Die normale SDL-Oberfläche bleibt erhalten. Das zusätzliche GTK-Fenster ist eine
alternative Bedienung des Control Centers mit nativen Namen, Rollen, Status,
enabled/checked und editierbaren Texten; die Emulation und Speicherung bleiben
gemeinsam. Bibliothek, Settings, Save-Aktionen, Tools und Mapping verwenden die
bestehenden Anwendungsaktionen. Die Spielgrafik selbst wird dadurch nicht zu
Screenreader-Inhalt. GTK3/ATK sind optionale Systembibliotheken; fehlen sie, zeigt
SDL eine verständliche Fehlermeldung und bleibt bedienbar.

Der GTK-Code läuft ausschließlich auf dem SDL-Owner-Thread. Callback-Signale
werden vor dem Zerstören getrennt; Änderungen spiegeln Werte ohne künstliche
Edit-Events. Strukturwechsel erhalten passende Widgets/Fokus und verwerfen alte
Ziele. Das prüft mehr als nur vorhandene Namen: die Tests aktivieren echte
ATK-Aktionen, lesen Rolle/State und ändern Text über die EditableText-Schnittstelle.
Die separate Busprobe verwendet einen Python-AT-SPI-Client und einen eigenen
Weston-/D-Bus-Testkontext. Sie darf niemals auf dem persönlichen Display laufen.

Ein AT-SPI-DoAction-Reply ist kein Beweis einer ausgeführten Aktion: der GNOME-
Adapter bestätigt die Anfrage vor dem ATK-Aufruf. Deshalb prüft die Busprobe
zusätzlich, dass deaktivierte Aktionen **keinen** Anwendungsauftrag erzeugen,
auch nach einer nachfolgenden Text-Roundtrip-Barriere. Primärquelle:
[GNOME action-adaptor.c](https://raw.githubusercontent.com/GNOME/at-spi2-core/main/atk-adaptor/adaptors/action-adaptor.c).
GTK beschreibt die native Semantik in [Widget.get_accessible](https://docs.gtk.org/gtk3/method.Widget.get_accessible.html).

## Bewusste Sicherungsgrenzen

- Profil-/ROM-Wechsel und Beenden warten auf ausstehende Settings-Writes. Ein
  fehlgeschlagener Flush blockiert den unsicheren Scopewechsel statt Änderungen
  still zu verlieren. Finaler Sessionabschluss und Resume-Schreiben beim Schließen
  sind weiterhin synchron; „die gesamte Anwendung ist asynchron“ wäre falsch.
- Ein bereits im Betriebssystem blockierter einzelner Dateiaufruf lässt sich
  nicht hart abbrechen. Cancel verhindert die spätere Übernahme des Ergebnisses;
  sichere, schon abgeschlossene Migration oder Pfadreparatur wird nicht rückgängig
  gemacht. Original-Saves bleiben erhalten.
- GTK-Fenster folgen den nativen Desktop-Schrift-/Skalierungseinstellungen. Das
  ersetzt keinen realen Test beim Verschieben zwischen Monitoren mit unterschiedlicher DPI.

## Verbleibende Grenzen und manuelle Qualifikation

| Prüfung | Status / nächster Nachweis |
| --- | --- |
| Vollständige Symbol-/Emoji-Fontabdeckung im SDL-Renderer | Offen; Daten bleiben erhalten, GTK verwendet Systemfonts. Kein vollständiger Unicode-Glyphensatz behauptet. |
| Reale Controller, Hotplug während Spiel, native Mapping-Tasten auf verschiedenen Layouts | Nutzergerät wurde nicht verändert. Physisch prüfen; virtuelle Tests separat ausweisen. |
| Gemischte DPI, 120/144-Hz-Monitore, Suspend/Resume und weitere Compositoren | Reale Desktop-/Hardwareprüfung offen. |
| Orca-Sprachausgabe und echte IME-Kandidatenbedienung | Semantische/IME-Ereignistests ersetzen keinen Hör- oder Desktop-Interaktionstest. |
| ARM64-Ausführung | Cross-Publish/ELF/Archivprüfung vorhanden; echter ARM64-Lauf offen. |
| Reale Spiele und 30-Minuten-Läufe | Übernimmt wie vereinbart der Nutzer; keine neuen langen Agentenläufe. |

Abschluss: **UI 9,1/10 / Features 8,9/10**, 108 Headless-Tests bestanden,
3 Audio-Skips, 0 Fehler. Aktuelle Prüfsummen, genaue Testzahlen und die
unabhängige Bewertung sind in
[Playtest Runde 3](LINUX_PLAYTEST_ROUND3.md) und
[Kritik Runde 3](LINUX_CRITIQUE_ROUND3.md) festgehalten. Nicht aus der alten Note
8,9/8,6 oder allein aus der Anzahl neuer Tests eine aktuelle Freigabe ableiten.
