using System.Drawing.Drawing2D;

namespace AeroVRC;

// ============================================================================
//  SETTINGS PAGE  (category pill buttons + card sections)
// ============================================================================

public partial class MainForm
{
    internal Panel pgSet;
    FlowLayoutPanel setCatBar, setFlow;
    internal readonly Dictionary<string, List<Panel>> setCategories = new();
    internal List<Button> setCatButtons = new();
    internal string setActiveCat = "";

    // ---- Settings control fields (two-way synced by ApplyConfigToUi) ----
    internal AeroStepper intervalNum, cooldownNum, crashMaxNum, crashWinNum, schedHrsNum,
        freezeHangNum, freezeStallNum, ramGBNum, fpsMinNum, fpsHoldNum, fpsPortNum,
        breakHrsNum, diskGBNum, crasherNum, vramPctNum, gpuMaxCNum, cpuMaxCNum,
        autoBmHrsNum, cacheDaysNum, goalDailyNum, goalWeeklyNum, bedWarnNum,
        oscIntNum, vrcxIntNum;
    internal AeroCheckBox desktopCb, crashCb, schedCb, freezeCb, ramCb, fpsCb, frameLogCb,
        crashHintsCb, crashArchCb, soundCb, breakCb, breakCloseCb, diskCb, crasherCb,
        pingMonCb, pingWarnCb, gpuMonCb, vramAlertCb, tempWarnCb, autoCloseCb, rejoinCb,
        rejoinRestartCb, autoBmCb, steamvrCb, cacheCb, schedLaunchCb, schedLaunchMonCb,
        bedCb, bedCloseCb, bedAppsCb, bedWindCb, bedMediaCb, schedJoinCb, blockCb,
        blockLeaveCb, watchCb, watchSoundCb, oscCb, discordCb, vrcxCb, sparkleCb,
        logoAnimCb, welcomeCb, startupSoundCb;
    internal TextBox fpsAddrBox, pingHostBox, schedLaunchTimeBox, perfListBox, bedTimeBox,
        bedWindEndBox, bedMediaUrlBox, schedJoinTimeBox, schedJoinTargetBox, schedJoinNameBox,
        blockListBox, watchListBox, oscTplBox, discIdBox, vrcxPathBox;
    internal ComboBox bedActCombo, bedMediaTypeCombo, sparkStyleCombo;
    Label hwStatusLbl;
    Button hwDlBtn;

    // Section container factory (a card that auto-sizes to a vertical stack inside).
    // The card grows to fit its widest row, so long labels never clip. Every section
    // is filed under a category (defaults to its own title); the pill buttons above
    // toggle card visibility.
    internal FlowLayoutPanel NewSettingsSection(string title, string category = null)
    {
        category ??= title;
        var stack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Padding = new Padding(16, 14, 16, 14),
            MinimumSize = new Size(720, 0),
        };

        var card = Ui.NewCard();
        card.AutoSize = true;
        card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        card.Margin = new Padding(0, 0, 0, 16);
        card.Controls.Add(stack);

