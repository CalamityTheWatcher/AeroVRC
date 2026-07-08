using System.Diagnostics;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace AeroVRC;

// ============================================================================
//  INTEGRATIONS  (OSC listener/chatbox + Discord Rich Presence) + BOOKMARK OPS
// ============================================================================

public partial class MainForm
{
    // ===== Bookmarks (data ops; the page UI lives in MainForm.BookmarksPage.cs) =====
    internal void AddBookmark()
    {
        if (string.IsNullOrEmpty(currentInstanceId))
        {
            MessageBox.Show("You need to be in a world to bookmark it.", "No world", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var name = !string.IsNullOrEmpty(currentWorld) ? currentWorld : "Unknown world";
        foreach (var b in config.Bookmarks)
        {
            if (b.InstanceId == currentInstanceId) { WriteLog("World already bookmarked."); return; }
        }
        config.Bookmarks.Add(new Bookmark
        {
            Name = name, World = name, InstanceId = currentInstanceId,
            Added = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Note = "", Pinned = false,
        });
        if (config.Bookmarks.Count > 100)
            config.Bookmarks = config.Bookmarks.Skip(config.Bookmarks.Count - 100).ToList();
        SaveConfig();
        WriteLog($"Bookmarked: {name}");
        RebuildBookmarks();
    }

    internal void RemoveBookmark(string instanceId)
    {
        config.Bookmarks = config.Bookmarks.Where(b => b.InstanceId != instanceId).ToList();
        SaveConfig();
        RebuildBookmarks();
    }

    internal void InvokeRejoin(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId)) return;
        WriteLog("Rejoining bookmarked instance...");
        StartVRChat(instanceId);
    }

    // ===== OSC listener (FPS feed) =====
    // Listens on VRChat's OSC output port (default 9001) for an FPS value sent as
    // an avatar parameter. Drained non-blockingly every tick; feeds the Dashboard
    // FPS card and the low-FPS auto-restart guard.
    internal UdpClient oscUdp;
    internal double? vrcFps;
    internal DateTime vrcFpsAt = DateTime.MinValue;
    internal DateTime? fpsBelowSince;
    internal DateTime lastFpsWarn = DateTime.MinValue;

    // Runs the service inits that the script performs inline (HW monitor + OSC).
    void BuildOscInfra()
    {
        InitializeHwMonitor();
        InitializeOscListener();
    }

