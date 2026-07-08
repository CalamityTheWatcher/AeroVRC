using System.Diagnostics;
using System.Text;
using Microsoft.Win32;

namespace AeroVRC;

// ============================================================================
//  PERFORMANCE SETTINGS  (was the Optimization tab - now the Performance category)
// ============================================================================

public partial class MainForm
{
    internal AeroCheckBox optCacheCb, optProcCb, optCompCb, optAffCb, optFsoCb, optDvrCb, loVerboseCb, loDebugCb;
    internal AeroStepper optCacheSizeNum, optCacheExpNum, optAffNum, wpCoresNum;
    internal TextBox optCacheDirBox, loExtraBox, loOutBox;
    internal ComboBox optProcLvl, optCompLvl, wpPrioCombo;
    internal Label optExeLbl;
    internal ListBox wpList;
    internal List<string> wpIds = new();
    internal int cpuCoreCount = Environment.ProcessorCount;

    // Small helper: a muted hint line inside a settings card.
    static Label NewOptHint(string text) => new()
    {
        Name = "onCardMuted", Text = text, Font = Ui.FontMuted, AutoSize = true,
        Margin = new Padding(0, 2, 0, 6),
    };

    void BuildPerformanceSettings()
    {
        // ---- VRChat Cache section ----
        var secCache = NewSettingsSection("VRChat Cache", "Performance");
        optCacheCb = NewSettingCheckbox("Manage VRChat cache settings (writes config.json; applied while VRChat is closed)", config.Optimization.CacheTuning.Enabled, (s, e) =>
        {
            if (loading) return;
            config.Optimization.CacheTuning.Enabled = optCacheCb.Checked;
            SaveConfig();
            if (optCacheCb.Checked) ApplyVrcCacheSettings(false);
        });
        secCache.Controls.Add(optCacheCb);
        secCache.Controls.Add(NewSettingNumeric("     Max cache size (GB):", 1, 500, config.Optimization.CacheTuning.SizeGB,
            (s, e) => { if (loading) return; config.Optimization.CacheTuning.SizeGB = (int)((AeroStepper)s).Value; SaveConfig(); }, out optCacheSizeNum));
        secCache.Controls.Add(NewSettingNumeric("     Cache expiry (days):", 1, 365, config.Optimization.CacheTuning.ExpiryDays,
            (s, e) => { if (loading) return; config.Optimization.CacheTuning.ExpiryDays = (int)((AeroStepper)s).Value; SaveConfig(); }, out optCacheExpNum));

        var rowCacheDir = new FlowLayoutPanel
        {
            AutoSize = true, WrapContents = false, BackColor = Color.Transparent,
            Margin = new Padding(0, 2, 0, 2),
        };
        rowCacheDir.Controls.Add(new Label
        {
            Name = "onCard", Text = "     Cache folder:", AutoSize = true,
            Margin = new Padding(0, 6, 10, 0),
        });
        optCacheDirBox = new TextBox
        {
            Width = 300, ReadOnly = true,
            BackColor = Ui.InputBg, ForeColor = Ui.Text,
            Text = !string.IsNullOrEmpty(config.Optimization.CacheTuning.Directory) ? config.Optimization.CacheTuning.Directory : "(VRChat default)",
            Margin = new Padding(0, 2, 8, 0),
        };
        rowCacheDir.Controls.Add(optCacheDirBox);
        var cacheDirBrowse = new Button
        {
            Text = "Browse...", AutoSize = true,
            Padding = new Padding(8, 2, 8, 2), Margin = new Padding(0, 1, 6, 0),
        };
        Ui.StyleButton(cacheDirBrowse, "secondary");
        cacheDirBrowse.Click += (s, e) =>
        {
            using var fb = new FolderBrowserDialog();
            if (!string.IsNullOrEmpty(config.Optimization.CacheTuning.Directory)) fb.SelectedPath = config.Optimization.CacheTuning.Directory;
            if (fb.ShowDialog() == DialogResult.OK)
            {
                config.Optimization.CacheTuning.Directory = fb.SelectedPath;
                optCacheDirBox.Text = fb.SelectedPath;
                SaveConfig();
            }
        };
        rowCacheDir.Controls.Add(cacheDirBrowse);
        var cacheDirClear = new Button
        {
            Text = "Use default", AutoSize = true,
            Padding = new Padding(8, 2, 8, 2), Margin = new Padding(0, 1, 0, 0),
        };
        Ui.StyleButton(cacheDirClear, "secondary");
        cacheDirClear.Click += (s, e) =>
        {
            config.Optimization.CacheTuning.Directory = "";
            optCacheDirBox.Text = "(VRChat default)";
            SaveConfig();
        };
        rowCacheDir.Controls.Add(cacheDirClear);
        secCache.Controls.Add(rowCacheDir);

        var applyCacheBtn = new Button
        {
            Text = "Apply cache settings now", AutoSize = true,
            Padding = new Padding(10, 4, 10, 4), Margin = new Padding(0, 8, 0, 4),
        };
        Ui.StyleButton(applyCacheBtn, "secondary");
        applyCacheBtn.Click += (s, e) =>
        {
            if (ApplyVrcCacheSettings(false))
                MessageBox.Show("VRChat cache settings written to config.json.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show("Could not write config.json. Make sure VRChat is fully closed, then try again.", "Not applied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };
        secCache.Controls.Add(applyCacheBtn);
        secCache.Controls.Add(NewOptHint("Larger cache = fewer re-downloads but more disk use. VRChat must be closed to change these."));

        // ---- Process Priority section ----
        var secPrio = NewSettingsSection("Process Priority", "Performance");
        optProcCb = NewSettingCheckbox("Boost VRChat's CPU priority while it's running", config.Optimization.ProcPriority.Enabled,
            (s, e) => { if (loading) return; config.Optimization.ProcPriority.Enabled = optProcCb.Checked; SaveConfig(); });
        secPrio.Controls.Add(optProcCb);
        secPrio.Controls.Add(NewSettingCombo("     VRChat priority:", new[] { "AboveNormal", "High" }, config.Optimization.ProcPriority.Level,
            (s, e) => { if (loading) return; config.Optimization.ProcPriority.Level = (string)((ComboBox)s).SelectedItem; SaveConfig(); }, out optProcLvl));
        optCompCb = NewSettingCheckbox("Lower companion apps' CPU priority while VRChat runs", config.Optimization.CompanionPriority.Enabled,
            (s, e) => { if (loading) return; config.Optimization.CompanionPriority.Enabled = optCompCb.Checked; SaveConfig(); });
        secPrio.Controls.Add(optCompCb);
        secPrio.Controls.Add(NewSettingCombo("     Companion priority:", new[] { "BelowNormal", "Idle" }, config.Optimization.CompanionPriority.Level,
            (s, e) => { if (loading) return; config.Optimization.CompanionPriority.Level = (string)((ComboBox)s).SelectedItem; SaveConfig(); }, out optCompLvl));
        secPrio.Controls.Add(NewOptHint("Companion apps are those you've given a path on the Apps tab (VRCX, VD Streamer, overlays, etc.)."));
        optAffCb = NewSettingCheckbox("Limit VRChat to a subset of CPU cores (affinity)", config.Optimization.Affinity.Enabled,
            (s, e) => { if (loading) return; config.Optimization.Affinity.Enabled = optAffCb.Checked; SaveConfig(); });
        secPrio.Controls.Add(optAffCb);
        int affCoresInit = config.Optimization.Affinity.Cores;
        if (affCoresInit <= 0 || affCoresInit > cpuCoreCount) affCoresInit = cpuCoreCount;
        secPrio.Controls.Add(NewSettingNumeric($"     Cores to use (of {cpuCoreCount}):", 1, cpuCoreCount, affCoresInit,
            (s, e) => { if (loading) return; config.Optimization.Affinity.Cores = (int)((AeroStepper)s).Value; SaveConfig(); }, out optAffNum));
        secPrio.Controls.Add(NewOptHint("Affinity pins VRChat to the first N logical cores while it runs; set to all cores to disable. Resets when VRChat closes."));

        // ---- Per-world performance profiles ----
        var secWProf = NewSettingsSection("Per-world performance profiles", "Performance");
        secWProf.Controls.Add(NewOptHint("While you're in a world saved here, its priority & core count override the global settings above. Great for heavy club/event worlds."));
        secWProf.Controls.Add(NewSettingCombo("     Priority in this world:", new[] { "Normal", "AboveNormal", "High" }, "High", (s, e) => { }, out wpPrioCombo));
        secWProf.Controls.Add(NewSettingNumeric($"     Cores to use (of {cpuCoreCount}; {cpuCoreCount} = all):", 1, cpuCoreCount, cpuCoreCount, (s, e) => { }, out wpCoresNum));
        var wpBtnRow = new FlowLayoutPanel
        {
            AutoSize = true, WrapContents = false, BackColor = Color.Transparent,
            Margin = new Padding(0, 6, 0, 2),
        };
        var wpSaveBtn = new Button
        {
            Text = "Save profile for current world", AutoSize = true,
            Padding = new Padding(10, 4, 10, 4), Margin = new Padding(0, 0, 8, 0),
        };
        Ui.StyleButton(wpSaveBtn, "primary");
        wpBtnRow.Controls.Add(wpSaveBtn);
        var wpDelBtn = new Button
        {
            Text = "Remove selected", AutoSize = true,
            Padding = new Padding(10, 4, 10, 4),
        };
        Ui.StyleButton(wpDelBtn, "secondary");
        wpBtnRow.Controls.Add(wpDelBtn);
        secWProf.Controls.Add(wpBtnRow);
        wpList = new ListBox
        {
            Size = new Size(620, 86),
            Font = Ui.FontBody, BorderStyle = BorderStyle.None,
            BackColor = Ui.InputBg, ForeColor = Ui.Text,
            Margin = new Padding(0, 4, 0, 2),
        };
        secWProf.Controls.Add(wpList);
        RebuildWorldProfiles();

        wpSaveBtn.Click += (s, e) =>
        {
            if (string.IsNullOrEmpty(currentInstanceId) || string.IsNullOrEmpty(currentWorld) || currentWorld == "Unknown world")
            {
                MessageBox.Show("Join a world first - the profile is saved for the world you're currently in.",
                    "No world", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var wid = currentInstanceId.Split(':')[0];
            if (string.IsNullOrEmpty(wid)) return;
            config.WorldProfiles[wid] = new WorldProfile
            {
                Name = currentWorld,
                Priority = (string)wpPrioCombo.SelectedItem,
                Cores = (int)wpCoresNum.Value,
            };
            SaveConfig();
            RebuildWorldProfiles();
            WriteLog($"World profile saved: {currentWorld} -> {wpPrioCombo.SelectedItem}, {(int)wpCoresNum.Value} core(s).");
        };
        wpDelBtn.Click += (s, e) =>
        {
            int i = wpList.SelectedIndex;
            if (i < 0 || i >= wpIds.Count) return;
            var wid = wpIds[i];
            if (config.WorldProfiles.Remove(wid))
            {
                SaveConfig();
                RebuildWorldProfiles();
            }
        };

        // ---- Windows Game Tweaks section ----
        var secGame = NewSettingsSection("Windows Game Tweaks", "Performance");
        optFsoCb = NewSettingCheckbox("Disable Fullscreen Optimizations for VRChat.exe", config.Optimization.FullscreenOpt.Enabled, (s, e) =>
        {
            if (loading) return;
            config.Optimization.FullscreenOpt.Enabled = optFsoCb.Checked;
            SaveConfig();
            SetFullscreenOpt(optFsoCb.Checked);
            RefreshOptStatus();
        });
        secGame.Controls.Add(optFsoCb);
        optDvrCb = NewSettingCheckbox("Disable Game DVR / background capture (Game Bar)", config.Optimization.GameDVR.Enabled, (s, e) =>
        {
            if (loading) return;
            config.Optimization.GameDVR.Enabled = optDvrCb.Checked;
            SaveConfig();
            SetGameDVR(optDvrCb.Checked);
        });
        secGame.Controls.Add(optDvrCb);
        optExeLbl = new Label
        {
            Name = "onCardMuted", Font = Ui.FontMuted, AutoSize = true,
            Margin = new Padding(0, 4, 0, 2),
        };
        secGame.Controls.Add(optExeLbl);
        secGame.Controls.Add(NewOptHint("These change Windows registry settings for your user account and are reversible - toggle off to restore."));

        // ---- Steam Launch Options helper ----
        var secLaunch = NewSettingsSection("Steam Launch Options", "Performance");
        secLaunch.Controls.Add(NewOptHint("Extra args for advanced users. Tick options, then Copy and paste into Steam - VRChat - Properties - Launch Options. (Desktop Mode is handled by the app - see the Monitoring settings.)"));
        loVerboseCb = NewSettingCheckbox("Verbose SDK logging (--enable-sdk-log-levels)", config.Optimization.LaunchOpts.VerboseLogs,
            (s, e) => { if (loading) return; config.Optimization.LaunchOpts.VerboseLogs = loVerboseCb.Checked; SaveConfig(); RefreshLaunchOpts(); });
        secLaunch.Controls.Add(loVerboseCb);
        loDebugCb = NewSettingCheckbox("Debug overlay (--enable-debug-gui)", config.Optimization.LaunchOpts.DebugGui,
            (s, e) => { if (loading) return; config.Optimization.LaunchOpts.DebugGui = loDebugCb.Checked; SaveConfig(); RefreshLaunchOpts(); });
        secLaunch.Controls.Add(loDebugCb);
        secLaunch.Controls.Add(NewSettingText("     Extra args:", config.Optimization.LaunchOpts.Extra, 300,
            (s, e) => { if (loading) return; config.Optimization.LaunchOpts.Extra = ((TextBox)s).Text; SaveConfig(); RefreshLaunchOpts(); }, out loExtraBox));
        var rowLoOut = new FlowLayoutPanel
        {
            AutoSize = true, WrapContents = false, BackColor = Color.Transparent,
            Margin = new Padding(0, 6, 0, 2),
        };
        loOutBox = new TextBox
        {
            Width = 360, ReadOnly = true,
            BackColor = Ui.InputBg, ForeColor = Ui.Text, Font = Ui.FontMono,
            Margin = new Padding(0, 0, 8, 0),
        };
        rowLoOut.Controls.Add(loOutBox);
        var loCopyBtn = new Button { Text = "Copy", AutoSize = true, Padding = new Padding(12, 3, 12, 3) };
        Ui.StyleButton(loCopyBtn, "secondary");
        loCopyBtn.Click += (s, e) =>
        {
            var str = BuildLaunchOptions();
            if (str.Length > 0) { Clipboard.SetText(str); WriteLog("Launch options copied to clipboard."); }
        };
        rowLoOut.Controls.Add(loCopyBtn);
        secLaunch.Controls.Add(rowLoOut);

        // ---- Startup impact analyzer ----
        var secImpact = NewSettingsSection("Startup impact analyzer", "Performance");
        secImpact.Controls.Add(NewOptHint("Snapshot of what's using your PC right now - run it before launching VRChat to see what's worth closing."));
        var impactBtn = new Button
        {
            Text = "Analyze now", AutoSize = true,
            Padding = new Padding(12, 5, 12, 5), Margin = new Padding(0, 4, 0, 2),
        };
        Ui.StyleButton(impactBtn, "primary");
        secImpact.Controls.Add(impactBtn);
        impactBtn.Click += (s, e) => ShowStartupAnalyzer();

        RefreshLaunchOpts();
        RefreshOptStatus();

        // All settings sections now exist - build the alphabetical category buttons
        // and select the first one so the page opens on a single category.
        BuildSetCategoryButtons();
    }

    void RebuildWorldProfiles()
    {
        wpList.BeginUpdate();
        wpList.Items.Clear();
        wpIds = new List<string>();
        foreach (var e in config.WorldProfiles.OrderBy(e => e.Value.Name, StringComparer.OrdinalIgnoreCase))
        {
            var v = e.Value;
            var coreTxt = v.Cores > 0 && v.Cores < cpuCoreCount ? $"{v.Cores} cores" : "all cores";
            wpList.Items.Add($"{v.Name}   -   {v.Priority}, {coreTxt}");
            wpIds.Add(e.Key);
        }
        if (wpList.Items.Count == 0) wpList.Items.Add("(no profiles yet - join a world and click Save)");
        wpList.EndUpdate();
    }

    internal void RefreshLaunchOpts()
    {
        if (loOutBox != null)
        {
            var s = BuildLaunchOptions();
            loOutBox.Text = s.Length > 0 ? s : "(no options)";
        }
    }

    internal void RefreshOptStatus()
    {
        if (optExeLbl == null) return;
        var p = GetVrcExePath();
        optExeLbl.Text = p != null
            ? $"Detected VRChat.exe: {p}"
            : "VRChat.exe not detected yet - launch VRChat once to enable the exe tweak.";
    }

    // Known heavyweight background apps -> friendly names for the recommendations.
    static readonly Dictionary<string, string> impactHeavyApps = new()
    {
        ["chrome"] = "Chrome", ["msedge"] = "Edge", ["firefox"] = "Firefox", ["opera"] = "Opera",
        ["Discord"] = "Discord", ["obs64"] = "OBS Studio", ["obs32"] = "OBS Studio",
        ["wallpaper32"] = "Wallpaper Engine", ["wallpaper64"] = "Wallpaper Engine",
        ["Spotify"] = "Spotify", ["EpicGamesLauncher"] = "Epic Games Launcher",
        ["RiotClientServices"] = "Riot Client", ["Battle_net"] = "Battle.net", ["GalaxyClient"] = "GOG Galaxy",
        ["OneDrive"] = "OneDrive", ["Dropbox"] = "Dropbox", ["Teams"] = "Microsoft Teams", ["ms_teams"] = "Microsoft Teams",
    };

    string GetStartupImpactReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"AeroVRC - Startup impact analysis   ({DateTime.Now:yyyy-MM-dd HH:mm})");
        sb.AppendLine(new string('=', 64));

        // --- Memory pressure ---
        long totMB = 0, freeMB = 0;
        try
        {
            var ci = new Microsoft.VisualBasic.Devices.ComputerInfo();
            totMB = (long)(ci.TotalPhysicalMemory / 1048576);
            freeMB = (long)(ci.AvailablePhysicalMemory / 1048576);
        }
        catch { }
        if (totMB > 0)
        {
            long usedPct = 100 * (totMB - freeMB) / totMB;
            sb.AppendLine();
            sb.AppendLine($"RAM:  {(totMB - freeMB) / 1024.0:0.0} GB of {totMB / 1024.0:0.0} GB in use ({usedPct}%)");
        }
        var disk = GetDiskFreeGB();
        if (disk.HasValue) sb.AppendLine($"Disk: {disk.Value} GB free on the VRChat drive");
        if (gpuStats.VramMB.HasValue && gpuStats.VramTotMB.HasValue && gpuStats.VramTotMB.Value > 0)
        {
            sb.AppendLine($"VRAM: {gpuStats.VramMB.Value / 1024.0:0.0} GB of {gpuStats.VramTotMB.Value / 1024.0:0.0} GB in use ({(int)(100.0 * gpuStats.VramMB.Value / gpuStats.VramTotMB.Value)}%)");
        }

        // --- CPU: two snapshots ~0.9s apart -> per-process CPU% ---
        var snap1 = new Dictionary<int, double>();
        foreach (var p in Process.GetProcesses())
        {
            try { if (p.Id > 4) snap1[p.Id] = p.TotalProcessorTime.TotalMilliseconds; } catch { }
        }
        Thread.Sleep(900);
        var cpuRows = new List<(string N, double P)>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (!snap1.TryGetValue(p.Id, out var prev)) continue;
                double d = p.TotalProcessorTime.TotalMilliseconds - prev;
                if (d <= 0) continue;
                double pct = Math.Round(d / 900.0 / Environment.ProcessorCount * 100, 1);
                if (pct >= 0.5) cpuRows.Add((p.ProcessName, pct));
            }
            catch { }
        }
        sb.AppendLine();
        sb.AppendLine("Top CPU users right now:");
        var top = cpuRows.OrderByDescending(r => r.P).Take(6).ToList();
        if (top.Count == 0) sb.AppendLine("  (nothing significant)");
        foreach (var r in top) sb.AppendLine(string.Format("  {0,5:0.0}%   {1}", r.P, r.N));

