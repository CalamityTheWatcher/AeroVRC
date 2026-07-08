using System.Text;
using System.Text.RegularExpressions;

namespace AeroVRC;

// ============================================================================
//  VRChat LOG PARSING  (world / instance / players / disconnect)
// ============================================================================

public partial class MainForm
{
    internal class RosterEntry { public string Name; public DateTime Join; public DateTime? Leave; }
    internal class RosterMeta { public string World; public string Instance; public DateTime Joined; }

    internal string vrcLogPath;
    internal long vrcLogPos;
    internal string currentWorld = "";
    internal string currentInstance = "";
    internal string currentInstanceId = "";
    internal DateTime? worldJoinTime;
    internal readonly List<string> players = new();
    internal readonly Dictionary<string, DateTime> playerJoinTimes = new();
    internal bool pendingRejoin;
    internal int rejoinCooldown;
    internal readonly List<DateTime> churnTimes = new();
    internal DateTime lastCrasherAlert = DateTime.MinValue;
    internal int avatarSwitches;
    internal DateTime lastLogGrowth = DateTime.MinValue;
    internal string blockAlertKey;
    internal bool blockLeaving;

    // Instance roster (who was present + join/leave stamps; export via Who's Here)
    internal readonly List<RosterEntry> rosterLog = new();
    internal RosterMeta rosterMeta;

    // Frame-time history (fed by the OSC FPS feed; drawn on the Dashboard)
    internal readonly List<double> frameHist = new();          // ms per frame, 1 sample/s
    internal readonly StringBuilder frameLogBuf = new();       // pending CSV lines
    internal string frameLogDir = Path.Combine(ConfigStore.ConfigDir, "FrameLogs");

    internal void FlushFrameLog()
    {
        if (frameLogBuf.Length == 0) return;
        try
        {
            Directory.CreateDirectory(frameLogDir);
            var f = Path.Combine(frameLogDir, $"frametimes_{DateTime.Now:yyyy-MM-dd}.csv");
            if (!File.Exists(f)) File.WriteAllText(f, "Time,FPS,FrameMs" + Environment.NewLine);
            File.AppendAllText(f, frameLogBuf.ToString().TrimEnd('\r', '\n') + Environment.NewLine);
        }
        catch { }
        frameLogBuf.Clear();
    }

    static readonly Regex reRoomName = new(@"(?:Joining or Creating Room|Entering Room):\s*(.+?)\s*$", RegexOptions.Compiled);
    static readonly Regex reJoinId = new(@"Joining (wrld_[A-Za-z0-9\-]+:(\S+))", RegexOptions.Compiled);
    static readonly Regex rePlayerJoin = new(@"OnPlayerJoined\s+(.+?)\s*(?:\(usr_[^)]+\))?\s*$", RegexOptions.Compiled);
    static readonly Regex rePlayerLeft = new(@"OnPlayerLeft\s+(.+?)\s*(?:\(usr_[^)]+\))?\s*$", RegexOptions.Compiled);
    static readonly Regex reDisconnect = new(@"(OnDisconnected|Lost connection to|Disconnecting from room)", RegexOptions.Compiled);
    static readonly Regex reAvatarSwitch = new(@"Switching to avatar\s*(.*?)\s*$", RegexOptions.Compiled);

    internal void ResetWorldState()
    {
        currentWorld = "";
        currentInstance = ""; currentInstanceId = "";
        worldJoinTime = null;
        players.Clear();
        playerJoinTimes.Clear();
    }

    // Records the finished world visit (when we leave one / join the next).
    internal void RecordWorldVisit()
    {
        if (!string.IsNullOrEmpty(currentWorld) && worldJoinTime.HasValue)
        {
            // Last-chance name resolution; if the name never arrived (crash before the
            // world finished loading), skip the entry rather than pollute history.
            var w = currentWorld;
            if (w == "Unknown world" && !string.IsNullOrEmpty(currentInstanceId))
            {
                var wid = currentInstanceId.Split(':')[0];
                if (!string.IsNullOrEmpty(wid) && config.WorldNames.TryGetValue(wid, out var known)) w = known;
            }
            if (w == "Unknown world") return;
            int dur = (int)(DateTime.Now - worldJoinTime.Value).TotalSeconds;
            if (dur >= 5)
            {
                config.WorldHistory.Add(new WorldVisit
                {
                    Time = worldJoinTime.Value.ToString("yyyy-MM-dd HH:mm"),
                    World = w,
                    Instance = currentInstance,
                    DurationSec = dur,
                });
                if (config.WorldHistory.Count > 500)
                    config.WorldHistory = config.WorldHistory.Skip(config.WorldHistory.Count - 500).ToList();
            }
        }
    }

