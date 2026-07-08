using System.Drawing.Drawing2D;

namespace AeroVRC;

// ============================================================================
//  STATISTICS PAGE  (charts, heatmap, summaries, leaderboards, analytics)
// ============================================================================

public partial class MainForm
{
    internal Panel pgStats;
    internal int chartDays = 7;
    internal int heatWeeks = 17;
    Panel chartPanel, heatPanel, restartsPanel, hourPanel, itPanel;
    // Playtime-chart hover state (tooltips + hovered-bar highlight).
    readonly List<(RectangleF Slot, string Tip)> playBars = new();
    int playHoverIdx = -1;
    ToolTip chartTip;
    Label statTotalPlay, statWeekPlay, statLongest, statTotalRe,
          statAvgSess, statSessCount, statReToday, statPhotoCnt,
          statGoalToday, statStreak, statQuality, statDiscover;
    Label peakBody;
    ListBox topWorldsList, topPlayersList, topPhotosList;
    ListBox worldList, sessList, recordsList, wowList, crashList, avatarList;
    TableLayoutPanel statsGrid, chartsGrid, lbGrid;

    internal static string FormatDuration(int sec)
    {
        var ts = TimeSpan.FromSeconds(sec);
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1) return $"{(int)ts.TotalMinutes}m";
        return $"{ts.Seconds}s";
    }

    void BuildStatsPage()
    {
        pgStats = NewPage("Statistics");
        var statsTitle = NewPageTitle("Statistics");
        pgStats.Controls.Add(statsTitle);

        // Chart card
        var chartCard = Ui.NewCard();
        chartCard.Location = new Point(4, 52);
        chartCard.Size = new Size(840, 220);
        chartCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pgStats.Controls.Add(chartCard);

        var chartTitle = new Label
        {
            Name = "onCardMuted", Text = "PLAYTIME - LAST 7 DAYS", Font = Ui.FontSmall,
            AutoSize = true, Location = new Point(16, 14),
        };
        chartCard.Controls.Add(chartTitle);

        var btnChartRange = new Button
        {
            Text = "30d", Size = new Size(56, 24), Location = new Point(716, 10),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        Ui.StyleButton(btnChartRange, "secondary");
        btnChartRange.Click += (s, e) =>
        {
            chartDays = chartDays == 7 ? 30 : 7;
            btnChartRange.Text = chartDays == 7 ? "30d" : "7d";
            chartTitle.Text = $"PLAYTIME - LAST {chartDays} DAYS";
            chartPanel.Invalidate();
        };
        chartCard.Controls.Add(btnChartRange);

        var btnChartPng = new Button
        {
            Text = "PNG", Size = new Size(50, 24), Location = new Point(778, 10),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        Ui.StyleButton(btnChartPng, "secondary");
        btnChartPng.Click += (s, e) => ExportPanelPng(chartPanel, "playtime-chart.png");
        chartCard.Controls.Add(btnChartPng);

        chartPanel = new Panel
        {
            Name = "chart", Location = new Point(12, 38), Size = new Size(816, 168),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Ui.Card,
        };
        chartCard.Controls.Add(chartPanel);
        Ui.SetDoubleBuffered(chartPanel);   // flicker-free repaint on hover / data refresh
        chartPanel.Paint += (s, e) => DrawPlayChart((Panel)s, e.Graphics);
        // Hover tooltips: exact date + duration for the bar/column under the cursor.
        chartTip = new ToolTip { ShowAlways = true, InitialDelay = 100, ReshowDelay = 40, AutoPopDelay = 32000, UseAnimation = false, UseFading = false };
        chartPanel.MouseMove += ChartPlayMouseMove;
        chartPanel.MouseLeave += (s, e) =>
        {
            if (playHoverIdx == -1) return;
            playHoverIdx = -1;
            chartTip.Hide(chartPanel);
            chartPanel.Invalidate();
        };

        // Activity heatmap card (GitHub-style)
        var heatCard = Ui.NewCard();
        heatCard.Location = new Point(4, 284);
        heatCard.Size = new Size(840, 150);
        heatCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pgStats.Controls.Add(heatCard);
        var heatTitle = new Label
        {
            Name = "onCardMuted", Text = "ACTIVITY - LAST 17 WEEKS", Font = Ui.FontSmall,
            AutoSize = true, Location = new Point(16, 14),
        };
        heatCard.Controls.Add(heatTitle);

        var btnHeatPng = new Button
        {
            Text = "PNG", Size = new Size(50, 24), Location = new Point(778, 10),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        Ui.StyleButton(btnHeatPng, "secondary");
        btnHeatPng.Click += (s, e) => ExportPanelPng(heatPanel, "activity-heatmap.png");
        heatCard.Controls.Add(btnHeatPng);
        // Year / 17-week toggle.
        var btnHeatRange = new Button
        {
            Text = "1 year", Size = new Size(64, 24), Location = new Point(708, 10),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        Ui.StyleButton(btnHeatRange, "secondary");
        btnHeatRange.Click += (s, e) =>
        {
            heatWeeks = heatWeeks == 17 ? 53 : 17;
            btnHeatRange.Text = heatWeeks == 17 ? "1 year" : "17 wks";
            heatTitle.Text = heatWeeks == 17 ? "ACTIVITY - LAST 17 WEEKS" : "ACTIVITY - LAST YEAR";
            heatPanel.Invalidate();
        };
        heatCard.Controls.Add(btnHeatRange);
        heatPanel = new Panel
        {
            Name = "chart", Location = new Point(14, 38), Size = new Size(812, 100),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Ui.Card,
        };
        heatCard.Controls.Add(heatPanel);
        Ui.SetDoubleBuffered(heatPanel);
        heatPanel.Paint += (s, e) => DrawHeatmap((Panel)s, e.Graphics);

        // Summary stat cards (three rows)
        statsGrid = new TableLayoutPanel
        {
            Location = new Point(4, 446), Size = new Size(840, 288),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ColumnCount = 4, RowCount = 3,
        };
        for (int i = 0; i < 4; i++) statsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        for (int i = 0; i < 3; i++) statsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        pgStats.Controls.Add(statsGrid);

        Label AddStatsSummaryCard(string title, int col, int row = 0)
        {
            var card = Ui.NewCard();
            card.Margin = new Padding(0, 0, 12, 12);
            card.Dock = DockStyle.Fill;
            var t = new Label
            {
                Name = "onCardMuted", Text = title.ToUpper(), Font = Ui.FontSmall,
                AutoSize = true, Location = new Point(14, 14),
            };
            card.Controls.Add(t);
            var v = new Label
            {
                Name = "onCardAccent", Text = "-", Font = Ui.FontValue,
                AutoSize = true, Location = new Point(14, 38),
            };
            card.Controls.Add(v);
            statsGrid.Controls.Add(card, col, row);
            return v;
        }
        statTotalPlay = AddStatsSummaryCard("Total playtime", 0);
        statWeekPlay = AddStatsSummaryCard("This week", 1);
        statLongest = AddStatsSummaryCard("Longest session", 2);
        statTotalRe = AddStatsSummaryCard("Total restarts", 3);
        statAvgSess = AddStatsSummaryCard("Avg session", 0, 1);
        statSessCount = AddStatsSummaryCard("Sessions recorded", 1, 1);
        statReToday = AddStatsSummaryCard("Restarts today", 2, 1);
        statPhotoCnt = AddStatsSummaryCard("Photos tracked", 3, 1);
        statGoalToday = AddStatsSummaryCard("Today's goal", 0, 2);
        statStreak = AddStatsSummaryCard("Day streak", 1, 2);
        statQuality = AddStatsSummaryCard("Session quality", 2, 2);
        statDiscover = AddStatsSummaryCard("New worlds / wk", 3, 2);

        // Mini charts: restarts per day + time-of-day histogram
        chartsGrid = new TableLayoutPanel
        {
            Location = new Point(4, 746), Size = new Size(840, 170),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ColumnCount = 2, RowCount = 1,
        };
        for (int i = 0; i < 2; i++) chartsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        chartsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 162));
        pgStats.Controls.Add(chartsGrid);

        Panel AddMiniChartCard(string title, int col, string pngName, PaintEventHandler paint)
        {
            var card = Ui.NewCard();
            card.Margin = new Padding(0, 0, 12, 0);
            card.Dock = DockStyle.Fill;
            var t = new Label
            {
                Name = "onCardMuted", Text = title, Font = Ui.FontSmall,
                AutoSize = true, Location = new Point(14, 12),
            };
            card.Controls.Add(t);
            var p = new Panel
            {
                Name = "chart", Location = new Point(12, 36),
                Size = new Size(372, 114), BackColor = Ui.Card,
            };
            Ui.SetDoubleBuffered(p);
            p.Paint += paint;
            card.Controls.Add(p);
            var png = new Button { Text = "PNG", Size = new Size(46, 22), Location = new Point(338, 8) };
            Ui.StyleButton(png, "secondary");
            png.Click += (s, e) => ExportPanelPng(p, pngName);
            card.Controls.Add(png);
            chartsGrid.Controls.Add(card, col, 0);
            return p;
        }
        restartsPanel = AddMiniChartCard("RESTARTS - LAST 14 DAYS", 0, "restarts-chart.png", (s, e) => DrawRestartChart((Panel)s, e.Graphics));
        hourPanel = AddMiniChartCard("TIME OF DAY (ALL TIME)", 1, "time-of-day.png", (s, e) => DrawHourChart((Panel)s, e.Graphics));

        // Leaderboards: top worlds + top players + most photographed
        lbGrid = new TableLayoutPanel
        {
            Location = new Point(4, 928), Size = new Size(840, 200),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ColumnCount = 3, RowCount = 1,
        };
        for (int i = 0; i < 3; i++) lbGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        lbGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        pgStats.Controls.Add(lbGrid);

        ListBox AddLeaderboardCard(string title, int col)
        {
            var card = Ui.NewCard();
            card.Margin = new Padding(0, 0, 12, 0);
            card.Dock = DockStyle.Fill;
            var hdr = new Label
            {
                Name = "onCardMuted", Text = title, Font = Ui.FontSmall, Dock = DockStyle.Top,
                Height = 26, Padding = new Padding(12, 8, 0, 0),
            };
            var lb = new ListBox
            {
                Name = "logBox", Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
                Font = Ui.FontBody, IntegralHeight = false,
            };
            card.Controls.Add(lb);
            card.Controls.Add(hdr);
            lbGrid.Controls.Add(card, col, 0);
            return lb;
        }
        topWorldsList = AddLeaderboardCard("MOST-VISITED WORLDS", 0);
        topPlayersList = AddLeaderboardCard("MOST-SEEN PLAYERS", 1);
        topPhotosList = AddLeaderboardCard("MOST PHOTOGRAPHED", 2);

        // World history list + export
        var histLbl = new Label
        {
            Name = "muted", Text = "RECENT WORLDS", Font = Ui.FontSmall,
            AutoSize = true, Location = new Point(6, 1144),
        };
        pgStats.Controls.Add(histLbl);

        var exportCsvBtn = new Button
        {
            Text = "Export stats to CSV", AutoSize = true,
            Padding = new Padding(10, 4, 10, 4),
            Location = new Point(690, 1136),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        Ui.StyleButton(exportCsvBtn, "secondary");
        pgStats.Controls.Add(exportCsvBtn);

        worldList = new ListBox
        {
            Location = new Point(4, 1174), Size = new Size(840, 150),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Ui.FontBody, IntegralHeight = false,
        };
        pgStats.Controls.Add(worldList);

        // Session history browser
        var sessLbl = new Label
        {
            Name = "muted", Text = "SESSION HISTORY", Font = Ui.FontSmall,
            AutoSize = true, Location = new Point(6, 1340),
        };
        pgStats.Controls.Add(sessLbl);

        sessList = new ListBox
        {
            Location = new Point(4, 1368), Size = new Size(840, 150),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Ui.FontBody, IntegralHeight = false,
        };
        pgStats.Controls.Add(sessList);

        // Instance-type breakdown: time spent by access type
        var itCard = Ui.NewCard();
        itCard.Location = new Point(4, 1536);
        itCard.Size = new Size(840, 196);
        itCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pgStats.Controls.Add(itCard);
        var itTitle = new Label
        {
            Name = "onCardMuted", Text = "TIME BY INSTANCE TYPE", Font = Ui.FontSmall,
            AutoSize = true, Location = new Point(16, 14),
        };
        itCard.Controls.Add(itTitle);
        var btnItPng = new Button
        {
            Text = "PNG", Size = new Size(50, 24), Location = new Point(778, 10),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        Ui.StyleButton(btnItPng, "secondary");
        btnItPng.Click += (s, e) => ExportPanelPng(itPanel, "instance-types.png");
        itCard.Controls.Add(btnItPng);
        itPanel = new Panel
        {
            Name = "chart", Location = new Point(14, 38), Size = new Size(812, 146),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Ui.Card,
        };
        itCard.Controls.Add(itPanel);
        Ui.SetDoubleBuffered(itPanel);
        itPanel.Paint += (s, e) => DrawInstanceTypes((Panel)s, e.Graphics);

        // Peak-hours analysis + recommendations card
        var peakCard = Ui.NewCard();
        peakCard.Location = new Point(4, 1744);
        peakCard.Size = new Size(840, 128);
        peakCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pgStats.Controls.Add(peakCard);
        var peakTitle = new Label
        {
            Name = "onCardMuted", Text = "PEAK HOURS & RECOMMENDATIONS", Font = Ui.FontSmall,
            AutoSize = true, Location = new Point(16, 14),
        };
        peakCard.Controls.Add(peakTitle);
        peakBody = new Label
        {
            Name = "onCard", Font = Ui.FontBody, Text = "",
            Location = new Point(16, 40), Size = new Size(808, 76),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };
        peakCard.Controls.Add(peakBody);

        // Records + week-over-week, and crash-cause + avatar analytics (two 2-col rows).
        ListBox AddStatListCard(string title, TableLayoutPanel grid, int col)
        {
            var card = Ui.NewCard();
            card.Margin = new Padding(0, 0, 12, 0);
            card.Dock = DockStyle.Fill;
            var hdr = new Label
            {
                Name = "onCardMuted", Text = title, Font = Ui.FontSmall, Dock = DockStyle.Top,
                Height = 26, Padding = new Padding(12, 8, 0, 0),
            };
            var lb = new ListBox
            {
                Name = "logBox", Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
                Font = Ui.FontBody, IntegralHeight = false,
            };
            card.Controls.Add(lb);
            card.Controls.Add(hdr);
            grid.Controls.Add(card, col, 0);
            return lb;
        }
        var recGrid = new TableLayoutPanel
        {
            Location = new Point(4, 1884), Size = new Size(840, 200),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent,
        };
        for (int i = 0; i < 2; i++) recGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        recGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        pgStats.Controls.Add(recGrid);
        recordsList = AddStatListCard("PERSONAL RECORDS", recGrid, 0);
        wowList = AddStatListCard("THIS WEEK vs LAST WEEK", recGrid, 1);

        var anaGrid = new TableLayoutPanel
        {
            Location = new Point(4, 2098), Size = new Size(840, 200),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent,
        };
        for (int i = 0; i < 2; i++) anaGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        anaGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        pgStats.Controls.Add(anaGrid);
        crashList = AddStatListCard("CRASH CAUSES (all time)", anaGrid, 0);
        avatarList = AddStatListCard("MOST-WORN AVATARS", anaGrid, 1);

        exportCsvBtn.Click += (s, e) =>
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = "AeroVRC-stats.csv",
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("\"Type\",\"Start\",\"End\",\"DurationMin\",\"World\",\"Instance\"");
                string Csv(string x) => "\"" + (x ?? "").Replace("\"", "\"\"") + "\"";
                int written = 0;
                foreach (var sr in config.Sessions)
                {
                    sb.AppendLine($"{Csv("Session")},{Csv(sr.Start)},{Csv(sr.End)},\"{Math.Round(sr.DurationSec / 60.0, 1)}\",{Csv("")},{Csv("")}");
                    written++;
                }
                foreach (var w in config.WorldHistory)
                {
                    if (w.World == "Unknown world") continue;
                    sb.AppendLine($"{Csv("World")},{Csv(w.Time)},{Csv("")},\"{Math.Round(w.DurationSec / 60.0, 1)}\",{Csv(w.World)},{Csv(w.Instance)}");
                    written++;
                }
                if (written == 0)
                    sb.AppendLine($"{Csv("(no data yet)")},{Csv("")},{Csv("")},\"0\",{Csv("")},{Csv("")}");
                File.WriteAllText(dlg.FileName, sb.ToString());
                WriteLog($"Stats exported to CSV: {dlg.FileName}");
            }
            catch (Exception ex) { WriteLog($"CSV export failed: {ex.Message}"); }
        };
    }

    int PlaySecOn(string key) => config.PlayHistory.TryGetValue(key, out var v) ? (int)v : 0;

    // A rectangle with only its top two corners rounded - bars sit flush on the
    // baseline, so the bottom edge stays square.
    static GraphicsPath TopRoundedRect(float x, float y, float w, float h, float r)
    {
        r = Math.Max(0, Math.Min(r, Math.Min(w / 2f, h)));
        var p = new GraphicsPath();
        if (r <= 0.1f) { p.AddRectangle(new RectangleF(x, y, w, h)); return p; }
        float d = r * 2;
        p.AddArc(x, y, d, d, 180, 90);           // top-left
        p.AddArc(x + w - d, y, d, d, 270, 90);   // top-right
        p.AddLine(x + w, y + r, x + w, y + h);   // right edge down
        p.AddLine(x + w, y + h, x, y + h);       // bottom edge
        p.CloseFigure();                         // left edge back up
        return p;
    }

    // "Nice" y-axis step (in hours) giving ~3-4 gridlines at the current scale.
    static double NiceHourStep(double max)
    {
        if (max <= 0) return 1;
        double target = max / 3.5;
        foreach (var s in new[] { 0.25, 0.5, 1, 2, 3, 4, 6, 8, 12, 24 })
            if (s >= target) return s;
        return 24;
    }

    void DrawPlayChart(Panel panel, Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Ui.Card);
        var today = DateTime.Now.Date;
        int days = Math.Max(7, chartDays);
        var vals = new List<(string Label, double Hours, int Sec, DateTime Date)>();
        for (int i = days - 1; i >= 0; i--)
        {
            var d = today.AddDays(-i);
            int sec = PlaySecOn(d.ToString("yyyy-MM-dd"));
            // 7d: weekday names; 30d: day-of-month numbers.
            var lbl = days <= 7 ? d.ToString("ddd") : d.ToString("dd");
            vals.Add((lbl, Math.Round(sec / 3600.0, 2), sec, d));
        }
        double max = vals.Max(v => v.Hours);
        if (max <= 0.01) max = 1;
        int w = panel.Width, h = panel.Height;
        int n = vals.Count;

        using var lblFont = new Font("Segoe UI", 8);              // weekday / day-of-month
        using var valFont = new Font("Segoe UI Semibold", 8);    // bar value (the data pops)
        using var axisFont = new Font("Segoe UI", 7.5f);         // y-scale + goal tag
        using var mutedBrush = new SolidBrush(Ui.TextMuted);
        using var valBrush = new SolidBrush(Ui.Text);
        using var fmt = new StringFormat { Alignment = StringAlignment.Center };
        using var axisFmt = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

        // Size the axis-label band from the font's real height so day names never clip.
        int lblH = (int)Math.Ceiling(lblFont.GetHeight(g)) + 2;
        const int leftPad = 34, rightPad = 8;   // left gutter for the y-scale + goal tag
        int plotL = leftPad, plotR = w - rightPad;
        int baseY = h - lblH - 8;
        int topPad = lblH + 6;

        // Goal line height (drawn behind bars); NaN when disabled / off the scale.
        double goalHrs = config.Goals.DailyMin / 60.0;
        float goalY = float.NaN;
        if (goalHrs > 0 && goalHrs <= max)
            goalY = (float)(baseY - goalHrs / max * (baseY - topPad));

        // ----- y-axis gridlines + hour labels (behind everything) -----
        double step = NiceHourStep(max);
        using (var gridPen = new Pen(Color.FromArgb(46, Ui.BorderHi), 1))
        {
            for (double hv = step; hv <= max + 1e-6; hv += step)
            {
                float gy = (float)(baseY - hv / max * (baseY - topPad));
                if (gy < topPad - 1) break;
                g.DrawLine(gridPen, plotL, gy, plotR, gy);
                // Skip the number when it would sit on top of the goal tag.
                if (!float.IsNaN(goalY) && Math.Abs(gy - goalY) < 9) continue;
                string t = hv >= 1 ? $"{hv:0.#}h" : $"{(int)Math.Round(hv * 60)}m";
                g.DrawString(t, axisFont, mutedBrush, new RectangleF(0, gy - 7, leftPad - 6, 14), axisFmt);
            }
        }

        // ----- daily-goal reference line + left-gutter tag (behind the bars) -----
        if (!float.IsNaN(goalY))
        {
            using var goalPen = new Pen(Color.FromArgb(175, Ui.Success), 1.4f) { DashStyle = DashStyle.Dash };
            g.DrawLine(goalPen, plotL, goalY, plotR, goalY);
            using var gb = new SolidBrush(Ui.Success);
            g.DrawString("goal", axisFont, gb, new RectangleF(0, goalY - 7, leftPad - 6, 14), axisFmt);
        }

        // ----- baseline -----
        using (var basePen = new Pen(Ui.Border, 1))
            g.DrawLine(basePen, plotL, baseY, plotR, baseY);

        // ----- bars -----
        float slot = (plotR - plotL) / (float)n;
        float barW = Math.Min(64, slot * 0.5f);
        playBars.Clear();
        for (int i = 0; i < n; i++)
        {
            float cx = plotL + slot * i + slot / 2;
            float x = cx - barW / 2;
            var d = vals[i].Date;
            // Full-column hit rect so hovering anywhere over a day shows its tooltip.
            var slotRect = new RectangleF(cx - slot / 2, topPad, slot, baseY - topPad);
            playBars.Add((slotRect, $"{d:ddd, MMM d}  -  {(vals[i].Sec > 0 ? FormatDuration(vals[i].Sec) : "no playtime")}"));

            bool isToday = i == n - 1;
            bool isHover = i == playHoverIdx;

            if (vals[i].Sec <= 0)
            {
                // Ghost stub: an empty day reads as "0", not a missing gap.
                const float stubH = 3f;
                using var stub = TopRoundedRect(x, baseY - stubH, barW, stubH, 1.5f);
                using var sb = new SolidBrush(isToday ? Color.FromArgb(120, Ui.AccentHover) : Color.FromArgb(80, Ui.TextMuted));
                g.FillPath(sb, stub);
            }
            else
            {
                int bh = (int)(vals[i].Hours / max * (baseY - topPad));
                if (bh < 2) bh = 2;
                float y = baseY - bh;
                float r = Math.Min(6f, barW / 2f);
                using var path = TopRoundedRect(x, y, barW, bh, r);
                // vertical azure -> violet gradient (matches the nav accent pill)
                var gradRect = new RectangleF(x, y - 1, barW, bh + 2);
                using (var barBrush = new LinearGradientBrush(gradRect, Ui.AccentHover, Ui.Accent2, 90f))
                    g.FillPath(barBrush, path);
                // specular cap along the top edge for a little depth
                if (bh > 6)
                {
                    using var capPen = new Pen(Color.FromArgb(130, 255, 255, 255), 1);
                    g.DrawLine(capPen, x + r, y + 1.5f, x + barW - r, y + 1.5f);
                }
                // bright rim on today and on the hovered bar
                if (isToday || isHover)
                {
                    var rimCol = isHover ? Color.White : Ui.AccentHover;
                    using var rimPen = new Pen(Color.FromArgb(isHover ? 210 : 150, rimCol), 1.4f);
                    g.DrawPath(rimPen, path);
                }
                // value label above the bar (7-day view only; 30 bars would collide)
                if (n <= 7)
                {
                    var vr = new RectangleF(cx - slot / 2, y - lblH - 1, slot, lblH);
                    g.DrawString(FormatDuration(vals[i].Sec), valFont, valBrush, vr, fmt);
                }
            }

            // weekday / day-of-month label under the baseline
            var lr = new RectangleF(cx - slot / 2, baseY + 4, slot, lblH);
            g.DrawString(vals[i].Label, lblFont, mutedBrush, lr, fmt);
        }
    }

    void ChartPlayMouseMove(object sender, MouseEventArgs e)
    {
        int hit = -1;
        for (int i = 0; i < playBars.Count; i++)
        {
            if (playBars[i].Slot.Contains(e.Location)) { hit = i; break; }
        }
        if (hit == playHoverIdx) return;
        playHoverIdx = hit;
        chartPanel.Invalidate();
        if (hit >= 0) chartTip.Show(playBars[hit].Tip, chartPanel, e.X + 14, e.Y + 2);
        else chartTip.Hide(chartPanel);
    }

    // GitHub-style activity heatmap: week columns x 7 weekday rows, coloured by
    // how many hours were played each day.
    void DrawHeatmap(Panel panel, Graphics g)
    {
        g.SmoothingMode = SmoothingMode.None;
        g.Clear(Ui.Card);
        int weeks = heatWeeks;
        // Cell size fits BOTH the 7 weekday rows (height) and all week columns
        // (width), so the full-year view scales down to fit.
        int gap = weeks > 30 ? 2 : 3;
        int legendW = 90;
        int cellH = (panel.Height - 6 * gap) / 7;
        int cellW = (panel.Width - legendW - (weeks - 1) * gap) / weeks;
        int cell = Math.Max(4, Math.Min(cellH, cellW));
        var today = DateTime.Now.Date;
        // Start on the Sunday of the earliest week so columns line up by week.
        var start = today.AddDays(-((weeks - 1) * 7 + (int)today.DayOfWeek));
        using (var emptyBrush = new SolidBrush(Ui.Border))
        {
            for (int c = 0; c < weeks; c++)
            {
                for (int r = 0; r < 7; r++)
                {
                    var d = start.AddDays(c * 7 + r);
                    if (d > today) continue;
                    int x = c * (cell + gap);
                    int y = r * (cell + gap);
                    int sec = PlaySecOn(d.ToString("yyyy-MM-dd"));
                    double hrs = sec / 3600.0;
                    if (hrs <= 0.01)
                    {
                        g.FillRectangle(emptyBrush, x, y, cell, cell);
                    }
                    else
                    {
                        double t = Math.Min(1.0, hrs / 5.0);       // 5h+ = full intensity
                        int a = (int)(60 + 195 * t);               // alpha ramp 60..255
                        using var b = new SolidBrush(Color.FromArgb(a, Ui.Accent));
                        g.FillRectangle(b, x, y, cell, cell);
                    }
                }
            }
        }
        // Legend at right: less -> more
        using var lblFont = new Font("Segoe UI", 7.5f);
        using var mutedBrush = new SolidBrush(Ui.TextMuted);
        int lx = weeks * (cell + gap) + 12;
        g.DrawString("Less", lblFont, mutedBrush, lx, 0.0f);
        for (int i = 0; i < 5; i++)
        {
            double t = i / 4.0;
            int a = (int)(60 + 195 * t);
            using var b = new SolidBrush(Color.FromArgb(a, Ui.Accent));
            g.FillRectangle(b, lx + 34 + i * (cell + 2), 1.0f, cell, cell);
        }
        g.DrawString("More", lblFont, mutedBrush, lx + 34 + 5 * (cell + 2) + 4, 0.0f);
    }

    void DrawRestartChart(Panel panel, Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Ui.Card);
        const int days = 14;
        var today = DateTime.Now.Date;
        var vals = new List<(string L, int C)>();
        for (int i = days - 1; i >= 0; i--)
        {
            var d = today.AddDays(-i);
            config.RestartHistory.TryGetValue(d.ToString("yyyy-MM-dd"), out var c);
            vals.Add((d.ToString("dd"), c));
        }
        int max = vals.Max(v => v.C);
        if (max < 1) max = 1;
        int w = panel.Width, h = panel.Height;
        using var lblFont = new Font("Segoe UI", 7.5f);
        int lblH = (int)Math.Ceiling(lblFont.GetHeight(g)) + 2;
        int baseY = h - lblH - 4, topPad = lblH + 2;
        float slot = w / (float)days;
        float barW = Math.Min(20, slot * 0.6f);
        using var muted = new SolidBrush(Ui.TextMuted);
        using var fmt = new StringFormat { Alignment = StringAlignment.Center };
        using (var basePen = new Pen(Ui.Border, 1)) g.DrawLine(basePen, 0, baseY, w, baseY);
        for (int i = 0; i < days; i++)
        {
            float cx = slot * i + slot / 2;
            int bh = (int)(vals[i].C / (double)max * (baseY - topPad));
            if (bh < 2 && vals[i].C > 0) bh = 2;
            if (bh > 0)
            {
                float x = cx - barW / 2, y = baseY - bh, r = Math.Min(4f, barW / 2f);
                using var path = TopRoundedRect(x, y, barW, bh, r);
                var gr = new RectangleF(x, y - 1, barW, bh + 2);
                using var b = new LinearGradientBrush(gr, Ui.Shift(Ui.Danger, 34), Ui.Danger, 90f);
                g.FillPath(b, path);
            }
            var rr = new RectangleF(cx - slot / 2, baseY + 2, slot, lblH);
            g.DrawString(vals[i].L, lblFont, muted, rr, fmt);
            if (vals[i].C > 0)
            {
                var vr = new RectangleF(cx - slot / 2, baseY - bh - lblH, slot, lblH);
                g.DrawString(vals[i].C.ToString(), lblFont, muted, vr, fmt);
            }
        }
    }

    void DrawHourChart(Panel panel, Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Ui.Card);
        int w = panel.Width, h = panel.Height;
        using var lblFont = new Font("Segoe UI", 7.5f);
        int lblH = (int)Math.Ceiling(lblFont.GetHeight(g)) + 2;
        int baseY = h - lblH - 4, topPad = 4;
        var vals = new int[24];
        int max = 1;
        for (int hr = 0; hr < 24; hr++)
        {
            config.HourHistogram.TryGetValue(hr.ToString(), out var s);
            vals[hr] = s;
            if (s > max) max = s;
        }
        float slot = w / 24f;
        float barW = Math.Max(3, slot * 0.65f);
        using var muted = new SolidBrush(Ui.TextMuted);
        using (var basePen = new Pen(Ui.Border, 1)) g.DrawLine(basePen, 0, baseY, w, baseY);
        for (int hr = 0; hr < 24; hr++)
        {
            float cx = slot * hr + slot / 2;
            int bh = (int)(vals[hr] / (double)max * (baseY - topPad));
            if (bh < 2 && vals[hr] > 0) bh = 2;
            if (bh > 0)
            {
                float x = cx - barW / 2, y = baseY - bh, r = Math.Min(3f, barW / 2f);
                using var path = TopRoundedRect(x, y, barW, bh, r);
                var gr = new RectangleF(x, y - 1, barW, bh + 2);
                using var b = new LinearGradientBrush(gr, Ui.AccentHover, Ui.Accent2, 90f);
                g.FillPath(b, path);
            }
            if (hr % 6 == 0) g.DrawString($"{hr:00}", lblFont, muted, cx - 8, baseY + 2);
        }
    }

    // Colour per access type so the bars read at a glance.
    static Color GetItypeColor(string t) => t switch
    {
        "Public" => Ui.Accent,
        "Group Public" => Ui.Accent,
        "Friends+" => Color.FromArgb(72, 190, 200),
        "Friends" => Ui.Success,
        "Invite+" => Ui.Warning,
        "Invite" => Ui.Danger,
        "Group+" => Color.FromArgb(158, 124, 244),
        "Group" => Color.FromArgb(120, 138, 244),
        _ => Ui.TextMuted,
    };

    void DrawInstanceTypes(Panel panel, Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Ui.Card);
        var rows = config.InstanceTypeHist
            .Where(e => e.Value > 0)
            .Select(e => (Name: e.Key, Sec: e.Value))
            .OrderByDescending(r => r.Sec)
            .Take(8)
            .ToList();
        using var muted = new SolidBrush(Ui.TextMuted);
        if (rows.Count == 0)
        {
            using var f = new Font("Segoe UI", 9);
            using var cfmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("No instance data yet - spend time in some worlds.", f, muted, new RectangleF(0, 0, panel.Width, panel.Height), cfmt);
            return;
        }
        int total = rows.Sum(r => r.Sec);
        int max = rows[0].Sec;
        int w = panel.Width, h = panel.Height;
        using var labFont = new Font("Segoe UI", 9);
        using var valFont = new Font("Segoe UI Semibold", 9);
        const int labW = 96, durW = 96;
        const int barX = labW + 10;
        int barMaxW = Math.Max(40, w - barX - durW);
        int rowH = Math.Min(26, h / rows.Count);
        int barH = Math.Max(8, rowH - 9);
        using var lfmtR = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
        using var lfmtL = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            int y = i * rowH;
            g.DrawString(r.Name, labFont, muted, new RectangleF(0, y, labW, rowH), lfmtR);
            int bw = (int)(r.Sec / (double)max * barMaxW);
            if (bw < 3) bw = 3;
            var col = GetItypeColor(r.Name);
            var barRect = new RectangleF(barX, y + (rowH - barH) / 2f, bw, barH);
            using (var path = Ui.RoundedPath(barRect.X, barRect.Y, barRect.Width, barRect.Height, 3))
            using (var br = new LinearGradientBrush(barRect, Ui.Shift(col, 28), col, LinearGradientMode.Horizontal))
                g.FillPath(br, path);
            int pct = total > 0 ? (int)(100.0 * r.Sec / total) : 0;
            var txt = $"{FormatDuration(r.Sec)}  ({pct}%)";
            g.DrawString(txt, valFont, muted, new RectangleF(barX + barMaxW + 6, y, durW - 6, rowH), lfmtL);
        }
    }

    void RefreshSessionList()
    {
        sessList.BeginUpdate();
        sessList.Items.Clear();
        var items = config.Sessions.TakeLast(100).Reverse().ToList();
        foreach (var s in items)
            sessList.Items.Add($"{s.Start}   ->   {s.End}    ({FormatDuration(s.DurationSec)})");
        if (sessList.Items.Count == 0) sessList.Items.Add("(no sessions recorded yet)");
        sessList.EndUpdate();
    }

    void RefreshWorldList()
    {
        worldList.BeginUpdate();
        worldList.Items.Clear();
        var items = config.WorldHistory.Where(w => w.World != "Unknown world").TakeLast(100).Reverse().ToList();
        foreach (var w in items)
            worldList.Items.Add($"{w.Time}   {w.World}  (#{w.Instance})   -   {FormatDuration(w.DurationSec)}");
        worldList.EndUpdate();
    }

    void RefreshLeaderboards()
    {
        // Most-visited worlds: aggregate visit count + total time from WorldHistory.
        topWorldsList.BeginUpdate();
        topWorldsList.Items.Clear();
        var agg = new Dictionary<string, (int Count, int Sec)>();
        foreach (var w in config.WorldHistory)
        {
            var nm = w.World;
            if (string.IsNullOrEmpty(nm) || nm == "Unknown world") continue;
            agg.TryGetValue(nm, out var cur);
            agg[nm] = (cur.Count + 1, cur.Sec + w.DurationSec);
        }
        int rank = 1;
        foreach (var e in agg.OrderByDescending(e => e.Value.Count).Take(15))
        {
            topWorldsList.Items.Add($"{rank}. {e.Key}  -  {e.Value.Count} visits, {FormatDuration(e.Value.Sec)}");
            rank++;
        }
        if (topWorldsList.Items.Count == 0) topWorldsList.Items.Add("(no world history yet)");
        topWorldsList.EndUpdate();

        // Most-seen players from the persistent tally.
        topPlayersList.BeginUpdate();
        topPlayersList.Items.Clear();
        rank = 1;
        foreach (var e in config.PlayerSeen.OrderByDescending(e => e.Value).Take(15))
        {
            topPlayersList.Items.Add($"{rank}. {e.Key}  -  seen {e.Value}x");
            rank++;
        }
        if (topPlayersList.Items.Count == 0) topPlayersList.Items.Add("(no players seen yet)");
        topPlayersList.EndUpdate();

        // Most-photographed worlds.
        topPhotosList.BeginUpdate();
        topPhotosList.Items.Clear();
        rank = 1;
        foreach (var e in config.PhotoWorlds.OrderByDescending(e => e.Value).Take(15))
        {
            topPhotosList.Items.Add($"{rank}. {e.Key}  -  {e.Value} photo(s)");
            rank++;
        }
        if (topPhotosList.Items.Count == 0) topPhotosList.Items.Add("(no photos tracked yet)");
        topPhotosList.EndUpdate();
    }

    // ---- Analytics (goals / streaks / quality / discovery / peak hours) ----

    // A day "counts" toward the streak when its playtime meets the daily goal.
    bool TestGoalMet(string dayKey)
    {
        int goalSec = config.Goals.DailyMin * 60;
        if (goalSec <= 0) return false;
        return PlaySecOn(dayKey) >= goalSec;
    }

    // Current streak = consecutive goal-met days ending today (or yesterday, so the
    // streak isn't shown as broken until a day is actually missed). Also returns
    // the best streak seen across recorded history.
    (int Current, int Longest) GetStreaks()
    {
        var today = DateTime.Now.Date;
        int cur = 0;
        var anchor = today;
        if (!TestGoalMet(today.ToString("yyyy-MM-dd"))) anchor = today.AddDays(-1);
        var d = anchor;
        while (TestGoalMet(d.ToString("yyyy-MM-dd"))) { cur++; d = d.AddDays(-1); }
        // Longest: walk the whole retained window.
        int best = 0, run = 0;
        for (var x = today.AddDays(-420); x <= today; x = x.AddDays(1))
        {
            if (TestGoalMet(x.ToString("yyyy-MM-dd"))) { run++; if (run > best) best = run; }
            else run = 0;
        }
        if (config.Stats.LongestStreak > best) best = config.Stats.LongestStreak;
        if (cur > best) best = cur;
        if (best != config.Stats.LongestStreak) config.Stats.LongestStreak = best;
        return (cur, best);
    }

    // Session quality (0-100) over the last 7 days: stability (fewest restarts per
    // hour), session length, and how social the instances were (avg nearby players).
    (int Score, double CrashRate, double AvgLenMin)? GetSessionQuality()
    {
        var today = DateTime.Now.Date;
        int playSec = 0, restarts = 0;
        for (int i = 0; i < 7; i++)
        {
            var k = today.AddDays(-i).ToString("yyyy-MM-dd");
            playSec += PlaySecOn(k);
            if (config.RestartHistory.TryGetValue(k, out var re)) restarts += re;
        }
        if (playSec < 600) return null;   // need at least 10 min of data to score
        var cut = DateTime.Now.AddDays(-7);
        var durs = new List<int>();
        var playerCounts = new List<int>();
        foreach (var s in config.Sessions)
        {
            if (DateTime.TryParse(s.Start, out var st) && st >= cut)
            {
                durs.Add(s.DurationSec);
                if (s.AvgPlayers > 0) playerCounts.Add(s.AvgPlayers);
            }
        }
        double playHrs = playSec / 3600.0;
        double crashRate = restarts / Math.Max(playHrs, 0.1);            // restarts per hour
        double stability = 50.0 / (1.0 + crashRate);                     // 0 restarts -> 50
        double avgLenMin = durs.Count > 0 ? durs.Sum() / (double)durs.Count / 60.0 : playHrs * 60;
        double length = 30.0 * Math.Min(1.0, avgLenMin / 90.0);          // 90+ min avg -> full
        double social = playerCounts.Count > 0 ? 20.0 * Math.Min(1.0, playerCounts.Average() / 8.0) : 10.0;
        int score = (int)Math.Round(stability + length + social);
        return (Math.Max(0, Math.Min(100, score)), crashRate, avgLenMin);
    }

    // World-name -> first time it appears in WorldHistory (for the discovery rate).
    Dictionary<string, DateTime> GetWorldFirstSeen()
    {
        var first = new Dictionary<string, DateTime>();
        foreach (var w in config.WorldHistory)
        {
            var nm = w.World;
            if (string.IsNullOrEmpty(nm) || nm == "Unknown world") continue;
            if (!DateTime.TryParse(w.Time, out var t)) continue;
            if (!first.TryGetValue(nm, out var cur) || t < cur) first[nm] = t;
        }
        return first;
    }

    // New distinct worlds discovered this week + a per-week average over recent weeks.
    (int ThisWeek, double AvgPerWeek, int Total) GetDiscoveryStats()
    {
        var first = GetWorldFirstSeen();
        var today = DateTime.Now.Date;
        var weekAgo = today.AddDays(-7);
        int thisWeek = 0;
        var weekBuckets = new Dictionary<int, int>();
        foreach (var t in first.Values)
        {
            if (t >= weekAgo) thisWeek++;
            int wk = (int)Math.Floor((today - t.Date).TotalDays / 7);
            if (wk >= 0 && wk < 12)
            {
                weekBuckets.TryGetValue(wk, out var c);
                weekBuckets[wk] = c + 1;
            }
        }
        double avg = weekBuckets.Count > 0 ? weekBuckets.Values.Sum() / (double)weekBuckets.Count : 0.0;
        return (thisWeek, avg, first.Count);
    }

    // Peak-hours recommendation from the all-time hour-of-day histogram.
    string GetPeakHoursText()
    {
        var hist = config.HourHistogram;
        int tot = hist.Values.Sum();
        if (tot < 3600) return "Not enough playtime yet to spot your peak hours - check back after a few sessions.";
        // Best 3-hour window (wrapping across midnight).
        int bestStart = 0, bestSum = -1;
        for (int h = 0; h < 24; h++)
        {
            int sum = 0;
            for (int k = 0; k < 3; k++)
            {
                var hh = ((h + k) % 24).ToString();
                if (hist.TryGetValue(hh, out var v)) sum += v;
            }
            if (sum > bestSum) { bestSum = sum; bestStart = h; }
        }
        static string FmtH(int x)
        {
            var ap = "am"; int hh = x;
            if (x == 0) hh = 12;
            else if (x == 12) ap = "pm";
            else if (x > 12) { hh = x - 12; ap = "pm"; }
            return $"{hh}{ap}";
        }
        int winPct = (int)(100L * bestSum / tot);
        var peakStr = $"{FmtH(bestStart)}-{FmtH((bestStart + 3) % 24)}";
        var lines = new List<string>
        {
            $"You play most between {peakStr} ({winPct}% of your time is in that window).",
        };
        // Simple guidance keyed to the peak window.
        if (bestStart >= 18 || bestStart <= 1)
            lines.Add("That lines up with VRChat's global prime time - public lobbies are busiest 8pm-1am, so you're already catching the crowds.");
        else if (bestStart >= 2 && bestStart <= 8)
            lines.Add("Late-night / early hours are quieter for public instances. For busier lobbies, try joining before midnight.");
        else
            lines.Add("Daytime instances are calmer. If you want fuller public lobbies, evenings (8pm-midnight) are the most active.");
        return string.Join(Environment.NewLine, lines);
    }

    class WeekCompRow { public string Label; public int This, Last; public string Kind; public bool GoodUp; }

    // This-week vs last-week comparison for the headline stats.
    List<WeekCompRow> GetWeekComparison()
    {
        var today = DateTime.Now.Date;
        int SumRangeI(Dictionary<string, int> dict, int from, int to)
        {
            int s = 0;
            for (int i = from; i <= to; i++)
            {
                if (dict.TryGetValue(today.AddDays(-i).ToString("yyyy-MM-dd"), out var v)) s += v;
            }
            return s;
        }
        int SumRangeD(Dictionary<string, double> dict, int from, int to)
        {
            double s = 0;
            for (int i = from; i <= to; i++)
            {
                if (dict.TryGetValue(today.AddDays(-i).ToString("yyyy-MM-dd"), out var v)) s += v;
            }
            return (int)s;
        }
        int ptThis = SumRangeD(config.PlayHistory, 0, 6);
        int ptLast = SumRangeD(config.PlayHistory, 7, 13);
        int reThis = SumRangeI(config.RestartHistory, 0, 6);
        int reLast = SumRangeI(config.RestartHistory, 7, 13);
        var wThis = new HashSet<string>();
        var wLast = new HashSet<string>();
        var wkStart = today.AddDays(-6);
        var pvStart = today.AddDays(-13);
        var pvEnd = today.AddDays(-7);
        foreach (var v in config.WorldHistory)
        {
            var nm = v.World;
            if (string.IsNullOrEmpty(nm) || nm == "Unknown world") continue;
            if (!DateTime.TryParse(v.Time, out var t)) continue;
            var d = t.Date;
            if (d >= wkStart && d <= today) wThis.Add(nm);
            else if (d >= pvStart && d <= pvEnd) wLast.Add(nm);
        }
        int npThis = 0, npLast = 0;
        foreach (var fm in config.FirstMet.Values)
        {
            if (!DateTime.TryParse(fm.T, out var t)) continue;
            var d = t.Date;
            if (d >= wkStart && d <= today) npThis++;
            else if (d >= pvStart && d <= pvEnd) npLast++;
        }
        return new List<WeekCompRow>
        {
            new() { Label = "Playtime", This = ptThis, Last = ptLast, Kind = "dur", GoodUp = true },
            new() { Label = "Worlds", This = wThis.Count, Last = wLast.Count, Kind = "num", GoodUp = true },
            new() { Label = "New people", This = npThis, Last = npLast, Kind = "num", GoodUp = true },
            new() { Label = "Restarts", This = reThis, Last = reLast, Kind = "num", GoodUp = false },
        };
    }

    // All-time personal records.
    List<(string L, string V)> GetPersonalRecords()
    {
        var recs = new List<(string L, string V)>
        {
            ("Longest session", FormatDuration(config.Stats.LongestSessionSec)),
        };
        var streaks = GetStreaks();
        recs.Add(("Longest day streak", $"{streaks.Longest} day{(streaks.Longest == 1 ? "" : "s")}"));
        int maxDay = 0;
        foreach (var v in config.PlayHistory.Values) if ((int)v > maxDay) maxDay = (int)v;
        recs.Add(("Most played in a day", FormatDuration(maxDay)));
        // most distinct worlds visited in a single day
        var perDay = new Dictionary<string, HashSet<string>>();
        foreach (var v in config.WorldHistory)
        {
            var nm = v.World;
            if (string.IsNullOrEmpty(nm) || nm == "Unknown world") continue;
            if (!DateTime.TryParse(v.Time, out var t)) continue;
            var dk = t.Date.ToString("yyyy-MM-dd");
            if (!perDay.TryGetValue(dk, out var set)) { set = new HashSet<string>(); perDay[dk] = set; }
            set.Add(nm);
        }
        int maxWD = 0;
        foreach (var s in perDay.Values) if (s.Count > maxWD) maxWD = s.Count;
        recs.Add(("Most worlds in a day", maxWD.ToString()));
        recs.Add(("Most players seen at once", config.Stats.MaxPlayersSeen.ToString()));
        int maxRe = 0;
        foreach (var v in config.RestartHistory.Values) if (v > maxRe) maxRe = v;
        recs.Add(("Most restarts in a day", maxRe.ToString()));
        recs.Add(("Worlds discovered (all time)", GetDiscoveryStats().Total.ToString()));
        return recs;
    }

    // Fills the four analytics list cards (records / week-over-week / crash / avatars).
    void RefreshStatsAnalytics()
    {
        // Personal records
        recordsList.BeginUpdate();
        recordsList.Items.Clear();
        foreach (var (l, v) in GetPersonalRecords()) recordsList.Items.Add(string.Format("{0,-30} {1}", l, v));
        recordsList.EndUpdate();
        // Week-over-week deltas
        wowList.BeginUpdate();
        wowList.Items.Clear();
        foreach (var wr in GetWeekComparison())
        {
            string Fmt(int x) => wr.Kind == "dur" ? FormatDuration(x) : x.ToString();
            int diff = wr.This - wr.Last;
            var arrow = diff > 0 ? "▲" : diff < 0 ? "▼" : "=";
            var deltaTxt = diff == 0 ? "same" : $"{arrow} {Fmt(Math.Abs(diff))}";
            wowList.Items.Add(string.Format("{0,-12} {1,-10}  {2}   (last {3})", wr.Label, Fmt(wr.This), deltaTxt, Fmt(wr.Last)));
        }
        wowList.EndUpdate();
        // Crash causes
        crashList.BeginUpdate();
        crashList.Items.Clear();
        int ccTotal = config.CrashCauses.Values.Sum();
        foreach (var e in config.CrashCauses.OrderByDescending(e => e.Value))
        {
            int pct = ccTotal > 0 ? 100 * e.Value / ccTotal : 0;
            crashList.Items.Add(string.Format("{0,-20} {1}  ({2}%)", e.Key, e.Value, pct));
        }
        if (crashList.Items.Count == 0) crashList.Items.Add("(no crashes categorized yet - good!)");
        crashList.EndUpdate();
        // Most-worn avatars
        avatarList.BeginUpdate();
        avatarList.Items.Clear();
        int rank = 1;
        foreach (var e in config.AvatarUsage.OrderByDescending(e => e.Value).Take(20))
        {
            avatarList.Items.Add($"{rank}. {e.Key}  -  {e.Value}x");
            rank++;
        }
        if (avatarList.Items.Count == 0) avatarList.Items.Add("(no avatar switches recorded yet)");
        avatarList.EndUpdate();
    }

    internal void UpdateStatsPage()
    {
        int total = 0;
        foreach (var v in config.PlayHistory.Values) total += (int)v;
        statTotalPlay.Text = FormatDuration(total);
        int week = 0;
        var today = DateTime.Now.Date;
        for (int i = 0; i < 7; i++) week += PlaySecOn(today.AddDays(-i).ToString("yyyy-MM-dd"));
        statWeekPlay.Text = FormatDuration(week);
        statLongest.Text = FormatDuration(config.Stats.LongestSessionSec);
        statTotalRe.Text = config.Stats.TotalRestarts.ToString();
        // Second summary row.
        var sess = config.Sessions;
        if (sess.Count > 0)
        {
            int sum = sess.Sum(s => s.DurationSec);
            statAvgSess.Text = FormatDuration(sum / sess.Count);
        }
        else statAvgSess.Text = "-";
        statSessCount.Text = sess.Count.ToString();
        var tk = DateTime.Now.ToString("yyyy-MM-dd");
        statReToday.Text = config.RestartHistory.TryGetValue(tk, out var reT) ? reT.ToString() : "0";
        int pSum = config.PhotoWorlds.Values.Sum();
        statPhotoCnt.Text = pSum.ToString();
        // Row 3: goals / streak / quality / discovery.
        int todaySec = PlaySecOn(tk);
        int goalSec = config.Goals.DailyMin * 60;
        statGoalToday.Text = goalSec > 0 ? $"{FormatDuration(todaySec)} / {FormatDuration(goalSec)}" : FormatDuration(todaySec);
        statGoalToday.ForeColor = goalSec > 0 && todaySec >= goalSec ? Ui.Success : Ui.AccentHover;
        var streaks = GetStreaks();
        statStreak.Text = streaks.Current == 1 ? "1 day" : $"{streaks.Current} days";
        var q = GetSessionQuality();
        statQuality.Text = q.HasValue ? $"{q.Value.Score} / 100" : "-";
        statQuality.ForeColor = q.HasValue
            ? (q.Value.Score >= 80 ? Ui.Success : q.Value.Score >= 55 ? Ui.Warning : Ui.Danger)
            : Ui.AccentHover;
        var disc = GetDiscoveryStats();
        statDiscover.Text = $"{disc.ThisWeek}  (avg {disc.AvgPerWeek:0.0})";
        peakBody.Text = GetPeakHoursText();
        RefreshStatsAnalytics();
        chartPanel.Invalidate();
        heatPanel.Invalidate();
        restartsPanel.Invalidate();
        hourPanel.Invalidate();
        itPanel.Invalidate();
        RefreshWorldList();
        RefreshLeaderboards();
        RefreshSessionList();
    }
}
