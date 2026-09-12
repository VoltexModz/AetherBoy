# AetherBoy – Changelog

Dieses Dokument unterscheidet bewusst zwischen vorhandenen, verifizierten und noch nicht freigegebenen Funktionen.

## Unveröffentlicht – WebRTC-Diagnose / Windows parity (2026-09-13)

- Linux-Commits `6e69f83` und `3850464` unverändert übernommen: F10-Reservierung,
  Bereinigung abgeschlossener Linux-Online-Sitzungen, sichere Browser-Fehlercodes
  und Schutz vor verspätetem Überschreiben von verbundenen/beendeten Browserzuständen.
- Windows zeigt validierte Verbindungsgründe auf Deutsch und hält sie unter
  TOOLS → Online Link → Letzte Verbindungsdiagnose nach dem Sitzungsende abrufbar.
  Beendete/fehlgeschlagene Online-Besitzer werden erst nach vollständiger
  Finalisierung freigegeben; Originalspielstände und Fortsetzen-Dateien bleiben erhalten.
- English: integrated the colleague's shared browser/native diagnostics and Linux
  cleanup; added typed failure categories, retained Windows diagnostics and safe
  completed-owner retirement before input/frame updates. No NAT/relay service,
  transport-version change or successful WAN Pokémon trade is implied.
- [Übergabe und Prüfgrenzen / handoff and verification limits (DE/EN)](docs/WEBRTC_DIAGNOSTICS_PARITY.md).

## Unveröffentlicht – GBA Gen3 Online / development profile

- Austauschbarer GBA-SIO-Gegenstellenvertrag und eigener Zweispieler-Gen3-Adapter:
  beobachteter Spiel-Handshake, acht Datenwörter, lokale Prüfsummen, begrenzte Queues
  und fehlersicherer Phasenwechsel. Lokaler GBA-Kabelpfad bleibt eigenständig.
- GBA-Onlinebesitzer in derselben Runtime, getrenntes v2-Protokoll, Pause-/Abbruchsteuerung
  und vorhandener verschlüsselter Browser-WebRTC-Transport für Windows und Linux.
- Exakte Originalfassungen über veröffentlichte Prüfsummen identifizieren; nur nach
  ausdrücklicher Entwicklungsbestätigung starten. Kein fertiger Kompatibilitätsnachweis,
  keine automatische Freigabe von Hacks anhand ihrer Spielkennung.
- Lokales Sitzungsjournal, Original-/Arbeitsdateiprüfung, konservative Behandlung
  ungeklärter Ergebnisse, bewusste Übernahme mit vollständiger vorheriger Sicherung.
  Beide Frontends archivieren alte Fortsetzen-Dateien; manuelle States bleiben erhalten.
- English: original two-player Gen3 protocol endpoint, shared cross-platform GBA owner,
  strict versioned transport, development-only profile identification and explicit,
  journaled save-copy recovery. No retail-trade/WAN claim, automatic import or hosted relay.
- [Anleitung, Architektur, Nachweise und offene Abnahme (DE/EN)](docs/GBA_ONLINE_HANDOFF.md).

## Unveröffentlicht – GB/GBC Online Link / Online Link prototype

- Eigenes GB/GBC-Netzwerkkabel mit eindeutigen Sitzungs-/Transfernummern,
  Bereitschafts-/Abschlussbarrieren, echten seriellen Takten und erklärten Abbrüchen.
- Gemeinsame Windows-/Linux-Runtime, Online-Link-Einstiege in beiden Frontends und
  lokale WebRTC-Browserbrücke mit manuellem Offer/Answer. Kein ROM-/Save-Dateiaustausch,
  kein standardmäßiger öffentlicher STUN/TURN-Kontakt, keine garantierte NAT-Durchquerung.
- Lokale Batterie-/RTC-Sitzungskopien und gesperrte Zeitmanipulation schützen Originale;
  auch nach Trennung keine automatische Spielstandübernahme.
- Beim Gesamttest gefundenen Schließfehler im bisherigen lokalen Linkfenster behoben:
  Bild-/Controller-Aktualisierung hält ihren Sitzungsbesitzer fest und erkennt
  zwischenzeitlichen Abbau durch erneut eintretende UI-Ereignisse. Regressionstest ergänzt.
- DoubleCherryGB als Referenz heruntergeladen und relevanten Netzwerkpfad auditiert,
  ohne Implementierungscode zu übernehmen. Eigenes Protokoll, nicht wire-kompatibel.
- Grenzen dieses früheren Meilensteins: paired-transfer GB/GBC-Prototyp, langsame
  Byte-Runden über WAN möglich, kein echter Pokémon-Tausch nachgewiesen. Das inzwischen
  ergänzte GBA-Gen3-Entwicklungsprofil ist im neueren Abschnitt oben beschrieben.
- English: original cross-platform GB/GBC cable-data prototype with an encrypted
  browser-assisted transport, bounded validated packets and private local save copies.
  Actual Pokémon trading and Internet/relay qualification remain pending. The later
  GBA Gen3 development profile is documented in the newer section above.
- [Anleitung und Entwicklerübergabe / usage and handoff](docs/ONLINE_LINK_HANDOFF.md),
  [Quellenprüfung / source audit](docs/DOUBLECHERRY_NETWORK_REFERENCE.md).

## Unveröffentlicht – GBA Link Lab und Linux-Zusammenführung

- Windows Local Link Lab unterstützt jetzt zwei GBA-Spiele mit 240×160-Puffern,
  L/R-Eingabe, gemeinsamer Zeitsteuerung und getrennten persistenten Spielständen.
  GB/GBC und GBA bleiben unterschiedliche, nicht mischbare Kabelprotokolle.
- GBA-SIO ergänzt Normal-8-/32-Bit und Multiplayer-16-Bit für zwei Geräte mit
  Baudraten, IDs, Ready/Busy/Error, Empfangsslots und Interrupts. Kein Netzwerk,
  Wireless, Joybus oder Vier-Spieler-Modus; echte Spielabnahme steht noch aus.
- Mehrtaktiger ARM-/Thumb-Instruktionszustand ist pro CPU statt global: zwei
  Geräte dürfen weder geladene Werte noch laufende Befehle gegenseitig ändern.
- GBA-Geräte-States schreiben Schema 6 und lesen weiterhin Schema 5. Batterie-
  Saves bleiben unverändert. Einseitiges Capture/Restore am Link wird abgewiesen.
- Linux-Commit `367674f` inhaltlich mit lokaler Windows-/Parity-/Link-Arbeit
  vereinigt. Neue Linux-Komfort-/Accessibility-/Ladeabläufe erhalten gemeinsame
  Audio-Generationen, Save-Locks, Screenshots und Performance-Anzeige.
- [GBA-Entwicklung, Herkunft und Tests (DE/EN)](docs/GBA_LOCAL_LINK_HANDOFF.md),
  [Linux-Beitrag und Integrationsprüfung (DE/EN)](docs/LINUX_UPSTREAM_INTEGRATION_REVIEW.md).
  Kein Commit oder Push dieses vereinten Stands ohne ausdrückliche Freigabe.

## Unveröffentlicht – Linux-Restkritik, Runde 3

- Vollständiger gemeinsamer Texteditor mit Caret, Auswahl, Maus/Drag, Clipboard,
  IME-Komposition, Scroll und sauberem Fokuswechsel für Titel/Suche/Cheats.
- Optionales natives GTK3-Control-Center mit ATK-/AT-SPI-Semantik via Ctrl+F7
  beziehungsweise `--accessible`; F7 bleibt Rewind. Reale Schnittstellen- und
  getrennte Busprüfungen ergänzen die SDL-Tests.
- Abbrechbare ROM-Vorbereitung im Hintergrund, sichere GBA-Fokus-/Owner-Barriere
  und gesperrte Aktionen hinter dem Ladeoverlay.
- Hintergrundschreiben unveränderlicher Settings-/Profilsnapshots mit Generation
  und Retry; kritische Übergänge warten weiterhin auf erfolgreiche Speicherung.
- [Fix-IDs, Vorher/Nachher, Tests und verbleibende Grenzen](docs/LINUX_FIX_LOG.md).

## Unveröffentlicht – Linux-Komfort und Distribution

- Begrenzte Hintergrundabfragen für Library, Save-Slots und Backup-Metadaten;
  veraltete Ergebnisse dürfen keine inzwischen gewechselte ROM überschreiben.
- Getrennte Resume-Punkte, State-Galerie mit verifizierten Vorschaubildern und
  Undo nach Laden; Startseite und Overview bieten direktes Fortsetzen.
  Ein nicht lesbarer Resume wird nicht automatisch durch den neuen Start ersetzt.
- Spielbezogene Einstellungen mit globaler Vererbung, Favoriten, eigene Titel,
  Systemfilter und aktive Spielzeit ergänzen die bisherigen Linux-Werkzeuge.
- Drei Schriftstufen, echte Palettenmuster, kurze Aktionsnamen und F6-Navigation
  verbessern das Control Center. Asynchrone Listenwechsel erhalten nur eindeutig
  zugeordneten Fokus; Umordnungen dürfen keine falsche Aktion auslösen.
- x64-/ARM64-Archive mit Runtime, Lizenzen und korrespondierenden Quellen;
  glibc mindestens 2.38. ARM64-Ausführung bleibt praktisch zu prüfen.
- Unabhängige, fehlerorientierte Abnahme und ehrliche Noten stehen in
  [Kritik Runde 2](docs/LINUX_CRITIQUE_ROUND2.md) und
  [Playtest Runde 2](docs/LINUX_PLAYTEST_ROUND2.md). Keine neuen Langläufe.

## Unveröffentlicht – Linux Patch Lab

