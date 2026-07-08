using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text.RegularExpressions;

namespace AeroVRC;

// ============================================================================
//  PROCESS METRICS + GPU/VRAM/TEMP MONITOR + PING + CRASH HINTS + BEDTIME
// ============================================================================

public partial class MainForm
{
    // ===== CPU / RAM for the VRChat process =====
    TimeSpan? lastCpuTime;
    DateTime? lastCpuStamp;
    internal int? vrcCpuPct;
    internal int? vrcRamMB;

    // Normalised CPU% (0-100 across all cores) from TotalProcessorTime deltas.
    internal int? GetVrcCpuPercent(Process proc)
    {
        if (proc == null) { lastCpuTime = null; return null; }
        try
        {
            var now = DateTime.Now;
            var cpu = proc.TotalProcessorTime;
            if (lastCpuTime.HasValue && lastCpuStamp.HasValue)
            {
                double wall = (now - lastCpuStamp.Value).TotalMilliseconds;
                double busy = (cpu - lastCpuTime.Value).TotalMilliseconds;
                lastCpuTime = cpu; lastCpuStamp = now;
                if (wall <= 0) return null;
                double pct = busy / wall / Environment.ProcessorCount * 100;
                return (int)Math.Round(Math.Max(0, Math.Min(100, pct)));
            }
            lastCpuTime = cpu; lastCpuStamp = now;
            return null;
        }
        catch { return null; }
    }

    // ===== GPU / VRAM / temperature monitor =====
    // NOTE: "GPU Engine" perf counters are deliberately NOT used here - on machines
    // with many GPU engine instances they take ~1s per query and stall the UI.
    // Instead we read from one of two cheap sources:
    //   1. LibreHardwareMonitorLib.dll (optional; dropped into %APPDATA%\AeroVRC via
    //      the Settings download button) - all GPU brands, plus temperatures.
    //   2. nvidia-smi (ships with NVIDIA drivers) - run ASYNCHRONOUSLY: we launch it
    //      hidden, keep ticking, and harvest the output on a later tick.
    internal class GpuStatsData
    {
        public double? Util;
        public int? VramMB;
        public int? VramTotMB;
        public double? TempC;
        public double? CpuTempC;
    }

    object hwComp;      // LibreHardwareMonitor Computer object (reflection-loaded)
    object hwGpu;       // chosen GPU IHardware
    object hwCpu;       // CPU IHardware (temps need admin rights to read)
    internal string hwSource = "";   // "LibreHardwareMonitor" | "nvidia-smi" | ""
    string nvsmiPath;
    internal Process nvsmiProc;      // in-flight async nvidia-smi poll
    DateTime nvsmiStarted = DateTime.Now;
    internal GpuStatsData gpuStats = new();

