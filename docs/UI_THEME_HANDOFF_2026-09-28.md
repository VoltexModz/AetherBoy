# UI-Farben und Logo-Abgleich — lokaler Arbeitsstand vom 28. September 2026

## Verbliebene Windows-Bauteile ersetzt — 02.10.2026

**Lokal implementiert, nicht committed/gepusht.** Die Fundliste bleibt darunter
als historischer Bestand und manuelle Abnahmeliste erhalten. Die nach Stufe 1
verbliebenen Bauteile sind jetzt zentral ersetzt, nicht nur anders eingefärbt:

| Bereich | Umsetzung |
| --- | --- |
| WUI-03 Eingaben/Memos | `AetherTextBox` kapselt den nativen EDIT ausschließlich für Unicode/IME, Auswahl, Tastatur und Undo. Rahmen, Fokus, Scrollleisten und das Bearbeitungsmenü werden selbst gezeichnet. Alle 26 TextBox-Stellen und die Diagnose-RichTextBox sind umgestellt. Die Diagnose behält Auswahl und Scrollposition beim Aktualisieren. |
| WUI-05 Datei-/Farbauswahl | `AetherFileDialog` ersetzt die vier Öffnen- und zwei Speichern-Dialogstellen. Navigation über Pfad, Laufwerk, Downloads/Dokumente, Ordner, Tastatur und Controller; Filter und Dateiendungen bleiben erhalten. `AetherColorDialog` bietet RGB-Regler, Zahlenfelder, Hex-Wert, Vorschau und explizites Übernehmen/Abbrechen. Alle drei anpassbaren Themefarben benutzen ihn. |
| WUI-06 Listen/Tabellen | Alle fünf Listenstellen verwenden `AetherList`: Cheats, Backups, ROM-Browser, Bibliothek und Archivauswahl. Eigene Tabellenköpfe, größenänderbare Spalten, Zeilen, Kacheln, Auswahl, Scrollen, Tastatur/Controller und Accessibility-Kinder; kein ListView-/Header-Fenster und kein DrawDefault-Fallback. |
| WUI-08 Bearbeitungsmenüs/Details | `AetherPopup` zeichnet Rückgängig, Ausschneiden, Kopieren, Einfügen, Löschen und Alles auswählen. Passwortfelder geben weder Kopieren/Ausschneiden noch ihren Accessibility-Wert frei. `AetherHint` zeigt lange Listendetails auswählbar und scrollbar im vorhandenen Fenster. |
| WUI-10 Rahmen | Die Eingaben und Listen besitzen eigene Aether-Konturen. Die alte generische TextBox-/RichTextBox-/ListView-Stylinglogik ist entfernt. |
| WUI-11 alte Menüstruktur | `AetherCommand`, `AetherCommandCollection` und `AetherCommandSet` sind reine Befehlsdaten. Keine MenuStrip-/ToolStripMenuItem-/Separator-Erzeugung mehr. Bestehende Aktionen, Verfügbarkeiten, Checkzustände und Untermenüs laufen weiter über `AetherCommandMenu`; das Tastenkürzel-Routing bleibt beim Hauptfenster. Der alte native DarkMenuRenderer entfällt. |
| WUI-12 Rahmen-Fallbacks | Alte Designer starten mit `FormBorderStyle.None`, der Sizable-Rückfallpfad ist entfernt. Raum-/Recovery- und neue Auswahldialoge verwenden die gemeinsame sofort rahmenlose `AetherWindow`-Basis und `AetherDialog`. |
| WUI-13 lange Meldungen | Zu lange Signal-/Sicherheitsmeldungen erhalten ein schreibgeschütztes Aether-Memo. Ihr Ende bleibt erreichbar und kopierbar; Schließen einer Ja/Nein-Frage ergibt weiterhin Nein. |

### Sicherheits- und Verhaltensgrenzen

- Der Dateidialog **wählt nur aus**. Er schreibt weder eine neue Datei noch
  überschreibt er eine vorhandene; auch nicht nach der Überschreibbestätigung.
  Die bisherigen Import-/Export-Routinen behalten Format-, Größen-, Integritäts-
  und Fehlerprüfungen. Abbruch lässt `FileName` und Dateien unverändert.
- Es gibt keine Ordner-Erstellen-/Löschen-/Umbenennen-Funktion im neuen Picker.
  Er zeigt bis zu 5000 Einträge; ein vollständiger eingegebener Pfad bleibt nutzbar.
  Netzwerkpfade werden wie andere Pfade synchron gelesen; langsame Freigaben
  sind ein verbleibender Komfort-/Performance-Testfall, keine neue Zusage.
- Native Texteingabe bleibt als **private verschachtelte** `AetherTextBox.Editor`
  erhalten, ohne Windows-Rahmen, Scrollleisten oder Kontextmenü. Ein vollständiger
  Ersatz von IME/Screenreader durch selbst gezeichnete Buchstaben wäre kein
  gleichwertiger Funktionsersatz. Clipboard-Sperren im Bearbeitungsmenü werden
  als Aether-Meldung angezeigt. Löschen/Ersetzen bleibt im Undo-Verlauf.
- `AetherWindow` ist die bewusst geprüfte neue Plattform-Brücke für Fenster,
  keine unkontrollierte neue Standardoberfläche. Zwei Inventareinträge dokumentieren
  ihre Form-Basis und das Setzen von `FormBorderStyle.None`; Regressionstests
  prüfen sie und die gekapselte EDIT-Brücke separat.
- Baseline gezielt reduziert: **82 auf 35 Gruppen**, **155 auf 35 Gesamtstellen**.
  49 entfernte Gruppen stehen zwei dokumentierten Fenster-Brücken-Gruppen gegenüber.
  Die verbleibenden 35 Stellen bestehen aus 25 Form-Basen und 10 Rahmenzuweisungen;
  keine Standard-Widget-/Datei-/Farb-/Menü-Erzeugungsstellen mehr. Der Wächter bleibt
  aktiv und wurde nicht um Ausnahmen für beliebige neue Standardcontrols erweitert.

### Nachweise und noch offene Abnahme

- Release-Build: **0 Warnungen, 0 Fehler**. Neue Regressionstests in
  `WindowsAetherRemainderTests` prüfen Editor/Undo/Unicode/Passwort, offene
  Bearbeitungsmenüs, initiale Scrollbereiche, Listen-/Kachelauswahl,
  Controller-Ordnernavigation, Dateifilter, verweigertes Überschreiben,
  rein lesende Dateiauswahl, Farb-Abbruch/Validierung und lange Warnungen.
