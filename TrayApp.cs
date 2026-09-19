using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using static SimMonitorSwitch.DisplayApi;

namespace SimMonitorSwitch;

/// <summary>Tray-Icon, Menue, Hotkey und automatische Spiel-Erkennung.</summary>
internal sealed class TrayApp : ApplicationContext
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "SimMonitorSwitch";

    private readonly AppConfig _cfg = AppConfig.Load();
    private readonly MonitorController _monitor;
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu = new();
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly HotkeyWindow _hotkey = new();
    private readonly Control _sync = new();   // nur um Aufrufe auf den UI-Thread zu holen

    private readonly Icon _iconOn = MakeIcon(Color.FromArgb(46, 204, 113), filled: true);
    private readonly Icon _iconOff = MakeIcon(Color.Gray, filled: false);
    private readonly Icon _iconUnknown = MakeIcon(Color.Orange, filled: false);

    private readonly ToolStripMenuItem _statusItem = new("...") { Enabled = false };
    private readonly ToolStripMenuItem _toggleItem = new();
    private readonly ToolStripMenuItem _onItem = new("Einschalten");
    private readonly ToolStripMenuItem _offItem = new("Ausschalten");
    private readonly ToolStripMenuItem _autoItem = new("Automatisch bei Spielstart") { CheckOnClick = true };
    private readonly ToolStripMenuItem _selectMenu = new("Sim-Monitor auswählen");
    private readonly ToolStripMenuItem _gamesMenu = new("Laufendes Programm als Spiel hinzufügen");
    private readonly ToolStripMenuItem _autostartItem = new("Mit Windows starten") { CheckOnClick = true };

    private bool _busy;
    private bool _gameWasRunning;
    private bool _autoEnabled;          // Hat die Automatik den Monitor eingeschaltet? Nur dann schaltet sie ihn auch wieder aus.
    private DateTime? _gameGoneSince;

    public TrayApp()
    {
        _monitor = new MonitorController(_cfg);
        _ = _sync.Handle;   // Handle erzwingen, damit BeginInvoke funktioniert

        BuildMenu();

        _tray = new NotifyIcon
        {
            Icon = _iconUnknown,
            Text = "Sim-Monitor",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => _ = ToggleAsync();

        _hotkey.Pressed += () => _ = ToggleAsync();
        RegisterHotkey();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        _timer.Interval = Math.Max(1, _cfg.PollSeconds) * 1000;
        _timer.Tick += OnTick;
        _timer.Start();

        RefreshUi();

        if (!_monitor.IsConfigured)
        {
            Notify("Sim-Monitor einrichten",
                "Rechtsklick auf das Tray-Icon und dort den Sim-Monitor auswählen (er muss dafür gerade eingeschaltet sein).",
                ToolTipIcon.Info);
        }
    }

    // ------------------------------------------------------------------
    // Menue
    // ------------------------------------------------------------------

    private void BuildMenu()
    {
        _toggleItem.Click += (_, _) => _ = ToggleAsync();
        _onItem.Click += async (_, _) => await ManualAsync(_monitor.Enable);
        _offItem.Click += async (_, _) => await ManualAsync(_monitor.Disable);

        _autoItem.Checked = _cfg.AutoMode;
        _autoItem.Click += (_, _) =>
        {
            _cfg.AutoMode = _autoItem.Checked;
            _cfg.Save();
        };

        // Untermenues werden erst beim Aufklappen gefuellt (aktueller Stand)
        _selectMenu.DropDownItems.Add("...");
        _selectMenu.DropDownOpening += (_, _) => FillSelectMenu();

        _gamesMenu.DropDownItems.Add("...");
        _gamesMenu.DropDownOpening += (_, _) => FillGamesMenu();

        var openCfg = new ToolStripMenuItem("Konfigurationsdatei öffnen");
        openCfg.Click += (_, _) => OpenConfigFile();

        var reloadCfg = new ToolStripMenuItem("Konfiguration neu laden");
        reloadCfg.Click += (_, _) => ReloadConfig();

        var emergency = new ToolStripMenuItem("Notfall: Alle Monitore erweitern (wie Win+P)");
        emergency.Click += async (_, _) =>
        {
            if (_busy) return;
            _autoEnabled = false;
            var r = await RunAsync(MonitorController.ExtendAll);
            Notify("Monitore erweitern", r.Message, r.Ok ? ToolTipIcon.Info : ToolTipIcon.Error);
        };

        var openLog = new ToolStripMenuItem("Log öffnen");
        openLog.Click += (_, _) => OpenLogFile();

        _autostartItem.Checked = IsAutostartEnabled();
        _autostartItem.Click += (_, _) => SetAutostart(_autostartItem.Checked);

        var exit = new ToolStripMenuItem("Beenden");
        exit.Click += (_, _) => ExitApp();

        _menu.Items.AddRange(new ToolStripItem[]
        {
            _statusItem,
            new ToolStripSeparator(),
            _toggleItem, _onItem, _offItem,
            new ToolStripSeparator(),
            _autoItem, _selectMenu, _gamesMenu,
            new ToolStripSeparator(),
            emergency,
            openCfg, reloadCfg, openLog, _autostartItem,
            new ToolStripSeparator(),
            exit,
        });

        _menu.Opening += (_, _) => RefreshUi();
    }

    private void FillSelectMenu()
    {
        _selectMenu.DropDownItems.Clear();

        var candidates = MonitorController.Enumerate().Where(m => m.Attached).ToList();
        if (candidates.Count == 0)
        {
            _selectMenu.DropDownItems.Add(new ToolStripMenuItem("Keine aktiven Monitore gefunden") { Enabled = false });
            return;
        }

        foreach (var m in candidates)
        {
            string dev = m.DeviceName.TrimStart('\\', '.');
            string text = $"{m.Name}  –  {dev}, {DescribeResolution(m.DeviceName)}" + (m.Primary ? "  (Hauptbildschirm)" : "");
            var item = new ToolStripMenuItem(text)
            {
                Checked = string.Equals(m.DeviceName, _cfg.LastDeviceName, StringComparison.OrdinalIgnoreCase),
                Enabled = !m.Primary,   // Hauptbildschirm darf nie abgeschaltet werden
            };
            var captured = m;
            item.Click += (_, _) =>
            {
                var r = _monitor.Select(captured);
                Notify(r.Ok ? "Sim-Monitor gespeichert" : "Fehler", r.Message,
                    r.Ok ? ToolTipIcon.Info : ToolTipIcon.Error);
                RefreshUi();
            };
            _selectMenu.DropDownItems.Add(item);
        }
    }

    private void FillGamesMenu()
    {
        _gamesMenu.DropDownItems.Clear();

        var self = Process.GetCurrentProcess().ProcessName;
        var known = new HashSet<string>(_cfg.GameProcesses.Select(NormalizeProcessName), StringComparer.OrdinalIgnoreCase);
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle)
                    && !p.ProcessName.Equals(self, StringComparison.OrdinalIgnoreCase)
                    && !p.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase)
                    && !known.Contains(p.ProcessName))
                {
                    names.Add(p.ProcessName);
                }
            }
            catch
            {
                // Prozess ist gerade beendet worden oder nicht lesbar
            }
            finally
            {
                p.Dispose();
            }
        }

        if (names.Count == 0)
        {
            _gamesMenu.DropDownItems.Add(new ToolStripMenuItem("Keine weiteren Programme mit Fenster") { Enabled = false });
            return;
        }

        foreach (var name in names)
        {
            var item = new ToolStripMenuItem(name);
            var captured = name;
            item.Click += (_, _) =>
            {
                _cfg.GameProcesses.Add(captured);
                _cfg.Save();
                Notify("Spiel hinzugefügt", $"\"{captured}\" schaltet ab jetzt den Sim-Monitor ein.", ToolTipIcon.Info);
            };
            _gamesMenu.DropDownItems.Add(item);
        }
    }

    private static string DescribeResolution(string deviceName)
    {
        var dm = NewDevMode();
        return EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref dm)
            ? $"{dm.dmPelsWidth}x{dm.dmPelsHeight}"
            : "?";
    }

    // ------------------------------------------------------------------
    // Ein-/Ausschalten
    // ------------------------------------------------------------------

    private async Task<ActionResult> RunAsync(Func<ActionResult> action)
    {
        _busy = true;
        try
        {
            return await Task.Run(action);
        }
        catch (Exception ex)
        {
            return new ActionResult(false, ex.Message);
        }
        finally
        {
            _busy = false;
            RefreshUi();
        }
    }

    /// <summary>Manuelle Aktion: hat immer Vorrang vor der Automatik.</summary>
    private async Task ManualAsync(Func<ActionResult> action)
    {
        if (_busy) return;
        if (!_monitor.IsConfigured)
        {
            Notify("Kein Sim-Monitor gewählt", "Bitte zuerst im Menü den Sim-Monitor auswählen.", ToolTipIcon.Warning);
            return;
        }

        _autoEnabled = false;
        var r = await RunAsync(action);
        Notify("Sim-Monitor", r.Message, r.Ok ? ToolTipIcon.Info : ToolTipIcon.Error);
    }

    private async Task ToggleAsync()
    {
        if (_busy) return;
        bool? enabled = _monitor.IsEnabled();
        await ManualAsync(enabled == true ? _monitor.Disable : _monitor.Enable);
    }

    // ------------------------------------------------------------------
    // Automatik: Spiel gestartet -> Monitor an, Spiel beendet -> Monitor aus
    // ------------------------------------------------------------------

    private async void OnTick(object? sender, EventArgs e)
    {
        if (!_cfg.AutoMode || _busy || !_monitor.IsConfigured)
            return;

        bool running = IsGameRunning();

        if (running)
        {
            _gameGoneSince = null;
            if (_gameWasRunning) return;

            _gameWasRunning = true;
            if (_monitor.IsEnabled() == false)
            {
                var r = await RunAsync(_monitor.Enable);
                if (r.Ok) _autoEnabled = true;
                Notify("Spiel erkannt", r.Message, r.Ok ? ToolTipIcon.Info : ToolTipIcon.Error);
            }
            return;
        }

        if (!_gameWasRunning) return;

        _gameGoneSince ??= DateTime.UtcNow;
        if ((DateTime.UtcNow - _gameGoneSince.Value).TotalSeconds < _cfg.DisableDelaySeconds)
            return;

        _gameWasRunning = false;
        _gameGoneSince = null;

        if (_autoEnabled)
        {
            _autoEnabled = false;
            var r = await RunAsync(_monitor.Disable);
            Notify("Spiel beendet", r.Message, r.Ok ? ToolTipIcon.Info : ToolTipIcon.Error);
        }
    }

    private bool IsGameRunning()
    {
        foreach (var raw in _cfg.GameProcesses)
        {
            var name = NormalizeProcessName(raw);
            if (name.Length == 0) continue;

            var procs = Process.GetProcessesByName(name);
            bool any = procs.Length > 0;
            foreach (var p in procs) p.Dispose();
            if (any) return true;
        }
        return false;
    }

    private static string NormalizeProcessName(string name)
    {
        name = name.Trim();
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }

    // ------------------------------------------------------------------
    // UI-Status
    // ------------------------------------------------------------------

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // Kommt evtl. von einem Hintergrund-Thread
        if (_sync.IsHandleCreated)
            _sync.BeginInvoke(new Action(RefreshUi));
    }

    private void RefreshUi()
    {
        bool? on = _monitor.IsConfigured ? _monitor.IsEnabled() : null;

        _tray.Icon = on switch { true => _iconOn, false => _iconOff, _ => _iconUnknown };

        string label = _cfg.MonitorLabel ?? "Sim-Monitor";
        string state = !_monitor.IsConfigured ? "nicht eingerichtet"
                     : on == true ? "AN"
                     : on == false ? "AUS"
                     : "nicht gefunden";

        _statusItem.Text = $"{label}: {state}";
        _tray.Text = Truncate($"Sim-Monitor: {state}", 63);

        _toggleItem.Text = $"Umschalten   ({_cfg.Hotkey})";
        _onItem.Enabled = _monitor.IsConfigured && on != true;
        _offItem.Enabled = _monitor.IsConfigured && on != false;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private void Notify(string title, string text, ToolTipIcon icon)
    {
        _tray.ShowBalloonTip(3000, title, text, icon);
    }

    // ------------------------------------------------------------------
    // Konfiguration, Hotkey, Autostart
    // ------------------------------------------------------------------

    private void RegisterHotkey()
    {
        if (!_hotkey.Register(_cfg.Hotkey, out var error))
            Notify("Hotkey nicht verfügbar", error, ToolTipIcon.Warning);
    }

    private void OpenConfigFile()
    {
        if (!File.Exists(AppConfig.ConfigPath))
            _cfg.Save();

        try
        {
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{AppConfig.ConfigPath}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Notify("Fehler", ex.Message, ToolTipIcon.Error);
        }
    }

    private void OpenLogFile()
    {
        try
        {
            if (!File.Exists(Log.FilePath))
                Log.Write("Log gestartet.");
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{Log.FilePath}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Notify("Fehler", ex.Message, ToolTipIcon.Error);
        }
    }

    private void ReloadConfig()
    {
        _cfg.ApplyFrom(AppConfig.Load());
        _timer.Interval = Math.Max(1, _cfg.PollSeconds) * 1000;
        _autoItem.Checked = _cfg.AutoMode;
        RegisterHotkey();
        RefreshUi();
        Notify("Konfiguration", "Neu geladen.", ToolTipIcon.Info);
    }

    private static bool IsAutostartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(RunValue) != null;
    }

    private static void SetAutostart(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey)!;
        if (enable)
        {
            string exe = Environment.ProcessPath ?? Application.ExecutablePath;
            key.SetValue(RunValue, $"\"{exe}\"");
        }
        else
        {
            key.DeleteValue(RunValue, throwOnMissingValue: false);
        }
    }

    private void ExitApp()
    {
        _timer.Stop();
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _hotkey.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        ExitThread();
    }

    // ------------------------------------------------------------------
    // Icon zeichnen (kleiner Bildschirm, gruen gefuellt = an)
    // ------------------------------------------------------------------

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private static Icon MakeIcon(Color color, bool filled)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var screen = new Rectangle(3, 5, 26, 17);

            if (filled)
            {
                using var brush = new SolidBrush(color);
                g.FillRectangle(brush, screen);
            }

            using var pen = new Pen(filled ? Color.White : color, 2.5f);
            g.DrawRectangle(pen, screen);
            g.DrawLine(pen, 16, 22, 16, 27);
            g.DrawLine(pen, 10, 27, 22, 27);
        }

        IntPtr handle = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }
}
