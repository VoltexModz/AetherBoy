# Unabhängige Linux-Kritik

Stand: 11. September 2026. Baseline: `d983ed4`. Die Bewertung ist ein
Engineering-Review, keine repräsentative Nutzerstudie. Zielwerte sind **UI > 8**
und **vorhandene Features > 6**; sie werden nicht als erreichte Ergebnisse
vorausgesetzt. Neue Features erhöhen die Note nicht automatisch.

**Aktuelles Ergebnis nach vier Prüfungen: UI 8,2 / 10; vorhandene Features
7,9 / 10.** Beide gewünschten Schwellen sind im unten beschriebenen Prüfrahmen
erreicht. Baseline, Zwischenbefunde und Nachweise bleiben zur Nachvollziehbarkeit
erhalten; reale Spiel-, Hardware- und Barrierefreiheitstests sind dadurch nicht
automatisch abgenommen.

## Evidenz und Grenzen

Der Reviewer hat den aktuellen SDL-Code, die Linux-Roadmap und den Skill
`ui-ux-pro-max` gelesen. Die passende Skill-Abfrage `keyboard focus visible`
bestätigt sichtbaren Fokus auf interaktiven Elementen. Mobile Touch-Mindestmaße
und Web-ARIA-Regeln werden nicht unverändert auf diese SDL-Desktop-App übertragen.

Der bestehende Build wurde ohne Neubau nativ auf Wayland geprüft:

```sh
AETHERBOY_UI_TESTS=1 \
AETHERBOY_UI_CAPTURE_DIR="$PWD/artifacts/ui-critique-baseline" \
dotnet test --project tests/AetherBoy.DesktopTests --no-build --no-restore
```

Ergebnis: **38/38 Desktop-Tests bestanden**, inklusive des nativen Tests mit
generierter GB-ROM, Tastatur, Einstellungen, Save/Load, Pause und Rendering.
Fünf Bilder wurden gezielt angesehen: `main`, `display`, `input`, `saves` und
`tall-window`. Der bestehende Test erzeugt elf Seiten-/Zustandsbilder; die übrigen
wurden nicht zur visuellen Bewertung herangezogen. Ablage:
`artifacts/ui-critique-baseline/`. Kein Screenshot-Polling, keine Aufnahme des
gesamten Desktops und keine fremden ROM-Dateien.

Nicht dadurch nachgewiesen: tatsächliche Audioqualität, kommerzielle
Spielkompatibilität, Hardware-Controller, Misch-DPI auf mehreren Monitoren,
Screenreader-Zugriff oder 30-Minuten-Stabilität. Diese Lücken sind echte
Abnahmegrenzen, kein implizites Bestehen.

## Feste Bewertungsrubrik

0 bedeutet unbenutzbar/fehlend, 3 erhebliche Hindernisse, 5 brauchbar mit häufigen
Reibungen, 7 zuverlässig mit klaren Schwächen, 9 ausgereift und gut geprüft,
10 praktisch keine relevanten Mängel im geprüften Umfang. Gewichtete Mittelwerte
werden auf eine Nachkommastelle gerundet. Rubrik und Gewichte bleiben für die
Nachprüfung unverändert.

| UI-Kriterium | Gewicht | Baseline | Begründung |
| --- | ---: | ---: | --- |
| Lesbarkeit | 25 % | 7,0 | Noto und guter Haupttextkontrast; viele sehr kleine Labels, violette Kontrastschwäche |
| Hierarchie | 20 % | 8,0 | Klare Spielbühne und primäre Aktion; unnötige technische Labels und doppelte Navigation |
| Konsistenz | 15 % | 7,0 | Einheitliche Panel-/Button-Sprache; Deutsch/Englisch-Mix, abweichende Seitentexte |
| Navigation und Feedback | 25 % | 6,0 | Funktionierender Control-Center-Fokus; zwei konkurrierende Eingabefokusse, kryptische Auswahlwerte und veraltete Statusmeldungen |
| Skalierung und Zugänglichkeit | 15 % | 5,5 | Dynamische Bühne und erreichbare Controls im Hochformat; schrumpfende Schrift, keine Textskalierung oder belegte Assistenztechnik-Anbindung |
| **UI gesamt** | **100 %** | **6,7 / 10** | **Ziel noch nicht erreicht** |

