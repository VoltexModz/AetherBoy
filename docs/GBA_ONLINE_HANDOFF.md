# GBA Gen3 Online – Entwicklungsübergabe / development handoff

Stand: 12. September 2026. Gemeinsamer Entwicklungsstand; **Commit und Push auf
`development` wurden nach der Ergebnisübergabe ausdrücklich freigegeben**.
Diese Git-Freigabe ist keine Freigabe als geprüfter Pokémon-Tauschrelease.
Dieses Dokument ergänzt die [Recherche](GBA_ONLINE_RESEARCH.md) und den historischen
[GB/GBC-Online-Meilenstein](ONLINE_LINK_HANDOFF.md).

## Deutsch

### Ergebnis und ehrliche Grenze

Ein eigener Zweispieler-Pokémon-Gen3-Protokolladapter ist jetzt an den bestehenden
GBA-Kern, die gemeinsame Runtime und beide Frontends angeschlossen. Jeder Rechner
emuliert nur sein eigenes Spiel. Die vorhandene Browser-WebRTC-Brücke bleibt der
verschlüsselte Transport; weder komplette ROMs noch Spielstanddateien werden versendet.
Spielbedingte Kabeldaten können selbstverständlich Trainer-/Pokémon-Informationen enthalten.

**Entwicklungsprofil, kein geprüfter Pokémon-Tauschrelease.** Synthetische Programme,
Registerfolgen und Pakettests belegen technische Eigenschaften, nicht einen erfolgreich
gespeicherten Tausch in einer Originaledition. GBA-Singleplayer und GBA-Link/Online sind
unterschiedliche Reifegrade; ein laufendes Singleplayer-Spiel beweist keinen funktionierenden
Linkmodus. Das neue Profil ist kein universelles GBA-Internetkabel, kein Wireless Adapter,
kein GB↔GBA-Tausch und kein Vier-Spieler-Modus.

### Aufteilung und Herkunft

- GBA-Anschluss: `ISerialPeer` trennt den seriellen Controller von der Gegenstelle.
  `LocalSerialLink` bleibt für das lokale Zweierpaar zuständig.
- Gen3: `PokemonGen3SerialAdapter` ist eine eigene C#-Implementierung des begrenzten
  Spielprotokolls. Die Quellen und bewussten Näherungen stehen in der Recherche.
  Kein gpSP-Code wurde übersetzt/kopiert, kein fremder Code durch Umbenennen als eigene
  Herkunft ausgegeben. Das ist keine Behauptung einer formalen Clean-Room-Entwicklung.
- Gemeinsame Sitzung: `GbaOnlineLinkMachine`/`GbaOnlineLinkCoordinator` verbinden
  den vorhandenen GBADotnet-abgeleiteten Kern mit dem Transport. Dessen bestehende
  MIT-Lizenz und Herkunftshinweise bleiben erhalten.
- Windows/Wayland: identisches Profil und gleicher Spielstandschutz, jeweils native
  Darstellung und Bedienung. Die Windows-Änderungen ersetzen nicht die Linux-Arbeit des
  Kollegen. [Sein Lieferstand und unsere Integration](LINUX_UPSTREAM_INTEGRATION_REVIEW.md).
- Schutz: `OnlineSaveWorkspace` und `OnlineSaveRecovery` werden auch vom bisherigen
  GB/GBC-Onlinepfad verwendet. Alte Sitzungen ohne Journal bleiben manuell erreichbar;
  sie werden nicht nachträglich als sicher bestätigt.

### Welche GBA-Dateien der Entwicklungseinstieg erkennt

Die Freigabe beruht auf vollständigen, veröffentlichten ROM-Prüfsummen plus Spielkennung
und Revision, nicht auf Dateinamen oder einer unveränderten Kennung im ROM-Hack.
Der zusätzliche lokale SHA-256 bindet den geprüften Inhalt an den tatsächlich geladenen
Inhalt. SHA-1 dient ausschließlich dem Abgleich historisch veröffentlichter Prüfsummen,
nicht der Netzwerkauthentifizierung. Keine ROMs wurden dafür heruntergeladen.

