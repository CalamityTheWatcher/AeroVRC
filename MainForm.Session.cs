using System.Diagnostics;

namespace AeroVRC;

// ============================================================================
//  APPLY CONFIG TO UI + SESSION/PLAYTIME TRACKING + STATUS + DASHBOARD REFRESH
//  + MONITORING TOGGLE
// ============================================================================

public partial class MainForm
{
    internal void ApplyConfigToUi()
    {
        loading = true;
        vdStreamerPath = config.VdStreamerPath;
        decimal Clamp(decimal min, decimal max, decimal v) => Math.Max(min, Math.Min(max, v));
        intervalNum.Value = Clamp(intervalNum.Minimum, intervalNum.Maximum, config.Interval);
        cooldownNum.Value = Clamp(cooldownNum.Minimum, cooldownNum.Maximum, config.Cooldown);
        desktopCb.Checked = config.DesktopMode;
        crashCb.Checked = config.CrashLoop.Enabled;
        crashMaxNum.Value = Clamp(2, 50, config.CrashLoop.MaxCrashes);
        crashWinNum.Value = Clamp(1, 120, config.CrashLoop.WindowMin);
        soundCb.Checked = config.SoundAlert;
        breakCb.Checked = config.BreakReminder.Enabled;
        breakHrsNum.Value = Clamp(1, 24, config.BreakReminder.Hours);
        breakCloseCb.Checked = config.BreakReminder.CloseVRChat;
        diskCb.Checked = config.DiskMonitor.Enabled;
        diskGBNum.Value = Clamp(1, 200, config.DiskMonitor.MinGB);
        autoCloseCb.Checked = config.AutoCloseCompanions;
        rejoinCb.Checked = config.AutoRejoin;
        rejoinRestartCb.Checked = config.RejoinOnRestart.Enabled;
        autoBmCb.Checked = config.AutoBookmark.Enabled;
        autoBmHrsNum.Value = Clamp(1, 12, config.AutoBookmark.Hours);
        steamvrCb.Checked = config.SteamVRAutoLaunch;
        cacheCb.Checked = config.AutoClearCache.Enabled;
        cacheDaysNum.Value = Clamp(1, 60, config.AutoClearCache.IntervalDays);
        oscCb.Checked = config.OscChatbox.Enabled;
        oscIntNum.Value = Clamp(5, 120, config.OscChatbox.IntervalSec);
        oscTplBox.Text = config.OscChatbox.Template;
        discordCb.Checked = config.DiscordRP.Enabled;
        discIdBox.Text = config.DiscordRP.ClientId;
        vrcxCb.Checked = config.Vrcx.Enabled;
        vrcxIntNum.Value = Clamp(5, 300, config.Vrcx.RefreshSec);
        vrcxPathBox.Text = config.Vrcx.DbPath;
        sparkleCb.Checked = config.Effects.Sparkles;
        var skey = config.Effects.Style;
        var styleDisplay = skey switch
        {
            "snow" => "Snow", "embers" => "Embers", "stars" => "Stars", "sakura" => "Sakura", _ => "Azure",
        };
        sparkStyleCombo.SelectedItem = styleDisplay;
        InitPageParticles();
        logoAnimCb.Checked = config.Effects.LogoAnim;
        welcomeCb.Checked = config.ShowWelcome;
        schedCb.Checked = config.ScheduledRestart.Enabled;
        schedHrsNum.Value = Clamp(1, 24, config.ScheduledRestart.IntervalHours);
        freezeCb.Checked = config.FreezeDetect.Enabled;
        freezeHangNum.Value = Clamp(10, 300, config.FreezeDetect.HangSec);
        freezeStallNum.Value = Clamp(2, 60, config.FreezeDetect.LogStallMin);
        ramCb.Checked = config.RamLimit.Enabled;
        ramGBNum.Value = Clamp(4, 64, config.RamLimit.MaxGB);
        gpuMonCb.Checked = config.GpuMonitor.Enabled;
        vramAlertCb.Checked = config.GpuMonitor.VramAlert;
        vramPctNum.Value = Clamp(50, 100, config.GpuMonitor.VramPct);
        tempWarnCb.Checked = config.TempWarn.Enabled;
        gpuMaxCNum.Value = Clamp(60, 110, config.TempWarn.GpuMaxC);
        cpuMaxCNum.Value = Clamp(60, 110, config.TempWarn.CpuMaxC);
        fpsCb.Checked = config.FpsGuard.Enabled;
        fpsMinNum.Value = Clamp(10, 120, config.FpsGuard.MinFps);
        fpsHoldNum.Value = Clamp(1, 30, config.FpsGuard.HoldMin);
        fpsPortNum.Value = Clamp(1024, 65535, config.FpsGuard.OscPort);
        fpsAddrBox.Text = config.FpsGuard.Address;
        frameLogCb.Checked = config.FrameLog.Enabled;
        pingMonCb.Checked = config.PingMonitor.Enabled;
        pingWarnCb.Checked = config.PingMonitor.Warn;
        pingHostBox.Text = config.PingMonitor.Host;
        InitializeHwMonitor();
        RefreshHwStatus();
        InitializeOscListener();
        perfListBox.Text = string.Join("\r\n", config.PerfMode.Apps);
        goalDailyNum.Value = Clamp(0, 1440, config.Goals.DailyMin);
        goalWeeklyNum.Value = Clamp(0, 168, config.Goals.WeeklyHours);
        schedLaunchCb.Checked = config.ScheduledLaunch.Enabled;
        schedLaunchTimeBox.Text = config.ScheduledLaunch.Time;
        schedLaunchMonCb.Checked = config.ScheduledLaunch.StartMonitoring;
        RebuildWorldProfiles();
        crashHintsCb.Checked = config.CrashHints.Enabled;
        crashArchCb.Checked = config.CrashArchive.Enabled;
        blockCb.Checked = config.WorldBlock.Enabled;
        blockLeaveCb.Checked = config.WorldBlock.AutoLeave;
        blockListBox.Text = string.Join("\r\n", config.WorldBlockList);
        watchCb.Checked = config.WatchlistNotify.Enabled;
        watchSoundCb.Checked = config.WatchlistNotify.Sound;
        watchListBox.Text = GetWatchlistText();
        crasherCb.Checked = config.CrasherAlert.Enabled;
        crasherNum.Value = Clamp(4, 60, config.CrasherAlert.ChurnPer10s);
        bedCb.Checked = config.Bedtime.Enabled;
        bedTimeBox.Text = config.Bedtime.Time;
        int bai = bedActCombo.Items.IndexOf(config.Bedtime.Action); if (bai < 0) bai = 0;
        bedActCombo.SelectedIndex = bai;
        bedCloseCb.Checked = config.Bedtime.CloseVRChat;
        bedWarnNum.Value = Clamp(0, 30, config.Bedtime.WarnMin);
        bedAppsCb.Checked = config.Bedtime.CloseApps;
        bedWindCb.Checked = config.Bedtime.WindDown;
        bedWindEndBox.Text = config.Bedtime.WindDownEnd;
        bedMediaCb.Checked = config.BedtimeMedia.Enabled;
        int bmi = bedMediaTypeCombo.Items.IndexOf(config.BedtimeMedia.Type); if (bmi < 0) bmi = 0;
        bedMediaTypeCombo.SelectedIndex = bmi;
        bedMediaUrlBox.Text = config.BedtimeMedia.Url;
        schedJoinCb.Checked = config.ScheduledJoin.Enabled;
        schedJoinTimeBox.Text = config.ScheduledJoin.Time;
        schedJoinTargetBox.Text = config.ScheduledJoin.Target;
        schedJoinNameBox.Text = config.ScheduledJoin.Name;
        // Optimization
        var opt = config.Optimization;
        optCacheCb.Checked = opt.CacheTuning.Enabled;
        optCacheSizeNum.Value = Clamp(1, 500, opt.CacheTuning.SizeGB);
        optCacheExpNum.Value = Clamp(1, 365, opt.CacheTuning.ExpiryDays);
        optCacheDirBox.Text = !string.IsNullOrEmpty(opt.CacheTuning.Directory) ? opt.CacheTuning.Directory : "(VRChat default)";
        optProcCb.Checked = opt.ProcPriority.Enabled;
        int pi = optProcLvl.Items.IndexOf(opt.ProcPriority.Level); if (pi < 0) pi = 0;
        optProcLvl.SelectedIndex = pi;
        optCompCb.Checked = opt.CompanionPriority.Enabled;
        int ci = optCompLvl.Items.IndexOf(opt.CompanionPriority.Level); if (ci < 0) ci = 0;
        optCompLvl.SelectedIndex = ci;
        optAffCb.Checked = opt.Affinity.Enabled;
        int affC = opt.Affinity.Cores;
        if (affC <= 0 || affC > cpuCoreCount) affC = cpuCoreCount;
        optAffNum.Value = affC;
        optFsoCb.Checked = opt.FullscreenOpt.Enabled;
        optDvrCb.Checked = opt.GameDVR.Enabled;
        loVerboseCb.Checked = opt.LaunchOpts.VerboseLogs;
        loDebugCb.Checked = opt.LaunchOpts.DebugGui;
        loExtraBox.Text = opt.LaunchOpts.Extra;
        RefreshOptStatus();
        RefreshLaunchOpts();
        ApplyTheme();
        RebuildAppsList();
        RebuildPresetsList();
        RebuildBookmarks();
        UpdateStatsPage();
        loading = false;
    }