| Feature-Kriterium | Gewicht | Baseline | Begründung |
| --- | ---: | ---: | --- |
| Kernabläufe: ROM, Darstellung, Pause, State, Rewind | 30 % | 7,5 | Solider gemeinsamer Kern und automatisierte Abläufe; reale Spielabdeckung begrenzt |
| Datenintegrität und Wiederherstellung | 30 % | 6,0 | Atomare Saves vorhanden; ROM-nahe Pfade, Kollisions-/Schreibschutzrisiken und keine Restore-UI |
| Desktop-Integration und Bedienbarkeit | 20 % | 7,0 | Wayland, Portal, Tastatur und Gamepad-Hotplug; Starter-/Update-Risiken und feste Gamepad-Belegung |
| Audio, Fehlerdiagnose und Betriebsstabilität | 20 % | 6,0 | Audio-Backend vorhanden; Mono, grobes Queue-Leeren und fehlende persistente Diagnose |
| **Features gesamt** | **100 %** | **6,7 / 10** | **Numerisch über 6, aber Langzeit-/Hardwareabnahme offen** |

## Priorisierte UI-Befunde

### U1 — P1: Kleine Fenster verkleinern ohnehin kleine Schrift

`UpdateLayout` skaliert die gesamte 1180×760-Basisfläche nach unten. Die erlaubte
Mindestbreite beträgt 860. Ein 10-Pixel-Label wird damit ungefähr 7,3 Pixel groß,
ein 12-Pixel-Buttontext ungefähr 8,7 Pixel. Das 900×1050-Bild zeigt diesen Effekt
bereits deutlich: mehr Höhe führt nicht zu besser lesbaren Controls.

**Änderung:** Lesbare Mindesttextgröße und eine bewusste kompakte Darstellung
definieren. Zumindest wichtige Button-/Status-/Hilfetexte vergrößern und die
Mindestgeometrie ehrlich festlegen; langfristig Inhalte umbrechen oder scrollen,
statt die komplette Bedienoberfläche zu schrumpfen. Eine höhere Mindestgröße
allein hilft kleinen Laptop-Arbeitsflächen nicht.

**Abnahme:** 860×554 beziehungsweise die neu dokumentierte Mindestgröße,
1180×760 und Hochformat prüfen; 125/150/200 % Skalierung separat qualifizieren.

### U2 — P1: Zwei Eingabefokusse widersprechen sich

Auf der Input-Seite setzt Tab `focusedControl`. Pfeiltasten ändern danach nur
`focusedBinding`. Enter aktiviert zuerst den bisherigen `focusedControl`, obwohl
die Pfeile inzwischen eine andere Belegung hervorheben können. Beispiel:
Input öffnen → Tab auf Schließen → Pfeil nach unten → Enter. Die Oberfläche
kann schließen, obwohl die Belegung als aktiver Pfeiltastenfokus erscheint.

**Änderung:** Ein einziges aktives Fokusmodell oder expliziter Wechsel zwischen
Tab- und Pfeilmodus. Beim Wechsel veralteten Fokus löschen. Hauptfenster-Aktionen
brauchen eine dokumentierte Fokusroute; Tab ist dort bereits Turbo und darf
nicht ohne Migrationsentscheidung umgewidmet werden.

**Abnahme:** Gemischte Tab-/Pfeil-/Enter-Sequenzen einschließlich Shift+Tab
ausführen und Zielaktion prüfen; nicht nur einzelne Tasten isoliert testen.

### U3 — P1: Violett erfüllt für kleine Texte nicht den Kontrastmaßstab

Aus den tatsächlichen sRGB-Farbwerten berechnet: `Void` (5,7,18) auf `Violet`
(139,56,255) ergibt **3,93:1**. Derselbe Wert gilt für den violetten Shortcuttext
auf dunklem Hintergrund. Für normalen Text dient 4,5:1 als Prüfmaßstab.
`Muted` auf `Surface` erreicht dagegen **6,28:1** und ist keine Hauptschwäche.
Die Buttons enthalten einen Verlauf; der Wert beschreibt dessen violettes Ende,
nicht den Mittelwert des Verlaufs oder jeden einzelnen Buchstaben.

**Änderung:** Violett für Text/Verlauf ausreichend aufhellen oder sichere
Textflächen verwenden; Renderfarben im Gradient-Helper mitändern, nicht nur
den Farbtoken. Fokusindikator und ausgewählte Werte zusätzlich eindeutig halten.

### U4 — P2: Display-Werte erklären ihre Wirkung nicht

