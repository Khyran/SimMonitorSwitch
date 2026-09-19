namespace SimMonitorSwitch;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Nur eine Instanz gleichzeitig
        using var mutex = new Mutex(true, @"Local\SimMonitorSwitch.SingleInstance", out bool isFirst);
        if (!isFirst)
            return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
