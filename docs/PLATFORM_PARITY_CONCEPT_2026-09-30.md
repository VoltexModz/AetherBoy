# Windows und Linux: Funktionsabgleich und gemeinsamer Umsetzungsplan

Stand: 30. September 2026. Dieser Abgleich beschreibt den Quellstand `7ced237` auf `development` und die am Windows-Arbeitsplatz vorhandenen, **noch nicht committeten** Änderungen. Er ist eine Bestandsaufnahme und ein Konzept, keine Freigabe als fertiger Online-Tausch-Emulator. Weder ein Commit noch ein Push oder eine Änderung am Raumdienst ist Teil dieser Arbeit.

## Was „gleicher Stand“ hier bedeutet

Beide Programme verwenden denselben [Core](../nanoboy/Core) und dieselbe [Runtime](../nanoboy/Runtime). Der Unterschied liegt vor allem darin, was die Oberflächen tatsächlich anbieten, wie Dateien sicher verwaltet werden und was auf echten Geräten geprüft wurde. Ziel ist **Funktionsparität**, nicht ein pixelgleiches Fenster oder dieselbe Audio-/Grafik-API: Windows bleibt WinForms mit seinen nativen Ausgaben; Linux bleibt SDL/Wayland mit XDG-Ablage und Hyprland-Unterstützung.

**Status in der Tabelle:** „Ja“ = aus der Oberfläche aufrufbar; „Teilweise“ = gemeinsame Funktion oder verwandter Ablauf vorhanden, aber die konkrete Bedienfunktion fehlt; „Offen“ = nicht nachgewiesen. Ein grüner Unit-Test ist kein bestätigter Pokémon-Tausch. Bei den Windows-Einstellungen/Farben/Controllerseiten ist „lokal“ ausdrücklich **noch nicht der gepushte Stand**.

## Tatsächlicher Funktionsabgleich

