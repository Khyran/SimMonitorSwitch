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

    /// <summary>Wie T, waehlt aber Einzahl (key + ".one") oder Mehrzahl (key + ".many") nach count.</summary>
    public static string N(string key, int count, params object[] args) =>
        T(key + (count == 1 ? ".one" : ".many"), args);

    private static readonly Dictionary<string, (string En, string De)> Table = new()
    {
        // --- Tray: Status -------------------------------------------------
        ["label.default"]       = ("Sim monitor", "Sim-Monitor"),
        ["label.multiple"]      = ("{0} sim monitors", "{0} Sim-Monitore"),
        ["label.multipleShort"] = ("Sim monitors", "Sim-Monitore"),
        ["state.notConfigured"] = ("not set up", "nicht eingerichtet"),
        ["state.on"]            = ("ON", "AN"),
        ["state.off"]           = ("OFF", "AUS"),
        ["state.notFound"]      = ("not found", "nicht gefunden"),
        ["state.partial"]       = ("PARTLY ON ({0}/{1})", "TEILWEISE AN ({0}/{1})"),

        // --- Tray: Menue --------------------------------------------------
        ["menu.toggle"]         = ("Toggle   ({0})", "Umschalten   ({0})"),
        ["menu.on"]             = ("Enable", "Einschalten"),
        ["menu.off"]            = ("Disable", "Ausschalten"),
        ["menu.auto"]           = ("Automatic on game start", "Automatisch bei Spielstart"),
        ["menu.select"]         = ("Select sim monitors", "Sim-Monitore auswählen"),
        ["menu.addGame"]        = ("Add running program as game", "Laufendes Programm als Spiel hinzufügen"),
        ["menu.emergency"]      = ("Emergency: extend all monitors (like Win+P)", "Notfall: Alle Monitore erweitern (wie Win+P)"),
        ["menu.openConfig"]     = ("Open configuration file", "Konfigurationsdatei öffnen"),
        ["menu.reloadConfig"]   = ("Reload configuration", "Konfiguration neu laden"),
        ["menu.openLog"]        = ("Open log", "Log öffnen"),
        ["menu.autostart"]      = ("Start with Windows", "Mit Windows starten"),
        ["menu.language"]       = ("Language", "Sprache"),
        ["menu.languageAuto"]   = ("Automatic (Windows language)", "Automatisch (Windows-Sprache)"),
        ["menu.exit"]           = ("Exit", "Beenden"),
        ["menu.checkUpdate"]    = ("Check for updates", "Nach Updates suchen"),
        ["menu.installUpdate"]  = ("Install update {0}", "Update {0} installieren"),

        // --- Updates ------------------------------------------------------
        ["update.title"]        = ("Update", "Update"),
        ["update.available"]    = ("Version {0} is available (installed: v{1}). Click here or use the menu to install it.",
                                   "Version {0} ist verfügbar (installiert: v{1}). Zum Installieren hier klicken oder das Menü verwenden."),
        ["update.none"]         = ("You are using the latest version (v{0}).", "Du hast die neueste Version (v{0})."),
        ["update.checkFailed"]  = ("Could not check for updates: {0}", "Suche nach Updates fehlgeschlagen: {0}"),
        ["update.confirm"]      = ("Install version {0} now?\n\nThe app downloads the new version and restarts. This only takes a few seconds.",
                                   "Version {0} jetzt installieren?\n\nDie App lädt die neue Version herunter und startet neu. Das dauert nur ein paar Sekunden."),
        ["update.downloading"]  = ("Downloading {0} ...", "{0} wird heruntergeladen ..."),
        ["update.failed"]       = ("Update failed: {0} Click here to open the download page.",
                                   "Update fehlgeschlagen: {0} Hier klicken, um die Download-Seite zu öffnen."),
        ["update.noAccess"]     = ("No write access to \"{0}\". Move the app to a folder you own, or update manually.",
                                   "Kein Schreibzugriff auf \"{0}\". Die App in einen eigenen Ordner verschieben oder von Hand aktualisieren."),
        ["update.badDownload"]  = ("The downloaded file is incomplete or damaged.", "Die heruntergeladene Datei ist unvollständig oder beschädigt."),
        ["update.doneTitle"]    = ("Update installed", "Update installiert"),
        ["update.done"]         = ("SimMonitorSwitch is now on version v{0}.", "SimMonitorSwitch ist jetzt auf Version v{0}."),

        ["select.none"]         = ("No active monitors found", "Keine aktiven Monitore gefunden"),
        ["select.primary"]      = ("(primary display)", "(Hauptbildschirm)"),
        ["select.off"]          = ("(currently off)", "(gerade aus)"),
        ["select.missing"]      = ("(not found)", "(nicht gefunden)"),
        ["games.none"]          = ("No other programs with a window", "Keine weiteren Programme mit Fenster"),

        // --- Benachrichtigungen -------------------------------------------
        ["notify.setup.title"]  = ("Set up sim monitor", "Sim-Monitor einrichten"),
        ["notify.setup.body"]   = ("Right-click the tray icon and select your sim monitors there (they must be switched on at that moment).",
                                   "Rechtsklick auf das Tray-Icon und dort die Sim-Monitore auswählen (sie müssen dafür gerade eingeschaltet sein)."),
        ["notify.added"]        = ("Sim monitor added", "Sim-Monitor hinzugefügt"),
        ["notify.removed"]      = ("Sim monitor removed", "Sim-Monitor entfernt"),
        ["notify.error"]        = ("Error", "Fehler"),
        ["notify.simMonitor"]   = ("Sim monitor", "Sim-Monitor"),
        ["notify.noMonitor.title"] = ("No sim monitor selected", "Kein Sim-Monitor gewählt"),
        ["notify.noMonitor.body"]  = ("Please select your sim monitors in the menu first.", "Bitte zuerst im Menü die Sim-Monitore auswählen."),
        ["notify.extend"]       = ("Extend monitors", "Monitore erweitern"),
        ["notify.gameStarted"]  = ("Game detected", "Spiel erkannt"),
        ["notify.gameEnded"]    = ("Game ended", "Spiel beendet"),
        ["notify.gameAdded"]    = ("Game added", "Spiel hinzugefügt"),
        ["notify.gameAdded.body"] = ("\"{0}\" will now switch on the sim monitor(s).", "\"{0}\" schaltet ab jetzt die Sim-Monitore ein."),
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
        ["select.added"]        = ("Sim monitor added: {0} ({1}x{2} @ {3} Hz).", "Sim-Monitor hinzugefügt: {0} ({1}x{2} @ {3} Hz)."),
        ["select.removed"]      = ("Sim monitor removed: {0}.", "Sim-Monitor entfernt: {0}."),
        ["select.removedOn"]    = ("Sim monitor switched back on and removed: {0}.", "Sim-Monitor wieder eingeschaltet und entfernt: {0}."),
        ["select.removeFailed"] = ("Could not switch {0} back on, so it was not removed. {1}",
                                   "{0} konnte nicht wieder eingeschaltet werden und wurde deshalb nicht entfernt. {1}"),

        // --- Monitor: Einschalten -----------------------------------------
        ["enable.notFound.one"]  = ("Sim monitor not found. Is it connected and switched on?", "Sim-Monitor nicht gefunden. Ist er angeschlossen und eingeschaltet?"),
        ["enable.notFound.many"] = ("No sim monitor found. Are they connected and switched on?", "Kein Sim-Monitor gefunden. Sind sie angeschlossen und eingeschaltet?"),
        ["enable.already.one"]   = ("Sim monitor is already active.", "Sim-Monitor ist bereits aktiv."),
        ["enable.already.many"]  = ("Sim monitors are already active.", "Sim-Monitore sind bereits aktiv."),
        ["enable.noMode.one"]    = ("No saved settings. Please select the sim monitor again.", "Keine gespeicherten Einstellungen. Bitte den Sim-Monitor neu auswählen."),
        ["enable.noMode.many"]   = ("No saved settings. Please select the sim monitors again.", "Keine gespeicherten Einstellungen. Bitte die Sim-Monitore neu auswählen."),
        ["enable.failed.one"]    = ("Windows did not activate the monitor (maybe switched off or cable unplugged).",
                                    "Windows hat den Monitor nicht aktiviert (evtl. ausgeschaltet oder Kabel getrennt)."),
        ["enable.failed.many"]   = ("Windows did not activate the monitors (maybe switched off or cables unplugged).",
                                    "Windows hat die Monitore nicht aktiviert (evtl. ausgeschaltet oder Kabel getrennt)."),
        ["enable.ok"]            = ("Sim monitor switched on ({0}).", "Sim-Monitor eingeschaltet ({0})."),
        ["enable.okMulti"]       = ("{0} sim monitors switched on.", "{0} Sim-Monitore eingeschaltet."),
        ["enable.partial"]       = ("Only {0} of {1} sim monitors switched on. See the log for details.",
                                    "Nur {0} von {1} Sim-Monitoren eingeschaltet. Details im Log."),
        ["extend.ok"]           = ("Monitors extended.", "Monitore erweitert."),
        ["extend.failed"]       = ("SetDisplayConfig failed (code {0}).", "SetDisplayConfig fehlgeschlagen (Code {0})."),
        ["mode.unknown"]        = ("unknown", "unbekannt"),
        ["mode.describe"]       = ("{0}x{1} @ {2} Hz, position {3},{4}", "{0}x{1} @ {2} Hz, Position {3},{4}"),

        // --- Monitor: Ausschalten -----------------------------------------
        ["disable.notFound.one"]  = ("Sim monitor not found.", "Sim-Monitor nicht gefunden."),
        ["disable.notFound.many"] = ("No sim monitor found.", "Kein Sim-Monitor gefunden."),
        ["disable.already.one"]   = ("Sim monitor is already off.", "Sim-Monitor ist bereits aus."),
        ["disable.already.many"]  = ("Sim monitors are already off.", "Sim-Monitore sind bereits aus."),
        ["disable.isPrimary"]   = ("The sim monitor is currently the primary display. Aborted, otherwise there would be no primary display left.",
                                   "Der Sim-Monitor ist gerade der Hauptbildschirm. Abbruch, sonst gäbe es keinen Hauptbildschirm mehr."),
        ["disable.lastActive"]  = ("No active display would be left. Aborted.", "Es würde kein aktiver Bildschirm übrig bleiben. Abbruch."),
        ["disable.ok.one"]      = ("Sim monitor switched off.", "Sim-Monitor ausgeschaltet."),
        ["disable.ok.many"]     = ("{0} sim monitors switched off.", "{0} Sim-Monitore ausgeschaltet."),
        ["disable.failed.one"]  = ("Windows did not deactivate the monitor.", "Windows hat den Monitor nicht deaktiviert."),
        ["disable.failed.many"] = ("Windows did not deactivate all monitors.", "Windows hat nicht alle Monitore deaktiviert."),
        ["disable.skippedPrimary"] = ("{0} skipped (primary display).", "{0} übersprungen (Hauptbildschirm)."),

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