- Abschließender Windows-Smoke-Testlauf: **323 Tests insgesamt, 319 bestanden,
  4 umgebungsabhängig übersprungen, 0 fehlgeschlagen**. Die übersprungenen
  Hardware-/Desktopprüfungen sind kein bestandener Praxistest.
- Bisherige Menütests prüfen weiterhin Klicks, Checkzustände und Untermenüs.
  Die obsolete native DropDown-Objektprüfung wurde durch Prüfung des tatsächlichen
  Fensterbaums **und** der kompilierten Befehls-Konstruktoren ersetzt.
- Neue Komponenten in allen sechs Themes gerendert; Original/Pocket Light,
  Bearbeitungsmenü, Dateiauswahl und RGB-Farbauswahl visuell geprüft. Bilder liegen lokal in
  `artifacts/aether-complete-ui-20261002/`. Geöffnete Bearbeitungsmenüs werden
  zusätzlich separat gerendert, da das Fenster-DrawToBitmap die Popup-Z-Reihenfolge
  nicht zuverlässig wiedergibt.
- Neue sichtbare Texte: Copy-Prüfung ohne Treffer. Der zusätzliche breite Scan
  nennt 59 Hinweise in unveränderten bisherigen Oberflächentexten; das ist keine
  abgeschlossene sprachliche Überarbeitung des gesamten Produkts.
- **Noch nicht vollständig abgenommen:** sämtliche 26 bisherigen Fenster und
  Neben-/Fehlerzustände mit echter Maus/Tastatur/Controller-Hardware, echte
  100/150/200-%-Monitor-DPI-Wechsel, IME-Komposition und Screenreader. Ebenso
  das Systemmenü-Verhalten (Alt+Leertaste) und langsame/unzugängliche Netzwerkpfade.
  Strukturtests und Renderings beweisen nicht, dass jeder Laufzeitzustand perfekt ist.
- Keine Linux-Produktivdateien geändert. Themes und Customize, ROMs, Spielstände,
  Emulationskerne, Online-Protokolle und Linux-Funktionalität sind unverändert.

## Aether-Bedienelemente: Umbaustufe 1 — 02.10.2026

**Lokal umgesetzt, nicht committed/gepusht.** Dies ist der erste Implementierungs-
schritt auf Basis der unverändert nachvollziehbaren Fundliste unten. Es ist noch
keine Erklärung, dass sämtliche Standardelemente verschwunden wären.

| Fundbereich | Aktueller Implementierungsstand |
| --- | --- |
| WUI-01: Container-Scrollen | `AetherScrollViewport` und `AetherScrollBar` ersetzen sämtliche bisherigen `AutoScroll`-Container: Dialogkörper, alle Einstellungsseiten, Suchergebnisse, Controller-Seiten, Befehlsmenüs und State-Galerie. Horizontales/vertikales Scrollen, Mausrad, Ziehen, Tastatur, Controller und Fokus-Nachführen sind enthalten. **Interne** Textfeld-/Tabellen-Scrollleisten gehören weiterhin zu WUI-03/06. |
| WUI-02: Dropdowns | Alle neun Erzeugungsstellen verwenden `AetherSelect`: Bibliotheksfilter, Sortierung, Genre/Bewertung, Laufwerke, Cheatformat und beide Slot-Auswahlen. Feld und aufklappende Liste werden selbst gezeichnet; kein nativer ComboBox-/ListBox-Popup. Pfeile, Home/End, PageUp/Down, Anfangsbuchstaben, F4/Alt+Pfeil abwärts, Enter, Escape, Mausrad und Controller sind angebunden. |
| WUI-03: Checkboxen | Alle fünf Stellen verwenden `AetherCheckBox`; Haken, Zwischenzustand, Hover, Fokus und deaktivierter Zustand sind Aether-gezeichnet. Texteingaben/Memos samt Kontextmenüs bleiben offen. |
| WUI-04 und WUI-07 | Die Raumdialog-Buttons verwenden `AetherButton`; GBA Direct Sound verwendet wie die übrigen Audiogruppen `AetherGroupBox`. Vorhandene Click-Handler, Zustimmungen und Sicherheitsprüfungen bleiben bestehen. |
| WUI-09 | Die punktierten Windows-Fokusrechtecke von Buttons und Listenkacheln sind durch die gemeinsame Aether-Kontur ersetzt, Fokus wird nicht versteckt. |
| WUI-10: Farbvorschau | `AetherColorSwatch` zeichnet den Vorschaurahmen selbst. Die eigentliche Farbauswahl und Text-/Listenrahmen sind weiterhin offen. |

### Architektur und Funktionsgrenzen

- `AetherScrollViewport` bewegt einen geclippten Inhaltscontainer. Es aktiviert
  keine nativen `WS_VSCROLL`/`WS_HSCROLL`-Leisten. Statische und dynamische
  Flow-Layouts werden vermessen; verkleinerte Inhalte setzen ungültige Offsets
  zurück. Die vorhandenen Controls und ihre Ereignisse werden weiterverwendet.
- `AetherSelect` öffnet eine Liste **im vorhandenen Fenster**, begrenzt auf dessen
  Fläche. Ein Klick außerhalb, Escape, Deaktivierung, Größenänderung und Dispose
  schließen sie und lösen die Nachrichtenfilter. Eine markierte Zeile wird erst
  mit Bestätigung übernommen; Controller-Zurück schließt zuerst die Liste, nicht
  das komplette Fenster. Alt+F4 bleibt beim Betriebssystem/Fenster.
- Die CheckBox-Basisklasse ist bewusst eine interne Plattform-Brücke für
  Checked/CheckState, Tastatur und Accessibility. `OnPaint` zeichnet sämtliche
  sichtbaren Zustände selbst; kein eingefärbter Standardhaken. Select/Scrollbar
  stellen eigene Accessibility-Rollen, Werte und Auswahlaktionen bereit.
  Ein Test dieser Rollen ersetzt **keinen** manuellen Screenreader-Durchlauf.
- Die sechs Themes und frei gespeicherten Farben bleiben unangetastet. Farben
  werden aus `AetherColors` bezogen; auch geöffnete Listen werden bei einem
  Themewechsel invalidiert. Keine Änderung an Spielständen, Kernen oder Netplay.
- Keine Linux-Produktivdateien in dieser Umbaustufe geändert. Linux verwendet
  weiterhin eigene SDL-Widgets; die gemeinsamen Theme-/Geometrieregeln gelten
  dort ebenso. WinForms-Klassen werden nicht auf Linux übertragen.

