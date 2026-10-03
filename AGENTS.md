# AetherBoy: Hinweise für Codex, Voltex und andere Agenten

## Texte in der Oberfläche

- Lies vor Änderungen an sichtbaren Texten [docs/UI_COPY_GUIDE.md](docs/UI_COPY_GUIDE.md). Die Regeln gelten für Linux, Windows und die lokale Online-Link-Browserseite.
- Schreibe zuerst den nächsten Schritt oder den tatsächlichen Zustand. Formuliere Warnungen konkret und überprüfbar. Behaupte keine bestätigten Pokémon-Tausche oder allgemeine GBA-Kompatibilität.
- Windows, Linux und die lokale Online-Link-Browserseite verwenden die gemeinsame Anzeigesprachauswahl (Deutsch/Englisch/System). Neue app-eigene Texte über `AetherBoy.Runtime.Localization.UiText` und den gemeinsamen Katalog anbinden. In den eingebetteten Browserressourcen nur explizite `{{ui:Quelltext}}`- bzw. `/*ui*/`-Marker verwenden; keine beliebigen JavaScript-Zeichenketten übersetzen. Behalte Begriffe wie „Raumcode“, „Einladung“, „Antwort“ und „Spielstandkopie“ innerhalb eines Ablaufs konsistent bei.
- Sprachschlüssel sind `system`, `de`, `en`. Automatisch: deutsche Systemsprache einschließlich AT/CH → Deutsch, sonst Englisch. Die Auswahl ist global und gilt nach Neustart; weder Spielprofile noch `CurrentCulture`/Datenformate umstellen. Nutzertexte, ROM-Titel, Dateipfade, Control-/Fokus-IDs und Protokollwerte niemals übersetzen. Unbekannte Quelltexte nicht heuristisch ersetzen.
- Vermeide Werbefloskeln, künstliche Dreiergruppen, Slogans aus Satzfragmenten, Schrägstrichketten und unübersetzte interne Statusnamen. Erkläre Fachwörter nur dort, wo sie für eine Entscheidung nötig sind.
- Prüfe, ob die Texte in ihrer vorhandenen UI-Breite lesbar bleiben. Fehler und Sicherheitshinweise dürfen nicht ihre entscheidende Aussage durch Abschneiden verlieren.
- Ändere bei Copy-Arbeit keine Protokollschlüssel, Diagnosecodes, Dateiformate oder Sicherheitsprüfungen. Sichtbare Texte dürfen sich ändern; technische Kennungen müssen stabil bleiben.
- Führe nach Copy-Änderungen `python3 scripts/review-ui-copy.py <geänderte Dateien>` aus und lies die Treffer selbst. Das Skript liefert Hinweise, keine fertigen Formulierungen. Teste betroffene Abläufe wie bisher im Projekt.

## Arbeitskontext

- Behandle vorhandene uncommittete Änderungen als Arbeit anderer Personen. Beschränke Änderungen auf den Auftrag und überschreibe sie nicht.
- Online Link ist ein Entwicklungsstand. Originalspielstände werden über Sitzungskopien geschützt; ein Verbindungsaufbau beweist keinen erfolgreichen Tausch.

## Verbindliches Aether-Design — Nutzerentscheidung vom 02.10.2026

- Neue sichtbare Bedienelemente und Dialoge müssen unsere gemeinsamen Aether-Komponenten verwenden. Keine neuen nackten WinForms-Buttons, Checkboxen, Dropdowns, Listen, Textfeldrahmen, Scrollleisten, Tooltips, Kontextmenüs oder Datei-/Farbauswahldialoge in Feature-Fenstern. Nur Einfärben eines Standardcontrols gilt nicht als Umsetzung.
- Vor UI-Arbeit die [verbindliche Fund- und Abnahmeliste](docs/UI_THEME_HANDOFF_2026-09-28.md#windows-standardelemente-verbindliche-fundliste--02102026) lesen und beim Umbau weiterpflegen. Ein fehlendes Bauteil wird zuerst zentral ergänzt, nicht pro Fenster neu improvisiert.
- Die sechs Themes, frei wählbaren Farben, Aether-Wave-Geometrie und lesbaren Hover-/Fokus-/Deaktiviert-Zustände bleiben erhalten. Keine fest verdrahtete dunkle Ersatzoberfläche. Gemeinsame Gestaltungsregeln gelten auch für Linux; keine Windows-Controls dorthin übertragen.
- WinForms als Plattform, layoutende Panels, einfache themegebundene Texte/Bilder, Zwischenablage, native Texteingabe und Accessibility dürfen technische Grundlagen bleiben. Native Editierfunktionen nur gekapselt hinter einem vollständig gestalteten Aether-Bauteil, einschließlich Popup, Scrollen und Kontextmenü. Tastatur, Controller, Unicode/IME, Auswahl, Undo, Passwortmaskierung, DPI und Screenreader nicht für die Optik opfern.
- Betriebssystem-Sicherheitsoberflächen (z. B. UAC), IME-/Hilfstechnologie-Fenster sowie ausdrücklich geöffneter Explorer/Browser sind keine app-eigenen Dialoge. Nicht imitieren oder Sicherheitsabfragen umgehen. Unsere Datei-/Farbauswahl dagegen gehört zum Umbau.
- `WindowsUiPolicyTests` prüft kompilierte Erzeugungsstellen und native UI-APIs. `tests/AetherBoy.SmokeTests/WindowsUiDebt.json` erfasst nur den bestehenden, geprüften Bestand — kein Freibrief. Beim Entfernen Einträge reduzieren; nicht blind regenerieren, erhöhen oder Tests abschalten. Notwendige interne Plattform-Brücken müssen begründet, gekapselt, dokumentiert und separat getestet werden.
- Nach Änderungen den Windows-UI-Policy-Test sowie betroffene Funktions-/Theme-Tests ausführen. Ein grüner Strukturtest ersetzt keine visuelle Abnahme von Dropdowns, Rechtsklick, Scrollleisten, Fehlerfällen und allen Fenstern.
