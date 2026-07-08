using System.Text.Json;

namespace AeroVRC;

// ============================================================================
//  PERSISTENT CONFIG  ->  %APPDATA%\AeroVRC\config.json
//  Faithful port of Get-DefaultConfig / Load-Config / Save-Config. Loading is
//  per-property and tolerant (bad/missing keys keep their defaults) so configs
//  written by the original PowerShell build load unchanged.
// ============================================================================

public class EffectsCfg { public bool Sparkles = true; public string Style = "azure"; public bool LogoAnim = true; }
public class CustomApp { public string Name = ""; public string Type = "steam"; public string Value = ""; public string Icon = ""; }
public class PresetApp { public string Name = ""; public string Type = "steam"; public string Value = ""; }
public class Preset { public string Name = ""; public bool AutoLaunch; public List<PresetApp> Apps = new(); }
public class CrashLoopCfg { public bool Enabled = true; public int MaxCrashes = 5; public int WindowMin = 10; }
public class BreakReminderCfg { public bool Enabled; public int Hours = 3; public bool CloseVRChat; }
public class AutoClearCacheCfg { public bool Enabled; public int IntervalDays = 3; public string LastClear = ""; }
public class OscChatboxCfg { public bool Enabled; public int IntervalSec = 15; public string Template = "VRChat {uptime} | {world} | {players} nearby"; }
public class DiscordRPCfg { public bool Enabled; public string ClientId = ""; }
public class DiskMonitorCfg { public bool Enabled = true; public int MinGB = 5; }
public class ScheduledRestartCfg { public bool Enabled; public int IntervalHours = 6; }
public class FreezeDetectCfg { public bool Enabled = true; public int HangSec = 45; public int LogStallMin = 5; }
public class RamLimitCfg { public bool Enabled; public int MaxGB = 12; }
public class GpuMonitorCfg { public bool Enabled = true; public bool VramAlert; public int VramPct = 92; }
public class TempWarnCfg { public bool Enabled; public int GpuMaxC = 90; public int CpuMaxC = 95; }
public class FpsGuardCfg { public bool Enabled; public int MinFps = 45; public int HoldMin = 3; public int OscPort = 9001; public string Address = "/avatar/parameters/FPS"; }
public class PingMonitorCfg { public bool Enabled = true; public bool Warn; public string Host = "api.vrchat.cloud"; }
public class WorldProfile { public string Name = ""; public string Priority = ""; public int Cores; }
public class PerfModeCfg { public List<string> Apps = new(); }
public class FrameLogCfg { public bool Enabled; }
public class FirstMetEntry { public string T = ""; public string W = ""; }
public class EnabledCfg { public bool Enabled; public EnabledCfg() { } public EnabledCfg(bool on) { Enabled = on; } }
public class CrashArchiveCfg { public bool Enabled = true; public int MaxFiles = 50; }
public class CrasherAlertCfg { public bool Enabled; public int ChurnPer10s = 10; }
public class WorldBlockCfg { public bool Enabled; public bool AutoLeave; }
public class WatchlistNotifyCfg { public bool Enabled = true; public bool Sound = true; }
public class AutoBookmarkCfg { public bool Enabled; public int Hours = 2; }
public class BedtimeCfg
{
    public bool Enabled; public string Time = "02:00"; public string Action = "Sleep";
    public bool CloseVRChat = true; public int WarnMin = 2;
    public bool WindDown; public string WindDownEnd = "08:00"; public bool CloseApps;
}
public class BedtimeMediaCfg { public bool Enabled; public string Type = "mp3"; public string Url = ""; }
public class ScheduledJoinCfg { public bool Enabled; public string Time = "20:00"; public string Target = ""; public string Name = ""; }
public class VrcxCfg { public bool Enabled = true; public string DbPath = ""; public int RefreshSec = 30; }
public class CacheTuningCfg { public bool Enabled; public int SizeGB = 20; public int ExpiryDays = 30; public string Directory = ""; }
public class ProcPriorityCfg { public bool Enabled; public string Level = "AboveNormal"; }        // AboveNormal | High
public class CompanionPriorityCfg { public bool Enabled; public string Level = "BelowNormal"; }   // BelowNormal | Idle
public class AffinityCfg { public bool Enabled; public int Cores; }                               // 0 = all logical cores
public class LaunchOptsCfg { public bool VerboseLogs; public bool DebugGui; public string Extra = ""; }
public class OptimizationCfg
{
    public string VrcExePath = "";
    public CacheTuningCfg CacheTuning = new();
    public ProcPriorityCfg ProcPriority = new();
    public CompanionPriorityCfg CompanionPriority = new();
    public AffinityCfg Affinity = new();
    public EnabledCfg FullscreenOpt = new(false);   // disable Fullscreen Optimizations for VRChat.exe
    public EnabledCfg GameDVR = new(false);         // disable Game DVR / Game Bar capture
    public LaunchOptsCfg LaunchOpts = new();
}
public class StatsCfg
{
    public int TotalRestarts; public string LastRestart = ""; public int LongestSessionSec;
    public string LastCrashReason = ""; public int LongestStreak; public int MaxPlayersSeen;
}
public class GoalsCfg { public int DailyMin = 120; public int WeeklyHours = 15; }
public class ScheduledLaunchCfg { public bool Enabled; public string Time = "18:00"; public bool StartMonitoring = true; }
public class Bookmark
{
    public string Name = ""; public string World = ""; public string InstanceId = "";
    public string Added = ""; public string Note = ""; public bool Pinned;
}
public class SessionRec { public string Start = ""; public string End = ""; public int DurationSec; public int AvgPlayers; }
public class WorldVisit { public string Time = ""; public string World = ""; public string Instance = ""; public int DurationSec; }