### Offene nächste Umbaustufen

1. Zentrale Eingabe/Memo-Komponente mit eigenen Rahmen, Bearbeitungsmenüs und
   Scrollleisten. Unicode/IME, Auswahl, Undo, Passwortfelder, Zwischenablage und
   Controller-Tastatur erhalten; keine Einzellösungen pro Fenster.
2. Tabellen/Kacheln inklusive Header, Scrollen, Tooltip und Auswahl vollständig
   kapseln, danach die alten generischen Styling-Fallbacks abbauen.
3. Eigene Öffnen-/Speichern-/Farbauswahl mit denselben Dateifiltern,
   Pfadprüfungen, Überschreibbestätigungen und Abbruchgarantien.
4. Versteckte Menü-Datenstruktur und Rahmen-Fallbacks auflösen sowie sämtliche
   26 Fenster-/Dialog-Einträge unten visuell und mit echter Eingabe abnehmen.

Die Bestandsbaseline wurde **nur reduziert**, nicht neu erzeugt: von 105 auf
82 Methoden-/Typgruppen, von 157 auf 131 statische native API-Aufrufe (zuzüglich
unverändert 24 Form-Unterklassen). Die 26 entfernten Aufrufe sind 9 ComboBox-,
5 CheckBox-, 2 Button-, 1 GroupBox-Konstruktor, 6 AutoScroll-, 2 Fokusrechteck-
und 1 nativer Farbvorschau-Rahmenaufruf. Dies zählt Quellstellen, nicht sichtbare
Instanzen oder verbliebene Fehler.

Neue Tests in `WindowsAetherWidgetsTests`: Auswahl-/Änderungsereignisse,
Sammlungsänderungen, Tastatur/Controller/Abbruch, Popup-Begrenzung und Lebensdauer,
nicht anklickbare unsichtbare Restzeilen, Checkbox-Zustände/Accessibility,
Scrollbar-Ziehen/Tastatur/Werte, beidseitiges Scrollen/Fokus/Resize, dynamisches
Flow-Layout und sechs Theme-Renderings. Screenshots in
`artifacts/aether-widgets-20261002/` (lokal/ignoriert).

### Validierung dieser Umbaustufe

- Release-Build der Windows-Anwendung und Smoke-Tests: **0 Warnungen, 0 Fehler**.
- Abschließender vollständiger Windows-Smoke-Lauf: **309 bestanden, 4 übersprungen,
  0 fehlgeschlagen** (313 gesamt). Die vier Ausnahmen betreffen Direct2D-/WASAPI-
  Hardware, Live-Screenshot und echte Vordergrund-Tastatureingabe in der Testumgebung.
- Gezielter Lauf für Widgets, UI-Bestandsschutz und Einstellungspersistenz:
  **20 bestanden**. Copy-Prüfung der neuen Komponenten: keine Prüfpunkte;
  `git diff --check` für die bearbeiteten Bereiche ohne Fehler.
- Vor dem Abschluss blieben zwei Gesamtläufe in einem modalen Einstellungs-
  Fehlerdialog hängen und wurden gezielt beendet; sie zählen nicht als bestanden.
  Der Stack zeigte `ReportSettingsSaveFailure` während eines Theme-Tests.
  Ein neuer Paralleltest reproduzierte zusätzlich das Lesen während des Datei-
  Ersetzens. `WindowsSettingsProvider` stimmt Laden und Speichern nun über dieselbe
  Sperre ab und öffnet Leser mit `FileShare.Delete`. Drei parallele Leser prüfen
  vollständige Snapshots während 80 Speicherungen. Fehlerdialoge wurden weder
  unterdrückt noch ihre Prüfungen entfernt. Testdaten liegen isoliert im Tempordner.
- Repräsentative Fenster und alle sechs neuen Widget-Theme-Renderings geprüft.
  Echte Monitor-DPI-Wechsel, IME und Screenreader sowie sämtliche Fensterzustände
  bleiben als manuelle Abnahme offen; ein erfolgreicher Build ersetzt sie nicht.

## Logo-Nachtrag — 02.10.2026

Das vom Nutzer fertig gestaltete AB-Logo wird unverändert als Master-JPEG unter
`branding/aetherboy-logo-2026-10-02.jpg` aufbewahrt. Die vorhandene Export-Pipeline
erzeugt daraus PNG-Größen und das Windows-ICO; Windows und Linux verwenden
dasselbe 512-px-Bild. Historische SVGs bleiben erhalten, sind aber nicht mehr die
aktive Exportquelle. Keine Neugestaltung oder Bildgenerierung.

Der sichtbare Überspringen-Button der Startanimation wurde auf Nutzerwunsch in
beiden Frontends entfernt, einschließlich der Linux-Maus-Trefferfläche. Enter,
Leertaste, Escape und die vorhandenen Controller-Wege bleiben bestehen. Der
zusätzliche einfache Schriftzug entfällt, weil das neue Logo ihn bereits enthält.
Das Bild ist nicht transparent; sein dunkler Hintergrund gehört zur gelieferten
Vorlage. Alle sechs Themes, eigene Farben und Creator-Intro-Dateien bleiben erhalten.

Release-Builds beider Frontends: keine Fehler/Warnungen. Vollständige Windows-
Smoke-Suite: 324 bestanden, 4 Hardware-/Foreground-Skips, keine Fehler; darin
UI-Policy und Intro-Renderings in allen sechs Themes. Die drei Linux-Introtests
unter WSLg/Wayland bestanden ebenfalls. Testbilder:
`artifacts/logo-update-20261002/`. Keine Aussage über neue Core-/Link-Kompatibilität.
Noch kein Commit/Push.

## Windows-Standardelemente: verbindliche Fundliste — 02.10.2026

**Nutzerauftrag:** Die bisherige Liste behalten, tiefer prüfen und künftig nur
noch unsere eigenen sichtbaren Komponenten verwenden. Diese Liste ist die
verbindliche offene UI-Merkliste. Der vorherige Aether-Wave-Umbau hat gemeinsame
Buttons/Panels wiederhergestellt, **nicht sämtliche Standardcontrols ersetzt**.
Die folgenden Fundstellen dokumentieren den **Ausgangsbestand vor Umbaustufe 1**.
Der aktuelle Erledigungsstand steht darüber; die ursprünglichen Fundstellen
bleiben als Abnahmecheckliste erhalten.

