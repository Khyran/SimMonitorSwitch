using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace SimMonitorSwitch;

internal sealed record UpdateInfo(Version Version, string Tag, string DownloadUrl, long Size);

/// <summary>Sucht auf GitHub nach einem neuen Release und installiert es (exe austauschen, neu starten).</summary>
internal static class Updater
{
    private const string Repo = "Khyran/SimMonitorSwitch";
    private const string AssetName = "SimMonitorSwitch.exe";

    /// <summary>Startargument der neuen Version: wartet, bis die alte Instanz beendet ist.</summary>
    public const string AfterUpdateArg = "--after-update";

    public static string ReleasesUrl => $"https://github.com/{Repo}/releases";

    /// <summary>Version dieses Builds (aus dem Git-Tag der CI), immer dreistellig, z. B. 1.2.0.</summary>
    public static Version CurrentVersion { get; } = Normalize(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0));

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        // GitHub lehnt Anfragen ohne User-Agent ab
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SimMonitorSwitch", CurrentVersion.ToString()));
        return client;
    }

    /// <summary>Liefert das neueste Release, wenn es neuer als diese Version ist, sonst null.</summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repo}/releases/latest");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await Http.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;   // noch kein Release veroeffentlicht
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        string tag = root.GetProperty("tag_name").GetString() ?? "";

        if (!TryParseVersion(tag, out var version))
        {
            Log.Write($"Update check: cannot read version from tag '{tag}'");
            return null;
        }
        if (version <= CurrentVersion)
            return null;

        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (string.Equals(asset.GetProperty("name").GetString(), AssetName, StringComparison.OrdinalIgnoreCase))
            {
                return new UpdateInfo(version, tag,
                    asset.GetProperty("browser_download_url").GetString() ?? "",
                    asset.GetProperty("size").GetInt64());
            }
        }

        Log.Write($"Update check: release {tag} has no {AssetName}");
        return null;
    }

    /// <summary>
    /// Laedt die neue exe herunter, tauscht sie gegen die laufende aus und startet sie.
    /// Danach muss sich diese Instanz beenden; die neue wartet so lange.
    /// </summary>
    public static async Task InstallAsync(UpdateInfo update)
    {
        string exe = Environment.ProcessPath ?? throw new InvalidOperationException("Unknown executable path.");
        string newPath = exe + ".new";
        string oldPath = exe + ".old";

        Log.Write($"Update: downloading {update.Tag} from {update.DownloadUrl}");
        try
        {
            // Im selben Ordner speichern, damit das Umbenennen unten sicher klappt (gleiches Laufwerk)
            using (var response = await Http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                await using var file = File.Create(newPath);
                await response.Content.CopyToAsync(file);
            }
        }
        catch (UnauthorizedAccessException)
        {
            throw new IOException(Loc.T("update.noAccess", Path.GetDirectoryName(exe) ?? exe));
        }

        if (!LooksLikeExe(newPath, update.Size))
        {
            TryDelete(newPath);
            throw new IOException(Loc.T("update.badDownload"));
        }

        // Eine laufende exe darf nicht ueberschrieben, aber umbenannt werden
        TryDelete(oldPath);
        File.Move(exe, oldPath);
        try
        {
            File.Move(newPath, exe);
        }
        catch
        {
            File.Move(oldPath, exe);
            throw;
        }

        Log.Write($"Update: {update.Tag} installed, restarting");
        Process.Start(new ProcessStartInfo(exe, AfterUpdateArg) { UseShellExecute = false });
    }

    /// <summary>Raeumt nach einem Update die alte exe weg (sie ist evtl. noch kurz gesperrt).</summary>
    public static void CleanUpAfterUpdate()
    {
        string? exe = Environment.ProcessPath;
        if (exe == null) return;

        Task.Run(async () =>
        {
            for (int i = 0; i < 20 && File.Exists(exe + ".old"); i++)
            {
                TryDelete(exe + ".old");
                await Task.Delay(500);
            }
            TryDelete(exe + ".new");
        });
    }

    private static bool LooksLikeExe(string path, long expectedSize)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < 1024 || (expectedSize > 0 && info.Length != expectedSize))
            return false;

        using var fs = File.OpenRead(path);
        return fs.ReadByte() == 'M' && fs.ReadByte() == 'Z';
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* noch gesperrt, naechstes Mal */ }
    }

    private static bool TryParseVersion(string tag, out Version version)
    {
        // "v1.2.3" oder "1.2.3-beta" -> 1.2.3
        var core = tag.Trim().TrimStart('v', 'V').Split('-', '+')[0];
        if (Version.TryParse(core, out var parsed))
        {
            version = Normalize(parsed);
            return true;
        }
        version = new Version(0, 0, 0);
        return false;
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));
}
