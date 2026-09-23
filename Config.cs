using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimMonitorSwitch;

/// <summary>Gespeicherte Anzeigeeinstellungen des Sim-Monitors (fuer das Wiedereinschalten).</summary>
public sealed class SavedMode
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int Frequency { get; set; } = 60;
    public int Orientation { get; set; }
    public int BitsPerPel { get; set; } = 32;
}

/// <summary>Ein Sim-Monitor: wie er wiedergefunden wird und mit welchen Einstellungen er eingeschaltet wird.</summary>
public sealed class SimMonitorEntry
{
    /// <summary>Hardware-ID des Monitors, z. B. "MONITOR\GSM5B09". Bleibt auch nach Neustart stabil.</summary>
    public string? MonitorId { get; set; }

    /// <summary>Zuletzt bekannter Windows-Name, z. B. "\\.\DISPLAY3" (Fallback, wenn die ID nicht lesbar ist).</summary>
    public string? LastDeviceName { get; set; }

    /// <summary>Anzeigename fuer das Tray-Menue.</summary>
    public string? Label { get; set; }

    public SavedMode? Mode { get; set; }
}

public sealed class AppConfig
{
    /// <summary>Alle Monitore, die zusammen ein- und ausgeschaltet werden.</summary>
    public List<SimMonitorEntry> SimMonitors { get; set; } = new();

    // Alte Felder (Version 1.0, nur ein Monitor). Werden beim Laden in SimMonitors uebernommen
    // und danach nicht mehr geschrieben.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MonitorId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastDeviceName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MonitorLabel { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SavedMode? Mode { get; set; }

    /// <summary>Prozessnamen ohne ".exe". Wird einer davon gestartet, geht der Sim-Monitor an.</summary>
    public List<string> GameProcesses { get; set; } = new()
    {
        "iRacingSim64DX11",
        "AC2-Win64-Shipping",   // Assetto Corsa Competizione
        "acs",                  // Assetto Corsa
        "AssettoCorsaEVO",
        "rFactor2",
        "Le Mans Ultimate",
        "AMS2AVX",              // Automobilista 2
        "AMS2",
        "WRC",                  // EA Sports WRC
        "dirtrally2",
        "BeamNG.drive.x64",
    };

    /// <summary>
    /// "Extend" = Monitor wie bei Win+P -> Erweitern einschalten (Standard).
    /// "Legacy" = nur ueber ChangeDisplaySettingsEx (Version 1, kann auf manchen Treibern ein schwarzes Bild liefern).
    /// </summary>
    public string EnableMethod { get; set; } = "Extend";

    /// <summary>Sprache der Oberflaeche: "Auto" (Windows-Sprache), "en" oder "de".</summary>
    public string Language { get; set; } = Loc.Auto;

    /// <summary>Automatische Erkennung beim Spielstart ein/aus.</summary>
    public bool AutoMode { get; set; } = true;

    /// <summary>Hotkey zum manuellen Umschalten, z. B. "Ctrl+Alt+S".</summary>
    public string Hotkey { get; set; } = "Ctrl+Alt+S";

    /// <summary>Wie oft (Sekunden) nach laufenden Spielen gesucht wird.</summary>
    public int PollSeconds { get; set; } = 2;

    /// <summary>Wartezeit (Sekunden) nach Spielende, bevor der Monitor wieder ausgeht.</summary>
    public int DisableDelaySeconds { get; set; } = 10;

    /// <summary>Regelmaessig auf GitHub nach einer neuen Version suchen.</summary>
    public bool CheckForUpdates { get; set; } = true;

    // ------------------------------------------------------------------

    [JsonIgnore]
    public static string ConfigDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SimMonitorSwitch");

    [JsonIgnore]
    public static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
                cfg.MigrateLegacy();
                return cfg;
            }
        }
        catch
        {
            // Kaputte Datei: mit Standardwerten weitermachen (Original bleibt liegen)
        }
        return new AppConfig();
    }

    /// <summary>Uebernimmt einen Monitor aus den alten Einzel-Feldern in die Liste.</summary>
    private void MigrateLegacy()
    {
        SimMonitors ??= new();

        if (SimMonitors.Count == 0
            && (!string.IsNullOrEmpty(MonitorId) || !string.IsNullOrEmpty(LastDeviceName)))
        {
            SimMonitors.Add(new SimMonitorEntry
            {
                MonitorId = MonitorId,
                LastDeviceName = LastDeviceName,
                Label = MonitorLabel,
                Mode = Mode,
            });
        }

        MonitorId = null;
        LastDeviceName = null;
        MonitorLabel = null;
        Mode = null;
    }

    /// <summary>Uebernimmt alle Werte einer frisch geladenen Konfiguration (fuer "Neu laden").</summary>
    public void ApplyFrom(AppConfig other)
    {
        foreach (var p in typeof(AppConfig).GetProperties())
        {
            if (p.CanWrite && p.CanRead)
                p.SetValue(this, p.GetValue(other));
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // Speichern ist "best effort"
        }
    }
}
