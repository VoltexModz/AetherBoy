# Phase 2: Cheats auf Windows und Linux / Cheat support

Stand / updated: 2026-10-01. Implementiert, synthetisch getestet; keine pauschale
Garantie für Spielcodes. Diese Matrix ersetzt die frühere Beschränkung auf
direkte GBA-RAM-Schreibzugriffe.

## Deutsch

### Benutzung

1. Spiel öffnen, dann Windows **Cheats** beziehungsweise Linux **Tools** öffnen.
2. Bei GBA das passende Format auswählen. **Automatisch** ist für eindeutige
   CodeBreaker- oder einfache GameShark-Zeilen gedacht, nicht zum Erraten einer
   beliebigen verschlüsselten Liste. Im Zweifel explizit das Geräteformat wählen.
3. Nur wenn die gewählten Codes einen Mastercode benötigen, diesen und die
   abhängigen Zeilen in ihrer ursprünglichen Reihenfolge als ein Set einfügen.
   Codes ohne Mastercode können direkt eingegeben werden; es gibt keine allgemeine
   Mastercode-Pflicht, keine automatische Beschaffung und keine Ergänzung durch
   AetherBoy. Unter Windows gehen Zeilenumbrüche oder `+`; Linux wandelt
   eingefügte Zeilenumbrüche zu `+` um. Name und Schaltzustand gelten für das Set.
4. Codes hinzufügen, ein-/ausschalten oder entfernen. Die GBA-**Gerätetaste**
   schaltet den Knopf am Cheat-Modul; sie ersetzt nicht A/B/L/R am GBA.
   Erneutes Betätigen lässt sie los; ein Reset lässt sie ebenfalls los.
5. Die Liste gilt für diese Sitzung, nicht automatisch für den nächsten Start.
   Vor Verwendung einen Spielstand sichern. Abschalten nimmt bereits erfolgte
   RAM- oder Save-Änderungen nicht zurück; ROM-Patches werden dagegen zurückgenommen.

Kein Download fremder Codes, keine Spieldatenbank und kein Verbot aufgrund einer
ROM-Kennung. Der Spieler wählt Version, Region, Code und erforderlichen Master.
Geprüft werden Syntax, implementierter Befehl, Adress-/Breitengrenzen und Aufwand.
Ein syntaktisch gültiger, aber falscher Code kann ein Spiel oder seinen Save verändern.
Online Link bleibt ohne Cheats; dafür werden keine Sicherheitsprüfungen gelockert.

### Gemeinsame Formatmatrix

| System / Format | Eingabe | Implementiert |
| --- | --- | --- |
| GB/GBC GameShark | `01VVLLHH` | Byte an Adresse HHLL schreiben |
| GB/GBC Game Genie | `XXX-XXX`, `XXX-XXX-XXX` | ROM-Leseüberlagerung, optional Originalbyte-Vergleich; auch ohne Bindestriche |
| GB/GBC CodeBreaker | `00AAAA-VV` | Byte an Adresse AAAA schreiben |
| GB/GBC Raw | `AAAA:VV` | Direkter Speicherschreibzugriff |
| GBA Raw | `AAAAAAAA:VV`, `:VVVV`, `:VVVVVVVV` | Ausgerichtete 8-/16-/32-Bit-Zugriffe in EWRAM/IWRAM |
| GBA CodeBreaker | `CB:AAAAAAAA VVVV`, optional `CBRAW:` | Master-Entschlüsselung, Byte/Halfword, AND/OR/Add, Vergleiche/Bit-/Tastenbedingungen, Slide/List, Thumb-Masterhook |
| GBA GameShark v1/v2 | `GS:AAAAAAAA VVVVVVVV`, `GSRAW:` | TEA/Reseed, 8/16/32 Bit, Adressgruppen, Bedingungen, Gerätetaste, Thumb-Hook, 16-Bit-ROM-Patch |
| GBA Action Replay v3 / PAR v3 | `AR3:AAAAAAAA VVVVVVVV`, `AR3RAW:`; auch `PAR3:` | Eigene TEA/Reseed-Schlüssel, direkte/wiederholte Zugriffe, Add, Zeiger, signed/unsigned Bedingungen, nächste 1/2 Befehle, Block/ELSE/ENDIF, Slides, Gerätetaste, Thumb-Hook, ROM-Patches, 16/32-Bit-I/O |

Ein Präfix gilt auch für nachfolgende unpräfixierte Zeilen im selben Set. Die
Formatwahl in der Oberfläche setzt es automatisch. Explizite Präfixe und Raw-
Adressen werden nicht überschrieben. `RAW` bedeutet bereits entschlüsselt, nicht
einen anderen GameShark-/Action-Replay-Gerätetyp.