- Library → Patch Lab verwendet den gemeinsamen IPS-/BPS-/UPS-Patcher mit
  Dateiauswahl, Drag-and-drop, explizitem UPS-Rückpatchen und direktem Ergebnisstart.
- Patchen läuft im Hintergrund; Ergebnisse liegen getrennt nach Inhalts-Hash.
  Originale/Saves bleiben erhalten, bekannte Ergebnisse werden wiederverwendet.
  Fehler und Abbruch der Dateiauswahl werden direkt im Ablauf behandelt.
- Acht neue Tests prüfen Import, drei Formate, UPS-Undo, Original-/Save-Erhalt,
  defekte Dateien, Katalogfehler und den nativen SDL-Bedienablauf.

## Unveröffentlicht – Linux-Daten, Werkzeuge und Bedienqualität

- Saves und States liegen getrennt nach ROM-Inhalts-Hash in der XDG-Datenablage.
  Bestehende Save-Familien werden kopiert; Schreibbesitz verhindert konkurrierende
  Instanzen, und eine lesbare Einstellungssicherung ermöglicht Wiederherstellung.
- Installation verwendet geprüfte Release-Ordner und einen gemeinsamen Starter.
  Isolierte Integrationstests prüfen fehlgeschlagene Updates, ungewöhnliche Pfade,
  Desktop-Start ohne dotnet im PATH und Deinstallation mit Datenerhalt.
- Bibliothek mit Suche und neu zugeordneten ROM-Pfaden, Save-Backup-Wiederherstellung
  mit Vorher-Archiv, Import/Export, Firmware-Import, WAV-Aufnahme und vom Core
  unterstützte Sitzungs-Cheats ergänzen das Control Center.
- Controller erhalten GUID-Profile, freie Belegung, Gerätewechsel und Deadzone.
  Fokus-Pause ist konfigurierbar; Menüfokus und Spieltasten sind getrennt.
- Mindestschrift, Kontrast, kleine Fenster, Speicherplatzstatus und Hilfetexte
  wurden nach unabhängiger Kritik überarbeitet. Dokumentierte Bewertung:
  UI 8,2/10, Features 7,9/10; Prüfgrenzen stehen in `docs/LINUX_CRITIQUE.md`.
- GBA-Stereo bleibt bis SDL erhalten; vorhandene Mono-Konsumenten bleiben
  kompatibel. Idle-/Pause-Rendering wird gedrosselt. Begrenzte lokale Diagnose
  mit ZIP-Export und Audio-Messwerten erleichtert Fehlersuche.
- Native Wayland-, Datenintegritäts- und synthetische Spieltests ergänzen die
  Linux-CI für x64 und ARM64. Reale Spielkompatibilität, hörbare Audioqualität
  und physische Geräte bleiben getrennte Abnahmen; siehe `docs/LINUX_PLAYTEST.md`.

Gesamtübergabe des Windows-Entwicklungspakets vom 11. September 2026:
[Deutsch / English: Änderungen, Linux-Verträge, Prüfung und nächste Schritte](docs/WINDOWS_DEVELOPMENT_HANDOFF.md).
CI-Mindestumfang: 440 Tests unter Windows, 126 Runtime-Tests unter Linux.
Der neue Patch-Lab-UI-Test verwendet einen expliziten WinForms-Kontext und prüft
unerlaubte Threadzugriffe, damit asynchrone Abschlussanzeigen zuverlässig getestet werden.

## Unveröffentlicht – UPS im Windows Patch Lab

- UPS1 für GB/GBC/GBA direkt im vorhandenen C#-Patcher, ohne neue Bibliothek.
  Basis-, Patch- und Ergebnis-CRC32 verpflichtend; begrenzte Größen/Positionen.
- Ausdrückliches Rückpatchen mit demselben UPS-Patch. Vergrößern und Verkleinern
  erzeugen die exakte Ergebnisgröße. Kein stilles Rückpatchen bei bereits gepatchter ROM.
- Original und Hack bleiben erhalten; beim Rückpatchen auf einen bekannten
  Bibliothekseintrag bleiben dessen Titel, Favoriten, Spielzeit, Profile und Saves bestehen.
- Ungültige IPS-/BPS-/UPS-Patches zeigen jetzt zuverlässig den Fehlergrund im
  Dialog; defekte optionale Bibliotheksmetadaten verbergen keinen erfolgreichen Import.
- English: UPS apply/explicit undo, all three CRCs, exact output lengths and
  existing-library data preservation. No new dependency or Linux frontend changes.
- [DE/EN-Bedienung und Testgrenzen](docs/WINDOWS_PATCH_LAB_DIAGNOSTICS.md).

## Unveröffentlicht – Patch Lab, Beobachtung und GBA Inspector (8–10)

- IPS-/BPS-Patcher in der Bibliothek: Originale unverändert, begrenzte Parser,
  BPS-CRC32-Prüfungen, eigene Saves, Hash-Provenienz und eigener Bibliothekstitel.
- Unabhängiger Development-Beobachter für verdächtige UI-/Emulations-/Bildhänger,
  lange Starts und einfarbige Ausgaben. Keine automatische Crash-Einstufung;
  Unterdrückung bei Pause/Fokusverlust, „Problem markieren“ in Quick Deck/Control
  Center. Weiterhin nur lokale, datenarme Berichte.
- GBA Inspector aktiviert: PSG, Direct Sound A/B, FIFO/Timer/Routing/Pegel,
  beide entpackten Wave-Bänke und Stereo-WAV mit 65.536 Hz.
- Shared runtime additions available for future Linux integration; Linux frontend
  unchanged. No mGBA implementation imported in this package.
- [DE/EN-Anleitung, Referenzen und Testgrenzen](docs/WINDOWS_PATCH_LAB_DIAGNOSTICS.md).

## Unveröffentlicht – Windows-Controller, Aufnahme und Stereo (4–6)

- Controller-Navigation für die Aether-Dialoge, kompakte Bildschirmtastatur und
  eigene ROM-Dateiauswahl. Fokuswechsel/Wiederverbinden verlangen Neutralstellung;
  Menüeingaben werden vom Spiel getrennt, Remapping sperrt die Menünavigation.
- Quick Deck mit F10, QUICK oder L3+R3: pausierte Bedienung von Slots,
  Speichern/Laden, Anzeigeoptionen und Zugängen zu Bibliothek/Control Center/Galerie.
- F12 für native Spielbild-PNGs unter der lokalen Screenshots-Ablage. F9 für
  Präsentations-FPS, Bildabstände/P95 und Audiopufferdaten, ohne automatische Uploads.
- Stereo für GB/GBC-Routing und GBA-PCM über die Runtime bis zur Windows-Ausgabe;
  frame-ausgerichtete Puffer und Stereo-WAV im bestehenden GB/GBC-Aufnahmedialog.
- Gemeinsame Runtime erweitert, Mono-Kompatibilitätsaufrufe für Linux beibehalten.
  GB/GBC-State-Leser akzeptiert alte Mono-Audiopayloads; neue Stereo-Payloads sind
  nicht rückwärtskompatibel mit älteren Builds. Batterie- und GBA-State-Formate unverändert.
- Neue Tests und DE/EN-Hinweise einschließlich Übergabe an Linux:
  [Controller / Screenshots / Stereo](docs/WINDOWS_PLAYER_TOOLS_STEREO.md).

## Unveröffentlicht – Windows-Spielkomfort

- State-Galerie (F6) mit fünf manuellen Slots, separatem Fortsetzen-Slot,
  prüfsummengebundenen lokalen PNG-Vorschauen und vorherigen Rohdateien als `.bak`.
  Fortsetzen-Sicherung etwa alle 60 Sekunden und beim regulären Beenden/Spielwechsel.
- Letztes erfolgreiches State-Laden einmalig rückgängig machen (Strg+F8), ohne
  manuelle Slots zu verändern. Vorheriger Pausenstatus bleibt erhalten.
- Cartridge Vault mit Suche, GB/GBC/GBA-Filtern, Favoriten, Spielzeit, letzter Sitzung,
  Sortierung und Kachel-/Listenansicht. Die bestehende Owner-Draw-Logik zeichnet
  jetzt auch Bildkacheln; Fortsetzen ist eine ausdrückliche Bibliotheksaktion.
- Spielprofile im Control Center: nur geänderte Audio-/Video-/Eingabewerte
  überschreiben globale Vorgaben. Boot-ROM und Save-Slot bleiben global.
- Atomare Metadatenablage, lesbare JSON-Sicherung und isolierte Windows-Tests für
  Speicher-/Profil-/Bibliotheksabläufe. Die bestehende Batterie-Backup-Aktivierung
  berücksichtigt die gemeinsame Speicheroperationssperre.
- Keine Änderungen an Core, Runtime oder Linux durch dieses Paket. Anleitung,
  Datenschutz und Grenzen auf Deutsch/Englisch: [Windows-Spielkomfort](docs/WINDOWS_GAME_COMFORT.md).

## Unveröffentlicht – Windows Audio, Video und Bedienung

- WASAPI Shared/Event als Windows-Standardausgabe, WinMM-Rückfallpfad, automatische
  Wiederverbindung mit dem Windows-Standardgerät. Geräteverwaltung läuft auf einem
  eigenen Thread; ein fehlendes Gerät schaltet die Audio-Einstellung nicht dauerhaft ab.
- Begrenzter Mono-Ringpuffer, Vorpuffer und 20/40/60/100-ms-Zielwerte (Standard 40 ms).
  Live-Anzeige von Backend, Gerät, Puffer, Unterläufen und verworfenen Samples.
- Direct2D-Hardwareausgabe mit VSync-Schalter, Sharp/Smooth/LCD Grid und optionalem
  Integer Scaling. Geräteverlust führt zunächst zur GDI-Ausgabe; begrenzte automatische
  Neuversuche, erneuter Versuch auch durch Aus-/Einschalten der GPU-Option.
