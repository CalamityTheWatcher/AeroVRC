using System.Diagnostics;

namespace AeroVRC;

// ============================================================================
//  TIMER  (1s tick orchestrates everything)
// ============================================================================

public partial class MainForm
{
    internal System.Windows.Forms.Timer timer;
    internal System.Windows.Forms.Timer fxTimer;
    DateTime lastPhotoScan = DateTime.Now;

    void BuildTimers()
    {
        timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (s, e) => MainTick();
        fxTimer = new System.Windows.Forms.Timer { Interval = 16 };   // ~60 fps (delta-time scaled)
        fxTimer.Tick += (s, e) => FxTick();
    }

    void MainTick()
    {
        tick++;
        if (rejoinCooldown > 0) rejoinCooldown--;

        // Once, a few seconds in: shed startup/JIT scratch and return the working
        // set to the OS so the app settles at a lower baseline than its peak.
        if (tick == 4) { GC.Collect(); MemTrim.Trim(); }

        // Drain pending OSC datagrams (FPS feed; bounded, non-blocking).
        if (oscUdp != null) ReadOscFps();

        // Performance Mode: force-kill closed apps that ignored the polite close.
        if (perfKillAt.HasValue && DateTime.Now >= perfKillAt.Value)
        {
            foreach (var procId in perfPendingKill)
            {
                try { Process.GetProcessById(procId).Kill(); } catch { }
            }
            perfPendingKill = new List<int>();
            perfKillAt = null;
        }

        Process proc = null;
        try
        {
            var procs = Process.GetProcessesByName(ProcessName);
            if (procs.Length > 0) proc = procs[0];
        }
        catch { proc = null; }
        bool running = proc != null;
        lastProc = proc;   // so ShowPage can refresh the dashboard instantly on switch

        // Edge transitions
        if (running && !vrcWasRunning)
        {
            try { sessionStart = proc.StartTime; } catch { sessionStart = DateTime.Now; }
            breakReminded = false;
            lastSchedRestart = DateTime.Now;
            lastCpuTime = null;
            avatarSwitches = 0;
            lastPhotoScan = DateTime.Now;
            freezeSince = null; freezeKillAt = null; ramHighSince = null;
            fpsBelowSince = null; tempHighSince = null;
            runPlayerSum = 0; runPlayerSamples = 0;
            lastLogGrowth = DateTime.Now;
            WriteLog("VRChat detected (session started).");
            // Learn VRChat.exe's path so the Performance settings' exe tweaks can target it.
            try
            {
                var mm = GetVrcExePathLive();
                if (mm != null && config.Optimization.VrcExePath != mm)
                {
                    config.Optimization.VrcExePath = mm;
                    SaveConfig();
                    RefreshOptStatus();
                    // Honour a pending "disable Fullscreen Optimizations" intent now that we know the path.
                    if (config.Optimization.FullscreenOpt.Enabled) SetFullscreenOpt(true);
                }
            }
            catch { }
        }
        if (!running && vrcWasRunning)
        {
            RecordWorldVisit();
            RecordSession();
            // Remember the instance we were in (for rejoin-on-restart) before we clear
            // it. Exception: we auto-left a blocked world - never rejoin that one.
            if (blockLeaving)
            {
                lastInstanceId = "";
                blockLeaving = false;
            }
            else if (!string.IsNullOrEmpty(currentInstanceId)) lastInstanceId = currentInstanceId;
            // Crash cause hint: scan the tail of the log for the last error line.
            if (config.CrashHints.Enabled)
            {
                var hint = GetCrashCauseHint();
                if (hint.Length > 0)
                {
                    config.Stats.LastCrashReason = hint;
                    WriteLog($"Possible crash cause: {hint}");
                    // Tally the crash category over time (crash-cause analytics).
                    var cat = GetCrashCategory(hint);
                    if (cat.Length > 0)
                    {
                        config.CrashCauses.TryGetValue(cat, out var cc);
                        config.CrashCauses[cat] = cc + 1;
                    }
                }
            }
            // Archive the log tail for crash history / bug reports.
            SaveCrashLogArchive();
            ResetWorldState();
            DisconnectDiscordRP();
            // Restore any companions we down-prioritised back to Normal.
            if (config.Optimization.CompanionPriority.Enabled) ResetCompanionPriorities();
            if (config.AutoCloseCompanions) CloseCompanions();
            vrcCpuPct = null; vrcRamMB = null; lastCpuTime = null; prioWarned = false;
            FlushFrameLog();
            WriteLog("VRChat closed.");
            // A rejoin was requested while VRChat was still running (bookmark /
            // restart into instance): the old session is gone now, so relaunch
            // straight into the target. Cooldown keeps the monitor loop from also
            // launching a copy.
            if (pendingJoinId.Length > 0)
            {
                var target = pendingJoinId;
                pendingJoinId = "";
                cooldownLeft = Math.Max(cooldownLeft, config.Cooldown);
                StartVRChat(target);
            }
            if (!monitoring) SetStatus("Stopped", Ui.Stopped);
        }
        vrcWasRunning = running;

        if (running)
        {
            PollVRChatLog();
            AddPlaytimeSeconds(1);
            // Tally in-world time by instance access type (Statistics breakdown).
            if (!string.IsNullOrEmpty(currentInstanceId) && worldJoinTime.HasValue)
            {
                var itype = GetInstanceType(currentInstanceId);
                if (itype.Length > 0)
                {
                    config.InstanceTypeHist.TryGetValue(itype, out var itc);
                    config.InstanceTypeHist[itype] = itc + 1;
                }
            }
            try
            {
                int sec = (int)(DateTime.Now - sessionStart.Value).TotalSeconds;
                if (sec > config.Stats.LongestSessionSec) config.Stats.LongestSessionSec = sec;
            }
            catch { }

            // Live process metrics: CPU + RAM are cheap process property reads.
            vrcCpuPct = GetVrcCpuPercent(proc);
            try { vrcRamMB = (int)(proc.WorkingSet64 / 1048576); } catch { vrcRamMB = null; }

            // Sample nearby-player count while in a world (session-quality avg).
            if (worldJoinTime.HasValue)
            {
                runPlayerSum += players.Count;
                runPlayerSamples++;
                if (players.Count > config.Stats.MaxPlayersSeen) config.Stats.MaxPlayersSeen = players.Count;
            }

            // Frame-time sample (1/s from the OSC FPS feed): rolling 10-min window
            // for the Dashboard graph + optional CSV log (flushed every 30s).
            if (vrcFps.HasValue && vrcFps.Value > 0 && (DateTime.Now - vrcFpsAt).TotalSeconds <= 15)
            {
                double ftMs = Math.Round(1000.0 / vrcFps.Value, 2);
                frameHist.Add(ftMs);
                while (frameHist.Count > 600) frameHist.RemoveAt(0);
                if (config.FrameLog.Enabled)
                    frameLogBuf.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{vrcFps.Value:0.#},{ftMs}");
            }
            if (tick % 30 == 0) FlushFrameLog();

            // Optimization: keep process priorities in line every few seconds.
            if (tick % 5 == 0) ApplyProcessPriorities(proc);

            // Auto-bookmark after N hours in the same instance.
            if (config.AutoBookmark.Enabled && !string.IsNullOrEmpty(currentInstanceId) && worldJoinTime.HasValue &&
                autoBookmarkedKey != currentInstanceId && tick % 15 == 0)
            {
                if ((DateTime.Now - worldJoinTime.Value).TotalHours >= config.AutoBookmark.Hours)
                {
                    autoBookmarkedKey = currentInstanceId;
                    bool exists = config.Bookmarks.Any(b => b.InstanceId == currentInstanceId);
                    if (!exists && !string.IsNullOrEmpty(currentWorld) && currentWorld != "Unknown world")
                    {
                        config.Bookmarks.Add(new Bookmark
                        {
                            Name = currentWorld, World = currentWorld, InstanceId = currentInstanceId,
                            Added = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Note = "Auto-bookmarked", Pinned = false,
                        });
                        SaveConfig();
                        WriteLog($"Auto-bookmarked: {currentWorld} (spent {config.AutoBookmark.Hours}+ hours here).");
                        RebuildBookmarks();
                    }
                }
            }

            // Photos-per-world: credit new photo files to the current world.
            if (tick % 30 == 0 && !string.IsNullOrEmpty(currentWorld) && currentWorld != "Unknown world" && Directory.Exists(photoDir))
            {
                try
                {
                    var di = new DirectoryInfo(photoDir);
                    int newPics = di.EnumerateFiles("*.png", SearchOption.AllDirectories)
                                    .Concat(di.EnumerateFiles("*.jpg", SearchOption.AllDirectories))
                                    .Count(f => f.LastWriteTime > lastPhotoScan);
                    if (newPics > 0)
                    {
                        config.PhotoWorlds.TryGetValue(currentWorld, out var pc);
                        config.PhotoWorlds[currentWorld] = pc + newPics;
                    }
                    lastPhotoScan = DateTime.Now;
                }
                catch { }
            }

            // Soft-close escalation shared by the freeze detector and the RAM guard:
            // a close request is in flight, and hung/busy processes often can't pump
            // WM_CLOSE - so force-kill once the grace period runs out.
            if (freezeKillAt.HasValue)
            {
                if (DateTime.Now >= freezeKillAt.Value)
                {
                    freezeKillAt = null;
                    WriteLog("Watchdog: VRChat ignored the close request - force-killing.");
                    try { proc.Kill(); } catch { }
                }
            }
            else
            {
                // Freeze / hang detection: window stuck "Not Responding", or the game
                // log gone silent while in a world -> soft-restart.
                if (config.FreezeDetect.Enabled)
                {
                    double sessionAge = 0;
                    try { sessionAge = (DateTime.Now - proc.StartTime).TotalSeconds; } catch { }
                    // (a) main window Not Responding for HangSec consecutive seconds
                    // (90s settle guard so launch/loading hitches never count).
                    bool hung = false;
                    if (sessionAge >= 90) { try { hung = !proc.Responding; } catch { } }
                    if (hung) { freezeSince ??= DateTime.Now; }
                    else freezeSince = null;
                    bool frozen = false;
                    var why = "";
                    if (freezeSince.HasValue && (DateTime.Now - freezeSince.Value).TotalSeconds >= config.FreezeDetect.HangSec)
                    {
                        frozen = true;
                        why = $"window Not Responding for {config.FreezeDetect.HangSec}s";
                    }
                    // (b) log stalled while in a world (VRChat normally logs constantly).
                    if (!frozen && worldJoinTime.HasValue && (DateTime.Now - worldJoinTime.Value).TotalMinutes >= 2)
                    {
                        double stall = (DateTime.Now - lastLogGrowth).TotalMinutes;
                        if (stall >= config.FreezeDetect.LogStallMin)
                        {
                            frozen = true;
                            why = $"game log silent for {(int)stall}m";
                        }
                    }
                    if (frozen)
                    {
                        if (monitoring)
                        {
                            WriteLog($"Freeze detect: {why} - soft-restarting VRChat (monitor will relaunch it).");
                            if (config.SoundAlert) System.Media.SystemSounds.Exclamation.Play();
                            ShowToast("Freeze detected", "VRChat looks frozen - restarting it", Ui.Warning);
                            freezeSince = null;
                            lastLogGrowth = DateTime.Now;
                            freezeKillAt = DateTime.Now.AddSeconds(10);
                            try { proc.CloseMainWindow(); } catch { }
                        }
                        else if ((DateTime.Now - lastFreezeWarn).TotalMinutes >= 5)
                        {
                            // Monitoring is off: warn (at most every 5 min) instead.
                            lastFreezeWarn = DateTime.Now;
                            WriteLog($"Freeze detect: {why}. Monitoring is off, so it won't be auto-restarted.");
                            if (config.SoundAlert) System.Media.SystemSounds.Exclamation.Play();
                            ShowToast("VRChat frozen?", "Looks frozen - monitoring is off, no auto-restart", Ui.Warning);
                        }
                    }
                }

                // RAM-leak guard: soft-restart when VRChat's working set stays above
                // the configured limit for 30s straight (spikes don't count). A 3-min
                // settle guard avoids tripping on launch.
                if (config.RamLimit.Enabled && vrcRamMB.HasValue)
                {
                    int limitMB = config.RamLimit.MaxGB * 1024;
                    double ramAge = 0;
                    try { ramAge = (DateTime.Now - proc.StartTime).TotalSeconds; } catch { }
                    if (vrcRamMB.Value >= limitMB && ramAge >= 180)
                    {
                        ramHighSince ??= DateTime.Now;
                        if ((DateTime.Now - ramHighSince.Value).TotalSeconds >= 30)
                        {
                            double ramGB = Math.Round(vrcRamMB.Value / 1024.0, 1);
                            if (monitoring)
                            {
                                ramHighSince = null;
                                WriteLog($"RAM guard: VRChat is using {ramGB} GB (limit {config.RamLimit.MaxGB} GB) - soft-restarting (monitor will relaunch it).");
                                if (config.SoundAlert) System.Media.SystemSounds.Exclamation.Play();
                                ShowToast("Memory limit hit", $"VRChat at {ramGB} GB - restarting it", Ui.Warning);
                                freezeKillAt = DateTime.Now.AddSeconds(15);
                                try { proc.CloseMainWindow(); } catch { }
                            }
                            else if ((DateTime.Now - lastRamWarn).TotalMinutes >= 5)
                            {
                                // Monitoring is off: warn (at most every 5 min) instead.
                                lastRamWarn = DateTime.Now;
                                WriteLog($"RAM guard: VRChat is using {ramGB} GB (limit {config.RamLimit.MaxGB} GB). Monitoring is off, so it won't be auto-restarted.");
                                if (config.SoundAlert) System.Media.SystemSounds.Exclamation.Play();
                                ShowToast("VRChat memory high", $"{ramGB} GB used - monitoring is off, no auto-restart", Ui.Warning);
                            }
                        }
                    }
                    else ramHighSince = null;
                }

                // FPS guard: FPS (fed via OSC) stuck below the threshold for the
                // configured number of minutes -> soft-restart. Requires fresh data
                // (a sample in the last 15s) so a missing FPS source never triggers.
                if (config.FpsGuard.Enabled)
                {
                    bool fpsFresh = vrcFps.HasValue && (DateTime.Now - vrcFpsAt).TotalSeconds <= 15;
                    if (fpsFresh && vrcFps.Value > 0 && vrcFps.Value < config.FpsGuard.MinFps)
                    {
                        fpsBelowSince ??= DateTime.Now;
                        if ((DateTime.Now - fpsBelowSince.Value).TotalMinutes >= config.FpsGuard.HoldMin)
                        {
                            if (monitoring)
                            {
                                fpsBelowSince = null;
                                WriteLog($"FPS guard: {vrcFps.Value:0} FPS below {config.FpsGuard.MinFps} for {config.FpsGuard.HoldMin}+ min - soft-restarting VRChat (monitor will relaunch it).");
                                if (config.SoundAlert) System.Media.SystemSounds.Exclamation.Play();
                                ShowToast("Low FPS", $"Stuck at {vrcFps.Value:0} FPS - restarting VRChat", Ui.Warning);
                                freezeKillAt = DateTime.Now.AddSeconds(15);
                                try { proc.CloseMainWindow(); } catch { }
                            }
                            else if ((DateTime.Now - lastFpsWarn).TotalMinutes >= 5)
                            {
                                lastFpsWarn = DateTime.Now;
                                WriteLog("FPS guard: sustained low FPS. Monitoring is off, so it won't be auto-restarted.");
                                if (config.SoundAlert) System.Media.SystemSounds.Exclamation.Play();
                                ShowToast("Low FPS", "Sustained low FPS - monitoring off, no auto-restart", Ui.Warning);
                            }
                        }
                    }
                    else fpsBelowSince = null;
                }
            }

            // Scheduled restart (clears memory leaks in long sessions).
            if (config.ScheduledRestart.Enabled && monitoring)
            {
                double hrs = (DateTime.Now - lastSchedRestart).TotalHours;
                if (hrs >= config.ScheduledRestart.IntervalHours)
                {
                    lastSchedRestart = DateTime.Now;
                    WriteLog($"Scheduled restart: closing VRChat after {config.ScheduledRestart.IntervalHours}h (monitor will relaunch it).");
                    try { proc.CloseMainWindow(); } catch { }
                }
            }

            // Break reminder
            if (config.BreakReminder.Enabled && !breakReminded && sessionStart.HasValue)
            {
                double hrs = (DateTime.Now - sessionStart.Value).TotalHours;
                if (hrs >= config.BreakReminder.Hours)
                {
                    breakReminded = true;
                    WriteLog($"Break reminder: {hrs:0.0}h in VRChat.");
                    if (config.SoundAlert) System.Media.SystemSounds.Asterisk.Play();
                    if (config.BreakReminder.CloseVRChat)
                    {
                        if (monitoring) ToggleMonitoring();
                        try { proc.CloseMainWindow(); } catch { }
                        WriteLog("Break reminder closed VRChat.");
                    }
                    MessageBox.Show($"You've been in VRChat for {config.BreakReminder.Hours}+ hours. Time for a break!",
                        "Break reminder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }

            // Auto-rejoin on disconnect
            if (pendingRejoin)
            {
                pendingRejoin = false;
                if (monitoring && config.AutoRejoin && !string.IsNullOrEmpty(currentInstanceId) && rejoinCooldown <= 0)
                {
                    WriteLog("Disconnect detected - attempting rejoin...");
                    StartVRChat(currentInstanceId);
                    rejoinCooldown = 60;
                }
            }

            // OSC chatbox (user-defined template)
            if (config.OscChatbox.Enabled && (DateTime.Now - lastOscSent).TotalSeconds >= config.OscChatbox.IntervalSec)
            {
                lastOscSent = DateTime.Now;
                SendOscChatbox(FormatOscTemplate(config.OscChatbox.Template));
            }

            // Discord RP
            if (config.DiscordRP.Enabled && config.DiscordRP.ClientId.Length > 0 && (DateTime.Now - lastDiscord).TotalSeconds >= 15)
            {
                lastDiscord = DateTime.Now;
                UpdateDiscordRP();
            }
        }
        else
        {
            if (currentWorld.Length > 0 || worldJoinTime.HasValue) ResetWorldState();
            // Auto-clear cache (only when VRChat closed)
            if (config.AutoClearCache.Enabled && tick % 60 == 0)
            {
                bool due = true;
                if (config.AutoClearCache.LastClear.Length > 0 && DateTime.TryParse(config.AutoClearCache.LastClear, out var last))
                    due = (DateTime.Now - last).TotalDays >= config.AutoClearCache.IntervalDays;
                if (due)
                {
                    WriteLog("Auto-clearing VRChat cache (scheduled)...");
                    ClearVRChatCache();
                }
            }
        }

        // Disk monitor
        if (config.DiskMonitor.Enabled && tick % 30 == 0)
        {
            var free = GetDiskFreeGB();
            if (free.HasValue && free.Value < config.DiskMonitor.MinGB)
            {
                if (!diskWarned)
                {
                    diskWarned = true;
                    WriteLog($"WARNING: low disk space ({free.Value} GB free).");
                    if (config.SoundAlert) System.Media.SystemSounds.Exclamation.Play();
                }
            }
            else diskWarned = false;
        }

        // Bedtime / sleep timer (checked every couple of seconds against wall-clock).
        if (config.Bedtime.Enabled && tick % 2 == 0) CheckBedtime();

        // Wind-down routine: while active, keep VRChat closed and block relaunches
        // until the end time (set when bedtime fires with wind-down enabled).
        if (windDownUntil.HasValue)
        {
            if (DateTime.Now >= windDownUntil.Value)
            {
                windDownUntil = null;
                WriteLog("Wind-down period ended - VRChat may reopen again.");
            }
            else if (running)
            {
                WriteLog("Wind-down: closing VRChat (rejoin/reopen blocked until the routine ends).");
                try { proc.CloseMainWindow(); } catch { }
            }
        }

        // Scheduled auto-join: at the set time, join a saved instance (event reminder).
        if (config.ScheduledJoin.Enabled && tick % 5 == 0 && config.ScheduledJoin.Target.Length > 0)
        {
            var todayKey = DateTime.Now.ToString("yyyy-MM-dd");
            if (schedJoinFiredDate != todayKey && DateTime.TryParse(config.ScheduledJoin.Time, out var t))
            {
                var target = DateTime.Now.Date.AddHours(t.Hour).AddMinutes(t.Minute);
                var now = DateTime.Now;
                if (now >= target && (now - target).TotalMinutes <= 2)
                {
                    schedJoinFiredDate = todayKey;
                    var jid = ConvertVrcLink(config.ScheduledJoin.Target) ?? config.ScheduledJoin.Target;
                    var jname = config.ScheduledJoin.Name.Length > 0 ? config.ScheduledJoin.Name : "your saved instance";
                    WriteLog($"Scheduled join ({config.ScheduledJoin.Time}): joining {jname}.");
                    if (config.SoundAlert) System.Media.SystemSounds.Asterisk.Play();
                    ShowToast("Event reminder", $"Joining {jname}", Ui.Accent);
                    StartVRChat(jid);
                }
            }
        }

        // Scheduled launch: at the set time of day, start VRChat + auto-launch apps
        // (fires once per day; skips if VRChat is already running).
        if (config.ScheduledLaunch.Enabled && tick % 5 == 0)
        {
            var todayKey = DateTime.Now.ToString("yyyy-MM-dd");
            if (schedLaunchFiredDate != todayKey && DateTime.TryParse(config.ScheduledLaunch.Time, out var t))
            {
                var target = DateTime.Now.Date.AddHours(t.Hour).AddMinutes(t.Minute);
                var now = DateTime.Now;
                // Fire within a 2-minute window after the target so a missed tick still catches it.
                if (now >= target && (now - target).TotalMinutes <= 2)
                {
                    schedLaunchFiredDate = todayKey;
                    if (Process.GetProcessesByName(ProcessName).Length > 0)
                    {
                        WriteLog("Scheduled launch: VRChat already running - skipping.");
                    }
                    else
                    {
                        WriteLog($"Scheduled launch ({config.ScheduledLaunch.Time}): starting VRChat + companions.");
                        if (config.ScheduledLaunch.StartMonitoring && !monitoring)
                        {
                            ToggleMonitoring();   // launches SteamVR + auto-launch apps + starts the watchdog
                        }
                        else
                        {
                            InvokeAutoLaunch();
                            StartVRChat();
                        }
                    }
                }
            }
        }

        // GPU / VRAM monitor poll (LibreHardwareMonitor read or async nvidia-smi -
        // both cheap; sampled while VRChat runs or the Dashboard is visible).
        if (config.GpuMonitor.Enabled && hwSource.Length > 0 && tick % 5 == 0 &&
            (running || currentPage == "Dashboard"))
        {
            UpdateGpuStats();
            // VRAM pressure alert: sustained 30s over the threshold, at most every
            // 10 min, and only while VRChat is actually running.
            if (running && config.GpuMonitor.VramAlert &&
                gpuStats.VramMB.HasValue && gpuStats.VramTotMB.HasValue && gpuStats.VramTotMB.Value > 0)
            {
                double vpct = 100.0 * gpuStats.VramMB.Value / gpuStats.VramTotMB.Value;
                if (vpct >= config.GpuMonitor.VramPct)
                {
                    vramHighSince ??= DateTime.Now;
                    if ((DateTime.Now - vramHighSince.Value).TotalSeconds >= 30 && (DateTime.Now - lastVramWarn).TotalMinutes >= 10)
                    {
                        lastVramWarn = DateTime.Now;
                        WriteLog($"VRAM alert: {vpct:0}% of {gpuStats.VramTotMB.Value / 1024.0:0.0} GB VRAM in use - risk of stutter or a crash.");
                        if (config.SoundAlert) System.Media.SystemSounds.Exclamation.Play();
                        ShowToast("VRAM nearly full", $"{vpct:0}% of VRAM in use", Ui.Warning);
                    }
                }
                else vramHighSince = null;
            }
            // Overheat warning (opt-in): GPU or CPU over its limit for 60s straight.
            if (config.TempWarn.Enabled)
            {
                bool hotG = gpuStats.TempC.HasValue && gpuStats.TempC.Value >= config.TempWarn.GpuMaxC;
                bool hotC = gpuStats.CpuTempC.HasValue && gpuStats.CpuTempC.Value >= config.TempWarn.CpuMaxC;
                if (hotG || hotC)
                {
                    tempHighSince ??= DateTime.Now;
                    if ((DateTime.Now - tempHighSince.Value).TotalSeconds >= 60 && (DateTime.Now - lastTempWarn).TotalMinutes >= 10)
                    {
                        lastTempWarn = DateTime.Now;
                        var what = new List<string>();
                        if (hotG) what.Add($"GPU {gpuStats.TempC.Value:0}C");
                        if (hotC) what.Add($"CPU {gpuStats.CpuTempC.Value:0}C");
                        WriteLog("OVERHEAT warning: " + string.Join(", ", what) + " - check cooling / lower graphics settings.");
                        if (config.SoundAlert) System.Media.SystemSounds.Exclamation.Play();
                        ShowToast("Running hot", string.Join("   ", what) + " - check your cooling", Ui.Danger);
                    }
                }
                else tempHighSince = null;
            }
        }

        // Ping / connection health (async; offset from the GPU poll tick).
        if (config.PingMonitor.Enabled && tick % 5 == 2 && (running || currentPage == "Dashboard"))
        {
            UpdatePingMonitor();
            // Disconnect prediction warning (opt-in): 3 degraded verdicts in a row.
            if (running && config.PingMonitor.Warn && netBadStreak >= 3 &&
                (DateTime.Now - lastNetWarn).TotalMinutes >= 10)
            {
                lastNetWarn = DateTime.Now;
                WriteLog($"Connection health: packet loss / latency spikes to {config.PingMonitor.Host} - a disconnect is likely.");
                if (config.SoundAlert) System.Media.SystemSounds.Exclamation.Play();
                ShowToast("Connection degrading", "Packet loss detected - disconnect likely", Ui.Warning);
            }
        }

        UpdateDashboard(proc);
        if (currentPage == "Statistics" && tick % 5 == 0) UpdateStatsPage();
        // (RefreshOptStatus is no longer polled here - it did an expensive MainModule
        //  lookup every 5s. It now runs only on VRChat-detection edges and when the
        //  Settings page is opened, which is all it needs.)

        // Persist playtime periodically
        if (tick % 30 == 0) SaveConfig();

        // ---- Monitoring loop ----
        if (!monitoring) return;
        tickCounter++;
        if (cooldownLeft > 0) { cooldownLeft--; return; }
        if (tickCounter >= config.Interval)
        {
            tickCounter = 0;
            if (!running)
            {
                // Wind-down routine: don't relaunch VRChat while it's active.
                if (windDownUntil.HasValue && DateTime.Now < windDownUntil.Value)
                {
                    SetStatus("Wind-down (VRChat blocked)", Ui.Stopped);
                    return;
                }
                // crash-loop protection
                if (config.CrashLoop.Enabled)
                {
                    var cut = DateTime.Now.AddMinutes(-config.CrashLoop.WindowMin);
                    for (int i = crashTimes.Count - 1; i >= 0; i--)
                        if (crashTimes[i] < cut) crashTimes.RemoveAt(i);
                    if (crashTimes.Count >= config.CrashLoop.MaxCrashes)
                    {
                        WriteLog($"Crash-loop tripped ({crashTimes.Count} restarts in {config.CrashLoop.WindowMin}m). Stopping.");
                        if (config.SoundAlert) System.Media.SystemSounds.Hand.Play();
                        ToggleMonitoring();
                        MessageBox.Show("VRChat has restarted too many times in a short period. Monitoring has been stopped so it doesn't loop forever.",
                            "Crash-loop protection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                SetStatus("VRChat not running", Ui.Danger);
                WriteLog("VRChat not detected. Relaunching...");
                // Optionally rejoin the instance the user was in before it went down.
                if (config.RejoinOnRestart.Enabled && lastInstanceId.Length > 0)
                {
                    WriteLog("Rejoin-on-restart: returning to the last instance.");
                    StartVRChat(lastInstanceId);
                }
                else
                {
                    StartVRChat();
                }
                if (config.SoundAlert) System.Media.SystemSounds.Asterisk.Play();
                sessionRestarts++;
                config.Stats.TotalRestarts++;
                config.Stats.LastRestart = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var rk = DateTime.Now.ToString("yyyy-MM-dd");
                config.RestartHistory.TryGetValue(rk, out var rc);
                config.RestartHistory[rk] = rc + 1;
                crashTimes.Add(DateTime.Now);
                SaveConfig();
                cooldownLeft = config.Cooldown;
                WriteLog($"Waiting {config.Cooldown}s before checking again.");
            }
            else
            {
                SetStatus("VRChat running", Ui.Success);
            }
        }
    }

    void OnAppClosing(object sender, FormClosingEventArgs e)
    {
        timer.Stop();
        fxTimer.Stop();
        try { photoLoadTimer?.Stop(); ClearPhotoGrid(); } catch { }
        try { RemoveVrcxSnapshot(); } catch { }
        CloseHwMonitor();
        try { nvsmiProc?.Dispose(); } catch { }
        try { oscUdp?.Close(); } catch { }
        try { pingSender?.Dispose(); } catch { }
        try { FlushFrameLog(); } catch { }
        toastTimer?.Stop();
        if (toastForm != null && !toastForm.IsDisposed) toastForm.Dispose();
        if (vrcWasRunning) RecordSession();
        DisconnectDiscordRP();
        SaveConfig();
    }
}
