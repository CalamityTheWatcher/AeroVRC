using System.Drawing.Drawing2D;
using System.Text;

namespace AeroVRC;

// ============================================================================
//  DASHBOARD PAGE
// ============================================================================

public partial class MainForm
{
    internal Panel pgDash;
    internal Label dashStatusBig;
    internal AeroCheckBox qtDesktop, qtRejoin, qtPerf;
    internal Label dashWorldName, dashWorldInst;
    internal Label dashUptime, dashInWorld, dashPlayers, dashRestarts, dashLastCrash, dashDisk,
                   dashPhotos, dashToday, dashSteamVR, dashCpu, dashRam, dashPrio, dashNext,
                   dashMic, dashAvatars, dashGpu, dashVram, dashGpuTemp, dashPing, dashFps, dashNet;
    internal TableLayoutPanel dashGrid;
    internal Panel framePanel;
    internal ListBox whoList;
    Button whoExportBtn;
    ContextMenuStrip whoMenu;
    ToolStripMenuItem whoMiNote, whoMiInfo;
    internal List<string> whoNames = new();
    string whoCtxName = "";

    void BuildDashboardPage()
    {
        pgDash = NewPage("Dashboard");
        var dashTitle = NewPageTitle("Dashboard");
        pgDash.Controls.Add(dashTitle);

        // Status line - sits directly on the page (no card / no coloured box)
        dashStatusBig = new Label
        {
            Name = "statusbig", Text = "Stopped", Font = Ui.FontBig,
            BackColor = Ui.Bg, ForeColor = Ui.Stopped, AutoSize = true,
            Location = new Point(6, 50),
        };
        pgDash.Controls.Add(dashStatusBig);

        // Quick toggles: mirror key settings right on the Dashboard (two-way synced).
        var qtPanel = new FlowLayoutPanel
        {
            Location = new Point(414, 58), Size = new Size(430, 30),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            WrapContents = false, BackColor = Color.Transparent,
        };
        pgDash.Controls.Add(qtPanel);

        AeroCheckBox NewQuickToggle(string text) => new()
        {
            Name = "muted", Text = text, AutoSize = true, Font = Ui.FontMuted,
            Margin = new Padding(0, 2, 12, 0),
        };
        qtDesktop = NewQuickToggle("Desktop");
        qtDesktop.Checked = config.DesktopMode;
        qtDesktop.CheckedChanged += (s, e) =>
        {
            if (loading) return;
            if (desktopCb.Checked != qtDesktop.Checked) desktopCb.Checked = qtDesktop.Checked;
        };
        qtRejoin = NewQuickToggle("Auto-rejoin");
        qtRejoin.Checked = config.RejoinOnRestart.Enabled;
        qtRejoin.CheckedChanged += (s, e) =>
        {
            if (loading) return;
            if (rejoinRestartCb.Checked != qtRejoin.Checked) rejoinRestartCb.Checked = qtRejoin.Checked;
        };
        qtPerf = NewQuickToggle("Performance");
        qtPerf.Checked = false;   // session-only state, never persisted
        qtPerf.CheckedChanged += (s, e) => { if (loading) return; SetPerformanceMode(qtPerf.Checked); };
        qtPanel.Controls.AddRange(new Control[] { qtDesktop, qtRejoin, qtPerf });

        // Current World indicator (dedicated section)
        var worldCard = Ui.NewCard();
        worldCard.Location = new Point(4, 98);
        worldCard.Size = new Size(840, 88);
        worldCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pgDash.Controls.Add(worldCard);

        var worldCardHdr = new Label
        {
            Name = "onCardMuted", Text = "CURRENT WORLD", Font = Ui.FontSmall,
            AutoSize = true, Location = new Point(16, 14),
        };
        worldCard.Controls.Add(worldCardHdr);

        dashWorldName = new Label
        {
            Name = "onCard", Text = "Not in a world", Font = Ui.FontValue,
            AutoSize = true, Location = new Point(16, 38),
        };
        worldCard.Controls.Add(dashWorldName);

        dashWorldInst = new Label
        {
            Name = "onCardMuted", Text = "", Font = Ui.FontMuted,
            AutoSize = true, Location = new Point(18, 64),
        };
        worldCard.Controls.Add(dashWorldInst);

        var dashBookmarkBtn = new Button
        {
            Text = "★ Bookmark", Size = new Size(130, 34), Location = new Point(694, 27),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        Ui.StyleButton(dashBookmarkBtn, "secondary");
        dashBookmarkBtn.Click += (s, e) => AddBookmark();
        worldCard.Controls.Add(dashBookmarkBtn);

        var dashCopyBtn = new Button
        {
            Text = "Copy Link", Size = new Size(92, 34), Location = new Point(446, 27),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        Ui.StyleButton(dashCopyBtn, "secondary");
        dashCopyBtn.Click += (s, e) =>
        {
            if (string.IsNullOrEmpty(currentInstanceId))
            {
                MessageBox.Show("You need to be in a world to copy its link.", "No world", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var parts = currentInstanceId.Split(':', 2);
            var link = $"https://vrchat.com/home/launch?worldId={parts[0]}";
            if (parts.Length > 1) link += "&instanceId=" + Uri.EscapeDataString(parts[1]);
            Clipboard.SetText(link);
            WriteLog("Share link copied to clipboard.");
        };
        worldCard.Controls.Add(dashCopyBtn);

        var dashHomeBtn = new Button
        {
            Text = "⌂ Take Me Home", Size = new Size(140, 34), Location = new Point(546, 27),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        Ui.StyleButton(dashHomeBtn, "secondary");
        dashHomeBtn.Click += (s, e) =>
        {
            var h = config.HomeWorld;
            if (string.IsNullOrEmpty(h))
            {
                // No home saved yet: offer to capture the current instance as home.
                if (!string.IsNullOrEmpty(currentInstanceId))
                {
                    var r = MessageBox.Show($"No home world set yet.\n\nSet your current world ({currentWorld}) as home?",
                        "Set home world", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (r == DialogResult.Yes)
                    {
                        config.HomeWorld = currentInstanceId;
                        SaveConfig();
                        WriteLog($"Home world set to the current instance ({currentWorld}).");
                    }
                }
                else
                {
                    MessageBox.Show("No home world set yet. Join your home world in VRChat, then press this button to save it.",
                        "No home world", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }
            var id = ConvertVrcLink(h) ?? h;
            WriteLog("Taking you home...");
            StartVRChat(id);
        };
        worldCard.Controls.Add(dashHomeBtn);

        // Stat cards grid
        dashGrid = new TableLayoutPanel
        {
            Location = new Point(4, 200), Size = new Size(840, 1038),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ColumnCount = 3, RowCount = 9, BackColor = Color.Transparent,
        };
        for (int i = 0; i < 3; i++) dashGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        for (int i = 0; i < 7; i++) dashGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        dashGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));   // frame-time graph
        dashGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 178));   // who's here
        pgDash.Controls.Add(dashGrid);

        // Builds a stat card into the grid; returns the value label for later updates.
        Label AddStatCard(string title, int col, int row)
        {
            var card = Ui.NewCard();
            card.Margin = new Padding(0, 0, 12, 12);
            card.Dock = DockStyle.Fill;

            var t = new Label
            {
                Name = "onCardMuted", Text = title.ToUpper(), Font = Ui.FontSmall,
                AutoSize = true, Location = new Point(16, 16),
            };
            card.Controls.Add(t);

            var v = new Label
            {
                Name = "onCard", Text = "-", Font = Ui.FontValue,
                AutoSize = true, Location = new Point(16, 42),
            };
            card.Controls.Add(v);

            dashGrid.Controls.Add(card, col, row);
            return v;
        }
        dashUptime = AddStatCard("VRChat uptime", 0, 0);
        dashInWorld = AddStatCard("In-world time", 1, 0);
        dashPlayers = AddStatCard("Players nearby", 2, 0);
        dashRestarts = AddStatCard("Restarts (session/total)", 0, 1);
        dashLastCrash = AddStatCard("Last restart", 1, 1);
        dashDisk = AddStatCard("Disk free", 2, 1);
        dashPhotos = AddStatCard("Photos this session", 0, 2);
        dashToday = AddStatCard("Played today", 1, 2);
        dashSteamVR = AddStatCard("SteamVR", 2, 2);
        dashCpu = AddStatCard("CPU (VRChat)", 0, 3);
        dashRam = AddStatCard("RAM (VRChat)", 1, 3);
        dashPrio = AddStatCard("VRChat priority", 2, 3);
        dashNext = AddStatCard("Next timer", 0, 4);
        dashMic = AddStatCard("Microphone", 1, 4);
        dashAvatars = AddStatCard("Avatar switches (session)", 2, 4);
        dashGpu = AddStatCard("GPU load", 0, 5);
        dashVram = AddStatCard("VRAM", 1, 5);
        dashGpuTemp = AddStatCard("Temps (GPU / CPU)", 2, 5);
        dashPing = AddStatCard("Ping", 0, 6);
        dashFps = AddStatCard("FPS (via OSC)", 1, 6);
        dashNet = AddStatCard("Connection", 2, 6);

        // Frame-time graph (fed by the OSC FPS feed; spans the full row).
        var frameCard = Ui.NewCard();
        frameCard.Margin = new Padding(0, 0, 12, 12);
        frameCard.Dock = DockStyle.Fill;
        var frameHdr = new Label
        {
            Name = "onCardMuted", Text = "FRAME TIMES (LAST 10 MIN)", Font = Ui.FontSmall,
            Dock = DockStyle.Top, Height = 24, Padding = new Padding(12, 8, 0, 0),
        };
        framePanel = new Panel { Name = "chart", Dock = DockStyle.Fill, BackColor = Ui.Card };
        Ui.SetDoubleBuffered(framePanel);
        framePanel.Paint += (s, e) => DrawFrameChart((Panel)s, e.Graphics);
        frameCard.Controls.Add(framePanel);
        frameCard.Controls.Add(frameHdr);
        dashGrid.Controls.Add(frameCard, 0, 7);
        dashGrid.SetColumnSpan(frameCard, 3);

        // Who's Here panel: current players with their join times (spans the full row).
        var whoCard = Ui.NewCard();
        whoCard.Margin = new Padding(0, 0, 12, 12);
        whoCard.Dock = DockStyle.Fill;
        var whoHdr = new Label
        {
            Name = "onCardMuted", Text = "WHO'S HERE   (right-click a player for notes & info)", Font = Ui.FontSmall,
            Dock = DockStyle.Top, Height = 26, Padding = new Padding(12, 8, 0, 0),
        };
        whoExportBtn = new Button
        {
            Text = "Export roster", Size = new Size(108, 26),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        Ui.StyleButton(whoExportBtn, "secondary");
        whoList = new ListBox
        {
            Name = "logBox", Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
            Font = Ui.FontBody, IntegralHeight = false,
        };
        whoCard.Controls.Add(whoList);
        whoCard.Controls.Add(whoHdr);
        whoCard.Controls.Add(whoExportBtn);
        whoExportBtn.BringToFront();
        whoCard.Resize += (s, e) => whoExportBtn.Location = new Point(((Panel)s).Width - 122, 8);
        dashGrid.Controls.Add(whoCard, 0, 8);
        dashGrid.SetColumnSpan(whoCard, 3);

        // Roster export: who was present in this instance, with join/leave timestamps.
        whoExportBtn.Click += (s, e) =>
        {
            if (rosterLog.Count == 0)
            {
                MessageBox.Show("No roster recorded yet - join an instance with other players first.",
                    "Nothing to export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var sfd = new SaveFileDialog
            {
                Filter = "CSV file|*.csv|Text file|*.txt",
                FileName = $"instance-roster_{DateTime.Now:yyyy-MM-dd_HH-mm}.csv",
            };
            if (sfd.ShowDialog() != DialogResult.OK) return;
            try
            {
                var sb = new StringBuilder();
                if (rosterMeta != null)
                    sb.AppendLine($"# World: {rosterMeta.World}   Instance: #{rosterMeta.Instance}   Entered: {rosterMeta.Joined:yyyy-MM-dd HH:mm}");
                sb.AppendLine("Name,Joined,Left");
                foreach (var r in rosterLog)
                {
                    var nm = "\"" + r.Name.Replace("\"", "\"\"") + "\"";
                    var lv = r.Leave.HasValue ? r.Leave.Value.ToString("yyyy-MM-dd HH:mm:ss") : "";
                    sb.AppendLine($"{nm},{r.Join:yyyy-MM-dd HH:mm:ss},{lv}");
                }
                File.WriteAllText(sfd.FileName, sb.ToString());
                WriteLog($"Instance roster exported: {sfd.FileName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        // ---- Who's Here right-click: player notes / first-met info ----
        whoMenu = new ContextMenuStrip { BackColor = Ui.Card, ForeColor = Ui.Text };
        try { whoMenu.Renderer = new ToolStripProfessionalRenderer(new AeroMenuColors()); } catch { }
        ToolStripMenuItem NewWhoMenuItem(string text, Action onClick)
        {
            var mi = new ToolStripMenuItem(text) { ForeColor = Ui.Text };
            mi.Click += (s, e) => onClick();
            whoMenu.Items.Add(mi);
            return mi;
        }
        whoMiNote = NewWhoMenuItem("Add / edit note...", () => { if (whoCtxName.Length > 0) EditPlayerNote(whoCtxName); });
        whoMiInfo = NewWhoMenuItem("Player info (first met, seen count)", () => { if (whoCtxName.Length > 0) ShowPlayerInfo(whoCtxName); });
        whoMenu.Items.Add(new ToolStripSeparator());
        NewWhoMenuItem("Look up any player...", () =>
        {
            var q = Microsoft.VisualBasic.Interaction.InputBox("Player name to look up (exact display name):", "Player lookup", "").Trim();
            if (q.Length > 0) ShowPlayerInfo(q);
        });
        whoList.MouseDown += (s, e) =>
        {
            if (e.Button != MouseButtons.Right) return;
            int i = whoList.IndexFromPoint(e.Location);
            whoCtxName = (i >= 0 && i < whoNames.Count) ? whoNames[i] : "";
            if (i >= 0) whoList.SelectedIndex = i;
            whoMiNote.Enabled = whoCtxName.Length > 0;
            whoMiInfo.Enabled = whoCtxName.Length > 0;
            whoMiNote.Text = whoCtxName.Length > 0 ? $"Add / edit note for {whoCtxName}..." : "Add / edit note...";
            whoMenu.Show(whoList, e.Location);
        };
    }

    // Frame-time graph paint (fed by the OSC FPS feed).
    void DrawFrameChart(Panel panel, Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Ui.Card);
        int w = panel.Width, h = panel.Height;
        using var lblFont = new Font("Segoe UI", 8);
        using var mutedBrush = new SolidBrush(Ui.TextMuted);
        if (frameHist.Count < 2)
        {
            g.DrawString("No frame-time data - needs the OSC FPS source (see Settings > Monitoring).", lblFont, mutedBrush, 6, h / 2 - 8);
            return;
        }
        var vals = frameHist;
        int n = vals.Count;
        double maxV = 4.0, sum = 0.0, curMax = 0.0;
        foreach (var v in vals) { sum += v; if (v > curMax) curMax = v; }
        if (curMax > maxV) maxV = curMax;
        maxV = Math.Min(maxV * 1.15, 100);     // headroom, clamp absurd spikes
        double avg = sum / n;
        // Reference lines: 90 FPS (11.1ms) and 60 FPS (16.7ms) frame budgets.
        using (var refPen = new Pen(Color.FromArgb(70, Ui.TextMuted), 1) { DashStyle = DashStyle.Dash })
        {
            foreach (var (ms, l) in new[] { (11.1, "90"), (16.7, "60") })
            {
                if (ms < maxV)
                {
                    double ry = h - ms / maxV * (h - 14) - 7;
                    g.DrawLine(refPen, 0, (float)ry, w - 34, (float)ry);
                    g.DrawString($"{l} FPS", lblFont, mutedBrush, w - 46, (float)(ry - 7));
                }
            }
        }
        // The trace itself.
        var pts = new List<PointF>();
        for (int i = 0; i < n; i++)
        {
            // newest sample anchored at the right edge of the plot area
            double x = (w - 40) - (n - 1 - i) / (double)Math.Max(1, 600 - 1) * (w - 40);
            double y = h - Math.Min(vals[i], maxV) / maxV * (h - 14) - 7;
            pts.Add(new PointF((float)x, (float)y));
        }
        if (pts.Count >= 2)
        {
            using var tracePen = new Pen(Ui.Accent, 1.6f);
            g.DrawLines(tracePen, pts.ToArray());
        }
        // Current / avg / worst caption (worst of the visible window).
        double curMs = vals[n - 1];
        using var capBrush = new SolidBrush(Ui.Text);
        g.DrawString($"now {curMs:0.0} ms ({1000 / Math.Max(0.1, curMs):0} FPS)   avg {avg:0.0} ms   worst {curMax:0.0} ms", lblFont, capBrush, 6, 2);
    }

    internal void RefreshWhoList()
    {
        whoList.BeginUpdate();
        whoList.Items.Clear();
        whoNames = new List<string>();
        if (players.Count == 0)
        {
            whoList.Items.Add("(nobody tracked - join a world with other players)");
        }
        else
        {
            var rows = new List<(string N, DateTime? T)>();
            foreach (var n in players)
            {
                DateTime? jt = playerJoinTimes.TryGetValue(n, out var t) ? t : null;
                rows.Add((n, jt));
            }
            foreach (var r in rows.OrderBy(r => r.T ?? DateTime.MinValue))
            {
                var noteMark = config.PlayerNotes.ContainsKey(r.N) ? "  [note]" : "";
                if (r.T.HasValue)
                {
                    var el = FormatDuration((int)(DateTime.Now - r.T.Value).TotalSeconds);
                    whoList.Items.Add(string.Format("{0:HH:mm}   ({1,-8})   {2}{3}", r.T.Value, el, r.N, noteMark));
                }
                else
                {
                    whoList.Items.Add($"--:--              {r.N}{noteMark}");
                }
                whoNames.Add(r.N);
            }
        }
        whoList.EndUpdate();
    }

    internal void EditPlayerNote(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        config.PlayerNotes.TryGetValue(name, out var cur);
        var note = Microsoft.VisualBasic.Interaction.InputBox(
            $"Private note for {name} (shown whenever they join your instance).{Environment.NewLine}Leave empty to remove the note.",
            "Player note", cur ?? "").Trim();
        if (note.Length > 0)
        {
            config.PlayerNotes[name] = note;
            WriteLog($"Note saved for {name}.");
        }
        else if (config.PlayerNotes.Remove(name))
        {
            WriteLog($"Note removed for {name}.");
        }
        SaveConfig();
        RefreshWhoList();
    }

    internal void ShowPlayerInfo(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        var lines = new List<string>();
        if (config.FirstMet.TryGetValue(name, out var fm))
        {
            var wtxt = !string.IsNullOrEmpty(fm.W) ? fm.W : "(world unknown)";
            lines.Add($"First met:  {fm.T}");
            lines.Add($"Where:      {wtxt}");
        }
        else
        {
            lines.Add("First met:  (not recorded yet - tracked from now on)");
        }
        if (config.PlayerSeen.TryGetValue(name, out var seen)) lines.Add($"Seen joining:  {seen} time(s)");
        if (config.PlayerNotes.TryGetValue(name, out var note)) { lines.Add(""); lines.Add($"Note:  {note}"); }
        MessageBox.Show(string.Join(Environment.NewLine, lines), name, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
