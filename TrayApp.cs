using System.Diagnostics;

namespace Accentra;

class TrayApp : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly KeyboardHook _hook;
    private readonly AccentEngine _engine;

    public TrayApp(bool firstRun = false)
    {
        _engine = new AccentEngine();
        _hook = new KeyboardHook(_engine);

        var startWithWindowsItem = new ToolStripMenuItem("Start with Windows")
        {
            Checked = Installer.IsAutoStartEnabled()
        };
        startWithWindowsItem.Click += (_, _) =>
        {
            Installer.ToggleAutoStart();
            startWithWindowsItem.Checked = Installer.IsAutoStartEnabled();
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(startWithWindowsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Edit accent maps...", null, (_, _) =>
            Process.Start(new ProcessStartInfo("explorer.exe", Installer.AccentMapsDir) { UseShellExecute = true }));
        menu.Items.Add("About Accentra...", null, (_, _) =>
            Process.Start(new ProcessStartInfo("https://ipavicevic.github.io/Accentra/") { UseShellExecute = true }));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _trayIcon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = $"Accentra {Application.ProductVersion}",
            ContextMenuStrip = menu,
            Visible = true,
        };

        if (firstRun)
            ShowWelcomeOnceLoopIsRunning();
    }

    private static void ShowWelcomeOnceLoopIsRunning()
    {
        // Defer until the message loop is running so the tray icon is already painted.
        var timer = new System.Windows.Forms.Timer { Interval = 200 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            MessageBox.Show(
                $"Accentra {Application.ProductVersion} is now running in your system tray.\n\n" +
                "Right-click the 'ã' icon in the tray for options. Accentra will start automatically with Windows.",
                "Welcome to Accentra",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        };
        timer.Start();
    }

    private static Icon LoadIcon()
    {
        using var stream = typeof(TrayApp).Assembly.GetManifestResourceStream("Accentra.icon.ico")!;
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hook.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
