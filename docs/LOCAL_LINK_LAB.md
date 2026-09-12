# Local Link Lab — GB / GBC / GBA (DE / EN)

Stand / status: 12 September 2026, lokaler Entwicklungsstand / local development milestone.

**Experimentell: zwei GB-/GBC- oder zwei GBA-Spielinstanzen auf einem PC, mit gemeinsamer
Zeitsteuerung und getrennten Spielständen. Kein Nachweis allgemeiner
Spielekompatibilität.**

## Deutsch

### Was jetzt vorhanden ist

Windows erhält unter **TOOLS → Lokales Link-Kabel (experimentell)** das eigene
Local Link Lab mit zwei Spielansichten. Beide emulierten Geräte laufen in
**einem Prozess und einer gemeinsam gesteuerten Sitzung**. Es werden weder ein
Netzwerkport geöffnet noch zwei unabhängig gestartete Programme verbunden.
Die Auswahl darf dieselbe ROM zweimal oder zwei unterschiedliche ROMs enthalten.
Die Spiele müssen selbst einen zueinander kompatiblen Link-Modus besitzen.

Dieser Schritt umfasst GB/GBC und GBA. GBA verwendet eigene serielle Hardware:
Normal-8-/32-Bit und 16-Bit-Multiplayer für zwei Geräte. GB/GBC und GBA lassen
sich nicht miteinander koppeln. **Netzwerk/Internet, GBA-Wireless-Adapter,
Joybus, Infrarot und Vier-Spieler-Adapter sind nicht Bestandteil dieses Meilensteins.**

### Kurzanleitung für Windows

1. **TOOLS → Lokales Link-Kabel (experimentell)** öffnen. Ein eventuell im
   Hauptfenster laufendes Spiel wird durch das Öffnen allein nicht beendet.
2. Für **PLAYER 1** und **PLAYER 2** jeweils `.gb`/`.gbc` oder zweimal `.gba` auswählen.
   Das Lab verwendet die lokale ROM-Bibliothek; Dateiname oder Titel allein
   bestimmen nicht, ob zwei ROMs identisch sind.
3. **LINK STARTEN** wählen. Erst nach der ROM-Prüfung wird die bisherige
   Hauptsitzung sicher beendet. Die beiden Link-Geräte starten neu; der laufende
   Zustand des Hauptfensters wird nicht als Save State übernommen.
4. Mit **F1 / F2**, **TASTATUR → P1 / P2** oder einem Klick auf das jeweilige
   Spielbild den Tastatur-Spieler wählen. Es gelten die vorhandenen GB-Tasten;
   GBA ergänzt die konfigurierten L/R-Tasten (Standard Q/E) und Controller-Schultern.
   Die ersten beiden erkannten Controller steuern P1 beziehungsweise P2
   (interne Indizes 0 und 1). Eine Tastatur bedient jeweils einen Spieler.
   Beide Controller werden aus derselben Windows-Geräteschnittstelle gelesen,
   damit ein über WGI und XInput gemeldetes Gerät nicht beide Spieler steuert.
   Bei verfügbarem WGI wird kein zusätzlicher XInput-only-Controller beigemischt;
   nach Änderungen der erkannten Geräte zuerst alle Tasten/Sticks loslassen.
5. In beiden Spielen den vorgesehenen Link-/Mehrspielermodus aufrufen.
   **BEIDE PAUSIEREN** pausiert die gemeinsame Sitzung. **Esc** pausiert beim
   Spielen ebenfalls beide; beim Verlassen des Lab-Fensters wird pausiert und
   gehaltene Eingabe freigegeben. Danach bewusst gemeinsam fortsetzen.
6. **AUDIO** wechselt zwischen aus, P1 und P2. Es wird ein Spieler gehört,
   nicht beide gemischt; die globale Audio-Einstellung muss eingeschaltet sein.
7. In jedem Spiel über dessen normale Speicherfunktion speichern und anschließend
   **SITZUNG BEENDEN** oder **ZURÜCK** verwenden. Das Lab wartet auf das Beenden
   und Schreiben beider Geräte. Bei einer Meldung über verzögertes Beenden nicht
   einfach einen weiteren Schreiber auf dieselben Daten starten.

