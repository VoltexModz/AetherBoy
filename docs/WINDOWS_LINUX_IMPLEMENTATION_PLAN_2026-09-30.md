# Umsetzungsplan für Windows und Linux

Dieser Plan übersetzt den [Funktionsabgleich](PLATFORM_PARITY_CONCEPT_2026-09-30.md) in getrennte Arbeitsfolgen für VoltexModz auf Windows und den Linux-Mitentwickler. Beide entwickeln **ein** AetherBoy mit gemeinsamem Core und gemeinsamer Runtime. Die Schritte beschreiben geplante Arbeit, nicht bereits erledigte Funktionen. Insbesondere ist ein erfolgreicher Pokémon-Tausch über das Internet bislang nicht nachgewiesen.

Ausgangsbasis ist `development` bei `7ced237` am 30. September 2026. Die auf diesem Windows-Arbeitsplatz vorhandenen Änderungen an Einstellungen, Controllerseiten, UI und CI sind noch uncommittet und gehören nicht automatisch zum gemeinsamen Stand. Vor einem Arbeitspaket prüfen beide Entwickler erneut `HEAD`, `origin/development` und den eigenen Arbeitsbaum. Dieser Plan autorisiert weder einen Commit noch einen Push.

## Windows-Arbeitsstand vom 30. September (noch nicht veröffentlicht)

Die folgenden Punkte sind **lokal implementiert**, aber erst mit echter Bedienung und einem abgestimmten Commit vollständig abgenommen:

