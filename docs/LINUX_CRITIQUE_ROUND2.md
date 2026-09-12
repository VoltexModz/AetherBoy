# Linux-Kritik, Runde 2

Stand: 12. September 2026. Ausgangscommit: `22a77ef`. Dieser unabhängige Review
bewertet die zweite Linux-Komfortrunde. Die Nutzerziele sind **Features > 8/10**
und **UI > 9/10**. Zielwerte sind keine vorgegebenen Ergebnisse; 8,0 beziehungsweise
9,0 reichen ausdrücklich nicht. Eine unbequeme Note bleibt stehen, wenn die
Belege eine bessere Note nicht rechtfertigen.

## Vorab festgelegte Rubrik

0: fehlt/unbenutzbar; 3: erhebliche Hindernisse; 5: brauchbar mit häufigen Problemen;
7: zuverlässig mit deutlichen Einschränkungen; 9: ausgereift und im vereinbarten
Umfang gut geprüft; 10: praktisch keine relevanten verbleibenden Mängel.
Gewichtete Mittelwerte auf eine Nachkommastelle. Gewichte werden nach Sichtung
neuer Screenshots oder Testergebnisse nicht geändert.

| UI-Kriterium | Gewicht | Bewertet wird |
| --- | ---: | --- |
| Lesbarkeit | 20 % | Kontrast, Schrift, lange Texte, erkennbare Auswahl und Zustände |
| Hierarchie | 15 % | klare Primäraktion, sinnvolle Gruppierung, Informationsdichte |
| Konsistenz | 15 % | Benennung, Layout, Rückwege, gleiche Bedienmuster |
| Navigation und Feedback | 25 % | Tastatur/Controller, Fokus, Erfolg/Fehler/Busy, Textfelder |
| Skalierung und Zugänglichkeit | 25 % | kleinstes Fenster, größte Textstufe, kein verdeckter Fokus, Assistenztechnik-Grenzen |

| Feature-Kriterium | Gewicht | Bewertet wird |
| --- | ---: | --- |
| Korrektheit | 25 % | Galerie, Resume, Undo, Profile, Bibliothek funktionieren auch nach Zustandswechseln |
| Datenintegrität | 30 % | Fehler/Abbruch, bestehende Saves, Snapshot-Zuordnung, ROM-Isolation, persistierte Metadaten |
| Reaktionsfähigkeit | 15 % | keine Datei-/Backup-Leseoperationen im Renderpfad, begrenzte Arbeit, veraltete Antworten |
| Vollständigkeit und Bedienbarkeit | 20 % | zusammenhängende Benutzerabläufe, sinnvolle Fehler-/Leerzustände, verständliche Semantik |
| Linux-Integration | 10 % | Paket/Launcher, Isolierung, reproduzierbare kurze native Prüfung |

Ein reproduzierbarer Datenverlust oder falsches ROM-Ziel blockiert die
Feature-Abnahme unabhängig vom Mittelwert. Eine überlappte oder unerreichbare
wesentliche Aktion blockiert die UI-Abnahme. Anzahl neuer Buttons, Testanzahl
und schöne Standardansichten bringen allein keine hohe Note.

## Prüfrahmen und vorliegende Baseline

Gelesen: Linux-Handoff, vorherige Kritik, nativer SDL-Host, Save-Store/-Tools,
Bibliothek und UI-/Patch-Lab-Tests. Der Skill `ui-ux-pro-max` wurde mit der
passenden Abfrage `keyboard focus modal` verwendet; sie bestätigt sichtbaren,
unverdeckten Fokus. Mobile Touch-Maße und Web-ARIA werden nicht blind auf SDL
übertragen.

Drei vorhandene Bilder angesehen: `artifacts/ui-patch-lab/patch-compact.png`,
`artifacts/ui-final-candidate/input.png` und `tall-window.png`. Die beiden letzteren
sind **historische** Bilder, keine aktuelle Abnahme; insbesondere der dort sichtbare
Controller-Setup-Überlapp wurde bereits vor dieser Runde behoben. Die frühere
Gesamtnote 8,2/7,9 wird weder als frische Prüfung noch unter geänderten Gewichten
weitergeführt. Endnoten folgen nach dem tatsächlichen neuen Stand.

### Konkrete Ausgangsmängel

- `WaylandEmulatorHost.cs:82`: `HasSelectedState` ruft `File.Exists` aus mehreren
  Renderpfaden auf. `DrawSavesPage` ab Zeile 1391 prüft zusätzlich alle Slots und
  ihren Zeitstempel. Ein langsamer/nicht erreichbarer Datenträger trifft die UI.