    // ========================================================================
    //  SESSION / PLAYTIME TRACKING (state)
    // ========================================================================
    internal bool vrcWasRunning;
    internal DateTime? sessionStart;
    internal bool breakReminded;
    internal DateTime lastOscSent = DateTime.MinValue;
    internal DateTime lastDiscord = DateTime.MinValue;
    internal bool diskWarned;
    internal readonly List<DateTime> crashTimes = new();
    internal DateTime lastSchedRestart = DateTime.Now;
    internal string schedLaunchFiredDate = "";
    internal string schedJoinFiredDate = "";
    internal string lastInstanceId = "";
    internal string autoBookmarkedKey = "";
    internal Process lastProc;
    double? dashDiskCache;
    int? dashPhotoCache;
    string dashSteamVRCache;
    // Per-run player sampling (for the session-quality "avg players" component)
    internal long runPlayerSum;
    internal long runPlayerSamples;
    // Freeze / hang detection state
    internal DateTime? freezeSince;
    internal DateTime? freezeKillAt;
    internal DateTime lastFreezeWarn = DateTime.MinValue;
    // RAM-leak guard state
    internal DateTime? ramHighSince;
    internal DateTime lastRamWarn = DateTime.MinValue;
    // VRAM pressure alert state
    internal DateTime? vramHighSince;
    internal DateTime lastVramWarn = DateTime.MinValue;
    // Overheat warning state
    internal DateTime? tempHighSince;
    internal DateTime lastTempWarn = DateTime.MinValue;

