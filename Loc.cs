using System.Globalization;

namespace SimMonitorSwitch;

/// <summary>Kleine Uebersetzungstabelle (Englisch/Deutsch). Sprache: "Auto", "en" oder "de".</summary>
internal static class Loc
{
    public const string Auto = "Auto";

    private static volatile bool _german;

    public static bool IsGerman => _german;

    /// <summary>"de" / "en" erzwingen, alles andere ("Auto") folgt der Windows-Anzeigesprache.</summary>
    public static void SetLanguage(string? setting)
    {
        _german = setting?.Trim().ToLowerInvariant() switch
        {
            "de" => true,
            "en" => false,
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de",
        };
    }

    /// <summary>Text zum Schluessel in der aktuellen Sprache. Platzhalter {0}, {1} ... werden mit args gefuellt.</summary>
    public static string T(string key, params object[] args)
    {
        if (!Table.TryGetValue(key, out var pair))
            return key;   // fehlender Schluessel faellt sofort auf

        var text = _german ? pair.De : pair.En;
        return args.Length == 0 ? text : string.Format(CultureInfo.CurrentCulture, text, args);
    }

    private static readonly Dictionary<string, (string En, string De)> Table = new()
    {
        // --- Tray: Status -------------------------------------------------
        ["label.default"]       = ("Sim monitor", "Sim-Monitor"),
        ["state.notConfigured"] = ("not set up", "nicht eingerichtet"),
        ["state.on"]            = ("ON", "AN"),
        ["state.off"]           = ("OFF", "AUS"),
        ["state.notFound"]      = ("not found", "nicht gefunden"),
        ["tray.tooltip"]        = ("Sim monitor: {0}", "Sim-Monitor: {0}"),

        // --- Tray: Menue --------------------------------------------------
        ["menu.toggle"]         = ("Toggle   ({0})", "Umschalten   ({0})"),
        ["menu.on"]             = ("Enable", "Einschalten"),
        ["menu.off"]            = ("Disable", "Ausschalten"),
        ["menu.auto"]           = ("Automatic on game start", "Automatisch bei Spielstart"),
        ["menu.select"]         = ("Select sim monitor", "Sim-Monitor auswählen"),
        ["menu.addGame"]        = ("Add running program as game", "Laufendes Programm als Spiel hinzufügen"),
        ["menu.emergency"]      = ("Emergency: extend all monitors (like Win+P)", "Notfall: Alle Monitore erweitern (wie Win+P)"),
        ["menu.openConfig"]     = ("Open configuration file", "Konfigurationsdatei öffnen"),
        ["menu.reloadConfig"]   = ("Reload configuration", "Konfiguration neu laden"),
        ["menu.openLog"]        = ("Open log", "Log öffnen"),
        ["menu.autostart"]      = ("Start with Windows", "Mit Windows starten"),
        ["menu.language"]       = ("Language", "Sprache"),
        ["menu.languageAuto"]   = ("Automatic (Windows language)", "Automatisch (Windows-Sprache)"),
        ["menu.exit"]           = ("Exit", "Beenden"),

        ["select.none"]         = ("No active monitors found", "Keine aktiven Monitore gefunden"),
        ["select.primary"]      = ("(primary display)", "(Hauptbildschirm)"),
        ["games.none"]          = ("No other programs with a window", "Keine weiteren Programme mit Fenster"),

        // --- Benachrichtigungen -------------------------------------------
        ["notify.setup.title"]  = ("Set up sim monitor", "Sim-Monitor einrichten"),
        ["notify.setup.body"]   = ("Right-click the tray icon and select the sim monitor there (it must be switched on at that moment).",
                                   "Rechtsklick auf das Tray-Icon und dort den Sim-Monitor auswählen (er muss dafür gerade eingeschaltet sein)."),
        ["notify.saved"]        = ("Sim monitor saved", "Sim-Monitor gespeichert"),
        ["notify.error"]        = ("Error", "Fehler"),
        ["notify.simMonitor"]   = ("Sim monitor", "Sim-Monitor"),
        ["notify.noMonitor.title"] = ("No sim monitor selected", "Kein Sim-Monitor gewählt"),
        ["notify.noMonitor.body"]  = ("Please select the sim monitor in the menu first.", "Bitte zuerst im Menü den Sim-Monitor auswählen."),
        ["notify.extend"]       = ("Extend monitors", "Monitore erweitern"),
        ["notify.gameStarted"]  = ("Game detected", "Spiel erkannt"),
        ["notify.gameEnded"]    = ("Game ended", "Spiel beendet"),
        ["notify.gameAdded"]    = ("Game added", "Spiel hinzugefügt"),
        ["notify.gameAdded.body"] = ("\"{0}\" will now switch on the sim monitor.", "\"{0}\" schaltet ab jetzt den Sim-Monitor ein."),
        ["notify.hotkey"]       = ("Hotkey unavailable", "Hotkey nicht verfügbar"),
        ["notify.config"]       = ("Configuration", "Konfiguration"),
        ["notify.reloaded"]     = ("Reloaded.", "Neu geladen."),

        // --- Hotkey -------------------------------------------------------
        ["hotkey.inUse"]        = ("Hotkey \"{0}\" is already in use by another program.", "Hotkey \"{0}\" ist schon von einem anderen Programm belegt."),
        ["hotkey.none"]         = ("No hotkey specified.", "Kein Hotkey angegeben."),
        ["hotkey.unknownKey"]   = ("Unknown key \"{0}\" in hotkey.", "Unbekannte Taste \"{0}\" im Hotkey."),
        ["hotkey.needModifier"] = ("The hotkey needs at least one modifier (Ctrl/Alt/Shift/Win) and one key.",
                                   "Der Hotkey braucht mindestens einen Modifier (Ctrl/Alt/Shift/Win) und eine Taste."),

        // --- Monitor: Auswaehlen ------------------------------------------
        ["select.mustBeActive"] = ("The monitor must be active for this.", "Der Monitor muss dafür gerade aktiv sein."),
        ["select.isPrimary"]    = ("This is the primary display. It cannot be switched off.", "Das ist der Hauptbildschirm. Dieser kann nicht ausgeschaltet werden."),
        ["select.readFailed"]   = ("Could not read the monitor's current settings.", "Aktuelle Einstellungen des Monitors konnten nicht gelesen werden."),
        ["select.ok"]           = ("Sim monitor saved: {0} ({1}x{2} @ {3} Hz).", "Sim-Monitor gespeichert: {0} ({1}x{2} @ {3} Hz)."),

        // --- Monitor: Einschalten -----------------------------------------
        ["enable.notFound"]     = ("Sim monitor not found. Is it connected and switched on?", "Sim-Monitor nicht gefunden. Ist er angeschlossen und eingeschaltet?"),
        ["enable.already"]      = ("Sim monitor is already active.", "Sim-Monitor ist bereits aktiv."),
        ["enable.noMode"]       = ("No saved settings. Please select the sim monitor again.", "Keine gespeicherten Einstellungen. Bitte den Sim-Monitor neu auswählen."),
        ["enable.failed"]       = ("Windows did not activate the monitor (maybe switched off or cable unplugged).",
                                   "Windows hat den Monitor nicht aktiviert (evtl. ausgeschaltet oder Kabel getrennt)."),
        ["enable.ok"]           = ("Sim monitor switched on ({0}).", "Sim-Monitor eingeschaltet ({0})."),
        ["extend.ok"]           = ("Monitors extended.", "Monitore erweitert."),
        ["extend.failed"]       = ("SetDisplayConfig failed (code {0}).", "SetDisplayConfig fehlgeschlagen (Code {0})."),
        ["mode.unknown"]        = ("unknown", "unbekannt"),
        ["mode.describe"]       = ("{0}x{1} @ {2} Hz, position {3},{4}", "{0}x{1} @ {2} Hz, Position {3},{4}"),

        // --- Monitor: Ausschalten -----------------------------------------
        ["disable.notFound"]    = ("Sim monitor not found.", "Sim-Monitor nicht gefunden."),
        ["disable.already"]     = ("Sim monitor is already off.", "Sim-Monitor ist bereits aus."),
        ["disable.isPrimary"]   = ("The sim monitor is currently the primary display. Aborted, otherwise there would be no primary display left.",
                                   "Der Sim-Monitor ist gerade der Hauptbildschirm. Abbruch, sonst gäbe es keinen Hauptbildschirm mehr."),
        ["disable.lastActive"]  = ("No active display would be left. Aborted.", "Es würde kein aktiver Bildschirm übrig bleiben. Abbruch."),
        ["disable.ok"]          = ("Sim monitor switched off.", "Sim-Monitor ausgeschaltet."),
        ["disable.failed"]      = ("Windows did not deactivate the monitor.", "Windows hat den Monitor nicht deaktiviert."),

        // --- Windows-Fehlercodes ------------------------------------------
        ["apply.rejected"]      = ("Windows rejected the change ({0}).", "Windows lehnt die Änderung ab ({0})."),
        ["apply.failed"]        = ("Applying failed ({0}).", "Anwenden fehlgeschlagen ({0})."),
        ["err.-1"]              = ("DISP_CHANGE_FAILED", "DISP_CHANGE_FAILED"),
        ["err.-2"]              = ("mode not supported", "Modus wird nicht unterstützt"),
        ["err.-3"]              = ("registry error", "Registry-Fehler"),
        ["err.-4"]              = ("invalid flags", "ungültige Flags"),
        ["err.-5"]              = ("invalid parameters", "ungültige Parameter"),
        ["err.-6"]              = ("configuration cannot be changed (display settings open?)",
                                   "Konfiguration nicht änderbar (evtl. Bildschirm-Einstellungen geöffnet?)"),
        ["err.1"]               = ("restart required", "Neustart nötig"),
        ["err.other"]           = ("error code {0}", "Fehlercode {0}"),
    };
}