    internal void InitializeHwMonitor()
    {
        if (hwComp != null) { try { ((dynamic)hwComp).Close(); } catch { } }
        hwComp = null; hwGpu = null; hwCpu = null; hwSource = "";
        gpuStats = new GpuStatsData();
        if (!config.GpuMonitor.Enabled) return;
        // Preferred: LibreHardwareMonitor library (works for NVIDIA / AMD / Intel and
        // exposes temperatures; GPU sensors don't need admin rights, CPU temps do).
        var dll = Path.Combine(ConfigStore.ConfigDir, "LibreHardwareMonitorLib.dll");
        if (File.Exists(dll))
        {
            try
            {
                var asm = Assembly.LoadFrom(dll);
                var compType = asm.GetType("LibreHardwareMonitor.Hardware.Computer", true);
                dynamic comp = Activator.CreateInstance(compType);
                comp.IsGpuEnabled = true;
                comp.IsCpuEnabled = true;
                comp.Open();
                object gpu = null, cpu = null;
                foreach (dynamic hw in comp.Hardware)
                {
                    string ht = hw.HardwareType.ToString();
                    if ((ht == "GpuNvidia" || ht == "GpuAmd") && (gpu == null || ((dynamic)gpu).HardwareType.ToString() == "GpuIntel")) gpu = hw;
                    else if (ht == "GpuIntel" && gpu == null) gpu = hw;
                    else if (ht == "Cpu" && cpu == null) cpu = hw;
                }
                if (gpu != null || cpu != null)
                {
                    hwComp = comp; hwGpu = gpu; hwCpu = cpu;
                    hwSource = "LibreHardwareMonitor";
                    WriteLog($"HW monitor: using LibreHardwareMonitor ({(gpu != null ? (string)((dynamic)gpu).Name : "no GPU found")}).");
                    return;
                }
                comp.Close();
            }
            catch (Exception ex) { WriteLog($"HW monitor: LibreHardwareMonitor failed to load - {ex.Message}"); }
        }
        // Fallback: nvidia-smi (NVIDIA only, no download needed).
        var cand = new List<string>();
        try
        {
            var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';');
            foreach (var d in pathDirs)
            {
                if (d.Trim().Length == 0) continue;
                var f = Path.Combine(d.Trim(), "nvidia-smi.exe");
                if (File.Exists(f)) { cand.Add(f); break; }
            }
        }
        catch { }
        cand.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\nvidia-smi.exe"));
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(pf)) cand.Add(Path.Combine(pf, @"NVIDIA Corporation\NVSMI\nvidia-smi.exe"));
        foreach (var p in cand)
        {
            if (!string.IsNullOrEmpty(p) && File.Exists(p)) { nvsmiPath = p; hwSource = "nvidia-smi"; return; }
        }
    }

    internal void UpdateGpuStats()
    {
        if (hwSource == "LibreHardwareMonitor")
        {
            if (hwGpu != null)
            {
                try
                {
                    dynamic gpu = hwGpu;
                    gpu.Update();
                    double? util = null, d3d = null, used = null, tot = null, temp = null;
                    foreach (dynamic s in gpu.Sensors)
                    {
                        if (s.Value == null) continue;
                        string st = s.SensorType.ToString(); string n = (string)s.Name;
                        if (st == "Load" && n == "GPU Core") util = (double)s.Value;
                        else if (st == "Load" && n == "D3D 3D") d3d = (double)s.Value;
                        else if (st == "SmallData" && n == "GPU Memory Used") used = (double)s.Value;
                        else if (st == "SmallData" && n == "GPU Memory Total") tot = (double)s.Value;
                        else if (st == "Temperature" && n == "GPU Core") temp = (double)s.Value;
                    }
                    util ??= d3d;
                    gpuStats.Util = util;
                    gpuStats.VramMB = used.HasValue ? (int)used.Value : null;
                    gpuStats.VramTotMB = tot.HasValue ? (int)tot.Value : null;
                    gpuStats.TempC = temp;
                }
                catch { }
            }
            // CPU package temperature (only readable when running elevated; stays
            // blank otherwise - the settings section says so).
            if (hwCpu != null)
            {
                try
                {
                    dynamic cpu = hwCpu;
                    cpu.Update();
                    double? ct = null;
                    foreach (dynamic s in cpu.Sensors)
                    {
                        if (s.Value == null) continue;
                        if (s.SensorType.ToString() != "Temperature") continue;
                        string n = (string)s.Name;
                        if (n == "CPU Package" || n == "Core (Tctl/Tdie)") { ct = (double)s.Value; break; }
                        ct ??= (double)s.Value;
                    }
                    gpuStats.CpuTempC = ct;
                }
                catch { }
            }
            return;
        }
        if (hwSource != "nvidia-smi") return;
        // Harvest the previous async poll (never block on it)...
        if (nvsmiProc != null)
        {
            try
            {
                if (nvsmiProc.HasExited)
                {
                    var line = nvsmiProc.StandardOutput.ReadLine();
                    nvsmiProc.Dispose(); nvsmiProc = null;
                    if (!string.IsNullOrEmpty(line))
                    {
                        try
                        {
                            var f = line.Split(',').Select(x => x.Trim()).ToArray();
                            if (f.Length >= 4)
                            {
                                gpuStats.Util = double.Parse(f[0]);
                                gpuStats.VramMB = int.Parse(f[1]);
                                gpuStats.VramTotMB = int.Parse(f[2]);
                                gpuStats.TempC = double.Parse(f[3]);
                            }
                        }
                        catch { }   // "[N/A]" fields etc.
                    }
                }
                else if ((DateTime.Now - nvsmiStarted).TotalSeconds > 30)
                {
                    try { nvsmiProc.Kill(); } catch { }
                    nvsmiProc.Dispose(); nvsmiProc = null;
                }
            }
            catch
            {
                try { nvsmiProc?.Dispose(); } catch { }
                nvsmiProc = null;
            }
        }
        // ...and start the next one.
        if (nvsmiProc == null)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = nvsmiPath,
                    Arguments = "--query-gpu=utilization.gpu,memory.used,memory.total,temperature.gpu --format=csv,noheader,nounits",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };
                nvsmiProc = Process.Start(psi);
                nvsmiStarted = DateTime.Now;
            }
            catch { nvsmiProc = null; hwSource = ""; }
        }
    }

    internal void CloseHwMonitor()
    {
        if (hwComp != null) { try { ((dynamic)hwComp).Close(); } catch { } }
    }

    // ===== Ping / connection health =====
    // Async ICMP ping to VRChat's API host every ~5s (SendPingAsync - the UI never
    // waits on it). A rolling 12-sample window drives a health verdict: packet loss
    // => "Degrading", a big latency spike vs the best recent RTT => "Unstable".
    // Three consecutive degraded verdicts = disconnect likely (warning is opt-in).
    internal Ping pingSender;
    Task<PingReply> pingTask;
    internal readonly List<(bool Ok, int? Ms)> pingHist = new();
    internal int? pingMs;
    internal string netState = "";      // "" | Stable | Unstable | Degrading
    internal int netBadStreak;
    internal DateTime lastNetWarn = DateTime.MinValue;

    internal void UpdatePingMonitor()
    {
        pingSender ??= new Ping();
        // Harvest the previous async ping (skip the round if still in flight).
        if (pingTask != null)
        {
            if (!pingTask.IsCompleted) return;
            bool ok = false; int? ms = null;
            try
            {
                if (!pingTask.IsFaulted && pingTask.Result != null)
                {
                    var r = pingTask.Result;
                    ok = r.Status == IPStatus.Success;
                    if (ok) ms = (int)r.RoundtripTime;
                }
            }
            catch { }
            pingTask = null;
            pingHist.Add((ok, ms));
            while (pingHist.Count > 12) pingHist.RemoveAt(0);
            pingMs = ms;
            if (pingHist.Count >= 6)
            {
                int fails = 0, okc = 0; int? best = null; double sum = 0;
                foreach (var h in pingHist)
                {
                    if (!h.Ok) { fails++; continue; }
                    okc++; sum += h.Ms ?? 0;
                    if (best == null || h.Ms < best) best = h.Ms;
                }
                double lossPct = 100.0 * fails / pingHist.Count;
                double? avg = okc > 0 ? sum / okc : null;
                netState =
                    lossPct >= 25 || avg == null ? "Degrading" :
                    avg > Math.Max(120, 2.5 * (best ?? 0)) ? "Unstable" :
                    "Stable";
                if (netState == "Degrading") netBadStreak++; else netBadStreak = 0;
            }
        }
        // Fire the next ping.
        try { pingTask = pingSender.SendPingAsync(config.PingMonitor.Host, 3000); }
        catch { pingTask = null; }
    }

    // ========================================================================
    //  CRASH DIAGNOSTICS  (scan the tail of VRChat's log for the last error)
    // ========================================================================
    static readonly Regex reLogError = new(@"(?i)(exception|error|fatal|assert|out of memory|d3d|vulkan|gpu (?:device )?(?:removed|hung|lost)|stack trace)", RegexOptions.Compiled);

    internal string GetCrashCauseHint()
    {
        if (string.IsNullOrEmpty(vrcLogPath) || !File.Exists(vrcLogPath)) return "";
        string text;
        try
        {
            using var fs = File.Open(vrcLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long start = Math.Max(0, fs.Length - 131072);   // last 128 KB
            fs.Seek(start, SeekOrigin.Begin);
            using var sr = new StreamReader(fs);
            text = sr.ReadToEnd();
        }
        catch { return ""; }
        var hit = "";
        foreach (var line in text.Split('\n'))
        {
            var t = line.Trim();
            if (t.Length >= 6 && reLogError.IsMatch(t)) hit = t;
        }
        if (hit.Length > 200) hit = hit[..200] + "...";
        return hit;
    }

    // Bucket a crash-hint line into a broad category for the analytics breakdown.
    internal static string GetCrashCategory(string hint)
    {
        if (string.IsNullOrEmpty(hint)) return "";
        var h = hint.ToLower();
        if (Regex.IsMatch(h, "out of memory|outofmemory|bad_alloc")) return "Out of memory";
        if (Regex.IsMatch(h, @"d3d|dxgi|vulkan|gpu (?:device )?(?:removed|hung|lost)|device removed|display driver")) return "GPU / graphics";
        if (Regex.IsMatch(h, "disconnect|lost connection|connection to|socket|network|timed out")) return "Network";
        if (Regex.IsMatch(h, "exception|stack trace|fatal|assert|access violation|nullreference")) return "Crash / exception";
        return "Other";
    }

    // ========================================================================
    //  BEDTIME / SLEEP TIMER
    // ========================================================================
    internal DateTime? windDownUntil;
    internal readonly string bedtimeMusicDir = Path.Combine(ConfigStore.ConfigDir, "BedtimeMusic");
    internal string bedtimeWarnedDate;
    internal string bedtimeFiredDate;

    // Blocks VRChat relaunch/reopen until the wind-down end time (next morning).
    internal void StartWindDown()
    {
        var now = DateTime.Now;
        DateTime end;
        if (DateTime.TryParse(config.Bedtime.WindDownEnd, out var t))
        {
            end = now.Date.AddHours(t.Hour).AddMinutes(t.Minute);
            if (end <= now) end = end.AddDays(1);
        }
        else end = now.AddHours(8);
        windDownUntil = end;
        WriteLog($"Wind-down active until {end:HH:mm} - VRChat won't relaunch or reopen.");
    }

    // Plays the chosen bedtime media (a YouTube link, or a random mp3 from the folder).
    internal void PlayBedtimeMedia()
    {
        if (!config.BedtimeMedia.Enabled) return;
        if (config.BedtimeMedia.Type == "youtube")
        {
            var url = (config.BedtimeMedia.Url ?? "").Trim();
            if (url.Length > 0)
            {
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); WriteLog("Bedtime: opened YouTube video."); }
                catch { WriteLog("Bedtime: couldn't open the video URL."); }
            }
            else WriteLog("Bedtime: no YouTube URL set.");
        }
        else
        {
            try { Directory.CreateDirectory(bedtimeMusicDir); } catch { }
            var mp3s = Directory.Exists(bedtimeMusicDir)
                ? new DirectoryInfo(bedtimeMusicDir).GetFiles("*.mp3")
                : Array.Empty<FileInfo>();
            if (mp3s.Length > 0)
            {
                var pick = mp3s[Random.Shared.Next(mp3s.Length)];
                try { Process.Start(new ProcessStartInfo(pick.FullName) { UseShellExecute = true }); WriteLog($"Bedtime: playing {pick.Name}."); } catch { }
            }
            else WriteLog("Bedtime: no mp3 files in the bedtime music folder (Settings > Bedtime > Open music folder).");
        }
    }

    internal void InvokeBedtimeAction()
    {
        var act = config.Bedtime.Action;
        if (config.Bedtime.CloseVRChat)
        {
            try
            {
                var p = Process.GetProcessesByName(ProcessName).FirstOrDefault();
                if (p != null) { p.CloseMainWindow(); Thread.Sleep(500); }
            }
            catch { }
        }
        if (config.Bedtime.CloseApps) CloseCompanions();
        if (config.Bedtime.WindDown) StartWindDown();
        PlayBedtimeMedia();
        switch (act)
        {
            case "Sleep":
                WriteLog("Bedtime: sleeping the PC.");
                Application.SetSuspendState(PowerState.Suspend, false, false);
                break;
            case "Hibernate":
                WriteLog("Bedtime: hibernating the PC.");
                Application.SetSuspendState(PowerState.Hibernate, false, false);
                break;
            case "Shutdown":
                WriteLog("Bedtime: shutting down.");
                Process.Start(new ProcessStartInfo("shutdown.exe", "/s /t 30 /c \"AeroVRC bedtime shutdown\"") { WindowStyle = ProcessWindowStyle.Hidden, UseShellExecute = true });
                break;
            default:
                WriteLog("Bedtime: closed VRChat (no power action).");
                break;
        }
    }

    // Fires the bedtime action once per day at the configured wall-clock time, with
    // an advance warning. Only acts within a short window after the target so it
    // won't trigger retroactively if the app is opened long after bedtime.
    internal void CheckBedtime()
    {
        if (!DateTime.TryParse(config.Bedtime.Time, out var t)) return;
        var now = DateTime.Now;
        var target = now.Date.AddHours(t.Hour).AddMinutes(t.Minute);
        var today = now.ToString("yyyy-MM-dd");
        int warnMin = Math.Max(0, config.Bedtime.WarnMin);

        // Advance warning (once per day).
        if (warnMin > 0 && bedtimeWarnedDate != today)
        {
            var warnAt = target.AddMinutes(-warnMin);
            if (now >= warnAt && now < target)
            {
                bedtimeWarnedDate = today;
                WriteLog($"Bedtime in {warnMin} min - PC will {config.Bedtime.Action.ToLower()}.");
                if (config.SoundAlert) System.Media.SystemSounds.Asterisk.Play();
                if (config.OscChatbox.Enabled) SendOscChatbox($"Bedtime in {warnMin} min...");
            }
        }

        // Fire (once per day) within a 15-minute window after the target.
        if (bedtimeFiredDate != today && now >= target && now < target.AddMinutes(15))
        {
            bedtimeFiredDate = today;
            if (monitoring) ToggleMonitoring();   // stop so we don't relaunch VRChat
            InvokeBedtimeAction();
        }
    }
}