- W1–W2: getrennte Save-Aktionen für rohe `.sav`, rotierende Backups und ein prüfbares ZIP-Vorher-/Exportarchiv. Die automatische und die manuelle Importstrecke müssen noch mit fremden echten Spielständen und Fehlerszenarien auf Windows bedient werden.
- W3: vier frei belegbare Controller-Richtungen pro Geräteprofil und ein editierbarer Bibliothekstitel als Metadatum; ROM-Hash und Save-Pfad bleiben unverändert. Synthetische Persistenztests bestehen, ein physischer Controller bleibt abzunehmen.
- W3a: sechs gemeinsame Presets einschließlich Aether Original sowie dezenter Akzent auf den Flächen. Core-Kontrast- und Windows-Theme-Tests bestehen; die Linux-Auswahl ist unter L5 und in [LINUX_UI_PARITY.md](LINUX_UI_PARITY.md#theme-parity-still-to-implement-on-linux) nachzutragen.
- W4: Vorbereitung eines ROM-Imports läuft für Menü, Drag-and-drop und Startargument im Hintergrund mit Abbruch-/Generationsprüfung; die alte Sitzung bleibt in dieser Phase aktiv. Der eigentliche Sitzungswechsel bleibt auf dem UI-Thread. Fehler-, Abbruch- und Profilwechseltests brauchen noch zusätzliche Abdeckung auf echter Hardware.
- W5: lokal umgesetzt. Globale Einstellungen, Spiel- und Controllerprofile gehen als unveränderliche Generationen in geordnete Hintergrundschreiber; ein ausstehendes Spielprofil bleibt beim Wechsel im Speicher lesbar. Vor ROM-Wechsel und Beenden wird der letzte Stand geschrieben. Bei Schreibfehlern bleibt die Änderung zum erneuten Speichern vorgemerkt und Windows zeigt einen Hinweis. Ein manueller Test mit absichtlich gesperrtem oder vollem Datenträger steht noch aus.
- W6: gemeinsame Runtime- und Windows-Smoke-Regressionen sowie vier echte lokale native Transportfälle bestanden. Das bestätigt weder einen Zwei-Rechner-WAN-Kanal noch einen Pokémon-Tausch.
- W7: `scripts/package-windows.ps1` erzeugt ein selbstenthaltendes Windows-x64-Verzeichnis und ZIP mit Buildkennung, nativer Bibliothek, Lizenzen und SHA-256-Liste. Der `win-x64`-Restore ergänzt die vier NuGet-Lockdateien um RID-Einträge; diese gehören zum reproduzierbaren Paketstand. Start, Controller, Audio, DPI und Screenreader auf einem frischen echten Windows-PC sind noch offen.

Prüfstand: Release-Solution-Build ohne Warnungen oder Fehler; Core 245/245, Runtime 442 bestanden und 5 umgebungsbedingt übersprungen, Windows-Smoke nach W5 249 bestanden und 4 Hardware-/Vordergrundfälle übersprungen, Desktop-Logik 91 bestanden und 42 native Linux-Fälle übersprungen. Die Core-/Runtime-/Desktop-Zahlen stammen vom vorherigen Stand; nach W5 wurde nur die gesamte Lösung neu gebaut. Die vier nativen Windows-Loopback-Fälle wurden separat aktiviert und bestanden. Der Quellbaum enthält weiterhin fremde uncommittete Arbeit; diese Zahlen sind **keine** Paket- oder Linux-Freigabe.

## Regeln für beide Arbeitsfolgen

- Core, Save-Codec, Kabel-Timing und Online-Protokoll bleiben gemeinsam. Plattformcode implementiert Dateidialoge, Oberfläche, Audio-/Video-Ausgabe und Controlleradapter. Ein Linux- oder Windows-Feature darf keinen zweiten Emulator-Core erzeugen.
- Original-ROMs, Original-Saves, RTC-Nebendateien und Online-Sitzungsspielstandkopien bleiben getrennt. Keine Testfunktion schreibt still in einen anderen Spielstand. Eine rohe `.sav` enthält keine sichere ROM-Kennung; gleiche Dateigröße beweist nicht, dass sie zum Spiel gehört.
- Jede sichtbare Textänderung folgt dem [UI-Copy-Leitfaden](UI_COPY_GUIDE.md). Keine Fehlermeldung darf Schlüssel, rohe SDP-Daten oder Save-Inhalte in Diagnose-ZIPs aufnehmen.
- Ein Schritt ist erst abgeschlossen, wenn der Code gebaut, seine Erfolgs- und Fehlerfälle getestet und die betroffene Oberfläche auf dem Zielsystem bedient wurde. Automatisierte Tests ersetzen keinen echten Controller-, Audio-, Wayland- oder WAN-Test.
- Der jeweils andere Entwickler übernimmt einen gemeinsamen Runtime-Commit erst nach Test seiner eigenen Plattform. Bei überlappenden Dateien zuerst die Änderung besprechen und auf denselben Quellstand bringen; keine parallelen, voneinander abweichenden Runtime-Varianten pflegen.

## Windows Schritt für Schritt

### W0 Lokalen Windows-Arbeitsstand sauber abnehmen

1. Die vorhandenen uncommitteten Dateien inventarisieren, ohne sie zu verwerfen. Insbesondere [Einstellungskatalog](../nanoboy/WindowsSettingsCatalog.cs), [Control-Center-Navigation](../nanoboy/frmControlCenter.Navigation.cs), [Controllerprofil](../nanoboy/NanoboySettings.Controller.cs) und die geänderten Tests bilden ein zusammenhängendes Arbeitspaket.
2. Windows-Release-Build, Core-, Runtime- und Smoke-Tests mit dem aktuellen SDK ausführen. Testzahl, Skips und Änderungen gegenüber `7ced237` notieren; die bestehenden [CI-Gates](../.github/workflows/ci.yml) nicht wegen einzelner Fehler senken.
3. Die neuen Unterseiten mit Maus, Tastatur und Controller öffnen; Einstellungen suchen, speichern, App neu starten und Persistenz prüfen. Erst danach darf die Windows-Seite der Paritätsliste als gemeinsamer Stand gelten.

**Fertig, wenn:** Suche und Controllerseiten nicht nur erscheinen, sondern ihre Einstellungen dauerhaft und ohne abgeschnittene Hinweise wirken. Keine fremden lokalen Änderungen wurden überschrieben.

### W1 Save-Import und Export fachlich festlegen

1. Den heutigen Ablauf nachvollziehen: [Save Safety Center](../nanoboy/frmBatterySaveManager.cs) wählt nur rotierende Backups; [frmNano](../nanoboy/frmNano.cs) beendet für Restore die Session, erwirbt eine Schreib-Lease, ruft `BatterySaveStore.Restore` auf und lädt die ROM neu. [WindowsRomLibrary](../nanoboy/Storage/WindowsRomLibrary.cs) übernimmt eine benachbarte `.sav` nur beim ROM-Import, nicht auf Knopfdruck.
2. Importumfang auf **rohe `.sav` für die aktuell ausgewählte ROM** begrenzen. ZIP ist zunächst Export-/Rettungsformat, kein unkontrollierter ZIP-Importer. Titel, ROM-Hash, System, erwartete Save-Länge und Dateigröße anzeigen; bei unbekannter Herkunft warnen und ausdrücklich bestätigen lassen.
3. Für den ZIP-Export alle persistierten Dateien der betreffenden Save-/State-Familie einschließlich Backups, Guard- und RTC-Dateien erfassen. Relative Namen und ein optionales Manifest mit Formatversion/ROM-Hash festlegen; ältere ZIPs bleiben weiterhin als Archiv lesbar, auch wenn ein Manifest fehlt. Kein absolutes AppData-Verzeichnis und keine Geheimnisse ins Archiv aufnehmen.
4. Vor einem Import aktuelle im Speicher befindliche Änderungen regulär persistieren und ein **nachprüfbar lesbares Vorher-Archiv** erzeugen. Schlägt dies fehl, findet kein Import statt. Danach die bestehende Restore-/Lease-/Neustartstrecke verwenden; bei Fehler darf weder eine halbe Datei noch ein fremdes Save aktiv werden.

**Fertig, wenn:** Import und Export fachlich beschrieben sind und die vorhandene Save-Sicherheitslogik wiederverwendet wird, ohne das Online-Copy-Verfahren zu ändern.

### W2 Save-Werkzeuge in Windows implementieren und absichern

1. Im [Save Safety Center](../nanoboy/frmBatterySaveManager.cs) klar getrennte Aktionen für „Backup wiederherstellen“, „`.sav` importieren“ und „Archiv exportieren“ ergänzen. Der Aufruf aus [frmNano](../nanoboy/frmNano.cs) bleibt verantwortlich für Stoppen, Lease und erneuten ROM-Start; die Form schreibt nie selbst in einen laufenden Emulator.
2. Die Dateiauswahl und das Zielverzeichnis über die bestehenden Windows-Datenpfade führen. Vor dem Export den Stand als „persistiert bis zum letzten regulären Save“ kennzeichnen, falls keine sichere Flush-Schnittstelle verfügbar ist. Keine falsche Zusage über noch im RAM befindliche Daten geben.
3. Tests ergänzen: richtige/falsche Länge, nicht lesbare Quelle, fehlende Schreibrechte, Abbruch vor Bestätigung, Vorher-Archiv nicht erzeugbar, RTC-Dateien, unterbrochener Import, wiederholter Import, echter Neustart und abweichende ROM-Identität. Hier gehören neue Windows-Smoke-Tests und portable Tests für eventuelle neue Save-Dienste hin.
4. Den derzeit irreführenden Import-Treffer im [Windows-Einstellungskatalog](../nanoboy/WindowsSettingsCatalog.cs) erst nach funktionierendem Import als erfüllt zählen.

**Fertig, wenn:** Eine vorhandene mGBA-`.sav` bewusst übernommen werden kann, der alte Stand rekonstruierbar bleibt und ein fehlerhafter Import **keine** Save-Datei verändert. Ein Spielstart allein beweist noch nicht die fachliche Richtigkeit der fremden `.sav`.

### W3 Steuerkreuz und Bibliothek ergänzen

1. Im [GamepadMapper](../nanoboy/Input/GamepadMapper.cs) sind A/B/Start/Select belegbar, die vier D-Pad-Richtungen aber fest verdrahtet. Profilformat und Migration so erweitern, dass Up/Down/Left/Right ohne Verlust vorhandener Controllerprofile belegbar werden. Der linke Stick bleibt ein zusätzlicher Richtungseingang mit eigener Totzone.
2. Doppelbelegungen, Gegenrichtungen und fehlende Controller beim Start definieren. Defaults müssen unverändert funktionieren; Zurücksetzen stellt alle vier Richtungen wieder her. UI und Tests der bereits lokal geänderten [Controllerseiten](../nanoboy/frmControls.Sections.cs) nach W0 anpassen.
3. In der [Windows-Bibliothek](../nanoboy/frmRomLibrary.cs) Titelbearbeitung ergänzen. Der Anzeigename gehört in die Metadaten, **nicht** in Dateinamen, ROM-Hash oder Save-Pfad. Leeren Titel ablehnen, Defaulttitel wiederherstellbar machen, Suche und Sortierung auf den neuen Titel anwenden.

**Fertig, wenn:** zwei verschiedene Controllerprofile nach Neustart korrekt zugeordnet sind und eine Titeländerung die ROM-/Save-Identität nicht verändert.

### W3a Farbdesigns unter Windows wiederherstellen

1. Das ursprüngliche Violett/Cyan als **Aether Original** erhalten und neben fünf deutlich unterscheidbaren Designs anbieten. Die Palette ist Teil des gemeinsamen Core; Windows zeigt Namen und Farbvorschau an und speichert die drei UI-Farben global, unabhängig von der Game-Boy-Spielpalette.
2. Benutzerdefinierte Farben bleiben möglich. Sobald eine Farbe frei verändert wird, ist kein Preset mehr ausgewählt; „Originaldesign wiederherstellen“ setzt exakt die drei ursprünglichen Werte zurück. Flächen sollen die Akzentfarben dezent aufnehmen statt neutral grau wirken, ohne Textkontrast zu verlieren.
3. Auswahl, Neustart, Reset, helle/dunkle Kontraste sowie bereits geöffnete Fenster testen. Die Windows-Umsetzung ist bis zur vollständigen Abnahme lokaler Arbeitsstand, kein veröffentlichter Linux-Funktionsnachweis.

**Fertig, wenn:** sechs Presets dauerhaft auswählbar sind, das alte Aether-Design wieder erkennbar ist und kein Dialog nach einem Wechsel in grauen oder unlesbaren Farben zurückbleibt.

### W4 ROM-Laden responsiv und abbrechbar machen

1. [LoadRomFile](../nanoboy/frmNano.cs) in Vorbereitung und Übergabe zerlegen. Vorbereitung darf Hashen/Kopieren, Headerprüfung und andere längere Lesevorgänge auf einem begrenzten Worker ausführen. Ein UI-Generationstoken verwirft verspätete Ergebnisse nach zweiter Dateiwahl oder Abbruch.
2. Die laufende Session während der **Vorbereitung** beibehalten. Erst nach erfolgreicher Vorbereitung auf dem UI-Thread den alten Emulationsbesitzer geordnet beenden, dessen Save-Lease freigeben und die neue Session starten. Derselbe ROM-Hash und laufende Save-Operationen brauchen eigene Konfliktprüfungen.
3. Status, Abbruch und Fehler so anzeigen, dass der Nutzer weiß, ob die alte Session weiterläuft oder beendet wurde. Kann der eigentliche Start nach Übergabe scheitern, alten Zustand kontrolliert neu öffnen oder einen klaren Wiederherstellungsweg zeigen; kein stiller schwarzer Bildschirm.
4. Tests für ungültige/große ROM, Abbruch während Kopie, zwei rasche Auswahlen, Stop-Timeout, Firmware-/Profilfehler, Fenster schließen und Save-Lease-Freigabe ergänzen. Der Linux-[Ladepfad](../frontends/AetherBoy.Desktop/WaylandEmulatorHost.Loading.cs) ist Verhaltensreferenz, kein WinForms-Code zum Kopieren.

**Fertig, wenn:** Vorbereitung/Abbruch die Oberfläche nicht blockieren und das alte Spiel bei Vorbereitungsfehlern spielbar bleibt; Save und Profil bleiben konsistent.

### W5 Einstellungs- und Profiländerungen ohne UI-Blockade speichern

1. Die synchronen Aufrufer des [WindowsSettingsProvider](../nanoboy/Storage/WindowsSettingsProvider.cs) und der Profilverwaltung erfassen. Zuerst messen, welche Aktionen spürbar blockieren; dann nur diese in eine geordnete Hintergrundschreibfolge verschieben.
2. Immer einen unveränderlichen Snapshot speichern, eine monoton steigende Generation vergeben und ältere abgeschlossene Writes nicht als aktuellen Stand melden. Die bestehende atomare Datei-Ersetzung behalten.
3. Beim ROM-/Profilwechsel und beim Beenden die letzte Generation zuverlässig flushen; Fehlermeldung und Wiederholungsmöglichkeit anbieten, statt Änderungen still zu verlieren. [Linux-Settings](../frontends/AetherBoy.Desktop/WaylandEmulatorHost.Settings.cs) zeigen eine mögliche Zustandsmaschine, die Windows-Implementierung bleibt plattformgerecht.

**Fertig, wenn:** schneller Wechsel A→B→C nach Neustart C erhält, ein Schreibfehler sichtbar ist und ein offener Write den Profilwechsel nicht verfälscht.

### W6 Diagnose, Online-Probe und Local Link auf Windows nachziehen

1. Vorhandene [Online-Probe](../nanoboy/frmOnlineConnectionTest.cs), [Local Link Lab](../nanoboy/frmLocalLinkLab.cs) und [Gesundheitsmonitor](../nanoboy/Diagnostics/WindowsSessionHealthMonitor.cs) behalten. Hier ist nicht ein Neubau, sondern eine Regression gegen die neuen gemeinsamen Verträge und gegen Linux nötig.
2. Probe lokal ohne ROM, mit zwei Endpunkten und echten Abbruch-/Timeoutfällen testen. Transport-PASS, Spiel-Handshake und Tausch bleiben getrennte Statusmeldungen. Datenschutzprüfung für Diagnose-ZIP, markiertes Problem, ICE-Stufe und Native-Fehler durchführen.
3. Local Link mit zwei verschiedenen und zweimal derselben ROM testen: getrennte Saves, Controllerzuordnung, Pause, Audio und geordnetes Schließen. In `LocalLinkSession` existieren **keine** unabhängigen Savestates oder Rewind; die Windows-Oberfläche darf diese für die Zwei-Spiel-Sitzung nicht versprechen.

**Fertig, wenn:** die bereits vorhandenen Windows-Funktionen nach Linux-/Runtime-Änderungen unverändert bedienbar und ihre Berichte vergleichbar sind. Reale Tausche sind ein eigener Abnahmeschritt.

### W7 Windows-Paket und reale Bedienprüfung

1. Ein reproduzierbares Windows-x64-Paket aus [nanoboy.csproj](../nanoboy/nanoboy.csproj) bauen: App, Managed-/Native-Abhängigkeiten, Lizenzhinweise, Buildkennung, SHA-256-Prüfsummen und kurze Startanleitung. Ein frischer PC darf kein Entwickler-SDK benötigen. Das Paket-Skript darf keine Benutzer-AppData ersetzen oder löschen.
2. Windows-Start, Import/Export, Controller-Hotplug, Turbo-Audio, Fenstergröße/DPI und Screenreader-Bedienbarkeit auf echter Hardware prüfen. Fehlende Screenreader-Abnahme offen kennzeichnen, nicht aus WinForms-Controls ableiten.
3. Windows-Smoke- und gemeinsame Runtime-Tests wiederholen; das Testpaket erst danach an nichttechnische Spieler geben, mit einfacher Anweisung zum Markieren und Öffnen eines Problemberichts.

**Fertig, wenn:** ein aus dem frischen Paket gestarteter Build seine Versions-/Buildkennung zeigt, Spielstände am vorgesehenen Ort anlegt und keine Entwicklungsumgebung braucht.

## Linux Schritt für Schritt

### L0 Linux-Basis und Zielumgebungen prüfen

1. Auf denselben abgestimmten Commit wie Windows wechseln, ohne fremde Änderungen zu überschreiben. Release-Build, Core-/Runtime- und Desktop-Tests ausführen; native Tests nur als bestanden zählen, wenn sie **nicht** übersprungen wurden.
2. Mindestens Wayland unter GNOME/KDE und Hyprland als reale Bedienpfade vorsehen. [Testskript mit isoliertem Weston](../scripts/test-linux-headless.sh) für reproduzierbare UI-Prüfungen nutzen; es ersetzt keine reale Audio-/Controller-/Portalprüfung.
3. XDG-Datenpfade, Desktop-Launcher, Paketarchitektur und optionale GTK/AT-SPI-Bibliotheken notieren. Ein x64-Test ist kein ARM64-Nachweis.

**Fertig, wenn:** der aktuelle Linux-Stand frisch baut und seine tatsächlich ausgeführten Tests/Skips dokumentiert sind.

### L1 ROM-freien Verbindungstest in Online Link anbieten

1. Neben der heutigen [Raumseite](../frontends/AetherBoy.Desktop/WaylandEmulatorHost.OnlineRooms.cs) einen klaren Einstieg „Verbindung ohne Spiel testen“ vorsehen. [OnlineProbeSession](../nanoboy/Runtime/Netplay/OnlineProbeSession.cs) ist bereits die gemeinsame zuständige Runtime; keine zweite Netzwerkimplementierung schreiben.
2. Vorhandene private Serverkonfiguration wiederverwenden. Host sieht den kurzen Raumcode; Gast gibt ihn ein. Start, Fortschritt, beidseitiger PASS/FAIL, Abbruch und Berichtspfad sind sichtbar. `Snapshot` im UI-Loop lesen, `StopAsync`/Cleanup nicht blockierend und ohne weiterlebende Session nach Fensterwechsel ausführen.
3. Raumprofile strikt trennen: `transport-probe-v1` für ROM-freie Messung, Spielprofil nur bei explizitem Spielstart mit Save-Kopie. Statusfelder dürfen keinen tatsächlichen Tausch suggerieren.
4. Desktop-Tests mit injizierter Probe/Fake-Zuständen ergänzen; echte Native-Loopback-Tests und danach WAN auf zwei Rechnern separat laufen lassen. 32, 256, 1024 und 4096 Byte sowie Sequenz/Integrität, Timeout, falscher Raumcode, fehlender Server und Abbruch prüfen.

**Fertig, wenn:** zwei Emulatoren **ohne ROM** über die Linux-Oberfläche einen Raum teilen und beide Berichte den passenden Status zeigen. Ein lokaler PASS ist noch kein WAN-PASS.

**Stand 30.09.2026:** Einstieg, Host-/Gast-Ablauf, Phasen, Abbruch, lokaler Bericht und ROM-freie Runtime-Anbindung sind im Linux-Frontend implementiert. Der Desktop-Build und die plattformunabhängigen Desktop-Tests laufen durch. Der native Wayland-Test sowie der Test auf zwei Rechnern stehen aus; L1 ist daher noch nicht als Ende-zu-Ende bestätigt.

### L2 Linux-Speicherplan für zwei lokale Spieler bauen

1. Vor dem UI-Bau einen Linux-spezifischen Planer analog zu [WindowsLocalLinkStorage](../nanoboy/Storage/WindowsLocalLinkStorage.cs) definieren. Beide ROMs erst vollständig validieren: Endung, Größe, Header/Mapper, Inhalts-Hash, gleiche Link-Familie. GB und GBC dürfen zusammen geplant werden; ein GB/GBC- und ein GBA-Spiel nicht.
2. Zwei unterschiedliche ROMs nutzen ihre eigenen hashbasierten Save-Familien. Zweimal dieselbe ROM braucht für Spieler 2 einen **separaten** Pfad wie `saves/<hash>/LinkPlayer2/game.sav`; niemals Save A kopieren oder B still mit benachbarter `.sav` befüllen. Bestehenden zweiten Spielstand bei späteren Sitzungen wiederverwenden.
3. Den Einzelspiel-Sessionbesitzer vor dem lokalen Link geordnet beenden; die [LocalLinkSession](../nanoboy/Runtime/LocalLinkSession.cs) hält danach die Schreib-Leases. Die normale [LinuxRomStorage](../frontends/AetherBoy.Desktop/LinuxRomStorage.cs) nicht zweimal für denselben Hash öffnen: deren Besitzmodell ist für ein Einzelspiel ausgelegt.
4. Fehlerfälle testen: gleiche ROM, zwei Editionen, gemischte Systeme, beschädigte ROM, unlesbarer Save, noch laufende Session, gesperrtes Ziel, Startabbruch. Vor der vollständigen Prüfung beider Quellen keine sichtbare Save-Migration durchführen.

**Fertig, wenn:** ein Plan immer zwei unterschiedliche Save-Pfade liefert, fehlerhafte Auswahl keine Save-Dateien verändert und normale Einzelspiel-/Link-Sitzungen nie gleichzeitig dieselbe Familie schreiben.

### L3 Zwei-Spiel-Oberfläche und Eingabesteuerung implementieren

1. Eine lokale Link-Seite mit ROM A/B, getrennten Bildflächen, Spielernamen/Controllerzuordnung, Status und sicherem „Beenden“ in [WaylandEmulatorHost](../frontends/AetherBoy.Desktop/WaylandEmulatorHost.cs) ergänzen. Das [Windows Local Link Lab](../nanoboy/frmLocalLinkLab.cs) ist die Bedienreferenz; SDL/Wayland-Ausgabe bleibt Linux-eigen.
2. Genau einen `LocalLinkSession`-Besitzer erzeugen. Pro Spieler die aktuellen Frames/Snapshots abrufen und in getrennte Viewports zeichnen. Beim Systemwechsel die korrekte GB/GBC- bzw. GBA-Geometrie nutzen; veraltete Frames nach Abbruch nicht mehr rendern.
3. Zwei Controller oder Controller plus Tastatur zuordnen. Eingaben über die asynchronen `SetButtonsAsync`/`SetGameBoyAdvanceButtonsAsync`-Kommandos der Session senden, niemals Emulatorregister direkt aus SDL-Events beschreiben. Hotplug, Fokusverlust, Pause und Abbruch ohne festhängende Richtung prüfen.
4. Zwei Audioquellen begrenzt mischen und über ein SDL-Gerät ausgeben; Pegel begrenzen, Queue nicht unendlich wachsen lassen, beim Pausieren/Schließen zurücksetzen. **Quicksave, Quickload, Rewind und unabhängige Zeitlinien in Local Link deaktivieren**, weil der heutige gemeinsame `LocalLinkSession`-Vertrag sie nicht anbietet.
5. Erst GB/GBC lokal mit synthetischen Link-Fällen, danach GBA-Gen3/IRQ getrennt testen. Spieltests mit echten ROMs nur auf nutzereigenen Dateien und getrennten Spielstandkopien durchführen.

**Fertig, wenn:** beide Ansichten gleichzeitig reagieren, Eingaben nicht vertauscht sind, Save-Familien getrennt bleiben und Schließen/Fehler alle Besitzer, Audioqueues und Locks freigibt. Ein sichtbarer Kabelstatus ist noch kein bestätigter Pokémon-Tausch.

### L4 Quick Deck und controllerfähige Texteingabe ergänzen

1. Ein kompaktes In-Game-Menü für Pause, Slotwahl, Bildoptionen und Rückkehr bauen. Der rechte Stick-Klick öffnet heute das Control Center; dessen Weg darf nicht kommentarlos verschwinden. Quick Deck deshalb zunächst als leicht erreichbare Unteransicht/zusätzliche Aktion vorsehen und Controller-only-Navigation messen.
2. Für Textfelder wie Suche, Titel und Raumcode eine Bildschirmtastatur ergänzen, die mit [LinuxTextEditor](../frontends/AetherBoy.Desktop/LinuxTextEditor.cs) und dem vorhandenen Fokusmodell zusammenarbeitet. Buchstaben, Ziffern, Löschen, Bestätigen und Abbrechen müssen ohne physische Tastatur gehen. Zugangsschlüssel nie im Klartext als Tastaturvorschlag oder Diagnoseereignis speichern.
3. Aus Single-Player- und Local-Link-Kontext jeweils nur erlaubte Aktionen zeigen. Insbesondere darf das Quick Deck im lokalen Link keine nicht unterstützten Save-State-Funktionen anbieten.

**Fertig, wenn:** ein Nutzer mit nur einem Controller die Bibliothek bedient, einen Raumcode eingibt und zum Spiel zurückkehrt, ohne die normale Tastatur-/IME-Eingabe zu beschädigen.

### L5 Audio-Inspector und Bibliothek aufholen

1. Einen Linux-Inspector auf Basis der gemeinsamen Audio-Snapshots aufbauen. Die [Windows-Ansicht](../nanoboy/frmAudioTool.cs) zeigt PSG-Kanäle und für GBA Direct Sound/FIFO; Linux soll dieselben **Informationen** lesbar machen, ohne Register oder Sound-Kern duplizieren zu müssen. Werte aktualisieren, aber Rendering und Audio nicht durch übermäßiges Polling ausbremsen.
2. In der [Linux-Bibliothek](../frontends/AetherBoy.Desktop/WaylandEmulatorHost.Library.cs) Sortieroptionen für zuletzt gespielt, Titel und Spielzeit ergänzen. Kachel-/Listenwechsel und Vorschaubild aus bestehenden State-Previews nach Cache-/Dateigrößenregeln implementieren; fehlende Vorschau als Platzhalter zeigen.
3. Favoriten, Suche, eigene Titel und „Open / Relocate“ nach Sortier-/Layoutwechsel erhalten. Vorschau- und Titelarbeit darf die ROM-Identität oder XDG-Save-Pfade nicht verändern.
4. **Farbdesigns nachziehen:** dieselben sechs Preset-IDs und RGB-Werte aus der gemeinsamen `UiThemePresets`-Definition anbieten, insbesondere **Aether Original**. Die Linux-Oberfläche darf die Namen ins Englische übertragen, aber Preset-Wechsel, eigener Farbwähler und Reset müssen dieselben Ergebnisse wie Windows liefern. Beim Laden bestehender Linux-Einstellungen die gespeicherten drei Farben erhalten; keine stillschweigende Überschreibung durch ein Preset.

**Fertig, wenn:** Linux-Nutzer dieselben Bibliotheksaufgaben, Audioinformationen und sechs Farbdesigns erreichen, auch wenn das Wayland-Layout anders aussieht als WinForms. Der Wechsel ist auf Wayland und Hyprland bei hellen und dunklen Designs lesbar.

### L6 Diagnose und Save-Sicherheit angleichen

1. Die vorhandenen [Linux-Berichte](../frontends/AetherBoy.Desktop/LinuxDiagnostics.cs) um eine explizite „Problem markieren“-Aktion ergänzen. Portable Messwerte für zuletzt dargestellten Frame, Fortschritt und Audiozustand prüfen und die Kriterien des Windows-[Gesundheitsmonitors](../nanoboy/Diagnostics/WindowsSessionHealthMonitor.cs) nur dort übernehmen, wo sie unter SDL dieselbe Bedeutung haben.
2. Gemeinsame stabile Ereigniscodes/Phasen für ROM-Start, Save-Wiederherstellung und Online-Verbindung definieren. Linux darf eigene UI-/Backend-Felder behalten, aber vergleichbare Fehler müssen in beiden exportierten Berichten auffindbar sein. Roh-Exceptions vor Anzeige und ZIP-Export auf private Pfade/Serverdaten prüfen.
3. Vorhandenen [`.sav`-Import und ZIP-Export](../frontends/AetherBoy.Desktop/WaylandEmulatorHost.SaveTools.cs) gegen die Windows-Akzeptanzfälle prüfen: falsche Größe, unbekannte ROM-Herkunft, RTC-Nebendateien, Fehler vor/bei Restore, persistierte vs. noch im Speicher befindliche Daten, Neustart. Falls W2 ein optionales Archivmanifest einführt, Linux-Export ergänzen, ohne ältere Archive oder rohe `.sav` zu brechen.

**Fertig, wenn:** problematische Sitzungen lokal nachvollziehbar sind und ein fehlerhafter Save-Import weder Original noch Backup gefährdet.

### L7 Linux-Pakete und echte Desktops abnehmen

1. [package-linux.sh](../scripts/package-linux.sh) für x64 und ARM64 mit aktuellem Core, Runtime, nativer Online-Bibliothek und Lizenzdateien ausführen. x64-Paket frisch entpackt starten; ARM64 **auf echter ARM64-Hardware** starten, nicht nur ELF/Archiv prüfen.
2. Auf Wayland und Hyprland Dateiauswahl, Fensterfokus, Vollbild, Controller-Hotplug, Audio/PipeWire, Text-/IME-Eingabe und lokale Zwei-Spiel-Anzeige prüfen. Die optionale GTK-/AT-SPI-Oberfläche mit Orca an einem realen Desktop testen; Weston-Headless bleibt zusätzliche Regression.
3. Linux-Desktop-Tests, Native-Loopback und Packaging-Smoke wiederholen. Testpaket, Buildkennung, Architektur und ungeklärte Einschränkungen für den Windows-Entwickler dokumentieren.

**Fertig, wenn:** beide unterstützten Architekturpakete tatsächlich starten und die Wayland-/Hyprland-Bedienung nicht nur im isolierten Testlauf funktioniert.

## Zusammenführung und Spielabnahme

1. **Erster Integrationspunkt:** W0, W1–W2 und L0–L1. Danach können beide Entwickler Saves sicher bewegen und die Verbindung ohne ROM messen. Vor dem nächsten Paket Core-/Runtime-/Frontend-Tests auf beiden Systemen wiederholen.
2. **Zweiter Integrationspunkt:** L2–L3 plus W6. Änderungen an `LocalLinkSession` nur mit gleichzeitigen Windows- und Linux-Regressionen zusammenführen. Der lokale Link wird für GB/GBC und GBA getrennt bewertet.
3. **Dritter Integrationspunkt:** W3–W5 einschließlich W3a und L4–L6. Jeder sichtbare Einstellungseintrag braucht eine ausführbare Aktion und Persistenztest; alle sechs Farbpresets haben auf beiden Systemen gleiche IDs/Farben, und gemeinsame Diagnosecodes werden auf beiden Seiten gelesen.
4. **Paketfreigabe:** W7 und L7 mit identifizierbarem Commit/Build, aber eigenen OS-Paketen. Datenschutz, Lizenzhinweise und Absturz-/Save-Wiederherstellung sind Pflicht.
5. **Online-Abnahme:** Die sechs Host/Gast-Paarungen der [Link-Testmatrix](LINK_PLATFORM_VALIDATION_2026-09-29.md) nacheinander prüfen: Windows↔Windows, Linux↔Linux, Windows↔Linux, jeweils beide Rollen. Erst ROM-freie Probe über zwei Internetanschlüsse, dann GB, GBC und GBA mit geschützten Sitzungsspielständen. Ein erfolgreicher Verbindungsaufbau, ein lokaler Austausch oder ein einzelner grüner Test ersetzt nicht reguläres Speichern und einen **Kaltstart beider Saves** nach einem echten Tausch.

Für jeden Abschluss den konkreten Commit, Plattform/Architektur, Paketkennung, ausgeführte Tests und Skips sowie offene Einschränkungen festhalten. Erst dann den zugehörigen Tabellenpunkt im [Funktionsabgleich](PLATFORM_PARITY_CONCEPT_2026-09-30.md) von „Teilweise“ auf „Ja“ ändern.
