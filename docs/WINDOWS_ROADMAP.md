# Windows-Ausbau

Aktueller Gesamtstand und gemeinsame Schnittstellen:
[Übergabe an die Entwicklung (DE/EN)](WINDOWS_DEVELOPMENT_HANDOFF.md).

Abgestimmt am 11. September 2026. Ein Projekt und eine Windows-Anwendung; der
Linux-Frontend-Ausbau bleibt beim Linux-Mitentwickler. Die folgenden Schritte
betreffen die Weiterentwicklung der Windows-Anwendung.

Das Komfortpaket **4–6** (Controller-Quick-Deck, Screenshots/Performance, Stereo)
ist eingebaut: [Bedienung und gemeinsame Audio-Schnittstelle](WINDOWS_PLAYER_TOOLS_STEREO.md).
Die folgenden W-Kategorien sind die ursprüngliche Roadmap, nicht die Nummerierung
dieses Komfortpakets. Linux behält vorerst seinen kompatiblen Mono-Ausgabeaufruf;
die gemeinsame Runtime liefert zusätzlich Stereo.

1. **Zentrale Ablage und Development-Diagnose:** umgesetzt. Die normale EXE
   zeichnet im Development-Kanal automatisch lokal auf. ROMs werden importiert,
   Saves/States nach Inhalt getrennt und Einstellungen versionsunabhängig abgelegt.
   Ordner lassen sich direkt in der Oberfläche öffnen. Eine separate Tester-EXE
   oder ein spezielles Tester-Paket ist nicht vorgesehen.
2. **Windows-Ausgabe (W3):** WASAPI, begrenzte Puffer und Messwerte, Gerätewechsel,
   Direct2D/VSync, Integer Scaling und aktive Timerpräzision sind eingebaut.
   [Details und noch offene Hardwareprüfungen](WINDOWS_AUDIO_VIDEO_UI.md).
   Weiterhin offen: breitere Geräte-/Latenzmessungen und Langzeit-Spieltests.
3. **Oberfläche (W4):** echtes Vollbild, DPI-/Dialoganpassung, Speicherstatus,
   Bibliothek mit Suche/Favoriten/Spielzeit/Vorschau und Profile pro Spiel sind
   eingebaut. [Bedienung und Grenzen](WINDOWS_GAME_COMFORT.md).
   Weitere Controller-Einrichtung und DPI-Prüfungen bleiben mögliche Folgeschritte.
4. **Save-Sicherheit (W5):** bestehende atomare Batterie-Saves, drei Backups,
   Integritätsprüfung und Restore-UI beibehalten. Auf der zentralen Ablage bessere
   Speicherstatus-Anzeigen, State-Galerie, separater Fortsetzen-Slot und einmaliges
   Lade-Rückgängig sind hinzugekommen. Komfortabler Save-Import/-Export bleibt offen.
   Keine erneute Implementierung bereits vorhandener Schutzmechanismen.
5. **Diagnose vertiefen:** Hintergrundbeobachter und manuelle Problemmarkierung
   umgesetzt. Fehlender Emulations-/UI-/Bildfortschritt, lange Starts und einfarbige
   Bilder werden als Verdacht behandelt, mit Unterdrückung bei Pause/Fokusverlust.
   Weiterhin offen: komfortabler Vergleich mehrerer Buildberichte.

Zusätzlich sind IPS-/BPS-/UPS-Patch Lab und GBA Audio Inspector (PSG + Direct Sound,
Stereo-WAV) eingebaut. [Paket 8–10, Bedienung und Grenzen](WINDOWS_PATCH_LAB_DIAGNOSTICS.md).

**W2 bleibt bis zum Windows-Spieltest zurückgestellt.** Laut Linux-Mitentwickler
laufen Pokémon und die HLE-BIOS-Korrekturen dort. Ein Windows-Durchspielnachweis
fehlt noch; synthetische Tests werden nicht als solcher ausgegeben. Gemeinsame
Core-/Runtime-Änderungen sind mit beiden Frontends zu prüfen.

Die Daten bleiben lokal. Keine ROMs oder BIOS-Dateien im Distributionspaket,
kein automatischer Versand von Berichten. Zur Weitergabe reicht der normale
vollständige Publish-Ordner auf einem USB-Stick.
