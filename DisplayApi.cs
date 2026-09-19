using System.Runtime.InteropServices;

namespace SimMonitorSwitch;

/// <summary>P/Invoke-Deklarationen fuer die Windows-Display-API.</summary>
internal static class DisplayApi
{
    // ---- ChangeDisplaySettingsEx flags ----
    public const int CDS_UPDATEREGISTRY = 0x00000001;
    public const int CDS_NORESET = 0x10000000;

    // ---- Rueckgabewerte ----
    public const int DISP_CHANGE_SUCCESSFUL = 0;
    public const int DISP_CHANGE_RESTART = 1;

    // ---- DEVMODE.dmFields ----
    public const int DM_POSITION = 0x00000020;
    public const int DM_DISPLAYORIENTATION = 0x00000080;
    public const int DM_BITSPERPEL = 0x00040000;
    public const int DM_PELSWIDTH = 0x00080000;
    public const int DM_PELSHEIGHT = 0x00100000;
    public const int DM_DISPLAYFREQUENCY = 0x00400000;

    // ---- EnumDisplaySettings ----
    public const int ENUM_CURRENT_SETTINGS = -1;
    public const int ENUM_REGISTRY_SETTINGS = -2;

    // ---- DISPLAY_DEVICE.StateFlags ----
    public const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;
    public const int DISPLAY_DEVICE_PRIMARY_DEVICE = 0x00000004;
    public const int DISPLAY_DEVICE_MIRRORING_DRIVER = 0x00000008;

    public const int EDD_GET_DEVICE_INTERFACE_NAME = 0x00000001;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct POINTL
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;

        public POINTL dmPosition;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;

        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    public static extern bool EnumDisplayDevices(string? lpDevice, int iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, int dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    public static extern bool EnumDisplaySettings(string? lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

    [DllImport("user32.dll", CharSet = CharSet.Ansi)]
    public static extern int ChangeDisplaySettingsEx(string? lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, int dwflags, IntPtr lParam);

    // Overload zum "Anwenden" aller gespeicherten Aenderungen (lpDevMode = NULL)
    [DllImport("user32.dll", CharSet = CharSet.Ansi, EntryPoint = "ChangeDisplaySettingsEx")]
    public static extern int ChangeDisplaySettingsExApply(string? lpszDeviceName, IntPtr lpDevMode, IntPtr hwnd, int dwflags, IntPtr lParam);

    // ---- SetDisplayConfig (dasselbe wie Win+P -> Erweitern) ----
    public const uint SDC_TOPOLOGY_EXTEND = 0x00000004;
    public const uint SDC_APPLY = 0x00000080;

    [DllImport("user32.dll")]
    public static extern int SetDisplayConfig(uint numPathArrayElements, IntPtr pathArray,
        uint numModeInfoArrayElements, IntPtr modeInfoArray, uint flags);

    // ---- Hotkey ----
    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public static DEVMODE NewDevMode()
    {
        var dm = new DEVMODE
        {
            dmDeviceName = new string('\0', 32),
            dmFormName = new string('\0', 32)
        };
        dm.dmSize = (short)Marshal.SizeOf<DEVMODE>();
        return dm;
    }

    public static DISPLAY_DEVICE NewDisplayDevice()
    {
        var dd = new DISPLAY_DEVICE
        {
            DeviceName = new string('\0', 32),
            DeviceString = new string('\0', 128),
            DeviceID = new string('\0', 128),
            DeviceKey = new string('\0', 128)
        };
        dd.cb = Marshal.SizeOf<DISPLAY_DEVICE>();
        return dd;
    }
}
