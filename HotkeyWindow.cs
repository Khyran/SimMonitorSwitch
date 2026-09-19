using static SimMonitorSwitch.DisplayApi;

namespace SimMonitorSwitch;

/// <summary>Unsichtbares Fenster, das einen globalen Hotkey (z. B. Ctrl+Alt+S) empfaengt.</summary>
internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    private const int HotkeyId = 0x5342;
    private bool _registered;

    public event Action? Pressed;

    public HotkeyWindow()
    {
        CreateHandle(new CreateParams());
    }

    /// <summary>Registriert den Hotkey aus einem Text wie "Ctrl+Alt+S". Ein alter Hotkey wird ersetzt.</summary>
    public bool Register(string text, out string error)
    {
        Unregister();
        error = "";

        if (!TryParse(text, out uint mods, out uint vk, out error))
            return false;

        if (!RegisterHotKey(Handle, HotkeyId, mods | MOD_NOREPEAT, vk))
        {
            error = Loc.T("hotkey.inUse", text);
            return false;
        }

        _registered = true;
        return true;
    }

    public void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(Handle, HotkeyId);
            _registered = false;
        }
    }

    private static bool TryParse(string text, out uint mods, out uint vk, out string error)
    {
        mods = 0;
        vk = 0;
        error = "";

        if (string.IsNullOrWhiteSpace(text))
        {
            error = Loc.T("hotkey.none");
            return false;
        }

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        Keys? key = null;

        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "strg":
                case "control":
                    mods |= MOD_CONTROL;
                    break;
                case "alt":
                    mods |= MOD_ALT;
                    break;
                case "shift":
                    mods |= MOD_SHIFT;
                    break;
                case "win":
                    mods |= MOD_WIN;
                    break;
                default:
                    // Einzelne Ziffer "1" -> Keys.D1 (sonst wuerde "1" als Zahl interpretiert)
                    var name = part.Length == 1 && char.IsDigit(part[0]) ? "D" + part : part;
                    if (Enum.TryParse<Keys>(name, true, out var k) && k != Keys.None)
                        key = k;
                    else
                    {
                        error = Loc.T("hotkey.unknownKey", part);
                        return false;
                    }
                    break;
            }
        }

        if (key == null || mods == 0)
        {
            error = Loc.T("hotkey.needModifier");
            return false;
        }

        vk = (uint)key.Value;
        return true;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            Pressed?.Invoke();

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        Unregister();
        if (Handle != IntPtr.Zero)
            DestroyHandle();
    }
}
