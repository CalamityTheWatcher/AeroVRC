using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;

namespace AeroVRC;

// ============================================================================
//  OPTIMIZATION  (cache config.json / process priority / Windows game tweaks)
// ============================================================================

public partial class MainForm
{
    internal string vrcConfigJsonPath => Path.Combine(vrcLowDir, "config.json");

    internal static ProcessPriorityClass ToPriorityClass(string s) => s switch
    {
        "High" => ProcessPriorityClass.High,
        "AboveNormal" => ProcessPriorityClass.AboveNormal,
        "BelowNormal" => ProcessPriorityClass.BelowNormal,
        "Idle" => ProcessPriorityClass.Idle,
        _ => ProcessPriorityClass.Normal,
    };

    internal bool perfMode;
    internal List<string> perfClosedApps = new();    // exe paths to reopen on deactivate
    internal List<int> perfPendingKill = new();      // pids that got CloseMainWindow; killed at the deadline
    internal DateTime? perfKillAt;
    internal bool affApplied;                        // an affinity mask is currently applied to VRChat
    internal bool prioWarned;                         // logged a priority-set failure this session (avoid spam)

    // Applies VRChat + companion process priorities per the Optimization config.
    // Called from the timer while VRChat is running; sets a priority only when it
    // differs, so it's cheap and quiet.
    // Precedence for VRChat's priority: Performance Mode (High) > the current
    // world's profile > the global Optimization setting. Same idea for affinity.
    internal void ApplyProcessPriorities(Process proc)
    {
        var opt = config.Optimization;
        // Per-world performance profile for the world we're currently in (if any).
        WorldProfile prof = null;
        if (!string.IsNullOrEmpty(currentInstanceId))
        {
            var wid = currentInstanceId.Split(':')[0];
            if (!string.IsNullOrEmpty(wid)) config.WorldProfiles.TryGetValue(wid, out prof);
        }
        if (proc != null)
        {
            ProcessPriorityClass? want = null;
            if (perfMode) want = ProcessPriorityClass.High;
            else if (prof != null) want = ToPriorityClass(prof.Priority);
            else if (opt.ProcPriority.Enabled) want = ToPriorityClass(opt.ProcPriority.Level);
            if (want.HasValue)
            {
                try
                {
                    if (proc.PriorityClass != want.Value)
                    {
                        proc.PriorityClass = want.Value;
                        WriteLog($"VRChat CPU priority set to {want.Value}.");
                    }
                }
                catch (Exception ex)
                {
                    if (!prioWarned)
                    {
                        prioWarned = true;
                        WriteLog($"Couldn't set VRChat priority to {want.Value}: {ex.Message}. If VRChat runs as administrator, run AeroVRC as administrator too.");
                    }
                }
            }
            // CPU affinity: pin VRChat to the first N logical cores (0 = all).
            int cores = 0;
            if (prof != null && prof.Cores > 0) cores = prof.Cores;
            else if (opt.Affinity.Enabled) cores = opt.Affinity.Cores;
            int total = Environment.ProcessorCount;
            if (cores > 0 && cores < total)
            {
                long mask = cores >= 64 ? -1L : (1L << cores) - 1;   // low N bits set
                try
                {
                    if ((long)proc.ProcessorAffinity != mask) { proc.ProcessorAffinity = (IntPtr)mask; affApplied = true; }
                    else affApplied = true;
                }
                catch { }
            }
            else if (affApplied)
            {
                // A previous world profile (or setting change) narrowed the mask and
                // nothing wants it narrowed any more - restore all cores once.
                try
                {
                    long mask = total >= 64 ? -1L : (1L << total) - 1;
                    if ((long)proc.ProcessorAffinity != mask) proc.ProcessorAffinity = (IntPtr)mask;
                    affApplied = false;
                }
                catch { affApplied = false; }
            }
        }
        if (opt.CompanionPriority.Enabled)
        {
            var want = ToPriorityClass(opt.CompanionPriority.Level);
            foreach (var n in GetCompanionProcessNames())
            {
                try
                {
                    foreach (var pr in Process.GetProcessesByName(n))
                        if (pr.PriorityClass != want) pr.PriorityClass = want;
                }
                catch { }
            }
        }
    }

