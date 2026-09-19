# SimMonitorSwitch

A small tray app for Windows 11 that activates your sim racing monitor only when you need it.
While it is switched off, Windows no longer treats the monitor as part of the desktop, so no windows or mouse pointer end up on a screen you can't see.

## Building

Requirement: .NET 8 SDK on the sim racing PC (or any other Windows PC).

```powershell
cd SimMonitorSwitch
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

The finished `SimMonitorSwitch.exe` ends up in `bin\Release\net8.0-windows\win-x64\publish\`.
If the target machine has no .NET runtime installed, use `--self-contained true` instead (the file will be considerably larger).

## First-time setup (one time only)

1. Start `SimMonitorSwitch.exe`. A small monitor icon appears in the tray (the arrow next to the clock).
2. **The sim monitor must be switched on for this step.** Right-click the icon, choose *Sim-Monitor auswählen* (select sim monitor), and click your sim racing monitor. The app remembers its resolution, refresh rate and position.
3. Right-click again and tick *Mit Windows starten* (start with Windows).

The main display cannot be selected, so you can never lock yourself out by accident.

## Usage

| Action | How |
| --- | --- |
| Toggle | Hotkey `Ctrl+Alt+S` or double-click the tray icon |
| Explicit on / off | Right-click, then *Einschalten* (enable) or *Ausschalten* (disable) |
| Automatic mode | *Automatisch bei Spielstart* (automatic on game start) in the menu (default: on) |
| Add a new game | Start the game, then right-click and choose *Laufendes Programm als Spiel hinzufügen* (add running program as game) |

Icon colors: filled green = monitor on, gray outline = off, orange outline = not set up or not found.

### How automatic mode works

- When one of the registered games starts, the app switches the sim monitor on.
- Once the game has been closed for 10 seconds, the app switches the monitor off again, but only if it was the one that switched it on.
- If you switched the monitor on manually (hotkey), it stays on. Manual actions always take precedence.

## Configuration

Use the *Konfigurationsdatei öffnen* (open config file) menu entry. The file is located at `%AppData%\SimMonitorSwitch\config.json`. After saving, choose *Konfiguration neu laden* (reload configuration).

| Field | Meaning |
| --- | --- |
| `GameProcesses` | Process names without `.exe`. The default list was written from memory and is unverified; the easiest way is to add your game through the menu. |
| `Hotkey` | e.g. `Ctrl+Alt+S`, `Ctrl+Shift+F9`, `Win+Alt+M` |
| `PollSeconds` | How often the app checks for running games (default 2) |
| `DisableDelaySeconds` | Wait time after the game exits before the monitor is switched off (default 10) |
| `AutoMode` | Automatic mode on/off |
| `EnableMethod` | `Extend` (default): enables the monitor the same way as `Win+P` → *Extend*. `Legacy`: older method, which may produce a black screen with some graphics drivers. |

## If the monitor stays black after switching on

1. Right-click the tray icon and choose *Notfall: Alle Monitore erweitern* (emergency: extend all monitors).
2. Open the log via the *Log öffnen* menu entry. It lists step by step what the app did and which codes Windows returned (`%AppData%\SimMonitorSwitch\log.txt`).
3. If `EnableMethod` is set to `Extend` and the screen still stays black, try `Legacy` (and vice versa).

## Safety nets

- The main display is never switched off.
- The last active display is never switched off.
- If the sim monitor is plugged into a different port, the app finds it again via its hardware ID. When several identical monitors are connected, the Windows name (`\\.\DISPLAY3`) serves as an additional check.
- If something does get stuck: press `Win+P`, then choose *Extend* to bring back all connected monitors.

## Good to know

- Many sims (iRacing, ACC) read the monitor list at startup. The app checks every 2 seconds, which is usually fast enough. If a game still doesn't detect the monitor, switch it on with the hotkey before launching the game.
- After switching on, it can take 1 to 3 seconds for Windows to bring up the picture. This is normal.
- The monitor itself must stay powered on and connected. The app only detaches it from the desktop in Windows; it does not switch the monitor off physically.