- Windows-Timerpräzision während aktiver Emulation und 8-ms-Abfrage des neuesten Bilds.
  Die Emulationsgeschwindigkeit bleibt unabhängig von der Bildwiederholrate.
- Echtes randloses Vollbild auf dem aktuellen Monitor (F11 / Alt+Enter), Esc zum
  Zurückkehren, Wiederherstellung des Fensters. Per-Monitor-V2-DPI und bei Bedarf
  scrollbar erreichbare Dialoginhalte statt abgeschnittener Einstellungen.
- Save-State-Rückmeldung für laufende, erfolgreiche und fehlgeschlagene Aktionen;
  Control Center zeigt Existenz und Änderungsdatum des gewählten Slots.
- Lokale Development-Heartbeats enthalten Audio-/Videozähler, keine Audiosamples
  oder Gerätenamen aus der Windows-Ausgabe. Abhängigkeiten und Lizenzhinweise aktualisiert.
- Umfang, Bedienung, Verifikation und offene Hardware-/Spieltests sind auf Deutsch
  und Englisch in [Windows Audio / Video / UI](docs/WINDOWS_AUDIO_VIDEO_UI.md) dokumentiert.

## Unveröffentlicht – NekoZDevTeam

- NekoZDevTeam als aktuelles Entwicklerteam in Windows-Titelleiste, Über-Fenster,
  deutschen/englischen READMEs und den Metadaten beider Frontends sichtbar gemacht.
- Über-Fenster mit aktuellem GB/GBC/GBA-Status und Herkunft der Codebasis ergänzt.
  Projektlink führt direkt zum gemeinsamen GitHub-Repository. Der Coffee-Button
  ist bereits sichtbar. Bis die bestätigte Unterstützungsadresse vorliegt, zeigt er
  „Kommt bald“ und öffnet beim Anklicken einen Hinweis statt einer externen Seite.

## Unveröffentlicht – Zentrale Windows-Daten und Entwicklungsdiagnose

- Die normale Windows-Anwendung zeichnet im Development-Kanal automatisch lokal
  auf, auch im Release-Build. Keine separate Tester-Anwendung erforderlich.
  Ein eingebetteter Buildkanal steuert das Verhalten; stabile Veröffentlichungen
  können `AetherBoyChannel=stable` setzen. `--tester-mode` bleibt optional verfügbar.
- Zentrale Ablage unter `%LOCALAPPDATA%\AetherBoy`: ROM-Kopien nach Inhalts-Hash,
  getrennte Saves und States, stabile JSON-Einstellungen und Firmware-Ordner.
  Legacy-Saves, RTC, Integritätsdateien und Backups werden beim ersten ROM-Import
  kopiert. Vorhandene zentrale Daten haben Vorrang, Originaldateien bleiben erhalten.
- Die zuletzt gespeicherte lesbare WinForms-Konfiguration wird beim ersten Start
  in die zentrale Einstellungsdatei übernommen, auch nach einem Wechsel des
  EXE-Ordners. Eine beschädigte JSON-Datei kann aus der letzten lesbaren Sicherung
  wiederhergestellt werden. Windows-Tests verwenden einen isolierten Datenordner.
- Vollständige lokale ROM-Bibliothek sowie Ordnerzugriffe in Bibliothek und Control
  Center ergänzt. Änderungen an der Slotwahl während eines asynchronen Quick Save
  verändern dessen ursprünglich gewählten Zielslot nicht mehr.
- Lokale JSONL-Sitzungsberichte erfassen Version, ROM-Header/Hash,
  Frame-Fortschritt, Controllerwechsel, Save-State/Rewind-Ergebnisse und
  datensparsame Fehlerklassen. ROM-Bytes, ROM-Pfade sowie Batterie- und
  Save-State-Inhalte werden nicht gespeichert; es existiert kein Upload.
- Das Windows-Control-Center zeigt den Aufzeichnungsstatus und kann den aktiven
  Testordner öffnen oder README und Protokoll manuell als ZIP exportieren.
  Der Export umfasst ausschließlich diese beiden Dateien. Development-Sitzungen
  und Crashlogs liegen getrennt unter `development/Sessions` und `development/Crashes`.
- Privacy-, ZIP- und UI-Smoke-Tests ergänzen den Windows-Gesamtlauf. CI prüft
  jetzt auch direkte Pushes auf `development` und fordert mindestens 354 Tests.

## Unveröffentlicht – Nativer Linux-/Wayland-Desktop

- Linux-Tastaturbelegung im Control Center frei änderbar: Aktion anklicken und
  neue Taste drücken, einschließlich Bewegung, GBA L/R, Turbo und Pause.
  Belegte Tasten werden getauscht; App-Shortcuts bleiben geschützt.
- Master-Lautstärke mit Schieberegler von 0 bis 100 Prozent und feinen
  1-Prozent-Schritten ergänzt. Tastaturbelegung, Lautstärke und Stummschaltung
  werden atomisch in der XDG-Konfiguration gespeichert und beim Start geladen.
- GBA-Start ohne BIOS repariert: eigener ARM-IRQ-Stub leitet Hardware-Interrupts
  an den Cartridge-Handler weiter und stellt Register/CPSR wieder her.
  CpuSet/CpuFastSet verwenden die korrekten Kopier-/Füllbits. LZ77 akzeptiert
  leere Daten sowie abweichende Typkennungen wie beim BIOS-Aufruf; ungültige
  Rückverweise bleiben abgefangen. Synthetische Regressionstests benötigen
  weder ein Spiel noch Nintendo-Firmware.
- Linux-Spieloberfläche mit größerer Spielfläche, optionaler Systemschrift,
  klaren Lade-/Fehlerzuständen und direkten Pause-/Save-/Load-Aktionen überarbeitet.
  Fehlgeschlagene ROM-Wechsel erhalten die vorige Sitzung. Fokusverlust löst
  gehaltene Tasten; Escape beendet das Spiel nicht mehr versehentlich.
- Launcher erkennt Änderungen am vendorten GBA-Kern und baut bei Bedarf neu.

- Eigenen `net10.0`-Linux-Host auf SDL3-Basis ergänzt. Er verwendet denselben
  Core und Runtime-Vertrag wie das Windows-Frontend, rendert GB/GBC mit 160×144
  und GBA mit 240×160 und lädt `.gb`, `.gbc` und `.gba` per Kommandozeile oder
  Wayland-Drag-and-drop.
- Den SDL-Videotreiber fest auf `wayland` gesetzt und den tatsächlich gewählten
  Backendnamen nach der Initialisierung geprüft. X11 und XWayland werden mit
  klarer Diagnose abgelehnt statt still als Fallback zu starten.
- Hyprland, KDE und GNOME werden getrennt erkannt. Hyprland erhält die stabile
  App-ID `io.github.VoltexModz.AetherBoy`, compositorseitige Dekoration und ein
  mitgeliefertes XDG-Portalprofil mit GTK-Dateiauswahl-Fallback.
- Native SDL3-Audioausgabe mit F32-Stream, Masterpegel, vier Kanalschaltern,
  begrenzter Warteschlange und Timeline-Flush ergänzt. Fehlende Linux-
  Audiobibliotheken degradieren kontrolliert zu stummem Betrieb.
- SDLs asynchronen Dateidialog direkt angebunden; unter Linux führt er über
  XDG Desktop Portal. Rückgaben gelangen threadsicher in den SDL-Hauptloop,
  während Erweiterung und Existenz weiterhin lokal validiert werden.
- Tastatur, SDL3-Gamepad-Hot-Plug und Analogstick angebunden; GBA-L/R, Pause,
  gehaltenes Turbo und Vollbild funktionieren im nativen Host. Das neue
  Aether Control Center schaltet Sharp/Smooth/LCD Grid, Frameskip, DMG-Paletten,
  Audio, Kanäle, Inputstatus und Timeline-Werkzeuge.
- Alle fünf `.ss1`–`.ss5`-Slots, atomisches Schreiben, Slotwahl und Rewind sind
  über Control Center und Tastatur erreichbar. Batterie-Saves und Zustände
  bleiben ROM-nah und mit dem Windows-Vertrag kompatibel.
- Reproduzierbare Build-, Run- und benutzerlokale Installationsskripte für
  `linux-x64` und `linux-arm64`, Freedesktop-Desktopdatei sowie die vorhandenen
  AetherBoy-Icons in allen Größen ergänzt.
- Linux-CI baut jetzt zusätzlich den nativen Desktop und prüft 15 neue Tests für
  Wayland-/Hyprland-Erkennung, Frontendoptionen und atomare Zustandsdateien.
  Unter Ubuntu wurden Publish, WSLg-Wayland-Fensterloop sowie 175 Core-, 87
  Runtime- und 15 Frontendtests verifiziert; insgesamt sind 305 Tests grün.
- Persistente Linux-Einstellungen, freie Eingabebelegung, Cartridge Vault,
  Cheats-, Diagnose-, Save-Safety- und WAV-Werkzeuge bleiben als nächste
  Linux-Frontendschritte offen.

## Unveröffentlicht – Integriertes GBA-Backend

- DaveTCode/GBADotnet am exakten Commit
  `994c4b225c6e4277ada8d37bb9283f53827ee3e1` geprüft und dessen MIT-lizenzierten
  C#-Kern samt vorerzeugter Decoder als nachvollziehbaren Source-Snapshot unter
  `third_party/GBADotnet.Core` aufgenommen. ROMs, BIOS, Oberflächen und
  Kompatibilitätsbilder wurden nicht übernommen.
- `GbaProductionMachine` verbindet ARM/Thumb, Bus, Scheduler, PPU, PSG plus
  Direct Sound, Keypad und SRAM/Flash/EEPROM/RTC mit AetherBoys Owner-Thread, dynamischem
  240×160-Frame-Austausch, Audioausgabe und geschützter Batterie-Speicherung.