### Umfang und Nachweisgrenzen

- Quellprüfung sämtlicher Windows-Formklassen samt Designer-/Partial-Dateien,
  gemeinsamem Styling, dynamischen Dialogen, Menüaufbau und Systemdialog-Aufrufen.
  24 benannte Formklassen einschließlich `AetherSignalDialog`, zusätzlich die
  dynamischen Fenster `ShowOnlineRoomDialog` und `ShowOnlineSaveRecovery`.
- Zusätzlich kompilierten Windows-Code ohne UI-Start nach nativen Erzeugungen
  und ausgewählten UI-APIs durchsucht: **81 Methoden-/API-Gruppen, 157 statische
  Aufrufstellen**, außerdem 24 direkte Form-Unterklassen (zusammen 105
  Baseline-Einträge). Darin 49 Konstruktorstellen für Standard-Bedienelemente,
  7 System-Datei-/Farbdialogstellen und 71 Stellen der noch vorhandenen, versteckten
  Menü-Infrastruktur, zwei dynamische Form-Konstruktoren. Der Rest sind Scroll-,
  Tooltip-, Rahmen- und Fokusaufrufe. Das sind **keine 157 sichtbaren Fehler oder Control-Instanzen**: Eine Factory
  kann mehrere Controls erzeugen, andere Aufrufe schalten natives Aussehen ab.
- Das ist ein vollständiger aktueller **Codebestand innerhalb dieses Umfangs**,
  keine Behauptung, jeden Laufzeitzustand visuell gesehen zu haben. Aufklappmenüs,
  Rechtsklick, Touch-/IME-Zustände, lange Listen, DPI-Wechsel und Fehlerzustände
  müssen bei der jeweiligen Migration zusätzlich visuell geprüft werden.

### Bisherige sechs Fundbereiche — beibehalten und präzisiert

| ID | Offen | Konkrete Fundstellen und Ziel |
| --- | --- | --- |
| WUI-01 | Scrollleisten | `Controls/AetherDialog.cs:Apply` verwendet einen nativen AutoScroll-Viewport für fast alle Dialoge. Zusätzlich `frmControlCenter.cs:CreatePage`, Suche in `frmControlCenter.Navigation.cs`, `frmControls.Sections.cs`, `frmStateGallery.cs` und `Controls/AetherCommandMenu.cs`. Ein zentraler Aether-Scrollbereich muss vertikales/horizontales Scrollen, Mausrad, Drag, Fokus-Nachführen und Controller übernehmen. Weiße Scrollleisten auch bei kleinen Fenstern/hoher Skalierung entfernen. |
| WUI-02 | Dropdowns | Neun `ComboBox`-Erzeugungsstellen: GBA-Cheatformat, drei Bibliotheksfilter/-sortierungen, Genre/Bewertung, Laufwerksauswahl, Quick-Menü-Slot und Sofa-Menü-Slot. Eigene Aether-Auswahl samt **geöffneter Liste**, Scrollen, Fokus, Auswahl und Abbruch; flacher Rahmen allein reicht nicht. |
| WUI-03 | Checkboxen und Eingaben | Fünf Checkboxstellen: Live-Refresh im Audio-Inspector, Online-Link-Zustimmung, Probe-Bericht, Bibliotheksfavoriten, UPS-Richtung. 26 TextBox-Erzeugungsstellen plus eine RichTextBox-Factory: Suche, Kürzel, Cheats, Raumdaten, Titel/Tags, Logs/Infos, Controller-Tastatur, Discord-ID, Patchpfade. Aether-Schalter und Aether-Eingaben inklusive Auswahl, Passwortmaskierung, Platzhalter, Validierung und Mehrzeilenfall. |
| WUI-04 | Standardbuttons | Zwei `new Button`-Stellen in `frmNano.OnlineRooms.cs`: lokale Button-Factory und „Server speichern“. Die Factory erzeugt mehrere Schaltflächen. `AetherDialog.Style` färbt sie nur flach ein; sie bekommen dadurch **keine** AetherButton-Geometrie. Auf `AetherButton` umstellen, ohne DialogResult, Accept/Cancel oder asynchrone Sperren zu ändern. |
| WUI-05 | Datei-/Farbauswahl | Vier OpenFileDialog-Stellen: Batterie-SAV importieren, Firmware importieren, Partner-ROM im Local Link, Patch wählen. Zwei SaveFileDialog-Stellen: Audio-WAV und Testerbericht exportieren. Ein ColorDialog in `AddThemeColorCard`, von allen drei Farbfeldern benutzt. Eigene Öffnen-/Speichern-/Farbauswahl, bestehende Dateifilter, Pfadprüfung und Überschreibschutz beibehalten. |
| WUI-06 | Listen/Tabellen | Fünf ListView-Stellen: Cheats, Batterie-Backups, Bibliothek, ROM-Browser und Archivauswahl. Header, Zeilen und Kacheln sind teilweise selbst gezeichnet, aber Steuerung/Scrollen/Fokus/Popup-Verhalten bleiben nativ. `StyleListView` fällt bei Ansichten außer Details/LargeIcon auf `DrawDefault` zurück. Eigene vollständige Aether-Liste für beide verwendeten Ansichten, nicht nur neue Zeilenfarben. |

Dateinamen dieser Tabellen sind relativ zu `nanoboy/`, Methodennamen bleiben
auch nach dem Verschieben von Quellzeilen als Suchanker nutzbar.

### Zusätzlich tiefer gefunden