    internal void AddPlaytimeSeconds(int sec)
    {
        var key = DateTime.Now.ToString("yyyy-MM-dd");
        config.PlayHistory.TryGetValue(key, out var cur);
        config.PlayHistory[key] = cur + sec;
        // Time-of-day histogram (seconds played per hour of day, all time).
        var hk = DateTime.Now.Hour.ToString();
        config.HourHistogram.TryGetValue(hk, out var hc);
        config.HourHistogram[hk] = hc + sec;
        // prune history older than 400 days (keeps a full year for the year heatmap)
        if (tick % 300 == 0)
        {
            var cut = DateTime.Now.Date.AddDays(-400);
            foreach (var k in config.PlayHistory.Keys.ToList())
            {
                if (DateTime.TryParse(k, out var d) && d < cut) config.PlayHistory.Remove(k);
            }
            var cut60 = DateTime.Now.Date.AddDays(-60);
            foreach (var k in config.RestartHistory.Keys.ToList())
            {
                if (DateTime.TryParse(k, out var d) && d < cut60) config.RestartHistory.Remove(k);
            }
            // Cap the most-seen-players tally: keep the top 1000 by count.
            if (config.PlayerSeen.Count > 1500)
            {
                config.PlayerSeen = config.PlayerSeen
                    .OrderByDescending(e => e.Value).Take(1000)
                    .ToDictionary(e => e.Key, e => e.Value);
            }
        }
    }