- `.gba` im Cartridge Vault, Drag-and-drop, Recent Files und Kommandozeilenstart
  freigeschaltet; Status, ROM-Info und Control Center zeigen das Modell als GBA.
  DMG/CGB-Boot-ROMs werden für diesen Pfad nicht geladen.
- GBA L/R separat vom unveränderten GB-Tastenbyte ergänzt. Q/E sowie LB/RB sind
  sichere Defaults, Tastatur und Gamepad lassen sich jetzt in der Input Matrix
  neu belegen; LT/RT bleiben als zusätzlicher Controller-Fallback aktiv.
- Einen eigenen, versionierten und Brotli-komprimierten GBA-Zustandsvertrag für
  CPU/Pipeline, Scheduler, Bus, RAM, PPU, APU, DMA, Timer, IRQ, Eingabe,
  Cartridge-Controller und Bildzustand ergänzt. SHA-256 schützt die Datei und
  bindet sie an exakte ROM und BIOS. Fünf Quick-Save-Slots, F5/F8 und Rewind
  sind im normalen Fenster freigeschaltet.
- Frameskip für GBA an die reine Bildpräsentation angebunden; Hardwarezeit und
  Rewind-Aufzeichnung laufen auch bei ausgelassenen Bildern vollständig weiter.
- Optionales, vom Nutzer bereitgestelltes `gba_bios.bin` mit strikter
  16-KiB-Prüfung ergänzt. Ohne Datei übernimmt ein eigener HLE-BIOS-Fallback
  Reset/Wait/Halt, Mathematik, CpuSet/CpuFastSet, affine Matrizen und die
  dokumentierten BitPack-, LZ77-, Huffman-, RLE- und Differential-Decoder;
  keine proprietäre Firmware wird mitgeliefert.
- Kern- und UI-Funktionen über zentral veröffentlichte Capability-Metadaten
  geschaltet und den statischen DMA-Pipelinezustand pro Emulatorinstanz isoliert.
- Den importierten Kern zur gepflegten AetherBoy-Basis weiterentwickelt: vollständige
  GBA-Headerfeldbreiten, allokationsfreie SDK-Save-Marker-Erkennung inklusive
  `SRAM_F`, stabilere Flash-Befehlsfolgen und korrektes 128-KiB-Banking ergänzt.
  EEPROM-Daten laufen nun in Hardware-Bitreihenfolge und können Nullbits korrekt
  überschreiben; ungültige ARM-/Thumb- und Coprozessorbefehle nehmen den
  Undefined-Instruction-Vektor, statt den Emulatorprozess zu beenden.
- Alle vier GBA-PSG-Kanäle mit Frame-Sequencer, Länge, Hüllkurve, Pulse-Sweep,
  Wave-RAM und Noise-LFSR implementiert und mit Direct Sound stereo gemischt.
  Die vorhandenen Kanal-Schalter und der Audio Inspector gelten nun auch für GBA;
  ein Wave-DAC-DC-Offset sowie FIFO-Nachfüllung nach Teilverbrauch wurden korrigiert.
- GPIO-RTC mit serieller Bitfolge, BCD-Datum/-Zeit, 12/24-Stundenmodus,
  Save-State-Transienten und eigener atomarer `.sav.rtc`-Persistenz samt Backups
  ergänzt. Der Cartridge-Status weist RTC und den aktiven BIOS-Modus aus.
- GBA-Serial von sofortigem Fake-IRQ auf zeitgesteuerte 8-/32-Bit-Transfers,
  Multiplayer-/UART-/Joybus-Register, IRQ und Save-State-Fortsetzung umgestellt.
  Ohne Gegenstelle liefert der interne Clockpfad deterministisch High-Bits. Eine
  lokale Zwei-Core-Kopplung tauscht 8-/32-Bit-Daten nun reproduzierbar mit
  internem oder externem Clock-Peer; App-Host und Netzwerk bleiben separat offen.
- GBA-Cheats um gängige CodeBreaker-Direkt-/Logik-/Bedingungscodes sowie rohe
  und verschlüsselte GameShark-v1/v2-RAM-Writes erweitert. Die Engine begrenzt
  auch dekodierte Ziele strikt auf EWRAM/IWRAM; Action Replay/PAR v3 und
  komplexe Hook-/Fill-/List-Codes werden nicht vorgetäuscht.
  Mosaic für Text-, Affine-, Bitmap- und OBJ-Pfade sowie
  mehrere Sprite-/Reset-Randfälle ergänzt.
- Begrenzten lokalen GBA-Diagnosepuffer für HLE-BIOS-Aufrufe, unbekannte
  ARM-/Thumb-Pfade, unmapped I/O und Link-Matches ergänzt. Er speichert keine
  ROM-Bytes, Schreibwerte oder Dateipfade und sendet keine Telemetrie. Auch
  Session-/UI-Abstürze erhalten ein lokales, pfadminimiertes Protokoll.
- Das interne GBA-Kernzustandsschema wegen der neuen Serial-Transienten auf 5
  angehoben. Ältere experimentelle GBA-Save-States werden klar abgelehnt.
- ARM7-SBC-Borrow, DMA-Adressmaskierung, STOP-Taktstillstand, Serial-Reset und
  das gespiegelte interne WRAM-Control-Register gehärtet. Der vollständige
  GBA-Zustand enthält die neuen APU-, RTC- und Serial-Transienten.
- Den PPU-Pfad weiter präzisiert: deaktivierte OBJ-Windows maskieren keine
  Sprites mehr, halbtransparente OBJ erzwingen Alpha-Blending gegen ein
  freigegebenes zweites Ziel, Bitmap-VRAM-Lücken liefern Open Bus und
  Farboperationen verändern nicht länger den deckenden Framebuffer-Alpha-Kanal.
- Bus und Taktmodell an weiteren Hardwaregrenzen gehärtet: korrekte Open-Bus-
  Lanes für unbenutztes MMIO, lesbares WAITCNT-High-Byte, Prefetch-Neustart bei
  WAITCNT-Schreibzugriffen, nichtsequenzieller Zugriff an 128-KiB-ROM-Grenzen,
  EWRAM-Abschaltung sowie fortgeschriebene PPU-Zeilenzyklen.
- EEPROM auf das finale ROM-Fenster begrenzt, vier Null-Dummybits korrigiert und
  die Batterie-Datei dynamisch von 512 Byte auf 8 KiB erweiterbar gemacht.
  Flash verlässt Identifikations-/Löschmodi nun auch über den direkten `F0`-
  Resetbefehl. Vorhandene EEPROM-Dateigrößen werden beim Start wiedererkannt.
- HLE-BIOS-`CpuFastSet` rundet Teilanforderungen korrekt auf acht Wörter auf;
  `RegisterRamReset` setzt jetzt auch DMA, Timer, IRQ, WAITCNT und Prefetch zurück
  und entfernt geplante Timer-Ereignisse. Der IRQ-Reset löscht IE und IF korrekt.
- Upstream bleibt WIP. Vollständig cycle-exaktes Timing/Open Bus, weitere
  Renderer-Kanten, fertiger Link-Host/Transport, PAR-v3-Codes und ein kommerzieller
  Durchspielnachweis bleiben offen. Die Gesamtsuite steigt auf 290 Tests
  (175 Core, 87 Runtime, 28 Windows-Smoke).

## Unveröffentlicht – Eigener GBA-Datenpfad und MBC1M

- Bereitgestelltes mGBA-Quellarchiv auf Architektur, Funktionen und Lizenzhinweise
  geprüft; [Vergleich und Herkunft](docs/MGBA_REVIEW.md) dokumentiert. Kein nativer
  mGBA-Kern und keine mechanische Übersetzung in den Produktcode aufgenommen.
- Eigene ARM7-Arithmetik-, Flag-, Bedingungs- und Shiftbausteine samt neun Tests
  ergänzt. Ein eigener erster ARM-/Thumb-1-Interpreter, GBA-Speicherbus und
  Bitmap-Mode-3-Renderer führen nun ein generiertes ROM end-to-end aus. Der
  plattformneutrale `AetherBoy.GbaProbe` erzeugt daraus ein 240×160-Prüfbild.
  Kommerzielle GBA-Spiele sind noch nicht freigegeben.
- MBC1M-Verdrahtung und konservative 1-MiB-Header-Erkennung in den normalen
  GB-Ladeweg aufgenommen. Sieben Tests decken u. a. den 0x10-Banksonderfall ab.
- Standard-MBC1-Zustände bleiben im bisherigen Format. MBC1M-Zustände erhalten
  eine Kennung; Zustände anderer Verdrahtung werden abgelehnt.
- 235 Tests bestanden; GB/GBC-Konformitätsauswahl erneut unverändert bei 67/70,
  mit den drei bekannten informativen Abweichungen. Elf neue GBA-Prototyptests,
  aber noch keine kommerziellen GBA-Spieltests.

## Unveröffentlicht – GBA-Vorbereitung

- Bildgeometrie von der festen GB-Auflösung entkoppelt: Runtime, Snapshots,
  Frame-Puffer, Windows-Ausgabe, Filter und Skalierung unterstützen nun auch
  das GBA-Format 240×160. Der produktive GB/GBC-Kern bleibt bei 160×144.
- Separaten, nur lesenden GBA-Header-Inspector mit Größenlimit, bereinigten
  Textfeldern, Header-Prüfstatus und vollständigem ROM-SHA-256 ergänzt.
- Synthetische Header-, Runtime- und Rendering-Tests hinzugefügt. Diese prüfen
  noch keine GBA-Emulation; es wurde kein GBA-Kern integriert.