| ID | Status | Befund und Folgeschritt |
| --- | --- | --- |
| WUI-07 | Offen, im Code bestätigt | `frmAudioTool.cs:ConfigureAetherLayout` erzeugt für **GBA Direct Sound** einen normalen `GroupBox`. Die älteren fünf Kanalgruppen sind bereits `AetherGroupBox`. Den Nachzügler auf denselben Baustein umstellen. |
| WUI-08 | Teilweise bestätigt, Laufzeit prüfen | Cheats aktiviert ausdrücklich `ListView.ShowItemToolTips`. Bibliothek setzt `ToolTipText`, aktiviert damit allein aber noch keine sichtbaren Tooltips. TextBox/RichTextBox können ihre Windows-Kontextmenüs mitbringen; Rechtsklick, Shift+F10 und Passwortfelder separat prüfen. Aether-Hinweise und Bearbeitungsmenüs vorsehen; niemals geheime Felder per Tooltip offenlegen. |
| WUI-09 | Offen, im Code bestätigt | `AetherButton.OnPaint` nutzt noch `ControlPaint.DrawFocusRectangle`; die ListView-Kachelansicht `DrawListViewItemEventArgs.DrawFocusRectangle`. Eigene Aether-Fokusmarkierungen einführen, **sichtbaren Fokus nicht entfernen**. Alle sechs Themes sowie sehr helle/dunkle Eigenfarben prüfen. |
| WUI-10 | Offen, im Code bestätigt | Die Farbvorschau in `AddThemeColorCard` nutzt `Panel.BorderStyle.FixedSingle`. TextBox-/RichTextBox-/ListBox-/ListView-Styling setzt ebenfalls native FixedSingle-Rahmen. Eigene gezeichnete Rahmen und Fokus-/Fehlerzustände; keine Doppelrahmen. |
| WUI-11 | Versteckte Altstruktur, nicht als sichtbarer Fehler zählen | `frmNano.Designer.cs` und Menü-Ergänzungen erzeugen MenuStrip/ToolStripMenuItem/Separator. `InitializeAetherShell` versteckt die Leiste, `AetherCommandMenu` zeigt eigene Buttons und verwendet die alten Einträge nur als Befehlsdaten. `DarkTheme.DarkMenuRenderer` ist ebenfalls noch vorhanden. Kein direktes Standard-Dropdown-Öffnen gefunden. Später auf ein plattformneutrales Befehlsmodell umstellen; bis dahin keine neue sichtbare native Menüoberfläche daraus öffnen. |
| WUI-12 | Fallback/Initialzustände, nicht als beobachteter Fehler ausgeben | Sechs alte Designer setzen klassische FormBorderStyles, anschließend ersetzt `AetherDialog.Apply` diese durch None. `frmNano.ResizeWindow` enthält außerdem Sizable als Rückfallpfad, wenn die Aether-Shell noch nicht initialisiert ist; die aktuelle Shell nimmt den anderen Zweig. Initialisierung, Wiederherstellen, Alt+Leertaste, Minimieren, Maximieren und DPI-Wechsel visuell abnehmen. Eigene Rahmen müssen auch bei Fehlern gelten. |
| WUI-13 | Prüfschuld | Fehlermeldungen nutzen bereits `AetherSignal`, nicht direkt `MessageBox.Show`. Trotzdem alle Buttons/Abbrüche, lange Sicherheitsmeldungen, Read-only-Textauswahl und kleine Bildschirme testen. Abgeschnittene Warnungen sind nicht durch ein schönes Panel erledigt. |
| WUI-14 | Bereits eigenes Rendering, keine pauschale Neuerstellung | Spielanzeige, `WaveDataControl`, `LevelDisplayControl`, Statuspunkt, Signal-Glyph, AetherGroupBox, AetherButton und AetherSurfacePanel werden selbst gezeichnet. Die nativen Basisklassen allein sind hier kein Stilbruch. Diese Controls in die Theme-/Fokus-/Skalierungsabnahme aufnehmen. |

### Fensterliste zur späteren Abnahme

Jede Zeile bleibt offen, bis Grundzustand **und** betroffene Neben-/Fehlerzustände
abgenommen sind. Ein Aether-Fensterkopf allein ist kein Fertig-Kriterium.

| Fenster / Einstieg | Noch zu berücksichtigen |
| --- | --- |
| `frmNano` / Hauptfenster | Aether-Shell vorhanden; Fokusmarkierung, Menüs/Untermenüs, kleine Größen, F11, Alt+Leertaste und Rahmen-Fallback. |
| `frmControlCenter` | Alle Seiten/Unterseiten: Scroll-Viewport, Suchfeld/-ergebnisse, Discord-ID, Diagnoseausgabe, Farbfelder/-vorschau/-dialog. Diagnose-RichTextBox bekommt nach dem Aufbau bereits ScrollBars.None; nicht fälschlich als aktuell sichtbaren eigenen Scrollbalken melden. |
| `frmControls` | Acht Designer-Tastenfelder plus zusätzliche Binding-Factory, Controller-Scrollseite, Aufnahme einer neuen Taste, Fokus und Gerätewechsel. |
| `frmCheats` | Tabelle inklusive breiter Codes, Tooltips, mehrzeilige Eingabe, Format-Dropdown für GBA, Fehler-/Sperrzustände. GB/GBC/GBA separat ansehen. |
| `frmAudioTool` | Live-Refresh-Checkbox, GBA-Gruppenrahmen, WAV-Speichern, Scrollen im verkleinerten Dialog; eigene Pegel/Wellenformen behalten. |
| `frmBatterySaveManager` | Backup-Tabelle, Import-Dateiauswahl, Auswahl/Fokus, inkompatible/gesperrte Saves und Wiederherstellungsbestätigung. |
| `frmFirmwareManager` | Import-Dateiauswahl und Ersatzbestätigung, fehlende/ungeeignete Datei, Dialog-Viewport. |
| `frmLocalLinkLab` | Partner-ROM-Dateiauswahl, beide Spielansichten, lange Namen, fokussierter Spieler, Fehler-/Ladezustände. |
| `frmLink` | Alter Host/Client-Dialog mit IP-TextBox. `menuLinkCable` führt aktuell ins Local-Link-Lab; alter Dialog ist trotzdem als Restbestand erfasst. |
| dynamisch: `ShowOnlineRoomDialog` | Standardbutton-Factory, Server speichern, drei Textfelder, Zustimmung, Passwortmaske, Raum-/Verbindungsfehler. |
| `frmOnlineConnectionTest` | Textfeld-Factory für URL/Schlüssel/Code/Ergebnisse, Berichtscheckbox, Ergebnis-Scrollbar, Abbruch/Schließen während eines Tests. |
| dynamisch: `ShowOnlineSaveRecovery` | Schon Aether-Buttons; Viewport, lange Pfade/Begründungen, gesperrte Kopien und Bestätigungen vollständig lesbar. |
| `frmRomLibrary` | Drei Dropdowns, Favoritencheckbox, Suche, Listen-/Kachelansicht, Auswahl/Focus/Tooltips und lange Titel. |
| `frmRomBrowser` | Trotz eigenem Fenster noch Standardliste und Laufwerks-Dropdown; daraus keine unveränderte native Grundlage für den neuen Dateidialog machen. |
| `frmArchiveChoice` | ROM-Tabelle, mehrzeiliger Eintragsname samt Scrollleiste, Auswahl/Abbruch, sehr lange Archivpfade. |
| `frmRomMetadataEditor` | Genre/Bewertung als Dropdown, Tag-Textfeld und Validierungsfehler. |
| `frmRomTitleEditor` | Titel-Textfeld, Zeichenlimit, leere Eingabe, Speichern/Abbrechen. |
| `frmRomPatcher` | Patch-Dateidialog, Textfeld-Factory, UPS-Checkbox, schreibgeschützte Pfade und lange Meldungen. |
| `frmQuickMenu` | Speicherplatz-Dropdown, Maus/Tastatur/Controller, deaktivierte Aktionen. |
| `frmSofaQuickMenu` | Speicherplatz-Dropdown, große Schrift, Controllerfokus, Dialog-Viewport und laufende Operation. |
| `frmSofaLibrary` | Eigene Vollbildoberfläche vorhanden; Karten/Tab-Fokus, leere/fehlende Spiele, Rückkehr und DPI. |
| `frmStateGallery` | Native FlowLayout-Scrollfläche, Miniaturen, Auswahl, leere Slots und Dateifehler. |
| `frmAbout` | Mehrzeilige TextBox mit Scrollleiste, Textauswahl/Kontextmenü, lange Lizenz-/Infotexte. |
| `frmChangelog` | Mehrzeilige TextBox mit Scrollleiste, Tastatur/Kontextmenü, langes Änderungsprotokoll. |
| `frmControllerKeyboard` | Schreibgeschütztes natives Textfeld; Fokus, Cursor/Anzeige und Bearbeitungsmenü prüfen. Tasten sind bereits eigene Buttons. |
| `AetherSignalDialog` | Eigene Symbole/Buttons vorhanden; alle Bestätigungsvarianten und lange Warn-/Fehlertexte auf kleinen Bildschirmen abnehmen. |

