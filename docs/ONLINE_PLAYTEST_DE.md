# Windows ↔ Linux: erster Online-Test über zwei Internetanschlüsse

Stand: 12. September 2026, gemeinsamer Entwicklungsstand `0566c96`.
Online Link ist ein Entwicklungsprototyp. Ein echter Pokémon-Tausch über zwei
Internetanschlüsse ist noch nicht nachgewiesen.

## Was zuerst testen?

Als ersten Spielversuch empfehlen wir Pokémon Rot/Blau (GB), beide in derselben
Sprache, mit entbehrlichen Testspielständen. Das ist ein Testkandidat, keine
bestätigte Kompatibilitätszusage. Bereitet die Spielstände offline so weit vor,
dass beide Spieler tauschen können, und speichert im Spiel vor dem Kabelraum.
Online Link startet das Spiel anschließend mit einer Kopie dieses Spielstands neu.

GBA erst als zweiten Versuch: Das Gen3-Profil lässt nur genau erkannte Fassungen
zu, darunter deutsche Rubin/Saphir-Fassungen. Deutsche Feuerrot/Blattgrün/Smaragd
stehen derzeit nicht in der Freigabeliste. Auch die erkannten Fassungen sind
ausdrücklich ungetestete Entwicklungsprofile. ROM-Hacks werden nicht allein
anhand ihres Titels freigegeben. Details: [GBA-Onlineprofil](GBA_ONLINE_HANDOFF.md).

## Start und Verbindung

1. Beide verwenden den neuen AetherBoy-Entwicklungsstand und öffnen ihr eigenes
   Spiel. Linux aus diesem Checkout: `bash scripts/run-linux.sh`.
2. Linux: **F10 → CHOOSE HOST** oder **CHOOSE GUEST → START OWN SAVE COPY**.
   Windows: **TOOLS → Online Link → Sitzung erstellen / beitreten**.
   Genau einen Host und einen Gast wählen.
3. Auf **beiden** automatisch geöffneten Browserseiten zuerst
   **Optional: STUN / TURN und Verbindungsprivatsphäre** ausklappen.
   Für einen ersten Versuch könnt ihr als STUN-Adresse
   `stun:stun.l.google.com:19302` eintragen und den Serverkontakt bestätigen.
   Diese Adresse ist der Standard im offiziellen
   [WebRTC-Testbeispiel](https://github.com/webrtc/samples/blob/gh-pages/src/content/peerconnection/trickle-ice/js/main.js).
   Sie bietet keine Garantie, dass eure beiden Router eine direkte Verbindung zulassen.
   TURN zunächst leer und **Nur über meinen TURN-Server verbinden** ausgeschaltet lassen.
4. Host: **Einladung erstellen**, warten, dann den erzeugten Text an den Gast geben.
   Gast: Text einfügen, **Aus Einladung eine Antwort erstellen**, Antwort zurückgeben.
   Host: Antwort einfügen und **Antwort übernehmen**. Tauscht die Texte aus,
   nicht die lokale `127.0.0.1`-Browseradresse.
5. Beide Browserseiten geöffnet lassen und zu den Emulatorfenstern zurückkehren.
   Einstellungen schließen und Pause beenden. Erst danach den Kabelraum im Spiel betreten.
   Turbo, Rewind und Save States bleiben während Online Link gesperrt.

Serverangaben müssen vor dem Erstellen von Einladung bzw. Antwort eingetragen sein.
Nach einem Fehlversuch die Sitzung im Emulator beenden und eine neue erstellen;
die alte Browserseite nicht durch Neuladen wiederverwenden.

## Wenn keine Verbindung zustande kommt

STUN hilft beim Finden direkter Verbindungswege. Wenn eure Router/Firewalls diese
nicht zulassen, braucht ihr einen erreichbaren TURN-Server mit gültigen Zugangsdaten.
AetherBoy bringt keinen betriebenen Relay-Dienst und keine Zugangsdaten mit.
Die Browserseite unterstützt TURN-Adresse, Benutzername und Passwort sowie einen
Relay-Modus. Siehe die [offizielle WebRTC-Erklärung zu TURN](https://webrtc.org/getting-started/turn-server).

Mit dem [offiziellen Trickle-ICE-Test](https://webrtc.github.io/samples/src/content/peerconnection/trickle-ice/)
könnt ihr unabhängig vom Spiel prüfen: `srflx` zeigt eine erfolgreiche STUN-Abfrage,
`relay` einen TURN-Kandidaten. Das allein beweist noch keine Verbindung zwischen euren Rechnern.

## Drei getrennte Prüfschritte

| Schritt | Erfolgskriterium | Was noch nicht bewiesen ist |
| --- | --- | --- |
| Verbindung | Beide Browser zeigen verbunden, beide Emulatoren verlassen den Handshake | Funktionierendes Spielprotokoll |
| Kabelraum | Beide Spiele erkennen sich, Transferzähler steigen | Vollständig gespeicherter Tausch |
| Tausch | Ein Test-Pokémon pro Seite getauscht, beide Spiele speichern, beide gespeicherten Kopien nach Neustart geprüft | Allgemeine Spielekompatibilität |

Zum Beenden zuerst die Online-Sitzung **im Emulator** schließen. Das Schließen des
Browser-Tabs trennt sofort. Bei GB/GBC kann das Ende einer Seite auf der Gegenseite
als Verbindungsfehler erscheinen; die beidseitige geordnete Abschlussvereinbarung
ist derzeit im GBA-Profil implementiert.

Originalspielstände werden nicht automatisch ersetzt. Unter **CHECK SAVE COPIES**
bzw. **Sitzungskopien prüfen / übernehmen** lassen sich abgeschlossene Kopien prüfen.
Erst nach Prüfung beider Ergebnisse bewusst übernehmen; als unsicher eingestufte
Kopien nicht durch Bearbeiten des Journals freischalten.

Bei einem Fehler festhalten: Spiel/Edition/Sprache, beide Betriebssysteme und
Buildstände, Browser, Host/Gast, STUN oder TURN, genaue Fehlermeldung und letzter
erreichter Schritt. Keine Einladungstexte, lokalen Browsergeheimnisse oder
TURN-Passwörter in öffentliche Fehlerberichte kopieren.