`P1` bis `P5` sind ohne Vorschau oder Namen nicht unterscheidbar. Frameskip
`0/1/2` erklärt weder den Standard noch den sichtbaren Kompromiss. Auswahl
funktioniert, verlangt aber unnötiges Probieren und Vorwissen.

**Änderung:** Paletten benennen und mit vier echten Farben zeigen; Frameskip
kurz erklären. Filterauswahl mit verständlicher Wirkung beschreiben.

### U5 — P2: Saves sind zu wenig überprüfbar

Der Belegtindikator ist ein nicht erklärter Punkt. Zeitpunkt und aktiver Pfad
fehlen. Load bleibt bei laufender Session auch für einen leeren Slot aktiv.
Eine UI-Zeile beschreibt den alten ROM-nahen Speicherort und muss mit L1
gemeinsam aktualisiert werden.

**Änderung:** `Leer`/`Belegt`, Änderungszeit und konkretes Ziel anzeigen; leeren
Load deaktivieren und den Grund zeigen. Vorhandene Backups sichtbar machen,
Restore erst nach Validierung und mit Sicherung des aktuellen Saves anbieten.

### U6 — P2: Sprache und Status sind nicht situationsbezogen genug

Deutschsprachige Erklärungen stehen neben englischen Hinweisen und technischen
Metaphern wie `TUNE`, `TOOLS`, `OPERATOR DECK`. Das spart keine Orientierung.
`A is now V. Occupied keys are swapped.` bleibt beim Wechsel zu Saves stehen.
`CORE 4.8` ist fest eingebaut und keine verlässliche Buildidentität.

**Änderung:** Sichtbare Bedienbegriffe vereinheitlichen, Navigationsziele klar
benennen, Statusmeldungen zur Seite/Aktion passend zurücksetzen oder als letzten
Vorgang kennzeichnen. Versionsdaten aus dem Build beziehen.

### U7 — P2: Assistenztechnik und Systemtextgröße sind nicht belegt

Die UI besteht aus SDL-Zeichenoperationen und geometrischen Hit-Targets. Im
geprüften Code ist keine semantische AT-SPI-Brücke erkennbar. Ein sichtbarer
Fokusrahmen ersetzt keine Screenreader-Rollen, Namen oder Werte.

**Änderung:** Textskalierung und Tastaturpfade kurzfristig verbessern;
Assistenztechnik als eigenes Architekturthema behandeln. Keine pauschale
Behauptung vollständiger Barrierefreiheit aufgrund grüner Screenshot-Tests.

## Kritik an Plan, Architektur und Umsetzungssicherheit

1. **Daten vor Komfort:** L1 ist richtig priorisiert. Ein Save-Verlust wiegt
   schwerer als zusätzliche Bibliotheksfunktionen. Migration braucht vollständige
   zusammengehörige Save-/RTC-/Guard-/Backup-Sätze, Wiederholbarkeit und Schutz
   gegen zwei Instanzen. Ein Hash allein löst konkurrierendes Schreiben nicht.
2. **L7 beginnt sofort:** Die Roadmap darf nicht als sieben große, nacheinander
   erledigte Pakete gelesen werden. Tests, Playtest und unabhängige Kritik laufen
   ab dem ersten Speicherumbau mit. UI-P1 ebenfalls vor Komfortumfang beheben.
3. **Installationsrollback wirklich testen:** Ein frisch erzeugter Publish-Ordner
   ist kein Beweis für ein atomisches Update. Launcher, vorhandene Nutzerdaten,
   Leerzeichen und absichtlich fehlgeschlagene Aktivierung sind Abnahmekriterien.
4. **Messung vor Audio-Umbau:** Queue-Länge misst keine Ende-zu-Ende-Latenz.
   Synthetische Samples beweisen nicht hörbar guten Ton. Stereo betrifft den
   gemeinsamen Runtime-Kontrakt und beide Frontends; Linux-Tests allein reichen
   dafür nicht. Ein riskanter gemeinsamer Umbau ist kein sinnvoller Quick Win.
5. **Host bleibt zu stark gekoppelt:** Rendering, Eingabe, Emulationslebenszyklus,
   Speicherpfade und Fehlerausgabe sind stark verbunden. Die neuen Pfad-/Diagnose-
   Dienste auslagern, statt weitere Sonderfälle in die Event-Schleife zu setzen.
   Ein großer Komplettumbau wäre während Save-Migration unnötig riskant.