### Regeln für neue UI und Reihenfolge des Umbaus

1. **Gemeinsame Bausteine zuerst:** Aether-Scrollbereich, Eingabe/Memo samt
   Bearbeitungsmenü, Auswahlfeld samt Popup, Toggle/Checkbox, Liste/Kacheln,
   Fokusmarkierung, Tooltip und Datei-/Farbauswahl. Die Namen beschreiben hier
   Zielkomponenten, nicht bereits fertig implementierte Klassen.
2. Zuerst die wiederkehrenden Elemente in Einstellungen, Bibliothek, Schnell- und
   Sofa-Menü ersetzen; danach Cheats, Audio, Save-/Firmware-/Patch-/Link-Tools.
   Fehlerdialoge und seltene Nebenpfade nicht ans Ende einer unbestimmten Merkliste
   schieben: je Fenster direkt mit abnehmen.
3. Datei-/Farbdialoge inklusive Dateinavigation, Filter, Archiv-/ROM-Unterscheidung,
   nicht existierenden/unzugänglichen Pfaden, bestehender Zieldatei, Abbruch,
   Zugangsfehlern und Tastatur-/Controllerbedienung fertigstellen.
4. Nur bei tatsächlichem Ersatz offene Fundstellen streichen und die unten
   beschriebene Baseline reduzieren. Versteckte Legacy-Menüdaten erst entfernen,
   wenn Befehle, Shortcuts und Tests auf ein gemeinsames Modell übertragen sind.
5. Screenshots/Interaktion aller Fenster mit Aether Original, Pocket Light und
   eigenen Extremfarben; übrige vier Themes mitprüfen. 100–150 % App-Skalierung,
   mindestens 100/150/200 % Windows-DPI, minimale Größe und Monitorwechsel.
   Hover, gedrückt, ausgewählt, deaktiviert, Fokus, Dropdown offen, Rechtsklick,
   leere/lange/fehlerhafte Inhalte, Suchtreffer und Passwortfelder gehören dazu.
6. Linux verwendet denselben visuellen Vertrag und eigene SDL-Komponenten.
   Windows-Controls nicht portieren. Hier erfasste Windows-Fundstellen sind kein
   vollständiger Linux-Audit; die Wayland-Abnahme bleibt separat nachzuweisen.

**Kein Funktionsverlust zugunsten der Optik:** Unicode/IME, Zwischenablage,
Maus-/Tastaturauswahl, Undo, Passwortschutz, Enter/Escape, Tab/Shift+Tab,
Controller, Screenreader-Rollen und Warntexte bleiben erhalten. WinForms kann
weiter die Plattform sein. Ein natives Text-Backend darf nur innerhalb einer
vollständig gestalteten, zentralen Aether-Komponente verwendet werden.
Einfache themegebundene Labels/Bilder und unsichtbare Layoutcontainer sind keine
zu imitierenden Windows-Dialoge. Betriebssystem-eigene Sicherheitsabfragen,
IME/Hilfstechnologie-Fenster und ausdrücklich geöffneter Explorer/Browser bleiben
außerhalb unserer Gestaltung; insbesondere kein nachgebautes UAC-Fenster.

### Automatische Grenze gegen neue Standardcontrols

`WindowsUiInventory` untersucht die **kompilierte Windows-Assembly**:
Konstruktoren, Lambdas, lokale Factories, async-Methoden, Designer-Code und auch
`new()`/Typ-Aliase. Auch neue direkte Form-Unterklassen und dynamische `new Form`
werden erfasst, damit ein neues Fenster nicht unbemerkt den Aether-Rahmen umgeht.
Es startet keine UI, Datei- oder Netzwerkdialoge.
`WindowsUiPolicyTests` vergleicht das Ergebnis mit
`tests/AetherBoy.SmokeTests/WindowsUiDebt.json`. Bestehender Bestand wird nicht
nachträglich als fertig erklärt. Neue/größere oder entfernte/verschobene Gruppen
lassen den Test fehlschlagen; beim Ersatz muss die Baseline bewusst schrumpfen.
Compiler-Ordinalzahlen sind normalisiert, Quellmethoden und lokale Factorynamen
bleiben erhalten. So verursacht eine zusätzliche, unabhängige Methode allein
keine Neunummerierung sämtlicher Fundstellen.

```powershell
dotnet test --project tests/AetherBoy.SmokeTests/AetherBoy.SmokeTests.csproj `
  -c Release --filter FullyQualifiedName~WindowsUiPolicyTests