- `WaylandEmulatorHost.SaveTools.cs:23`: `BatterySaveStore.Inspect` läuft während
  `DrawBackupPage`; die Inspektion prüft tatsächliche Dateien statt eines
  vorbereiteten unveränderlichen Ergebnisses.
- `WaylandEmulatorHost.Library.cs:40`: `File.Exists(entry.Path)` erfolgt pro
  sichtbarem Eintrag und Frame. Der Speicherort einer ROM kann ein langsames
  externes Laufwerk sein.
- `LinuxLibrary.cs:18`: `Remember` ersetzt den bisherigen Katalogeintrag vollständig.
  Neue benutzerdefinierte Titel/Favoriten/Spielzeit müssen beim Öffnen und
  Neuzuordnen ausdrücklich erhalten werden.
- `WaylandEmulatorHost.AetherUi.cs:299`: gemeinsame Seitenfläche hat feste Höhe,
  neun Navigationseinträge feste Positionen. Größere Schrift benötigt echte
  Layoutprüfung, nicht nur größere Glyphen.
- Der SDL-Zeichenbaum besitzt weiterhin keine nachgewiesene semantische AT-SPI-
  Anbindung. Sichtbarer Tastaturfokus ist kein Screenreader-Ersatz.

Zeilenangaben oben beziehen sich auf die Baseline vor dieser Runde.

## Adversariale Abnahmekriterien

1. Cache-Anfragen sind an ROM-Identität/Generation gebunden. Spät fertige ROM-A-
   Daten dürfen nach Wechsel auf ROM B nichts überschreiben. Loading, Fehler und
   leer sind verschiedene Zustände; Fehler dürfen nicht als freier Slot erscheinen.
2. State und Vorschaubild gehören zum gleichen Snapshot. Fehler beim Schreiben
   beschädigen keinen bestehenden Slot. Fehlgeschlagenes Load zerstört weder
   laufenden Zustand noch bisheriges Undo. ROM-Wechsel/Neustart invalidiert Undo.
3. Resume überschreibt keine manuellen Slots. Das letzte Spiel kann fehlen oder
   verändert sein. Abgebrochene Starts dürfen bestehende Resume-Daten nicht
   als erfolgreiche Sitzung ersetzen.
4. Spielprofile isolieren ROM A, ROM B und globale Defaults. Deaktivieren stellt
   globale Werte wieder her. Belegung und Audio/Darstellung müssen beim Wechsel
   tatsächlich angewandt werden, nicht nur im JSON stehen.
5. Öffnen/Relocate/Patch-Reuse erhält Titel und Favoriten. Spielzeit zählt aktive
   Emulation; Pause, Settings und Fokus-Pause zählen nicht mit. Kaputter Katalog
   bleibt reparierbar und behindert andere Einträge nicht.
6. 900×650 sowie größte angebotene Textstufe: Galerie, Bibliothek und Input ohne
   verdeckte Texte/Aktionen; lange Namen, leere und Fehlerzustände berücksichtigen.
   Navigation bleibt bei aktualisierten asynchronen Listen vorhersehbar.
7. Tastatur-/virtueller Controllerpfad für neue Aktionen; kein unsichtbares
   Textfeld fängt spätere Shortcuts ab. Busy-Aktionen dürfen nicht doppelt starten.
8. Paketstart ohne installierte .NET-Runtime und mit Leerzeichen im Pfad.

Keine 30-Minuten-Läufe: der Nutzer übernimmt diese selbst. Reale Spiele,
hörbarer Ton, physische Controller, Mixed-DPI, ARM64 und Windows-Laufzeit werden
nur dann als geprüft bezeichnet, wenn dafür neue direkte Belege vorliegen.

## Erster neuer Kandidat: ernsthafte Schwächen gefunden und nachverfolgt

Die Codeprüfung fand mehrere echte Übergangsfehler, bevor eine Endnote vergeben
wurde. Sie wurden dem Implementierer direkt mitgeteilt:

- **Profilwechsel innerhalb der Settings-Debounce:** eine globale Änderung konnte
  beim sofortigen Aktivieren eines Profils versehentlich zur Spielausnahme werden.
  Ein erzwungener erfolgreicher Flush vor Scope-Wechsel und der zugehörige native
  Regressionstest beheben dies.
- **Veraltete Bibliotheksantwort:** eine Änderung während laufender Aktualisierung
  konnte ihre anschließende Aktualisierung verlieren. Ein vorgemerkter neuer Lauf
  und im Hintergrund serialisierte Mutationen vermeiden diese verlorene Anfrage.
