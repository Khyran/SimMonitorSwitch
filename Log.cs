namespace SimMonitorSwitch;

/// <summary>Einfaches Textlog unter %AppData%\SimMonitorSwitch\log.txt (zur Fehlersuche).</summary>
internal static class Log
{
    private static readonly object Gate = new();

    public static string FilePath => System.IO.Path.Combine(AppConfig.ConfigDir, "log.txt");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppConfig.ConfigDir);

                // Log klein halten: ab 200 KB wird neu begonnen
                var fi = new FileInfo(FilePath);
                if (fi.Exists && fi.Length > 200_000)
                    fi.Delete();

                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging darf nie etwas kaputt machen
        }
    }
}
