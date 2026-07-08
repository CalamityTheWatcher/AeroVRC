namespace AeroVRC;

// ============================================================================
//  VRCX PAGE  (read-only view of your VRChat history from VRCX's database)
// ============================================================================

public partial class MainForm
{
    internal Panel pgVrcx;
    Label vrcxStatus;
    Label vrcxStatFriends, vrcxStatPeople, vrcxStatWorlds, vrcxStatActive;
    ListBox vrcxPeopleList, vrcxWorldsList, vrcxFriendsList, vrcxFeedList;

    void BuildVrcxPage()
    {
        pgVrcx = NewPage("VRCX");
        var vrcxTitle = NewPageTitle("VRCX");
        pgVrcx.Controls.Add(vrcxTitle);

        var vrcxSub = new Label
        {
            Name = "muted",
            Text = "Your VRChat history from VRCX - friends, people you've met and worlds you've visited. Read-only; VRCX's data is never modified.",
            Font = Ui.FontMuted, AutoSize = true, Location = new Point(4, 46),
        };
        pgVrcx.Controls.Add(vrcxSub);

        // Toolbar: Refresh + Open VRCX + status
        var vrcxRefreshBtn = new Button { Text = "Refresh", Size = new Size(84, 28), Location = new Point(4, 74) };
        Ui.StyleButton(vrcxRefreshBtn, "secondary");
        vrcxRefreshBtn.Click += (s, e) => { UpdateVrcxSnapshot(force: true); UpdateVrcxPage(); };
        pgVrcx.Controls.Add(vrcxRefreshBtn);

        var vrcxOpenBtn = new Button { Text = "Open VRCX", Size = new Size(96, 28), Location = new Point(94, 74) };
        Ui.StyleButton(vrcxOpenBtn, "secondary");
        vrcxOpenBtn.Click += (s, e) =>
        {
            if (!string.IsNullOrEmpty(config.VrcxPath) && File.Exists(config.VrcxPath)) StartExeApp(config.VrcxPath, "VRCX");
            else ShowToast("VRCX", "Set the VRCX path on the Apps page first.", Ui.Danger);
        };
        pgVrcx.Controls.Add(vrcxOpenBtn);

        vrcxStatus = new Label
        {
            Name = "muted", Text = "", Font = Ui.FontMuted,
            AutoSize = true, Location = new Point(200, 80),
        };
        pgVrcx.Controls.Add(vrcxStatus);

        // Summary cards (Friends / People met / Worlds / Active 24h)
        var vrcxSumGrid = new TableLayoutPanel
        {
            Location = new Point(4, 112), Size = new Size(840, 92),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ColumnCount = 4, RowCount = 1,
        };
        for (int i = 0; i < 4; i++) vrcxSumGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        vrcxSumGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        pgVrcx.Controls.Add(vrcxSumGrid);

        Label AddVrcxSummaryCard(string title, int col)
        {
            var card = Ui.NewCard();
            card.Margin = new Padding(0, 0, 12, 0);
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
            vrcxSumGrid.Controls.Add(card, col, 0);
            return v;
        }
        vrcxStatFriends = AddVrcxSummaryCard("Friends", 0);
        vrcxStatPeople = AddVrcxSummaryCard("People met", 1);
        vrcxStatWorlds = AddVrcxSummaryCard("Worlds visited", 2);
        vrcxStatActive = AddVrcxSummaryCard("Active (24h)", 3);

        // Leaderboards (People you've met / Most-visited worlds / Recently active friends)
        var vrcxLbGrid = new TableLayoutPanel
        {
            Location = new Point(4, 216), Size = new Size(840, 236),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ColumnCount = 3, RowCount = 1,
        };
        for (int i = 0; i < 3; i++) vrcxLbGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        vrcxLbGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 226));
        pgVrcx.Controls.Add(vrcxLbGrid);

        ListBox AddVrcxListCard(string title, int col)
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
                Font = Ui.FontMono, IntegralHeight = false,
            };
            card.Controls.Add(lb);
            card.Controls.Add(hdr);
            vrcxLbGrid.Controls.Add(card, col, 0);
            return lb;
        }
        vrcxPeopleList = AddVrcxListCard("PEOPLE YOU'VE MET", 0);
        vrcxWorldsList = AddVrcxListCard("MOST-VISITED WORLDS", 1);
        vrcxFriendsList = AddVrcxListCard("RECENTLY ACTIVE FRIENDS", 2);

        // Friend activity feed
        var vrcxFeedLbl = new Label
        {
            Name = "muted", Text = "FRIEND ACTIVITY", Font = Ui.FontSmall,
            AutoSize = true, Location = new Point(6, 464),
        };
        pgVrcx.Controls.Add(vrcxFeedLbl);

        vrcxFeedList = new ListBox
        {
            Location = new Point(4, 490), Size = new Size(840, 200),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Ui.FontMono, IntegralHeight = false,
        };
        pgVrcx.Controls.Add(vrcxFeedList);
    }

    // ISO-UTC (VRCX stores "...Z") -> local short time for display.
    static string FormatVrcxTime(string iso)
    {
        if (string.IsNullOrEmpty(iso)) return "";
        try { return DateTimeOffset.Parse(iso).LocalDateTime.ToString("MMM d  HH:mm"); }
        catch { return iso; }
    }

    internal void UpdateVrcxPage()
    {
        if (vrcxStatFriends == null) return;   // page not built yet
        var lists = new[] { vrcxPeopleList, vrcxWorldsList, vrcxFriendsList, vrcxFeedList };
        foreach (var l in lists) { l.BeginUpdate(); l.Items.Clear(); }
        void Reset(string msg)
        {
            foreach (var v in new[] { vrcxStatFriends, vrcxStatPeople, vrcxStatWorlds, vrcxStatActive }) v.Text = "-";
            vrcxPeopleList.Items.Add(msg);
        }

        if (!config.Vrcx.Enabled)
        {
            Reset("VRCX integration is turned off (Settings > VRCX).");
            vrcxStatus.Text = "Disabled in Settings.";
            foreach (var l in lists) l.EndUpdate();
            return;
        }
        if (!File.Exists(GetVrcxDbPath()))
        {
            Reset("VRCX database not found. Install VRCX, or set its path in Settings.");
            vrcxStatus.Text = $"No VRCX database at {GetVrcxDbPath()}";
            foreach (var l in lists) l.EndUpdate();
            return;
        }

        try
        {
            var pref = GetVrcxPrefix();

            // ---- Summary ----
            int I(string s) => int.TryParse(s, out var v) ? v : 0;
            vrcxStatPeople.Text = $"{I(GetVrcxScalar("SELECT COUNT(DISTINCT user_id) FROM gamelog_join_leave WHERE user_id<>''")):N0}";
            vrcxStatWorlds.Text = $"{I(GetVrcxScalar("SELECT COUNT(DISTINCT world_id) FROM gamelog_location WHERE world_id<>''")):N0}";
            if (pref != null)
            {
                vrcxStatFriends.Text = $"{I(GetVrcxScalar($"SELECT COUNT(*) FROM {pref}_friend_log_current")):N0}";
                vrcxStatActive.Text = $"{I(GetVrcxScalar($"SELECT COUNT(DISTINCT user_id) FROM {pref}_feed_online_offline WHERE type='Online' AND created_at>=datetime('now','-1 day')")):N0}";
            }
            else
            {
                vrcxStatFriends.Text = "-";
                vrcxStatActive.Text = "-";
            }

            // ---- People you've met ----
            var ppl = GetVrcxRows(@"
SELECT display_name AS name,
       COUNT(CASE WHEN type='OnPlayerJoined' THEN 1 END) AS enc,
       ROUND(SUM(CASE WHEN type='OnPlayerLeft' THEN time ELSE 0 END)/3600000.0, 1) AS hours
FROM gamelog_join_leave WHERE user_id<>''
GROUP BY user_id ORDER BY hours DESC, enc DESC LIMIT 14");
            if (ppl.Count > 0)
                foreach (var r in ppl) vrcxPeopleList.Items.Add(string.Format("{0,6}h  {1}  ({2}x)", r["hours"], r["name"], r["enc"]));
            else vrcxPeopleList.Items.Add("(no encounters logged yet)");

            // ---- Most-visited worlds ----
            var wl = GetVrcxRows(@"
SELECT world_name AS name, COUNT(*) AS visits,
       ROUND(SUM(time)/3600000.0, 1) AS hours
FROM gamelog_location WHERE world_id<>''
GROUP BY world_id ORDER BY hours DESC, visits DESC LIMIT 14");
            if (wl.Count > 0)
                foreach (var r in wl) vrcxWorldsList.Items.Add(string.Format("{0,6}h  {1,3}x  {2}", r["hours"], r["visits"], r["name"]));
            else vrcxWorldsList.Items.Add("(no world visits logged yet)");

            // ---- Recently active friends ----
            if (pref != null)
            {
                var ra = GetVrcxRows($@"
SELECT display_name AS name, MAX(created_at) AS last
FROM {pref}_feed_online_offline WHERE type='Online'
GROUP BY user_id ORDER BY last DESC LIMIT 16");
                if (ra.Count > 0)
                    foreach (var r in ra) vrcxFriendsList.Items.Add($"{FormatVrcxTime(r["last"])}  {r["name"]}");
                else vrcxFriendsList.Items.Add("(no friend activity logged yet)");

                // ---- Activity feed ----
                var feed = GetVrcxRows($@"
SELECT created_at, display_name AS name, type, world_name AS world
FROM {pref}_feed_online_offline ORDER BY created_at DESC LIMIT 60");
                if (feed.Count > 0)
                {
                    foreach (var r in feed)
                    {
                        var where = !string.IsNullOrEmpty(r.GetValueOrDefault("world")) ? $"  ->  {r["world"]}" : "";
                        vrcxFeedList.Items.Add(string.Format("{0,-14}  {1,-7}  {2}{3}", FormatVrcxTime(r["created_at"]), r["type"], r["name"], where));
                    }
                }
                else vrcxFeedList.Items.Add("(no friend activity logged yet)");
            }
            else
            {
                vrcxFriendsList.Items.Add("(friend feed not available)");
                vrcxFeedList.Items.Add("(friend feed not available - open VRCX at least once while logged in)");
            }

            vrcxStatus.Text = $"Updated {DateTime.Now:HH:mm:ss}   -   reading {Path.GetFileName(GetVrcxDbPath())}";
        }
        catch (Exception ex)
        {
            Reset($"Could not read VRCX database: {ex.Message}");
            vrcxStatus.Text = "Read error.";
        }
        finally
        {
            foreach (var l in lists) l.EndUpdate();
        }
    }
}