- **Unbegrenzte Save-Scans:** wiederholtes Öffnen der Save-Seite startete vorher
  neue Hintergrundtasks, obwohl alte noch liefen. Jetzt höchstens ein aktiver
  Scan und ein zusammengefasster Folgeauftrag; alte ROM-Resultate werden verworfen.
- **Späte Titel-Speicherung und Texteingabe:** der Callback konnte die Texteingabe
  eines inzwischen anderen Editors stoppen. Der Abschluss ist jetzt an die
  ursprüngliche Titel-Edit-Version gebunden.
- **GBC-Dateien mit `.gb`-Endung:** reine Extension-Erkennung hätte den Systemfilter
  falsch gemacht. Neu erkannte Hardware-Metadaten berücksichtigen das Headerbit.

Die ersten fünf frischen Captures wurden tatsächlich angesehen, ohne sichtbare
Fenster zu öffnen: Galerie und Bibliothek bei Standardgröße sowie Input, System
und Display bei 900×650 und Textstufe 18. Ursprünglicher Ausgabeort war wegen des
Test-Working-Directory `tests/AetherBoy.DesktopTests/bin/Release/net10.0/artifacts/comfort-review/`;
anschließend nach `artifacts/comfort-review/` kopiert. Die Normalansichten sind
ruhig, die Galerie zeigt korrekt getrennte Resume-/Manual-Slots, Paletten haben
nun echte Farbstreifen und Profilscope wird auf Display/Input sichtbar.

**Noch kein UI >9:** vier Kernaktionen werden bei großer Textstufe abgeschnitten
(`CONTROLLER SET…`, `Reset keyboard def…`, `IMPORT BOOT ROM / BI…`,
`SHARP · PIXEL P…`). Die Textgröße wird als Pixelzahl bezeichnet, obwohl diese
logisch und am kleinen Fenster heruntergerechnet ist. Seiteninhalte erreicht
man per Tab erst nach der gesamten Sidebar. Async-Aktualisierung verhindert
falsche Aktionen durch Fokus-Reset, verliert damit aber auch den bisherigen
Bedienplatz. Galerie/Bibliothek brauchen noch ihre größte Textstufe als Sichttest.

Vorläufige Einordnung dieses Kandidaten: UI ungefähr **8,3**, Features ungefähr
**8,4**. Diese sind ausdrücklich **keine Endnoten**: Paket- und Gesamtlauf sowie
Nachprüfung der gemeldeten UI-Funde standen zu diesem Zeitpunkt noch aus.

## Abschluss der unabhängigen Bewertung

**UI: 8,9 / 10. Neue Features: 8,6 / 10.** Das Feature-Ziel **>8** ist im
geprüften Umfang erreicht. Das strengere UI-Ziel **>9** ist **nicht erreicht**.
8,9 wird nicht auf 9,1 angehoben, nur weil der Auftrag eine höhere Zielnote nennt.
Die Oberfläche ist deutlich verbessert und benutzbar; die verbleibende Lücke
besteht inzwischen hauptsächlich aus Zugang und Textbedienung, nicht aus einem
weiteren leicht zu verschiebenden Button.

| UI-Kriterium | Gewicht | Endnote | Begründung |
| --- | ---: | ---: | --- |
| Lesbarkeit | 20 % | 9,2 | Kürzere Kernaktionen passen; echte Palettenvorschau; drei tatsächlich unterschiedliche Textstufen auch bei 900×650. Headeruntertitel ist in der letzten Bildserie noch knapp an der Trennlinie. |
| Hierarchie | 15 % | 8,9 | Klare Galerie-Aktionen, getrennte Resume-/Manual-Slots und nachvollziehbarer Profilscope; neun Sidebar-Ziele und mehrere Unterseiten bleiben relativ dicht. |
| Konsistenz | 15 % | 9,2 | Einheitliche Schaltflächen, klare Labels und Loading/Empty/Unavailable; direktes Continue ergänzt die vorher versteckte Funktion. |
| Navigation und Feedback | 25 % | 9,0 | Geprüfter F6-Regionswechsel, stabiler Fokus bei asynchronen Listen und sichtbares Ctrl+A; Texteditor besitzt weiterhin keinen vollständigen Cursor-/Clipboard-/IME-Komfort. |
| Skalierung und Zugänglichkeit | 25 % | 8,2 | Größte Stufe und kleine Fenster sind brauchbar, wesentliche Aktionen bleiben sichtbar. Keine nachgewiesene AT-SPI-Semantik, keine Mixed-DPI- oder vollständige physische Controllerabnahme. |
| **UI gesamt** | **100 %** | **8,9** | **>9 nicht erreicht** |

