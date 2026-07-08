namespace AeroVRC;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Deliberately DPI-UNAWARE, matching the original build: the whole UI is laid
        // out in fixed 96-DPI pixels and Windows scales the window as one bitmap on
        // 125%/150% displays, so fonts and layout always scale together.
        Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
