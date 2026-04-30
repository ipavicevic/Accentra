namespace Accentra;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--uninstall"))
        {
            Installer.Uninstall();
            return;
        }

        if (!Installer.IsInstalledLocation())
        {
            Installer.Install();
            return;
        }

        using var mutex = new Mutex(initiallyOwned: true, "Global\\Accentra", out bool createdNew);
        if (!createdNew)
            return;

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayApp(firstRun: args.Contains("--first-run")));
    }
}