6. **Hot-path-I/O vermeiden:** Die Save-Seite prüft Slot-Dateien beim Zeichnen;
   Änderungen an Katalog, Zeitstempeln und Backup-Details sollten gecacht und
   nach Aktionen aktualisiert werden. Kein Hashing oder Verzeichnisscan pro Frame.
7. **Testanzahl ist kein Qualitätsurteil:** Der native Test ruft private Methoden
   direkt auf. Das ist effizient für Navigation und Regressionen, beweist aber
   weder reale Portal-Zustellung noch vollständige SDL-Event-Routen oder reale
   Controller. Einige gezielte integrierte Tests bleiben nötig.
8. **Umfang ehrlicher schneiden:** L5 enthält mehrere eigenständige Produkte.
   BIOS, Wiederherstellung und Bibliothek einzeln abnehmen. Nicht anhand einer
   neuen Schaltfläche behaupten, dass Import, Relink und Datenrettung erledigt sind.

## Nachprüfung

Die ursprünglichen Noten bleiben als Baseline erhalten. Eine spätere Bewertung
erfasst die tatsächlich behobenen Befunde, aktualisierte gezielte Captures und
Playtest-Ergebnisse. Offene Hardware-/Langzeittests werden gesondert genannt.
Ein Zielwert gilt erst nach dieser Nachprüfung als erreicht.

## Zweite Prüfung: erste Umsetzung von L1–L4 und UI-Fixes

Geprüft wurde der aktuelle uncommittete Arbeitsstand nach dem ersten Release-Build
der Linux-Erweiterungen. Native Nachprüfung mit `--configuration Release
--no-build --no-restore`: **45 bestanden, 3 bewusst nicht aktivierte
Playtest-Fälle übersprungen, 0 Fehler**. Captures in
`artifacts/ui-critique-followup/`; erneut nur vier gezielt angesehen: `main`,
`display`, `saves`, `tall-window`. Neu entstehende Bibliotheks-/Firmwaredateien
wurden zusätzlich im Code geprüft; sie sind damit noch nicht als vollständige
Benutzerabläufe abgenommen.

| UI-Kriterium | Gewicht | Baseline | Zweite Prüfung |
| --- | ---: | ---: | ---: |
| Lesbarkeit | 25 % | 7,0 | 8,5 |
| Hierarchie | 20 % | 8,0 | 8,5 |
| Konsistenz | 15 % | 7,0 | 8,0 |
| Navigation und Feedback | 25 % | 6,0 | 7,5 |
| Skalierung und Zugänglichkeit | 15 % | 5,5 | 6,0 |
| **UI gesamt** | **100 %** | **6,7** | **7,8 / 10** |

**Verbessert:** Mindestens 14 logische beziehungsweise ungefähr 12 sichtbare
Pixel machen die Texte deutlich lesbarer. Violett wurde in Token und Verlauf
aufgehellt. Frameskip erklärt nun den Kompromiss; Paletten haben Namen. Die
Sprache ist deutlich konsistenter. Speicherplätze zeigen `EMPTY`, der aktive
Slot hat eine Erklärung, und der Speicherordner ist erreichbar. Die irreführende
fest eingebaute Core-Version ist verschwunden. Pfeile löschen den konkurrierenden
Tab-Fokus auf der Input-Seite; F6 bietet einen separaten Fokuspfad im Hauptfenster.

**Noch nicht gut genug für >8:** Im 900×1050-Capture werden die oberen Ziele zu
`LIBRA…`, `DISPL…` und `REPO…` gekürzt. Rechts läuft `F6 ACTIONS · F5 / F8 SAVE /
LOAD` aus dem Session-Panel und teilweise aus der Fensterfläche. Ein globales
Schriftminimum braucht daher passende Buttonbreiten, kurze Labels und Umbruch;
größere Schrift allein ist noch kein fertiges responsives Layout. Farbpaletten
haben weiterhin keine tatsächliche Farbvorschau. Assistenztechnik und gemischte
DPI sind weiterhin nicht nachgewiesen.