```

Die vorhandene Windows-CI führt das gesamte Smoke-Test-Projekt bereits aus und
nimmt diese Tests dadurch mit. **Baseline nicht blind regenerieren oder erhöhen.**
Eine notwendige Plattform-Brücke innerhalb eines Aether-Bauteils braucht eine
begründete, dokumentierte Ausnahme und eigene Tests.

Grenzen: Ein Test erkennt nicht jede optische Abweichung. Reflection/dynamisch
nachgeladene Controls, P/Invoke-erzeugte Fenster, intern von Windows erzeugte
Unterfenster und neue selbstgeschriebene, aber hässliche Widgets werden nicht
automatisch vollständig erfasst. Der Test zählt nach Methode/API, nicht pro
Bildschirminstanz; Austausch einer Stelle gegen eine andere innerhalb derselben
Gruppe ist kein vollständiger Änderungsnachweis. Deshalb bleiben Quellenreview,
Fensterliste und visuelle Abnahme verbindlich. Keine aktuell vorhandenen
RadioButton-, TrackBar-, NumericUpDown-, ProgressBar-, TabControl-, TreeView-,
DataGridView-, FolderBrowserDialog-, FontDialog-, TaskDialog- oder direkten
MessageBox.Show-Erzeugungs-/Aufrufstellen gefunden; manche dieser Typen kommen
nur in generischer Eingabebehandlung vor. Der Wächter erfasst neue native
Controls und CommonDialogs dieser Art ebenfalls.

### Validierung dieser Bestandsaufnahme

- Windows-Smoke-Projekt im Release gebaut: **0 Fehler, 0 Warnungen**.
- Neue UI-Policy-Tests: **3 bestanden**. Danach vollständige Windows-Testreihe:
  **297 bestanden, 4 übersprungen**, keine fehlgeschlagenen Tests. Die vier
  ausgelassenen Fälle benötigen Hardware-/Foreground-Zugriff am interaktiven Desktop.
- Baseline nach dem Build geprüft: 105 Einträge, zusammengesetzt aus 157
  statischen API-Aufrufstellen und 24 direkten Form-Unterklassen.
- In diesem Audit keine produktive UI-Logik und keine sichtbaren Texte geändert.
  Die Laufzeit-/Bildprüfung der offenen Fundstellen bleibt Teil ihrer Migration;
  dieser Testlauf ist kein Nachweis, dass alle Windows-Elemente ersetzt wären.

## Aether Wave wiederhergestellt, Themes erhalten — 02.10.2026

**Lokal implementiert für Windows und Linux; noch kein Commit/Push.** Die
Wiederherstellung betrifft die Gestaltung, nicht den funktionalen Stand eines
alten Commits. Die sechs Presets einschließlich Pocket Light und die drei freien
Farbfelder bleiben erhalten. Vorhandene Benutzerfarben werden nicht zurückgesetzt.
Windows: **Einstellungen → Aussehen → Aether Original**; Linux:
**Settings → App & files → Appearance → Aether Original**.

- `UiThemePalette` liefert für Aether Original wieder die unten dokumentierten
  historischen Flächen-, Rahmen- und Textwerte. Andere Presets/Eigenfarben erhalten
  passende dunkle oder helle Flächen. ROM-Farbpaletten sind weiterhin getrennt.
- `UiChamfer` definiert gemeinsam die scharfen Ausschnitte oben rechts und unten
  links. Windows zeichnet damit seine Buttons; Linux verwendet dieselben Punkte
  über `AetherShapeRenderer` und SDL-Geometrie. Die Trefferflächen und bestehenden
  Tastatur-/Controller-Aktionen ändern sich nicht.
- Primäre Aktionen haben wieder einen Verlauf, ausgewählte Menüpunkte einen
  schmalen Akzent und Kontur. Panels sind kantig; Header und Karten tragen die
  Akzentlinien. Gedämpfte/deaktivierte Beschriftung und sichtbarer Fokus bleiben.
- `UiButtonGradient` prüft den Verlauf einschließlich Zwischenfarben, Hover und
  gedrücktem Zustand. Falls nötig werden **nur die gezeichneten Button-Farben**
  für lesbare Schrift angepasst; die gespeicherten HEX-Werte und Farbmuster
  bleiben exakt. Akzenttexte werden auf allen vier Flächen geprüft.
- Die Windows-Listenauswahl benutzt ebenfalls Theme-Farben statt eines fest
  violetten Hintergrunds. Die Kopflinie bleibt unter den angedockten Controls
  sichtbar. Aktuelle Schriftgrößen, Suche, Skalierung und Dialogfunktionen bleiben.

### Validierung dieses Umbaus

- Release-Build der Lösung: **0 Fehler, 0 Warnungen**.
- Core: **256 bestanden**; Windows: **294 bestanden, 4 übersprungen**;
  Linux-Desktop: **141 bestanden, 53 übersprungen**. Die Skip-Zahlen umfassen
  Hardware-/Foreground- bzw. native Wayland-/AT-SPI-/Opt-in-Prüfungen.
- Die 11 Windows-Theme-Fälle prüfen alle sechs Presets, zwei freie Paletten,
  Live-Umschaltung, Speicherung, Zurücksetzen, unveränderte ROM-Palette,
  Button-Geometrie/Verlauf und deaktivierte/kleinste Controls.
- Frische, visuell geprüfte Windows-Testbilder (keine echten Spielstände):
  `artifacts/aether-wave-restored-20261002/windows-theme-logo.png`,
  `windows-appearance-logo.png` und `windows-appearance-pocket.png` sowie
  weitere Theme-Varianten im selben lokalen, ignorierten Ordner.
- Copy-Prüfung der betroffenen UI-Dateien: zwei Hinweise auf den **unveränderten**
  System-Aufzählungstext `GAME BOY · COLOR · ADVANCE`; keine sichtbaren Texte
  dieses Ablaufs umformuliert.

**Linux noch auf einem echten Wayland-System abnehmen.** Die neuen SDL-Vertices
sind hier ohne native Sitzung getestet, nicht die tatsächliche Darstellung.
`NativeShellRoutesControlsRendersAssetsAndPreservesSettings` prüft jetzt zusätzlich
alle sechs Theme-Buttons und nutzt für die freien Farben die aktuellen Feldpositionen.
Auf Linux nach dem Release-Build ausführen:

```bash
AETHERBOY_UI_TESTS=1 AETHERBOY_UI_CAPTURE_DIR="$PWD/artifacts/aether-wave-linux" \
  dotnet test --project tests/AetherBoy.DesktopTests/AetherBoy.DesktopTests.csproj \
  -c Release --no-build --filter FullyQualifiedName~NativeShellRoutesControlsRendersAssetsAndPreservesSettings
