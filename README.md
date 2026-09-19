# SimMonitorSwitch

A small tray app for Windows 11 that activates your sim racing monitors only when you need them. It works with a single monitor or a multi-monitor setup (for example a triple-screen rig): all selected monitors are switched on and off together.
While they are switched off, Windows no longer treats the monitors as part of the desktop, so no windows or mouse pointer end up on a screen you can't see.

## Building

Requirement: .NET 8 SDK on the sim racing PC (or any other Windows PC).

```powershell
cd SimMonitorSwitch
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

The finished `SimMonitorSwitch.exe` ends up in `bin\Release\net8.0-windows\win-x64\publish\`.
If the target machine has no .NET runtime installed, use `--self-contained true` instead (the file will be considerably larger).

## Language

The app is available in English and German. By default it follows the Windows display language (German if Windows is set to German, English otherwise). To change it, right-click the tray icon, choose *Language*, and pick *English*, *Deutsch* or *Automatic*. The change applies immediately and is stored in the `Language` field of the config file.

Menu entries are quoted below by their English names. In German they read: *Sim-Monitore auswählen*, *Mit Windows starten*, *Einschalten* / *Ausschalten*, *Automatisch bei Spielstart*, *Laufendes Programm als Spiel hinzufügen*, *Konfigurationsdatei öffnen*, *Konfiguration neu laden*, *Notfall: Alle Monitore erweitern (wie Win+P)*, *Log öffnen*, *Sprache*.

## First-time setup (one time only)

1. Start `SimMonitorSwitch.exe`. A small monitor icon appears in the tray (the arrow next to the clock).
2. **Your sim monitors must be switched on for this step.** Right-click the icon, choose *Select sim monitors*, and click a sim racing monitor to add it. Repeat for every sim monitor: the menu closes after each click, so open it again for the next one. The app remembers each monitor's resolution, refresh rate and position.
3. Right-click again and tick *Start with Windows*.

Checked entries in the *Select sim monitors* menu are your sim monitors. Click a checked entry to remove it again. Monitors that are currently switched off stay in the list, marked *(currently off)*, so you can still see and remove them.

The main display cannot be selected, so you can never lock yourself out by accident.

If you remove a monitor that is currently switched off, it stays off until you bring it back with *Emergency: extend all monitors* or `Win+P`.

## Usage

| Action | How |
| --- | --- |
| Toggle | Hotkey `Ctrl+Alt+S` or double-click the tray icon |
| Explicit on / off | Right-click, then *Enable* or *Disable* |
| Automatic mode | *Automatic on game start* in the menu (default: on) |
| Add a new game | Start the game, then right-click and choose *Add running program as game* |

Icon colors: filled green = all sim monitors on, gray outline = off, filled orange = only some of them on, orange outline = not set up or not found.

With several sim monitors, *Toggle* switches them all off if every connected one is on. In any other state (all off, or only some on) it switches them all on.

### How automatic mode works

- When one of the registered games starts, the app switches the sim monitors on.
- Once the game has been closed for 10 seconds, the app switches them off again, but only if it was the one that switched them on.
- If you switched the monitors on manually (hotkey), they stay on. Manual actions always take precedence.

## Configuration

Use the *Open configuration file* menu entry. The file is located at `%AppData%\SimMonitorSwitch\config.json`. After saving, choose *Reload configuration*.

| Field | Meaning |
| --- | --- |
| `SimMonitors` | The list of sim monitors. Each entry stores the hardware ID, the last Windows name, a label and the resolution/position used when switching it on. Manage it through the *Select sim monitors* menu rather than by hand. A config from version 1.0 (single monitor) is converted automatically. |
| `GameProcesses` | Process names without `.exe`. The default list was written from memory and is unverified; the easiest way is to add your game through the menu. |
| `Hotkey` | e.g. `Ctrl+Alt+S`, `Ctrl+Shift+F9`, `Win+Alt+M` |
| `PollSeconds` | How often the app checks for running games (default 2) |
| `DisableDelaySeconds` | Wait time after the game exits before the monitors are switched off (default 10) |
| `AutoMode` | Automatic mode on/off |
| `Language` | `Auto` (default, follows the Windows language), `en` or `de` |
| `EnableMethod` | `Extend` (default): enables the monitor the same way as `Win+P` → *Extend*. `Legacy`: older method, which may produce a black screen with some graphics drivers. |

## If the monitor stays black after switching on

1. Right-click the tray icon and choose *Emergency: extend all monitors (like Win+P)*.
2. Open the log via the *Open log* menu entry. It lists step by step what the app did and which codes Windows returned (`%AppData%\SimMonitorSwitch\log.txt`). The log is always in English.
3. If `EnableMethod` is set to `Extend` and the screen still stays black, try `Legacy` (and vice versa).

## Safety nets

- The main display is never switched off, and can't be added as a sim monitor.
- The last active display is never switched off.
- If a sim monitor is plugged into a different port, the app finds it again via its hardware ID. When several identical monitors are connected, the Windows name (`\\.\DISPLAY3`) serves as an additional check, and each physical monitor is only ever matched to one entry.
- If something does get stuck: press `Win+P`, then choose *Extend* to bring back all connected monitors.

## Good to know

- Many sims (iRacing, ACC) read the monitor list at startup. The app checks every 2 seconds, which is usually fast enough. If a game still doesn't detect the monitor, switch it on with the hotkey before launching the game.
- After switching on, it can take 1 to 3 seconds for Windows to bring up the picture. This is normal.
- With several sim monitors, if one of them is unplugged or powered off, the app still switches the others on and off. The tray status then shows *PARTLY ON (x/y)*.
- The monitors themselves must stay powered on and connected. The app only detaches them from the desktop in Windows; it does not switch them off physically.