public class AppConfig
{
    public int Interval = 5;
    public int Cooldown = 5;
    public bool DesktopMode;
    public bool ShowWelcome = true;
    public EffectsCfg Effects = new();
    public string VdStreamerPath = @"C:\Program Files\Virtual Desktop Streamer\VirtualDesktop.Streamer.exe";
    public string VrcxPath = "";
    public string AmethystPath = "";
    public Dictionary<string, bool> AutoLaunch = new();
    public List<CustomApp> CustomApps = new();         // Name; Type ('steam'|'exe'); Value; Icon
    public List<Preset> Presets = new();               // Name; AutoLaunch; Apps
    public bool SoundAlert = true;
    public bool AutoCloseCompanions;
    public bool AutoRejoin;
    public bool SteamVRAutoLaunch;
    public CrashLoopCfg CrashLoop = new();
    public BreakReminderCfg BreakReminder = new();
    public AutoClearCacheCfg AutoClearCache = new();
    public OscChatboxCfg OscChatbox = new();
    public DiscordRPCfg DiscordRP = new();
    public DiskMonitorCfg DiskMonitor = new();
    public ScheduledRestartCfg ScheduledRestart = new();
    public FreezeDetectCfg FreezeDetect = new();
    public RamLimitCfg RamLimit = new();
    public GpuMonitorCfg GpuMonitor = new();
    public TempWarnCfg TempWarn = new();
    public FpsGuardCfg FpsGuard = new();
    public PingMonitorCfg PingMonitor = new();
    public Dictionary<string, WorldProfile> WorldProfiles = new();   // wrld_id -> per-world perf profile
    public PerfModeCfg PerfMode = new();                             // process names closed by Performance Mode
    public FrameLogCfg FrameLog = new();
    public Dictionary<string, string> PlayerNotes = new();           // player -> private note
    public Dictionary<string, FirstMetEntry> FirstMet = new();       // player -> when/where first met
    public Dictionary<string, string> WatchGroups = new();           // watchlist player -> group name
    public EnabledCfg RejoinOnRestart = new(false);
    public EnabledCfg CrashHints = new(true);
    public CrashArchiveCfg CrashArchive = new();
    public CrasherAlertCfg CrasherAlert = new();
    public WorldBlockCfg WorldBlock = new();
    public List<string> WorldBlockList = new();
    public WatchlistNotifyCfg WatchlistNotify = new();
    public List<string> Watchlist = new();
    public Dictionary<string, int> InstanceTypeHist = new();         // instance-type -> total seconds
    public string HomeWorld = "";
    public AutoBookmarkCfg AutoBookmark = new();
    public Dictionary<string, int> RestartHistory = new();
    public Dictionary<string, int> HourHistogram = new();
    public Dictionary<string, int> PhotoWorlds = new();
    public Dictionary<string, string> WorldNames = new();            // wrld_id -> last known name
    public BedtimeCfg Bedtime = new();
    public BedtimeMediaCfg BedtimeMedia = new();
    public ScheduledJoinCfg ScheduledJoin = new();
    public VrcxCfg Vrcx = new();
    public OptimizationCfg Optimization = new();
    public StatsCfg Stats = new();
    public GoalsCfg Goals = new();
    public Dictionary<string, int> CrashCauses = new();
    public Dictionary<string, int> AvatarUsage = new();
    public ScheduledLaunchCfg ScheduledLaunch = new();
    public Dictionary<string, double> PlayHistory = new();           // yyyy-MM-dd -> minutes played
    public Dictionary<string, int> PlayerSeen = new();
    public List<Bookmark> Bookmarks = new();
    public List<SessionRec> Sessions = new();
    public List<WorldVisit> WorldHistory = new();
}