    // Re-apply priorities/affinity right now (used when a setting changes so the
    // effect is instant instead of waiting for the next 5s tick).
    internal void ApplyPrioritiesNow()
    {
        prioWarned = false;
        try
        {
            var p = Process.GetProcessesByName(ProcessName).FirstOrDefault();
            if (p != null) ApplyProcessPriorities(p);
            else WriteLog("Priority setting saved - it applies while VRChat is running.");
        }
        catch { }
    }

    // Restores companion processes to Normal priority (used when VRChat exits).
    internal void ResetCompanionPriorities()
    {
        foreach (var n in GetCompanionProcessNames())
        {
            try
            {
                foreach (var pr in Process.GetProcessesByName(n))
                    if (pr.PriorityClass != ProcessPriorityClass.Normal) pr.PriorityClass = ProcessPriorityClass.Normal;
            }
            catch { }
        }
    }

    // ===== Performance Mode =====
    // One-click boost: closes the background apps listed in Settings (politely
    // first, force-killed a few seconds later via the timer if they ignore it),
    // bumps VRChat to High priority, and reopens everything on deactivate.
    internal void SetPerformanceMode(bool on)
    {
        if (on == perfMode) return;
        perfMode = on;
        if (on)
        {
            perfClosedApps = new List<string>();
            perfPendingKill = new List<int>();
            foreach (var name in config.PerfMode.Apps)
            {
                var n = (name ?? "").Trim();
                if (n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) n = n[..^4];
                if (n.Length == 0 || string.Equals(n, ProcessName, StringComparison.OrdinalIgnoreCase)) continue;   // never close VRChat itself
                foreach (var pr in Process.GetProcessesByName(n))
                {
                    try
                    {
                        string path = null;
                        try { path = pr.MainModule?.FileName; } catch { }
                        if (path != null && !perfClosedApps.Contains(path)) perfClosedApps.Add(path);
                        if (pr.MainWindowHandle != IntPtr.Zero && pr.CloseMainWindow())
                            perfPendingKill.Add(pr.Id);    // escalate later if it lingers
                        else
                            try { pr.Kill(); } catch { }
                    }
                    catch { }
                }
            }
            if (perfPendingKill.Count > 0) perfKillAt = DateTime.Now.AddSeconds(5);
            WriteLog($"Performance Mode ON - closing {perfClosedApps.Count} background app(s); VRChat priority boosted to High.");
            ShowToast("Performance Mode", "Background apps closed - VRChat boosted", Ui.Accent);
        }
        else
        {
            perfPendingKill = new List<int>(); perfKillAt = null;
            int reopened = 0;
            foreach (var path in perfClosedApps)
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); reopened++; } catch { }
                }
            }
            perfClosedApps = new List<string>();
            WriteLog($"Performance Mode OFF - reopened {reopened} app(s); VRChat priority back to your settings.");
            ShowToast("Performance Mode", "Off - background apps restored", Ui.Accent);
        }
        // Re-apply priorities immediately so the boost / restore is instant.
        try
        {
            var p = Process.GetProcessesByName(ProcessName).FirstOrDefault();
            if (p != null) ApplyProcessPriorities(p);
        }
        catch { }
    }

    // Writes VRChat's own config.json (cache size / expiry / directory). VRChat
    // reads it at launch and rewrites it on exit, so we only ever touch it while
    // closed and preserve any keys we don't manage.
    internal bool ApplyVrcCacheSettings(bool silent = false)
    {
        if (Process.GetProcessesByName(ProcessName).Length > 0)
        {
            if (!silent) WriteLog("Cannot write VRChat config.json while VRChat is running.");
            return false;
        }
        var ct = config.Optimization.CacheTuning;
        var obj = new Dictionary<string, object>();
        if (File.Exists(vrcConfigJsonPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(vrcConfigJsonPath));
                foreach (var p in doc.RootElement.EnumerateObject()) obj[p.Name] = p.Value.Clone();
            }
            catch { }
        }
        obj["cache_size"] = ct.SizeGB;
        obj["cache_expiry_delay"] = ct.ExpiryDays;
        if (!string.IsNullOrEmpty(ct.Directory)) obj["cache_directory"] = ct.Directory;
        else obj.Remove("cache_directory");
        try
        {
            Directory.CreateDirectory(vrcLowDir);
            File.WriteAllText(vrcConfigJsonPath, JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
            WriteLog($"VRChat cache settings written (size {ct.SizeGB} GB, expiry {ct.ExpiryDays} d).");
            return true;
        }
        catch (Exception ex)
        {
            WriteLog($"Failed to write VRChat config.json: {ex.Message}");
            return false;
        }
    }

    // Resolves VRChat.exe's full path from the running process. (The original used
    // WMI because a 32-bit host can't inspect a 64-bit process's MainModule; this
    // build is 64-bit, so MainModule works directly.)
    internal string GetVrcExePathLive()
    {
        try
        {
            var p = Process.GetProcessesByName(ProcessName).FirstOrDefault();
            return p?.MainModule?.FileName;
        }
        catch { }
        return null;
    }

    // Returns the VRChat.exe path we know about (captured when VRChat is detected).
    internal string GetVrcExePath()
    {
        if (!string.IsNullOrEmpty(config.Optimization.VrcExePath) && File.Exists(config.Optimization.VrcExePath))
            return config.Optimization.VrcExePath;
        return GetVrcExePathLive();
    }

    // Toggles the per-user "Disable Fullscreen Optimizations" compatibility flag
    // for VRChat.exe (HKCU AppCompatFlags\Layers). Reversible.
    internal bool SetFullscreenOpt(bool disable)
    {
        var exe = GetVrcExePath();
        if (exe == null)
        {
            WriteLog("VRChat.exe path unknown - launch VRChat once so it can be detected, then try again.");
            return false;
        }
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers");
            if (disable)
            {
                key.SetValue(exe, "~ DISABLEDXMAXIMIZEDWINDOWEDMODE");
                WriteLog("Fullscreen Optimizations disabled for VRChat.exe.");
            }
            else
            {
                try { key.DeleteValue(exe, false); } catch { }
                WriteLog("Fullscreen Optimizations flag removed for VRChat.exe.");
            }
            return true;
        }
        catch (Exception ex) { WriteLog($"Fullscreen Optimizations change failed: {ex.Message}"); return false; }
    }

    // Toggles Game DVR / background capture for the current user (reversible).
    internal bool SetGameDVR(bool disable)
    {
        int val = disable ? 0 : 1;
        try
        {
            using (var k1 = Registry.CurrentUser.CreateSubKey(@"System\GameConfigStore"))
                k1.SetValue("GameDVR_Enabled", val, RegistryValueKind.DWord);
            using (var k2 = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\GameDVR"))
                k2.SetValue("AppCaptureEnabled", val, RegistryValueKind.DWord);
            WriteLog(disable ? "Game DVR / background capture disabled." : "Game DVR / background capture re-enabled.");
            return true;
        }
        catch (Exception ex) { WriteLog($"Game DVR change failed: {ex.Message}"); return false; }
    }

    // Assembles a Steam launch-options string from the saved LaunchOpts toggles.
    // (Desktop Mode is handled by the app directly, so it's not part of this string.)
    internal string BuildLaunchOptions()
    {
        var lo = config.Optimization.LaunchOpts;
        var parts = new List<string>();
        if (lo.VerboseLogs) parts.Add("--enable-sdk-log-levels");
        if (lo.DebugGui) parts.Add("--enable-debug-gui");
        if (config.Optimization.Affinity.Enabled && config.Optimization.Affinity.Cores > 0)
        {
            int c = config.Optimization.Affinity.Cores;
            long mask = c >= 64 ? -1L : (1L << c) - 1;
            parts.Add($"--affinity=0x{mask:X}");
        }
        if (!string.IsNullOrEmpty(lo.Extra)) parts.Add(lo.Extra.Trim());
        return string.Join(" ", parts);
    }
}