| Feature-Kriterium | Gewicht | Endnote | Begründung |
| --- | ---: | ---: | --- |
| Korrektheit | 25 % | 8,8 | Save/Load/Undo/Resume sowie ROM-Wechsel und Profile mit konkreten Zustandsvergleichen geprüft. |
| Datenintegrität | 30 % | 8,9 | Atomische States, gebundene Preview-Sidecars, ROM-Isolation, retryfähige Settings und Erhalt fehlgeschlagener Resume-Daten. Keine beliebige Stromausfall-/Filesystem-Abnahme. |
| Reaktionsfähigkeit | 15 % | 8,3 | Begrenzte Hintergrund-Scans und Mutationen entfernen den Render-I/O; explizites ROM-Öffnen/Hashing und manche Abschluss-/Einstellungsoperationen können weiter synchron warten. |
| Vollständigkeit und Bedienbarkeit | 20 % | 8,4 | Zusammenhängende neue Abläufe inklusive direktem Continue, Profile, Favoriten, Titel, Spielzeit. Galerie ist ein Slot-Inspektor mit einzelner Vorschau, keine frei sortierbare Screenshot-Sammlung. |
| Linux-Integration | 10 % | 8,4 | Selbstenthaltendes x64-Paket startet ohne dotnet im PATH; ARM64 erfolgreich paketiert, Ausführung korrekt als ungeprüft markiert. Tarball ist noch kein AppImage/Flatpak. |
| **Features gesamt** | **100 %** | **8,6** | **>8 erreicht** |

### Tatsächlich zusätzliche Nachweise

Die aktualisierten sechs Bilder unter `artifacts/comfort-review/` wurden alle
gezielt angesehen: Galerie, Bibliothek mit langem Titel, Input, System/Profil,
Display und Rename mit sichtbarer Vollauswahl. Alle sechs in dieser letzten Serie
laufen bei 900×650 mit der größten Textstufe; deren wirksames Minimum beträgt
jetzt 20 logische Pixel. Keine wesentliche Aktion ist überlappt oder abgeschnitten.
Der volle ROM-Titel wird in der Liste absichtlich gekürzt und im Editor angezeigt.

Der Reviewer hat die TRX-Zähler selbst gelesen:
`artifacts/comfort-playtest/AetherBoy.DesktopTests_net10.0_x64.trx` enthält
**19 ausgeführte / 19 bestandene / 0 fehlgeschlagene** fokussierte Fälle.
Zusätzlich zu den vorherigen Fällen prüfen diese den Erhalt eines fehlgeschlagenen
Home-Resume beim Schließen, F6-Navigation, Fokus bei Indexverschiebung und
identisch benannten umsortierten ROMs sowie Ctrl+A/Ersetzen. Ein echter
Seitennavigationsfehler — Sidebar-Input blieb auf der Controller-Unterseite —
wurde in diesem Lauf gefunden und korrigiert.

Der neu gefundene Resume-Datenfehler ist besonders relevant:
`WaylandEmulatorHost.Comfort.cs` setzt nach erfolglosem Resume-Laden einen Schutz,
der periodisches und abschließendes Überschreiben verhindert. Auch ein von einem
neueren Build stammendes, hier unlesbares Resume bleibt erhalten. Ein erfolgreicher
anderer Load beziehungsweise ausdrücklich gewähltes Update kann den Schutz wieder
aufheben. Ein Test vergleicht die ursprünglichen Bytes nach dem Schließen.

Paketlogs wurden gelesen: `artifacts/linux-package-x64.log` bestätigt Archiv,
Quellen/Lizenzen, Sonderzeichenpfade und gebündelten Runtime-Start;
`artifacts/linux-package-arm64.log` bestätigt das ARM64-Archiv und benennt den
Ausführungs-Skip auf x64. Nachfolgende finale Build-/Gesamttest- und Paketläufe
werden im Playtest/Handoff separat dokumentiert; ihre Zahlen werden hier nicht
als bereits von diesem Reviewer ausgeführte Tests ausgegeben.

### Verbleibende Prioritäten ohne Schönreden

1. **Zugang und Textbedienung:** semantische Assistenztechnik und bessere native
   Texteingabe (Cursor, Auswahl, Clipboard, IME) sind substanzielle Folgeschritte
   für eine UI über 9. Sichtbares Ctrl+A ist ein guter kleiner Fix, kein vollständiger
   Texteditor. Mehr dekorative Screenshots würden diese Lücke nicht schließen.
2. **Langsame Dateisysteme bei expliziten Aktionen:** weitere blockierende
   Identitäts-/Öffnungs- und Flush-Arbeit aus der Hauptschleife herauslösen,
   mit Abbruch- und Busy-Verhalten. Die Aussage „kein Datei-I/O beim Rendern“
   ist enger und zutreffender als „alles ist asynchron“.