**KABEL TRENNEN / VERBINDEN** simuliert das Aus-/Einstecken, nicht das Beenden
der Spiele. Nicht während eines wertvollen Tauschs oder eines Speichervorgangs
ausprobieren. Ein sichtbarer Verbindungsstatus oder steigender Taktzähler bedeutet
nur, dass die virtuelle Hardware arbeitet — nicht, dass ein Tausch erfolgreich war.
Der **Link-Zähler** zählt übertragene Datenbits; GBA-Multiplayer addiert 16 je
Teilnehmer. Das ist kein Zähler aller physikalischen Takt-/Framing-Flanken.

### Welche Spielstände werden verwendet?

Alle folgenden Windows-Pfade liegen unter `%LOCALAPPDATA%\AetherBoy`.
`<SHA256>` bezeichnet die Inhaltskennung der verwalteten ROM, nicht ihren Namen.

| Auswahl | Spieler 1 | Spieler 2 |
| --- | --- | --- |
| Verschiedene ROM-Inhalte | `Saves\<SHA256-A>\game.sav` | `Saves\<SHA256-B>\game.sav` |
| Gleicher ROM-Inhalt, auch umbenannt | `Saves\<SHA256>\game.sav` | `Saves\<SHA256>\LinkPlayer2\game.sav` |

Bei derselben ROM startet P2 ohne bisherigen Fortschritt, **sofern noch kein
eigener LinkPlayer2-Spielstand existiert**. Dieser getrennte Spielstand bleibt
für spätere Link-Sitzungen erhalten. P1 wird nicht nach P2 kopiert und ein
vorhandener P2-Spielstand wird nicht zurückgesetzt.

Die Link-Startplanung importiert ROMs, aber **keine danebenliegenden externen
`.sav`-, RTC-, Backup- oder Save-State-Dateien**. Sie verwendet vorhandene zentrale
Spielstände und verändert diese beim Planen nicht. Ein gesonderter Import fremder
Spielstände bleibt wie vereinbart zurückgestellt. Bereits zuvor auf normalem Weg
in der Bibliothek übernommene Spielstände bleiben natürlich vorhanden.

Die Runtime hält beide Schreibsperren vor dem Start der Geräte und bis nach
deren Beenden. Dadurch kann keine zweite reguläre AetherBoy-Sitzung parallel
dieselben Save-Dateien überschreiben. **Turbo, Rewind, einzelne Save States und
Einzel-Reset sind im Lab bewusst nicht verfügbar:** einseitige Zeitsprünge würden
die laufende Verbindung auseinanderbringen. Normale Einzelspieler-Funktionen
außerhalb des Labs bleiben unverändert.

Nach einer Link-Sitzung das Einzelspiel **normal neu starten und im Spiel laden**.
Ältere Fortsetzen-/Save-State-Dateien bleiben erhalten, enthalten aber möglicherweise
den Stand vor dem Tausch. Sie danach zu laden und erneut zu speichern kann den
neueren Batterie-Spielstand zurücksetzen. Das Lab überschreibt diese Dateien nicht.

Das Lab übernimmt die globalen Anzeige-, Audio-, Tastatur- und Controller-
Einstellungen; separate Profile pro Spiel sind dort noch nicht angebunden.
Beide Geräte starten derzeit mit dem integrierten Boot-Verhalten. Die in der
Firmware Station gewählte externe Boot-ROM wird im Link Lab noch nicht verwendet.

### Entwicklungsstand und Grenzen

Für GB/GBC führt die gemeinsame Runtime jeweils das zeitlich zurückliegende Gerät um eine
Instruktion weiter. Vergleichsbasis sind Game-Boy-Bildtakte (Base Dots), nicht
gleiche Befehlsanzahlen: CGB-Double-Speed wird im selben Zeitmaß berücksichtigt.
Das ist **instruktionsweise Synchronisation, keine vollständig zyklusgenaue
Simulation zweier Busse**. Besonders der schnelle CGB-Link kann durch den
verbleibenden Versatz empfindlich sein. Gleichzeitiger interner Takt beider
Geräte wird deterministisch mit P1-Vorrang behandelt; elektrischer Taktkonflikt
echter Hardware ist nicht vollständig nachgebildet.