    internal void PollVRChatLog()
    {
        if (!Directory.Exists(vrcLowDir)) return;
        FileInfo latest;
        try
        {
            latest = new DirectoryInfo(vrcLowDir).GetFiles("output_log_*.txt")
                     .OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
        }
        catch { return; }
        if (latest == null) return;

        if (vrcLogPath != latest.FullName)
        {
            vrcLogPath = latest.FullName;
            vrcLogPos = Math.Max(0, latest.Length - 262144);
        }

        string text;
        try
        {
            using var fs = File.Open(latest.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (vrcLogPos > fs.Length) vrcLogPos = 0;
            fs.Seek(vrcLogPos, SeekOrigin.Begin);
            using var sr = new StreamReader(fs);
            text = sr.ReadToEnd();
            vrcLogPos = fs.Position;
        }
        catch { return; }
        if (string.IsNullOrEmpty(text)) return;
        lastLogGrowth = DateTime.Now;   // freeze detect: the log is still flowing

        foreach (var line in text.Split('\n'))
        {
            // VRChat logs the join line ("Joining wrld_...") first, then the world NAME
            // line a moment later. So a join sets a placeholder, and the name line that
            // follows fills in the real name for the world we're currently in.
            var m = reRoomName.Match(line);
            if (m.Success)
            {
                if (worldJoinTime.HasValue)
                {
                    currentWorld = m.Groups[1].Value.Trim();
                    if (rosterMeta != null) rosterMeta.World = currentWorld;
                    // Remember the name for this world id so future visits (and visits
                    // cut short by crashes) never end up as "Unknown world".
                    if (!string.IsNullOrEmpty(currentInstanceId))
                    {
                        var wid = currentInstanceId.Split(':')[0];
                        if (!string.IsNullOrEmpty(wid)) config.WorldNames[wid] = currentWorld;
                    }
                }
                continue;
            }

            m = reJoinId.Match(line);
            if (m.Success)
            {
                RecordWorldVisit();
                currentInstanceId = m.Groups[1].Value;
                currentInstance = m.Groups[2].Value.Split('~')[0];
                // Use the remembered name for this world if we have one; the name line
                // that follows will refresh it anyway.
                var widJ = currentInstanceId.Split(':')[0];
                currentWorld = (!string.IsNullOrEmpty(widJ) && config.WorldNames.TryGetValue(widJ, out var known)) ? known : "Unknown world";
                worldJoinTime = DateTime.Now;
                players.Clear();
                playerJoinTimes.Clear();
                churnTimes.Clear();   // ignore the join burst when first entering an instance
                rosterLog.Clear();    // fresh roster for the new instance
                rosterMeta = new RosterMeta { World = currentWorld, Instance = currentInstance, Joined = DateTime.Now };
                continue;
            }

            m = rePlayerJoin.Match(line);
            if (m.Success)
            {
                var n = m.Groups[1].Value.Trim();
                if (n.Length > 0 && !players.Contains(n)) players.Add(n);
                if (n.Length > 0)
                {
                    // Persistent "most-seen players" tally + join time for Who's Here.
                    config.PlayerSeen.TryGetValue(n, out var cur);
                    config.PlayerSeen[n] = cur + 1;
                    if (!playerJoinTimes.ContainsKey(n)) playerJoinTimes[n] = DateTime.Now;
                    // Instance roster: one record per join (re-joins get a new row).
                    rosterLog.Add(new RosterEntry { Name = n, Join = DateTime.Now, Leave = null });
                    // First-met log: remember when/where we first saw this player.
                    if (!config.FirstMet.ContainsKey(n))
                    {
                        var fmW = (!string.IsNullOrEmpty(currentWorld) && currentWorld != "Unknown world") ? currentWorld : "";
                        config.FirstMet[n] = new FirstMetEntry { T = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), W = fmW };
                    }
                    // Private player note -> surface it as they join.
                    if (config.PlayerNotes.TryGetValue(n, out var note) && !string.IsNullOrEmpty(note))
                    {
                        WriteLog($"Note - {n}: {note}");
                        ShowToast($"Note: {n}", note, Ui.Accent2);
                    }
                    // Watchlist: alert (sound + toast) when a watched player appears.
                    if (config.WatchlistNotify.Enabled && config.Watchlist.Count > 0)
                    {
                        var nl = n.ToLower();
                        foreach (var wn in config.Watchlist)
                        {
                            if (!string.IsNullOrEmpty(wn) && nl == wn.Trim().ToLower())
                            {
                                config.WatchGroups.TryGetValue(wn, out var grp);
                                grp ??= "";
                                WriteLog($"Watchlist{(grp.Length > 0 ? $" ({grp})" : "")}: {n} joined the instance.");
                                if (config.WatchlistNotify.Sound) System.Media.SystemSounds.Asterisk.Play();
                                ShowToast(grp.Length > 0 ? $"{grp} - player joined" : "Player joined", $"{n} is in your instance", Ui.Accent);
                                break;
                            }
                        }
                    }
                }
                churnTimes.Add(DateTime.Now);   // crasher-alert churn tracking
                continue;
            }
            m = rePlayerLeft.Match(line);
            if (m.Success)
            {
                var n = m.Groups[1].Value.Trim();
                players.Remove(n);
                playerJoinTimes.Remove(n);
                // Roster: stamp the leave time on their most recent open record.
                for (int ri = rosterLog.Count - 1; ri >= 0; ri--)
                {
                    if (rosterLog[ri].Name == n && rosterLog[ri].Leave == null)
                    {
                        rosterLog[ri].Leave = DateTime.Now;
                        break;
                    }
                }
                churnTimes.Add(DateTime.Now);
                continue;
            }
            var mav = reAvatarSwitch.Match(line);
            if (mav.Success)
            {
                avatarSwitches++;
                // Tally the avatar name for usage stats (log gives a display name here).
                var an = mav.Groups[1].Value.Trim();
                if (an.Length > 0 && an.Length <= 60)
                {
                    config.AvatarUsage.TryGetValue(an, out var ac);
                    config.AvatarUsage[an] = ac + 1;
                    if (config.AvatarUsage.Count > 400)
                    {
                        config.AvatarUsage = config.AvatarUsage
                            .OrderByDescending(e => e.Value).Take(200)
                            .ToDictionary(e => e.Key, e => e.Value);
                    }
                }
                continue;
            }
            if (reDisconnect.IsMatch(line)) pendingRejoin = true;
        }

        // Crasher heuristic: too many join/leave events in a 10s window = likely
        // join-spam. Only evaluate once settled (past the initial join burst).
        bool settled = worldJoinTime.HasValue && (DateTime.Now - worldJoinTime.Value).TotalSeconds >= 20;
        if (config.CrasherAlert.Enabled && settled && churnTimes.Count > 0)
        {
            var cut = DateTime.Now.AddSeconds(-10);
            for (int i = churnTimes.Count - 1; i >= 0; i--)
                if (churnTimes[i] < cut) churnTimes.RemoveAt(i);
            if (churnTimes.Count >= config.CrasherAlert.ChurnPer10s && (DateTime.Now - lastCrasherAlert).TotalSeconds >= 60)
            {
                lastCrasherAlert = DateTime.Now;
                WriteLog($"WARNING: possible join-spam / crasher activity ({churnTimes.Count} join/leave events in 10s).");
                if (config.SoundAlert) System.Media.SystemSounds.Exclamation.Play();
            }
        }
        // World block list: alert (and optionally auto-leave) when the joined world is
        // flagged. Alerts once per instance; names arrive shortly after the join line,
        // so we keep checking until a match fires or we leave.
        if (config.WorldBlock.Enabled && !string.IsNullOrEmpty(currentInstanceId) && blockAlertKey != currentInstanceId)
        {
            if (TestWorldBlocked())
            {
                blockAlertKey = currentInstanceId;
                WriteLog($"WARNING: joined a BLOCKED world: {currentWorld}");
                if (config.SoundAlert) System.Media.SystemSounds.Hand.Play();
                if (config.WorldBlock.AutoLeave)
                {
                    WriteLog("Auto-leave: closing VRChat (blocked world).");
                    blockLeaving = true;    // close edge: don't remember this instance for rejoin
                    try
                    {
                        var p = System.Diagnostics.Process.GetProcessesByName(ProcessName).FirstOrDefault();
                        p?.CloseMainWindow();
                    }
                    catch { }
                }
            }
        }
    }
}