        // --- Top RAM users (grouped by process name) ---
        var ramAgg = new Dictionary<string, long>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                var n = p.ProcessName;
                ramAgg.TryGetValue(n, out var cur);
                ramAgg[n] = cur + p.WorkingSet64;
            }
            catch { }
        }
        sb.AppendLine();
        sb.AppendLine("Top RAM users (all instances combined):");
        foreach (var e in ramAgg.OrderByDescending(e => e.Value).Take(6))
            sb.AppendLine(string.Format("  {0,7:0.00} GB   {1}", e.Value / 1073741824.0, e.Key));

        // --- Startup programs ---
        var startup = new List<string>();
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using var k = hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (k != null) startup.AddRange(k.GetValueNames().Where(n => !string.IsNullOrEmpty(n)));
            }
            catch { }
        }
        try
        {
            var startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            startup.AddRange(Directory.GetFiles(startupDir).Select(Path.GetFileNameWithoutExtension));
        }
        catch { }
        sb.AppendLine();
        sb.AppendLine($"Programs that launch with Windows ({startup.Count}):");
        if (startup.Count == 0) sb.AppendLine("  (none found)");
        foreach (var s in startup.Distinct().OrderBy(x => x)) sb.AppendLine($"  - {s}");

        // --- Recommendations ---
        var recs = new List<string>();
        foreach (var e in impactHeavyApps)
        {
            if (Process.GetProcessesByName(e.Key).Length > 0)
                recs.Add($"{e.Value} is running - close it (or add '{e.Key}' to the Performance Mode list in Settings).");
        }
        if (totMB > 0 && 100 * (totMB - freeMB) / totMB >= 80)
            recs.Add("RAM is over 80% used before VRChat even starts - close some of the apps above.");
        if (disk.HasValue && disk.Value < config.DiskMonitor.MinGB)
            recs.Add("Disk space is low - clear the VRChat cache (Settings > Automation) or free some space.");
        sb.AppendLine();
        sb.AppendLine("Recommendations:");
        if (recs.Count == 0) sb.AppendLine("  Looking good - nothing obvious is weighing the system down.");
        foreach (var r in recs.Distinct().OrderBy(x => x)) sb.AppendLine($"  * {r}");
        return sb.ToString();
    }

    void ShowStartupAnalyzer()
    {
        Cursor = Cursors.WaitCursor;
        string txt;
        try { txt = GetStartupImpactReport(); }
        finally { Cursor = Cursors.Default; }
        using var dlg = new Form
        {
            Text = "Startup impact analyzer",
            Size = new Size(660, 580),
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Ui.Bg,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false,
        };
        Ui.SetDarkTitleBar(dlg);
        var tb = new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            Font = Ui.FontMono, BackColor = Ui.LogBg, ForeColor = Ui.Text,
            BorderStyle = BorderStyle.None,
            Location = new Point(14, 14),
            Size = new Size(616, 468),
            Text = txt,
        };
        tb.SelectionStart = 0; tb.SelectionLength = 0;
        dlg.Controls.Add(tb);
        var copyBtn = new Button { Text = "Copy report", Size = new Size(110, 32), Location = new Point(404, 496) };
        Ui.StyleButton(copyBtn, "secondary");
        copyBtn.Click += (s, e) => Clipboard.SetText(tb.Text);
        dlg.Controls.Add(copyBtn);
        var closeBtn = new Button { Text = "Close", Size = new Size(96, 32), Location = new Point(534, 496) };
        Ui.StyleButton(closeBtn, "primary");
        closeBtn.Click += (s, e) => dlg.Close();
        dlg.Controls.Add(closeBtn);
        dlg.ShowDialog(this);
    }
}