    internal void InitializeOscListener()
    {
        if (oscUdp != null) { try { oscUdp.Close(); } catch { } oscUdp = null; }
        vrcFps = null; fpsBelowSince = null;
        if (!config.FpsGuard.Enabled) return;
        int port = config.FpsGuard.OscPort;
        try
        {
            var udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Loopback, port));
            oscUdp = udp;
            WriteLog($"FPS guard: listening for OSC on 127.0.0.1:{port}.");
        }
        catch (Exception ex)
        {
            WriteLog($"FPS guard: couldn't bind OSC port {port} - {ex.Message}");
        }
    }

    // Parses a single (non-bundle) OSC datagram; returns the numeric value when the
    // address matches, else null.
    internal static double? GetOscValue(byte[] data, string address)
    {
        try
        {
            int z = Array.IndexOf(data, (byte)0);
            if (z <= 0) return null;
            var addr = Encoding.ASCII.GetString(data, 0, z);
            if (addr != address) return null;
            int i = (z + 4) & ~3;            // skip padded address string
            if (i >= data.Length || data[i] != 44) return null;   // ','
            int z2 = Array.IndexOf(data, (byte)0, i);
            if (z2 < 0) return null;
            var tags = Encoding.ASCII.GetString(data, i, z2 - i);
            if (tags.Length < 2) return null;
            int j = (z2 + 4) & ~3;           // skip padded type-tag string
            if (j + 4 > data.Length) return null;
            var b = new byte[4];
            Array.Copy(data, j, b, 0, 4);
            Array.Reverse(b);                // OSC values are big-endian
            return tags[1] switch
            {
                'f' => BitConverter.ToSingle(b, 0),
                'i' => BitConverter.ToInt32(b, 0),
                _ => null,
            };
        }
        catch { return null; }
    }

    // Drain all waiting datagrams (bounded per tick so a chatty avatar can't stall us).
    internal void ReadOscFps()
    {
        if (oscUdp == null) return;
        int n = 0;
        var target = config.FpsGuard.Address;
        try
        {
            var ep = new IPEndPoint(IPAddress.Any, 0);
            while (oscUdp.Available > 0 && n < 400)
            {
                n++;
                var data = oscUdp.Receive(ref ep);
                var v = GetOscValue(data, target);
                if (v.HasValue) { vrcFps = v.Value; vrcFpsAt = DateTime.Now; }
            }
        }
        catch { }
    }

    internal static byte[] ToOscBytes(string s)
    {
        var list = new List<byte>();
        list.AddRange(Encoding.ASCII.GetBytes(s));
        list.Add(0);
        while (list.Count % 4 != 0) list.Add(0);
        return list.ToArray();
    }

    // Spotify "now playing" from the desktop app's window title ("Artist - Song").
    // When paused/idle the title is just "Spotify" (and the ad title varies), so we
    // return "" in those cases. No API/login needed.
    internal static string GetSpotifyNowPlaying()
    {
        try
        {
            foreach (var p in Process.GetProcessesByName("Spotify"))
            {
                var t = p.MainWindowTitle;
                if (!string.IsNullOrEmpty(t) && t != "Spotify" &&
                    !System.Text.RegularExpressions.Regex.IsMatch(t, "^Spotify( Free| Premium)?$") &&
                    t.Contains(" - "))
                {
                    return t;
                }
            }
        }
        catch { }
        return "";
    }

    // Expands the user's chatbox template tokens against the current session state.
    internal string FormatOscTemplate(string tpl)
    {
        if (string.IsNullOrEmpty(tpl)) tpl = "VRChat {uptime} | {world}";
        var up = "";
        if (sessionStart.HasValue)
        {
            var u = DateTime.Now - sessionStart.Value;
            up = $"{(int)u.TotalHours}h{u.Minutes:00}m";
        }
        var ram = vrcRamMB.HasValue ? $"{vrcRamMB.Value / 1024.0:0.0}GB" : "-";
        var cpu = vrcCpuPct.HasValue ? $"{vrcCpuPct.Value}%" : "-";
        var fps = (vrcFps.HasValue && (DateTime.Now - vrcFpsAt).TotalSeconds <= 15) ? $"{(int)vrcFps.Value}" : "-";
        var map = new Dictionary<string, string>
        {
            ["{world}"] = !string.IsNullOrEmpty(currentWorld) ? currentWorld : "no world",
            ["{instance}"] = currentInstance ?? "",
            ["{uptime}"] = up,
            ["{players}"] = players.Count.ToString(),
            ["{restarts}"] = sessionRestarts.ToString(),
            ["{cpu}"] = cpu,
            ["{ram}"] = ram,
            ["{fps}"] = fps,   // from the OSC FPS feed if configured
            ["{spotify}"] = GetSpotifyNowPlaying(),
            ["{time}"] = DateTime.Now.ToString("HH:mm"),
            ["{date}"] = DateTime.Now.ToString("yyyy-MM-dd"),
        };
        var outStr = tpl;
        foreach (var k in map.Keys) outStr = outStr.Replace(k, map[k]);
        return outStr;
    }

    internal void SendOscChatbox(string text)
    {
        if (text.Length > 140) text = text[..140];
        try
        {
            var packet = new List<byte>();
            packet.AddRange(ToOscBytes("/chatbox/input"));
            packet.AddRange(ToOscBytes(",sTF"));   // string, immediate=True, sfx=False
            packet.AddRange(ToOscBytes(text));
            var bytes = packet.ToArray();
            using var udp = new UdpClient();
            udp.Send(bytes, bytes.Length, "127.0.0.1", 9000);
        }
        catch { }
    }

    // ===== Discord Rich Presence (raw named-pipe IPC, no SDK needed) =====
    NamedPipeClientStream discordPipe;

    static void SendDiscordFrame(NamedPipeClientStream pipe, int op, string json)
    {
        var data = Encoding.UTF8.GetBytes(json);
        var header = new byte[8];
        BitConverter.GetBytes(op).CopyTo(header, 0);
        BitConverter.GetBytes(data.Length).CopyTo(header, 4);
        pipe.Write(header, 0, 8);
        pipe.Write(data, 0, data.Length);
        pipe.Flush();
    }

    internal bool ConnectDiscordRP()
    {
        if (string.IsNullOrEmpty(config.DiscordRP.ClientId)) return false;
        try
        {
            var pipe = new NamedPipeClientStream(".", "discord-ipc-0", PipeDirection.InOut);
            pipe.Connect(600);
            SendDiscordFrame(pipe, 0, JsonSerializer.Serialize(new { v = 1, client_id = config.DiscordRP.ClientId }));
            discordPipe = pipe;
            WriteLog("Discord Rich Presence connected.");
            return true;
        }
        catch { discordPipe = null; return false; }
    }

    internal void UpdateDiscordRP()
    {
        if (discordPipe == null && !ConnectDiscordRP()) return;
        var details = !string.IsNullOrEmpty(currentWorld) ? $"In: {currentWorld}" : "In VRChat";
        var state = players.Count > 0 ? $"{players.Count} in instance" : "Solo / loading";
        long startEpoch = 0;
        if (sessionStart.HasValue)
            startEpoch = (long)(sessionStart.Value.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        var payload = new
        {
            cmd = "SET_ACTIVITY",
            nonce = Guid.NewGuid().ToString(),
            args = new
            {
                pid = Environment.ProcessId,
                activity = new
                {
                    details,
                    state,
                    timestamps = new { start = startEpoch },
                },
            },
        };
        try { SendDiscordFrame(discordPipe, 1, JsonSerializer.Serialize(payload)); }
        catch { discordPipe = null; }
    }

    internal void DisconnectDiscordRP()
    {
        if (discordPipe != null) { try { discordPipe.Dispose(); } catch { } discordPipe = null; }
    }
}
