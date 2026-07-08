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

    // Dashboard stat-card visuals: per-card icon glyph + colour, and an optional
    // rolling sparkline (drawn only while the sparkle effect is enabled).
    class DashFx { public char Glyph; public Color IconColor; public string Spark; public string Gauge; }
    readonly Dictionary<Panel, DashFx> dashFx = new();
    readonly Dictionary<string, List<double>> sparkHist = new();
    readonly List<Panel> sparkCards = new();
    readonly List<Panel> gaugeCards = new();
    internal Panel dashWorldCard;
    internal Color dashWorldColor = Ui.Border;

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

        // Current World indicator (hero card): world-icon chip tinted by instance type.
        var worldCard = Ui.NewCard();
        worldCard.Location = new Point(4, 98);
        worldCard.Size = new Size(840, 88);
        worldCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pgDash.Controls.Add(worldCard);
        dashWorldCard = worldCard;
        worldCard.Paint += (s, e) =>
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var chip = Ui.RoundedPath(16, 20, 48, 48, 12))
            using (var b = new SolidBrush(Color.FromArgb(40, dashWorldColor)))
                g.FillPath(b, chip);
            DrawGlyph(g, '◍', new RectangleF(16, 20, 48, 48), dashWorldColor, 26f);
        };

        var worldCardHdr = new Label
        {
            Name = "onCardMuted", Text = "CURRENT WORLD", Font = Ui.FontSmall,
            AutoSize = true, Location = new Point(74, 14),
        };
        worldCard.Controls.Add(worldCardHdr);

        dashWorldName = new Label
        {
            Name = "onCard", Text = "Not in a world", Font = Ui.FontValue,
            AutoSize = true, Location = new Point(74, 36),
        };
        worldCard.Controls.Add(dashWorldName);

        dashWorldInst = new Label
        {
            Name = "onCardMuted", Text = "", Font = Ui.FontMuted,
            AutoSize = true, Location = new Point(76, 62),
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

        // Stat cards grid - grouped into Session / Performance / System bands.
        dashGrid = new TableLayoutPanel
        {
            Location = new Point(4, 200), Size = new Size(840, 1176),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ColumnCount = 3, RowCount = 12, BackColor = Color.Transparent,
        };
        for (int i = 0; i < 3; i++) dashGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        int[] rowH = { 36, 104, 104, 104, 36, 104, 104, 104, 132, 36, 104, 178 };
        foreach (var rh in rowH) dashGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, rh));
        pgDash.Controls.Add(dashGrid);

        // Section header spanning all three columns.
        void AddSectionHeader(string text, int row)
        {
            var hdr = new Label
            {
                Name = "section", Text = text, Font = Ui.FontHeader,
                AutoSize = true, Margin = new Padding(4, 12, 0, 2),
            };
            dashGrid.Controls.Add(hdr, 0, row);
            dashGrid.SetColumnSpan(hdr, 3);
        }

        // Builds a stat card (icon chip + title + value, optional sparkline).
        Label AddStatCard(string title, int col, int row, char glyph, Color iconColor, string spark = null, string gauge = null)
        {
            var card = Ui.NewCard();
            card.Margin = new Padding(0, 0, 12, 12);
            card.Dock = DockStyle.Fill;
            Ui.SetDoubleBuffered(card);

            var t = new Label
            {
                Name = "onCardMuted", Text = title.ToUpper(), Font = Ui.FontSmall,
                AutoSize = true, Location = new Point(56, 18),
            };
            card.Controls.Add(t);

            var v = new Label
            {
                Name = "onCard", Text = "-", Font = Ui.FontValue,
                AutoSize = true, Location = new Point(56, 42),
            };
            card.Controls.Add(v);

            dashFx[card] = new DashFx { Glyph = glyph, IconColor = iconColor, Spark = spark, Gauge = gauge };
            if (spark != null) sparkCards.Add(card);
            if (gauge != null) gaugeCards.Add(card);
            card.Paint += (s, e) => DrawDashCardFx((Panel)s, e.Graphics);

            dashGrid.Controls.Add(card, col, row);
            return v;
        }

        var cAzure = Ui.Accent; var cViolet = Ui.Accent2; var cTeal = Color.FromArgb(72, 190, 200);
        var cGreen = Ui.Success; var cAmber = Ui.Warning; var cRose = Color.FromArgb(240, 120, 150);
        var cIndigo = Color.FromArgb(120, 138, 244); var cPurple = Color.FromArgb(158, 124, 244);

        AddSectionHeader("SESSION", 0);
        dashUptime    = AddStatCard("VRChat uptime", 0, 1, '◷', cAzure);
        dashInWorld   = AddStatCard("In-world time", 1, 1, '◉', cTeal);
        dashPlayers   = AddStatCard("Players nearby", 2, 1, '☺', cViolet, "players");
        dashRestarts  = AddStatCard("Restarts (session/total)", 0, 2, '⟳', cAmber);
        dashLastCrash = AddStatCard("Last restart", 1, 2, '↺', cPurple);
        dashToday     = AddStatCard("Played today", 2, 2, '◔', cGreen, gauge: "todayGoal");
        dashPhotos    = AddStatCard("Photos this session", 0, 3, '▣', cRose);
        dashAvatars   = AddStatCard("Avatar switches (session)", 1, 3, '❂', cIndigo);
        dashNext      = AddStatCard("Next timer", 2, 3, '⧖', cAmber);

        AddSectionHeader("PERFORMANCE", 4);
        dashCpu     = AddStatCard("CPU (VRChat)", 0, 5, '▦', cAzure, "cpu");
        dashRam     = AddStatCard("RAM (VRChat)", 1, 5, '▥', cViolet, "ram");
        dashPrio    = AddStatCard("VRChat priority", 2, 5, '▲', cTeal);
        dashGpu     = AddStatCard("GPU load", 0, 6, '◧', cPurple, "gpu");
        dashVram    = AddStatCard("VRAM", 1, 6, '▨', cIndigo);
        dashGpuTemp = AddStatCard("Temps (GPU / CPU)", 2, 6, '♨', cAmber);
        dashFps     = AddStatCard("FPS (via OSC)", 0, 7, '⚡', cGreen, "fps");
        dashPing    = AddStatCard("Ping", 1, 7, '⇅', cTeal, "ping");
        dashNet     = AddStatCard("Connection", 2, 7, '⇄', cAzure);

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
        dashGrid.Controls.Add(frameCard, 0, 8);
        dashGrid.SetColumnSpan(frameCard, 3);

        // System band (machine-level, changes slowly).
        AddSectionHeader("SYSTEM", 9);
        dashDisk    = AddStatCard("Disk free", 0, 10, '▤', Ui.Accent);
        dashSteamVR = AddStatCard("SteamVR", 1, 10, '◈', Ui.Accent2);
        dashMic     = AddStatCard("Microphone", 2, 10, '◉', Ui.Success);

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
        dashGrid.Controls.Add(whoCard, 0, 11);
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

    // Icon chip (+ optional sparkline) painted on top of a dashboard stat card.
    void DrawDashCardFx(Panel card, Graphics g)
    {
        if (!dashFx.TryGetValue(card, out var fx)) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        const float cx = 14, cy = 24, sz = 34;
        using (var chip = Ui.RoundedPath(cx, cy, sz, sz, 9))
        using (var fill = new SolidBrush(Color.FromArgb(38, fx.IconColor)))
            g.FillPath(fill, chip);
        DrawGlyph(g, fx.Glyph, new RectangleF(cx, cy, sz, sz), fx.IconColor, 18f);
        if (fx.Spark != null && config.Effects.Sparkles &&
            sparkHist.TryGetValue(fx.Spark, out var hist) && hist.Count >= 2)
            DrawSparkline(g, card, hist, fx.IconColor);
        if (fx.Gauge == "todayGoal") DrawGoalGauge(g, card);
    }

    // Radial progress ring on the right of a card: today's playtime vs the daily goal.
    void DrawGoalGauge(Graphics g, Panel card)
    {
        int goalSec = config.Goals.DailyMin * 60;
        if (goalSec <= 0) return;
        int todaySec = config.PlayHistory.TryGetValue(DateTime.Now.ToString("yyyy-MM-dd"), out var ts) ? (int)ts : 0;
        double prog = Math.Min(1.0, todaySec / (double)goalSec);
        var color = prog >= 1.0 ? Ui.Success : Ui.AccentHover;
        float d = 46, x = card.Width - 16 - d, y = (card.Height - d) / 2f + 4;
        var rect = new RectangleF(x, y, d, d);
        using (var bgp = new Pen(Ui.Border, 5)) g.DrawArc(bgp, rect, 0, 360);
        using (var fgp = new Pen(color, 5) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawArc(fgp, rect, -90, (float)(360 * prog));
        using var f = new Font("Segoe UI Semibold", 8f);
        using var tb = new SolidBrush(Ui.Text);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString($"{(int)(prog * 100)}%", f, tb, rect, sf);
    }

    // Draws a Segoe UI Symbol glyph scaled + centred into a cell (same technique as
    // the nav icons), so every glyph reads at a uniform size.
    void DrawGlyph(Graphics g, char ch, RectangleF cell, Color color, float target)
    {
        using var path = new GraphicsPath();
        try
        {
            path.AddString(ch.ToString(), navIconFamily, 0, 34f, new PointF(0, 0), StringFormat.GenericTypographic);
            var bn = path.GetBounds();
            if (bn.Width < 0.1f || bn.Height < 0.1f) return;
            float scale = target / Math.Max(bn.Width, bn.Height);
            float gw = bn.Width * scale, gh = bn.Height * scale;
            float tx = cell.X + (cell.Width - gw) / 2f - bn.X * scale;
            float ty = cell.Y + (cell.Height - gh) / 2f - bn.Y * scale;
            using var m = new Matrix(); m.Translate(tx, ty); m.Scale(scale, scale); path.Transform(m);
            using var brush = new SolidBrush(color); g.FillPath(brush, path);
        }
        catch { }
    }

    // Small rolling sparkline in the lower area of a card (below the value).
    void DrawSparkline(Graphics g, Panel card, List<double> hist, Color color)
    {
        int n = hist.Count;
        float x0 = 56, x1 = card.Width - 14, y0 = 66, y1 = card.Height - 12;
        if (x1 - x0 < 24 || y1 - y0 < 8) return;
        double mn = double.MaxValue, mx = double.MinValue;
        foreach (var v in hist) { if (v < mn) mn = v; if (v > mx) mx = v; }
        if (mx - mn < 1e-6) mx = mn + 1;
        var pts = new PointF[n];
        for (int i = 0; i < n; i++)
        {
            float x = x0 + (x1 - x0) * (i / (float)(n - 1));
            float y = (float)(y1 - (hist[i] - mn) / (mx - mn) * (y1 - y0));
            pts[i] = new PointF(x, y);
        }
        using (var area = new GraphicsPath())
        {
            area.AddLines(pts);
            area.AddLine(pts[n - 1].X, y1, pts[0].X, y1);
            area.CloseFigure();
            using var ab = new SolidBrush(Color.FromArgb(28, color));
            g.FillPath(ab, area);
        }
        using var pen = new Pen(Color.FromArgb(210, color), 1.4f);
        g.DrawLines(pen, pts);
    }

    // Push a live value into a named sparkline buffer (bounded ring).
    void PushSpark(string key, double? v)
    {
        if (!v.HasValue) return;
        if (!sparkHist.TryGetValue(key, out var l)) { l = new List<double>(); sparkHist[key] = l; }
        l.Add(v.Value);
        if (l.Count > 48) l.RemoveAt(0);
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
