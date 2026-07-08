using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AeroVRC;

// ============================================================================
//  LAUNCHERS + VRCHAT LAUNCH + COMPANION MANAGEMENT + MAINTENANCE
// ============================================================================

public partial class MainForm
{
    internal void StartSteamApp(string appId, string displayName)
    {
        WriteLog($"Launching {displayName} via Steam (App ID {appId})...");
        SetAppsStatus($"Launching {displayName}...");
        Process.Start(new ProcessStartInfo($"steam://run/{appId}") { UseShellExecute = true });
    }

    internal void StartExeApp(string path, string displayName)
    {
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            WriteLog($"Launching {displayName}...");
            SetAppsStatus($"Launching {displayName}...");
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        else
        {
            WriteLog($"{displayName} not found at: {path}");
            SetAppsStatus($"{displayName} not found - check its path.");
            MessageBox.Show($"Could not find the executable for {displayName} at:\n\n{path}",
                "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // Launches one app entry via the right mechanism.
    internal void LaunchAppEntry(CustomApp app)
    {
        if (app == null) return;
        if (app.Type == "steam") StartSteamApp(app.Value, app.Name);
        else StartExeApp(app.Value, app.Name);
    }
    internal void LaunchAppEntry(PresetApp app)
    {
        if (app == null) return;
        if (app.Type == "steam") StartSteamApp(app.Value, app.Name);
        else StartExeApp(app.Value, app.Name);
    }

    // Loads an image from disk into a detached Bitmap (no file lock, so the source
    // file stays movable/deletable). Returns null on any problem.
    internal static Bitmap LoadIconImage(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        try
        {
            var bytes = File.ReadAllBytes(path);
            using var ms = new MemoryStream(bytes);
            using var img = Image.FromStream(ms);
            // Copy into a standalone bitmap so we can close the stream immediately.
            return new Bitmap(img);
        }
        catch { return null; }
    }

    // ========================================================================
    //  VRCHAT LAUNCH  (single entry point - honours Desktop Mode everywhere)
    // ========================================================================
    // Resolves the Steam client executable (cached after first lookup).
    string steamExe;
    internal string GetSteamExe()
    {
        if (steamExe != null && File.Exists(steamExe)) return steamExe;
        foreach (var k in new[] { @"HKEY_CURRENT_USER\Software\Valve\Steam", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam" })
        {
            try
            {
                string cand = null;
                if (Registry.GetValue(k, "SteamExe", null) is string se && se.Length > 0) cand = se;
                else if (Registry.GetValue(k, "SteamPath", null) is string sp && sp.Length > 0) cand = Path.Combine(sp, "steam.exe");
                else if (Registry.GetValue(k, "InstallPath", null) is string ip && ip.Length > 0) cand = Path.Combine(ip, "steam.exe");
                if (cand != null && File.Exists(cand)) { steamExe = cand; return cand; }
            }
            catch { }
        }
        var g = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Steam\steam.exe");
        if (File.Exists(g)) { steamExe = g; return g; }
        return null;
    }

    internal string pendingJoinId = "";

    // Launches VRChat, optionally into a specific instance. Desktop Mode applies to
    // every path. Rules that matter for VRChat specifically:
    //   1. VRChat (Easy Anti-Cheat) must be launched THROUGH Steam. We use
    //      "steam.exe -applaunch 438100 [--no-vr] [vrchat://...]" - launch args
    //      passed this way reach the game reliably, so ONE process starts in the
    //      right mode, directly in the target instance.
    //   2. NEVER fire a vrchat:// URL while VRChat is running: the protocol handler
    //      does not join the live session - it boots a SECOND copy of the game. If
    //      a rejoin is requested while VRChat runs, we close the session and
    //      relaunch into the instance instead (timer close edge via pendingJoinId).
    internal void StartVRChat(string instanceId = "")
    {
        bool desktop = config.DesktopMode;
        string modeName = desktop ? "Desktop" : "VR";

        // Already running + rejoin requested: close the session first; the timer's
        // close edge relaunches straight into the instance.
        if (!string.IsNullOrEmpty(instanceId) && Process.GetProcessesByName(ProcessName).Length > 0)
        {
            pendingJoinId = instanceId;
            WriteLog("Restarting VRChat into the saved instance (closing the current session first)...");
            try { Process.GetProcessesByName(ProcessName).FirstOrDefault()?.CloseMainWindow(); } catch { }
            return;
        }

        // Cold launch through Steam; the join URL rides along as a launch argument.
        var steam = GetSteamExe();
        if (steam != null)
        {
            var a = new List<string> { "-applaunch", SteamAppId };
            if (desktop) a.Add("--no-vr");
            if (!string.IsNullOrEmpty(instanceId)) a.Add($"vrchat://launch?ref=vrchat.com&id={instanceId}");
            if (!string.IsNullOrEmpty(instanceId)) WriteLog($"Launching VRChat into the saved instance ({modeName} Mode)...");
            else WriteLog($"Launching VRChat via Steam ({modeName} Mode)...");
            var psi = new ProcessStartInfo(steam) { UseShellExecute = true };
            foreach (var arg in a) psi.ArgumentList.Add(arg);
            Process.Start(psi);
        }
        else
        {
            // Fallback if steam.exe can't be found (rare); plain launch only.
            if (!string.IsNullOrEmpty(instanceId)) WriteLog("steam.exe not found - launching without the instance rejoin.");
            var url = desktop ? $"steam://run/{SteamAppId}//--no-vr" : $"steam://run/{SteamAppId}";
            WriteLog($"Launching VRChat ({modeName} Mode via Steam URL)...");
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        pendingJoinId = "";
    }

    // Extracts a "wrld_xxx[:instance]" id from any form the user might paste:
    // a vrchat.com share link, a vrchat:// launch URL, or a raw world/instance id.
    internal static string ConvertVrcLink(string text)
    {
        var t = (text ?? "").Trim();
        if (t.Length == 0) return null;
        var m = Regex.Match(t, @"worldId=(wrld_[A-Za-z0-9\-]+)");
        if (m.Success)
        {
            var mi = Regex.Match(t, @"instanceId=([^&\s]+)");
            if (mi.Success) return m.Groups[1].Value + ":" + Uri.UnescapeDataString(mi.Groups[1].Value);
            return m.Groups[1].Value;
        }
        m = Regex.Match(t, @"id=(wrld_[A-Za-z0-9\-]+(?::[^&\s]+)?)");
        if (m.Success) return Uri.UnescapeDataString(m.Groups[1].Value);
        m = Regex.Match(t, @"^(wrld_[A-Za-z0-9\-]+(?::\S+)?)$");
        if (m.Success) return m.Groups[1].Value;
        // Last resort: a vrchat.com URL with a world id somewhere in it - world-only.
        if (t.Contains("vrchat", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(t, "vrchat"))
        {
            m = Regex.Match(t, @"(wrld_[A-Za-z0-9\-]+)");
            if (m.Success) return m.Groups[1].Value;
        }
        return null;
    }

    // Human-readable instance access type from the instance id's tags.
    internal static string GetInstanceType(string id)
    {
        if (string.IsNullOrEmpty(id) || !id.Contains(':')) return "";
        var inst = id.Split(':', 2)[1];
        if (inst.Contains("~group("))
        {
            if (inst.Contains("groupAccessType(public)")) return "Group Public";
            if (inst.Contains("groupAccessType(plus)")) return "Group+";
            return "Group";
        }
        if (inst.Contains("~hidden(")) return "Friends+";
        if (inst.Contains("~friends(")) return "Friends";
        if (inst.Contains("~canRequestInvite")) return "Invite+";
        if (inst.Contains("~private(")) return "Invite";
        return "Public";
    }

    // Saves any chart panel as a PNG (DrawToBitmap replays the Paint handler).
    internal void ExportPanelPng(Control panel, string defaultName)
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "PNG images (*.png)|*.png|All files (*.*)|*.*",
            FileName = defaultName,
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        try
        {
            using var bmp = new Bitmap(panel.Width, panel.Height);
            panel.DrawToBitmap(bmp, new Rectangle(0, 0, panel.Width, panel.Height));
            bmp.Save(dlg.FileName, System.Drawing.Imaging.ImageFormat.Png);
            WriteLog($"Chart exported: {dlg.FileName}");
        }
        catch (Exception ex) { WriteLog($"Chart export failed: {ex.Message}"); }
    }

    // Saves the tail of the current VRChat log to a timestamped file so crash
    // history survives VRChat's log rotation. Pruned to CrashArchive.MaxFiles.
    internal readonly string crashLogDir = Path.Combine(ConfigStore.ConfigDir, "CrashLogs");
    internal void SaveCrashLogArchive()
    {
        if (!config.CrashArchive.Enabled) return;
        if (string.IsNullOrEmpty(vrcLogPath) || !File.Exists(vrcLogPath)) return;
        try
        {
            Directory.CreateDirectory(crashLogDir);
            string text;
            using (var fs = File.Open(vrcLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long start = Math.Max(0, fs.Length - 524288);   // last 512 KB
                fs.Seek(start, SeekOrigin.Begin);
                using var sr = new StreamReader(fs);
                text = sr.ReadToEnd();
            }
            var outPath = Path.Combine(crashLogDir, $"vrchat_log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
            File.WriteAllText(outPath, text);
            WriteLog($"Log tail archived: {outPath}");
            // prune oldest beyond the cap
            int max = Math.Max(5, config.CrashArchive.MaxFiles);
            var files = new DirectoryInfo(crashLogDir).GetFiles("vrchat_log_*.txt").OrderBy(f => f.LastWriteTime).ToList();
            if (files.Count > max)
                foreach (var f in files.Take(files.Count - max))
                    try { f.Delete(); } catch { }
        }
        catch (Exception ex) { WriteLog($"Log archive failed: {ex.Message}"); }
    }

    // True when the current world matches an entry in the block list. Entries are
    // either wrld_ ids (matched against the instance id) or partial world names.
    internal bool TestWorldBlocked()
    {
        foreach (var e in config.WorldBlockList)
        {
            var t = (e ?? "").Trim();
            if (t.Length == 0) continue;
            if (t.StartsWith("wrld_"))
            {
                if (!string.IsNullOrEmpty(currentInstanceId) && currentInstanceId.StartsWith(t)) return true;
            }
            else if (!string.IsNullOrEmpty(currentWorld) && currentWorld.Contains(t, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    // Launches every app in a saved preset.
    internal void LaunchPreset(Preset preset)
    {
        if (preset == null) return;
        WriteLog($"Preset '{preset.Name}': launching {preset.Apps.Count} app(s)...");
        foreach (var app in preset.Apps) LaunchAppEntry(app);
        SetAppsStatus($"Preset '{preset.Name}' launched.");
    }

    // Runs on monitoring start (and scheduled launch): opens every individual app
    // and every preset that has Auto-launch ticked. Reads config directly so it
    // works even if the Apps page was never opened.
    internal void InvokeAutoLaunch()
    {
        foreach (var app in config.CustomApps)
        {
            if (config.AutoLaunch.TryGetValue(app.Name, out var on) && on)
            {
                WriteLog($"Auto-launching {app.Name}...");
                LaunchAppEntry(app);
            }
        }
        foreach (var p in config.Presets)
        {
            if (p.AutoLaunch) LaunchPreset(p);
        }
    }

    // ========================================================================
    //  COMPANION PROCESS MANAGEMENT  (auto-close / panic)
    // ========================================================================
    // Collects process base-names for exe-based companions we can identify.
    internal List<string> GetCompanionProcessNames()
    {
        var names = new List<string>();
        var paths = new List<string> { vdStreamerPath, config.VrcxPath, config.AmethystPath };
        foreach (var a in config.CustomApps)
            if (a.Type == "exe") paths.Add(a.Value);
        foreach (var p in paths)
        {
            if (!string.IsNullOrEmpty(p) && File.Exists(p))
            {
                var bn = Path.GetFileNameWithoutExtension(p);
                if (!string.IsNullOrEmpty(bn) && !names.Contains(bn)) names.Add(bn);
            }
        }
        return names;
    }

    internal void CloseCompanions()
    {
        int closed = 0;
        foreach (var n in GetCompanionProcessNames())
        {
            try
            {
                foreach (var pr in Process.GetProcessesByName(n)) { pr.CloseMainWindow(); closed++; }
            }
            catch { }
        }
        if (closed > 0) WriteLog($"Auto-closed {closed} companion process(es).");
    }

    internal void InvokePanic()
    {
        var r = MessageBox.Show(
            "This will force-close VRChat and all identifiable companion apps.\n\nContinue?",
            "Panic - Close Everything", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (r != DialogResult.Yes) return;
        if (monitoring) ToggleMonitoring();   // stop first so we don't relaunch
        int killed = 0;
        var targets = new List<string> { ProcessName };
        targets.AddRange(GetCompanionProcessNames());
        foreach (var n in targets)
        {
            try
            {
                foreach (var pr in Process.GetProcessesByName(n)) { pr.Kill(); killed++; }
            }
            catch { }
        }
        WriteLog($"Panic: force-closed {killed} process(es).");
        SetAppsStatus($"Panic: closed {killed} process(es).");
    }

    // ========================================================================
    //  MAINTENANCE  (cache / disk)
    // ========================================================================
    internal bool ClearVRChatCache()
    {
        if (Process.GetProcessesByName(ProcessName).Length > 0)
        {
            WriteLog("Cannot clear cache while VRChat is running.");
            return false;
        }
        if (!Directory.Exists(vrcCacheDir))
        {
            WriteLog("VRChat cache folder not found (nothing to clear).");
            return false;
        }
        // Safety: only ever touch a path that really is the VRChat cache folder.
        if (!vrcCacheDir.Contains("VRChat") || !vrcCacheDir.Contains("Cache-WindowsPlayer"))
        {
            WriteLog("Cache path failed validation; aborting.");
            return false;
        }
        try
        {
            var di = new DirectoryInfo(vrcCacheDir);
            foreach (var f in di.GetFiles()) { try { f.Delete(); } catch { } }
            foreach (var d in di.GetDirectories()) { try { d.Delete(true); } catch { } }
            config.AutoClearCache.LastClear = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            SaveConfig();
            WriteLog("VRChat cache cleared.");
            return true;
        }
        catch (Exception ex)
        {
            WriteLog($"Cache clear failed: {ex.Message}");
            return false;
        }
    }

    internal double? GetDiskFreeGB()
    {
        try
        {
            var root = Path.GetPathRoot(vrcLowDir);
            var di = new DriveInfo(root);
            return Math.Round(di.AvailableFreeSpace / 1073741824.0, 1);
        }
        catch { return null; }
    }

    internal int GetPhotoCount()
    {
        if (!sessionStart.HasValue || !Directory.Exists(photoDir)) return 0;
        try
        {
            var di = new DirectoryInfo(photoDir);
            return di.EnumerateFiles("*.png", SearchOption.AllDirectories)
                     .Concat(di.EnumerateFiles("*.jpg", SearchOption.AllDirectories))
                     .Count(f => f.LastWriteTime >= sessionStart.Value);
        }
        catch { return 0; }
    }
}
