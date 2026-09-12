# Linux-Playtest — Runde 3

Stand: **12. September 2026** · Basis **22a77ef** plus lokale Änderungen.
Diese Abschlussprüfung gilt für die in [der Fixliste](LINUX_FIX_LOG.md)
zugeordneten Änderungen R3-01 bis R3-08. Vorherige Ergebnisse bleiben in
[Runde 2](LINUX_PLAYTEST_ROUND2.md) erhalten.

## Ergebnisse

| Prüfung | Ergebnis | Lokaler Beleg unter `artifacts/` |
| --- | --- | --- |
| DesktopTests Release-Build | 0 Warnungen, 0 Fehler | `linux-round3-build-final.log` |
| Desktop-Logik ohne native Opt-ins | 78 bestanden, 33 native Skips, 0 Fehler; 111 entdeckt | `linux-round3-logic-final.log` |
| Vollständiger isolierter Weston-/D-Bus-Lauf | **108 bestanden, 3 Audio-Skips, 0 Fehler**; 111 entdeckt | `linux-round3-headless-final.log` |
| GTK/ATK und separater AT-SPI-Client, gezielt | 6/6 bestanden, kein Skip; Teilmenge des vollständigen Laufs | `gtk-accessibility-bus-final.log` |
| Gemeinsamer Texteditor, gezielt | 9/9 bestanden; letzter nativer Feldtest nach Kontrastkorrektur erneut 1/1 | `text-editor.trx`, siehe unabhängige Kritik |
| Async ROM + Settings + Komfort, gezielt | 31/31 bestanden: 7 ROM-, 5 Settings-, 19 Komfortfälle; Teilmenge | Vom zuständigen Agenten geprüft; zusätzlich vollständiger Lauf oben |
| Normaler Linux-x64-Publish | erfolgreich, Start-Build aktualisiert | `linux-round3-publish-final.log` |
| Paket linux-x64 | Archiv, Quellcode, Notices und Start ohne installierte .NET-Runtime bestanden | `linux-round3-package-x64.log` |
| Paket linux-arm64 | Cross-Publish, ELF-/Archiv-/Quellenprüfung bestanden; Ausführung auf x64 übersprungen | `linux-round3-package-arm64.log` |
| Shell-Syntax und Whitespace | `bash -n scripts/test-linux-headless.sh`, `git diff --check` bestanden | lokale Abschlussprüfung |

Teilprüfungen sind keine zusätzlichen, unabhängigen Testfälle und werden nicht
zur Zahl 108 addiert. Die CI-Mindestwerte sind auf 78 beziehungsweise 108
aktualisiert; ein tatsächlicher GitHub-CI-Lauf wurde hier nicht ausgeführt.
Windows und ARM64 wurden nicht auf entsprechender Hardware gestartet.

## Was gezielt geprüft wurde

- Text und Auswahl bleiben bei Clipboardfehlern erhalten; Cut löscht erst nach
  erfolgreichem Kopieren. Tests verwenden eine Fake-Zwischenablage.
- Graphem-Cursor, Pointer-Auswahl, AltGr, SDL-IME-Offsets/Preedit, Commit und
  Eingabefokus beim Verlassen eines Felds. Save Title ist während Preedit deaktiviert.
- GTK liefert echte Namen, Rollen, checked/enabled und EditableText. Native Aktionen
  ändern die tatsächliche Host-Konfiguration; veraltete und deaktivierte Aktionen
  erzeugen keine nachträgliche Anwendungsänderung. Ctrl+F7 lässt F7-Rewind bestehen.
- Ein separater Python-AT-SPI-Prozess liest und bedient Widgets über einen privaten
  Bus. Die Probe zeigt ihr GTK-Fenster ausschließlich auf dem eigenen, isolierten
  Weston-Compositor, nie auf dem persönlichen Desktop.
- Abbruch, späte ROM-Ergebnisse, Lock-Freigabe und Fortbestand der Originalsession;
  GBA→GBA-Fokuswechsel dürfen den alten Owner nicht parallel fortsetzen.
- Unveränderliche Settings-Snapshots, neue Änderungen während Schreiben,
  Fehlerrückmeldung/Retry und sichere Scope-Grenzen.

