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

/// <summary>Schaltet den konfigurierten Sim-Monitor ueber die Windows-API ein und aus.</summary>
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

    public MonitorInfo? FindConfigured()
    {
        if (string.IsNullOrEmpty(_cfg.MonitorId) && string.IsNullOrEmpty(_cfg.LastDeviceName))
            return null;

        var all = Enumerate();

        bool SameId(MonitorInfo m) =>
            !string.IsNullOrEmpty(_cfg.MonitorId)
            && string.Equals(m.MonitorId, _cfg.MonitorId, StringComparison.OrdinalIgnoreCase);

        bool SameName(MonitorInfo m) =>
            !string.IsNullOrEmpty(_cfg.LastDeviceName)
            && string.Equals(m.DeviceName, _cfg.LastDeviceName, StringComparison.OrdinalIgnoreCase);

        // 1. Beides passt (ID und Windows-Name): das ist der richtige Monitor.
        var exact = all.FirstOrDefault(m => SameId(m) && SameName(m));
        if (exact != null) return exact;

        // 2. Nur die Hardware-ID passt (Monitor wurde an einen anderen Anschluss gesteckt).
        //    Nur akzeptieren, wenn sie eindeutig ist. Bei mehreren baugleichen Monitoren
        //    waere das sonst ein Ratespiel und es koennte der falsche abgeschaltet werden.
        var byId = all.Where(SameId).ToList();
        if (byId.Count == 1) return byId[0];

        // 3. Nur der Windows-Name passt. Das ist der Fall, wenn der abgeschaltete Ausgang
        //    keine ID mehr meldet. Ein Ausgang mit einer anderen ID ist ein anderer Monitor.
        var byName = all.FirstOrDefault(SameName);
        if (byName != null
            && (byName.MonitorId == null || string.IsNullOrEmpty(_cfg.MonitorId) || SameId(byName)))
            return byName;

        return null;
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_cfg.MonitorId) || !string.IsNullOrEmpty(_cfg.LastDeviceName);

    public bool? IsEnabled() => FindConfigured()?.Attached;

    // ------------------------------------------------------------------
    // Auswaehlen und aktuelle Einstellungen merken
    // ------------------------------------------------------------------

    /// <summary>Merkt sich den Monitor samt aktueller Aufloesung/Position.</summary>
    public ActionResult Select(MonitorInfo monitor)
    {
        if (!monitor.Attached)
            return new(false, Loc.T("select.mustBeActive"));
        if (monitor.Primary)
            return new(false, Loc.T("select.isPrimary"));

        _cfg.MonitorId = monitor.MonitorId;
        _cfg.LastDeviceName = monitor.DeviceName;
        _cfg.MonitorLabel = monitor.Name;

        var mode = ReadCurrentMode(monitor.DeviceName);
        if (mode == null)
            return new(false, Loc.T("select.readFailed"));

        _cfg.Mode = mode;
        _cfg.Save();
        return new(true, Loc.T("select.ok", monitor.Name, mode.Width, mode.Height, mode.Frequency));
    }

    private static SavedMode? ReadCurrentMode(string deviceName)
    {
        var dm = NewDevMode();
        if (!EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref dm) || dm.dmPelsWidth <= 0)
            return null;

        return new SavedMode
        {
            X = dm.dmPosition.x,
            Y = dm.dmPosition.y,
            Width = dm.dmPelsWidth,
            Height = dm.dmPelsHeight,
            Frequency = dm.dmDisplayFrequency > 1 ? dm.dmDisplayFrequency : 60,
            Orientation = dm.dmDisplayOrientation,
            BitsPerPel = dm.dmBitsPerPel > 0 ? dm.dmBitsPerPel : 32,
        };
    }

    // ------------------------------------------------------------------
    // Einschalten
    // ------------------------------------------------------------------

    public ActionResult Enable()
    {
        var monitor = FindConfigured();
        if (monitor == null)
            return new(false, Loc.T("enable.notFound"));
        if (monitor.Attached)
            return new(true, Loc.T("enable.already"));

        var mode = _cfg.Mode ?? ReadRegistryMode(monitor.DeviceName);
        if (mode == null)
            return new(false, Loc.T("enable.noMode"));

        Log.Write($"Enable: {monitor.DeviceName}, saved: {Describe(mode)}, method: {_cfg.EnableMethod}");

        bool attached = false;

        // Weg 1 (Standard): wie Win+P -> Erweitern. Das ist ein echter Neuaufbau der Bildausgabe.
        if (string.Equals(_cfg.EnableMethod, "Extend", StringComparison.OrdinalIgnoreCase))
        {
            var ext = ExtendAll();
            Log.Write($"Extend: {ext.Message}");
            attached = ext.Ok && WaitFor(monitor, attached: true);
        }

        // Weg 2 (Rueckfall): ueber die aeltere Schnittstelle
        if (!attached)
        {
            var apply = ApplyMode(monitor.DeviceName, mode);
            Log.Write($"Legacy activation: {apply.Message}");
            if (!apply.Ok) return apply;
            attached = WaitFor(monitor, attached: true);
        }

        if (!attached)
            return new(false, Loc.T("enable.failed"));

        // Nachpruefen: stimmen Aufloesung, Hz und Position noch mit den gespeicherten Werten?
        Thread.Sleep(500);
        var now = ReadCurrentMode(monitor.DeviceName);
        Log.Write($"After enabling: {Describe(now)}");
        if (now == null || !SameMode(now, mode))
        {
            var fix = ApplyMode(monitor.DeviceName, mode);
            Log.Write($"Mode re-applied: {fix.Message}");
            now = ReadCurrentMode(monitor.DeviceName) ?? now;
            Log.Write($"Afterwards: {Describe(now)}");
        }

        return new(true, Loc.T("enable.ok", Describe(now)));
    }

    /// <summary>Wie Win+P -> Erweitern: alle angeschlossenen Monitore in den Desktop holen.</summary>
    public static ActionResult ExtendAll()
    {
        int rc = SetDisplayConfig(0, IntPtr.Zero, 0, IntPtr.Zero, SDC_APPLY | SDC_TOPOLOGY_EXTEND);
        return rc == 0
            ? new(true, Loc.T("extend.ok"))
            : new(false, Loc.T("extend.failed", rc));
    }

    private static ActionResult ApplyMode(string deviceName, SavedMode mode)
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
        return ApplyChange(deviceName, ref dm);
    }

    private static bool SameMode(SavedMode a, SavedMode b) =>
        a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height && a.Frequency == b.Frequency;

    private static string Describe(SavedMode? m) =>
        m == null ? Loc.T("mode.unknown") : Loc.T("mode.describe", m.Width, m.Height, m.Frequency, m.X, m.Y);

    private static SavedMode? ReadRegistryMode(string deviceName)
    {
        var dm = NewDevMode();
        if (!EnumDisplaySettings(deviceName, ENUM_REGISTRY_SETTINGS, ref dm) || dm.dmPelsWidth <= 0)
            return null;

        return new SavedMode
        {
            X = dm.dmPosition.x,
            Y = dm.dmPosition.y,
            Width = dm.dmPelsWidth,
            Height = dm.dmPelsHeight,
            Frequency = dm.dmDisplayFrequency > 1 ? dm.dmDisplayFrequency : 60,
            Orientation = dm.dmDisplayOrientation,
            BitsPerPel = dm.dmBitsPerPel > 0 ? dm.dmBitsPerPel : 32,
        };
    }

    // ------------------------------------------------------------------
    // Ausschalten
    // ------------------------------------------------------------------

    public ActionResult Disable()
    {
        var monitor = FindConfigured();
        if (monitor == null)
            return new(false, Loc.T("disable.notFound"));
        if (!monitor.Attached)
            return new(true, Loc.T("disable.already"));

        // Sicherheitsnetz 1: nie den Hauptbildschirm abschalten
        if (monitor.Primary)
            return new(false, Loc.T("disable.isPrimary"));

        // Sicherheitsnetz 2: mindestens ein anderer Bildschirm muss aktiv bleiben
        int otherActive = Enumerate().Count(m => m.Attached && m.DeviceName != monitor.DeviceName);
        if (otherActive < 1)
            return new(false, Loc.T("disable.lastActive"));

        // Aktuelle Einstellungen merken, damit beim Einschalten alles wieder passt
        var current = ReadCurrentMode(monitor.DeviceName);
        if (current != null)
        {
            _cfg.Mode = current;
            _cfg.LastDeviceName = monitor.DeviceName;
            _cfg.Save();
        }

        // Aufloesung 0x0 = Ausgang vom Desktop trennen
        var dm = NewDevMode();
        dm.dmFields = DM_POSITION | DM_PELSWIDTH | DM_PELSHEIGHT;
        dm.dmPosition = new POINTL { x = current?.X ?? 0, y = current?.Y ?? 0 };
        dm.dmPelsWidth = 0;
        dm.dmPelsHeight = 0;

        Log.Write($"Disable: {monitor.DeviceName}, remembered: {Describe(current)}");
        var apply = ApplyChange(monitor.DeviceName, ref dm);
        Log.Write($"Disable result: {apply.Message}");
        if (!apply.Ok) return apply;

        return WaitFor(monitor, attached: false)
            ? new(true, Loc.T("disable.ok"))
            : new(false, Loc.T("disable.failed"));
    }

    // ------------------------------------------------------------------
    // Gemeinsame Helfer
    // ------------------------------------------------------------------

    /// <summary>Aenderung in der Registry vormerken (NORESET) und danach gesammelt anwenden.</summary>
    private static ActionResult ApplyChange(string deviceName, ref DEVMODE dm)
    {
        int r = ChangeDisplaySettingsEx(deviceName, ref dm, IntPtr.Zero,
            CDS_UPDATEREGISTRY | CDS_NORESET, IntPtr.Zero);
        Log.Write($"ChangeDisplaySettingsEx({deviceName}) -> {r}");
        if (r != DISP_CHANGE_SUCCESSFUL)
            return new(false, Loc.T("apply.rejected", DescribeError(r)));

        r = ChangeDisplaySettingsExApply(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
        if (r != DISP_CHANGE_SUCCESSFUL && r != DISP_CHANGE_RESTART)
            return new(false, Loc.T("apply.failed", DescribeError(r)));

        return new(true, "OK");
    }

    private bool WaitFor(MonitorInfo monitor, bool attached)
    {
        for (int i = 0; i < 20; i++)   // bis zu ~4 s
        {
            var cur = Enumerate().FirstOrDefault(m =>
                string.Equals(m.DeviceName, monitor.DeviceName, StringComparison.OrdinalIgnoreCase));
            if (cur != null && cur.Attached == attached)
            {
                // Nach dem Einschalten Hardware-ID auffrischen
                if (attached && cur.MonitorId != null && _cfg.MonitorId != cur.MonitorId)
                {
                    _cfg.MonitorId = cur.MonitorId;
                    _cfg.Save();
                }
                return true;
            }
            Thread.Sleep(200);
        }
        return false;
    }

    private static string DescribeError(int code) => code switch
    {
        -6 or -5 or -4 or -3 or -2 or -1 or 1 => Loc.T("err." + code),
        _ => Loc.T("err.other", code),
    };
}