| Bereich | Windows | Linux | Handlungsbedarf |
| --- | --- | --- | --- |
| GB, GBC, GBA und gemeinsame Emulationslaufzeit | Ja | Ja | Gemeinsame Regressionen; GBA-Spielkompatibilität nicht pauschal behaupten. |
| Audio, Stereo, Turbo, Screenshot, FPS-Anzeige | Ja | Ja | Backend-spezifisch testen: Windows-Ausgabe gegen SDL/PipeWire, besonders Turbo und Gerätewechsel. |
| Pause, Rewind, Savestates, Resume, State-Galerie | Ja | Ja | Je System und bei ROM-/Profilwechsel prüfen; Zustandsschemata gemeinsam halten. |
| ROM-Patcher IPS/BPS/UPS und UPS-Rückwärtslauf | Ja | Ja | Gemeinsame Patcher-Tests; UI-Texte dürfen differieren. |
| Cheats und WAV-Aufnahme | Ja | Ja | Spiel- und Audiosession-Lebenszyklus auf beiden Systemen prüfen. |
| Firmware DMG/CGB/GBA, Größenprüfung | Ja | Ja | Start mit/ohne eigene Firmware je Plattform testen. |
| Spielprofile, Fokus-Pause, Farbschema, Bildskalierung | Lokal ja; aktuelle Windows-Unterseiten sind uncommittet | Ja | Windows-Arbeit separat abnehmen und erst nach Freigabe integrieren. |
| Einstellungssuche, Controller-Auswahl und Stick-Totzone | Lokal ja; uncommittet | Ja | Gleiche auffindbare Funktionen, nicht dieselbe UI-Hierarchie. |
| Steuerkreuz-Richtungen frei neu belegen | Teilweise: Windows-Controllerprofil remappt Aktionsknöpfe; Richtungen bleiben fest | Ja | Windows-Richtungsbelegung ergänzen oder bewusst als Plattformgrenze dokumentieren. |
| Bibliothek: Suche, Systemfilter, Favoriten, Spielzeit, Fortsetzen | Ja | Ja | Gemeinsame Metadaten-/Migrationsfälle, keine ROMs ins Repo. |
| Bibliothek: Sortierwahl, Kachel-/Listenansicht, Spielbild-Vorschau | Ja | Teilweise: Liste und State-Galerie, aber diese Bibliotheksfunktionen fehlen | Linux ergänzen, sofern die bestehende kleine Seitenansicht sinnvoll bleibt. |
| Eigener Titel und Pfad-Neuzuordnung für verschobene ROM | Teilweise: verwaltete ROM-Kopie, aber kein Titel-Editor | Ja | Titel-Editor auf Windows; Neuzuordnung nur dort, wo externe Originalpfade verwendet werden. |
| Automatische Übernahme einer benachbarten `.sav` beim ROM-Import | Ja | Eigene Save-Migrationslogik | Sicherungs- und Identitätsverhalten plattformübergreifend spezifizieren. |
| Gezielter `.sav`-Import, Export des Save-/State-Familienarchivs | Teilweise: Backups wiederherstellbar, aber keine manuelle Import-/Export-Aktion | Ja | **Windows-Priorität**; bestehenden Windows-Suchtext zu Save-Import bis dahin nicht als erfülltes Feature werten. |
| Lokaler Link mit zwei Spielen in einer Oberfläche | Ja, Local Link Lab für GB/GBC/GBA | Teilweise: gemeinsame `LocalLinkSession` vorhanden, kein Linux-Einstieg/Zwei-Spiel-UI | **Linux-Priorität**; keine zweite Kabel-Runtime schreiben. |
| Online-Räume, Sitzungsspielstandkopien und Schutz der Originale | Ja, Entwicklungsstand | Ja, Entwicklungsstand | Gleiches Protokoll und gleiche Sicherheitsregeln; echte Tausche bleiben offen. |
| ROM-freier nativer Raum-/Transporttest in der Oberfläche | Ja | Teilweise: gemeinsamer Probe und CLI vorhanden, keine Linux-Seite | **Linux-Priorität**; Probe ohne ROM nutzbar machen. |
| Quick Deck für Pause/Slot/Bild ohne Tastatur | Ja | Teilweise: Control Center ist per Controller bedienbar, aber kein entsprechendes Quick Deck | Linux-Kurzmenü ergänzen oder vorhandene Bedienung mit gleicher Aktionszahl nachweisen. |
| Bildschirmtastatur für Texteingaben mit Controller | Ja | Teilweise: Texteingabe und Controller-Menünavigation, aber keine Bildschirmtastatur | Linux ergänzen, damit ein Controller allein für Raumcode/Suche reicht. |
| Audio-Inspector mit PSG- und GBA-Direct-Sound-Werten | Ja | Teilweise: Kanal-/Lautstärkeeinstellungen und Aufnahme, aber kein Inspector | Linux-Inspector aus gemeinsamem Snapshot-Vertrag ableiten. |
| Diagnose: begrenzte lokale JSONL-Berichte, ZIP-Export | Ja | Ja | Schema/Redaktion und Testmarker abgleichen; keine Secrets, Roh-SDPs oder Save-Inhalte exportieren. |
| Automatische Sitzungsgesundheit und „Problem markieren“ | Ja | Teilweise: Status-/Fehlerseite, kein vergleichbarer Hint-/Marker-Ablauf | Gemeinsame Auswertungslogik prüfen, Linux-Bedienung ergänzen. |
| ROM-Vorbereitung abbrechbar im Hintergrund | Teilweise: Import/Session-Erzeugung im UI-Aufruf | Ja | Windows asynchron und abbrechbar machen; altes Spiel bis zur sicheren Übergabe erhalten. |
| Settings-/Profil-Schreibarbeit ohne UI-Blockade | Teilweise: atomare Speicherung, aber synchrone Aufrufer | Ja | Windows mit Snapshot-/Worker-Verfahren prüfen; Flush beim Beenden garantieren. |
| Screenreader-/semantische Oberfläche | Nicht als vollständiger realer Screenreader-Test nachgewiesen | Optionaler GTK/AT-SPI-Pfad; reale Orca-Prüfung offen | Für beide getrennt manuell qualifizieren, nicht aus API-Präsenz ableiten. |
| Reproduzierbares Desktop-Paket | Windows-Publish/Build vorhanden; kein entsprechender Paket-Script-/Freigabeablauf im Repo | x64-/ARM64-Paketskript und Installationspfad vorhanden; ARM64-Ausführung offen | Windows-Paketierung ergänzen, Linux-ARM64 tatsächlich starten und prüfen. |