    internal void RecordSession()
    {
        if (!sessionStart.HasValue) return;
        int dur = (int)(DateTime.Now - sessionStart.Value).TotalSeconds;
        if (dur >= 30)
        {
            int avgP = runPlayerSamples > 0 ? (int)Math.Round(runPlayerSum / (double)runPlayerSamples) : 0;
            config.Sessions.Add(new SessionRec
            {
                Start = sessionStart.Value.ToString("yyyy-MM-dd HH:mm"),
                End = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                DurationSec = dur,
                AvgPlayers = avgP,
            });
            if (config.Sessions.Count > 500)
                config.Sessions = config.Sessions.Skip(config.Sessions.Count - 500).ToList();
            if (dur > config.Stats.LongestSessionSec) config.Stats.LongestSessionSec = dur;
        }
        SaveConfig();
    }

    // ========================================================================
    //  STATUS + DASHBOARD REFRESH
    // ========================================================================
    internal void SetStatus(string text, Color color)
    {
        // Owns the nav-rail indicator (monitoring state). The dashboard's big status
        // label is driven separately by UpdateDashboard so it always reflects reality.
        navStatusText.Text = text;
        dotColor = color;
        navStatusDot.Invalidate();
    }

    internal void UpdateDashboard(Process proc)
    {
        // The dashboard's ~20 labels + disk/photo/mic/SteamVR probes only matter
        // while the Dashboard is actually on screen; refreshed on switch otherwise.
        if (dashStatusBig == null || currentPage != "Dashboard" || WindowState == FormWindowState.Minimized) return;
        // Big status label = plain-language current state.
        if (monitoring)
        {
            dashStatusBig.Text = proc != null ? "Monitoring - VRChat up" : "Monitoring - waiting";
            dashStatusBig.ForeColor = Ui.Accent;
        }
        else if (proc != null)
        {
            dashStatusBig.Text = "VRChat running";
            dashStatusBig.ForeColor = Ui.Success;
        }
        else
        {
            dashStatusBig.Text = "Idle";
            dashStatusBig.ForeColor = Ui.Stopped;
        }

        if (proc != null)
        {
            try
            {
                var u = DateTime.Now - proc.StartTime;
                dashUptime.Text = $"{u.Days}d {u.Hours}h {u.Minutes}m {u.Seconds}s";
                // Colour shifts as the session grows: green < 2h, amber < 4h, red beyond.
                dashUptime.ForeColor = u.TotalHours < 2 ? Ui.Success : u.TotalHours < 4 ? Ui.Warning : Ui.Danger;
            }
            catch { dashUptime.Text = "-"; }
        }
        else
        {
            dashUptime.Text = "not running";
            dashUptime.ForeColor = Ui.TextMuted;
        }

        if (!string.IsNullOrEmpty(currentWorld) && worldJoinTime.HasValue)
        {
            dashWorldName.Text = currentWorld;
            var itype = GetInstanceType(currentInstanceId);
            dashWorldInst.Text = !string.IsNullOrEmpty(currentInstance)
                ? $"Instance #{currentInstance}" + (itype.Length > 0 ? $"   -   {itype}" : "")
                : "";
            var wt = DateTime.Now - worldJoinTime.Value;
            dashInWorld.Text = $"{(int)wt.TotalHours}h {wt.Minutes}m {wt.Seconds}s";
        }
        else
        {
            dashWorldName.Text = "Not in a world";
            dashWorldInst.Text = "";
            dashInWorld.Text = "-";
        }
        dashPlayers.Text = players.Count.ToString();
        dashRestarts.Text = $"{sessionRestarts} / {config.Stats.TotalRestarts}";
        // Last restart without seconds so it fits its card at any scaling.
        var lr = config.Stats.LastRestart;
        if (lr.Length >= 16) lr = lr[..16];
        dashLastCrash.Text = lr.Length > 0 ? lr : "Never";
        // Disk free + photo count change slowly, and GetPhotoCount does a recursive
        // folder scan - refresh them on an interval instead of every second.
        if (tick % 10 == 0 || dashDiskCache == null) dashDiskCache = GetDiskFreeGB();
        dashDisk.Text = dashDiskCache.HasValue ? $"{dashDiskCache.Value} GB" : "-";
        if (tick % 15 == 0 || dashPhotoCache == null) dashPhotoCache = GetPhotoCount();
        dashPhotos.Text = dashPhotoCache.ToString();
        var todayKey = DateTime.Now.ToString("yyyy-MM-dd");
        int todaySec = config.PlayHistory.TryGetValue(todayKey, out var ts) ? (int)ts : 0;
        dashToday.Text = FormatDuration(todaySec);
        if (tick % 5 == 0 || dashSteamVRCache == null)
            dashSteamVRCache = Process.GetProcessesByName(SteamVRProcess).Length > 0 ? "Running" : "Not running";
        dashSteamVR.Text = dashSteamVRCache;
        dashCpu.Text = proc != null && vrcCpuPct.HasValue ? $"{vrcCpuPct.Value}%" : "-";
        dashRam.Text = proc != null && vrcRamMB.HasValue
            ? (vrcRamMB.Value >= 1024 ? $"{vrcRamMB.Value / 1024.0:0.0} GB" : $"{vrcRamMB.Value} MB")
            : "-";
        string prioTxt = "-";
        if (proc != null) { try { prioTxt = proc.PriorityClass.ToString(); } catch { } }
        dashPrio.Text = prioTxt;

        // GPU / VRAM / temp cards (fed by UpdateGpuStats every 5s).
        if (config.GpuMonitor.Enabled && hwSource.Length > 0)
        {
            dashGpu.Text = gpuStats.Util.HasValue ? $"{(int)gpuStats.Util.Value}%" : "-";
            if (gpuStats.VramMB.HasValue)
            {
                var vtxt = gpuStats.VramMB.Value >= 1024 ? $"{gpuStats.VramMB.Value / 1024.0:0.0} GB" : $"{gpuStats.VramMB.Value} MB";
                if (gpuStats.VramTotMB.HasValue && gpuStats.VramTotMB.Value > 0) vtxt += $" / {gpuStats.VramTotMB.Value / 1024.0:0} GB";
                dashVram.Text = vtxt;
            }
            else dashVram.Text = "-";
            const string deg = "°C";
            var gt = gpuStats.TempC.HasValue ? $"{gpuStats.TempC.Value:0}{deg}" : "-";
            var ct = gpuStats.CpuTempC.HasValue ? $"{gpuStats.CpuTempC.Value:0}{deg}" : "-";
            dashGpuTemp.Text = $"{gt} / {ct}";
        }
        else
        {
            dashGpu.Text = "-"; dashVram.Text = "-"; dashGpuTemp.Text = "-";
        }

        // Ping / FPS / connection health cards.
        dashPing.Text = config.PingMonitor.Enabled && pingMs.HasValue ? $"{pingMs.Value} ms" : "-";
        if (config.PingMonitor.Enabled && netState.Length > 0)
        {
            dashNet.Text = netState;
            dashNet.ForeColor = netState switch
            {
                "Stable" => Ui.Success,
                "Unstable" => Ui.Warning,
                _ => Ui.Danger,
            };
        }
        else { dashNet.Text = "-"; dashNet.ForeColor = Ui.Text; }
        bool fpsFresh = vrcFps.HasValue && (DateTime.Now - vrcFpsAt).TotalSeconds <= 15;
        dashFps.Text = fpsFresh ? $"{vrcFps.Value:0}" : "-";
        // Frame-time graph repaint (every 2s; cheap line redraw).
        if (tick % 2 == 0) framePanel.Invalidate();

        // Countdown card: nearest of bedtime / scheduled restart.
        DateTime? best = null;
        var bestLbl = "";
        if (config.Bedtime.Enabled && DateTime.TryParse(config.Bedtime.Time, out var t))
        {
            var tg = DateTime.Now.Date.AddHours(t.Hour).AddMinutes(t.Minute);
            if (tg <= DateTime.Now) tg = tg.AddDays(1);
            best = tg; bestLbl = "Bedtime";
        }
        if (config.ScheduledRestart.Enabled && monitoring && proc != null)
        {
            var tg2 = lastSchedRestart.AddHours(config.ScheduledRestart.IntervalHours);
            if (tg2 > DateTime.Now && (best == null || tg2 < best)) { best = tg2; bestLbl = "Restart"; }
        }
        if (best.HasValue)
        {
            var sp = best.Value - DateTime.Now;
            dashNext.Text = $"{bestLbl} in {(int)sp.TotalHours}h {sp.Minutes:00}m";
        }
        else dashNext.Text = "-";

        // Mic state (live Windows default-microphone mute state, refreshed every tick).
        try { dashMic.Text = MicCtl.GetMicMute() ? "Muted" : "Live"; }
        catch { dashMic.Text = "Unavailable"; }
        dashAvatars.Text = avatarSwitches.ToString();

        // Quick toggles + Who's Here refresh.
        qtDesktop.Checked = config.DesktopMode;
        qtRejoin.Checked = config.RejoinOnRestart.Enabled;
        if (currentPage == "Dashboard" && tick % 5 == 0) RefreshWhoList();
    }

