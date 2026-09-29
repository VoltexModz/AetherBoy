# GBA-Startup-Fix und erweiterte Interrupt-Prüfung

Stand: 29. September 2026. Folgt auf den [Quellenvergleich](LINK_REFERENCE_COMPARISON_2026-09-29.md). Kein echter Pokémon-Tausch wird durch diesen Bericht behauptet.

## Implementiert

`PokemonGen3SerialAdapter` unterscheidet jetzt die zuletzt **lokal ausgelieferte** Handshake-Antwort von einem Reset der entfernten Netzwerkphase:

- Ein Peer-Reset vor Etablierung verwirft weiterhin alte Peer-Bereitschaft, bricht ein aktives serielles Wort ab und verlangt frische Metadaten. Er löscht aber nicht mehr, was das lokale Spiel schon als Teilnehmerzahl beobachtet hat.
- Ein echter lokaler SIO-Austritt löscht diese lokale Historie weiterhin.
- Eine tatsächlich abgeschlossene Antwort mit fehlendem oder ungültigem Teilnehmer löscht die lokale Readiness ebenfalls.
- Das Mastertoken etabliert den Adapter erst bei zwei aufeinanderfolgenden lokal gültigen Teilnehmerbeobachtungen. Der frühere unbegrenzt hochgezählte Rundenzähler wurde durch die dafür ausreichende vorherige Readiness ersetzt.
- Bei einem bereits etablierten Abschnitt bleibt der zuvor ergänzte ausstehende Phasenwechsel erhalten: Ein Peer-Reset startet das lokale Spiel nicht vorzeitig neu.

Keine neue Paketversion, keine künstlichen Spielkommandos, keine Lockerung von Rollen-, Phasen-, Sequenz- oder Queueprüfungen. Windows und Linux verwenden denselben korrigierten Runtime-Code.

## Vorher-/Nachher-Nachweis

`GbaGen3StartupReferenceTests` enthält jetzt 20 Fälle und keinen grünen Test mehr, der den alten Fehler als erwartetes Ergebnis festschreibt.

Vor dem Produktionsfix: **6 fehlgeschlagen, 14 bestanden**. Die Fehlschläge betreffen beide Rollen nach einer oder zwei schon beobachteten Antworten sowie Peer-Reset während eines folgenden aktiven Wortes. Danach bestanden alle 20 mit dem gewünschten Invariant; der erste Acht-Wort-Befehl bleibt unverändert und erscheint genau einmal.

Zusätzlich abgedeckt: Reset vor der ersten Antwort, echtes Disable/Enable, veraltete Peer-Metadaten, fehlender Partner, ungültiges lokales Terminal, kein erfundener IRQ für abgebrochene Wörter und Reset nach dem Mastertoken. Die vorhandenen Mehrabschnittstests prüfen weiterhin Abschluss, verspätete letzte Kommandos und drei Abschnitte mit Wiederbetreten.

TRX-Verzeichnisse unter `artifacts/link-startup-fix-20260929/`:

- `before/`: 20 Startup-Fälle, alter Fehler gezielt rot nachgewiesen.
- `after/`: 67 Startup-/Adapter-/Abschnittsfälle nach der Korrektur bestanden.
- `linux-wsl-complete/`: 80 gezielte GBA-Fälle auf der vorhandenen Linux-Laufzeit bestanden.
- `core-windows/`: 242 Core-Fälle bestanden.
- `windows-online/`: 25 Windows-Online-UI-Fälle bestanden.
- `runtime-windows-final/`: 447 Fälle, 446 bestanden, ein POSIX-Symlink-Test unter Windows übersprungen, keine Fehler. Die vier nativen lokalen Verbindungsfälle wurden ausgeführt, nicht übersprungen.

Die 80 Linux-Fälle sind zusätzliche Ausführungen derselben plattformneutralen GBA-Prüfungen, nicht 80 neue unterschiedliche Testdefinitionen. Die vollständige Linux-Runtime-/Frontend-Suite wurde hier nicht ausgeführt.

## Neuer eigener ARM-IRQ-Test

`GbaGen3SyntheticRom` kann zusätzlich zum bisherigen IF-Polling ein selbst geschriebenes ARM-Testprogramm mit BIOS-dispatchten Interrupts erzeugen. Ein echter Timer-3-IRQ startet den nächsten Hosttransfer; der Serial-IRQ bestätigt die fertigen Empfangsregister; VBlank-IRQs werden zusätzlich verarbeitet. Beide Seiten senden drei unterscheidbare vollständige Kommandos und anschließend Null-Leerlauf. Alle drei Gegenbefehle werden geordnet im synthetischen SRAM abgelegt.

`GbaGen3IrqLifecycleTests` prüft vier Varianten: keine Verzögerung, zwei oder sieben emulierte Frames Paketverzögerung sowie sieben Frames Verzögerung mit drei Frames später startendem Gast. Dazu kommen zwei neue Varianten im unabhängigen Owner-Thread-Test, darunter 100 ms künstliche Verzögerung mit Jitter. Nach dem Stop werden die synthetischen Arbeits-Saves gelesen; die ursprünglichen Test-Saves müssen unverändert bleiben.

Das ist mehr als IF-Polling, aber bewusst **kein vollständiger Nachbau von FireReds Ereigniskette**: Die VBlank-IRQ-Erfassung erzwingt hier nicht die genaue spielseitige Neun-Wort-/Frame-Regel, und dieser ARM-Test führt selbst keinen Kabelraum-Wiedereintritt aus. Wiederbetreten wird bislang durch den gesonderten Adapter-/Controller-Treiber geprüft. Echte Spiele und ihre Speicherregeln bleiben eine weitere Abnahme.

## Plattformgrenzen

Die ausgewählten 80 GBA-Fälle liefen auch auf Ubuntu 26.04 / WSL2 x64 mit .NET 10.0.12. Die portable Testassembly wurde auf Windows mit SDK 10.0.302 gebaut. Wegen fehlendem ICU wurde ausschließlich für diese Linux-Testprozesse der invariante Globalisierungsmodus gesetzt. Das ist kein nativer Linux-Desktop- oder Linux-WebRTC-Nachweis.

Der Windows-Release-Build wurde aktualisiert, einschließlich OnlineProbe. Der erste Gesamtbuild benötigte eine Wiederherstellung der fehlenden `project.assets.json` der OnlineProbe; die bestehenden Lockfiles blieben unverändert. Anschließend: **0 Fehler, 0 Warnungen**.

Alle echten Rechnerpaarungen und beide Host-/Gastrollen sind in der [verbindlichen Plattformmatrix](LINK_PLATFORM_VALIDATION_2026-09-29.md) aufgeführt. Windows-Loopback und Linux-Modelltests werden dort ausdrücklich nicht als Windows ↔ Linux oder WAN-Erfolg ausgegeben. Keine Serverzugänge verwendet, keine echten ROMs oder Spielstände angefasst. Die hier dokumentierten Prüfungen erfolgten vor dem Commit; die Übergabe beschreibt [dieser Überblick](LINK_CABLE_COMMIT_HANDOFF_2026-09-29.md).
