using System.Diagnostics;
using System.Security.Principal;

namespace Accentra;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        bool elevated = new WindowsPrincipal(WindowsIdentity.GetCurrent())
                            .IsInRole(WindowsBuiltInRole.Administrator);
        Logger.Log($"Startup args=[{string.Join(", ", args)}] elevated={elevated} path={Environment.ProcessPath}");

        // Kill any existing instance so a new one (e.g. elevated) can take over.
        var others = Process.GetProcessesByName("Accentra")
                            .Where(p => p.Id != Environment.ProcessId)
                            .ToArray();
        if (others.Length > 0)
        {
            Logger.Log($"Found {others.Length} existing instance(s) — taking over");
            foreach (var p in others)
            {
                try
                {
                    Logger.Log($"  Killing pid={p.Id} startTime={p.StartTime:HH:mm:ss}");
                    p.Kill();
                    bool exited = p.WaitForExit(2000);
                    Logger.Log($"  pid={p.Id} exited={exited}");
                }
                catch (Exception ex)
                {
                    Logger.Log($"  pid={p.Id} kill failed: {ex.GetType().Name}: {ex.Message}");
                }
            }
            Thread.Sleep(500);
            Logger.Log("Takeover complete");
        }
        else
        {
            Logger.Log("No existing instance found");
        }

        using var mutex = new Mutex(initiallyOwned: true, "Local\\Accentra", out bool createdNew);
        if (!createdNew)
        {
            Logger.Log("Another instance is already running after kill attempt — exiting");
            return;
        }

        Installer.EnsureAccentMapsJson();

        Logger.Log("Starting tray app");
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayApp(firstRun: args.Contains("--first-run")));
    }
}
