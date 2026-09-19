using System.Runtime.InteropServices;
using static SimMonitorSwitch.DisplayApi;

namespace SimMonitorSwitch;

internal sealed record MonitorInfo(
    string DeviceName,      // z. B. \\.\DISPLAY3 (Adapter-Ausgang)
    string? MonitorId,      // z. B. MONITOR\GSM5B09 (Hardware-ID des Geraets, falls lesbar)
    string Name,            // Anzeigename
    bool Attached,          // aktuell Teil des Desktops?
    bool Primary);

internal sealed record ActionResult(bool Ok, string Message);

/// <summary>Ein konfigurierter Sim-Monitor und (falls gefunden) sein aktueller Windows-Zustand.</summary>
internal sealed record ResolvedMonitor(SimMonitorEntry Entry, MonitorInfo? Info)
{
    public bool Found => Info != null;
    public bool Attached => Info?.Attached == true;
}

/// <summary>Total = konfiguriert, Found = von Windows gefunden, Attached = davon aktuell am Desktop.</summary>
internal sealed record MonitorStatus(int Total, int Found, int Attached)
{
    /// <summary>Alle gefundenen Sim-Monitore sind an (und mindestens einer wurde gefunden).</summary>
    public bool AllFoundOn => Found > 0 && Attached == Found;

    /// <summary>Mindestens ein gefundener Sim-Monitor ist aus.</summary>
    public bool AnyFoundOff => Found > Attached;
}

/// <summary>Schaltet die konfigurierten Sim-Monitore ueber die Windows-API ein und aus.</summary>
internal sealed class MonitorController
{
    private readonly AppConfig _cfg;

    public MonitorController(AppConfig cfg) => _cfg = cfg;

    // ------------------------------------------------------------------
    // Monitore auflisten
    // ------------------------------------------------------------------