Ein vorheriges Set kann Cipher-/Hook-Kontext an das nächste vererben, wie im
geprüften mGBA-Pfad. Der Kontext wird beim Hinzufügen kopiert; Entfernen oder
Abschalten eines Masters kompiliert abhängige Sets nicht nachträglich neu.
Deshalb zusammengehörige Zeilen bevorzugt gemeinsam verwalten. Ein unvollständiges
Set veröffentlicht keine Teiländerungen, Patches oder geänderten Cipher-Schlüssel.

### Bewusste Grenzen

- GB/GameShark unterstützt hier `01`, nicht die bankumschaltenden `80`-/`90`-
  Varianten. Sie werden abgewiesen statt als gewöhnlicher Schreibcode missdeutet.
- AR-v3-Slowdown und bedingtes dauerhaftes Abschalten aller Codes sind nicht
  implementiert (auch im geprüften mGBA-Pfad fehlen diese). Keine verschachtelten
  Blockbedingungen zugesichert. Unbekannte Gerätebefehle werden abgewiesen.
- GBA-Raw bleibt auf Work-RAM beschränkt. Gerätebefehle verwenden ausgerichtete
  kanonische RAM-/I/O-/Palette-/VRAM-/OAM-/Backup-Bereiche; beliebige Bus-Mirrors
  und undefinierte/unalignierte Zielzugriffe werden nicht zugesichert. Ein ungültiges
  dynamisches Zeigerziel wird für diesen Durchlauf übersprungen.
- ROM-Patches arbeiten nur in der tatsächlich geladenen ROM-Kopie; keine
  ROM-Vergrößerung und kein Schreiben in die Quelldatei. Überlappende Sets werden
  in Listenreihenfolge angewandt und korrekt entfernt.
- Game-/ROM-ID-Zeilen werden als Metadaten verarbeitet, nicht als Nachweis der
  passenden Edition. Die Gerätetaste und Cheatliste sind Sitzungszustand, nicht
  Inhalt einer Cheat-Datei, eines Savestates oder der dauerhaften Einstellungen.
  Ein geladener Savestate nutzt weiterhin die aktuell aktivierten Cheats.
- Grenzen: 32 Zeilen je GB/GBC-Set; 512 Zeilen und 32 KiB Text je GBA-Set,
  maximal 128 GBA-Sets und 65.536 ausgeführte Operationen je Set/Durchlauf.
- Kein Import/Export von `.cht`-Dateien und kein RAM-Sucher in diesem Paket.

### Architektur, Referenz und Tests

- `Core/CheatEngine.cs`: GB/GBC-Decoder und ROM-Leseüberlagerung; Runtime verwaltet
  mehrzeilige Sets atomar, ohne unabhängig laufenden Cheat-Thread.
- `Runtime/GbaCheatProgram.cs`: vollständiges Set erst prüfen/übersetzen, dann
  veröffentlichen; Formate und Fortsetzungszeilen getrennt behandeln.
