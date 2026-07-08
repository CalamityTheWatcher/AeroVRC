using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AeroVRC;

static class Program
{
    static Mutex singleInstance;

    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);

    [STAThread]
    static void Main()
    {
        // Single instance: a second copy would double-count playtime (every running
        // instance ticks +1s into today's total while VRChat is open), so focus the
        // existing window and exit. Skipped for the isolated test/screenshot harness,
        // which uses its own config dir and can't touch the real totals.
        bool isolated = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AEROVRC_CONFIGDIR"))
                     || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AEROVRC_SHOTDIR"));
        if (!isolated)
        {
            singleInstance = new Mutex(true, @"Local\AeroVRC_SingleInstance", out bool isNew);
            if (!isNew) { FocusExistingInstance(); return; }
        }

        // Deliberately DPI-UNAWARE, matching the original build: the whole UI is laid
        // out in fixed 96-DPI pixels and Windows scales the window as one bitmap on
        // 125%/150% displays, so fonts and layout always scale together.
        Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
        GC.KeepAlive(singleInstance);
    }

    static void FocusExistingInstance()
    {
        try
        {
            var me = Process.GetCurrentProcess();
            foreach (var p in Process.GetProcessesByName(me.ProcessName))
            {
                if (p.Id == me.Id || p.MainWindowHandle == IntPtr.Zero) continue;
                ShowWindow(p.MainWindowHandle, 9);   // SW_RESTORE
                SetForegroundWindow(p.MainWindowHandle);
                break;
            }
        }
        catch { }
    }
}