- GBA-ROMs und BIOS-Dateiname vom Git-Quellbaum ausgeschlossen. `.gba` bleibt
  in der Oberfläche nicht freigegeben und wird an der Session-Grenze explizit
  abgelehnt, solange kein passender Kern angebunden ist. Nächste Schritte: [GBA.md](GBA.md).

## Unveröffentlicht – Aether-Wave-UI

### Markenfundament

- Die vom Projektinhaber entworfene Aether-Wave-Richtung als reproduzierbare Vektormarke rekonstruiert: ein verlaufendes `A`, eine aufgelöste Signalwelle und eine integrierte Handheld-Silhouette.
- Detaillierte Mastermarke und optisch vereinfachte Small-Mark für Windows-Systemflächen getrennt, damit das Zeichen auch bei 16 × 16 Pixeln lesbar bleibt.
- Multi-Resolution-ICO mit 16, 20, 24, 32, 40, 48, 64, 128 und 256 Pixeln sowie 512-Pixel-Anwendungsgrafik aus denselben SVG-Quellen erzeugt.
- Altes `N3`-Bild, historische Designer-Icons und das kryptisch benannte 2018er Anwendungsicon entfernt.
- EXE, Hauptfenster, Werkzeuge und About-Dialog beziehen ihre Marke nun aus einer zentralen eingebetteten Branding-Ressource.
- Markenfarben, Größenregeln und der absichtlich sparsame Einsatz des Aether-Verlaufs in `branding/BRAND.md` dokumentiert.

### Hauptfenster

- Klassische sichtbare Menüleiste durch eine eigene rahmenlose Aether-Wave-Chrome mit verschiebbarer Titelfläche, Fenstersteuerung und vier kompakten Funktionsmenüs ersetzt.
- Neue Display-Bühne mit eigenständigem Leerlaufzustand, Markenmotiv und direkter ROM-Aktion ergänzt; einzelne `.gb`- und `.gbc`-Dateien lassen sich außerdem auf das Fenster ziehen.
- Session-Instrumentenleiste zeigt ROM-Titel, DMG/CGB-Modell, Laufzustand, Framezahl, Audio, Bildfilter und aktiven Save-State-Slot ohne zusätzliche Dialoge.
- Neues Command-Deck stellt Öffnen, Pause/Fortsetzen, Rewind, Speichern, Laden und umschaltbares Turbo direkt bereit; die fünf State-Slots sind ebenfalls unmittelbar anwählbar.
- Bestehende vollständige Menüs bleiben hinter `SYSTEM`, `TUNE`, `TOOLS` und `INFO` erreichbar; `Strg+O`, F5, F8 sowie die bisherigen Spiel- und Turbo-Tasten bleiben erhalten.

### Dialoge und Cartridge Vault

- About, Steuerung, Changelog, Cheat-Manager, Audio Inspector und Link-Lab auf eine gemeinsame rahmenlose Aether-Chrome mit eigener Typografie, Flächen und Fenstersteuerung umgestellt.
- Audiopegel und Wave-RAM-Scope als eigene animierte Telemetrieelemente statt generischer WinForms-Balken neu gezeichnet.
- Native MessageBoxen vollständig durch farbcodierte Aether-Signale für Information, Warnung, Entscheidung und Fehler ersetzt.
- Neue Cartridge-Vault bündelt persistente zuletzt verwendete ROMs, markiert fehlende Dateien, akzeptiert Drag-and-drop und führt erst bei Bedarf in den nativen Dateibrowser.
- Laufender Session-Status und gültige ROM-Drop-Ziele erhalten dezente Puls- und Signalübergänge; Animationen verändern keine Emulationstaktrate.

### Controller

- Windows Gaming Input als primäre Gamepad-Schnittstelle ergänzt; dadurch werden semantisch erkannte PlayStation- und generische HID-Controller neben Xbox-Controllern ohne Zusatztreiber unterstützt.
- Bestehendes XInput-Polling als Rückfall erhalten und beide Quellen auf eine gemeinsame, testbare Gamepad-Zustands- und Mapping-Schicht vereinheitlicht.
- Hot-Plug funktioniert auch ohne laufende ROM; Hauptfenster und Steuerungsdialog zeigen den aktuell erkannten Controller sowie die aktive Belegung an.
- A, B, Start, Select, Quick Load und Quick Save lassen sich im Steuerungsdialog durch Anklicken und anschließenden Tastendruck direkt und persistent neu belegen.
- D-Pad und linker Stick steuern die Richtung, Cross/A die Game-Boy-A-Taste, Circle/B oder Square/X die B-Taste, Options/Menu Start und Share/View Select; L1/R1 laden beziehungsweise speichern weiterhin per Flankenerkennung.

### Phase 9.1 – Save Safety

- Batterie-RAM wird nun spätestens alle 1.800 emulierten Frames und beim sicheren Beenden geschrieben, statt ausschließlich vom erfolgreichen Programmende abzuhängen.
- Rohdatenkompatible `.sav`-Dateien erhalten atomare Write-Through-Ersetzung, drei rotierende Generationen und separate SHA-256-Integritätswächter mit Crash-Recovery für das Zwei-Dateien-Protokoll.
- Trunkierte oder nach der ersten geschützten Sicherung gleich groß verfälschte Hauptdateien werden nicht mehr still teilweise geladen; AetherBoy fällt automatisch auf die jüngste gültige Generation zurück und repariert den Hauptstand.
- Neues Save Safety Center zeigt aktuellen Stand und Backups mit Zeit, Größe und Schutzstatus. Eine gewählte Generation lässt sich kontrolliert wiederherstellen, während der zuvor aktive Stand als neues Backup erhalten bleibt.
- Alte rohe `.sav`-Dateien ohne AetherBoy-Metadaten bleiben kompatibel. Sie erhalten beim nächsten Schreibvorgang automatisch den Integritätsschutz.
- Gesamtsuite auf **189 Tests** erweitert: 148 Core-, 18 Runtime- und 23 Windows-Smoke-Tests; vollständiger Release-Build ohne Warnungen.

### Phase 9.2 – Aether Control Center

- Eigenständige rahmenlose Control-Center-Shell mit sieben klar getrennten Bereichen für Übersicht, Display, Audio, Eingabe, Saves, System und Diagnose ergänzt.
- Filter, fünf DMG-Paletten, Integer-Fenstergrößen und Borderless-Fullscreen sind direkt erreichbar und werden live angewendet.
- Echte persistente Master-Lautstärke von 0 bis 100 Prozent ergänzt; Audio-Master, alle vier Hardwarekanäle und Audio Inspector sind zentral steuerbar.
- Gamepad-Live-Status, USB-Kennung, aktive Tastatur- und Controller-Belegung sowie direkter Einstieg in den Remap-Dialog zusammengeführt.
- Save-State-Slots, Quick Save/Load und Save Safety zeigen ihren aktiven Zustand und den Zustand aller Batterie-Backup-Generationen an.
- Frameskip und Boot-ROM-Autoerkennung sind sichtbar steuerbar; ein sicherer Reset setzt ausschließlich Einstellungen zurück und löscht keine ROMs oder Spielstände.
- Diagnoseansicht zeigt Session, Framezahl, Mapper, Modell, ROM-/RAM-Größe, Region, ROM-SHA-256, lokalen Pfad und Save-Status und kopiert diese Daten nur auf ausdrückliche Aktion.
- Palette, Filter, Save-Slot, Boot-ROM-Policy und Lautstärke sind nun über Neustarts hinweg persistent. Gesamtsuite auf **190 Tests** erweitert.

## 4.8.0-alpha.1 – Phase 8 (2026-08-09)

### APU und Wave-RAM

- Sweep-Shift 0 führt auf dem 128-Hz-Takt die Overflow-Prüfung aus, ohne die Frequenz zurückzuschreiben; Sweep-Negate-Latch und zweistufige Overflow-Prüfung sind vervollständigt.
- APU-Power-On richtet den Frame-Sequencer an der aktuellen DIV-Hälfte aus und überspringt die nächste Flanke, wenn die Quellflanke beim Einschalten bereits hoch war.
- DMG- und CGB-Wave-Startphase, laufende Frequenzänderungen, modellabhängige Wave-RAM-Lese-/Schreibfenster sowie die DMG-Retrigger-Korruption implementiert.
- Analogen modellabhängigen Hochpass mit gehaltenem Kondensatorzustand bei abgeschalteten DACs ergänzt.
- Blargg `dmg_sound` und `cgb_sound` bestehen nun jeweils **12/12** Einzel-ROMs.

### PPU, Serial und Diagnose

- Sprite-Transfergrenze in Mode 3 um die inklusive letzte Fetch-Kante präzisiert; die Mooneye-PPU-Auswahl steigt auf **11/12**.
- Serial-Port von sofortigem Byteabschluss auf acht hardwaregetaktete Bits umgestellt: 512 T-Zyklen pro DMG/CGB-Normalbit, 16 T-Zyklen im CGB-Fast-Modus sowie externer Clock-Eingang und optionaler Bit-Gerätevertrag.
- Laufende Serial-Transfers einschließlich Clockphase, Restbits und Schieberegister in Save States aufgenommen.
- Conformance-Timeouts berichten nun Blargg-Zwischenstand, CPU-Register, DIV, NR52 und aktuellen Opcode statt eines leeren Ergebnisses.

### Verifikation und Grenzen

- Testsuite auf **167 Tests** erweitert: 141 Core-, 17 Runtime- und 9 Windows-Smoke-Tests. Das Save-State-Komponentenschema steigt für neue PPU-, APU- und Serial-Transienten auf 5.
- Reproduzierbare Matrix: **67/70** Läufe bestanden, keine blockierenden Fehler; offen bleiben `rapid_toggle`, `lcdon_timing-GS` und `sources-GS`. Drei lokale Spiele liefen erneut jeweils 600 Frames absturzfrei.
- Ein echter Pixel-FIFO, subzyklische Timer-/DMA-Buskonflikte und ein konkreter Link-Transport zwischen zwei Instanzen bleiben offen. Serielle Bitsemantik und externe Clock-Anbindung sind vorhanden, TCP/Netzwerk bewusst noch nicht.

