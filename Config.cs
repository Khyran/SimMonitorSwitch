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

public sealed class AppConfig
{
    /// <summary>Hardware-ID des Monitors, z. B. "MONITOR\GSM5B09". Bleibt auch nach Neustart stabil.</summary>
    public string? MonitorId { get; set; }

    /// <summary>Zuletzt bekannter Windows-Name, z. B. "\\.\DISPLAY3" (Fallback, wenn die ID nicht lesbar ist).</summary>
    public string? LastDeviceName { get; set; }

    /// <summary>Anzeigename fuer das Tray-Menue.</summary>
    public string? MonitorLabel { get; set; }

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

    /// <summary>Automatische Erkennung beim Spielstart ein/aus.</summary>
    public bool AutoMode { get; set; } = true;

    /// <summary>Hotkey zum manuellen Umschalten, z. B. "Ctrl+Alt+S".</summary>
    public string Hotkey { get; set; } = "Ctrl+Alt+S";

    /// <summary>Wie oft (Sekunden) nach laufenden Spielen gesucht wird.</summary>
    public int PollSeconds { get; set; } = 2;

    /// <summary>Wartezeit (Sekunden) nach Spielende, bevor der Monitor wieder ausgeht.</summary>
    public int DisableDelaySeconds { get; set; } = 10;

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
                return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
            }
        }
        catch
        {
            // Kaputte Datei: mit Standardwerten weitermachen (Original bleibt liegen)
        }
        return new AppConfig();
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
