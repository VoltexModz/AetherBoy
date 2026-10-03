# Portable Mode (Windows und Linux)

Portable Mode speichert neue ROM-Kopien, Spielstände, Save States, Einstellungen, Firmware und lokale Berichte beim Programm. Die normalen Profilordner bleiben unverändert. Es findet **keine automatische Übernahme** bisheriger Daten statt.
Beim Öffnen einer ROM legt AetherBoy eine eigene Kopie im portablen Datenordner ab; die Quelldatei bleibt erhalten.

## Einschalten

Entpackte Anwendung in einen beschreibbaren Ordner kopieren und entweder mit `--portable` starten oder im Ordner der ausführbaren Datei eine leere Datei namens `aetherboy.portable` anlegen. Ein Dateiname wie `aetherboy.portable.txt` reicht nicht aus. Der Modus wird beim Start festgelegt; danach legt AetherBoy neben der Anwendung `AetherBoyData` an.

- Windows: `AetherBoy.exe --portable` (oder Markerdatei). Alle App-Daten liegen unter `AetherBoyData`.
- Linux: `./AetherBoy.Desktop --portable` (oder Markerdatei). Daten, Einstellungen, Status und Cache liegen unter `AetherBoyData/data`, `config`, `state` und `cache`. Die native Wayland-Anforderung bleibt bestehen.

Kann AetherBoy den Ordner nicht beschreiben, startet der portable Modus nicht. Es wird **nicht** stillschweigend auf AppData oder XDG-Ordner ausgewichen. Eine Installation unter einem schreibgeschützten Systempfad eignet sich daher nicht; verwende eine entpackte Kopie im eigenen Benutzer- oder USB-Ordner.

## Vorhandene Daten mitnehmen

Beende AetherBoy zuerst. Kopiere benötigte ROMs und Spielstände bewusst in den neuen Datenordner oder importiere ROMs erneut und übertrage Spielstände mit den vorhandenen Importwerkzeugen. Ersetze bestehende Spielstände nicht ungeprüft. Für ein Backup genügt es, die Anwendung zusammen mit `AetherBoyData` zu kopieren, solange keine Sitzung läuft.

Entfernst du `--portable` oder die Markerdatei vor dem nächsten Start, nutzt AetherBoy wieder die normalen Profilordner. Die portablen Daten bleiben in `AetherBoyData` erhalten und werden nicht gelöscht.

## Ordner oder Laufwerk wechseln

Beende die Anwendung und nimm den vollständigen `AetherBoyData`-Ordner mit.
Windows referenziert verwaltete ROMs in „Zuletzt geöffnet“ per Inhaltsidentität;
alte absolute Bibliothekspfade werden beim Lesen auf die passende, hashgeprüfte
Kopie im aktuellen ROM-Ordner aufgelöst. Linux speichert interne Bibliothekspfade
relativ zu seinem Datenordner. Externe ROM-Verweise werden dadurch nicht automatisch
kopiert oder auf anderen PCs verfügbar.

Windows und Linux verwenden weiterhin unterschiedliche Unterordner und Metadatenformate.
Ein portables Profil ist deshalb noch kein austauschbares Windows/Linux-Profil.
Ein echter Laufwerkswechsel und Start auf dem Zielgerät bleibt Teil des praktischen Tests.