Der erste vollständige Lauf fand eine veraltete Testannahme: nach dem Schließen
von Settings erwartete der Test sofort synchrone Speicherung. Der Test wartet
jetzt begrenzt über den normalen Flush-Pfad auf das Ende des Hintergrundauftrags
und prüft weiterhin alle ursprünglichen gespeicherten Werte. Die Implementierung
wurde dafür nicht wieder synchron gemacht. Der abschließende Wiederholungslauf
besteht vollständig innerhalb der oben genannten Opt-ins.

MESA-Softwarefallback-Meldungen und GTK-Meldungen über den fehlenden echten
Keyboard-Seat stammen aus dem Headless-Kontext. Sie sind keine Behauptung
funktionierender physischer Tastaturzustellung. Die AT-SPI-Testsemantik und dabei
korrigierte Testfehler sind ausführlich in [Kritik Runde 3](LINUX_CRITIQUE_ROUND3.md)
mit Primärquellen dokumentiert.

## Reproduktion

Mit gebautem DesktopTests-Release und installierten Wayland-Testabhängigkeiten:

```bash
dotnet build tests/AetherBoy.DesktopTests -c Release --no-restore
bash scripts/test-linux-headless.sh \
  "$PWD/tests/AetherBoy.DesktopTests/bin/Release/net10.0/AetherBoy.DesktopTests.dll" \
  --minimum-expected-tests 108
```

Das Skript erzeugt einen privaten Runtime-Ordner, Weston und D-Bus; es beendet
nur seinen eigenen Compositor. Die Ubuntu-24.04-Abhängigkeiten und beide
Architekturjobs stehen in `.github/workflows/ci.yml`. Die lokale Prüfung lief
im Docker-Image `aetherboy-linux-accessibility-check` mit schreibgeschützt
bereitgestellter Testassembly und `--network none`. Keine Test-ROMs, BIOS-Dateien
oder Nutzersaves sind Bestandteil der Pakete.

## Visuelle und unabhängige Abnahme

Das finale versteckte 900×650-Capture
`artifacts/comfort-review-round3/title-ime-large.png` zeigt die größte Textstufe,
den dunklen Editor mit Fokusrahmen, IME-Auswahl/Caret und deaktiviertem Save Title.
Reviewer und Hauptagent haben das Ergebnis angesehen. Der anfängliche
Weiß-auf-Cyan-Kontrastfehler wurde vor der Endbewertung behoben.

**Unabhängige Bewertung: UI 9,1/10, Features 8,9/10.** Die unveränderte Rubrik,
Befunde und Begrenzungen stehen in [Kritik Runde 3](LINUX_CRITIQUE_ROUND3.md).
Diese Noten gelten für den geprüften Softwareumfang, nicht als pauschale
Hardware- oder Releasefreigabe.

## Ausgelieferte lokale Archive

Unter `artifacts/packages/` liegen die neu gebauten Runtime-Archive und ihre
`.sha256`-Dateien:

| Datei | SHA-256 |
| --- | --- |
| `AetherBoy-4.8.0-alpha.1-linux-x64-self-contained.tar.gz` | `c4a9c26944f50ba6dd252abbe520c1e41f1af85993922fdea3640b977a810952` |
| `AetherBoy-4.8.0-alpha.1-linux-arm64-self-contained.tar.gz` | `45a7fab5b1aa2404da7106dd54826879bd89eef17cb83d9d7bc73884404d4a2c` |

Paketmetadaten kennzeichnen den modifizierten Stand auf 22a77ef. Die Archive
enthalten den finalen Produktionscode: alle 164 enthaltenen C#-Quelldateien
wurden pro Architektur byteweise mit dem aktuellen Checkout verglichen.
Diese Abschlussdokumentation wurde
anschließend ergänzt. Anforderungen und optionale GTK-Bibliotheken:
[Linux-Distribution](LINUX_DISTRIBUTION.md). Der normale Start bleibt
`bash scripts/run-linux.sh`, optional mit ROM-Pfad oder `--accessible`.

## Offen und bewusst nicht behauptet

Physische Controller/Hotplug, weitere Compositoren, Misch-DPI, 120/144 Hz,
Suspend/Resume, echte Orca-Sprachausgabe, reale IME-Kandidatenbedienung und
ARM64-Ausführung bleiben manuell. SDL hat weiterhin begrenzte Symbol-/Emoji-
Fontabdeckung; Textdaten bleiben erhalten. Neue hörbare Audiotests und die
30-Minuten-Läufe wurden wie vereinbart nicht durchgeführt. Der Nutzer übernimmt
die langen Spieltests. Es gab keinen automatischen Commit oder Push.
