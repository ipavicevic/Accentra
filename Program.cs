namespace Accentra;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Logger.Log($"Startup args=[{string.Join(", ", args)}] path={Environment.ProcessPath}");

        if (args.Contains("--uninstall"))
        {
            Logger.Log("Uninstall requested");
            Installer.Uninstall();
            return;
        }

        if (!Installer.IsInstalledLocation())
        {
            Logger.Log("Not installed location — running installer");
            Installer.Install();
            return;
        }

        using var mutex = new Mutex(initiallyOwned: true, "Local\\Accentra", out bool createdNew);
        if (!createdNew)
        {
            Logger.Log("Another instance is already running — exiting");
            return;
        }

        Logger.Log("Starting tray app");
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayApp(firstRun: args.Contains("--first-run")));
    }
}