public static class ConfigStore
{
    // AEROVRC_CONFIGDIR overrides the data folder (test/verification harnesses
    // only - keeps automated runs away from the user's real %APPDATA%\AeroVRC).
    public static readonly string ConfigDir =
        Environment.GetEnvironmentVariable("AEROVRC_CONFIGDIR") is { Length: > 0 } o
            ? o
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AeroVRC");
    public static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    static readonly JsonSerializerOptions SaveOpts = new()
    {
        WriteIndented = true,
        IncludeFields = true,
    };

    // ---- tiny tolerant readers (the PS loader coerced every value; bad values
    //      just keep the default, exactly like the original's per-key try logic) ----
    static bool Has(JsonElement el, string name, out JsonElement v)
    {
        v = default;
        return el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out v) && v.ValueKind != JsonValueKind.Null;
    }
    static int I(JsonElement el, string name, int def)
    {
        if (!Has(el, name, out var v)) return def;
        try
        {
            if (v.ValueKind == JsonValueKind.Number) return (int)Math.Round(v.GetDouble());
            if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var n)) return n;
            if (v.ValueKind == JsonValueKind.True) return 1;
            if (v.ValueKind == JsonValueKind.False) return 0;
        }
        catch { }
        return def;
    }
    static double D(JsonElement el, string name, double def)
    {
        if (!Has(el, name, out var v)) return def;
        try
        {
            if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
            if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), out var n)) return n;
        }
        catch { }
        return def;
    }
    static bool B(JsonElement el, string name, bool def)
    {
        if (!Has(el, name, out var v)) return def;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => v.GetDouble() != 0,
            JsonValueKind.String => string.Equals(v.GetString(), "true", StringComparison.OrdinalIgnoreCase),
            _ => def,
        };
    }
    static string S(JsonElement el, string name, string def)
    {
        if (!Has(el, name, out var v)) return def;
        if (v.ValueKind == JsonValueKind.String) return v.GetString() ?? def;
        return v.ToString();
    }
    // Non-empty string (PS used `if ($j.X)` which skips "" too).
    static string SNe(JsonElement el, string name, string def)
    {
        var s = S(el, name, def);
        return string.IsNullOrEmpty(s) ? def : s;
    }
    static string EStr(JsonElement v) => v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : v.ToString();
    static int EInt(JsonElement v)
    {
        try
        {
            if (v.ValueKind == JsonValueKind.Number) return (int)Math.Round(v.GetDouble());
            if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var n)) return n;
            if (v.ValueKind == JsonValueKind.True) return 1;
        }
        catch { }
        return 0;
    }
    static double EDbl(JsonElement v)
    {
        try
        {
            if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
            if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), out var n)) return n;
        }
        catch { }
        return 0;
    }
    static bool EBool(JsonElement v) =>
        v.ValueKind == JsonValueKind.True ||
        (v.ValueKind == JsonValueKind.String && string.Equals(v.GetString(), "true", StringComparison.OrdinalIgnoreCase)) ||
        (v.ValueKind == JsonValueKind.Number && v.GetDouble() != 0);

    static void MapStrDict(JsonElement root, string name, Dictionary<string, string> target, bool skipEmpty = true)
    {
        if (!Has(root, name, out var el) || el.ValueKind != JsonValueKind.Object) return;
        foreach (var p in el.EnumerateObject())
        {
            var s = EStr(p.Value);
            if (skipEmpty && string.IsNullOrEmpty(s)) continue;
            target[p.Name] = s;
        }
    }
    static void MapIntDict(JsonElement root, string name, Dictionary<string, int> target)
    {
        if (!Has(root, name, out var el) || el.ValueKind != JsonValueKind.Object) return;
        foreach (var p in el.EnumerateObject()) target[p.Name] = EInt(p.Value);
    }
    static void MapDblDict(JsonElement root, string name, Dictionary<string, double> target)
    {
        if (!Has(root, name, out var el) || el.ValueKind != JsonValueKind.Object) return;
        foreach (var p in el.EnumerateObject()) target[p.Name] = EDbl(p.Value);
    }
    static void MapStrList(JsonElement root, string name, List<string> target)
    {
        if (!Has(root, name, out var el) || el.ValueKind != JsonValueKind.Array) return;
        foreach (var v in el.EnumerateArray())
        {
            var s = EStr(v);
            if (!string.IsNullOrEmpty(s)) target.Add(s);
        }
    }

    // Migrate data from an older folder on first run so nothing is lost after a
    // rename (config, crash logs, etc.). Prefer "Azure", then "VRChatWatchdog".
    static void MigrateOldFolder()
    {
        if (File.Exists(ConfigPath)) return;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string oldDir = null;
        foreach (var cand in new[] { Path.Combine(appData, "Azure"), Path.Combine(appData, "VRChatWatchdog") })
        {
            if (File.Exists(Path.Combine(cand, "config.json"))) { oldDir = cand; break; }
        }
        if (oldDir == null) return;
        try
        {
            Directory.CreateDirectory(ConfigDir);
            CopyDirContents(oldDir, ConfigDir);
        }
        catch { }
    }
    static void CopyDirContents(string src, string dst)
    {
        foreach (var f in Directory.GetFiles(src))
        {
            try { File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true); } catch { }
        }
        foreach (var d in Directory.GetDirectories(src))
        {
            var sub = Path.Combine(dst, Path.GetFileName(d));
            try { Directory.CreateDirectory(sub); CopyDirContents(d, sub); } catch { }
        }
    }

    public static AppConfig Load() => Load(ConfigPath, migrate: true);

    public static AppConfig Load(string path, bool migrate = false)
    {
        if (migrate) MigrateOldFolder();
        var cfg = new AppConfig();
        if (!File.Exists(path)) return cfg;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(File.ReadAllText(path)); }
        catch { return cfg; }

        try
        {
            var j = doc.RootElement;
            cfg.Interval = I(j, "Interval", cfg.Interval);
            cfg.Cooldown = I(j, "Cooldown", cfg.Cooldown);
            cfg.DesktopMode = B(j, "DesktopMode", cfg.DesktopMode);
            cfg.ShowWelcome = B(j, "ShowWelcome", cfg.ShowWelcome);
            cfg.VdStreamerPath = SNe(j, "VdStreamerPath", cfg.VdStreamerPath);
            cfg.VrcxPath = SNe(j, "VrcxPath", cfg.VrcxPath);
            cfg.AmethystPath = SNe(j, "AmethystPath", cfg.AmethystPath);
            cfg.SoundAlert = B(j, "SoundAlert", cfg.SoundAlert);
            cfg.AutoCloseCompanions = B(j, "AutoCloseCompanions", cfg.AutoCloseCompanions);
            cfg.AutoRejoin = B(j, "AutoRejoin", cfg.AutoRejoin);
            cfg.SteamVRAutoLaunch = B(j, "SteamVRAutoLaunch", cfg.SteamVRAutoLaunch);

            if (Has(j, "AutoLaunch", out var al) && al.ValueKind == JsonValueKind.Object)
                foreach (var p in al.EnumerateObject()) cfg.AutoLaunch[p.Name] = EBool(p.Value);

            if (Has(j, "CustomApps", out var ca) && ca.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in ca.EnumerateArray())
                {
                    var t = S(a, "Type", "");
                    var nm = S(a, "Name", ""); var val = S(a, "Value", "");
                    if (nm.Length > 0 && val.Length > 0 && (t == "steam" || t == "exe"))
                        cfg.CustomApps.Add(new CustomApp { Name = nm, Type = t, Value = val, Icon = S(a, "Icon", "") });
                }
            }
            if (Has(j, "Presets", out var pr) && pr.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in pr.EnumerateArray())
                {
                    var nm = S(p, "Name", "");
                    if (nm.Length == 0) continue;
                    var preset = new Preset { Name = nm, AutoLaunch = B(p, "AutoLaunch", false) };
                    if (Has(p, "Apps", out var apps) && apps.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var a in apps.EnumerateArray())
                        {
                            var t = S(a, "Type", "");
                            var an = S(a, "Name", ""); var av = S(a, "Value", "");
                            if (an.Length > 0 && av.Length > 0 && (t == "steam" || t == "exe"))
                                preset.Apps.Add(new PresetApp { Name = an, Type = t, Value = av });
                        }
                    }
                    cfg.Presets.Add(preset);
                }
            }

            if (Has(j, "Effects", out var fx))
            {
                cfg.Effects.Sparkles = B(fx, "Sparkles", cfg.Effects.Sparkles);
                cfg.Effects.Style = S(fx, "Style", cfg.Effects.Style);
                cfg.Effects.LogoAnim = B(fx, "LogoAnim", cfg.Effects.LogoAnim);
            }
            if (Has(j, "CrashLoop", out var cl))
            {
                cfg.CrashLoop.Enabled = B(cl, "Enabled", cfg.CrashLoop.Enabled);
                cfg.CrashLoop.MaxCrashes = I(cl, "MaxCrashes", cfg.CrashLoop.MaxCrashes);
                cfg.CrashLoop.WindowMin = I(cl, "WindowMin", cfg.CrashLoop.WindowMin);
            }
            if (Has(j, "BreakReminder", out var br))
            {
                cfg.BreakReminder.Enabled = B(br, "Enabled", cfg.BreakReminder.Enabled);
                cfg.BreakReminder.Hours = I(br, "Hours", cfg.BreakReminder.Hours);
                cfg.BreakReminder.CloseVRChat = B(br, "CloseVRChat", cfg.BreakReminder.CloseVRChat);
            }
            if (Has(j, "AutoClearCache", out var acc))
            {
                cfg.AutoClearCache.Enabled = B(acc, "Enabled", cfg.AutoClearCache.Enabled);
                cfg.AutoClearCache.IntervalDays = I(acc, "IntervalDays", cfg.AutoClearCache.IntervalDays);
                cfg.AutoClearCache.LastClear = S(acc, "LastClear", cfg.AutoClearCache.LastClear);
            }
            if (Has(j, "OscChatbox", out var oc))
            {
                cfg.OscChatbox.Enabled = B(oc, "Enabled", cfg.OscChatbox.Enabled);
                cfg.OscChatbox.IntervalSec = I(oc, "IntervalSec", cfg.OscChatbox.IntervalSec);
                cfg.OscChatbox.Template = S(oc, "Template", cfg.OscChatbox.Template);
            }
            if (Has(j, "DiscordRP", out var drp))
            {
                cfg.DiscordRP.Enabled = B(drp, "Enabled", cfg.DiscordRP.Enabled);
                cfg.DiscordRP.ClientId = S(drp, "ClientId", cfg.DiscordRP.ClientId);
            }
            if (Has(j, "DiskMonitor", out var dm))
            {
                cfg.DiskMonitor.Enabled = B(dm, "Enabled", cfg.DiskMonitor.Enabled);
                cfg.DiskMonitor.MinGB = I(dm, "MinGB", cfg.DiskMonitor.MinGB);
            }
            if (Has(j, "ScheduledRestart", out var sr))
            {
                cfg.ScheduledRestart.Enabled = B(sr, "Enabled", cfg.ScheduledRestart.Enabled);
                cfg.ScheduledRestart.IntervalHours = I(sr, "IntervalHours", cfg.ScheduledRestart.IntervalHours);
            }
            if (Has(j, "FreezeDetect", out var fd))
            {
                cfg.FreezeDetect.Enabled = B(fd, "Enabled", cfg.FreezeDetect.Enabled);
                cfg.FreezeDetect.HangSec = I(fd, "HangSec", cfg.FreezeDetect.HangSec);
                cfg.FreezeDetect.LogStallMin = I(fd, "LogStallMin", cfg.FreezeDetect.LogStallMin);
            }
            if (Has(j, "RamLimit", out var rl))
            {
                cfg.RamLimit.Enabled = B(rl, "Enabled", cfg.RamLimit.Enabled);
                cfg.RamLimit.MaxGB = I(rl, "MaxGB", cfg.RamLimit.MaxGB);
            }
            if (Has(j, "GpuMonitor", out var gm))
            {
                cfg.GpuMonitor.Enabled = B(gm, "Enabled", cfg.GpuMonitor.Enabled);
                cfg.GpuMonitor.VramAlert = B(gm, "VramAlert", cfg.GpuMonitor.VramAlert);
                cfg.GpuMonitor.VramPct = I(gm, "VramPct", cfg.GpuMonitor.VramPct);
            }
            if (Has(j, "TempWarn", out var tw))
            {
                cfg.TempWarn.Enabled = B(tw, "Enabled", cfg.TempWarn.Enabled);
                cfg.TempWarn.GpuMaxC = I(tw, "GpuMaxC", cfg.TempWarn.GpuMaxC);
                cfg.TempWarn.CpuMaxC = I(tw, "CpuMaxC", cfg.TempWarn.CpuMaxC);
            }
            if (Has(j, "FpsGuard", out var fg))
            {
                cfg.FpsGuard.Enabled = B(fg, "Enabled", cfg.FpsGuard.Enabled);
                cfg.FpsGuard.MinFps = I(fg, "MinFps", cfg.FpsGuard.MinFps);
                cfg.FpsGuard.HoldMin = I(fg, "HoldMin", cfg.FpsGuard.HoldMin);
                cfg.FpsGuard.OscPort = I(fg, "OscPort", cfg.FpsGuard.OscPort);
                cfg.FpsGuard.Address = S(fg, "Address", cfg.FpsGuard.Address);
            }
            if (Has(j, "PingMonitor", out var pm))
            {
                cfg.PingMonitor.Enabled = B(pm, "Enabled", cfg.PingMonitor.Enabled);
                cfg.PingMonitor.Warn = B(pm, "Warn", cfg.PingMonitor.Warn);
                cfg.PingMonitor.Host = S(pm, "Host", cfg.PingMonitor.Host);
            }
            if (Has(j, "WorldProfiles", out var wp) && wp.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in wp.EnumerateObject())
                {
                    var prio = S(p.Value, "Priority", "");
                    if (prio.Length == 0) continue;
                    cfg.WorldProfiles[p.Name] = new WorldProfile
                    {
                        Name = S(p.Value, "Name", ""),
                        Priority = prio,
                        Cores = I(p.Value, "Cores", 0),
                    };
                }
            }
            if (Has(j, "PerfMode", out var pfm)) MapStrList(pfm, "Apps", cfg.PerfMode.Apps);
            if (Has(j, "FrameLog", out var fl)) cfg.FrameLog.Enabled = B(fl, "Enabled", cfg.FrameLog.Enabled);
            MapStrDict(j, "PlayerNotes", cfg.PlayerNotes);
            if (Has(j, "FirstMet", out var fm) && fm.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in fm.EnumerateObject())
                {
                    var t = S(p.Value, "T", "");
                    if (t.Length > 0) cfg.FirstMet[p.Name] = new FirstMetEntry { T = t, W = S(p.Value, "W", "") };
                }
            }
            MapStrDict(j, "WatchGroups", cfg.WatchGroups);
            if (Has(j, "RejoinOnRestart", out var ror)) cfg.RejoinOnRestart.Enabled = B(ror, "Enabled", cfg.RejoinOnRestart.Enabled);
            if (Has(j, "CrashHints", out var ch)) cfg.CrashHints.Enabled = B(ch, "Enabled", cfg.CrashHints.Enabled);
            if (Has(j, "CrashArchive", out var carc))
            {
                cfg.CrashArchive.Enabled = B(carc, "Enabled", cfg.CrashArchive.Enabled);
                cfg.CrashArchive.MaxFiles = I(carc, "MaxFiles", cfg.CrashArchive.MaxFiles);
            }
            if (Has(j, "CrasherAlert", out var cra))
            {
                cfg.CrasherAlert.Enabled = B(cra, "Enabled", cfg.CrasherAlert.Enabled);
                cfg.CrasherAlert.ChurnPer10s = I(cra, "ChurnPer10s", cfg.CrasherAlert.ChurnPer10s);
            }
            if (Has(j, "WorldBlock", out var wb))
            {
                cfg.WorldBlock.Enabled = B(wb, "Enabled", cfg.WorldBlock.Enabled);
                cfg.WorldBlock.AutoLeave = B(wb, "AutoLeave", cfg.WorldBlock.AutoLeave);
            }
            MapStrList(j, "WorldBlockList", cfg.WorldBlockList);
            if (Has(j, "WatchlistNotify", out var wn))
            {
                cfg.WatchlistNotify.Enabled = B(wn, "Enabled", cfg.WatchlistNotify.Enabled);
                cfg.WatchlistNotify.Sound = B(wn, "Sound", cfg.WatchlistNotify.Sound);
            }
            MapStrList(j, "Watchlist", cfg.Watchlist);
            MapIntDict(j, "InstanceTypeHist", cfg.InstanceTypeHist);
            cfg.HomeWorld = SNe(j, "HomeWorld", cfg.HomeWorld);
            if (Has(j, "AutoBookmark", out var ab))
            {
                cfg.AutoBookmark.Enabled = B(ab, "Enabled", cfg.AutoBookmark.Enabled);
                cfg.AutoBookmark.Hours = I(ab, "Hours", cfg.AutoBookmark.Hours);
            }
            MapIntDict(j, "RestartHistory", cfg.RestartHistory);
            MapIntDict(j, "HourHistogram", cfg.HourHistogram);
            MapIntDict(j, "PhotoWorlds", cfg.PhotoWorlds);
            MapStrDict(j, "WorldNames", cfg.WorldNames, skipEmpty: false);
            if (Has(j, "Bedtime", out var bt))
            {
                cfg.Bedtime.Enabled = B(bt, "Enabled", cfg.Bedtime.Enabled);
                cfg.Bedtime.Time = S(bt, "Time", cfg.Bedtime.Time);
                cfg.Bedtime.Action = S(bt, "Action", cfg.Bedtime.Action);
                cfg.Bedtime.CloseVRChat = B(bt, "CloseVRChat", cfg.Bedtime.CloseVRChat);
                cfg.Bedtime.WarnMin = I(bt, "WarnMin", cfg.Bedtime.WarnMin);
                cfg.Bedtime.WindDown = B(bt, "WindDown", cfg.Bedtime.WindDown);
                cfg.Bedtime.WindDownEnd = S(bt, "WindDownEnd", cfg.Bedtime.WindDownEnd);
                cfg.Bedtime.CloseApps = B(bt, "CloseApps", cfg.Bedtime.CloseApps);
            }
            if (Has(j, "BedtimeMedia", out var bm))
            {
                cfg.BedtimeMedia.Enabled = B(bm, "Enabled", cfg.BedtimeMedia.Enabled);
                cfg.BedtimeMedia.Type = S(bm, "Type", cfg.BedtimeMedia.Type);
                cfg.BedtimeMedia.Url = S(bm, "Url", cfg.BedtimeMedia.Url);
            }
            if (Has(j, "ScheduledJoin", out var sj))
            {
                cfg.ScheduledJoin.Enabled = B(sj, "Enabled", cfg.ScheduledJoin.Enabled);
                cfg.ScheduledJoin.Time = S(sj, "Time", cfg.ScheduledJoin.Time);
                cfg.ScheduledJoin.Target = S(sj, "Target", cfg.ScheduledJoin.Target);
                cfg.ScheduledJoin.Name = S(sj, "Name", cfg.ScheduledJoin.Name);
            }
            if (Has(j, "Vrcx", out var vx))
            {
                cfg.Vrcx.Enabled = B(vx, "Enabled", cfg.Vrcx.Enabled);
                cfg.Vrcx.DbPath = S(vx, "DbPath", cfg.Vrcx.DbPath);
                cfg.Vrcx.RefreshSec = I(vx, "RefreshSec", cfg.Vrcx.RefreshSec);
            }
            if (Has(j, "Optimization", out var op))
            {
                cfg.Optimization.VrcExePath = SNe(op, "VrcExePath", cfg.Optimization.VrcExePath);
                if (Has(op, "CacheTuning", out var ct))
                {
                    cfg.Optimization.CacheTuning.Enabled = B(ct, "Enabled", cfg.Optimization.CacheTuning.Enabled);
                    cfg.Optimization.CacheTuning.SizeGB = I(ct, "SizeGB", cfg.Optimization.CacheTuning.SizeGB);
                    cfg.Optimization.CacheTuning.ExpiryDays = I(ct, "ExpiryDays", cfg.Optimization.CacheTuning.ExpiryDays);
                    cfg.Optimization.CacheTuning.Directory = S(ct, "Directory", cfg.Optimization.CacheTuning.Directory);
                }
                if (Has(op, "ProcPriority", out var pp))
                {
                    cfg.Optimization.ProcPriority.Enabled = B(pp, "Enabled", cfg.Optimization.ProcPriority.Enabled);
                    cfg.Optimization.ProcPriority.Level = S(pp, "Level", cfg.Optimization.ProcPriority.Level);
                }
                if (Has(op, "CompanionPriority", out var cp))
                {
                    cfg.Optimization.CompanionPriority.Enabled = B(cp, "Enabled", cfg.Optimization.CompanionPriority.Enabled);
                    cfg.Optimization.CompanionPriority.Level = S(cp, "Level", cfg.Optimization.CompanionPriority.Level);
                }
                if (Has(op, "Affinity", out var af))
                {
                    cfg.Optimization.Affinity.Enabled = B(af, "Enabled", cfg.Optimization.Affinity.Enabled);
                    cfg.Optimization.Affinity.Cores = I(af, "Cores", cfg.Optimization.Affinity.Cores);
                }
                if (Has(op, "FullscreenOpt", out var fso)) cfg.Optimization.FullscreenOpt.Enabled = B(fso, "Enabled", cfg.Optimization.FullscreenOpt.Enabled);
                if (Has(op, "GameDVR", out var dvr)) cfg.Optimization.GameDVR.Enabled = B(dvr, "Enabled", cfg.Optimization.GameDVR.Enabled);
                if (Has(op, "LaunchOpts", out var lo))
                {
                    cfg.Optimization.LaunchOpts.VerboseLogs = B(lo, "VerboseLogs", cfg.Optimization.LaunchOpts.VerboseLogs);
                    cfg.Optimization.LaunchOpts.DebugGui = B(lo, "DebugGui", cfg.Optimization.LaunchOpts.DebugGui);
                    cfg.Optimization.LaunchOpts.Extra = S(lo, "Extra", cfg.Optimization.LaunchOpts.Extra);
                }
            }
            if (Has(j, "Stats", out var st))
            {
                cfg.Stats.TotalRestarts = I(st, "TotalRestarts", cfg.Stats.TotalRestarts);
                cfg.Stats.LastRestart = S(st, "LastRestart", cfg.Stats.LastRestart);
                cfg.Stats.LongestSessionSec = I(st, "LongestSessionSec", cfg.Stats.LongestSessionSec);
                cfg.Stats.LastCrashReason = S(st, "LastCrashReason", cfg.Stats.LastCrashReason);
                cfg.Stats.LongestStreak = I(st, "LongestStreak", cfg.Stats.LongestStreak);
                cfg.Stats.MaxPlayersSeen = I(st, "MaxPlayersSeen", cfg.Stats.MaxPlayersSeen);
            }
            if (Has(j, "Goals", out var go))
            {
                cfg.Goals.DailyMin = I(go, "DailyMin", cfg.Goals.DailyMin);
                cfg.Goals.WeeklyHours = I(go, "WeeklyHours", cfg.Goals.WeeklyHours);
            }
            if (Has(j, "ScheduledLaunch", out var sl))
            {
                cfg.ScheduledLaunch.Enabled = B(sl, "Enabled", cfg.ScheduledLaunch.Enabled);
                cfg.ScheduledLaunch.Time = S(sl, "Time", cfg.ScheduledLaunch.Time);
                cfg.ScheduledLaunch.StartMonitoring = B(sl, "StartMonitoring", cfg.ScheduledLaunch.StartMonitoring);
            }
            MapIntDict(j, "CrashCauses", cfg.CrashCauses);
            MapIntDict(j, "AvatarUsage", cfg.AvatarUsage);
            MapDblDict(j, "PlayHistory", cfg.PlayHistory);
            MapIntDict(j, "PlayerSeen", cfg.PlayerSeen);

            if (Has(j, "Bookmarks", out var bms) && bms.ValueKind == JsonValueKind.Array)
            {
                foreach (var b in bms.EnumerateArray())
                {
                    var id = S(b, "InstanceId", "");
                    if (id.Length == 0) continue;
                    cfg.Bookmarks.Add(new Bookmark
                    {
                        Name = S(b, "Name", ""),
                        World = S(b, "World", ""),
                        InstanceId = id,
                        Added = S(b, "Added", ""),
                        Note = S(b, "Note", ""),
                        Pinned = B(b, "Pinned", false),
                    });
                }
            }
            if (Has(j, "Sessions", out var ses) && ses.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in ses.EnumerateArray())
                {
                    var start = S(s, "Start", "");
                    if (start.Length == 0) continue;
                    cfg.Sessions.Add(new SessionRec
                    {
                        Start = start,
                        End = S(s, "End", ""),
                        DurationSec = I(s, "DurationSec", 0),
                        AvgPlayers = I(s, "AvgPlayers", 0),
                    });
                }
            }
            if (Has(j, "WorldHistory", out var wh) && wh.ValueKind == JsonValueKind.Array)
            {
                foreach (var w in wh.EnumerateArray())
                {
                    var world = S(w, "World", "");
                    if (world.Length == 0) continue;
                    cfg.WorldHistory.Add(new WorldVisit
                    {
                        Time = S(w, "Time", ""),
                        World = world,
                        Instance = S(w, "Instance", ""),
                        DurationSec = I(w, "DurationSec", 0),
                    });
                }
            }
        }
        catch { }
        finally { doc.Dispose(); }

        // One-time scrub: drop "Unknown world" entries from history (visits recorded
        // before the world name arrived - typically crash/restart churn).
        cfg.WorldHistory = cfg.WorldHistory.Where(w => !string.IsNullOrEmpty(w.World) && w.World != "Unknown world").ToList();
        cfg.PhotoWorlds.Remove("Unknown world");

        return cfg;
    }

    public static void Save(AppConfig cfg, bool loading)
    {
        if (loading) return;   // mirrors the $script:loading guard
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg, SaveOpts));
        }
        catch { }
    }

    public static string SerializeForExport(AppConfig cfg) => JsonSerializer.Serialize(cfg, SaveOpts);
}
