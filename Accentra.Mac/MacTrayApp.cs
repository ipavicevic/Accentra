using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Accentra;

class MacTrayApp : IDisposable
{
    private static AccentEngine? _engine;
    private static MacKeyboardHook? _hook;
    private static IntPtr _pauseItem;
    private static IntPtr _startAtLoginItem;
    private static IntPtr _permissionItem;
    private static IntPtr _permissionSeparator;
    private static IntPtr _statusButton;
    private static IntPtr _nsApp;

    private static string DisplayVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    private static string FullVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            if (v is null) return "";
            return v.Revision > 0
                ? $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}"
                : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    public MacTrayApp(bool firstRun = false)
    {
        // AppKit is not loaded in a .NET process by default.
        MacNativeMethods.LoadAppKit();
        // Promote to foreground app so the window server accepts status bar registration.
        MacNativeMethods.EnsureForegroundApp();

        _nsApp = MacNativeMethods.objc_msgSend(
            MacNativeMethods.objc_getClass("NSApplication"),
            MacNativeMethods.sel_registerName("sharedApplication"));

        // No dock icon
        MacNativeMethods.objc_msgSend_void_nint(_nsApp,
            MacNativeMethods.sel_registerName("setActivationPolicy:"), 1);

        _engine = new AccentEngine();
        _hook = new MacKeyboardHook(_engine) { ActivatedAfterGrant = OnPermissionGranted };

        var statusBar = MacNativeMethods.objc_msgSend(
            MacNativeMethods.objc_getClass("NSStatusBar"),
            MacNativeMethods.sel_registerName("systemStatusBar"));

        var statusItem = MacNativeMethods.objc_msgSend_cgfloat(
            statusBar,
            MacNativeMethods.sel_registerName("statusItemWithLength:"),
            -1.0); // NSVariableStatusItemLength

        // Retain the status item so it stays alive
        MacNativeMethods.objc_msgSend(statusItem, MacNativeMethods.sel_registerName("retain"));

        _statusButton = MacNativeMethods.objc_msgSend(statusItem, MacNativeMethods.sel_registerName("button"));
        MacNativeMethods.objc_msgSend_void_id(_statusButton,
            MacNativeMethods.sel_registerName("setTitle:"),
            MacNativeMethods.ToNSString("ā"));
        MacNativeMethods.objc_msgSend_void_id(_statusButton,
            MacNativeMethods.sel_registerName("setToolTip:"),
            MacNativeMethods.ToNSString($"Accentra {DisplayVersion}"));

        var target = CreateTarget();
        var menu = BuildMenu(target);

        MacNativeMethods.objc_msgSend_void_id(statusItem,
            MacNativeMethods.sel_registerName("setMenu:"), menu);

        // If the keyboard hook isn't active yet (Accessibility not granted), present
        // a distinct waiting state: dimmed icon + a "grant access" menu item.
        SetWaitingForPermission(!_hook.IsActive);

        if (firstRun)
            ShowAlert("Welcome to Accentra",
                $"Accentra {DisplayVersion} is running in your menu bar.\n\n" +
                "Hold a key to enter accent mode, then press the same key to cycle through variants.\n\n" +
                "Accentra will start automatically when you log in. You can turn this off any time from the menu bar icon.");

        if (AccentMaps.VersionMismatchMessage is { } versionMsg)
            ShowAlert("Accentra — accent maps updated", versionMsg);
        else if (AccentMaps.LoadError is not null) // exact reason is in the log, not shown to the user
        {
            if (AccentMaps.HasLastGood)
            {
                if (ShowChoiceAlert("Accentra — Accent Settings Problem",
                        "Your accent settings couldn't be read. Accentra is using its default settings for now — " +
                        "would you like to go back to your last working settings instead?",
                        "Revert", "Keep"))
                    AccentMaps.RestoreLastGood();
            }
            else
            {
                ShowAlert("Accentra — Accent Settings Problem",
                    "Your accent settings couldn't be read, so Accentra is using its default settings for now.",
                    critical: true);
            }
        }

        AccentMaps.Reloaded += OnAccentMapsReloaded;

        Logger.Log("MacTrayApp started");
    }

    public void Run()
    {
        MacNativeMethods.objc_msgSend_void(_nsApp, MacNativeMethods.sel_registerName("finishLaunching"));
        MacNativeMethods.objc_msgSend_void(_nsApp, MacNativeMethods.sel_registerName("run"));
    }

    // ── ObjC target class ────────────────────────────────────────────────────

    private static IntPtr CreateTarget()
    {
        var superclass = MacNativeMethods.objc_getClass("NSObject");
        var cls = MacNativeMethods.objc_allocateClassPair(superclass, "AccentraTarget", 0);

        unsafe
        {
            MacNativeMethods.class_addMethod(cls, MacNativeMethods.sel_registerName("onStartAtLogin:"),
                (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void>)&OnStartAtLogin, "v@:@");
            MacNativeMethods.class_addMethod(cls, MacNativeMethods.sel_registerName("onPause:"),
                (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void>)&OnPause, "v@:@");
            MacNativeMethods.class_addMethod(cls, MacNativeMethods.sel_registerName("onEditAccentMaps:"),
                (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void>)&OnEditAccentMaps, "v@:@");
            MacNativeMethods.class_addMethod(cls, MacNativeMethods.sel_registerName("onOpenLog:"),
                (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void>)&OnOpenLog, "v@:@");
            MacNativeMethods.class_addMethod(cls, MacNativeMethods.sel_registerName("onAbout:"),
                (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void>)&OnAbout, "v@:@");
            MacNativeMethods.class_addMethod(cls, MacNativeMethods.sel_registerName("onReportProblem:"),
                (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void>)&OnReportProblem, "v@:@");
            MacNativeMethods.class_addMethod(cls, MacNativeMethods.sel_registerName("onQuit:"),
                (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void>)&OnQuit, "v@:@");
            MacNativeMethods.class_addMethod(cls, MacNativeMethods.sel_registerName("onToggleSection:"),
                (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void>)&OnToggleSection, "v@:@");
            MacNativeMethods.class_addMethod(cls, MacNativeMethods.sel_registerName("onOpenAccessibility:"),
                (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void>)&OnOpenAccessibility, "v@:@");
        }

        MacNativeMethods.objc_registerClassPair(cls);
        return MacNativeMethods.objc_msgSend(cls, MacNativeMethods.sel_registerName("new"));
    }

    // ── Menu ─────────────────────────────────────────────────────────────────

    private static IntPtr BuildMenu(IntPtr target)
    {
        var menu = MacNativeMethods.objc_msgSend(
            MacNativeMethods.objc_getClass("NSMenu"),
            MacNativeMethods.sel_registerName("new"));

        // Shown only while Accessibility permission is missing (see SetWaitingForPermission)
        _permissionItem = AddItem(menu, target, "⚠  Enable Accessibility access…", "onOpenAccessibility:");
        _permissionSeparator = AddSeparator(menu);

        _startAtLoginItem = AddItem(menu, target, "Start at Login", "onStartAtLogin:");
        SetCheckmark(_startAtLoginItem, Installer.IsAutoStartEnabled());

        _pauseItem = AddItem(menu, target, "Pause accent input", "onPause:");
        SetCheckmark(_pauseItem, false);

        AddSeparator(menu);
        AddSectionsItem(menu, target);
        AddItem(menu, target, "Edit accent maps…", "onEditAccentMaps:");
        AddItem(menu, target, "Open log file…", "onOpenLog:");
        AddItem(menu, target, $"About Accentra {FullVersion}…", "onAbout:");
        AddItem(menu, target, "Report a problem…", "onReportProblem:");
        AddSeparator(menu);
        AddItem(menu, target, "Quit", "onQuit:");

        return menu;
    }

    private static IntPtr AddItem(IntPtr menu, IntPtr target, string title, string selector)
    {
        var item = MacNativeMethods.objc_msgSend_id_id_id(
            MacNativeMethods.objc_msgSend(MacNativeMethods.objc_getClass("NSMenuItem"),
                MacNativeMethods.sel_registerName("alloc")),
            MacNativeMethods.sel_registerName("initWithTitle:action:keyEquivalent:"),
            MacNativeMethods.ToNSString(title),
            MacNativeMethods.sel_registerName(selector),
            MacNativeMethods.ToNSString(""));

        MacNativeMethods.objc_msgSend_void_id(item,
            MacNativeMethods.sel_registerName("setTarget:"), target);
        MacNativeMethods.objc_msgSend_void_id(menu,
            MacNativeMethods.sel_registerName("addItem:"), item);
        return item;
    }

    private static void AddSectionsItem(IntPtr menu, IntPtr target)
    {
        var sections = AccentMaps.GetSections();
        if (sections.Count == 0) return;

        var parent = MacNativeMethods.objc_msgSend_id_id_id(
            MacNativeMethods.objc_msgSend(MacNativeMethods.objc_getClass("NSMenuItem"),
                MacNativeMethods.sel_registerName("alloc")),
            MacNativeMethods.sel_registerName("initWithTitle:action:keyEquivalent:"),
            MacNativeMethods.ToNSString("Sections"),
            IntPtr.Zero,
            MacNativeMethods.ToNSString(""));

        var submenu = MacNativeMethods.objc_msgSend(
            MacNativeMethods.objc_getClass("NSMenu"),
            MacNativeMethods.sel_registerName("new"));

        foreach (var (name, enabled) in sections)
        {
            var item = AddItem(submenu, target, name, "onToggleSection:");
            SetCheckmark(item, enabled);
        }

        MacNativeMethods.objc_msgSend_void_id(parent,
            MacNativeMethods.sel_registerName("setSubmenu:"), submenu);
        MacNativeMethods.objc_msgSend_void_id(menu,
            MacNativeMethods.sel_registerName("addItem:"), parent);
    }

    private static IntPtr AddSeparator(IntPtr menu)
    {
        var sep = MacNativeMethods.objc_msgSend(
            MacNativeMethods.objc_getClass("NSMenuItem"),
            MacNativeMethods.sel_registerName("separatorItem"));
        MacNativeMethods.objc_msgSend_void_id(menu,
            MacNativeMethods.sel_registerName("addItem:"), sep);
        return sep;
    }

    private static void SetCheckmark(IntPtr item, bool on)
    {
        // NSControlStateValueOn = 1, NSControlStateValueOff = 0
        MacNativeMethods.objc_msgSend_void_nint(item,
            MacNativeMethods.sel_registerName("setState:"), on ? 1 : 0);
    }

    private static void SetHidden(IntPtr item, bool hidden) =>
        MacNativeMethods.objc_msgSend_void_bool(item,
            MacNativeMethods.sel_registerName("setHidden:"), hidden);

    // Reflects the "waiting for Accessibility permission" state in the UI:
    // dims the menu bar icon and swaps the permission item in for the Pause item.
    private static void SetWaitingForPermission(bool waiting)
    {
        MacNativeMethods.objc_msgSend_cgfloat(_statusButton,
            MacNativeMethods.sel_registerName("setAlphaValue:"), waiting ? 0.35 : 1.0);
        SetHidden(_permissionItem, !waiting);
        SetHidden(_permissionSeparator, !waiting);
        SetHidden(_pauseItem, waiting);
    }

    // Called (on the main thread) when the hook activates after the user grants
    // Accessibility. Clears the waiting state and confirms the app is ready.
    private static void OnPermissionGranted()
    {
        SetWaitingForPermission(false);
        ShowAlert("Accentra is ready",
            "Accessibility access granted — Accentra is now active.\n\n" +
            "Hold a key to enter accent mode, then press the same key to cycle variants.");
    }

    // ── Action handlers (called on main thread by ObjC runtime) ──────────────

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnOpenAccessibility(IntPtr self, IntPtr cmd, IntPtr sender)
    {
        try
        {
            Process.Start(new ProcessStartInfo(
                "open", "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")
                { UseShellExecute = true });
        }
        catch (Exception ex) { Logger.Log($"OnOpenAccessibility error: {ex.Message}"); }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnStartAtLogin(IntPtr self, IntPtr cmd, IntPtr sender)
    {
        try
        {
            Installer.ToggleAutoStart();
            SetCheckmark(_startAtLoginItem, Installer.IsAutoStartEnabled());
        }
        catch (Exception ex) { Logger.Log($"OnStartAtLogin error: {ex.Message}"); }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnPause(IntPtr self, IntPtr cmd, IntPtr sender)
    {
        try
        {
            _engine!.Enabled = !_engine.Enabled;
            SetCheckmark(_pauseItem, !_engine.Enabled);
        }
        catch (Exception ex) { Logger.Log($"OnPause error: {ex.Message}"); }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnEditAccentMaps(IntPtr self, IntPtr cmd, IntPtr sender)
    {
        try { Process.Start(new ProcessStartInfo(Installer.AccentMapsDir) { UseShellExecute = true }); }
        catch (Exception ex) { Logger.Log($"OnEditAccentMaps error: {ex.Message}"); }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnOpenLog(IntPtr self, IntPtr cmd, IntPtr sender)
    {
        try { Process.Start(new ProcessStartInfo(Logger.LogPath) { UseShellExecute = true }); }
        catch (Exception ex) { Logger.Log($"OnOpenLog error: {ex.Message}"); }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnAbout(IntPtr self, IntPtr cmd, IntPtr sender)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://ipavicevic.github.io/Accentra/mac.html")
                { UseShellExecute = true });
        }
        catch (Exception ex) { Logger.Log($"OnAbout error: {ex.Message}"); }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnReportProblem(IntPtr self, IntPtr cmd, IntPtr sender)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://ipavicevic.github.io/Accentra/mac.html#known-issues")
                { UseShellExecute = true });
        }
        catch (Exception ex) { Logger.Log($"OnReportProblem error: {ex.Message}"); }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnToggleSection(IntPtr self, IntPtr cmd, IntPtr sender)
    {
        try
        {
            var titleNs = MacNativeMethods.objc_msgSend(sender, MacNativeMethods.sel_registerName("title"));
            var title = MacNativeMethods.FromNSString(titleNs);
            AccentMaps.ToggleSection(title);
            var enabled = AccentMaps.GetSections().FirstOrDefault(s => s.Name == title).Enabled;
            SetCheckmark(sender, enabled);
        }
        catch (Exception ex) { Logger.Log($"OnToggleSection error: {ex.Message}"); }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnQuit(IntPtr self, IntPtr cmd, IntPtr sender)
    {
        try
        {
            Logger.Log("Quit requested from tray menu");
            MacNativeMethods.objc_msgSend_void_id(_nsApp,
                MacNativeMethods.sel_registerName("terminate:"), IntPtr.Zero);
        }
        catch (Exception ex) { Logger.Log($"OnQuit error: {ex.Message}"); }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // NSAlertStyleCritical — stable, documented AppKit constant; gives error
    // alerts a more prominent icon/presentation than the plain default style.
    private const nint NSAlertStyleCritical = 2;

    // NSAlertFirstButtonReturn — stable, documented AppKit constant.
    private const nint NSAlertFirstButtonReturn = 1000;

    private static void ShowAlert(string title, string body, bool critical = false)
    {
        var alert = MacNativeMethods.objc_msgSend(
            MacNativeMethods.objc_getClass("NSAlert"),
            MacNativeMethods.sel_registerName("new"));
        if (critical)
            MacNativeMethods.objc_msgSend_void_nint(alert,
                MacNativeMethods.sel_registerName("setAlertStyle:"), NSAlertStyleCritical);
        MacNativeMethods.objc_msgSend_void_id(alert,
            MacNativeMethods.sel_registerName("setMessageText:"),
            MacNativeMethods.ToNSString(title));
        MacNativeMethods.objc_msgSend_void_id(alert,
            MacNativeMethods.sel_registerName("setInformativeText:"),
            MacNativeMethods.ToNSString(body));
        MacNativeMethods.objc_msgSend(alert, MacNativeMethods.sel_registerName("runModal"));
    }

    // Two-button, critical-style alert; returns true if the user picked the
    // first (primary) button. Always used for the restore-or-keep prompts.
    private static bool ShowChoiceAlert(string title, string body, string primaryButton, string secondaryButton)
    {
        var alert = MacNativeMethods.objc_msgSend(
            MacNativeMethods.objc_getClass("NSAlert"),
            MacNativeMethods.sel_registerName("new"));
        MacNativeMethods.objc_msgSend_void_nint(alert,
            MacNativeMethods.sel_registerName("setAlertStyle:"), NSAlertStyleCritical);
        MacNativeMethods.objc_msgSend_void_id(alert,
            MacNativeMethods.sel_registerName("setMessageText:"),
            MacNativeMethods.ToNSString(title));
        MacNativeMethods.objc_msgSend_void_id(alert,
            MacNativeMethods.sel_registerName("setInformativeText:"),
            MacNativeMethods.ToNSString(body));
        MacNativeMethods.objc_msgSend_id(alert,
            MacNativeMethods.sel_registerName("addButtonWithTitle:"),
            MacNativeMethods.ToNSString(primaryButton));
        MacNativeMethods.objc_msgSend_id(alert,
            MacNativeMethods.sel_registerName("addButtonWithTitle:"),
            MacNativeMethods.ToNSString(secondaryButton));
        var response = MacNativeMethods.objc_msgSend_nint(alert, MacNativeMethods.sel_registerName("runModal"));
        return response == NSAlertFirstButtonReturn;
    }

    // AccentMaps.Reloaded fires from a background debounce timer; marshal to
    // the main thread (required for any Cocoa API) the same way MacTimer does.
    private static readonly MacNativeMethods.DispatchFunction _reloadDispatcher = DispatchReload;

    private static void OnAccentMapsReloaded(string? error)
    {
        var handle = GCHandle.Alloc(error);
        MacNativeMethods.dispatch_async_f(
            MacNativeMethods.dispatch_get_main_queue(),
            GCHandle.ToIntPtr(handle),
            _reloadDispatcher);
    }

    private static void DispatchReload(IntPtr context)
    {
        var handle = GCHandle.FromIntPtr(context);
        var error = (string?)handle.Target;
        handle.Free();
        if (error is null)
        {
            ShowAlert("Accentra", "Your accent settings were updated.");
            return;
        }

        // Exact reason is in the log (Open log file... menu item), not shown to the user.
        if (AccentMaps.HasLastGood)
        {
            if (ShowChoiceAlert("Accentra — Accent Settings Problem",
                    "Your accent settings couldn't be read. Accentra is still using your previous settings — " +
                    "would you like to go back to your last saved working version instead?",
                    "Revert", "Keep"))
                AccentMaps.RestoreLastGood();
        }
        else
        {
            ShowAlert("Accentra — Accent Settings Problem",
                "Your accent settings couldn't be read. Accentra is still using your previous settings.",
                critical: true);
        }
    }

    public void Dispose()
    {
        AccentMaps.Reloaded -= OnAccentMapsReloaded;
        _hook?.Dispose();
        _hook = null;
    }
}