- `GbaCheatCipher.cs` / `GbaCheatTables.cs`: mGBA-adaptierte Entschlüsselung unter
  MPL-2.0. Original-C-Cipher dient separat als Testvektor-Referenz, nicht als
  Produktionsabhängigkeit. [Provenienz](MGBA_REVIEW.md#cheat-abgleich-vom-01102026),
  [Lizenz/Notice](../third_party/mgba-cheats/NOTICE.txt).
- `GbaCheatEngine.cs`: Ausführung im Owner-Thread; Sets ohne Hook pro Frame,
  gehookte Sets nur an der passenden Thumb-Instruktion. ROM-Patch-Überlagerung
  separat rücknehmbar; Cheat-Zugriffe verändern nicht das CPU-Waitstate-Budget.
- `SetCheatButtonAsync` und unveränderliche Snapshots verbinden beide Oberflächen
  mit demselben Runtime-Vertrag. Kein zweiter Linux-/Windows-Cheatdecoder.

Geprüft werden unter anderem mGBA-Cipher-Gegenproben mit Reseed, CB-Listen-
Byteordnung, Bedingungen über ganze Fills, AR-ELSE/signed/unsigned, Zeigerwechsel,
Gerätetaste/Keypad, I/O, überlappende ROM-Patches, echter Thumb-Hook, ungültige Sets,
Reset/Snapshot-Lifecycle, Linux-Mehrzeilen-Paste und der Windows-Dialog.
Testzahlen und Plattformgrenzen sind im aktuellen Handoff erfasst. Synthetische
ROMs beweisen diese Abläufe, aber nicht die Wirkung beliebiger Internet-Cheatcodes.

## Merkliste: offene Cheat-Arbeit (01.10.2026)

Arbeitsstatus: Phase 2 ist für den vereinbarten Umfang dieser Entwicklungsrunde
vorerst abgeschlossen, auf Windows und Linux für GB, GBC und GBA gemäß der
Formatmatrix oben. Das bedeutet nicht, dass sämtliche Gerätebefehle implementiert
oder echte Spielcodes vollständig getestet sind.

Vom Nutzer ausdrücklich für spätere Arbeit zurückgestellt; diese Liste ist kein
Auftrag zur sofortigen Umsetzung. Die noch offenen Hardware-, Portable-/USB- und
Wayland-Prüfungen nach Phase 1.1 stehen ergänzend in der
[gemeinsamen Phase-1-Merkliste](WINDOWS_ROADMAP.md#arbeitsstatus-und-merkliste-nach-phase-11).

- [ ] **GB/GBC:** bankumschaltende GameShark-Varianten (`80`/`90`-Familien) anhand
  der Geräte-/Mapper-Semantik ergänzen; RAM-Bankwahl, aktivierte Bank und
  ROM-/RAM-Schutz getrennt regressieren.
- [ ] **GBA / AR v3:** Slowdown und bedingtes dauerhaftes Abschalten aller Codes
  separat spezifizieren/implementieren. Verschachtelte Bedingungen untersuchen;
  erst nach definierten und getesteten Regeln als unterstützt ausgeben.
- [ ] **GBA-Zieladressen:** benötigte Spiegelbereiche/unalignierte Gerätebefehle
  anhand belastbarer Referenzen bewerten. Nicht einfach die vorhandenen Grenzen
  aufheben. Unbekannte/gefährlich große Programme weiterhin begrenzt abweisen.
- [ ] **Master-Kontext:** eigenständige Codes ohne Master und abhängige Sets mit
  Master getrennt testen; Verhalten nach Abschalten/Entfernen/Reihenfolgewechsel
  eines getrennten Masters verständlich halten. Keine automatische Codeauswahl.
- [ ] **Echte Spielcodes für GB, GBC und GBA:** passende eigene Codes mit und
  ohne Master, Verschlüsselung, Bedingungen und Gerätetaste auf Windows und
  Linux prüfen. Wirkung im Spiel sowie Aus-/Einschalten und Neustart protokollieren.
- [ ] **Sichere Testbasis:** vor solchen Versuchen eine unabhängige Batterie-Save-
  Sicherung (`.sav`, gegebenenfalls RTC) **und** einen Savestate erstellen. Bei
  gefährdetem Original bevorzugt eigene Testkopie verwenden. Ein Savestate allein
  garantiert keine Rücknahme bereits gespeicherter Cheat-Auswirkungen. Restore
  und Zuordnung zur richtigen ROM-Version prüfen; Save-Dateien nicht ungefragt ersetzen.
- [ ] **Native Linux-Abnahme:** Formatwahl, mehrzeiliges Paste, hinzufügen,
  umschalten, entfernen, Gerätetaste/Reset unter echtem Wayland testen.

Zusätzliche spätere Produktfunktionen, nicht bereits Teil dieses implementierten
Pakets: persistente Cheatlisten/`.cht`-Import und -Export sowie RAM-Sucher. Vor
Implementierung gesondert priorisieren; keine automatische Online-Codedatenbank.

## English

Both frontends use the same cheat compiler, ciphers and owner-thread executor.
Select the GBA device format. Codes that do not require a master can be entered
directly. Only where required, include the user-supplied master and dependent
lines in one set; there is no mandatory or automatically fetched master code.
Add/toggle/remove the set. Linux converts pasted newlines to `+`. The virtual
device button emulates the cheat cartridge's button; it is distinct from keypad
conditions and is released on reset.

The table above lists accepted syntax and operations: GB/GBC GameShark `01`,
Game Genie, CodeBreaker and raw writes; GBA CodeBreaker encrypted master streams,
GameShark v1/v2 and Action Replay v3 including reseeding, conditions, fills/lists,
hooks, pointers, I/O and reversible in-memory ROM patches. Explicit prefixes win
over the format selector. Unknown/ambiguous formats are not silently reinterpreted.

Users supply their own codes and master codes. There is no ROM whitelist or
game-specific compatibility claim. Syntax/address/work limits prevent malformed
programs, not incorrect game effects. Disabling a code does not undo RAM/save
changes; back up saves. Lists and device-button state are session-only, not part
of persisted settings or savestates. Online Link keeps cheats disabled.

Not supported: bank-switching GB GameShark variants, AR-v3 slowdown/disable-all
conditions, arbitrary unaligned or mirrored GBA bus accesses, nested AR blocks,
cheat-file import/export and RAM searching. Master context is copied on add;
removing a separate master does not recompile existing dependent sets. ROM files
are never patched on disk. mGBA-derived cipher/table files retain MPL-2.0; license
and source notices are shipped in `licenses/mgba-cheats/`.

Windows UI and portable Linux-frontend tests are separate from native Wayland
and commercial-game testing. No successful game-specific cheat effect or
cross-platform Pokémon trade is claimed by this implementation.
