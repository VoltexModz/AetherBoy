# AetherBoy unter Linux: natives Wayland

Der Linux-Client ist ein eigener SDL3-Desktop-Host für `AetherBoy.Core` und
`AetherBoy.Runtime`. Er startet ausschließlich mit dem SDL-Treiber `wayland` und
bricht ab, wenn nur X11 beziehungsweise XWayland verfügbar ist. Die stabile
Anwendungs-ID lautet `io.github.VoltexModz.AetherBoy`.

## Voraussetzungen

- eine native Wayland-Sitzung mit funktionierendem GPU-/Mesa- oder Herstellertreiber
- .NET SDK 10.0.302 oder ein neuerer Patch derselben Feature-Band
- ICU sowie die üblichen Systembibliotheken des .NET-Runtimes
- für Hyprland: `xdg-desktop-portal-hyprland` und ein GTK- oder KDE-Portal als
  Dateiauswahl-Fallback

Unter Ubuntu 26.04 oder 24.04 kann das SDK aus den Ubuntu-Paketquellen installiert
werden:

```bash
sudo apt update
sudo apt install dotnet-sdk-10.0
```

Prüfe vor dem Start die Sitzung:

```bash
printf '%s / %s / %s\n' "$XDG_SESSION_TYPE" "$XDG_CURRENT_DESKTOP" "$WAYLAND_DISPLAY"
```

`WAYLAND_DISPLAY` muss gesetzt sein. `XDG_SESSION_TYPE=x11` wird absichtlich
abgelehnt.

## Bauen und starten

Im Repository-Root:

```bash
bash scripts/build-linux.sh
bash scripts/run-linux.sh "/pfad/zu/deinem-spiel.gba"
```

Ohne ROM-Pfad öffnet sich die Oberfläche ebenfalls. Eine rechtmäßig verwendbare
`.gb`, `.gbc` oder `.gba` kann anschließend auf das Fenster gezogen werden.

Optional installiert das folgende Skript den Build, den Desktop-Eintrag und alle
Icon-Größen nur für den aktuellen Benutzer unter `~/.local`:

```bash
bash scripts/install-linux-user.sh
```

Danach erscheint AetherBoy im App-Menü. Alternativ startet `aetherboy
"/pfad/zu/deinem-spiel.gbc"`, sofern `~/.local/bin` im `PATH` liegt.

Optional als Benutzeranwendung mit AetherBoy-Icon und Desktop-Eintrag installieren:

```bash
bash scripts/install-linux-user.sh
aetherboy "/pfad/zu/deinem-spiel.gba"
```

Dabei landen Programmdateien unter `~/.local/share/aetherboy`, der Starter unter
`~/.local/bin/aetherboy` und der Desktop-Eintrag unter
`~/.local/share/applications`. Es werden keine Root-Rechte benötigt.

Nur die Desktop-Erkennung prüfen, ohne SDL-Fenster oder ROM:

```bash
dotnet run --project frontends/AetherBoy.Desktop/AetherBoy.Desktop.csproj -- --platform-info
```

## Bedienung des ersten Linux-Hosts

| Aktion | Tastatur |
| --- | --- |
| Steuerkreuz | Pfeiltasten |
| A / B | Z / X |
| Start / Select | Eingabe / Rücktaste |
| GBA L / R | Q / E |
| Pause | Leertaste |
| Turbo halten | Tabulator |
| Schnellspeichern / laden, Slot 1 | F5 / F8 |
| Vollbild | F11 |
| Beenden | Escape |

SDL3-Gamepads werden beim Start und über Hot-Plug erkannt. Batterie-Spielstände
(`.sav`) und Slot 1 (`.ss1`) liegen neben der ROM.

## Hyprland-Profil

AetherBoy erkennt Hyprland über `XDG_CURRENT_DESKTOP` oder
`HYPRLAND_INSTANCE_SIGNATURE`, setzt eine feste Wayland-App-ID und verzichtet dort
auf die optionale `libdecor`-Abhängigkeit. Dadurch kann Hyprland Fensterrahmen,
Skalierung und Regeln compositorseitig verwalten.

`xdg-desktop-portal-hyprland` besitzt selbst keine Dateiauswahl. Das mitgelieferte
Profil lässt deshalb das Hyprland-Portal zuerst arbeiten und delegiert
`FileChooser` ausdrücklich an GTK:

```bash
mkdir -p ~/.config/xdg-desktop-portal
cp packaging/linux/hyprland/hyprland-portals.conf \
  ~/.config/xdg-desktop-portal/hyprland-portals.conf
systemctl --user restart xdg-desktop-portal xdg-desktop-portal-hyprland
```

Wer KDEs Dateidialog bevorzugt, ersetzt in der letzten Zeile der Konfiguration
`gtk` durch `kde` und installiert das KDE-Portal. Eine bestehende persönliche
Portal-Konfiguration sollte nicht blind überschrieben, sondern zusammengeführt
werden.

## Aktuelle Grenze

Der Linux-Host beherrscht Bild, Tastatur/Gamepad, Pause, Turbo, Vollbild,
Batterie-Saves und einen Save-State-Slot. Audio, das vollständige Control Center,
alle fünf sichtbaren Slots, Rewind-Bedienung und ein nativer Portal-Öffnen-Dialog
sind noch nicht auf dem Stand des Windows-Frontends. Der Emulator-Core selbst ist
derselbe; die verbleibende Arbeit betrifft den Linux-Desktopadapter.

WSLg ist für einen Backend-Smoke-Test geeignet, aber kein Ersatz für einen Test
unter echtem Hyprland. Fehlt WSLs Mesa/EGL-Stack, kann SDL den Wayland-Treiber
korrekt auswählen und trotzdem beim Erzeugen des Renderers scheitern.