Die wichtigsten Codebelege: Windows-[ROM-Bibliothek](../nanoboy/frmRomLibrary.cs), [Save Safety Center](../nanoboy/frmBatterySaveManager.cs), [Local Link Lab](../nanoboy/frmLocalLinkLab.cs), [ROM-Ladepfad](../nanoboy/frmNano.cs), [Online-Probe-Dialog](../nanoboy/frmOnlineConnectionTest.cs), [Audio Inspector](../nanoboy/frmAudioTool.cs), [Quick Deck](../nanoboy/frmQuickMenu.cs), [Gesundheitsmonitor](../nanoboy/Diagnostics/WindowsSessionHealthMonitor.cs) und der [lokale Windows-Einstellungskatalog](../nanoboy/WindowsSettingsCatalog.cs). Linux-[Bibliothek](../frontends/AetherBoy.Desktop/WaylandEmulatorHost.Library.cs), [Save-Werkzeuge](../frontends/AetherBoy.Desktop/WaylandEmulatorHost.SaveTools.cs), [ROM-Vorbereitung](../frontends/AetherBoy.Desktop/WaylandEmulatorHost.Loading.cs), [Controllerbedienung](../frontends/AetherBoy.Desktop/WaylandEmulatorHost.Controller.cs), [Werkzeuge](../frontends/AetherBoy.Desktop/WaylandEmulatorHost.Tools.cs), [Einstellungskatalog](../frontends/AetherBoy.Desktop/LinuxSettingsCatalog.cs), [Diagnose](../frontends/AetherBoy.Desktop/LinuxDiagnostics.cs) und [Paketierung](../scripts/package-linux.sh).

## Umsetzung: ein Projekt, zwei Oberflächen

1. **Gemeinsame Verträge zuerst:** Alles, was emuliertes Timing, Saves, Netplay-Protokoll, Prüfsummen, diagnostische Codes oder Dateiformate betrifft, gehört in Core/Runtime beziehungsweise einen kleinen gemeinsamen Dienst. Windows und Linux stellen diesen Dienst nur verschieden dar. Plattformpfade, Dateidialoge, Controller-APIs, Audio und Rendering bleiben getrennt.
2. **Keine scheinbare Parität:** Ein Suchergebnis oder Menüeintrag zählt erst, wenn die Aktion funktioniert. Die Windows-Suche erwähnt derzeit Batterie-Save-Import, obwohl nur der ROM-Import eine benachbarte `.sav` übernehmen kann.
3. **Datenintegrität vor Komfort:** Niemals Originalspielstände durch Online-Link-Tests oder Importversuche still ersetzen. Vor manuellem Restore/Import eine lesbare Sicherung samt RTC-Nebendateien erzeugen, aktives Spiel geordnet beenden, erwartete Save-Größe prüfen und das Ziel explizit bestätigen. Da eine rohe `.sav` keine verlässliche ROM-Identität enthält, ist die Größe allein **keine** Herkunftsgarantie.
4. **Gleiche Testfälle statt identischer Screenshots:** Für jede Funktion ein plattformneutrales Akzeptanzkriterium, dazu Windows- und Linux-UI-Tests. Native Ausgabe und Barrierefreiheit brauchen reale Hardware-/Desktop-Tests.

## Arbeitspakete und Reihenfolge

Die Aufwände sind grobe Entwicklungsschätzungen **ohne** lange Spieltests oder externe Serverreparatur. „Windows“ und „Linux“ bezeichnen die jeweiligen Oberflächen; gemeinsame Tests/Runtime werden von beiden gegengeprüft. Pakete erst nach einem sauberen, gemeinsam notierten Quellstand zusammenführen.