| Entwicklungs-Kandidaten | Sprache | Revisionen |
| --- | --- | --- |
| Ruby / Sapphire | Englisch | jeweils 0, 1, 2 |
| Rubin / Saphir | Deutsch | jeweils 0, 1 |
| FireRed / LeafGreen | Englisch | jeweils 0, 1 |
| Emerald | Englisch | 0 |

Das sind **15 erkannte Fassungen, keine 15 erfolgreich getesteten Spiele**. Die Liste
bestätigter Tauschfassungen ist zunächst leer. Alle Kandidaten verlangen eine ausdrückliche
Entwicklungsbestätigung. Andere Regionen/Revisionen, deutsche Feuerrot/Blattgrün/Smaragd
und Hacks wie Rocket Edition werden nicht automatisch zugelassen. Zwei unterschiedliche
Kandidaten dürfen dasselbe Gen3-Profil nutzen; unterschiedliche Editionen müssen gerade
nicht dieselbe ROM-Prüfsumme besitzen. Ob eine Paarung im Spiel tatsächlich freigeschaltet
und kompatibel ist, bleibt separat zu prüfen.

Primärquellen der Kennungen: [pret/pokeruby](https://github.com/pret/pokeruby/tree/63a8cbf0016b351a4e68f7036fa0b77e23d2f2c1)
(`ruby*.sha1`, `sapphire*.sha1`),
[pret/pokefirered](https://github.com/pret/pokefirered/tree/c75f352304d529f6ba92d4f74b9cf8b5c3810788)
(`firered*.sha1`, `leafgreen*.sha1`) und
[pret/pokeemerald/rom.sha1](https://github.com/pret/pokeemerald/blob/5eff78649e7170a877b961ef0b3da13b81a16038/rom.sha1).
Die konkrete Zuordnung steht in [GbaOnlineProfileCatalog](../nanoboy/Runtime/Netplay/GbaOnlineProfileCatalog.cs).

### Bedienung

1. Beide verwenden denselben neuen AetherBoy-Stand und öffnen jeweils ihre eigene
   erkannte Originalfassung. Zum Entwickeln nur entbehrliche Spielstandkopien verwenden.
2. Windows: **Tools → Online Link**, Sitzung erstellen oder beitreten. Linux: **F10**
   beziehungsweise **Tools → Online Link**, dann Host/Gast. Die GBA-Entwicklungswarnung
   lesen und ausdrücklich bestätigen; genau einen Host und einen Gast wählen.
3. Beide Spiele starten mit einer neuen privaten Sitzungskopie. Der Host erstellt im
   lokalen Browserhelfer eine Einladung. Der Gast übernimmt sie und erstellt eine Antwort;
   der Host übernimmt diese. Nur Einladung/Antwort privat austauschen, niemals die lokale
   Browseradresse oder deren Geheimnis. Beide Browser geöffnet lassen.
4. Zum Emulator zurückkehren und die für eure Editionen vorgesehenen **Kabelräume** im
   Spiel betreten. Kein Funkraum. GBA-Bildgröße bleibt 240×160; L/R funktionieren über die
   vorhandenen Eingabebelegungen. Der aktuelle GBA-Onlinepfad startet mit **HLE BIOS**;
   eine eigene Voll-BIOS-Auswahl wird in diesem Profil noch nicht übernommen.
5. Beidseitige Pause hält die Emulation an, während die Verbindung weiter bearbeitet
   wird. Turbo, Reset, Rewind, einseitige States und Cheats bleiben online gesperrt.
6. Nach dem Speichern das Spiel kontrolliert beenden. Ein sauberer technischer Abschluss
   ist ausdrücklich **kein Tauschbeweis**. Bei fehlenden Bestätigungen oder noch offenen
   Nutzdaten wird die Sitzung als ungeklärt behandelt.
7. Unter **Online-Spielstände prüfen / Session copies** die Kopien des gewählten Spiels
   ansehen. Prüft auf beiden Rechnern separate Kopien nach einem Neustart. Die protokollierten
   Archivdateien nicht direkt neu starten/verändern: neue RTC-/Backup-Schreibzugriffe würden
   die gespeicherten Prüfsummen verändern und die automatische Freigabe zu Recht sperren.
8. Erst nach dieser Prüfung bewusst **Übernehmen / Adopt** bestätigen. Vorheriger Stand
   und Fortsetzen-Dateien bleiben wiederherstellbar archiviert. Alte manuelle Save States
   werden nicht gelöscht; ihr Laden kann den übernommenen Fortschritt zurückdrehen.

Die Browserseite ist eine vorläufige Verbindungsoberfläche, keine native Lobby.
Eigene STUN/TURN-Server lassen sich dort ausdrücklich konfigurieren; standardmäßig wird
kein solcher Dienst kontaktiert. Direkte P2P-Verbindungen hinter beliebigen Routern sind
nicht garantiert. Ein verwalteter Relaydienst, Zugangsdaten, Kosten oder Firewalländerungen
wurden nicht eingerichtet. [Transport und Datenschutz](ONLINE_LINK_HANDOFF.md#internet-router-und-sicherheit).

### Spielstandjournal und Wiederherstellung

Sitzungspfade bleiben:

- Windows: `%LOCALAPPDATA%/AetherBoy/development/OnlineLink/<Sitzung>/`
- Linux: `${XDG_STATE_HOME:-~/.local/state}/aetherboy/online-link/<Sitzung>/`

`game.sav` ist die private Batterie; `.rtc`, drei Backupgenerationen und Integritätsdateien
werden als Familie behandelt. `online-session.json` enthält Version, Profil, lokale
Originalzuordnung, Zeitpunkte und Datei-Prüfsummen. Keine Rohpakete, ROM-Inhalte, WebRTC-
Einladungen oder TURN-Geheimnisse gehören in dieses Journal. **Es enthält lokale Pfade
und ist privat; vor Weitergabe prüfen.** Die Existenz einer `.lock`-/Markerdatei bedeutet
nicht allein, dass die Sitzung noch aktiv ist; entscheidend ist die tatsächlich gehaltene
exklusive Dateisperre.

Die Übernahme prüft die zur ausgewählten ROM gehörende Zieladresse, den freien
Sitzungs-/Original-Lock, gültiges begrenztes Journal, vollständige Dateifingerprints,
Integritätsdaten, unveränderte Originale und den letzten bestätigten privaten Flush.
Aktive, abgebrochene, fehlerhafte, veränderte oder bereits übernommene Sitzungen werden
nicht einfach erneut importiert. Symbolische Links/Junctions im Speicherpfad werden
konservativ abgewiesen. Die Prüfung erstellt keine fremden Pfade aus Journalangaben.

Vor dem Schreiben wird die komplette alte Familie in `original-before-import-<GUID>/`
gesichert und nachgeprüft. Alte Fortsetzen-Dateien liegen in `resume-before-import-<GUID>/`.
Die Hauptdateien verwenden den vorhandenen atomaren Writer mit rotierenden Backups.
**Batterie und RTC sind keine gemeinsame Dateisystemtransaktion:** Ein Absturz zwischen
beiden Schreibvorgängen hinterlässt `Promoting` und verlangt manuelle Prüfung anhand der
vollständigen vorherigen Sicherung. Es gibt keine automatische Wiederholung, keine
automatische Wiederherstellung über ein möglicherweise inzwischen verändertes Original
und kein Versprechen gleichzeitiger Speicherung auf beiden PCs. Ein fremdes Programm,
das unsere Dateisperren ignoriert, ist keine gemeinsam kontrollierte Schreibinstanz.

### Protokoll und Grenzen für die Weiterentwicklung

- GB/GBC behält Wire-v1. GBA verwendet v2 mit festem Familien-/Gen3-Profil, Revision,
  Entwicklungszustimmung und entgegengesetzten Rollen im 32-Byte-Handshake.
- Gen3-Nachrichten verwenden einen 72-Byte-Umschlag, Sendernonce, monotone Sequenznummern,
  Phasenkennung, acht 16-Bit-Wörter und Inhaltsprüfsumme. Pause und Abschluss sind getrennte
  Kontrollnachrichten. Exakte Längen/reservierte Felder/Replay werden geprüft.
- Der Adapter beobachtet den realen `B9A0..B9A3`-/`8FFF`-Handshake und verarbeitet
  vollständige nichtleere Achtwortbefehle. Lokale Prüfsummen und Null-Leerlaufframes sind
  Protokollmechanik, keine erfundenen Tausch-/Speicherbestätigungen.
- Unterstützt ist ausschließlich das zweispielerige 115200-Baud-Multiplayerprofil.
  Normal-8/32-Accessory-Probes sehen eine unbelegte Leitung; ein Funkadapter wird nicht
  vorgetäuscht. Gast-Interruptabstände sind eine spielprotokollbezogene Näherung mit
  absoluten emulierten Zielzeiten, keine vollständige elektrische WAN-Kabelsimulation.
- Eingehende und ausgehende Adapterqueues sind begrenzt. Überlauf, ungültige Prüfsummen,
  Rollenwechsel, verlorene Phasenzuordnung und verspätete Nutzdaten nach Neustart brechen
  kontrolliert ab. Wichtige Befehle werden nicht still verworfen.
  Ein Reset der Gegenstelle mitten in einer bereits aufgebauten lokalen Verbindung wird
  ebenfalls konservativ abgewiesen; ein nahtloser Neuaufbau bei versetzten Spielphasen
  ist nicht zugesichert. Im Zweifel eine neue Sitzung starten.
- Netzwerkcallbacks schreiben keine GBA-Register. Die eigene Emulationsinstanz arbeitet
  in kurzen Abschnitten; Pause, Verbindungsverarbeitung und Beenden bleiben erreichbar.
  Audio-Generationen werden bei Netzwerk-Wartewechseln verworfen und native Ausgaben
  beim Stillstand geleert/angehalten, statt einen alten Block endlos zu wiederholen.
- Kontrolliertes Beenden verwendet Request/Ack/Receipt, eine Drei-Sekunden-Frist und
  eine kurze Nachlaufphase. Offene Nutzdaten verhindern einen sauberen Abschluss.
  Das ist weder ein garantierter Transport-Drain noch eine atomare Speichertransaktion
  auf beiden Rechnern. Windows wartet für diesen GBA-Onlinepfad bis zu fünf Sekunden;
  ein noch nicht beendeter Besitzer und seine Dateisperren werden nicht verworfen.

### Prüfstand

Abschließender lokaler Stand nach Integration der drei Fachagenten und der gemeinsamen
Spielstand-Wiederherstellung, 12. September 2026:

| Prüfung | Bestanden | Ausgelassen | Fehler |
| --- | ---: | ---: | ---: |
| Windows: Core | 232 | 0 | 0 |
| Windows: Runtime | 345 | 1 | 0 |
| Windows: native Smoke-Tests | 187 | 1 | 0 |
| Windows: Desktop-Suite | 80 | 39 | 0 |
| **Windows: gesamter Lösungslauf** | **844** | **41** | **0** |
| Ubuntu/WSL: Core | 232 | 0 | 0 |
| Ubuntu/WSL: aktuelle Runtime | 346 | 0 | 0 |
| Ubuntu/WSL: Desktop-Suite ohne Wayland-Sitzung | 82 | 37 | 0 |

Der Release-Build der gesamten Lösung hatte **null Warnungen und null Fehler**.
Der Windows-Lösungslauf umfasst 885 Fälle. Der Runtime-Skip betrifft einen POSIX-
Symlinktest; Windows verweigerte einem Tastatur-Smoke-Test die Vordergrundaktivierung.
Die übrigen Skips benötigen native Linux-/Wayland-/Unix-Bedingungen. Windows-
Hardwareproben waren eingeschaltet, ersetzen aber keinen Hör- oder Spieltest.
Ubuntu führte den plattformneutralen Code nativ unter .NET 10.0.12 aus, nicht unter Wine.
Mangels ICU wurde nur für diese Testprozesse der invariante Globalisierungsmodus gesetzt.
Eine native Wayland-Oberflächenabnahme wurde hier nicht durchgeführt.

Die Tests enthalten unter anderem echte synthetische ARM-Programme mit Serial-Interrupts
und SRAM-Ergebnisprüfung, 0 bis 100 ms simulierte Verzögerung je Richtung sowie einen
geordneten Jitterfall. Das ist kein Nachweis realer Paketverluste oder einer WAN-Verbindung.
Die synthetische Profilfreigabe ist ausschließlich ein interner Testzugang; unbekannte
Benutzer-ROMs werden dadurch nicht freigeschaltet.

**Echte WebRTC-Probe:** Zwei getrennte Emulationsprozesse, einer unter Windows und einer
unter Ubuntu/WSL, tauschten über echte Browser-DataChannels jeweils einen vollständigen
Achtwortbefehl aus. Beide synthetischen Original-Saves blieben unverändert. Ein kontrollierter
Stopp auf Hostseite beendete beide Besitzer mit `CleanStopped`; beide Archive waren
technisch übernehmbar, wurden aber nicht übernommen. Beide Browser liefen unter Windows
auf demselben physischen PC. Kein STUN/TURN, kein zweiter Rechner, kein Original-Pokémon.
Der Versuch wurde zweimal erfolgreich durchgeführt; die zweite Probe verwendete die
aktualisierten Entwicklungswarnungen im Browser. Die letzte anschließende Änderung am
Runtime-Lebenszyklus betrifft die Anzeige von `Stopping` während des finalen Flushs.

Lokale, von Git ausgeschlossene Nachweise:

- `TestResults/gba-online-final-*.trx`: vier Module des abschließenden Windows-Laufs.
- `tests/AetherBoy.RuntimeTests/bin/Release/net10.0/TestResults/gba-online-ubuntu-final.trx`:
  aktueller Ubuntu-Lauf mit 346 bestandenen Fällen.
- `tests/AetherBoy.DesktopTests/bin/Release/net10.0/TestResults/gba-online-desktop-ubuntu-min82.trx`:
  Ubuntu-Desktop-Logiklauf; 82 ausgeführte Fälle, 37 Skips.
- `artifacts/webrtc-probe/bin/Release/net10.0/cross-os-gba-02/host.json` und `guest.json`:
  bereinigte Verbindungs-/Speicherergebnisse; zugehörige Browserbilder liegen unter
  `artifacts/webrtc-probe/cross-os-gba-02-*.png`, ohne Verbindungsgeheimnisse.
- `artifacts/gba-online-validation/windows-ui-final/`: Windows-Oberflächen-Smoke-Captures.

Die CI-Untergrenzen sind auf 885 insgesamt, 346 Runtime und 119 Desktop aktualisiert.
Beim direkten Aufruf einer Test-DLL zählt `--minimum-expected-tests` in diesem Testhost
ausgeführte Fälle ohne Skips: Der erste Ubuntu-Desktop-Aufruf mit 119 verlangte zu viele
ausgeführte Fälle und endete mit Code 9, obwohl kein Test fehlschlug. Die Wiederholung
mit 82 bestand. Das war kein Wayland-/SDL-Absturz; der separate Headless-Wayland-Job
behält seine zu dessen aktivierten Bedingungen passende Grenze von 116.

### Offener Befund: sporadisch langsamer Speicherabschluss

**Update 03.10.2026:** Stacks aus tatsächlich fehlgeschlagenen Vollastläufen zeigen
Backup-/Guard-Flush und Handle-Schließen. Die folgende Reparatur reduziert das
erneute Schreiben älterer Backups und ergänzt aktive Phasen-/Dauermessung, ohne
Save-Schutz oder Zwölf-Sekunden-Testfrist zu schwächen. Acht neue vollständige
Runtime-Prozesse (je vier gleichzeitig) bestehen: 5108 bestanden, 40 Skips.
Kein erneuter Shutdown-Timeout; die genaue tiefere OS-/Datenträgerschicht bleibt
unbelegt. [Aktuelle Reparatur und Prüfgrenzen](STABILIZATION_2026-10-02.md#reparaturrunde-vom-03102026-speichern-fehleranzeige-sprache).
Die folgenden Absätze dokumentieren den älteren Untersuchungsstand.

Zwei frühere Gesamtläufe überschritten bei unterschiedlichen GBA-Online-Testfällen die
unveränderte Zwölf-Sekunden-Frist zum Beenden. Ein Lauf fand gleichzeitig mit Ubuntu-
Tests statt. Die private Sitzung war beim anschließenden Test-Cleanup noch gesperrt.
Einzelwiederholungen einschließlich 20 verzögerter Austauschfälle und der abschließende
Gesamtlauf bestanden. **Die ursprüngliche Verzögerungsursache ist nicht abschließend
belegt und wird trotz des grünen Abschlusslaufs nicht als behoben erklärt.**

Konkret verbessert wurden:

- Der Besitzer meldet jetzt vor dem finalen synchronen Speichern/Dispose `Stopping`,
  akzeptiert keine neuen Eingaben und hält seine Sperren bis zum tatsächlichen Ende.
- Der Test-Cleanup löscht keine noch vom Besitzer verwendete Testablage mehr. Zuvor
  konnte er nach einem Timeout ungesperrte Begleitdateien entfernen, während die
  Emulation noch schrieb, und so zusätzliche Fehler in synthetischen Testdaten erzeugen.
- Ein deterministischer Regressionstest blockiert den Abschluss gezielt und prüft Status,
  Eingabesperre und Erhalt der belegten Ablage. Timeout-Berichte enthalten nun Besitzer,
  Sitzungszustand und Netzwerkphase; die Testfrist wurde nicht zum Kaschieren erhöht.

Stack-Stichproben zeigten synchrone dauerhafte Datei-Schreibvorgänge, aber nicht den Stack
des tatsächlich hängenden GBA-Falls. Eine eindeutige Festplatten- oder Deadlock-Ursache
lässt sich daraus nicht ableiten. Vor einer Releasefreigabe unter Speicherlast wiederholen
und bei einem erneuten Hänger den betroffenen Besitzer vor jeder Bereinigung erfassen.
Keine Original-Spielstände des Nutzers wurden für diese Prüfungen verändert.

### Nächste Abnahme – nicht durch Agenten erfindbar

1. Zwei originale, tauschbereite lokale GBA-Spielstände bereitstellen. Gefunden war bisher
   Rocket Edition; dieser Hack ist kein Ersatz für die Original-Profilabnahme.
2. Zuerst echtes lokales Link Lab als Kontrollfall prüfen, dann dasselbe Paar online lokal.
   Beide Richtungen und komplettes Beenden/Neuladen mit Besitzstandkontrolle durchführen.
3. Native Windows↔Linux/Wayland-Oberflächen und Browser auf zwei physischen Rechnern testen.
4. Deutschland↔Österreich: direkte Verbindung und ausdrücklich konfigurierter TURN-Relay;
   RTT/Jitter/Unterbrechungen vor Auswahl, Bestätigung und Speichern dokumentieren.
5. Erst erfolgreiche reproduzierbare Fälle mit Edition, Region, Revision, Bootprofil und
   Build in die bestätigte Kompatibilitätsmatrix aufnehmen. Danach weitere Regionen/Hacks
   separat qualifizieren; kein automatisches Freischalten der gesamten Gen3-Familie.

Die Nutzerfreigabe für Commit/Push liegt inzwischen vor. Der erneute Abruf vor dem
Git-Abschluss bestätigte unverändert Linux-Upstream `367674f`. Dessen Commit muss in
der gemeinsamen Historie erhalten bleiben; kein Force-Push. Die vorhandenen lokalen
Windows-/Linux-/Link-Ergänzungen gehören zum gemeinsamen Entwicklungsstand.

## English

This milestone implements an original two-player Gen3 protocol adapter on top of the
existing GBA core and shared Windows/Linux runtime. It reuses browser-assisted encrypted
WebRTC; each peer runs only its own game and no full ROM/save files are transferred.
It is a **development-only profile, not a verified Pokémon trading release**, generic
GBA WAN cable, wireless adapter or GB-to-GBA bridge.

The catalog identifies fifteen exact published original builds: English Ruby/Sapphire
revisions 0/1/2, German Rubin/Saphir 0/1, English FireRed/LeafGreen 0/1 and English
Emerald 0. Recognition is not validation. No retail pairing is marked verified; explicit
development consent is required. Unknown revisions and hacks remain blocked even if
their headers match. The current online owner uses HLE BIOS, not a selected full BIOS.

Windows Tools → Online Link and Linux F10/Tools → Online Link use the same profile.
Choose one host and one guest, confirm the development warning, privately exchange
browser invitation/answer and keep both helper pages open. Return to the emulator and
use the game's cable room. Native 240×160 display and shoulder-button input are retained.
Pause, audio invalidation and shutdown cooperate with networking; timeline manipulation
and cheats are disabled. No hosted lobby, default STUN/TURN service or firewall change
was introduced. Router traversal may require explicitly configured infrastructure.

Every online session owns private battery/RTC files and a bounded local journal. Original
files remain locked and untouched during play. Both frontends offer journal inspection
and deliberate adoption for the selected cartridge only, after the owner has stopped.
Never equate a clean close with successful trading: inspect **separate copies** on both
peers after restarting, leaving the journaled files unchanged. Interrupted/faulted,
modified and already adopted sessions cannot be silently promoted.

Before adoption, the complete old family is retained and checked; stale auto-resume and
preview files are archived under the same save lock. Manual slots remain and may undo
the imported progress if loaded. Per-file atomic replacement is not a multi-file battery/
RTC transaction: interrupted adoption remains marked `Promoting` for manual review.
Original and working copies are retained, not automatically rolled back. Journals contain
local paths/fingerprints, not invitation secrets or raw game/network data; keep them private.

The serial peer contract keeps local cable emulation separate. GBA wire-v2 negotiates
the Gen3 development profile and rejects GB/GBC-v1/malformed/replayed traffic. The adapter
assembles eight-word game commands and local checksums, with bounded queues, explicit
phase resets and fail-closed treatment of stale nonempty commands. Guest IRQ cadence is
an approximation requiring retail qualification. Source research is documented separately;
no gpSP implementation was copied or translated, and existing vendored-core notices remain.

### Verification and outstanding shutdown investigation

The final Release solution build completed with no warnings or errors. The final Windows
solution run contains **885 tests: 844 passed, 41 skipped, zero failed** (Core 232/232,
Runtime 345+1 POSIX skip, Windows smoke 187+1 foreground-activation skip, Desktop 80+39
native-environment skips). Native Ubuntu/WSL passed Core 232/232 and the final Runtime
346/346. Its Desktop run passed 82 tests with 37 skips without a Wayland session. These
are native Linux .NET tests, not a native Wayland GUI qualification. The first direct-DLL
Desktop invocation used an excessive minimum-executed count and returned testhost code 9;
the corrected count passed. No test failure or SDL crash was reported in that invocation.

Two real-browser probes connected independent Windows and Ubuntu/WSL GBA owners using
WebRTC. Synthetic ARM programs exchanged complete commands and recorded the expected
SRAM values; original test saves were unchanged. A host-initiated close ended both owners
with `CleanStopped` and technically adoptable archives; no adoption was performed.
Both browsers ran on Windows on the same physical PC, without STUN/TURN. This establishes
neither retail trading nor physical WAN/router traversal. Local ignored report and sanitized
manifest locations are listed above. The final lifecycle-only change publishes `Stopping`
while the owner performs its final flush; the network protocol is unchanged.

**Two earlier aggregate runs intermittently exceeded the unchanged twelve-second shutdown
test deadline. The original cause remains unproven despite subsequent passing runs.**
Runtime now reports `Stopping` during final disposal; a regression test verifies that state,
input rejection and ownership retention. Test cleanup no longer removes files belonging
to an unfinished owner, which previously caused secondary errors in synthetic artifacts.
Diagnostic snapshots saw synchronous durable I/O, but did not capture the failing GBA
owner's stack. Do not claim a proven disk cause, a proven deadlock, or a definitive fix.
Repeat shutdown/storage-load tests before release, preserving live evidence on recurrence.

Synthetic test/real-browser evidence must be read with its environment and exact counts,
not as a retail trade or physical WAN proof. Remaining acceptance gates are a genuine
local trade control case, actual saved outcomes in both directions, two physical native
Windows/Wayland PCs, and Germany–Austria direct/relay/failure scenarios. The user explicitly
authorized commit and push after reviewing these results. The pre-publication fetch still
resolved Linux upstream to `367674f`, which must remain in the shared history. This Git
authorization is not a retail-trading release approval or an infrastructure deployment.
