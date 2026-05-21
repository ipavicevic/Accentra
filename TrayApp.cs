using System.Diagnostics;

namespace Accentra;

class TrayApp : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly KeyboardHook _hook;
    private readonly AccentEngine _engine;
    private readonly ToolStripMenuItem _sectionsItem;
    private bool _suppressMenuClose;

    public TrayApp(bool firstRun = false, bool elevatedTakeover = false)
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

        var pauseItem = new ToolStripMenuItem("Pause accent input")
        {
            Checked = false
        };
        pauseItem.Click += (_, _) =>
        {
            _engine.Enabled = !_engine.Enabled;
            pauseItem.Checked = !_engine.Enabled;
        };

        _sectionsItem = new ToolStripMenuItem("Sections");
        BuildSectionsMenu();

        var menu = new ContextMenuStrip();
        menu.Closing += (_, e) =>
        {
            if (_suppressMenuClose)
            {
                e.Cancel = true;
                _suppressMenuClose = false;
            }
        };
        menu.Items.Add(startWithWindowsItem);
        menu.Items.Add(pauseItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_sectionsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Edit accent maps...", null, (_, _) =>
            Process.Start(new ProcessStartInfo(
                Path.Combine(Installer.AccentMapsDir, "accent-maps.json")) { UseShellExecute = true }));
        menu.Items.Add("Open log file...", null, (_, _) =>
            Process.Start(new ProcessStartInfo("notepad.exe", Logger.LogPath) { UseShellExecute = true }));
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
        else if (elevatedTakeover)
            ShowElevatedTakeoverOnceLoopIsRunning();
        else
            ShowRunningOnceLoopIsRunning();

        WireAccentMapsOnceLoopIsRunning();
    }

    private void BuildSectionsMenu()
    {
        _sectionsItem.DropDownItems.Clear();
        foreach (var (name, enabled) in AccentMaps.GetSections())
        {
            var item = new ToolStripMenuItem(name) { Checked = enabled };
            var capName = name;
            item.Click += (_, _) =>
            {
                _suppressMenuClose = true;
                AccentMaps.ToggleSection(capName);
                item.Checked = !item.Checked;
            };
            _sectionsItem.DropDownItems.Add(item);
        }
    }

    private static void ShowWelcomeOnceLoopIsRunning()
    {
        var timer = new System.Windows.Forms.Timer { Interval = 200 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            MessageBox.Show(
                $"Accentra {Application.ProductVersion} is now running in your system tray.\n\n" +
                "Right-click the 'ā' icon in the tray for options. Accentra will start automatically with Windows.",
                "Welcome to Accentra",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        };
        timer.Start();
    }

    private void WireAccentMapsOnceLoopIsRunning()
    {
        var timer = new System.Windows.Forms.Timer { Interval = 200 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();

            if (AccentMaps.LoadError is { } startupErr)
                _trayIcon.ShowBalloonTip(8000, "Accentra — accent-maps.json error",
                    $"Could not load your accent maps: {startupErr}\n\nUsing built-in defaults.", ToolTipIcon.Warning);

            var syncCtx = SynchronizationContext.Current!;
            AccentMaps.Reloaded += error => syncCtx.Post(_ =>
            {
                BuildSectionsMenu();
                if (error is null)
                    _trayIcon.ShowBalloonTip(3000, "Accentra", "Accent maps reloaded.", ToolTipIcon.Info);
                else
                    _trayIcon.ShowBalloonTip(8000, "Accentra — accent-maps.json error",
                        $"Could not reload: {error}\n\nKeeping previous maps.", ToolTipIcon.Warning);
            }, null);
        };
        timer.Start();
    }

    private void ShowRunningOnceLoopIsRunning()
    {
        var timer = new System.Windows.Forms.Timer { Interval = 200 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            _trayIcon.ShowBalloonTip(3000, "Accentra", $"Accentra {Application.ProductVersion} is running.", ToolTipIcon.Info);
        };
        timer.Start();
    }

    private void ShowElevatedTakeoverOnceLoopIsRunning()
    {
        var timer = new System.Windows.Forms.Timer { Interval = 200 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            _trayIcon.ShowBalloonTip(4000, "Accentra", "Now running elevated — accent mode works in admin apps.", ToolTipIcon.Info);
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