Die Feature-Bewertung steigt vorläufig auf **7,5 / 10** (Kernabläufe 8,0;
Datenintegrität 8,0; Desktop-Integration 7,0; Audio/Diagnose/Stabilität 6,5,
unveränderte Gewichte). Zentrale hashbasierte Speicherung, unveränderte Originale,
ein ROM-Schreibbesitz, Einstellungs-Backup, lokales begrenztes Logging und
getrennte Programm-Releases lösen wesentliche ursprüngliche Schwächen.
Stereo erhält nun die GBA-Kanäle, während der vorhandene Windows-Monokonsument
einen kompatiblen Downmix bekommt. Das ist eine sinnvolle Übergangslösung, aber
keine Windows-Laufzeitabnahme. Queue-Dropping muss weiterhin hörbar und über
Zeit geprüft werden; die neue Strategie ist nicht allein durch ihren Code
als besser bewiesen.

### Neue beziehungsweise verbleibende konkrete Risiken

| ID | Priorität | Befund und notwendige Korrektur |
| --- | --- | --- |
| N1 | P1 | `LinuxLibrary.Read` dereferenziert `entry.Identity.Length`, obwohl JSON die Property auslassen oder auf `null` setzen kann. Korrupte lokale Katalogeinträge dürfen das Öffnen der Library nicht abbrechen. Null-/Pfadvalidierung und Regression erforderlich. |
| N2 | P1 | Bei aktiver Library-Suche setzt `CloseControlCenter` weder `editingSearch` zurück noch beendet es SDL-TextInput. Nach Schließen per Maus kann die Spielsteuerung weiter von der versteckten Suche abgefangen werden. Schließen muss Such- und Fokuszustand aufräumen. |
| N3 | P2 | `TryLoadRom` kehrt bei gleichem ROM-Hash vor `library.Remember` zurück. Wird die laufende ROM verschoben und am neuen Ort geöffnet, bleibt der Katalog auf dem alten Pfad. Den Pfad aktualisieren, ohne eine zweite Session oder Schreibsperre zu öffnen. |
| N4 | P1 | Der Installer ersetzt den alten Launcher vor der `current`-Aktivierung. Bei Migration aus dem alten Layout kann ein anschließender Icon-/Desktop-/Aktivierungsfehler den bisherigen Starter unbrauchbar machen. Alte Integration bis zum Commit erhalten oder bei Fehler vollständig zurückrollen. |
| N5 | P2 | Große Schrift überschreitet feste Panel-/Buttonbreiten im Hochformat. Kurze eindeutige Labels, Umbruch und passende Geometrie müssen gemeinsam geprüft werden. |
| N6 | P2 | Firmware wird nach Dateigröße klassifiziert. Das ist eine Größenprüfung, keine Garantie gültigen Firmware-Inhalts. Die Oberfläche und Abnahme dürfen nicht mehr versprechen; passende Boot-/State-Kombinationen brauchen Tests. |

Diese Befunde wurden früh an die Umsetzung und die Playtest-Agenten übergeben.
Ein späterer Fix ersetzt die hier dokumentierte Momentaufnahme nicht rückwirkend;
er erhält eine eigene Nachprüfung.

## Dritte Prüfung: korrigierter UI-Kandidat und neue Werkzeuge

Drei Bilder aus `artifacts/ui-final-candidate/` wurden angesehen: `main`,
`saves`, `tall-window`. Die breiteren Headerbuttons sind nun auch im 900-Pixel-
Hochformat vollständig lesbar; die Session-Hilfe bleibt innerhalb des Panels.
Der Speicherbereich ergänzt einen klaren Zugang zu Backups/Export. Die früheren
Library-Nullzugriffe, der nicht aktualisierte Relocate-Pfad und der versteckte
Suchzustand beim Schließen sind im Code korrigiert. Der Installer aktiviert
eine vorhandene neue Release-Struktur vor dem Ersetzen alter Starter, sodass
der konkret gemeldete Migrationsfehler vermieden wird. Das ist weiterhin keine
Zusicherung atomischer Rollbacks aller Desktop-/Icon-Dateien.

Die Ergebnisdatei unter `artifacts/playtest-current/` bestätigt **50 Tests,
50 bestanden, 0 fehlgeschlagen**. Das ist ein stärkerer Funktionsnachweis als
ein Screenshot; neue danach ergänzte Dateien sind davon nicht automatisch
abgedeckt. Der Playtest enthält inzwischen gezielte Such-/Fokus-/Relocate-
Regressionen und Batterie-Restore mit Sicherung des vorherigen Zustands.