3. **Reale Desktop-Qualifikation:** physischer Controller, Mixed-DPI und hörbarer
   Ton bleiben offen. Im Kurzlauf wurde das virtuelle Controller-Szenario wegen
   des bereits angeschlossenen Nutzergeräts übersprungen; das Gerät wurde nicht
   getrennt. Die Nutzersitzung wurde nicht gestört.
4. **Kleine visuelle Reststelle:** mehr Luft unter dem Headeruntertitel in der
   größten Textstufe. Diese Stelle allein verhindert oder begründet keine
   Gesamtnote; ein kosmetischer Fix hebt die Note nicht automatisch über 9.

Keine weiteren Langläufe gestartet. Keine neue sichtbare App auf dem aktiven
Workspace geöffnet. Der Nutzer übernimmt wie vereinbart lange und echte Spieltests.


Nachtrag der koordinierenden Instanz: Der Headeruntertitel wurde anschließend
acht logische Pixel angehoben. Die aktualisierte Input-Aufnahme bei 900×650 und
größter Schrift wurde angesehen; der Text liegt nun oberhalb der Trennlinie.
Das ändert die unabhängige Endnote nicht. Finale Integrationszahlen und frisch
gebaute Pakete sind im Playtest-Bericht und in der Linux-Handoff dokumentiert.

## Anschließender Nutzerauftrag: verbleibende Softwarepunkte schließen

Die vorstehenden **8,9 / 8,6** bleiben die abgeschlossene Bewertung des dort
geprüften Stands. Der Nutzer hat danach die Umsetzung der verbleibenden Punkte
beauftragt. Die folgende Zusatzprüfung darf diese Noten erst nach neuen Belegen
ersetzen; der bloße Einbau von GTK oder mehr Texttasten verdient keine neue Note.

### Vorab-Anforderungen: Texteingabe

- Cursor links/rechts und Home/End, Shift-Auswahl, Delete/Backspace und Ctrl+A.
- Ctrl+C/X/V über die tatsächliche SDL-Clipboard-Anbindung; Paste ersetzt eine
  Auswahl atomisch und respektiert Feldlimit/Zeilenregeln. Ein Fehler beim
  Schreiben der Zwischenablage darf bei Cut keine ausgewählten Daten löschen.
- Cursor und Löschung teilen weder UTF-16-Surrogate noch kombinierte sichtbare
  Zeichen. Auswahl und Cursor sind sichtbar; lange Eingaben folgen dem Cursor.
- IME-Vorkomposition ist getrennt vom gespeicherten/gesuchten Text; erst Commit
  ändert diesen. Escape, Fokusverlust, Seitenwechsel und Schließen hinterlassen
  keine verborgene Komposition. Offset-Einheiten der SDL-Events werden korrekt
  in die verwendeten Textindizes umgesetzt.

### Vorab-Anforderungen: optionaler semantischer GTK-Pfad

- Ohne Maus erreichbar; echte semantische Rollen, Namen, aktuelle Werte,
  Auswahlzustände, Aktivierbarkeit und Fokus. Fehler/Status sind zugänglicher
  Text, nicht ausschließlich farbliche SDL-Markierungen.
- Aktionen verwenden dieselben Sitzung-/Speicherschutzpfade wie die Haupt-UI;
  Datenänderungen synchronisieren korrekt in beide Richtungen. GTK-/SDL-Thread-
  Besitz bleibt korrekt, Schließen/Dispose ist wiederholbar sicher.
- Abgedeckte und noch nicht abgedeckte Funktionen werden ausdrücklich benannt.
  Ein Zusatzfenster mit wenigen benannten Buttons beweist keine vollständig
  zugängliche Emulatoroberfläche.
- Fehlende optionale GTK-Bibliotheken erzeugen einen verständlichen Fallback.
  Keine stillschweigenden Systemänderungen oder Änderung fremder Accessibility-
  Einstellungen.

Read-only-Umgebungsprüfung: GTK 3.24.52, ATK/AT-SPI 2.60.6 und zugehörige native
Bibliotheken/Headers sind vorhanden; `dbus-run-session`, `gdbus` und
`/usr/lib/at-spi-bus-launcher` ebenso. In-Process-ATK-Prüfungen können Rollen und
Actions testen. Für den stärkeren Beleg einer tatsächlich sichtbaren AT-SPI-
Anwendung ist ein **privater D-Bus plus isolierter headless Wayland-Compositor**
vorgesehen. Der persönliche Accessibility-Bus wird nicht umkonfiguriert.