    public static List<MonitorInfo> Enumerate()
    {
        var result = new List<MonitorInfo>();

        for (int i = 0; ; i++)
        {
            var adapter = NewDisplayDevice();
            if (!EnumDisplayDevices(null, i, ref adapter, 0))
                break;

            if ((adapter.StateFlags & DISPLAY_DEVICE_MIRRORING_DRIVER) != 0)
                continue;

            bool attached = (adapter.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0;
            bool primary = (adapter.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE) != 0;

            // Das angeschlossene Geraet (Monitor) haengt als "Kind" am Adapter-Ausgang.
            string? monitorId = null;
            string name = adapter.DeviceString.TrimEnd('\0');
            var mon = NewDisplayDevice();
            if (EnumDisplayDevices(adapter.DeviceName, 0, ref mon, 0))
            {
                monitorId = ShortMonitorId(mon.DeviceID);
                var friendly = mon.DeviceString.TrimEnd('\0');
                if (!string.IsNullOrWhiteSpace(friendly))
                    name = friendly;
            }

            result.Add(new MonitorInfo(
                adapter.DeviceName.TrimEnd('\0'), monitorId, name, attached, primary));
        }

        return result;
    }

    /// <summary>"MONITOR\GSM5B09\{guid}\0004" -> "MONITOR\GSM5B09"</summary>
    private static string? ShortMonitorId(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return null;
        var parts = deviceId.TrimEnd('\0').Split('\\');
        return parts.Length >= 2 ? parts[0] + "\\" + parts[1] : deviceId.TrimEnd('\0');
    }

    public bool IsConfigured => _cfg.SimMonitors.Count > 0;

    private static bool SameId(SimMonitorEntry e, MonitorInfo m) =>
        !string.IsNullOrEmpty(e.MonitorId)
        && string.Equals(m.MonitorId, e.MonitorId, StringComparison.OrdinalIgnoreCase);

    private static bool SameName(SimMonitorEntry e, MonitorInfo m) =>
        !string.IsNullOrEmpty(e.LastDeviceName)
        && string.Equals(m.DeviceName, e.LastDeviceName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Ordnet jedem konfigurierten Sim-Monitor den passenden Windows-Ausgang zu. Ein Ausgang wird
    /// hoechstens einem Eintrag zugeordnet, auch bei mehreren baugleichen Monitoren.
    /// </summary>
    public List<ResolvedMonitor> ResolveAll()
    {
        var entries = _cfg.SimMonitors.ToList();
        var all = Enumerate();
        var info = new MonitorInfo?[entries.Count];
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Claim(int i, MonitorInfo m)
        {
            info[i] = m;
            claimed.Add(m.DeviceName);
        }

        IEnumerable<MonitorInfo> Free() => all.Where(m => !claimed.Contains(m.DeviceName));

        // 1. Beides passt (ID und Windows-Name): das ist der richtige Monitor.
        for (int i = 0; i < entries.Count; i++)
        {
            if (info[i] != null) continue;
            var m = Free().FirstOrDefault(m => SameId(entries[i], m) && SameName(entries[i], m));
            if (m != null) Claim(i, m);
        }

        // 2. Nur die Hardware-ID passt (Monitor wurde an einen anderen Anschluss gesteckt).
        //    Nur akzeptieren, wenn sie unter den noch freien Ausgaengen eindeutig ist. Bei mehreren
        //    baugleichen Monitoren waere das sonst ein Ratespiel und es koennte der falsche
        //    abgeschaltet werden.
        for (int i = 0; i < entries.Count; i++)
        {
            if (info[i] != null) continue;
            var byId = Free().Where(m => SameId(entries[i], m)).ToList();
            if (byId.Count == 1) Claim(i, byId[0]);
        }

        // 3. Nur der Windows-Name passt. Das ist der Fall, wenn der abgeschaltete Ausgang
        //    keine ID mehr meldet. Ein Ausgang mit einer anderen ID ist ein anderer Monitor.
        for (int i = 0; i < entries.Count; i++)
        {
            if (info[i] != null) continue;
            var m = Free().FirstOrDefault(m => SameName(entries[i], m)
                && (m.MonitorId == null || string.IsNullOrEmpty(entries[i].MonitorId) || SameId(entries[i], m)));
            if (m != null) Claim(i, m);
        }

        return entries.Select((e, i) => new ResolvedMonitor(e, info[i])).ToList();
    }

    public MonitorStatus GetStatus()
    {
        var resolved = ResolveAll();
        return new MonitorStatus(resolved.Count, resolved.Count(r => r.Found), resolved.Count(r => r.Attached));
    }

    // ------------------------------------------------------------------
    // Sim-Monitore hinzufuegen / entfernen
    // ------------------------------------------------------------------

    /// <summary>Nimmt den Monitor samt aktueller Aufloesung/Position in die Liste auf.</summary>
    public ActionResult Add(MonitorInfo monitor)
    {
        if (!monitor.Attached)
            return new(false, Loc.T("select.mustBeActive"));
        if (monitor.Primary)
            return new(false, Loc.T("select.isPrimary"));

        var mode = ReadCurrentMode(monitor.DeviceName);
        if (mode == null)
            return new(false, Loc.T("select.readFailed"));

        _cfg.SimMonitors.Add(new SimMonitorEntry
        {
            MonitorId = monitor.MonitorId,
            LastDeviceName = monitor.DeviceName,
            Label = monitor.Name,
            Mode = mode,
        });
        _cfg.Save();
        return new(true, Loc.T("select.added", monitor.Name, mode.Width, mode.Height, mode.Frequency));
    }

    public ActionResult Remove(SimMonitorEntry entry)
    {
        _cfg.SimMonitors.Remove(entry);
        _cfg.Save();
        return new(true, Loc.T("select.removed", entry.Label ?? Loc.T("label.default")));
    }

    private static SavedMode? ReadCurrentMode(string deviceName)
    {
        var dm = NewDevMode();
        if (!EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref dm) || dm.dmPelsWidth <= 0)
            return null;

        return ToSavedMode(dm);
    }

    private static SavedMode? ReadRegistryMode(string deviceName)
    {
        var dm = NewDevMode();
        if (!EnumDisplaySettings(deviceName, ENUM_REGISTRY_SETTINGS, ref dm) || dm.dmPelsWidth <= 0)
            return null;

        return ToSavedMode(dm);
    }

    private static SavedMode ToSavedMode(DEVMODE dm) => new()
    {
        X = dm.dmPosition.x,
        Y = dm.dmPosition.y,
        Width = dm.dmPelsWidth,
        Height = dm.dmPelsHeight,
        Frequency = dm.dmDisplayFrequency > 1 ? dm.dmDisplayFrequency : 60,
        Orientation = dm.dmDisplayOrientation,
        BitsPerPel = dm.dmBitsPerPel > 0 ? dm.dmBitsPerPel : 32,
    };

    // ------------------------------------------------------------------
    // Einschalten
    // ------------------------------------------------------------------

    public ActionResult Enable()
    {
        var resolved = ResolveAll();
        int total = resolved.Count;

        var found = resolved.Where(r => r.Found).ToList();
        if (found.Count == 0)
            return new(false, Loc.N("enable.notFound", total));

        var targets = found.Where(r => !r.Attached).ToList();
        if (targets.Count == 0)
            return new(true, Loc.N("enable.already", total));

        // Gespeicherte Einstellungen je Monitor. Ohne Einstellungen kann ein Monitor nicht sicher eingeschaltet werden.
        var jobs = new List<(ResolvedMonitor Monitor, SavedMode Mode)>();
        foreach (var t in targets)
        {
            var mode = t.Entry.Mode ?? ReadRegistryMode(t.Info!.DeviceName);
            if (mode == null)
            {
                Log.Write($"Enable: no saved mode for {t.Info!.DeviceName}, skipped");
                continue;
            }
            jobs.Add((t, mode));
            Log.Write($"Enable: {t.Info!.DeviceName}, saved: {Describe(mode)}, method: {_cfg.EnableMethod}");
        }

        if (jobs.Count == 0)
            return new(false, Loc.N("enable.noMode", total));

        var done = new List<ResolvedMonitor>();
        string? lastError = null;

        // Weg 1 (Standard): wie Win+P -> Erweitern. Das ist ein echter Neuaufbau der Bildausgabe.
        if (string.Equals(_cfg.EnableMethod, "Extend", StringComparison.OrdinalIgnoreCase))
        {
            var ext = ExtendAll();
            Log.Write($"Extend: {ext.Message}");
            if (ext.Ok)
                done.AddRange(WaitForAll(jobs.Select(j => j.Monitor).ToList(), attached: true));
        }

        // Weg 2 (Rueckfall): ueber die aeltere Schnittstelle, fuer alle noch fehlenden Monitore gesammelt
        var rest = jobs.Where(j => !done.Contains(j.Monitor)).ToList();
        if (rest.Count > 0)
        {
            bool anyStaged = false;
            foreach (var (monitor, mode) in rest)
            {
                var stage = StageMode(monitor.Info!.DeviceName, mode);
                Log.Write($"Legacy activation staged ({monitor.Info.DeviceName}): {stage.Message}");
                if (stage.Ok) anyStaged = true; else lastError = stage.Message;
            }

            if (anyStaged)
            {
                var commit = Commit();
                Log.Write($"Legacy activation: {commit.Message}");
                if (commit.Ok)
                    done.AddRange(WaitForAll(rest.Select(j => j.Monitor).ToList(), attached: true));
                else
                    lastError = commit.Message;
            }
        }

        if (done.Count == 0)
            return new(false, lastError ?? Loc.N("enable.failed", total));

        // Nachpruefen: stimmen Aufloesung, Hz und Position noch mit den gespeicherten Werten?
        Thread.Sleep(500);
        var fixes = new List<(ResolvedMonitor Monitor, SavedMode Mode)>();
        foreach (var job in jobs.Where(j => done.Contains(j.Monitor)))
        {
            string dev = job.Monitor.Info!.DeviceName;
            var now = ReadCurrentMode(dev);
            Log.Write($"After enabling ({dev}): {Describe(now)}");
            if (now == null || !SameMode(now, job.Mode))
                fixes.Add(job);
        }

        if (fixes.Count > 0)
        {
            foreach (var (monitor, mode) in fixes)
                Log.Write($"Mode re-applied ({monitor.Info!.DeviceName}): {StageMode(monitor.Info.DeviceName, mode).Message}");
            Log.Write($"Mode re-applied: {Commit().Message}");
            foreach (var (monitor, _) in fixes)
                Log.Write($"Afterwards ({monitor.Info!.DeviceName}): {Describe(ReadCurrentMode(monitor.Info.DeviceName))}");
        }

        if (done.Count < targets.Count)
            return new(false, Loc.T("enable.partial", done.Count, targets.Count));

        return done.Count == 1
            ? new(true, Loc.T("enable.ok", Describe(ReadCurrentMode(done[0].Info!.DeviceName))))
            : new(true, Loc.T("enable.okMulti", done.Count));
    }

    /// <summary>Wie Win+P -> Erweitern: alle angeschlossenen Monitore in den Desktop holen.</summary>
    public static ActionResult ExtendAll()
    {
        int rc = SetDisplayConfig(0, IntPtr.Zero, 0, IntPtr.Zero, SDC_APPLY | SDC_TOPOLOGY_EXTEND);
        return rc == 0
            ? new(true, Loc.T("extend.ok"))
            : new(false, Loc.T("extend.failed", rc));
    }

    private static ActionResult StageMode(string deviceName, SavedMode mode)
    {
        var dm = NewDevMode();
        dm.dmFields = DM_POSITION | DM_PELSWIDTH | DM_PELSHEIGHT | DM_BITSPERPEL
                    | DM_DISPLAYFREQUENCY | DM_DISPLAYORIENTATION;
        dm.dmPosition = new POINTL { x = mode.X, y = mode.Y };
        dm.dmPelsWidth = mode.Width;
        dm.dmPelsHeight = mode.Height;
        dm.dmBitsPerPel = mode.BitsPerPel;
        dm.dmDisplayFrequency = mode.Frequency;
        dm.dmDisplayOrientation = mode.Orientation;
        return Stage(deviceName, ref dm);
    }

    private static bool SameMode(SavedMode a, SavedMode b) =>
        a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height && a.Frequency == b.Frequency;

    private static string Describe(SavedMode? m) =>
        m == null ? Loc.T("mode.unknown") : Loc.T("mode.describe", m.Width, m.Height, m.Frequency, m.X, m.Y);

    // ------------------------------------------------------------------
    // Ausschalten
    // ------------------------------------------------------------------

    public ActionResult Disable()
    {
        var resolved = ResolveAll();
        int total = resolved.Count;

        var found = resolved.Where(r => r.Found).ToList();
        if (found.Count == 0)
            return new(false, Loc.N("disable.notFound", total));

        var active = found.Where(r => r.Attached).ToList();
        if (active.Count == 0)
            return new(true, Loc.N("disable.already", total));

        // Sicherheitsnetz 1: nie den Hauptbildschirm abschalten
        var primary = active.Where(r => r.Info!.Primary).ToList();
        var targets = active.Where(r => !r.Info!.Primary).ToList();
        if (targets.Count == 0)
            return new(false, Loc.T("disable.isPrimary"));

        // Sicherheitsnetz 2: mindestens ein anderer Bildschirm muss aktiv bleiben
        var switchOff = targets.Select(t => t.Info!.DeviceName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int otherActive = Enumerate().Count(m => m.Attached && !switchOff.Contains(m.DeviceName));
        if (otherActive < 1)
            return new(false, Loc.T("disable.lastActive"));

        // Aktuelle Einstellungen merken, damit beim Einschalten alles wieder passt
        var currentModes = new Dictionary<ResolvedMonitor, SavedMode?>();
        foreach (var t in targets)
        {
            var current = ReadCurrentMode(t.Info!.DeviceName);
            currentModes[t] = current;
            if (current != null)
            {
                t.Entry.Mode = current;
                t.Entry.LastDeviceName = t.Info.DeviceName;
            }
        }
        _cfg.Save();

        // Aufloesung 0x0 = Ausgang vom Desktop trennen. Erst alle vormerken, dann einmal anwenden.
        bool anyStaged = false;
        string? lastError = null;
        foreach (var t in targets)
        {
            var current = currentModes[t];
            var dm = NewDevMode();
            dm.dmFields = DM_POSITION | DM_PELSWIDTH | DM_PELSHEIGHT;
            dm.dmPosition = new POINTL { x = current?.X ?? 0, y = current?.Y ?? 0 };
            dm.dmPelsWidth = 0;
            dm.dmPelsHeight = 0;

            Log.Write($"Disable: {t.Info!.DeviceName}, remembered: {Describe(current)}");
            var stage = Stage(t.Info.DeviceName, ref dm);
            if (stage.Ok) anyStaged = true; else lastError = stage.Message;
        }

        if (!anyStaged)
            return new(false, lastError ?? Loc.N("disable.failed", targets.Count));

        var commit = Commit();
        Log.Write($"Disable result: {commit.Message}");
        if (!commit.Ok) return commit;

        var gone = WaitForAll(targets, attached: false);
        if (gone.Count < targets.Count)
            return new(false, Loc.N("disable.failed", targets.Count));

        string message = Loc.N("disable.ok", targets.Count, targets.Count);
        if (primary.Count > 0)
            message += " " + Loc.T("disable.skippedPrimary", primary.Count);
        return new(true, message);
    }

    // ------------------------------------------------------------------
    // Gemeinsame Helfer
    // ------------------------------------------------------------------

    /// <summary>Aenderung in der Registry vormerken (NORESET). Angewendet wird spaeter mit Commit().</summary>
    private static ActionResult Stage(string deviceName, ref DEVMODE dm)
    {
        int r = ChangeDisplaySettingsEx(deviceName, ref dm, IntPtr.Zero,
            CDS_UPDATEREGISTRY | CDS_NORESET, IntPtr.Zero);
        Log.Write($"ChangeDisplaySettingsEx({deviceName}) -> {r}");
        return r == DISP_CHANGE_SUCCESSFUL
            ? new(true, "OK")
            : new(false, Loc.T("apply.rejected", DescribeError(r)));
    }

    /// <summary>Wendet alle vorgemerkten Aenderungen auf einmal an.</summary>
    private static ActionResult Commit()
    {
        int r = ChangeDisplaySettingsExApply(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
        return r == DISP_CHANGE_SUCCESSFUL || r == DISP_CHANGE_RESTART
            ? new(true, "OK")
            : new(false, Loc.T("apply.failed", DescribeError(r)));
    }

    /// <summary>Wartet (bis ca. 4 s), bis die Monitore am Desktop haengen bzw. getrennt sind. Liefert die, die es geschafft haben.</summary>
    private List<ResolvedMonitor> WaitForAll(List<ResolvedMonitor> monitors, bool attached)
    {
        var reached = new List<ResolvedMonitor>();
        bool idsChanged = false;

        for (int i = 0; i < 20; i++)
        {
            var all = Enumerate();
            foreach (var m in monitors.Where(m => !reached.Contains(m)))
            {
                var cur = all.FirstOrDefault(c =>
                    string.Equals(c.DeviceName, m.Info!.DeviceName, StringComparison.OrdinalIgnoreCase));
                if (cur == null || cur.Attached != attached) continue;

                reached.Add(m);

                // Nach dem Einschalten Hardware-ID auffrischen
                if (attached && cur.MonitorId != null && m.Entry.MonitorId != cur.MonitorId)
                {
                    m.Entry.MonitorId = cur.MonitorId;
                    idsChanged = true;
                }
            }

            if (reached.Count == monitors.Count) break;
            Thread.Sleep(200);
        }

        if (idsChanged) _cfg.Save();
        return reached;
    }

    private static string DescribeError(int code) => code switch
    {
        -6 or -5 or -4 or -3 or -2 or -1 or 1 => Loc.T("err." + code),
        _ => Loc.T("err.other", code),
    };
}
