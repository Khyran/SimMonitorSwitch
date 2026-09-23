namespace SimMonitorSwitch;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        bool afterUpdate = args.Contains(Updater.AfterUpdateArg);

        // Nur eine Instanz gleichzeitig. Nach einem Update laeuft die alte Instanz noch kurz, darauf warten.
        using var mutex = new Mutex(false, @"Local\SimMonitorSwitch.SingleInstance");
        bool isFirst;
        try
        {
            isFirst = mutex.WaitOne(afterUpdate ? TimeSpan.FromSeconds(15) : TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            isFirst = true;   // alte Instanz hat sich beendet, ohne den Mutex freizugeben
        }
        if (!isFirst)
            return;

        if (afterUpdate)
            Updater.CleanUpAfterUpdate();

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp(afterUpdate));
    }
}
