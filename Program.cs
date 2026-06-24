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
        Logger.Prune();
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
            Thread.Sleep(500); // let the old tray icon vanish before we add ours
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

        bool firstRun = Installer.EnsureAccentMapsJson();
        bool elevatedTakeover = elevated && others.Length > 0;

        Application.ThreadException += (_, e) => HandleUnhandledException(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) HandleUnhandledException(ex);
        };

        Logger.Log("Starting tray app");
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayApp(firstRun: firstRun, elevatedTakeover: elevatedTakeover, elevated: elevated));
    }

    static void HandleUnhandledException(Exception ex)
    {
        Logger.Log($"Unhandled exception: {ex}");
        MessageBox.Show(
            $"Accentra encountered an unexpected error:\n\n{ex.Message}\n\n" +
            "The error has been written to the log file (Open log file… in the tray menu).\n\n" +
            "Please report this problem so it can be fixed.",
            "Accentra — Unexpected Error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
        Process.Start(new ProcessStartInfo("https://ipavicevic.github.io/Accentra/#known-issues") { UseShellExecute = true });
    }
}
