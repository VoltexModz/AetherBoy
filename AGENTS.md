# AetherBoy: Hinweise für Codex, Voltex und andere Agenten

## Texte in der Oberfläche

- Lies vor Änderungen an sichtbaren Texten [docs/UI_COPY_GUIDE.md](docs/UI_COPY_GUIDE.md). Die Regeln gelten für Linux, Windows und die lokale Online-Link-Browserseite.
- Schreibe zuerst den nächsten Schritt oder den tatsächlichen Zustand. Formuliere Warnungen konkret und überprüfbar. Behaupte keine bestätigten Pokémon-Tausche oder allgemeine GBA-Kompatibilität.
- Nutze in der deutschen Windows-Oberfläche und Browserseite natürliches Deutsch; die Linux-Oberfläche ist derzeit Englisch. Behalte Begriffe wie „Raumcode“, „Einladung“, „Antwort“ und „Spielstandkopie“ innerhalb eines Ablaufs konsistent bei.
- Vermeide Werbefloskeln, künstliche Dreiergruppen, Slogans aus Satzfragmenten, Schrägstrichketten und unübersetzte interne Statusnamen. Erkläre Fachwörter nur dort, wo sie für eine Entscheidung nötig sind.
- Prüfe, ob die Texte in ihrer vorhandenen UI-Breite lesbar bleiben. Fehler und Sicherheitshinweise dürfen nicht ihre entscheidende Aussage durch Abschneiden verlieren.
- Ändere bei Copy-Arbeit keine Protokollschlüssel, Diagnosecodes, Dateiformate oder Sicherheitsprüfungen. Sichtbare Texte dürfen sich ändern; technische Kennungen müssen stabil bleiben.
- Führe nach Copy-Änderungen `python3 scripts/review-ui-copy.py <geänderte Dateien>` aus und lies die Treffer selbst. Das Skript liefert Hinweise, keine fertigen Formulierungen. Teste betroffene Abläufe wie bisher im Projekt.

## Arbeitskontext

- Behandle vorhandene uncommittete Änderungen als Arbeit anderer Personen. Beschränke Änderungen auf den Auftrag und überschreibe sie nicht.
- Online Link ist ein Entwicklungsstand. Originalspielstände werden über Sitzungskopien geschützt; ein Verbindungsaufbau beweist keinen erfolgreichen Tausch.