    // ========================================================================
    //  MONITORING TOGGLE
    // ========================================================================
    internal void ToggleMonitoring()
    {
        if (!monitoring)
        {
            monitoring = true;
            tickCounter = config.Interval;
            cooldownLeft = 0;
            crashTimes.Clear();
            toggleButton.Text = "Stop Monitoring";
            SetStatus("Monitoring", Ui.Accent);
            WriteLog("Monitoring started.");

            // Push cache settings into VRChat's config.json before it launches.
            if (config.Optimization.CacheTuning.Enabled) ApplyVrcCacheSettings(true);

            if (config.SteamVRAutoLaunch)
            {
                if (config.DesktopMode)
                {
                    WriteLog("Desktop Mode is on - skipping SteamVR auto-launch.");
                }
                else if (Process.GetProcessesByName(SteamVRProcess).Length == 0)
                {
                    WriteLog("Launching SteamVR...");
                    Process.Start(new ProcessStartInfo($"steam://run/{SteamVRAppId}") { UseShellExecute = true });
                }
            }
            InvokeAutoLaunch();
        }
        else
        {
            monitoring = false;
            toggleButton.Text = "Start Monitoring";
            if (Process.GetProcessesByName(ProcessName).Length > 0) SetStatus("VRChat running", Ui.Success);
            else SetStatus("Stopped", Ui.Stopped);
            WriteLog("Monitoring stopped.");
        }
    }
}