## 4.7.0-alpha.1 – Phase 7 (2026-08-09)

### Kompatibilität und CPU-Bus

- Manifestgesteuerte Conformance-CLI mit Pflicht- und Informationsläufen, Conformance-/Game-Smoke-Modus, Protokolldiagnosen, finalem PC, Frame-SHA-256 und JSON-Berichten ergänzt.
- CPU-Buszugriffe an T-Zykluspositionen verschoben; Stack, CALL/RET/RST/PUSH, Interrupt-Re-Selektion und Abbruch, DAA, signierte SP-Flags sowie mehrere `(HL)`-Kosten korrigiert.
- Blargg-CPU-Einzelsuite mit 11/11 und `instr_timing` vollständig bestanden.

### Timer, DMA und PPU

- TIMA-/TMA-Schreibkollisionen im Reload-Takt, IF-Lesemaske sowie OAM-DMA-Register, Startverzögerung und Neustart während eines Transfers implementiert.
- LCD-Aus-/Einschaltphase, LYC-Freeze, OAM-Randfenster, überlappende Sprite-Fetches und der DMG-spezifische OAM-STAT-Impuls bei VBlank gehärtet.
- Mooneye-Ergebnis auf 12/13 Timer- und 10/12 PPU-ROMs erhöht; bekannte Restfälle sind in `COMPATIBILITY.md` dokumentiert.

### APU, Zustände und Gates

- APU-Registermasken, DIV-APU-Resetflanke, echte 64-/256-Schritt-Längenzähler, Trigger-Extra-Clock, DMG-Power-Off-Längenregister und initiale Sweep-Overflow-Prüfung implementiert.
- Blargg-Sound-Fortschritt auf 6/12 DMG- und 7/12 CGB-Einzel-ROMs erhöht; Sweep-Negate, analoger Hochpass und DMG-Wave-RAM-Kollisionen bleiben offen.
- Testsuite auf **154 Tests** erweitert: 128 Core-, 17 Runtime- und 9 Windows-Smoke-Tests. Save-State-Komponentenschema wegen neuer CPU-, PPU- und APU-Transienten auf 4 angehoben.
- Drei lokal ignorierte Spiele jeweils 600 Frames absturzfrei ausgeführt; ROMs und externe Test-Binärdateien bleiben außerhalb des Repositorys.

## 4.6.0-alpha.1 – Phase 6 (2026-08-09)

### CPU, Bus und CGB-DMA

- Die elf nicht belegten LR35902-Opcodes verriegeln die CPU nun nach dem Opcode-Fetch bis zum Reset; anstehende Interrupts lösen die Verriegelung nicht.
- General-DMA und HBlank-DMA von sofortiger Blockkopie auf progressive Übertragung mit einem Byte je zwei Dots und 32 CPU-Stall-Dots pro 16-Byte-Block umgestellt.
- HBlank-DMA startet keinen Block während CPU-HALT, bleibt blockweise abbrechbar und hält Quelle, Ziel, Restblöcke sowie Teilblockfortschritt vollständig im Save State.
- CGB-exklusive DMA-, Palette-, VRAM-/WRAM-Bank- und Speed-Register im DMG-Modell geschlossen; unbenutzte Bits von SVBK werden auf CGB hoch gelesen.

### PPU und APU

- Mode 3 von einer festen Dauer auf 172 bis 289 Dots umgestellt. Fine-Scroll, sichtbarer Fensterstart und bis zu zehn ausgewählte Sprites liefern deterministische Fetch-Strafen; die feste Scanline-Dauer von 456 Dots bleibt erhalten.
- CGB-Paletten-RAM während Mode 3 genauso wie VRAM gesperrt und die maximale Mode-3-Dauer mit einem eigenen Grenztest abgesichert.
- NR52 als APU-Master-Power und Kanalstatus implementiert: Abschalten leert die APU-Register und Kanäle, erhält Wave-RAM und blockiert reguläre APU-Schreibzugriffe bis zum Wiedereinschalten.
- DAC-Abschaltung beendet aktive Kanäle sofort; Längenablauf aktualisiert die NR52-Statusbits. Puls-, Wave- und Noise-Kanäle laufen über ganzzahlige Hardwareperioden statt über host-sampleratenabhängige Phasen.
- NR50/NR51 steuern jetzt Masterlautstärke und Links-/Rechts-Routing im Mixer. Ein Vollframe-Allokationsgate schützt den APU-Hotpath.

### Conformance, Langlauf und Tests

- `HeadlessConformanceRunner` um Blargg-Memory-Status und Mooneye-Registersignaturen ergänzt; serielles `Passed`/`Failed`, Fehlerausgabe und Timeouts liefern nun das erkannte Protokoll zurück.
- Neue plattformneutrale `AetherBoy.Conformance`-CLI für einzelne ROMs oder rekursive lokale Suites ergänzt, einschließlich Frame-Limit, aussagekräftiger Exitcodes und optionalem JSON-Bericht.
- Restore/Replay-Langlauf von 300 auf 600 Frames verdoppelt und um laufende DMA-, PPU- und APU-Teilzustände erweitert.
- Gesamtsuite auf **127 Tests** erweitert: 101 Core-, 17 Runtime- und 9 Windows-Smoke-Tests. Windows- und Linux-CI bauen die Conformance-CLI; Linux führt zusätzlich ihren Hilfe-Smoke-Test aus.
- Deterministischen Zustandsvertrag wegen der neuen CPU-, DMA-, PPU- und APU-Felder auf Komponentenschema 3 angehoben und zusätzliche Plausibilitätsprüfungen für aktive Transfers und Audiokanäle ergänzt.

### Weiterhin offen

- CPU-Instruktionen und allgemeine Buszugriffe bleiben intern atomar; DMA-Quellbuskonflikte und seltene LCD-/HBlank-Umschaltkanten sind noch nicht vollständig mikrozyklusgenau.
- Die variable Mode-3-Dauer modelliert Fetch-Strafen ohne echten Pixel-FIFO. Mid-Scanline-Registereffekte, Sprite-Fetch-Abbrüche und mehrere Grenzkombinationen bleiben angenähert.
- Der digitale APU-Pfad besitzt noch keinen analogen Hochpassfilter; modellabhängige Power-off-Längenregister und seltene Frame-Sequencer-Schreibkanten sind offen.
- Externe Conformance-ROMs sind aus Lizenzgründen nicht enthalten. Link-Kabel, serielles Bit-Timing, Game Genie und Spezialmapper bleiben spätere Arbeit.
- Save States aus Komponentenschema 1 oder 2 werden bewusst abgelehnt; eine automatische Migration ist nicht vorhanden.

## 4.5.0-alpha.1 – Phase 5 (2026-08-09)

### CPU, Bus und DMA

- STOP ohne vorbereiteten CGB-Speed-Switch als echten Ruhezustand modelliert; eine neue Joypad-Flanke weckt die CPU, während PC und Hardwaretakte im Schlaf stehen bleiben.
- OAM-DMA von einer sofortigen 160-Byte-Kopie auf ein Byte je vier T-Zyklen beziehungsweise 640 T-Zyklen Gesamtdauer umgestellt.
- CPU-Zugriffe während OAM-DMA auf HRAM begrenzt; der DMA-Lesepfad berücksichtigt Boot-ROM-Mapping und der laufende Transfer ist vollständig im Save State enthalten.
- Generiertes OAM-DMA-Test-ROM auf eine während des Transfers aus HRAM ausgeführte Routine migriert.

### PPU und APU

- Scanline-Rendering ohne Tile-/Zeilen-Heapallokationen neu aufgebaut und mit einem harten Allokationsgate für einen vollständigen Frame abgesichert.
- DMG-Spritepriorität anhand des rohen Hintergrund-Farbindex statt der bereits palettierten ARGB-Farbe korrigiert.
- CGB-Hintergrundattribut Bit 7, OBJ-Priorität und LCDC.0 als Master-Priorität korrekt kombiniert; Hintergrund und Fenster bleiben auf CGB auch bei gelöschtem LCDC.0 sichtbar.
- APU auf den achtstufigen 512-Hz-Frame-Sequencer umgestellt: Länge bei 256 Hz, Channel-1-Sweep bei 128 Hz und Lautstärke-Hüllkurven bei 64 Hz.
- Frame-Sequencer, OAM-DMA, STOP und Serial-Control in das neue Komponentenschema 2 des deterministischen Zustandsvertrags aufgenommen.

### Conformance, Rewind und Tests

- Plattformneutralen, framebegrenzten `HeadlessConformanceRunner` ergänzt, der das verbreitete serielle `Passed`/`Failed`-Protokoll von Test-ROMs auswertet.
- Seriellen Byte-Transfer mit lesbarem Control-Register und unmittelbar ausgelöstem Serial-Interrupt vervollständigt.
- Rewind-Historie auf schnelle Brotli-Kompression umgestellt und eine öffentliche Metrik für tatsächlich gehaltene Bytes ergänzt; Anzahl und Zeitfenster bleiben strikt begrenzt.
- Byte-identischen 300-Frame-Restore/Replay-Langlauftest sowie neue STOP-, DMA-, PPU-Prioritäts-, Renderer-Allokations-, APU-Sequencer-, Serial- und Rewind-Speichertests ergänzt.
- Gesamtsuite auf **107 Tests** erweitert: 81 Core-, 17 Runtime- und 9 Windows-Smoke-Tests; CI-Mindestzahlen entsprechend angehoben.

### Weiterhin offen