        // Header: an azure accent bar + the title, for a bit more visual hierarchy.
        var hdrRow = new FlowLayoutPanel
        {
            WrapContents = false, AutoSize = true, BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 12),
        };
        var bar = new Panel { Size = new Size(4, 20), Margin = new Padding(0, 2, 11, 0) };
        bar.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Ui.OpaqueBack((Panel)s));
            using var p = Ui.RoundedPath(0, 0, ((Panel)s).Width, ((Panel)s).Height, 2);
            using var b = new SolidBrush(Ui.Accent);
            e.Graphics.FillPath(b, p);
        };
        hdrRow.Controls.Add(bar);
        var h = new Label
        {
            Name = "onCard", Text = title, Font = Ui.FontHeader, AutoSize = true,
            Margin = new Padding(0, 1, 0, 0),
        };
        hdrRow.Controls.Add(h);
        stack.Controls.Add(hdrRow);

        setFlow.Controls.Add(card);
        if (!setCategories.TryGetValue(category, out var list))
        {
            list = new List<Panel>();
            setCategories[category] = list;
        }
        list.Add(card);
        return stack;
    }

    // Reveals one category's cards, hides the rest, and lights its button.
    internal void ShowSetCategory(string name)
    {
        setActiveCat = name;
        foreach (var cat in setCategories.Keys)
        {
            bool vis = cat == name;
            foreach (var card in setCategories[cat]) card.Visible = vis;
        }
        foreach (var b in setCatButtons)
            Ui.StyleButton(b, (string)b.Tag == name ? "primary" : "secondary");
        try { setFlow.AutoScrollPosition = new Point(0, 0); } catch { }
    }

    // Builds the alphabetical category buttons - call once every section exists.
    internal void BuildSetCategoryButtons()
    {
        setCatBar.SuspendLayout();
        setCatBar.Controls.Clear();
        setCatButtons = new List<Button>();
        foreach (var cat in setCategories.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var b = new Button
            {
                Text = cat, Tag = cat, AutoSize = true,
                MinimumSize = new Size(0, 32),
                Padding = new Padding(12, 4, 12, 4),
                Margin = new Padding(0, 0, 8, 8),
            };
            Ui.StyleButton(b, "secondary");
            b.Click += (s, e) => ShowSetCategory((string)((Button)s).Tag);
            setCatBar.Controls.Add(b);
            setCatButtons.Add(b);
        }
        setCatBar.ResumeLayout();
        if (setCatButtons.Count > 0) ShowSetCategory((string)setCatButtons[0].Tag);
    }

    // ---- Row helpers used inside sections ----
    internal AeroCheckBox NewSettingCheckbox(string text, bool isChecked, EventHandler onChange)
    {
        var cb = new AeroCheckBox
        {
            Name = "onCard", Text = text, AutoSize = true, Font = Ui.FontBody, Checked = isChecked,
            Margin = new Padding(0, 5, 0, 5),
        };
        cb.CheckedChanged += onChange;
        return cb;
    }
    internal FlowLayoutPanel NewSettingNumeric(string label, int min, int max, int val, EventHandler onChange, out AeroStepper stepper)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0, 2, 0, 2) };
        var l = new Label { Name = "onCard", Text = label, AutoSize = true, Font = Ui.FontBody, Margin = new Padding(0, 7, 10, 0) };
        row.Controls.Add(l);
        var n = new AeroStepper { Minimum = min, Maximum = max, Value = Math.Max(min, Math.Min(max, val)), Width = 74, Font = Ui.FontBody, BackColor = Ui.Card, Margin = new Padding(0, 2, 0, 0) };
        n.ValueChanged += onChange;
        row.Controls.Add(n);
        stepper = n;
        return row;
    }
    internal FlowLayoutPanel NewSettingCombo(string label, string[] items, string selected, EventHandler onChange, out ComboBox combo)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0, 2, 0, 2) };
        var l = new Label { Name = "onCard", Text = label, AutoSize = true, Font = Ui.FontBody, Margin = new Padding(0, 6, 10, 0) };
        row.Controls.Add(l);
        var c = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 130, Font = Ui.FontBody, BackColor = Ui.InputBg, ForeColor = Ui.Text,
            Margin = new Padding(0, 2, 0, 0),
        };
        foreach (var it in items) c.Items.Add(it);
        int idx = c.Items.IndexOf(selected);
        if (idx < 0) idx = 0;
        if (c.Items.Count > 0) c.SelectedIndex = idx;
        c.SelectedIndexChanged += onChange;
        row.Controls.Add(c);
        combo = c;
        return row;
    }
    internal FlowLayoutPanel NewSettingText(string label, string value, int width, EventHandler onChange, out TextBox box)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0, 2, 0, 2) };
        var l = new Label { Name = "onCard", Text = label, AutoSize = true, Font = Ui.FontBody, Margin = new Padding(0, 6, 10, 0) };
        row.Controls.Add(l);
        var tb = new TextBox { Width = width, Font = Ui.FontBody, Text = value, BackColor = Ui.InputBg, ForeColor = Ui.Text, Margin = new Padding(0, 2, 0, 0) };
        tb.TextChanged += onChange;
        row.Controls.Add(tb);
        box = tb;
        return row;
    }

    void BuildSettingsPage()
    {
        pgSet = NewPage("Settings");
        var setTitle = NewPageTitle("Settings");
        pgSet.Controls.Add(setTitle);

        // Category chooser: a wrapping row of pill buttons.
        setCatBar = new FlowLayoutPanel
        {
            Location = new Point(4, 48), Size = new Size(852, 84),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true, BackColor = Color.Transparent,
        };
        pgSet.Controls.Add(setCatBar);

        setFlow = new FlowLayoutPanel
        {
            Location = new Point(4, 138), Size = new Size(852, 472),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true,
            BackColor = Color.Transparent,
        };
        pgSet.Controls.Add(setFlow);

        // ---- Monitoring section ----
        var secMon = NewSettingsSection("Monitoring");
        secMon.Controls.Add(NewSettingNumeric("Check every (seconds):", 1, 300, config.Interval,
            (s, e) => { if (loading) return; config.Interval = (int)((AeroStepper)s).Value; SaveConfig(); }, out intervalNum));
        secMon.Controls.Add(NewSettingNumeric("Restart cooldown (seconds):", 5, 600, config.Cooldown,
            (s, e) => { if (loading) return; config.Cooldown = (int)((AeroStepper)s).Value; SaveConfig(); }, out cooldownNum));
        desktopCb = NewSettingCheckbox("Desktop Mode - launch VRChat & join instances without VR", config.DesktopMode, (s, e) =>
        {
            if (loading) return;
            config.DesktopMode = desktopCb.Checked;
            SaveConfig();
            WriteLog(desktopCb.Checked ? "Launch mode: Desktop (applies to all launches & instance joins)." : "Launch mode: VR.");
        });
        secMon.Controls.Add(desktopCb);
        crashCb = NewSettingCheckbox("Crash-loop protection (stop + alert after too many restarts)", config.CrashLoop.Enabled,
            (s, e) => { if (loading) return; config.CrashLoop.Enabled = crashCb.Checked; SaveConfig(); });
        secMon.Controls.Add(crashCb);
        secMon.Controls.Add(NewSettingNumeric("     Max restarts:", 2, 50, config.CrashLoop.MaxCrashes,
            (s, e) => { if (loading) return; config.CrashLoop.MaxCrashes = (int)((AeroStepper)s).Value; SaveConfig(); }, out crashMaxNum));
        secMon.Controls.Add(NewSettingNumeric("     Within (minutes):", 1, 120, config.CrashLoop.WindowMin,
            (s, e) => { if (loading) return; config.CrashLoop.WindowMin = (int)((AeroStepper)s).Value; SaveConfig(); }, out crashWinNum));
        schedCb = NewSettingCheckbox("Restart VRChat on a schedule (clears memory leaks in long sessions)", config.ScheduledRestart.Enabled,
            (s, e) => { if (loading) return; config.ScheduledRestart.Enabled = schedCb.Checked; SaveConfig(); });
        secMon.Controls.Add(schedCb);
        secMon.Controls.Add(NewSettingNumeric("     Every (hours):", 1, 24, config.ScheduledRestart.IntervalHours,
            (s, e) => { if (loading) return; config.ScheduledRestart.IntervalHours = (int)((AeroStepper)s).Value; SaveConfig(); }, out schedHrsNum));
        freezeCb = NewSettingCheckbox("Freeze / hang detection (soft-restart VRChat when it stops responding)", config.FreezeDetect.Enabled,
            (s, e) => { if (loading) return; config.FreezeDetect.Enabled = freezeCb.Checked; SaveConfig(); });
        secMon.Controls.Add(freezeCb);
        secMon.Controls.Add(NewSettingNumeric("     Window Not Responding for (seconds):", 10, 300, config.FreezeDetect.HangSec,
            (s, e) => { if (loading) return; config.FreezeDetect.HangSec = (int)((AeroStepper)s).Value; SaveConfig(); }, out freezeHangNum));
        secMon.Controls.Add(NewSettingNumeric("     Or game log silent for (minutes):", 2, 60, config.FreezeDetect.LogStallMin,
            (s, e) => { if (loading) return; config.FreezeDetect.LogStallMin = (int)((AeroStepper)s).Value; SaveConfig(); }, out freezeStallNum));
        ramCb = NewSettingCheckbox("Restart VRChat when its RAM use passes a limit (memory-leak guard)", config.RamLimit.Enabled,
            (s, e) => { if (loading) return; config.RamLimit.Enabled = ramCb.Checked; SaveConfig(); });
        secMon.Controls.Add(ramCb);
        secMon.Controls.Add(NewSettingNumeric("     RAM limit (GB):", 4, 64, config.RamLimit.MaxGB,
            (s, e) => { if (loading) return; config.RamLimit.MaxGB = (int)((AeroStepper)s).Value; SaveConfig(); }, out ramGBNum));
        fpsCb = NewSettingCheckbox("Restart VRChat if FPS stays low (needs an OSC FPS source, e.g. an avatar FPS counter)", config.FpsGuard.Enabled,
            (s, e) => { if (loading) return; config.FpsGuard.Enabled = fpsCb.Checked; SaveConfig(); InitializeOscListener(); });
        secMon.Controls.Add(fpsCb);
        secMon.Controls.Add(NewSettingNumeric("     Below (FPS):", 10, 120, config.FpsGuard.MinFps,
            (s, e) => { if (loading) return; config.FpsGuard.MinFps = (int)((AeroStepper)s).Value; SaveConfig(); }, out fpsMinNum));
        secMon.Controls.Add(NewSettingNumeric("     For (minutes):", 1, 30, config.FpsGuard.HoldMin,
            (s, e) => { if (loading) return; config.FpsGuard.HoldMin = (int)((AeroStepper)s).Value; SaveConfig(); }, out fpsHoldNum));
        secMon.Controls.Add(NewSettingNumeric("     OSC listen port:", 1024, 65535, config.FpsGuard.OscPort,
            (s, e) => { if (loading) return; config.FpsGuard.OscPort = (int)((AeroStepper)s).Value; SaveConfig(); InitializeOscListener(); }, out fpsPortNum));
        secMon.Controls.Add(NewSettingText("     OSC address:", config.FpsGuard.Address, 260,
            (s, e) => { if (loading) return; config.FpsGuard.Address = ((TextBox)s).Text.Trim(); SaveConfig(); }, out fpsAddrBox));
        frameLogCb = NewSettingCheckbox("     Also log frame times to CSV (AeroVRC data folder \\ FrameLogs)", config.FrameLog.Enabled,
            (s, e) => { if (loading) return; config.FrameLog.Enabled = frameLogCb.Checked; SaveConfig(); if (!frameLogCb.Checked) FlushFrameLog(); });
        secMon.Controls.Add(frameLogCb);
        crashHintsCb = NewSettingCheckbox("Show a crash-cause hint from the log when VRChat exits", config.CrashHints.Enabled,
            (s, e) => { if (loading) return; config.CrashHints.Enabled = crashHintsCb.Checked; SaveConfig(); });
        secMon.Controls.Add(crashHintsCb);
        crashArchCb = NewSettingCheckbox("Archive the VRChat log tail whenever VRChat exits (crash history for bug reports)", config.CrashArchive.Enabled,
            (s, e) => { if (loading) return; config.CrashArchive.Enabled = crashArchCb.Checked; SaveConfig(); });
        secMon.Controls.Add(crashArchCb);
        var openCrashBtn = new Button
        {
            Text = "Open crash log archive", AutoSize = true,
            Padding = new Padding(10, 4, 10, 4), Margin = new Padding(0, 4, 0, 2),
        };
        Ui.StyleButton(openCrashBtn, "secondary");
        openCrashBtn.Click += (s, e) =>
        {
            Directory.CreateDirectory(crashLogDir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(crashLogDir) { UseShellExecute = true });
        };
        secMon.Controls.Add(openCrashBtn);

        // ---- Alerts section ----
        var secAlert = NewSettingsSection("Alerts");
        soundCb = NewSettingCheckbox("Play a sound when VRChat crashes / is relaunched", config.SoundAlert,
            (s, e) => { if (loading) return; config.SoundAlert = soundCb.Checked; SaveConfig(); });
        secAlert.Controls.Add(soundCb);
        breakCb = NewSettingCheckbox("Break reminder after a set time in VRChat", config.BreakReminder.Enabled,
            (s, e) => { if (loading) return; config.BreakReminder.Enabled = breakCb.Checked; SaveConfig(); });
        secAlert.Controls.Add(breakCb);
        secAlert.Controls.Add(NewSettingNumeric("     Remind after (hours):", 1, 24, config.BreakReminder.Hours,
            (s, e) => { if (loading) return; config.BreakReminder.Hours = (int)((AeroStepper)s).Value; SaveConfig(); }, out breakHrsNum));
        breakCloseCb = NewSettingCheckbox("     Also close VRChat when the reminder fires", config.BreakReminder.CloseVRChat,
            (s, e) => { if (loading) return; config.BreakReminder.CloseVRChat = breakCloseCb.Checked; SaveConfig(); });
        secAlert.Controls.Add(breakCloseCb);
        diskCb = NewSettingCheckbox("Warn when disk space runs low", config.DiskMonitor.Enabled,
            (s, e) => { if (loading) return; config.DiskMonitor.Enabled = diskCb.Checked; SaveConfig(); });
        secAlert.Controls.Add(diskCb);
        secAlert.Controls.Add(NewSettingNumeric("     Warn below (GB free):", 1, 200, config.DiskMonitor.MinGB,
            (s, e) => { if (loading) return; config.DiskMonitor.MinGB = (int)((AeroStepper)s).Value; SaveConfig(); }, out diskGBNum));
        crasherCb = NewSettingCheckbox("Alert on suspected join-spam / crasher activity", config.CrasherAlert.Enabled,
            (s, e) => { if (loading) return; config.CrasherAlert.Enabled = crasherCb.Checked; SaveConfig(); });
        secAlert.Controls.Add(crasherCb);
        secAlert.Controls.Add(NewSettingNumeric("     Join/leave events per 10s:", 4, 60, config.CrasherAlert.ChurnPer10s,
            (s, e) => { if (loading) return; config.CrasherAlert.ChurnPer10s = (int)((AeroStepper)s).Value; SaveConfig(); }, out crasherNum));
        pingMonCb = NewSettingCheckbox("Track connection health to VRChat's servers (Ping / Connection cards on the Dashboard)", config.PingMonitor.Enabled,
            (s, e) => { if (loading) return; config.PingMonitor.Enabled = pingMonCb.Checked; SaveConfig(); });
        secAlert.Controls.Add(pingMonCb);
        pingWarnCb = NewSettingCheckbox("     Warn when a disconnect looks likely (packet loss / latency spikes)", config.PingMonitor.Warn,
            (s, e) => { if (loading) return; config.PingMonitor.Warn = pingWarnCb.Checked; SaveConfig(); });
        secAlert.Controls.Add(pingWarnCb);
        secAlert.Controls.Add(NewSettingText("     Ping host:", config.PingMonitor.Host, 200,
            (s, e) => { if (loading) return; config.PingMonitor.Host = ((TextBox)s).Text.Trim(); SaveConfig(); }, out pingHostBox));

        // ---- Hardware monitor section ----
        var secHw = NewSettingsSection("Hardware monitor (GPU / VRAM)", "Hardware Monitoring");
        gpuMonCb = NewSettingCheckbox("Monitor GPU load, VRAM & GPU temperature (shown on the Dashboard)", config.GpuMonitor.Enabled, (s, e) =>
        {
            if (loading) return;
            config.GpuMonitor.Enabled = gpuMonCb.Checked;
            SaveConfig();
            InitializeHwMonitor();
            RefreshHwStatus();
        });
        secHw.Controls.Add(gpuMonCb);
        vramAlertCb = NewSettingCheckbox("     Warn when VRAM use gets critically high (risk of stutter / crashes)", config.GpuMonitor.VramAlert,
            (s, e) => { if (loading) return; config.GpuMonitor.VramAlert = vramAlertCb.Checked; SaveConfig(); });
        secHw.Controls.Add(vramAlertCb);
        secHw.Controls.Add(NewSettingNumeric("     Warn at (% of VRAM used):", 50, 100, config.GpuMonitor.VramPct,
            (s, e) => { if (loading) return; config.GpuMonitor.VramPct = (int)((AeroStepper)s).Value; SaveConfig(); }, out vramPctNum));
        tempWarnCb = NewSettingCheckbox("Overheat warning (GPU / CPU temperature)", config.TempWarn.Enabled,
            (s, e) => { if (loading) return; config.TempWarn.Enabled = tempWarnCb.Checked; SaveConfig(); });
        secHw.Controls.Add(tempWarnCb);
        secHw.Controls.Add(NewSettingNumeric("     GPU warn at (deg C):", 60, 110, config.TempWarn.GpuMaxC,
            (s, e) => { if (loading) return; config.TempWarn.GpuMaxC = (int)((AeroStepper)s).Value; SaveConfig(); }, out gpuMaxCNum));
        secHw.Controls.Add(NewSettingNumeric("     CPU warn at (deg C):", 60, 110, config.TempWarn.CpuMaxC,
            (s, e) => { if (loading) return; config.TempWarn.CpuMaxC = (int)((AeroStepper)s).Value; SaveConfig(); }, out cpuMaxCNum));
        secHw.Controls.Add(new Label
        {
            Name = "onCardMuted", AutoSize = true, Font = Ui.FontMuted,
            Text = "GPU temperature works with either source. CPU temperature needs the LibreHardwareMonitor library AND running AeroVRC as administrator.",
            Margin = new Padding(0, 2, 0, 0),
        });
        hwStatusLbl = new Label
        {
            Name = "onCardMuted", AutoSize = true, Font = Ui.FontMuted,
            Margin = new Padding(0, 8, 0, 2),
        };
        secHw.Controls.Add(hwStatusLbl);
        hwDlBtn = new Button
        {
            Text = "Download LibreHardwareMonitor library", AutoSize = true,
            Padding = new Padding(10, 4, 10, 4), Margin = new Padding(0, 4, 0, 2),
        };
        Ui.StyleButton(hwDlBtn, "secondary");
        secHw.Controls.Add(hwDlBtn);
        RefreshHwStatus();
        hwDlBtn.Click += (s, e) => DownloadLhmLibrary();

        // ---- Automation section ----
        var secAuto = NewSettingsSection("Automation");
        autoCloseCb = NewSettingCheckbox("Close companion apps when VRChat exits (exe-based apps)", config.AutoCloseCompanions,
            (s, e) => { if (loading) return; config.AutoCloseCompanions = autoCloseCb.Checked; SaveConfig(); });
        secAuto.Controls.Add(autoCloseCb);
        rejoinCb = NewSettingCheckbox("Auto-rejoin last instance on disconnect (experimental)", config.AutoRejoin,
            (s, e) => { if (loading) return; config.AutoRejoin = rejoinCb.Checked; SaveConfig(); });
        secAuto.Controls.Add(rejoinCb);
        rejoinRestartCb = NewSettingCheckbox("Rejoin the last instance when the watchdog restarts VRChat (after a crash/exit)", config.RejoinOnRestart.Enabled,
            (s, e) => { if (loading) return; config.RejoinOnRestart.Enabled = rejoinRestartCb.Checked; SaveConfig(); });
        secAuto.Controls.Add(rejoinRestartCb);
        autoBmCb = NewSettingCheckbox("Auto-bookmark worlds after spending a while in them", config.AutoBookmark.Enabled,
            (s, e) => { if (loading) return; config.AutoBookmark.Enabled = autoBmCb.Checked; SaveConfig(); });
        secAuto.Controls.Add(autoBmCb);
        secAuto.Controls.Add(NewSettingNumeric("     After (hours):", 1, 12, config.AutoBookmark.Hours,
            (s, e) => { if (loading) return; config.AutoBookmark.Hours = (int)((AeroStepper)s).Value; SaveConfig(); }, out autoBmHrsNum));
        steamvrCb = NewSettingCheckbox("Launch SteamVR when monitoring starts (skipped in Desktop Mode)", config.SteamVRAutoLaunch,
            (s, e) => { if (loading) return; config.SteamVRAutoLaunch = steamvrCb.Checked; SaveConfig(); });
        secAuto.Controls.Add(steamvrCb);
        cacheCb = NewSettingCheckbox("Auto-clear VRChat cache periodically (only while VRChat is closed)", config.AutoClearCache.Enabled,
            (s, e) => { if (loading) return; config.AutoClearCache.Enabled = cacheCb.Checked; SaveConfig(); });
        secAuto.Controls.Add(cacheCb);
        secAuto.Controls.Add(NewSettingNumeric("     Every (days):", 1, 60, config.AutoClearCache.IntervalDays,
            (s, e) => { if (loading) return; config.AutoClearCache.IntervalDays = (int)((AeroStepper)s).Value; SaveConfig(); }, out cacheDaysNum));
        var cacheNowBtn = new Button
        {
            Text = "Clear cache now", AutoSize = true,
            Padding = new Padding(10, 4, 10, 4), Margin = new Padding(0, 6, 0, 2),
        };
        Ui.StyleButton(cacheNowBtn, "secondary");
        cacheNowBtn.Click += (s, e) =>
        {
            if (ClearVRChatCache())
                MessageBox.Show("VRChat cache cleared.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        secAuto.Controls.Add(cacheNowBtn);

        // ---- Goals section ----
        var secGoals = NewSettingsSection("Playtime goals", "Monitoring");
        secGoals.Controls.Add(new Label
        {
            Name = "onCardMuted", AutoSize = true, Font = Ui.FontMuted,
            Text = "Set a daily target to build a streak (Statistics page). The daily goal also draws a line on the playtime chart.",
            Margin = new Padding(0, 0, 0, 4),
        });
        secGoals.Controls.Add(NewSettingNumeric("Daily goal (minutes):", 0, 1440, config.Goals.DailyMin,
            (s, e) => { if (loading) return; config.Goals.DailyMin = (int)((AeroStepper)s).Value; SaveConfig(); try { UpdateStatsPage(); } catch { } }, out goalDailyNum));
        secGoals.Controls.Add(NewSettingNumeric("Weekly goal (hours):", 0, 168, config.Goals.WeeklyHours,
            (s, e) => { if (loading) return; config.Goals.WeeklyHours = (int)((AeroStepper)s).Value; SaveConfig(); try { UpdateStatsPage(); } catch { } }, out goalWeeklyNum));

        // ---- Scheduled launch section ----
        var secSchedLaunch = NewSettingsSection("Scheduled launch", "Scheduled Launch");
        schedLaunchCb = NewSettingCheckbox("Start VRChat (and auto-launch apps) at a set time each day", config.ScheduledLaunch.Enabled,
            (s, e) => { if (loading) return; config.ScheduledLaunch.Enabled = schedLaunchCb.Checked; SaveConfig(); });
        secSchedLaunch.Controls.Add(schedLaunchCb);
        secSchedLaunch.Controls.Add(NewSettingText("     Time (HH:mm, 24h):", config.ScheduledLaunch.Time, 80,
            (s, e) => { if (loading) return; config.ScheduledLaunch.Time = ((TextBox)s).Text.Trim(); SaveConfig(); }, out schedLaunchTimeBox));
        schedLaunchMonCb = NewSettingCheckbox("     Also start monitoring (auto-relaunch if it crashes)", config.ScheduledLaunch.StartMonitoring,
            (s, e) => { if (loading) return; config.ScheduledLaunch.StartMonitoring = schedLaunchMonCb.Checked; SaveConfig(); });
        secSchedLaunch.Controls.Add(schedLaunchMonCb);

        // ---- Performance Mode section ----
        var secPerf = NewSettingsSection("Performance Mode", "Performance");
        secPerf.Controls.Add(new Label
        {
            Name = "onCardMuted", AutoSize = true, Font = Ui.FontMuted,
            Text = "Apps to close when the Performance quick-toggle (Dashboard) is switched on - one process name per line," + Environment.NewLine +
                   "e.g. chrome, Discord, obs64, Spotify. They're reopened when you switch it off. VRChat is never closed.",
            Margin = new Padding(0, 0, 0, 4),
        });
        perfListBox = new TextBox
        {
            Multiline = true, ScrollBars = ScrollBars.Vertical,
            Size = new Size(520, 76), Font = Ui.FontBody,
            BackColor = Ui.InputBg, ForeColor = Ui.Text,
            Text = string.Join("\r\n", config.PerfMode.Apps),
            Margin = new Padding(0, 4, 0, 2),
        };
        perfListBox.TextChanged += (s, e) =>
        {
            if (loading) return;
            config.PerfMode.Apps = perfListBox.Lines.Where(l => l.Trim().Length > 0).Select(l => l.Trim()).ToList();
            SaveConfig();
        };
        secPerf.Controls.Add(perfListBox);

        // ---- Bedtime section ----
        var secBed = NewSettingsSection("Bedtime / Sleep timer", "Bedtime/Sleep");
        bedCb = NewSettingCheckbox("Enable bedtime (close VRChat and/or sleep the PC at a set time)", config.Bedtime.Enabled,
            (s, e) => { if (loading) return; config.Bedtime.Enabled = bedCb.Checked; SaveConfig(); });
        secBed.Controls.Add(bedCb);
        secBed.Controls.Add(NewSettingText("     Time (HH:mm, 24h):", config.Bedtime.Time, 80,
            (s, e) => { if (loading) return; config.Bedtime.Time = ((TextBox)s).Text.Trim(); SaveConfig(); }, out bedTimeBox));
        secBed.Controls.Add(NewSettingCombo("     Action:", new[] { "Sleep", "Hibernate", "Shutdown", "Close only" }, config.Bedtime.Action,
            (s, e) => { if (loading) return; config.Bedtime.Action = (string)((ComboBox)s).SelectedItem; SaveConfig(); }, out bedActCombo));
        bedCloseCb = NewSettingCheckbox("     Close VRChat first (recommended)", config.Bedtime.CloseVRChat,
            (s, e) => { if (loading) return; config.Bedtime.CloseVRChat = bedCloseCb.Checked; SaveConfig(); });
        secBed.Controls.Add(bedCloseCb);
        secBed.Controls.Add(NewSettingNumeric("     Warn this many minutes before:", 0, 30, config.Bedtime.WarnMin,
            (s, e) => { if (loading) return; config.Bedtime.WarnMin = (int)((AeroStepper)s).Value; SaveConfig(); }, out bedWarnNum));
        bedAppsCb = NewSettingCheckbox("     Also close companion apps at bedtime", config.Bedtime.CloseApps,
            (s, e) => { if (loading) return; config.Bedtime.CloseApps = bedAppsCb.Checked; SaveConfig(); });
        secBed.Controls.Add(bedAppsCb);
        bedWindCb = NewSettingCheckbox("     Wind-down: block VRChat from reopening until morning", config.Bedtime.WindDown,
            (s, e) => { if (loading) return; config.Bedtime.WindDown = bedWindCb.Checked; SaveConfig(); });
        secBed.Controls.Add(bedWindCb);
        secBed.Controls.Add(NewSettingText("          Wind-down ends at (HH:mm):", config.Bedtime.WindDownEnd, 80,
            (s, e) => { if (loading) return; config.Bedtime.WindDownEnd = ((TextBox)s).Text.Trim(); SaveConfig(); }, out bedWindEndBox));
        // Bedtime media (mp3 / YouTube)
        bedMediaCb = NewSettingCheckbox("     Play media at bedtime", config.BedtimeMedia.Enabled,
            (s, e) => { if (loading) return; config.BedtimeMedia.Enabled = bedMediaCb.Checked; SaveConfig(); });
        secBed.Controls.Add(bedMediaCb);
        secBed.Controls.Add(NewSettingCombo("          Media type:", new[] { "mp3", "youtube" }, config.BedtimeMedia.Type,
            (s, e) => { if (loading) return; config.BedtimeMedia.Type = (string)((ComboBox)s).SelectedItem; SaveConfig(); }, out bedMediaTypeCombo));
        secBed.Controls.Add(NewSettingText("          YouTube URL:", config.BedtimeMedia.Url, 320,
            (s, e) => { if (loading) return; config.BedtimeMedia.Url = ((TextBox)s).Text.Trim(); SaveConfig(); }, out bedMediaUrlBox));
        var bedMusicBtn = new Button
        {
            Text = "Open bedtime music folder (mp3)", AutoSize = true,
            Padding = new Padding(10, 4, 10, 4), Margin = new Padding(0, 4, 0, 2),
        };
        Ui.StyleButton(bedMusicBtn, "secondary");
        bedMusicBtn.Click += (s, e) =>
        {
            Directory.CreateDirectory(bedtimeMusicDir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(bedtimeMusicDir) { UseShellExecute = true });
        };
        secBed.Controls.Add(bedMusicBtn);
        secBed.Controls.Add(new Label
        {
            Name = "onCardMuted", Font = Ui.FontMuted, AutoSize = true,
            Text = "Fires once per day within 15 min of the set time. Sleep/Hibernate use Windows power; Shutdown gives a 30s cancel window." + Environment.NewLine +
                   "For mp3, drop songs in the music folder above - a random one plays. Wind-down/media work best with Action = Close only.",
        });

        // ---- Scheduled join section ----
        var secSchedJoin = NewSettingsSection("Scheduled auto-join (event reminder)", "Worlds");
        schedJoinCb = NewSettingCheckbox("Join a saved instance at a set time each day", config.ScheduledJoin.Enabled,
            (s, e) => { if (loading) return; config.ScheduledJoin.Enabled = schedJoinCb.Checked; SaveConfig(); });
        secSchedJoin.Controls.Add(schedJoinCb);
        secSchedJoin.Controls.Add(NewSettingText("     Time (HH:mm, 24h):", config.ScheduledJoin.Time, 80,
            (s, e) => { if (loading) return; config.ScheduledJoin.Time = ((TextBox)s).Text.Trim(); SaveConfig(); }, out schedJoinTimeBox));
        secSchedJoin.Controls.Add(NewSettingText("     Instance link / id:", config.ScheduledJoin.Target, 340,
            (s, e) => { if (loading) return; config.ScheduledJoin.Target = ((TextBox)s).Text.Trim(); SaveConfig(); }, out schedJoinTargetBox));
        secSchedJoin.Controls.Add(NewSettingText("     Label (optional):", config.ScheduledJoin.Name, 200,
            (s, e) => { if (loading) return; config.ScheduledJoin.Name = ((TextBox)s).Text.Trim(); SaveConfig(); }, out schedJoinNameBox));
        var schedJoinUseBtn = new Button
        {
            Text = "Use current instance", AutoSize = true,
            Padding = new Padding(10, 4, 10, 4), Margin = new Padding(0, 4, 0, 2),
        };
        Ui.StyleButton(schedJoinUseBtn, "secondary");
        schedJoinUseBtn.Click += (s, e) =>
        {
            if (string.IsNullOrEmpty(currentInstanceId))
            {
                MessageBox.Show("Join a world first to capture its instance.", "No instance", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            config.ScheduledJoin.Target = currentInstanceId;
            config.ScheduledJoin.Name = currentWorld ?? "";
            SaveConfig();
            schedJoinTargetBox.Text = currentInstanceId;
            schedJoinNameBox.Text = config.ScheduledJoin.Name;
        };
        secSchedJoin.Controls.Add(schedJoinUseBtn);

        // (Home world settings removed - the Dashboard's "Take Me Home" button now
        //  sets your home from the current instance on first use.)

        // ---- World block list section ----
        var secBlock = NewSettingsSection("World block list", "World Block List");
        blockCb = NewSettingCheckbox("Alert when I join a blocked world", config.WorldBlock.Enabled,
            (s, e) => { if (loading) return; config.WorldBlock.Enabled = blockCb.Checked; SaveConfig(); });
        secBlock.Controls.Add(blockCb);
        blockLeaveCb = NewSettingCheckbox("     Auto-leave: close VRChat when a blocked world is joined (monitor relaunches to home)", config.WorldBlock.AutoLeave,
            (s, e) => { if (loading) return; config.WorldBlock.AutoLeave = blockLeaveCb.Checked; SaveConfig(); });
        secBlock.Controls.Add(blockLeaveCb);
        blockListBox = new TextBox
        {
            Multiline = true, ScrollBars = ScrollBars.Vertical,
            Size = new Size(520, 76), Font = Ui.FontBody,
            BackColor = Ui.InputBg, ForeColor = Ui.Text,
            Text = string.Join("\r\n", config.WorldBlockList),
            Margin = new Padding(0, 4, 0, 2),
        };
        blockListBox.TextChanged += (s, e) =>
        {
            if (loading) return;
            config.WorldBlockList = blockListBox.Lines.Where(l => l.Trim().Length > 0).Select(l => l.Trim()).ToList();
            SaveConfig();
        };
        secBlock.Controls.Add(blockListBox);
        secBlock.Controls.Add(new Label
        {
            Name = "onCardMuted", Font = Ui.FontMuted, AutoSize = true,
            Text = "One entry per line: a wrld_ id (exact) or part of a world name (partial match).",
        });

        // ---- Player watchlist section ----
        var secWatch = NewSettingsSection("Player watchlist", "Player Watchlist");
        watchCb = NewSettingCheckbox("Notify me when a watched player joins my instance", config.WatchlistNotify.Enabled,
            (s, e) => { if (loading) return; config.WatchlistNotify.Enabled = watchCb.Checked; SaveConfig(); });
        secWatch.Controls.Add(watchCb);
        watchSoundCb = NewSettingCheckbox("     Play a sound too", config.WatchlistNotify.Sound,
            (s, e) => { if (loading) return; config.WatchlistNotify.Sound = watchSoundCb.Checked; SaveConfig(); });
        secWatch.Controls.Add(watchSoundCb);
        secWatch.Controls.Add(new Label
        {
            Name = "onCardMuted", AutoSize = true, Font = Ui.FontMuted,
            Text = "One player per line. Optional groups: put a [Group name] line above its members - e.g. [VR friends], [Work], [Squad]." + Environment.NewLine +
                   "The group name is shown in the join notification.",
            Margin = new Padding(0, 4, 0, 0),
        });
        watchListBox = new TextBox
        {
            Multiline = true, ScrollBars = ScrollBars.Vertical,
            Size = new Size(520, 96), Font = Ui.FontBody,
            BackColor = Ui.InputBg, ForeColor = Ui.Text,
            Text = GetWatchlistText(),
            Margin = new Padding(0, 4, 0, 2),
        };
        watchListBox.TextChanged += (s, e) =>
        {
            if (loading) return;
            var names = new List<string>();
            var groups = new Dictionary<string, string>();
            var curGroup = "";
            foreach (var ln in watchListBox.Lines)
            {
                var t = ln.Trim();
                if (t.Length == 0) continue;
                var m = System.Text.RegularExpressions.Regex.Match(t, @"^\[(.+)\]$");
                if (m.Success) { curGroup = m.Groups[1].Value.Trim(); continue; }
                names.Add(t);
                if (curGroup.Length > 0) groups[t] = curGroup;
            }
            config.Watchlist = names;
            config.WatchGroups = groups;
            SaveConfig();
        };
        secWatch.Controls.Add(watchListBox);
        secWatch.Controls.Add(new Label
        {
            Name = "onCardMuted", Font = Ui.FontMuted, AutoSize = true,
            Text = "One display name per line (exact, case-insensitive). You'll get a toast + sound when they appear in your instance.",
        });
        var watchTestBtn = new Button
        {
            Text = "Test toast", AutoSize = true,
            Padding = new Padding(10, 4, 10, 4), Margin = new Padding(0, 4, 0, 2),
        };
        Ui.StyleButton(watchTestBtn, "secondary");
        watchTestBtn.Click += (s, e) => ShowToast("Player joined", "ExampleUser is in your instance", Ui.Accent);
        secWatch.Controls.Add(watchTestBtn);

        // ---- Integrations section ----
        var secInt = NewSettingsSection("Integrations");
        oscCb = NewSettingCheckbox("OSC chatbox status (posts uptime/world to your in-game chatbox)", config.OscChatbox.Enabled,
            (s, e) => { if (loading) return; config.OscChatbox.Enabled = oscCb.Checked; SaveConfig(); });
        secInt.Controls.Add(oscCb);
        secInt.Controls.Add(NewSettingNumeric("     Update every (seconds):", 5, 120, config.OscChatbox.IntervalSec,
            (s, e) => { if (loading) return; config.OscChatbox.IntervalSec = (int)((AeroStepper)s).Value; SaveConfig(); }, out oscIntNum));
        secInt.Controls.Add(NewSettingText("     Message template:", config.OscChatbox.Template, 380,
            (s, e) => { if (loading) return; config.OscChatbox.Template = ((TextBox)s).Text; SaveConfig(); }, out oscTplBox));
        secInt.Controls.Add(new Label
        {
            Name = "onCardMuted", Font = Ui.FontMuted, AutoSize = true,
            Text = "Tokens: {world} {instance} {uptime} {players} {restarts} {cpu} {ram} {time} {date}",
        });
        secInt.Controls.Add(new Label
        {
            Name = "onCardMuted", Font = Ui.FontMuted, AutoSize = true,
            Text = "Requires OSC enabled in VRChat (Action Menu > Options > OSC > Enabled).",
            Margin = new Padding(0, 0, 0, 6),
        });
        discordCb = NewSettingCheckbox("Discord Rich Presence", config.DiscordRP.Enabled, (s, e) =>
        {
            if (loading) return;
            config.DiscordRP.Enabled = discordCb.Checked;
            SaveConfig();
            if (!discordCb.Checked) DisconnectDiscordRP();
        });
        secInt.Controls.Add(discordCb);
        var rowDiscId = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Color.Transparent };
        rowDiscId.Controls.Add(new Label
        {
            Name = "onCard", Text = "     Application Client ID:", AutoSize = true,
            Margin = new Padding(0, 6, 8, 0),
        });
        discIdBox = new TextBox { Width = 240, Text = config.DiscordRP.ClientId, BackColor = Ui.InputBg, ForeColor = Ui.Text };
        discIdBox.TextChanged += (s, e) => { if (loading) return; config.DiscordRP.ClientId = discIdBox.Text.Trim(); SaveConfig(); };
        rowDiscId.Controls.Add(discIdBox);
        secInt.Controls.Add(rowDiscId);
        secInt.Controls.Add(new Label
        {
            Name = "onCardMuted", Font = Ui.FontMuted, AutoSize = true,
            Text = "Create a free app at discord.com/developers to get a Client ID.",
        });
        vrcxCb = NewSettingCheckbox("VRCX integration (read your VRChat history on the VRCX tab)", config.Vrcx.Enabled, (s, e) =>
        {
            if (loading) return;
            config.Vrcx.Enabled = vrcxCb.Checked;
            SaveConfig();
            try { UpdateVrcxPage(); } catch { }
        });
        secInt.Controls.Add(vrcxCb);
        secInt.Controls.Add(NewSettingNumeric("     Refresh every (seconds):", 5, 300, config.Vrcx.RefreshSec,
            (s, e) => { if (loading) return; config.Vrcx.RefreshSec = (int)((AeroStepper)s).Value; SaveConfig(); }, out vrcxIntNum));
        secInt.Controls.Add(NewSettingText("     Database path (blank = auto-detect):", config.Vrcx.DbPath, 380,
            (s, e) => { if (loading) return; config.Vrcx.DbPath = ((TextBox)s).Text.Trim(); SaveConfig(); }, out vrcxPathBox));
        secInt.Controls.Add(new Label
        {
            Name = "onCardMuted", Font = Ui.FontMuted, AutoSize = true,
            Text = @"Reads VRCX's SQLite DB (default %APPDATA%\VRCX\VRCX.sqlite3), read-only. Uses Windows' built-in SQLite - nothing to install.",
        });

        // ---- Appearance section ----
        var secApp = NewSettingsSection("Appearance");
        sparkleCb = NewSettingCheckbox("Animated background sparkles", config.Effects.Sparkles, (s, e) =>
        {
            if (loading) return;
            config.Effects.Sparkles = sparkleCb.Checked;
            SaveConfig();
            if (pages.TryGetValue(currentPage, out var pg)) pg.Invalidate();
            WriteLog(sparkleCb.Checked ? "Background sparkles enabled." : "Background sparkles disabled.");
        });
        secApp.Controls.Add(sparkleCb);
        var sparkStyleDisplay = new (string Key, string Display)[]
        {
            ("azure", "Azure"), ("snow", "Snow"), ("embers", "Embers"), ("stars", "Stars"), ("sakura", "Sakura"),
        };
        var curStyleKey = config.Effects.Style;
        if (!sparkStyleDisplay.Any(p => p.Key == curStyleKey)) curStyleKey = "azure";
        var curDisplay = sparkStyleDisplay.First(p => p.Key == curStyleKey).Display;
        secApp.Controls.Add(NewSettingCombo("     Sparkle style:", sparkStyleDisplay.Select(p => p.Display).ToArray(), curDisplay, (s, e) =>
        {
            if (loading) return;
            var sel = (string)((ComboBox)s).SelectedItem;
            foreach (var (key, display) in sparkStyleDisplay)
            {
                if (display == sel) { config.Effects.Style = key; break; }
            }
            SaveConfig();
            InitPageParticles();
            if (pages.TryGetValue(currentPage, out var pg)) pg.Invalidate();
            WriteLog($"Sparkle style: {sel}.");
        }, out sparkStyleCombo));
        logoAnimCb = NewSettingCheckbox("Animate the logo (only while the window is focused)", config.Effects.LogoAnim, (s, e) =>
        {
            if (loading) return;
            config.Effects.LogoAnim = logoAnimCb.Checked;
            SaveConfig();
            navLogo?.Invalidate();
        });
        secApp.Controls.Add(logoAnimCb);
        welcomeCb = NewSettingCheckbox("Show the welcome screen on startup", config.ShowWelcome,
            (s, e) => { if (loading) return; config.ShowWelcome = welcomeCb.Checked; SaveConfig(); });
        secApp.Controls.Add(welcomeCb);
        startupSoundCb = NewSettingCheckbox("Play a startup sound", config.StartupSound,
            (s, e) => { if (loading) return; config.StartupSound = startupSoundCb.Checked; SaveConfig(); });
        secApp.Controls.Add(startupSoundCb);
        secApp.Controls.Add(new Label
        {
            Name = "onCardMuted", Font = Ui.FontMuted, AutoSize = true,
            Text = "Startup sound: drop a startup.wav or startup.mp3 in %APPDATA%\\AeroVRC to use your own.",
        });
        secApp.Controls.Add(new Label
        {
            Name = "onCardMuted", Font = Ui.FontMuted, AutoSize = true,
            Text = "Turn sparkles off to reduce CPU/GPU use on low-end machines.",
        });
    }

    // Serializes the watchlist back to text: ungrouped names first, then each
    // group as a [Header] followed by its members.
    internal string GetWatchlistText()
    {
        var lines = new List<string>();
        foreach (var n in config.Watchlist)
        {
            config.WatchGroups.TryGetValue(n, out var g);
            if (string.IsNullOrEmpty(g)) lines.Add(n);
        }
        var groups = config.WatchGroups.Values.Where(g => !string.IsNullOrEmpty(g)).Distinct().OrderBy(g => g).ToList();
        foreach (var g in groups)
        {
            lines.Add($"[{g}]");
            foreach (var n in config.Watchlist)
            {
                if (config.WatchGroups.TryGetValue(n, out var ng) && ng == g) lines.Add(n);
            }
        }
        return string.Join("\r\n", lines);
    }

    void RefreshHwStatus()
    {
        hwStatusLbl.Text = hwSource switch
        {
            "LibreHardwareMonitor" => $"Source: LibreHardwareMonitor - {GetHwGpuName()}",
            "nvidia-smi" => "Source: nvidia-smi (NVIDIA). The LibreHardwareMonitor library adds AMD/Intel support + CPU temperature.",
            _ => config.GpuMonitor.Enabled
                ? "No GPU data source found. Download the LibreHardwareMonitor library below to enable monitoring."
                : "Monitoring disabled.",
        };
        hwDlBtn.Visible = hwSource != "LibreHardwareMonitor";
    }
    string GetHwGpuName()
    {
        try { return hwGpu != null ? (string)((dynamic)hwGpu).Name : "no GPU found"; }
        catch { return "GPU"; }
    }

    void DownloadLhmLibrary()
    {
        var r = MessageBox.Show(
            "Download the LibreHardwareMonitor library (~2 MB) from its official GitHub releases into AeroVRC's data folder?\n\nIt enables GPU load / VRAM / temperature monitoring for all GPU brands (no admin rights needed for GPU sensors).",
            "Download library", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (r != DialogResult.Yes) return;
        Cursor = Cursors.WaitCursor;
        try
        {
            var zip = Path.Combine(Path.GetTempPath(), "aerovrc_lhm.zip");
            var tmp = Path.Combine(Path.GetTempPath(), "aerovrc_lhm");
            const string url = "https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases/download/v0.9.4/LibreHardwareMonitor-net472.zip";
            using (var http = new HttpClient())
            {
                var bytes = http.GetByteArrayAsync(url).GetAwaiter().GetResult();
                File.WriteAllBytes(zip, bytes);
            }
            if (Directory.Exists(tmp)) { try { Directory.Delete(tmp, true); } catch { } }
            System.IO.Compression.ZipFile.ExtractToDirectory(zip, tmp, true);
            int copied = 0;
            foreach (var f in new[] { "LibreHardwareMonitorLib.dll", "HidSharp.dll" })
            {
                var src = Directory.EnumerateFiles(tmp, f, SearchOption.AllDirectories).FirstOrDefault();
                if (src != null)
                {
                    Directory.CreateDirectory(ConfigStore.ConfigDir);
                    File.Copy(src, Path.Combine(ConfigStore.ConfigDir, f), true);
                    copied++;
                }
            }
            try { File.Delete(zip); } catch { }
            try { Directory.Delete(tmp, true); } catch { }
            if (copied >= 1)
            {
                InitializeHwMonitor();
                RefreshHwStatus();
                WriteLog("HW monitor: LibreHardwareMonitor library installed.");
                MessageBox.Show("Library installed. GPU monitoring is now active.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("The download completed but the library DLL wasn't found in the archive.", "Hmm", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Download failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }
}