Neue Tools-, Library-, Controller- und Backup-Unterseiten wurden in dieser
Runde im Code geprüft. Ihre eigentlichen Seitenbilder fehlen noch in dieser
Capture-Serie; ihre vollständige visuelle Abnahme steht deshalb aus.

Zwei weitere konkrete Tastaturprobleme wurden vor der Endabnahme gemeldet:

- **N7, P1:** `LinuxKeyBindings.CanBind` erlaubt F6, obwohl F6 nun global den
  Hauptfensterfokus bewegt. Eine auf F6 gelegte Spielaktion wird abgefangen.
  Die neue reservierte Taste muss auch beim Laden alter Einstellungen sinnvoll
  behandelt werden.
- **N8, P2:** Die Controller-Unterseite wird unter `ControlCenterPage.Input`
  gerendert. Die normale Pfeil-/Enter-Logik für Tastaturbelegungen prüft noch
  nicht `!showController` und kann eine unsichtbare Tastatur-Neubelegung starten.
  Sie darf ausschließlich auf der Tastatur-Unterseite aktiv sein.

Die visuelle Qualität der erneut geprüften Hauptseiten erreicht jetzt einen
plausiblen Kandidatenstand über 8. Die endgültige Gesamtnote bleibt bis zur
Prüfung dieser Tastaturkorrekturen und der neuen Seiten offen; fehlende Evidenz
wird nicht durch eine günstigere Gewichtung ersetzt.

## Endprüfung und Bewertung

Die letzte gezielte Bildstichprobe umfasst `library.png`, `tools.png` und
`controller.png` aus `artifacts/ui-final-candidate/`. Keine wiederholte
Screenshot-Schleife. Die bereits geprüften Hauptseiten und Hochformatansichten
bleiben die Vergleichsbasis. Der neue Backup-Unterbereich wird durch Codeprüfung
und den tatsächlichen Restore-/Archiv-Test bewertet; er wurde in dieser letzten
Dreier-Bildstichprobe nicht zusätzlich visuell geprüft.

`artifacts/playtest-current/AetherBoy.DesktopTests_net10.0_x64.trx` wurde erneut
unabhängig ausgelesen: **56 ausgeführt, 56 bestanden, 0 Fehler, 0 übersprungen**.
`artifacts/linux-install-test.log` meldet erfolgreiche Tests des echten Installers
für alte Installationslayouts, kontrollierte Fehler, Leerzeichen/Sonderzeichen,
Minimal-PATH und Deinstallation mit Erhalt der Nutzerdaten. Der zugehörige
Testcode `scripts/test-linux-install.py` wurde gelesen. Alle Tests liefen in
isolierten Daten-/Programmverzeichnissen.

F6 ist nun reserviert; vorhandene F6-Belegungen werden gezielt migriert, statt
die gesamte Nutzerbelegung zurückzusetzen. Die Tastatur-Pfeillogik gilt nur noch
auf der Tastatur-Unterseite, nicht auf der Controller-Unterseite. Der Recorder
erklärt, dass nach Start die Einstellungen geschlossen werden müssen, damit
die pausierte Emulation wieder Audio produziert. Die gemeldeten funktionalen
Blockaden N1, N2, N3, N7 und N8 sind damit behoben. N4 ist im konkret getesteten
Migrations-/Fehlerfall behoben; N5 ist auf den geprüften Hauptansichten behoben.
N6 bleibt eine dokumentierte Validierungsgrenze.

| UI-Kriterium | Unverändertes Gewicht | Baseline | Endprüfung | Begründung |
| --- | ---: | ---: | ---: | --- |
| Lesbarkeit | 25 % | 7,0 | 8,5 | Größere Texte, sichererer Kontrast, lesbare Header im Hochformat |
| Hierarchie | 20 % | 8,0 | 8,5 | Spielbühne und Hauptaktion klar, Werkzeuge sinnvoll nach Bereichen geordnet |
| Konsistenz | 15 % | 7,0 | 8,0 | Einheitliche Sprache und Controls; einzelne kleine Leerzustands-/Abstandsfehler verbleiben |
| Navigation und Feedback | 25 % | 6,0 | 8,5 | F6-/Tab-/Pfeilpfade getrennt, Suchzustand korrekt beendet, erklärende Save-/Aufnahmestati, Restore bestätigt |
| Skalierung und Zugänglichkeit | 15 % | 5,5 | 7,0 | Nachgewiesen lesbarere kleine Fenster und klare Tastaturroute; keine belegte AT-SPI-/Systemtextskalierungsunterstützung |
| **UI gesamt** | **100 %** | **6,7** | **8,2 / 10** | **Gewichtet 8,20; Ziel >8 erreicht** |