### 0 — Arbeitsstand sichern und Messbasis festlegen (beide; etwa 0,5–1 Tag)

- Die lokalen Windows-Änderungen getrennt von `7ced237` inventarisieren und die offenen UI-/Controller-/CI-Änderungen nicht überschreiben. Linux-Kollege und Windows-Entwickler halten beide exakt die getestete Commit-ID und lokale Änderungen fest.
- [CI](../.github/workflows/ci.yml), Core-/Runtime-/Windows-Smoke-/Desktop-Tests und Node-Raumdiensttests auf dem jeweiligen System ausführen. Skips, Warnungen und tatsächlich ausgeführte native Fälle getrennt notieren.
- Eine gemeinsame Funktionsliste als Abnahmecheckliste verwenden: **Runtime vorhanden → UI erreichbar → automatischer Test → lokaler realer Test → WAN-/Spieltest**. Kein Statusspringer von „gebaut“ zu „fertig“.

### 1 — Save-Portabilität und Schutz (Windows federführend, Linux Abgleich; etwa 2–4 Tage)

- Windows: [Save Safety Center](../nanoboy/frmBatterySaveManager.cs) um expliziten Import einer vom Nutzer gewählten `.sav` und Export der aktuellen persistierten Save-/State-Familie als ZIP ergänzen. Die bestehende Backup-/Restore-Schicht wiederverwenden; keine zweite Save-Implementierung in der Form bauen.
- Der Ablauf zeigt ROM-Titel/System, Zielpfad und erwartete Länge, warnt bei unbekannter Herkunft, erzeugt **vor** der Änderung ein Archiv, bestätigt gezielt, schreibt atomar und startet die betroffene Session neu. Import in ein anderes oder laufendes ROM und stille Überschreibung sind zu verweigern. Keine automatische Übernahme von Online-Sitzungskopien.
- Linux: bestehende [Save-Tools](../frontends/AetherBoy.Desktop/WaylandEmulatorHost.SaveTools.cs) gegen dieselben Grenzfälle testen: falsche Länge, korruptes Archiv, fehlender Schreibzugriff, RTC-Nebendatei, Neustart. Eine portable Manifestdatei im Export nur ergänzen, wenn beide Importe sie verstehen; vorhandene rohe `.sav` weiter akzeptieren.
- **Abnahme:** Ausgangssave bytegleich im Vorher-Archiv; falsche Datei ändert nichts; korrekte Datei ist nach echtem Emulator-Neustart lesbar; Original und eventuelle RTC-Daten bleiben wiederherstellbar.

### 2 — Verbindungsdiagnose auf Linux anbieten (Linux federführend, Windows Gegenprüfung; etwa 1–2 Tage)

- Linux: [OnlineProbeSession](../nanoboy/Runtime/Netplay/OnlineProbeSession.cs) über eine eigene Seite unter Online Link anbieten, mit Raum erstellen/beitreten, kurzem Raumcode, Start/Abbruch, beidseitigem Ergebnis und bereinigtem Diagnoseexport. **Kein ROM** voraussetzen. Die bestehende [Linux-Raumseite](../frontends/AetherBoy.Desktop/WaylandEmulatorHost.OnlineRooms.cs) und dieselben privaten Einstellungen wiederverwenden.
- Windows: vorhandenen [Probe-Dialog](../nanoboy/frmOnlineConnectionTest.cs) gegen genau dieselben Probe-Größen, Zustände, Abbruch-/Timeout- und Exportfälle testen. In beiden UIs bleibt „DataChannel geöffnet“ klar getrennt von „ROM-Tausch erfolgreich“.
- **Abnahme:** 32/256/1024/4096-Byte-Echos mit Sequenz und Integritätsprüfung auf beiden Seiten; Abbruch/Fehlschlag ohne Hänger; Berichte enthalten keine Zugangsdaten, Roh-SDPs oder öffentlichen Trainer-/Save-Daten.

