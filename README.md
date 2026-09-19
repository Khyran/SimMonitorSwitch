# SimMonitorSwitch

Kleine Tray-App für Windows 11, die deinen Simracing-Monitor nur dann aktiviert, wenn du ihn brauchst.
Im ausgeschalteten Zustand ist der Monitor für Windows nicht mehr Teil des Desktops. Es landen also keine Fenster und keine Maus mehr auf dem Bildschirm, den du gerade nicht siehst.

## Bauen

Voraussetzung: .NET 8 SDK auf dem Simracing-PC (oder einem anderen Windows-PC).

```powershell
cd SimMonitorSwitch
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Die fertige `SimMonitorSwitch.exe` liegt danach in `bin\Release\net8.0-windows\win-x64\publish\`.
Ohne .NET-Runtime auf dem Zielrechner: `--self-contained true` verwenden (die Datei wird dann deutlich grösser).

## Erste Einrichtung (einmalig)

1. `SimMonitorSwitch.exe` starten. Im Tray (Pfeil neben der Uhr) erscheint ein kleines Bildschirm-Symbol.
2. **Sim-Monitor muss dabei eingeschaltet sein.** Rechtsklick auf das Symbol, dann *Sim-Monitor auswählen* und den Simracing-Monitor anklicken. Die App merkt sich Auflösung, Bildwiederholrate und Position.
3. Rechtsklick, dann *Mit Windows starten* anhaken.

Der Hauptbildschirm lässt sich nicht auswählen, damit du dich nie aus Versehen aussperrst.

## Bedienung

| Aktion | Wie |
| --- | --- |
| Umschalten | Hotkey `Ctrl+Alt+S` oder Doppelklick auf das Tray-Symbol |
| Ein / Aus explizit | Rechtsklick, dann *Einschalten* oder *Ausschalten* |
| Automatik | *Automatisch bei Spielstart* im Menü (Standard: an) |
| Neues Spiel eintragen | Spiel starten, dann Rechtsklick, dann *Laufendes Programm als Spiel hinzufügen* |

Symbolfarbe: grün gefüllt = Monitor an, grauer Rahmen = aus, oranger Rahmen = nicht eingerichtet oder nicht gefunden.

### So funktioniert die Automatik

- Startet eines der eingetragenen Spiele, schaltet die App den Sim-Monitor ein.
- Ist das Spiel seit 10 Sekunden beendet, schaltet sie ihn wieder aus, aber nur, wenn sie ihn selbst eingeschaltet hat.
- Hast du den Monitor von Hand eingeschaltet (Hotkey), bleibt er an. Manuelle Aktionen haben immer Vorrang.

## Konfiguration

Menü *Konfigurationsdatei öffnen*, Datei liegt unter `%AppData%\SimMonitorSwitch\config.json`. Nach dem Speichern *Konfiguration neu laden* wählen.

| Feld | Bedeutung |
| --- | --- |
| `GameProcesses` | Prozessnamen ohne `.exe`. Die Vorgabeliste ist aus dem Gedächtnis geschrieben und nicht geprüft, am einfachsten trägst du dein Spiel über das Menü ein. |
| `Hotkey` | z. B. `Ctrl+Alt+S`, `Ctrl+Shift+F9`, `Win+Alt+M` |
| `PollSeconds` | Wie oft nach Spielen gesucht wird (Standard 2) |
| `DisableDelaySeconds` | Wartezeit nach Spielende bis zum Ausschalten (Standard 10) |
| `AutoMode` | Automatik an/aus |
| `EnableMethod` | `Extend` (Standard): Monitor wie bei `Win+P` → *Erweitern* einschalten. `Legacy`: ältere Methode, kann auf manchen Grafiktreibern ein schwarzes Bild liefern. |

## Wenn der Monitor nach dem Einschalten schwarz bleibt

1. Rechtsklick auf das Tray-Symbol, dann *Notfall: Alle Monitore erweitern*.
2. Menü *Log öffnen*: Dort steht Schritt für Schritt, was die App gemacht hat und welche Codes Windows zurückgab (`%AppData%\SimMonitorSwitch\log.txt`).
3. Wenn `EnableMethod` auf `Extend` steht und es trotzdem schwarz bleibt, probiere `Legacy` (und umgekehrt).

## Sicherheitsnetze

- Der Hauptbildschirm wird nie ausgeschaltet.
- Es wird nie der letzte aktive Bildschirm ausgeschaltet.
- Wird der Sim-Monitor an einen anderen Anschluss gesteckt, findet ihn die App über seine Hardware-ID wieder. Bei mehreren baugleichen Monitoren dient der Windows-Name (`\\.\DISPLAY3`) als zusätzliche Prüfung.
- Falls doch einmal etwas hängt: `Win+P`, dann *Erweitern* holt alle angeschlossenen Monitore zurück.

## Gut zu wissen

- Viele Sims (iRacing, ACC) lesen die Monitor-Liste beim Start. Die App prüft alle 2 Sekunden, das ist meist schnell genug. Erkennt ein Spiel den Monitor trotzdem nicht, schalte ihn vor dem Spielstart per Hotkey ein.
- Nach dem Einschalten kann es 1 bis 3 Sekunden dauern, bis Windows das Bild aufgebaut hat. Das ist normal.
- Der Monitor selbst muss eingeschaltet und angeschlossen bleiben. Die App trennt ihn nur in Windows vom Desktop und schaltet ihn nicht physisch aus.