| Feature-Kriterium | Unverändertes Gewicht | Baseline | Endprüfung | Begründung |
| --- | ---: | ---: | ---: | --- |
| Kernabläufe | 30 % | 7,5 | 8,0 | Native synthetische GB/GBC/GBA-Ausführung, Spielereingabe, Pause, State und Rewind funktionieren |
| Datenintegrität und Wiederherstellung | 30 % | 6,0 | 8,5 | Zentrale Identität, Schreibbesitz, Migration, Settings-Sicherung und echter Restore mit erhaltenem Vorher-ZIP geprüft |
| Desktop-Integration und Bedienbarkeit | 20 % | 7,0 | 7,5 | Installer praktisch getestet, Bibliothek/Reassociate und Profile vorhanden; Hardware-/Desktopbreite noch begrenzt |
| Audio, Diagnose und Betriebsstabilität | 20 % | 6,0 | 7,0 | Native Samples und GBA-Stereo, begrenzte lokale Berichte und WAV; hörbare Qualität/Gerätewechsel nicht nachgewiesen |
| **Features gesamt** | **100 %** | **6,7** | **7,9 / 10** | **Gewichtet 7,85; Ziel >6 erreicht** |

Der separate Playtest-Reviewer bewertet mit seiner eigenen, ebenfalls
dokumentierten Rubrik **7,8 / 10**. Die geringe Abweichung ist kein Widerspruch:
dieser Bericht behält seine ursprünglichen vier Feature-Kriterien unverändert.

### Verbleibende Kritik

- In der geprüften Library-Ansicht berührt die letzte Hilfszeile die untere
  Panelkante. Das ist ein kleiner Layoutfehler, keine unzugängliche Aktion.
- Die Controller-Unterseite zeigt ohne angeschlossenes Gerät prominent
  `KEYBOARD`; `No controller connected` wäre dort der bessere Leerzustand.
- Palettennamen sind verständlicher als P1–P5, echte Farbvorschauen fehlen aber.
- Neun Hauptbereiche plus Unterseiten sind für diese App noch benutzbar, werden
  bei weiterem Ausbau jedoch unübersichtlich. Neue Funktionen sollten bevorzugt
  in bestehende Bereiche passen.
- Save-/Backup-Inspektion und Dateiprüfungen erfolgen weiterhin teilweise im
  Zeichenpfad. Bei großen oder langsamen Datenverzeichnissen sollte dies in
  aktualisierte Modelle außerhalb des Renderpfads übergehen.

Keine neue kritische Funktionsblockade wurde in der letzten Stichprobe entdeckt.
Die Note ist dennoch **keine vollständige Release- oder Barrierefreiheitsfreigabe**:
reale Spiele, hörende Audioabnahme und Gerätewechsel, zwei physische Controller,
gemischte DPI und andere Compositoren, ARM64 sowie Windows-Laufzeitprüfungen
bleiben separat. Laufende Langtests haben eigene eingefrorene Buildstände und
dürfen nur für genau diese Stände ausgewertet werden. Mehr automatisierte
Screenshots würden diese Evidenzlücken nicht schließen.

### Abschließender Nachtrag ohne neue Bewertungsrunde

Die zwei letzten kosmetischen Hinweise sind im Code behoben: Die Library-Hilfe
steht nun bei y=598 innerhalb des auf 456 Pixel erhöhten Inhalts-Panels. Die
Controller-Unterseite meldet ohne Gerät ausdrücklich `No controller connected —
keyboard is ready.` Die Änderungen wurden im Code nachgelesen; dafür wurden
keine weiteren Screenshots oder Builds ausgelöst.

Der aktuelle TRX-Bericht wurde nochmals ausgelesen: **57 ausgeführt, 57 bestanden,
0 Fehler, 0 übersprungen**. Darunter besteht
`VirtualControllersRouteButtonsRemappingDeadzoneMenuFocusAndHotplug` mit einem
prozesslokalen virtuellen SDL-Gamepad. Dies ergänzt den Nachweis der tatsächlichen
SDL-Eingaberouten; physische Controller bleiben eine separate Hardwareabnahme.
Die Bewertungen bleiben unverändert bei **UI 8,2 / 10** und **Features 7,9 / 10**.