### 3 — Lokalen Zwei-Spiel-Link unter Linux nutzbar machen (Linux federführend; etwa 4–8 Tage)

- Linux: eine Zwei-Spiel-Seite mit ROM A/B, separaten Spielstandpfaden und zwei Viewports bauen. Die gemeinsame [LocalLinkSession](../nanoboy/Runtime/LocalLinkSession.cs) und deren Besitzer-/Timingregeln verwenden; [Windows Local Link Lab](../nanoboy/frmLocalLinkLab.cs) ist der Verhaltensvergleich, nicht ein Code-Port für SDL.
- Zwei unabhängige Eingaben zuordnen (zwei Controller oder Controller plus Tastatur), Fokus/Pause, Audio-Mix, Vollbild, Save-/Load-Sperren während kritischer Kabelvorgänge und sauberen Session-Abbruch definieren. Pro ROM exklusive Schreib-Lease und verständliche Warnung bei derselben Save-Familie.
- Erst GB/GBC-Byte-Transfer und UI-Lebenszyklus prüfen, dann GBA-Gen3-Kommandos/IRQ separat. Windows führt dieselben Regressionen nach Änderungen an der Runtime erneut aus.
- **Abnahme:** beide Spiele starten, beide reagieren getrennt, ein lokaler Link-Test beendet sich geordnet, Spielstände liegen getrennt, Abbruch lässt keine gesperrten Saves zurück. Pokémon-Tausch erst nach beobachtetem beidseitigem Kaltstart als bestanden markieren.

### 4 — Bedienwerkzeuge angleichen (parallel pro Plattform; etwa 4–7 Tage)

- Linux: Audio-Inspector mit vorhandenen PSG-/GBA-Snapshots, Quick Deck und Controller-taugliche Bildschirmtastatur ergänzen. Raumcode, Suche und ROM-Auswahl müssen ohne physische Tastatur erreichbar bleiben. In der Bibliothek Sortierung, optional Kachel-/Listenansicht und Spielbild-Vorschau aus vorhandenen State-Previews ergänzen; fehlende Bilder klar kennzeichnen.
- Windows: eigenen Bibliothekstitel editierbar machen; Controller-Richtungsbelegung analog zum Linux-Profil prüfen und bei gleichbleibender Input-Sicherheit ergänzen. Die lokalen neuen Einstellungsseiten erst nach Test und Zustimmung als gemeinsamen Stand zählen.
- Beide: Profil-/Theme-Wechsel, Textgröße, Controller-Hotplug und schmale Fenster mit realen Eingabegeräten prüfen. Windows-GPU/VSync und Linux-Wayland/Hyprland bleiben absichtlich plattformspezifisch.
- **Abnahme:** gleiche Nutzeraufgaben in höchstens wenigen nachvollziehbaren Aktionen; keine abgeschnittenen Sicherheitsmeldungen; Controller-only-Pfad bis zum Online-Raumcode.

### 5 — Diagnose vereinheitlichen (beide; etwa 2–4 Tage)

- Gemeinsame, sensible Daten aussparende Ereignisdefinitionen für ROM-Start, Ladefehler, Save-Operation, Online-Stufe und Session-Health festlegen. Die bestehenden [Windows-Hints](../nanoboy/Diagnostics/WindowsSessionHealthMonitor.cs) auf portable Daten statt WinForms-Abhängigkeiten prüfen und die entsprechende Linux-Markierung/Anzeige ergänzen. Nicht alle Windows-/Linux-JSON-Dateien zwangsweise bytegleich machen; ein gemeinsames Auswertungsschema genügt.
- Native Fehlerkette, ICE/Relay, Datenkanal und Probe-/Spielprotokoll im Bericht getrennt halten. Roh-Exception-Meldungen vor UI/Export auf Pfade, Serverdaten und Tokens prüfen. Eine technische Diagnose darf Fehlerquellen nicht aus einem Timeout allein behaupten.
- **Abnahme:** auf beiden Plattformen Problem markieren, ZIP ohne ROM/Save/Schlüssel exportieren, gleichartige Störungen mit denselben stabilen Codes auswerten.