GBA wird abwechselnd um einen CPU-Takt je Gerät weitergeschaltet (280.896 Takte
pro Bild). Die Multiplayer-Übertragung berücksichtigt Baudrate, Teilnehmer-ID,
Bereitschaft, Empfangsslots und Serial-Interrupts. Auch hier sind die HLE-BIOS-
Zeitannahmen und die Verbindung nicht vollständig gegen echte Hardware qualifiziert.
Zustand für mehrtaktige ARM-/Thumb-Befehle muss zu genau einer CPU gehören;
globale Zwischenwerte dürfen nicht zwischen beiden Geräten wandern. Details zu
diesem Ausbau und seinen Regressionen: [GBA-Link-Übergabe](GBA_LOCAL_LINK_HANDOFF.md).

Die bereitgestellte Liste von 100 Spielen ist **keine geprüfte Kompatibilitätsliste**.
Beispielsweise nennt Nintendo für [Wario Land II](https://www.nintendo.com/de-de/Spiele/Game-Boy/Wario-Land-2-297878.html)
und [Alleyway](https://www.nintendo.com/de-de/Spiele/Game-Boy/Alleyway-275451.html)
jeweils einen Spieler. Ein emuliertes Kabel ergänzt keinen Mehrspielermodus,
den ein Spiel nicht besitzt. Die Implementierung enthält keine titelspezifische
Whitelist und keine zugesagte Unterstützung aller Kombinationen.

**Bisher wurde für dieses Link-Paket kein kommerzielles Spiel praktisch getestet.**
Automatisierte Prüfungen verwenden synthetische ROMs, Registerabläufe, temporäre
Save-Dateien und Oberflächenproben. Sie ersetzen weder einen Pokémon-Tausch noch
eine längere Mehrspielerrunde.

### Historischer Abschlusslauf vor dem GBA-Ausbau

Der vorherige GB/GBC-Meilenstein hatte einen Release-Build ohne Warnungen oder Fehler. Lösungstest unter Windows:
**591 Testfälle, 582 bestanden, 9 ausgelassen, 0 Fehler**. Gegenüber dem vorherigen
Paket wurden 60 Link-bezogene Fälle ergänzt (Core 16, Runtime 17, Windows-Storage 12,
Controller-Auswahl 7, Oberfläche 8).

- Core: 197 bestanden; Runtime: 157 bestanden.
- Windows-Suite: 169 bestanden, 1 ausgelassen. Windows hat dem Testfenster den
  Vordergrund verweigert; der vollständige Tastatur-bis-CPU-Test meldet deshalb
  ehrlich „nicht prüfbar“. Der Eingabeschutz wurde nicht umgangen. Die übrigen
  Link-Oberflächenprüfungen einschließlich Start, Frames, F1/F2, Pause, Kabel,
  Neustart und Save-Isolation bestanden.
- Linux-Frontend-Logik unter Windows: 59 bestanden, 8 ausgelassen wegen fehlender
  nativer Wayland-/Unix-/Gerätebedingungen. Kein Linux-UI-Nachweis.

Der optionale Screenshot `artifacts/parity-review/local-link-lab.png` zeigt echte
PPU-Ausgabe zweier generierter Test-ROMs, nicht ein getestetes kommerzielles Spiel.
Körperliche Controller, das vollständige Tastaturspiel, Audio mit Spielen sowie
insbesondere Pokémon-Tausch und CGB-Fast-Link müssen praktisch geprüft werden.

Der neuere zusammengeführte GBA-/Linux-Stand hat einen eigenen Prüfbericht in
[GBA-Link-Übergabe](GBA_LOCAL_LINK_HANDOFF.md) und
[Linux-Integrationsprüfung](LINUX_UPSTREAM_INTEGRATION_REVIEW.md). Die 591 Fälle
oben sind **keine** Ergebniszahl für diesen späteren Stand.

## English

### Windows quick start

Open **TOOLS → Lokales Link-Kabel (experimentell)**, select two `.gb`/`.gbc`
cartridges or two `.gba` cartridges, then choose **LINK STARTEN**. You may select the same
ROM twice or two different games with mutually compatible link modes. Opening
the Lab does not end the main game; starting the validated pair first shuts down
that main session safely. Both linked machines boot fresh, using their battery
saves rather than importing the main window's live state.

Use **F1 / F2**, the player keyboard buttons, or click a display to target the
keyboard. Existing GB key mappings apply; GBA also uses configured L/R keys and
controller shoulders. Controller indices 0/1 target players
1/2. Pause affects both machines; Escape during gameplay and losing window focus
also pause the pair. Audio selects off/player 1/player 2, with global audio enabled.
The cable button disconnects/reconnects the virtual wire without stopping either
game. Save inside each game, then end the session normally so both saves flush.

The Lab is **two emulated devices in one process**, not two independently launched
applications and not a TCP/localhost connection. Mixed GB/GBA pairs, network play,
GBA wireless, Joybus, infrared, four-player adapters, turbo, rewind, individual save states and individual reset
are outside this milestone. The usual single-player tools remain unchanged.

Different ROM contents use their existing primary AppData saves. Identical ROM
contents, even under different names, use the primary save for P1 and persistent
`Saves\<SHA256>\LinkPlayer2\game.sav` for P2. P2 starts fresh only if its separate
save does not exist. **No save cloning, overwriting of existing player saves or
external save/state import occurs during link setup.** Both saves are leased
exclusively for the lifetime of the linked machines.

After a link session, boot the single-player game normally and load its in-game
save. Existing resume/save-state files are not updated by the Lab and may predate
the trade; restoring and saving them can undo newer battery-save progress.

### Shared implementation / Linux handoff

| Layer | Responsibility |
| --- | --- |
| Core: `LocalSerialCable`, `Memory` serial port, `Nanoboy.StepInstruction` | Two GB/GBC serial endpoints; simultaneous outgoing-bit sampling, externally clocked input, interrupt completion, disconnect semantics and instruction stepping |
| GBA Core: `SerialController`, `LocalSerialLink` | Normal 8/32-bit serial and two-device multiplayer 16-bit transfers, mode/baud/status/interrupt behavior, independent per-CPU instruction state |
| Runtime: `LocalLinkSession`, `LocalLinkMachine` | One owner thread, GB base-dot or GBA CPU-cycle scheduling, queued player input and shared pause/connection commands, two frame exchanges, bounded audio dispatch and both save-write leases |
| Windows: `WindowsLocalLinkStorage`, `frmLocalLinkLab` | Managed ROM selection/validation, safe player-save planning, two Aether displays, controller/keyboard routing, audio selection and clean lifecycle |

`LocalLinkPlayerConfiguration` supplies each ROM path, save path, optional boot
ROM, emulator configuration and palette. Public player indices are zero-based.
Await `Ready` before treating the pair as running. Use `SetButtonsAsync`,
`SetPausedAsync`, `SetConnectedAsync`, `TryCopyLatestFrame`, `LatestSnapshot`,
`AudioSamplesAvailable`, and `ShutdownAsync`/`DisposeAsync`; do not mutate the
machines from UI or audio callbacks. GBA adds `SetGameBoyAdvanceButtonsAsync`.
Allocate each frame destination from `GetVideoGeometry(player)`: 160×144 for GB/GBC
or 240×160 for GBA, before any first-frame callback. Audio events identify their player and carry playback session and
generation tags. Recreate the output/cursor when switching between the ordinary
session and a link session, or between audible link players. Those session types
currently allocate their playback IDs independently; merely clearing queued
samples does not reset a reused cursor. Windows creates a fresh output for each
such switch. Keep honoring generation tags within each active output.

The Windows Lab currently starts both machines with integrated boot behavior:
the external boot ROM selected in Firmware Station is not applied here yet. The
runtime parameter is not proof of a completed per-player firmware UI. Global
display/audio/keyboard/controller settings are used; per-game profiles are not
wired to this Lab. Preferences do not imply that every single-player tool is
present in this window.

**The Linux frontend is not wired to Local Link Lab yet.** The colleague should
reuse the shared Core/Runtime, add native two-player selection/display/input,
choose separate XDG player-save paths and preserve Linux ownership/migration
contracts. Keep errors, focus loss, neutral controller re-arming, pause, disconnect
and shutdown equivalent. A Windows build is not native Wayland, PipeWire or
controller validation. Do not introduce a second link core or silently connect
existing independently running sessions.

GB/GBC machines share base-dot time, advancing the less advanced participant one
instruction at a time. Atomic CPU instructions still allow timing skew, potentially
larger than a fast CGB serial bit. This is an **experimental instruction-level
scheduler, not cycle-exact cross-machine execution**. The deterministic P1 fallback
for simultaneous internal clock requests is not a complete model of physical
clock contention. More precise scheduling and real-game compatibility work remain.

GBA pairs advance one CPU cycle at a time, alternating devices. Multiplayer uses
emulated completion events at the selected baud rate, not host time or blocking
socket reads. The link counter is a data-bit counter, not all physical clock
edges. CPU helper state is instance-owned; starting/capturing one device must not
change a peer. Unilateral state operations are rejected while attached to a GBA
link. See [GBA implementation and state-version handoff](GBA_LOCAL_LINK_HANDOFF.md).

### Hardware and architecture references

The implementation was informed by architecture review, **not copied from mGBA**.
mGBA's Qt local multiplayer coordinates cores in memory and explicitly assigns
separate save IDs; its GB link driver handles transfer phases and emulated timing.
See [MultiplayerController](https://github.com/mgba-emu/mgba/blob/master/src/platform/qt/MultiplayerController.cpp)
and [GB lockstep](https://github.com/mgba-emu/mgba/blob/master/src/gb/sio/lockstep.c).
This does not establish equivalent accuracy or game compatibility for AetherBoy.

The GBA expansion also reviewed [mGBA GBA SIO](https://github.com/mgba-emu/mgba/blob/master/src/gba/sio.c)
and [GBA lockstep](https://github.com/mgba-emu/mgba/blob/master/src/gba/sio/lockstep.c).
GBA is a separate serial implementation, not the GB byte exchange with a new extension filter.

For timing, [Pan Docs: Serial Data Transfer](https://gbdev.io/pandocs/Serial_Data_Transfer_%28Link_Cable%29.html)
specifies 8,192 bits/s normally and 262,144 bits/s in CGB fast mode; double-speed
doubles those rates. Thus one eight-bit transfer takes 4,096 CPU T-cycles
(about 976.56 µs normally) or 128 T-cycles (about 30.52 µs in fast mode), with
half the elapsed time in double-speed mode. Fast mode is **32×**, not 16×.
An unplugged internally clocked port shifts in ones; an externally clocked port
waits for external edges instead of completing on a host timer.

### Validation still required

Historical GB/GBC-only Windows solution run: **591 total, 582 passed, 9 skipped, 0 failed**;
Release build: zero warnings/errors. Core 197/197 and Runtime 157/157 passed.
Windows smoke: 169 passed, one native foreground-dependent keyboard-to-CPU test
skipped because Windows denied activation. Desktop logic: 59 passed, eight native
Linux/Wayland/Unix tests skipped. The production foreground guard remains intact.
This adds 60 test cases, but does not constitute physical-controller qualification.

No commercial-game link session has yet been tested for this milestone. There
is no verified 100-game list. Automated synthetic coverage and UI smoke checks
must not be described as proof of successful trades, battles or sustained play.

- Run the Core, Runtime and Windows smoke suites; record the actual final results.
- Exercise a known two-player game's own link mode; verify both players' progress
  and inputs over a longer session, not just two moving screens.
- Test pause/resume, player switching, neutral controller reconnect, off/P1/P2
  audio and orderly closing. Run interruption tests only with disposable progress.
- Verify distinct-game primary saves and same-ROM P2 persistence independently;
  repeat with another window holding a save lease and confirm safe refusal.
- Qualify CGB fast and mixed CPU-speed behavior separately. Log timing failures
  as compatibility limitations; do not add unverified game-specific workarounds.
- After Linux UI integration, repeat on native Wayland with real controller and
  audio backends. Keep ROMs and private saves out of commits and diagnostic exports.
