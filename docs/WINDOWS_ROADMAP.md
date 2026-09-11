# Windows-Ausbau

Abgestimmt am 11. September 2026. Ein Projekt und eine Windows-Anwendung; der
Linux-Frontend-Ausbau bleibt beim Linux-Mitentwickler. Die folgenden Schritte
betreffen die Weiterentwicklung der Windows-Anwendung.

1. **Zentrale Ablage und Development-Diagnose:** umgesetzt. Die normale EXE
   zeichnet im Development-Kanal automatisch lokal auf. ROMs werden importiert,
   Saves/States nach Inhalt getrennt und Einstellungen versionsunabhängig abgelegt.
   Ordner lassen sich direkt in der Oberfläche öffnen. Eine separate Tester-EXE
   oder ein spezielles Tester-Paket ist nicht vorgesehen.
2. **Windows-Ausgabe (W3):** als Nächstes Audioausgabe und Messwerte verbessern:
   WASAPI, kontrollierte Puffer, Unterläufe und Gerätewechsel. Danach Frame-Pacing,
   VSync, GPU-Ausgabe, geringe Eingabelatenz und zusätzliche LCD-Effekte. Bestehende
   Integer-Skalierung, Filter, Windows Gaming Input und XInput werden weiterentwickelt.
   Zusätzliche Controllerpfade entstehen anhand tatsächlich fehlender Geräteunterstützung.
3. **Oberfläche (W4):** die Aether-Wave-Gestaltung, Control Center, Vollbild und
   DPI-Skalierung vervollständigen. Bibliothek mit Spielzeit/Save-Status, Profile
   pro Spiel, Controller-Einrichtung und verständliche Status-Overlays ausbauen.
4. **Save-Sicherheit (W5):** bestehende atomare Batterie-Saves, drei Backups,
   Integritätsprüfung und Restore-UI beibehalten. Auf der zentralen Ablage bessere
   Speicherstatus-Anzeigen, Import/Export und weitergehende Save-State-Sicherung
   entwickeln. Keine erneute Implementierung bereits vorhandener Schutzmechanismen.
5. **Diagnose vertiefen:** fehlenden Emulations-/UI-Fortschritt erfassen,
   ungewöhnlich lange Startzustände und dauerhaft einfarbige Bilder als Verdacht
   markieren. Pausen, Menüs und absichtliche Standbilder dürfen nicht automatisch
   als Absturz gelten. Buildberichte sollen vergleichbar werden.

**W2 bleibt bis zum Windows-Spieltest zurückgestellt.** Laut Linux-Mitentwickler
laufen Pokémon und die HLE-BIOS-Korrekturen dort. Ein Windows-Durchspielnachweis
fehlt noch; synthetische Tests werden nicht als solcher ausgegeben. Gemeinsame
Core-/Runtime-Änderungen sind mit beiden Frontends zu prüfen.

Die Daten bleiben lokal. Keine ROMs oder BIOS-Dateien im Distributionspaket,
kein automatischer Versand von Berichten. Zur Weitergabe reicht der normale
vollständige Publish-Ordner auf einem USB-Stick.