### 6 — Windows-Ladevorgang und Distribution härten (Windows federführend; etwa 3–5 Tage)

- Den synchronen [ROM-Ladepfad](../nanoboy/frmNano.cs) in Vorbereitung und atomare UI-Übergabe zerlegen: Hash/Kopie/Firmware-/Profil-Lesezugriffe auf begrenztem Worker, sichtbarer Fortschritt und Abbruch, veraltete Ergebnisse verwerfen. Die laufende Session bleibt bis zum erfolgreichen vorbereiteten Austausch erhalten. Save-Lease und Beenden in definierter Reihenfolge.
- Settings-/Profil-Schreiben mit unveränderlichen Snapshots außerhalb der UI prüfen; bei Beenden oder ROM-Wechsel einen erfolgreichen Flush verlangen. Bestehende atomare Dateischreibweise behalten.
- Reproduzierbares Windows-x64-Paket mit Native-Abhängigkeiten, Buildkennung, Checksums und Start-Smoke erstellen, ähnlich der Linux-[Paketierung](../scripts/package-linux.sh); Linux ARM64 muss zusätzlich auf echter ARM64-Hardware starten, nicht nur cross-publishen.
- **Abnahme:** Abbruch startet keine andere ROM; Fehler lässt das alte Spiel spielbar; kein Save-Verlust bei schnellem ROM-Wechsel; frisches Paket startet ohne Entwicklungs-SDK.

### 7 — Verbindliche Endabnahme (beide Entwickler und Spieler; Aufwand abhängig von Netzwerk/Spielen)

- Drei getrennte lokale Linien: GB, GBC, GBA. Zuerst automatisierte Core-/Runtime-/Frontend-Tests, dann Desktop-Start auf Windows und Wayland/Hyprland mit echter Audio- und Controller-Hardware.
- Danach die sechs Rollenpaare aus der [Link-Abnahmematrix](LINK_PLATFORM_VALIDATION_2026-09-29.md): Windows→Windows und zurück, Linux→Linux und zurück, Windows→Linux und zurück. **Jede** Zeile erst ROM-frei über echten WAN-/TURN-Pfad, dann mit geschützten Sitzungskopien und Spiel. Linux x64/ARM64 separat kennzeichnen.
- Für Pokémon-Tausche Edition/Revision, Freischaltungen, Verbindung, Tausch, Rückkehr ins Spiel, reguläres Speichern und **beidseitigen Kaltstart** dokumentieren. GB/GBC und GBA getrennt bewerten; ROM-Hacks sind zusätzliche, nicht automatisch abgedeckte Profile.
- Externe Tester erhalten ein Paket plus kurze Spielanleitung und einen einfachen „Problem markieren / Bericht öffnen“-Weg; keine Git-, Versions- oder Terminalkenntnisse voraussetzen.

## Priorität und Zusammenführung

**Zuerst** Save-Sicherheit auf Windows und Probe-Oberfläche auf Linux; beides ermöglicht ungefährliche Tests. **Danach** Linux Local Link und die fehlenden Bedienwerkzeuge. Parallel dürfen Windows-Bibliothek/Controller und Diagnosevertrag entstehen, solange gemeinsame Runtime-Dateien nicht unabhängig auseinanderlaufen. Windows-Ladehärtung folgt vor einer breiteren Tester-Verteilung. Ein reines WAN- oder lokales Transport-PASS ersetzt keine Spielabnahme.

Ein Feature gilt erst als „auf gleichem Stand“, wenn es auf **beiden** Systemen sichtbar und bedienbar ist, seine Fehlerfälle Tests haben, die Datenintegrität erhalten bleibt und der reale Plattformtest protokolliert ist. Die bereits offenen, echten WAN-/Tauschnachweise stehen weiterhin in der [separaten Abnahmematrix](LINK_PLATFORM_VALIDATION_2026-09-29.md).
