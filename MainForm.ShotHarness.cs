namespace AeroVRC;

// ============================================================================
//  OFF-SCREEN SCREENSHOT HARNESS (verification builds only)
//  Activated by AEROVRC_SHOTDIR; seeds demo data, renders each page to a PNG
//  off-screen, then exits. Never runs without the env var.
// ============================================================================

public partial class MainForm
{
    void RunScreenshotHarness(string shotDir)
    {
        wShown = true;   // suppress the welcome splash
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-2600, -2600);
        Shown += (s, e) =>
        {
            try
            {
                Directory.CreateDirectory(shotDir);
                timer.Stop();
                fxTimer.Stop();
                // Demo data so Apps/Presets don't render empty (mirrors the harness
                // used to screenshot the original build).
                loading = true;   // block Save-Config so nothing is persisted
                config.CustomApps = new List<CustomApp>
                {
                    new() { Name = "SlimeVR", Type = "steam", Value = "3245490", Icon = "" },
                    new() { Name = "Virtual Desktop", Type = "exe", Value = @"C:\Program Files\Virtual Desktop Streamer\VirtualDesktop.Streamer.exe", Icon = "" },
                };
                config.AutoLaunch["SlimeVR"] = true;
                config.Presets = new List<Preset>
                {
                    new()
                    {
                        Name = "Full Body VR", AutoLaunch = true,
                        Apps = new List<PresetApp>
                        {
                            new() { Name = "SlimeVR", Type = "steam", Value = "3245490" },
                            new() { Name = "OVR Advanced Settings", Type = "steam", Value = "1009850" },
                        },
                    },
                };
                RebuildAppsList();
                RebuildPresetsList();

                void Shot(string page, string file)
                {
                    ShowPage(page);
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(200);
                    Application.DoEvents();
                    using var bmp = new Bitmap(ClientSize.Width, ClientSize.Height);
                    DrawToBitmap(bmp, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));
                    bmp.Save(Path.Combine(shotDir, file), System.Drawing.Imaging.ImageFormat.Png);
                }
                Shot("Dashboard", "cs_dashboard.png");
                Shot("Apps", "cs_apps.png");
                Shot("Presets", "cs_presets.png");
                Shot("Settings", "cs_settings.png");
                ShowSetCategory("Performance");
                Application.DoEvents();
                System.Threading.Thread.Sleep(150);
                Shot("Settings", "cs_settings_perf.png");
                Shot("Bookmarks", "cs_bookmarks.png");
                Shot("Statistics", "cs_statistics.png");
                Shot("VRCX", "cs_vrcx.png");
                Shot("Logs", "cs_logs.png");
                File.WriteAllText(Path.Combine(shotDir, "done.txt"), "ok\nconfig: " + ConfigStore.ConfigPath + "\ndiskMinGB: " + config.DiskMonitor.MinGB);
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(Path.Combine(shotDir, "error.txt"), ex.ToString()); } catch { }
            }
            finally
            {
                Close();
            }
        };
    }
}