- CPU-Instruktionen und allgemeine Buszugriffe sind noch nicht mikrozyklusgenau; CGB-OAM-Buskonflikte und CPU-Stalls bei General-/HBlank-DMA sind angenähert.
- Die PPU verwendet noch keinen echten Pixel-FIFO mit variabler Mode-3-Dauer; APU-DAC-Power, Frequenz-Timer und mehrere Register-Nebenwirkungen benötigen weitere Präzisierung.
- Der Headless-Runner stellt die lokale Conformance-Infrastruktur bereit, enthält aber bewusst keine fremden Test-ROMs. TCP-Link-Kabel und serielles Bit-Timing bleiben offen.
- Save States aus Komponentenschema 1 werden wegen der neuen deterministischen Felder bewusst abgelehnt; eine automatische Migration ist nicht vorhanden.

## 4.4.0-alpha.1 – Phase 4 (2026-08-09)

### Vollständige Save States

- Sämtliche für eine deterministische Fortsetzung relevanten Zustände von CPU, Scheduler, Hauptspeicher, Timer, Interrupts, Video, Audio, Cartridge, DMA, Joypad und Serial in einzeln versionierten `AETHSTAT`-Pflichtsektionen erfasst.
- CPU-Transienten wie EI-Verzögerung, HALT-Bug und Double-Speed-Phase sowie PPU-Arbeits-/Ausgabeframes, APU-Oszillatorphasen, Sample-Akkumulator und noch nicht ausgegebene Samples aufgenommen.
- Boot-ROM-Nutzung per Länge und SHA-256 gebunden, ohne Firmware- oder ROM-Inhalte in Zustandsdateien einzubetten.
- Alle Abschnitte, Mapperregister und Wertebereiche werden vorbereitet und validiert, bevor die erste Komponente verändert wird; falsche ROMs, beschädigte Dateien und semantisch ungültige Payloads lassen die laufende Sitzung unverändert.
- Zustandsdateien besitzen harte Größenlimits, eine Dokument-SHA-256 und werden über eine temporäre Datei atomar ersetzt.

### Runtime und Bedienung

- Capture, Restore und Rewind als typisierte FIFO-Befehle auf dem exklusiven Emulations-Owner-Thread ergänzt.
- Nach Reset, Restore oder Rewind alte Audioereignisse verworfen, Frame-Pacing zurückgesetzt und eine neue monotone Bildgeneration veröffentlicht.
- Fünf Save-Slots neben der ROM (`.ss1` bis `.ss5`), F5/F8 und XInput-Schultertasten aktiviert; Datei-I/O blockiert weder UI noch Emulations-Thread.
- Rewind auf denselben vollständigen Zustandsvertrag migriert, alle vier Frames erfasst und strikt auf 150 Zustände beziehungsweise ungefähr zehn Sekunden begrenzt.
- Das Hauptfenster schreibt beim Start keine unveränderten Einstellungen mehr; fehlende Schreibrechte für die UI-Konfiguration verhindern nicht länger den Programmstart.

### Tests und CI

- Byte-identische Capture/Restore/Capture-Roundtrips und identische zukünftige Ausführung nach Restore verifiziert.
- Falsche ROM, neu signierte aber semantisch ungültige Komponenten, atomare Dateiablage, Rewind-Timeline und Rewind-Kapazität als Regressionstests ergänzt.
- Owner-Thread-Zugriff, defensive Pufferkopien, Frame-Neuveröffentlichung, Audio-Generationswechsel und aktivierte WinForms-Menüs abgesichert.
- Gesamtsuite auf **94 Tests** erweitert: 68 Core-, 17 Runtime- und 9 Windows-Smoke-Tests; CI-Mindestzahlen entsprechend angehoben.

### Weiterhin offen

- Save States sind absichtlich an die exakte ROM und das jeweilige Komponentenschema gebunden; eine automatische Migration künftiger inkompatibler Schemata ist noch nicht vorhanden.
- Rewind ist ein speicherresidenter Sitzungspuffer ohne Kompression oder Vorschau-Timeline.
- Link-Kabel, Game Genie, Spezialmapper und die in Phase 3 genannten Timing-/Hardware-Randfälle bleiben offen.

## 4.3.0-alpha.1 – Phase 3 (2026-08-09)

### Cartridge und Mapper

- ROM-Header auf Mindestlänge, deklarierte ROM-Größe, moderne RAM-Größen und Sondercodes für 72/80/96 ROM-Bänke validiert.
- CGB-Titelfeld korrekt begrenzt und jede geladene ROM über eine stabile SHA-256-Identität gebunden.
- Gemeinsamen Cartridge-Vertrag mit defensiven Mapper-Zuständen, begrenztem RAM und explizitem Flush/Dispose eingeführt.
- MBC1-Banking, RAM-Freigabe und beide Banking-Modi korrigiert; verbotene Banknummern werden hardwaregerecht umgebogen.
- MBC2 mit adressbitgesteuerten Registern, 512×4-Bit-RAM und Spiegelung implementiert.
- MBC3-Banking und RTC-Register mit Latch, Halt, 512-Tage-Carry, injizierbarer Zeitquelle und atomarer RTC-Persistenz implementiert.
- MBC5 mit vollständiger 9-Bit-ROM-Bank, Bank 0, bis zu 16 RAM-Bänken und separater Rumble-Maske implementiert.
- Battery-RAM wird im Speicher geändert und an Lebenszyklusgrenzen atomar ersetzt, statt bei jedem Byte ein Datei-Handle zu öffnen.

### PPU, DMA und APU

- STAT als gemeinsame, flankengesteuerte Interruptleitung für LYC sowie Modi 0/1/2 modelliert; schreibgeschützte Statusbits können nicht mehr überschrieben werden.
- LCD-Abschaltung setzt LY/Modus unmittelbar zurück; CPU-Zugriffe auf VRAM und OAM beachten die gesperrten PPU-Modi.
- Fenster außerhalb des sichtbaren Bereichs, CGB-Paletten-Autoinkrement, Zehn-Sprites-Limit, DMG-Sprite-Reihenfolge und vertikal gespiegelte 8×16-Sprites korrigiert.
- OAM-DMA auf alle 160 Bytes korrigiert.
- CGB-General- und HBlank-DMA kopieren fortlaufende 16-Byte-Blöcke, aktualisieren Quell-/Zielregister und bilden Abschluss sowie Abbruch in FF55 ab.
- NR41–NR44 sind ohne Exception lesbar und bewahren die beschreibbaren Noise-Felder.

### Deterministischer Zustandsvertrag

- Neues `AETHSTAT`-Format mit Versions- und Mindestleserversion, ROM-SHA-256, DMG/CGB-Modell und einzeln versionierten Pflichtsektionen entworfen.
- Deterministische Abschnittsreihenfolge, Duplikatprüfung, Vorwärtskompatibilität für optionale Sektionen, harte Größenlimits und SHA-256-Integritätsprüfung implementiert.
- Mapper-Zustände besitzen einen eigenen begrenzten Binärcodec und können verlustfrei in echte Mapper zurückgespielt werden.
- Das historische unvollständige Save-State-v1 und Rewind bleiben bewusst deaktiviert; Phase 4 liefert vollständige Payloads für alle Pflichtsektionen.

### Tests und CI

- Mapper-, ROM-Header-, RTC-, DMA-, STAT-, Speicherzugriffs- und Zustandsvertrag-Regressionstests ergänzt.
- Zweites vollständig generiertes Test-ROM führt OAM-DMA über CPU, Loader und Bus bis zum letzten OAM-Byte aus.
- Gesamtsuite auf **83 Tests** erweitert; Windows- und Linux-Mindesttestzahlen entsprechend angehoben.

### Weiterhin offen

- T-Zyklus-genaue OAM-DMA-Buskonflikte, vollständiger STOP-Ruhemodus, PPU-FIFO/CGB-Pixelpriorität und APU-Frame-Sequencer.
- MBC1M, MBC4, MMM01, Kamera- und HuC-Spezialhardware.
- Vollständige Save-State-Payloads, Rewind und Link-Kabel bleiben deaktiviert.

## 4.2.0-alpha.1 – Phase 2 (2026-08-01)

### Architektur und Plattformgrenzen

- Den Emulator als eigenständiges `AetherBoy.Core`-Projekt auf plattformneutrales `net10.0` ausgelagert.
- Windows Forms, `System.Drawing`, XInput und NAudio vollständig aus der Core-Assembly entfernt.
- Host-Tastencodes durch die logische `[Flags]`-Eingabemaske `GameBoyButtons` ersetzt.
- Persistente UI-Einstellungen von der unveränderlichen Core-Konfiguration `EmulatorConfiguration` getrennt.
- DMG-Paletten ohne `System.Drawing.Color` als bitidentische `0xAARRGGBB`-Werte abgebildet.
- NAudio als reinen Windows-Ausgabeadapter außerhalb des Cores neu angebunden.

### Owner-Thread und Oberfläche