```

Zusätzlich Hauptfenster, Einstellungen, Sofa-Modus und ein Werkzeugdialog mit
Aether Original und Pocket Light prüfen: keine überdeckten Texte, diagonale
Ecken, sichtbarer Tastatur-/Controller-Fokus, eigene Farben nach Neustart erhalten.
Dieser Designumbau ist kein neuer Nachweis für Emulations- oder Online-Link-Kompatibilität.

## Ursprüngliches Aether-Wave-Design wiedergefunden — 02.10.2026

Historischer Befund **vor** der oben beschriebenen Wiederherstellung:

Der Nutzer vermisst ausdrücklich die diagonalen Abschrägungen **oben rechts und
unten links**, ursprüngliche Farbflächen und Fenster-/Button-Gestaltung. Der
Befund ist im Git-Verlauf eindeutig nachvollziehbar:

- `099c2f9` vom 10.08.2026 (`feat: launch aether wave interface`) enthält in
  `nanoboy/Controls/AetherUiControls.cs` die sechs Eckpunkte von
  `CreateChamferedPath`. Derselbe Stil ist unmittelbar vor `2a91c9f` noch vorhanden.
- `2a91c9f` vom 28.09.2026 (`Improve link session handling and UI`, xJessyX)
  ersetzt diesen Pfad durch `CreateRoundedPath`, die primären
  Violett–Cyan-Verläufe durch einfarbige Füllungen, kantige Panels durch Rundungen
  sowie Farb-/Akzentlinien im Kopfbereich durch eine neutrale Trennlinie.
  Gleichzeitig ersetzt eine gemeinsame berechnete Palette die festen Farbwerte.
- Die späteren lokalen Änderungen an `UiThemePalette.SurfaceAt` tönen die Flächen
  wieder mit den Akzenten. Die sechs Theme-Presets sind ebenfalls lokal ergänzt.
  Das Preset **Aether Original** verwendet die ursprünglichen drei Grundfarben,
  stellt aber weder die historischen Flächenwerte noch Geometrie und Verläufe
  vollständig wieder her. Die Bezeichnung allein belegt keine Originaltreue.

Historische Werte: Hintergrund `#050712`, Chrome `#080B18`, Flächen `#0C101F`,
angehobene Flächen `#111629`, Umrisse `#2F3753`, Text `#F1F4FF`, gedämpfter Text
`#8B94B1`; Akzente `#8B38FF`, `#A942F5`, `#29E2ED`.

Vorhandene, visuell geprüfte **alte UI-Test-Screenshots**, keine neue Nachbildung:

- `artifacts/parity-review/windows-shell.png`
- `artifacts/parity-review/control-center-display.png`

Diese Artefakte sind lokal/ignoriert und stehen nicht zwangsläufig nach einem
frischen Clone zur Verfügung. Der Git-Code ist die reproduzierbare Referenz.

Aus diesem Befund entstand der oben umgesetzte, gezielte Designumbau. Kein
vollständiges Zurücksetzen von `2a91c9f`; dieser Commit enthält zusätzlich
funktionale Änderungen. Eigene Screenshots des Nutzers waren für diesen
eindeutig gefundenen Stil nicht zwingend nötig.

**Integrationsnachtrag:** Der unten beschriebene damalige lokale Stand ist inzwischen
als `2a91c9f` auf `development` veröffentlicht. [Windows-Abnahme, CI-Testkorrektur
und Slot-Layoutfix](WINDOWS_UI_INTEGRATION_2026-09-28.md) dokumentieren die anschließende
Integration. Die folgenden Aussagen „uncommittet“ beziehen sich auf den ursprünglichen
Schreibzeitpunkt, nicht mehr auf den aktuellen Git-Status.

Basis: `development` auf `4b56039` (Voltex' Diagnose, ROM-freier Windows-Verbindungstest und Linux-Handoff). Die unten beschriebenen UI-Änderungen liegen derzeit **uncommittet** im Linux-Arbeitsbaum; sie sind noch nicht Teil dieses Upstream-Commits. Bestehende lokale Änderungen am neuen Windows-/Linux-Layout und an den README-Screenshots bleiben erhalten.

## Verhalten

- Die Standard-UI nutzt die Markenfarben `#8B38FF` (primärer Akzent), `#29E2ED` (zweiter Akzent) und `#050712` (Hintergrund) aus `branding/BRAND.md`. Das Logo selbst wird nicht umgefärbt.
- Beide Akzente und der Hintergrund sind global frei wählbar. Die Spielbild-/DMG-Palette bleibt eine getrennte Einstellung. Text, Flächen und die Schrift auf primären Schaltflächen werden für hellere und dunklere Hintergrundfarben abgeleitet.
- Windows: **Settings → Appearance**, drei native Farbdialoge mit Vorschau und „Restore logo colors“. Änderungen erscheinen sofort und werden über den bestehenden Windows-Einstellungsspeicher gesichert.
- Linux/Wayland: **Settings → System → Appearance colors**, drei `#RRGGBB`-Felder mit Vorschau, „Apply colors“ und „Restore logo colors“. Die Werte werden im globalen `settings.json` gespeichert, nicht im Spielprofil.
- Die gemeinsame, plattformneutrale Farblogik liegt in `nanoboy/Core/UiThemePalette.cs`. Die Linux-Testoberfläche für den ROM-freien Verbindungstest aus Voltex' Handoff ist weiterhin eine separate offene Aufgabe.

## Prüfung

- Linux- und Windows-Cross-Build: jeweils 0 Warnungen, 0 Fehler.
- Core-Tests: 234 bestanden, darunter Logo-Defaults, freie Hex-Farben und Textkontrast bei hellen, mittleren und dunklen Hintergründen.
- Linux-Desktop-Tests: 123 erfasst, 83 bestanden, 40 Tests ohne native Sitzung/Opt-in übersprungen. Der gezielte native Wayland-Shelltest wurde zusätzlich mit Opt-in ausgeführt: 1 bestanden; er hat die drei Farben über die Textfelder geändert und nach dem Speichern wieder geladen.
- Screenshots der Standard- und frei gewählten hellen Variante: `artifacts/theme-ui-preview/appearance.png` und `appearance-custom.png` (lokale, ignorierte Testartefakte).

Eine visuelle Windows-Abnahme auf einem Windows-Rechner steht noch aus. Die Windows-Oberfläche wurde unter Linux mit aktiviertem Windows-Targeting kompiliert, aber dort nicht gestartet. Kein Commit, Push oder Server-Deployment durch diese UI-Arbeit.