- `AetherBoy.Runtime` mit einem exklusiven Owner-Thread eingeführt: Konstruktion, Frames, Eingaben, Einstellungen, Cheats, Snapshots und Disposal laufen auf genau diesem Thread.
- Typisierte, geordnete Commands für Eingaben, Konfiguration, Palette, Pause, Turbo, Reset, Cheats und Shutdown ergänzt; endliche Batches verhindern Command-Starvation.
- ROM-, Audio-, Cheat- und Wave-RAM-Snapshots werden unveränderlich und defensiv kopiert veröffentlicht.
- Zwei dauerhaft vorallokierte Frame-Puffer ersetzen die frühere Kopie pro Emulationsframe; ein synchronisierter Austausch liefert der Oberfläche nur vollständige Bilder ohne laufende Large-Object-Heap-Allokationen.
- WinForms greift nicht mehr direkt auf `Nanoboy`, `CPU`, `Memory`, `Video`, `Audio` oder mutable Cheat-Listen zu.
- Tastatur und Gamepad werden als unabhängige Zustände zusammengeführt; ein Gerät kann eine vom anderen gehaltene Taste nicht mehr freigeben.
- Audio-Inspector und WAV-Aufnahme auf Session-Snapshots beziehungsweise kopierte Sampleblöcke umgestellt.
- Externe Audio-Consumer über einen begrenzten Hintergrundkanal vom Emulations-Owner-Thread entkoppelt; langsame Consumer können weder Frames noch Shutdown blockieren.
- WAV-Schreiben und Header-Finalisierung gegen gleichzeitige Zugriffe synchronisiert, Fehlerpfade schließen Handles zuverlässig und Datei-I/O blockiert weder Oberfläche noch Emulations-Thread.
- Geordneter, idempotenter Shutdown entsorgt den Core ausschließlich auf seinem Owner-Thread und meldet Fehler über `Completion`, `Fault` und den Sessionstatus.

### Tests und CI

- 15 deterministische Runtime-Tests für Threadbesitz, FIFO, Pause, Frame-Austausch, begrenzten Audio-Dispatch, Commands, WAV, Shutdown und Fehlerpfade ergänzt.
- Einen vollständig selbst erzeugten 32-KiB-Test-ROM durch Loader, CPU, Scheduler und WRAM ausgeführt; keine fremden ROM-Daten werden eingecheckt.
- Architekturtests verhindern neue WinForms-, Drawing- oder NAudio-Referenzen im Core.
- Joypad-Gesamtzustand, ARGB-Paletten und parallele WAV-Finalisierung mit Regressionstests abgesichert.
- Gesamtsuite auf **59 Tests** erweitert.
- GitHub Actions um ein Linux-Gate für Core und Runtime, feste Mindesttestzahlen sowie die .NET-10-Testsyntax `--solution`/`--project` ergänzt.

### Weiterhin offen

- T-Zyklus-genaue Buszugriffe, vollständiger STOP-Ruhemodus sowie breitere PPU-, DMA-, Mapper- und APU-Conformance.
- Save States, Rewind und Link-Kabel bleiben deaktiviert, bis ihr Zustands- und Timingmodell neu aufgebaut und ROM-basiert verifiziert ist.
- Game Genie bleibt deaktiviert; GameShark-RAM-Writes bleiben experimentell.

## 4.1.0-alpha.1 – Phase 1 (2026-07-31)

### Plattform und Build

- Zielplattform von .NET 8 auf **.NET 10 LTS** aktualisiert und SDK `10.0.302` über `global.json` festgelegt.
- OpenTK 3 und GLControl vollständig entfernt; damit entfällt der .NET-Framework-Kompatibilitätsfallback.
- NAudio auf das kleinere Laufzeitpaket `NAudio.WinMM 2.3.0` aktualisiert.
- Reproduzierbaren Restore über `NuGet.config` und drei `packages.lock.json` eingeführt.
- GitHub Actions mit eingeschränkten Rechten, gepinnten Action-SHAs, Release-Build und Tests ergänzt.
- Dependabot für NuGet- und Actions-Aktualisierungen eingerichtet.

### Core und Timing

- Zentrale Hardwaretaktdaten mit 4.194.304 Hz, 456 Dots pro Scanline und 70.224 Dots pro Frame eingeführt.
- Normal- und Double-Speed-Taktdomänen getrennt; PPU/APU laufen nicht mehr mit doppelter Framezahl.
- Interrupts werden an Instruktionsgrenzen priorisiert, benötigen 20 CPU-Ticks und wecken HALT auch bei gelöschtem IME.
- EI-Verzögerung, DI-Abbruch, RETI, HALT-Bug und CGB-STOP-Speed-Toggle korrigiert.
- Timer auf einen 16-Bit-Divider mit TAC-Falling-Edges und verzögertem TIMA-Reload umgestellt.
- Driftarmes Frame-Pacing auf die tatsächlichen rund 59,7275 Hz statt pauschaler 16 ms umgestellt.

### Bild, Eingabe und Audio

- OpenGL-Immediate-Mode durch einen verwalteten WinForms-Renderer mit Seitenverhältnis, Letterboxing und drei Filtern ersetzt.
- Framebuffer als kopierten, sequenzierten Snapshot veröffentlicht; das frühere GCHandle-Leak und gleichzeitiges Lesen/Schreiben entfallen.
- Gamepad-Eingabe über die Windows-Systemkomponente XInput angebunden; Disconnect setzt Controllerzustände zurück.
- Audio-Sampling auf einen rationalen 4.194.304-Hz-Akkumulator umgestellt.
- Puffergröße von versehentlichen 1025 auf exakt 1024 Samples korrigiert und NAudio-Queue begrenzt.
- Pulse-, Wave- und Noise-Längenzähler sowie Noise-LFSR-Periode korrigiert.
- Headless-Audiobackend für deterministische Tests ergänzt; veralteten OpenAL-/Thread-Abort-Code entfernt.

### Tests

- Den bisherigen Console-Smoke-Test in ein echtes MSTest-/Microsoft-Testing-Platform-Projekt umgewandelt.
- Neues Core-Testprojekt für Hardwaretakt, Timerfrequenzen und -overflow, Interruptpriorität, HALT-Wakeup und -Bug, EI/DI/STOP, PPU-Framegrenzen sowie Audio-Samplezahl und Kanal-Längen ergänzt.

### Weiterhin offen

- T-Zyklus-genaue Buszugriffe, vollständiger STOP-Ruhemodus, vollständige PPU/STAT-/DMA-Prioritäten und APU-Frame-Sequencer.
- Plattformneutrale Trennung des Emulator-Cores vom Windows-Frontend.
- Vollständiger Owner-Thread/Command-Queue-Vertrag für alle UI-Mutationen.

## 4.0.0-alpha.1 – Phase 0 (2026-07-31)

### Geändert

- Projektstatus auf **Alpha / experimentell** korrigiert; die Bezeichnung „High-End Edition“ wurde entfernt.
- Produkt-, Assembly- und Dateiversion zentral auf `4.0.0-alpha.1` beziehungsweise `4.0.0.0` vereinheitlicht.
- Alte ChiiBoy-Produktmetadaten aus `AssemblyInfo.cs` entfernt; Urheberschaft wird weiterhin dokumentiert.
- Visual-Studio-Lösung auf aktuelle Metadaten bereinigt und die verwaiste `Debugger`-Konfiguration entfernt.
- README, GPLv3-Lizenz, Drittanbieterhinweise, `.gitignore`, `.gitattributes` und `.editorconfig` ergänzt.
- Kommerzielle ROMs und der persönliche Spielstand aus dem veröffentlichbaren Quellbaum entfernt und in ein ignoriertes lokales Archiv verschoben.
- Alte Build-, Release-, IDE- und Paketkopien entfernt.
- Crashprotokolle werden nicht mehr ins Arbeitsverzeichnis geschrieben, sondern unter `%LOCALAPPDATA%\AetherBoy\Logs` abgelegt.
- Das In-App-Changelog liest nun dieselbe `CHANGELOG.md`, die auch im Repository liegt.

### Repariert

- Den unvollständigen Audio-Inspector-Designer rekonstruiert.
- Vier Pegelanzeigen, Wellenformanzeige und WAV-Aufnahmebutton werden wieder erzeugt und ins Layout eingebunden.
- Der Aktualisieren-Schalter verwendet nun seinen `Checked`- statt seines `Enabled`-Zustands.
- Ein automatisierter Smoke-Test prüft Initialisierung und Aktualisierungsschalter des Audio Inspectors.
- Veraltete und inkonsistente Fenstertitel durch eine zentrale Produkt-/Versionsanzeige ersetzt.

### Vorläufig deaktiviert

- Save States und Save-Slots: Das bisherige Binärformat ist inkonsistent und bildet keinen vollständigen deterministischen Zustand ab.
- Rewind: Verwendet denselben fehlerhaften Save-State-Unterbau.
- Link-Kabel/Netplay: Die TCP-Oberfläche ist noch nicht mit der emulierten seriellen Hardware verbunden.
- Game Genie: Codes werden noch nicht im ROM-Lesepfad angewendet.

GameShark-RAM-Codes bleiben als **experimentelle** Funktion sichtbar. Das ist keine Aussage über Timing- oder Thread-Sicherheit.

### Bekannte technische Schulden

- Kein automatisiertes Testprojekt und keine Hardware-Conformance-Gates.
- CPU-, Timer-, DMA-, Mapper-, PPU- und APU-Timing benötigen Korrekturen.
- OpenTK 3.x wird unter .NET 8 nur über NuGets .NET-Framework-Kompatibilitätsfallback eingebunden.
- Der OpenAL-Backendcode verwendet noch nicht unterstützte Thread-Abbruchlogik.
- Der Emulator-Core besitzt noch keinen einzelnen Owner-Thread mit Command-Queue und sicheren Snapshots.

## Historie

### ChiiBoy Color 3.2.2 (2021/2022)

- Oberflächen- und Branding-Anpassungen.
- OpenTK-GLControl-Renderer und Audio-Werkzeuge.

### nanoboy 1.0.0 (2014)

- Ursprünglicher kompakter Game-Boy-Color-Emulator von Frédéric Meyer.
- LR35902-Core, grundlegende Mapper-, Video- und Audioimplementierung.
- Projektchronik und Altmetadaten nennen GNU GPLv3 als Lizenz.

## Nächster Meilenstein

Nach Phase 6 folgt die Release-Härtung: breitere, lokal bereitgestellte Conformance-Suites, echte Pixel-FIFO- und Bus-Mikrozyklen, APU-Analogeffekte sowie Praxisvalidierung auf unterstützten Windows-Systemen.
