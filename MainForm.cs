using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace AeroVRC;

// ============================================================================
//  MAIN FORM SHELL: window, nav rail, content host, page infra, log store.
//  Everything else (pages, services, monitoring engine) lives in the other
//  MainForm.*.cs partials - mirroring the single-scope layout of the original.
// ============================================================================

public partial class MainForm : Form
{
    // ===== Constants =====
    public const string ProcessName = "VRChat";
    public const string SteamAppId = "438100";
    public const string SteamVRAppId = "250820";
    public const string SteamVRProcess = "vrmonitor";

    // ===== Core state =====
    internal AppConfig config;
    internal bool loading = true;
    internal bool monitoring;
    internal int cooldownLeft;
    internal int sessionRestarts;
    internal long tick;
    internal int tickCounter;
    internal string currentPage = "Dashboard";
    internal string vdStreamerPath;

    // VRChat data locations
    internal readonly string vrcLowDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"AppData\LocalLow\VRChat\VRChat");
    internal readonly string vrcCacheDir;
    internal readonly string photoDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"Pictures\VRChat");

    // ===== Shell controls =====
    internal Panel nav, navHeader, navFooter, navLogo, navStatusDot, navSlider;
    internal FlowLayoutPanel navList, statusRow;
    internal TableLayoutPanel footGrid;
    internal Label navTitle, navSubtitle, navStatusText;
    internal Button toggleButton, panicButton;
    internal Panel content;
    internal Panel monitorBar;   // animated accent line shown across the top while monitoring
    internal Size contentDesignSize;
    internal Bitmap logoImage;

    internal Color dotColor = Ui.Stopped;
    internal double fxPhase;
    internal double logoPhase;
    internal int navSliderTarget;
    float navSliderPos; double navSliderVel;      // spring for the elastic nav slider
    // Page-crossfade overlay (old page dissolves into the new one).
    Panel xfadeOverlay; Bitmap xfadeBmpOld, xfadeBmpNew; double xfadeAlpha;

    internal readonly Dictionary<string, Panel> pages = new();
    internal bool photosLoaded;

    // ===== Log backing store (Logs page wiring lives in MainForm.LogsPage.cs) =====
    internal readonly List<string> logLines = new();
    internal bool logAutoScroll = true;
    internal string logFilter = "";
    internal TextBox logBox;
    internal Label appsStatus;

    public MainForm()
    {
        vrcCacheDir = Path.Combine(vrcLowDir, "Cache-WindowsPlayer");
        config = ConfigStore.Load();
        vdStreamerPath = config.VdStreamerPath;

        Text = "AeroVRC";
        // DPI-unaware process (see Program.cs): no WinForms auto-scaling; Windows
        // scales the whole window uniformly. Size is in 96-DPI logical pixels.
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(1120, 680);
        MinimumSize = new Size(1000, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Ui.Bg;
        Font = Ui.FontBody;

        Shown += (s, e) => Ui.SetDarkTitleBar(this);

        // Window / taskbar icon: prefer Logo.png (the "A" orb) next to the exe,
        // falling back to Logo.jpg, then to the embedded exe icon.
        var baseDir = AppContext.BaseDirectory;
        var logoPath = Path.Combine(baseDir, "Logo.png");
        if (!File.Exists(logoPath)) logoPath = Path.Combine(baseDir, "Logo.jpg");
        if (File.Exists(logoPath))
        {
            try
            {
                using var rawImg = Image.FromFile(logoPath);
                logoImage = new Bitmap(rawImg);
                Icon = Icon.FromHandle(logoImage.GetHicon());
            }
            catch { logoImage = null; }
        }

        BuildNavRail();
        BuildContentHost();
        InitPageParticles();

        // Pages, in the same order the script builds them.
        BuildOscInfra();
        BuildDashboardPage();
        BuildAppsPage();
        BuildPresetsPage();
        BuildStatsPage();
        BuildVrcxPage();
        BuildLogsPage();
        BuildSettingsPage();
        BuildPerformanceSettings();
        BuildBookmarksPage();
        BuildPhotosPage();

        BuildTimers();

        // ===== FINALISE =====
        RebuildAppsList();
        RebuildPresetsList();
        RebuildBookmarks();
        NewNavButton("Dashboard", "Dashboard");
        NewNavButton("Apps", "Apps");
        NewNavButton("Bookmarks", "Bookmarks");
        NewNavButton("Photos", "Photos");
        NewNavButton("Statistics", "Statistics");
        NewNavButton("VRCX", "VRCX");
        NewNavButton("Settings", "Settings");
        NewNavButton("Logs", "Logs");
        FitNavButtons();

        loading = false;
        ApplyTheme();
        UpdateStatsPage();
        ShowPage("Dashboard");
        WriteLog("AeroVRC ready.");

        // Verification hook: AEROVRC_SHOTDIR=<dir> renders every page off-screen to
        // PNGs and exits. Used by the build pipeline only; inert in normal runs.
        var shotDir = Environment.GetEnvironmentVariable("AEROVRC_SHOTDIR");
        if (!string.IsNullOrEmpty(shotDir))
        {
            RunScreenshotHarness(shotDir);
        }
        else
        {
            Opacity = 0;   // fade the window in on launch (ramped by the fx timer)
            // Animated welcome popup on startup (dismiss with button, Enter, or Esc).
            if (config.ShowWelcome) { try { Shown += (s, e) => ShowWelcomeScreen(); } catch { } }
        }

        timer.Start();
        fxTimer.Start();

        FormClosing += OnAppClosing;
        Resize += OnFormResize;
    }

    // Minimizing = "background watchdog" mode: the UI isn't visible, so shed the
    // heavy, easily-rebuilt memory (photo thumbnails), pause animation, compact the
    // heap, and hand the working set back to the OS. Everything reloads on restore.
    FormWindowState lastWinState = FormWindowState.Normal;
    void OnFormResize(object sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized)
        {
            if (lastWinState == FormWindowState.Minimized) return;   // debounce
            fxTimer.Stop();
            try { ClearPhotoGrid(); } catch { }                     // dispose thumbnail bitmaps
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            MemTrim.Trim();
        }
        else if (lastWinState == FormWindowState.Minimized)
        {
            if (!fxTimer.Enabled) fxTimer.Start();
            // Repopulate the photo grid if it was freed while minimized.
            if (currentPage == "Photos" && photosLoaded && photoGrid.Controls.Count == 0 && photoAll.Count > 0)
                try { ApplyPhotoFilter(); } catch { }
        }
        lastWinState = WindowState;
    }

    internal void SaveConfig() => ConfigStore.Save(config, loading);

    // ========================================================================
    //  NAV RAIL
    // ========================================================================
    void BuildNavRail()
    {
        nav = new Panel { Dock = DockStyle.Left, Width = 226, BackColor = Ui.Nav };
        Controls.Add(nav);

        // Footer (bottom): status + start/stop + panic, laid out in a fixed grid.
        navFooter = new Panel { Dock = DockStyle.Bottom, Height = 176, BackColor = Ui.Nav, Padding = new Padding(14, 8, 14, 14) };
        nav.Controls.Add(navFooter);

        footGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Ui.Nav };
        // Constrain the single column to the panel width. Without an explicit ColumnStyle
        // the Dock=Fill buttons render ~8px wider than the panel, clipping their right border.
        footGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        footGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        footGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        navFooter.Controls.Add(footGrid);

        statusRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Ui.Nav };
        footGrid.Controls.Add(statusRow, 0, 0);

        // Animated status dot: solid circle for the current state; while monitoring it
        // emits a soft expanding pulse ring (driven by the shared fx timer).
        navStatusDot = new Panel { Size = new Size(20, 22), BackColor = Ui.Nav, Margin = new Padding(0, 3, 2, 0) };
        navStatusDot.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = 10.0f, cy = 12.0f;
            if (monitoring)
            {
                // expanding, fading pulse ring
                double ph = fxPhase;
                float rr = (float)(4.0 + ph * 6.0);
                int al = (int)((1.0 - ph) * 120);
                if (al > 0)
                {
                    using var ringPen = new Pen(Color.FromArgb(al, dotColor), 2);
                    g.DrawEllipse(ringPen, cx - rr, cy - rr, rr * 2, rr * 2);
                }
            }
            using (var b = new SolidBrush(dotColor)) g.FillEllipse(b, cx - 4.5f, cy - 4.5f, 9, 9);
            // tiny specular highlight for depth
            using var hb = new SolidBrush(Color.FromArgb(90, 255, 255, 255));
            g.FillEllipse(hb, cx - 2.5f, cy - 3.5f, 3, 3);
        };
        statusRow.Controls.Add(navStatusDot);

        navStatusText = new Label
        {
            Text = "Stopped", Font = Ui.FontHeader, ForeColor = Color.White,
            BackColor = Ui.Nav, AutoSize = true, Margin = new Padding(0, 7, 0, 0),
        };
        statusRow.Controls.Add(navStatusText);

        toggleButton = new Button { Text = "Start Monitoring", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
        Ui.StyleButton(toggleButton, "primary");
        footGrid.Controls.Add(toggleButton, 0, 1);

        panicButton = new Button { Text = "Panic - Close All", Dock = DockStyle.Fill, Font = Ui.FontMuted, Margin = new Padding(0, 0, 0, 2) };
        Ui.StyleButton(panicButton, "ghost");
        footGrid.Controls.Add(panicButton, 0, 2);

        toggleButton.Click += (s, e) => ToggleMonitoring();
        panicButton.Click += (s, e) => InvokePanic();

        // Header (top): logo + title
        navHeader = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Ui.Nav };
        nav.Controls.Add(navHeader);

        // Animated "A" orb (custom-drawn) instead of a static logo image.
        navLogo = new Panel { Size = new Size(44, 44), Location = new Point(15, 24), BackColor = Ui.Nav };
        Ui.SetDoubleBuffered(navLogo);
        navLogo.Paint += (s, e) => DrawNavLogo((Panel)s, e.Graphics);
        navHeader.Controls.Add(navLogo);
        // Redraw the logo (still frame) whenever focus changes, so it settles cleanly
        // when the animation stops on blur.
        Activated += (s, e) => navLogo?.Invalidate();
        Deactivate += (s, e) => navLogo?.Invalidate();
        const int titleX = 66;

        navTitle = new Label
        {
            Text = "AeroVRC", Font = new Font("Segoe UI Semibold", 14), ForeColor = Color.White,
            BackColor = Ui.Nav, AutoSize = true, Location = new Point(titleX, 24),
        };
        navHeader.Controls.Add(navTitle);

        navSubtitle = new Label
        {
            Text = "FOR VRCHAT", Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Ui.NavText,
            BackColor = Ui.Nav, AutoSize = true, Location = new Point(titleX + 1, 55),
        };
        navHeader.Controls.Add(navSubtitle);

        // Nav button list (fills middle)
        navList = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,   // scroll the tab list when the rail is too short to fit them
            BackColor = Ui.Nav,
            Padding = new Padding(0, 10, 0, 0),
        };
        nav.Controls.Add(navList);
        navList.BringToFront();
        navList.ClientSizeChanged += (s, e) => FitNavButtons();

        // Sliding accent bar that glides to the active page's nav entry.
        navSlider = new Panel { Size = new Size(5, 26), Left = 0, BackColor = Ui.NavActive, Visible = false };
        Ui.SetDoubleBuffered(navSlider);
        // Rounded accent->violet pill with a soft glow bleeding into the active row.
        navSlider.Paint += (s, e) =>
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            var p = (Panel)s;
            int w = p.Width, h = p.Height;
            g.Clear(Ui.NavActive);
            using var path = Ui.RoundedPath(0, 0, w, h, w / 2.0f);
            var rf = new RectangleF(0, 0, w, h);
            using var gb = new LinearGradientBrush(rf, Ui.AccentHover, Ui.Accent2, 90.0f);
            g.FillPath(gb, path);
        };
        nav.Controls.Add(navSlider);
        navSlider.BringToFront();
        navSliderTarget = 0;
    }

    // Keep each nav button exactly the width of the list's client area. When the list
    // overflows and grows a vertical scrollbar the client area narrows, so the buttons
    // follow suit - no stray horizontal scrollbar, and full width when there's no bar.
    bool navFitting;
    internal void FitNavButtons()
    {
        if (navFitting) return;
        navFitting = true;
        int w = navList.ClientSize.Width;
        if (w > 20)
        {
            foreach (var b in navButtons)
                if (b.Width != w) b.Width = w;
        }
        navFitting = false;
    }

    // Nav icons are owner-drawn (not baked into the button text) so every glyph is
    // normalised to the same visual size and centred in a fixed cell. The stock font
    // renders these symbols at wildly different sizes; Segoe UI Symbol carries real
    // outlines for all of them, which we scale uniformly.
    static readonly Dictionary<string, char> navIcons = new()
    {
        ["Dashboard"] = '⌂',    // house
        ["Apps"] = '▦',         // grid
        ["Bookmarks"] = '★',    // star
        ["Photos"] = '▣',       // framed square (photo)
        ["Statistics"] = '▲',   // chart peak
        ["VRCX"] = '◈',         // diamond-in-diamond (social/history)
        ["Logs"] = '☰',         // lines
        ["Settings"] = '⚙',     // gear
    };
    static readonly FontFamily navIconFamily = GetNavIconFamily();
    static FontFamily GetNavIconFamily()
    {
        try { return new FontFamily("Segoe UI Symbol"); }
        catch { return FontFamily.GenericSansSerif; }
    }
    const float navIconCellX = 16.0f;    // left of the icon cell
    const float navIconCellW = 28.0f;    // cell width (icon centred within)
    const float navIconTarget = 19.0f;   // target glyph size (longest side), in px

    void DrawNavIcon(Button btn, Graphics g)
    {
        if (!navIcons.TryGetValue((string)btn.Tag, out var ch)) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        bool active = (string)btn.Tag == (currentPage == "Presets" ? "Apps" : currentPage);
        // Soft accent glow behind the active icon.
        if (active)
        {
            float icx = navIconCellX + navIconCellW / 2f, icy = btn.Height / 2f;
            using var gp = new GraphicsPath();
            gp.AddEllipse(icx - 16, icy - 16, 32, 32);
            using var pg = new PathGradientBrush(gp)
            {
                CenterColor = Color.FromArgb(85, Ui.Accent),
                SurroundColors = new[] { Color.FromArgb(0, Ui.Accent) },
            };
            g.FillPath(pg, gp);
        }
        using var path = new GraphicsPath();
        try
        {
            path.AddString(ch.ToString(), navIconFamily, 0, 34.0f, new PointF(0, 0), StringFormat.GenericTypographic);
            var bn = path.GetBounds();
            if (bn.Width > 0.1f && bn.Height > 0.1f)
            {
                float scale = navIconTarget / Math.Max(bn.Width, bn.Height);
                float gw = bn.Width * scale, gh = bn.Height * scale;
                float tx = navIconCellX + (navIconCellW - gw) / 2.0f - bn.X * scale;
                float ty = (btn.Height - gh) / 2.0f - bn.Y * scale;
                using var m = new Matrix();
                m.Translate(tx, ty); m.Scale(scale, scale);
                path.Transform(m);
                using var brush = new SolidBrush(active ? Ui.AccentHover : btn.ForeColor);
                g.FillPath(brush, path);
            }
        }
        catch { }
    }

    // Count badge (Apps / Bookmarks) on the right of a nav button.
    void DrawNavBadge(Button btn, Graphics g)
    {
        int count = (string)btn.Tag switch
        {
            "Apps" => config.CustomApps.Count,
            "Bookmarks" => config.Bookmarks.Count,
            _ => -1,
        };
        if (count <= 0) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        string txt = count > 99 ? "99+" : count.ToString();
        using var f = new Font("Segoe UI Semibold", 8f);
        var sz = g.MeasureString(txt, f);
        float bw = Math.Max(20, sz.Width + 10), bh = 18;
        float bx = btn.Width - bw - 14, by = (btn.Height - bh) / 2f;
        bool active = (string)btn.Tag == (currentPage == "Presets" ? "Apps" : currentPage);
        using (var path = Ui.RoundedPath(bx, by, bw, bh, bh / 2f))
        using (var bb = new SolidBrush(active ? Ui.Accent : Ui.NavActive))
            g.FillPath(bb, path);
        using var tb = new SolidBrush(active ? Color.White : Ui.NavText);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(txt, f, tb, new RectangleF(bx, by, bw, bh), sf);
    }

    internal readonly List<Button> navButtons = new();
    Button NewNavButton(string text, string key)
    {
        var b = new Button
        {
            Text = text, Tag = key, Width = 226, Height = 44,
            Margin = new Padding(0),
            Padding = new Padding(50, 0, 0, 0),   // leave room for the icon cell
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = Ui.FontNav, ForeColor = Ui.NavText, BackColor = Ui.Nav,
            Cursor = Cursors.Hand,
        };
        b.FlatAppearance.BorderSize = 0;
        b.Click += (s, e) => ShowPage((string)((Button)s).Tag);
        b.Paint += (s, e) => { DrawNavIcon((Button)s, e.Graphics); DrawNavBadge((Button)s, e.Graphics); };
        navButtons.Add(b);
        navList.Controls.Add(b);
        return b;
    }

    internal void RefreshNavButtons()
    {
        // The Presets page has no rail entry - it lives under Apps, so keep Apps lit there.
        string activeKey = currentPage == "Presets" ? "Apps" : currentPage;
        foreach (var b in navButtons)
        {
            if ((string)b.Tag == activeKey)
            {
                b.BackColor = Ui.NavActive;
                b.ForeColor = Color.White;
                b.Font = Ui.FontNavActive;
                // glide the accent bar to this entry (animated by the fx timer), clamped
                // to the rail so a scrolled list never parks it over the header/footer
                // Centre using the slider's RESTING height (26), not its live height -
                // it stretches while moving, and the fx spring offsets Top for that.
                const int baseH = 26;
                int target = navList.Top + b.Top + (b.Height - baseH) / 2;
                int lo = navList.Top, hi = navList.Top + navList.Height - baseH;
                navSliderTarget = Math.Max(lo, Math.Min(hi, target));
                navSlider.BackColor = Ui.Accent;
                if (!navSlider.Visible) { navSliderPos = navSliderTarget; navSliderVel = 0; navSlider.Top = navSliderTarget; navSlider.Visible = true; }
            }
            else
            {
                b.BackColor = Ui.Nav;
                b.ForeColor = Ui.NavText;
                b.Font = Ui.FontNav;
            }
            b.FlatAppearance.MouseOverBackColor = Ui.NavHover;
        }
    }

    // Animated "A" orb logo (same look as the welcome splash). Breathing pulse only
    // while the window is focused AND Effects.LogoAnim is on; still orb otherwise.
    internal void DrawNavLogo(Panel panel, Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Ui.Nav);
        float cx = panel.Width / 2.0f, cy = panel.Height / 2.0f;
        bool animOn = config.Effects.LogoAnim && ContainsFocus;
        double pulse = animOn ? 0.5 + 0.5 * Math.Sin(logoPhase) : 0.5;
        var bright = Ui.AccentHover;
        var deep = Color.FromArgb(28, 92, 200);
        float lsz = (float)(32.0 + pulse * 3.0);
        // Soft glow that fades fully to transparent before the panel edge (a radial
        // brush, so it never clips into a visible box like discrete rings would).
        float halo = (float)((lsz / 2) + 6.0 + pulse * 1.5);
        halo = Math.Min(halo, panel.Width / 2.0f - 1);   // keep inside the panel
        using (var hp = new GraphicsPath())
        {
            hp.AddEllipse(cx - halo, cy - halo, halo * 2, halo * 2);
            using var hpg = new PathGradientBrush(hp)
            {
                CenterColor = Color.FromArgb(70, Ui.Accent),
                SurroundColors = new[] { Color.FromArgb(0, Ui.Accent) },
            };
            g.FillPath(hpg, hp);
        }
        // sphere
        var orbRect = new RectangleF(cx - lsz / 2, cy - lsz / 2, lsz, lsz);
        using (var orb = new GraphicsPath())
        {
            orb.AddEllipse(orbRect);
            using (var pgb = new PathGradientBrush(orb))
            {
                pgb.CenterPoint = new PointF(cx - lsz * 0.16f, cy - lsz * 0.18f);
                pgb.CenterColor = bright;
                pgb.SurroundColors = new[] { deep };
                g.FillPath(pgb, orb);
            }
            using var rimPen = new Pen(Color.FromArgb(150, bright), 1.4f);
            g.DrawPath(rimPen, orb);
        }
        // specular highlight
        using (var spec = new SolidBrush(Color.FromArgb(150, Color.White)))
            g.FillEllipse(spec, cx - lsz * 0.26f, cy - lsz * 0.28f, lsz * 0.16f, lsz * 0.12f);
        // letter A
        using var ab = new SolidBrush(Color.White);
        using var af = new Font("Segoe UI Semibold", lsz * 0.5f);
        using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("A", af, ab, new RectangleF(cx - lsz / 2, cy - lsz / 2 - 1, lsz, lsz), fmt);
    }

    // ========================================================================
    //  CONTENT HOST + PAGES
    // ========================================================================
    void BuildContentHost()
    {
        content = new Panel { Name = "page", Dock = DockStyle.Fill, BackColor = Ui.Bg };
        Controls.Add(content);
        content.BringToFront();

        // Live-monitoring accent line across the very top of the content area. Kept at
        // form level (above content) so it survives page BringToFront; hidden when idle.
        monitorBar = new Panel
        {
            Location = new Point(nav.Width, 0),
            Size = new Size(Math.Max(10, ClientSize.Width - nav.Width), 3),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Visible = false, BackColor = Ui.Bg,
        };
        Ui.SetDoubleBuffered(monitorBar);
        monitorBar.Paint += (s, e) => DrawMonitorBar((Panel)s, e.Graphics);
        Controls.Add(monitorBar);
        monitorBar.BringToFront();

        // Design-time content size = form client width minus the nav rail. Pages are
        // pinned to this so anchored children compute correct margins from the start.
        contentDesignSize = new Size(ClientSize.Width - nav.Width, ClientSize.Height);
    }

    // Animated azure->violet line with a travelling highlight; signals "monitoring".
    void DrawMonitorBar(Panel p, Graphics g)
    {
        int w = p.Width, h = p.Height;
        if (w < 4) return;
        using (var lg = new LinearGradientBrush(new Rectangle(0, 0, w, h), Ui.Accent, Ui.Accent2, LinearGradientMode.Horizontal))
            g.FillRectangle(lg, 0, 0, w, h);
        // travelling glint
        float hx = (float)((Environment.TickCount % 2400) / 2400.0 * (w + 120) - 60);
        using var glow = new LinearGradientBrush(new RectangleF(hx - 60, 0, 120, h), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(150, 255, 255, 255), LinearGradientMode.Horizontal);
        g.FillRectangle(glow, hx - 60, 0, 60, h);
        using var glow2 = new LinearGradientBrush(new RectangleF(hx, 0, 120, h), Color.FromArgb(150, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), LinearGradientMode.Horizontal);
        g.FillRectangle(glow2, hx, 0, 60, h);
    }

    // ===== Animated page background (deep navy + drifting sparkles) =====
    // Shared sparkle field. Positions are viewport pixels (drawn in client space so
    // they stay put while the page scrolls). Only the visible page is animated.
    internal class Particle
    {
        public double X, Y, VX, VY;
        public int S, A;
        public Color C;
        public double Ph;
    }
    internal class SparkStyleDef
    {
        public Color[] Colors;
        public bool Down;
        public double VYmin, VYrng, VXrng;
        public int SizeMax;
        public bool Flicker;
    }
    internal List<Particle> pageParticles = new();
    internal SparkStyleDef sparkStyle;
    internal readonly Random sparkRnd = new();
    SolidBrush sparkBrush;

    internal static SparkStyleDef GetSparkStyle(string name) => name switch
    {
        "snow" => new SparkStyleDef { Colors = new[] { Color.FromArgb(255, 255, 255), Color.FromArgb(205, 224, 255) }, Down = true, VYmin = 0.20, VYrng = 0.55, VXrng = 0.60, SizeMax = 3, Flicker = false },
        "embers" => new SparkStyleDef { Colors = new[] { Color.FromArgb(255, 150, 60), Color.FromArgb(255, 95, 40), Color.FromArgb(255, 200, 110) }, Down = false, VYmin = 0.35, VYrng = 0.85, VXrng = 0.40, SizeMax = 3, Flicker = true },
        "stars" => new SparkStyleDef { Colors = new[] { Color.FromArgb(190, 205, 255), Color.FromArgb(255, 255, 255) }, Down = false, VYmin = 0.02, VYrng = 0.10, VXrng = 0.05, SizeMax = 3, Flicker = true },
        "sakura" => new SparkStyleDef { Colors = new[] { Color.FromArgb(255, 190, 214), Color.FromArgb(255, 150, 190), Color.FromArgb(255, 214, 228) }, Down = true, VYmin = 0.18, VYrng = 0.45, VXrng = 0.95, SizeMax = 3, Flicker = false },
        _ => new SparkStyleDef { Colors = new[] { Ui.Spark }, Down = false, VYmin = 0.12, VYrng = 0.55, VXrng = 0.30, SizeMax = 3, Flicker = false },
    };

    internal void InitPageParticles()
    {
        var rnd = new Random();
        int w = Math.Max(200, contentDesignSize.Width);
        int h = Math.Max(200, contentDesignSize.Height);
        var st = GetSparkStyle(config.Effects.Style);
        sparkStyle = st;
        int nc = st.Colors.Length;
        pageParticles = new List<Particle>();
        for (int i = 0; i < 70; i++)
        {
            pageParticles.Add(new Particle
            {
                X = rnd.Next(0, w), Y = rnd.Next(0, h),
                VX = (rnd.NextDouble() - 0.5) * st.VXrng,
                VY = st.VYmin + rnd.NextDouble() * st.VYrng,
                S = 1 + rnd.Next(0, st.SizeMax), A = 22 + rnd.Next(0, 86),
                C = st.Colors[rnd.Next(0, nc)],
                Ph = rnd.NextDouble() * 6.283,
            });
        }
    }

    // A page's Paint: flat navy fill + the sparkle field showing through the gaps
    // between cards/controls. ResetTransform pins the dots to the viewport regardless
    // of scroll; Clear fills the whole visible region either way.
    internal void DrawPageBackground(Panel panel, Graphics g)
    {
        g.ResetTransform();
        g.Clear(Ui.Bg);
        if (!config.Effects.Sparkles || pageParticles.Count == 0) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        bool flick = sparkStyle != null && sparkStyle.Flicker;
        double t = Environment.TickCount / 320.0;   // smooth rising time for the twinkle
        int h = panel.Height;
        // Reuse one brush (its Color is settable) instead of allocating/disposing a
        // SolidBrush per particle every frame - identical output, no GDI churn.
        sparkBrush ??= new SolidBrush(Color.White);
        var b = sparkBrush;
        foreach (var p in pageParticles)
        {
            if (p.Y < -6 || p.Y > h + 6) continue;
            int a = p.A;
            if (flick) a = (int)(p.A * (0.4 + 0.6 * (0.5 + 0.5 * Math.Sin(t + p.Ph))));
            if (a < 1) continue;
            var col = p.C;
            // soft halo behind the bigger dots -> a gentle glow
            if (p.S >= 3)
            {
                b.Color = Color.FromArgb((int)(a * 0.32), col);
                g.FillEllipse(b, (float)(p.X - p.S), (float)(p.Y - p.S), p.S * 3, p.S * 3);
            }
            b.Color = Color.FromArgb(a, col);
            g.FillEllipse(b, (float)p.X, (float)p.Y, p.S, p.S);
        }
    }

    internal Panel NewPage(string name)
    {
        var pg = new Panel
        {
            Name = "page",
            Dock = DockStyle.Fill,
            BackColor = Ui.Bg,
            Padding = new Padding(28, 22, 28, 22),
            AutoScroll = true,
            Visible = false,
            Size = contentDesignSize,
        };
        Ui.SetDoubleBuffered(pg);
        pg.Paint += (s, e) => DrawPageBackground((Panel)s, e.Graphics);
        content.Controls.Add(pg);
        pages[name] = pg;
        return pg;
    }

    internal static Label NewPageTitle(string text) => new()
    {
        Name = "title",
        Text = text,
        Font = Ui.FontBig,
        ForeColor = Ui.Text,
        BackColor = Ui.Bg,
        AutoSize = true,
        Location = new Point(4, 2),
    };

    internal void ShowPage(string name)
    {
        // Snapshot the outgoing page for a crossfade (skipped on first show / harness).
        Bitmap oldBmp = null;
        bool animate = fxTimer != null && fxTimer.Enabled && currentPage != name;
        if (animate && pages.TryGetValue(currentPage, out var oldPg) && oldPg.Visible && oldPg.Width > 4 && oldPg.Height > 4)
        {
            try { oldBmp = new Bitmap(oldPg.Width, oldPg.Height); oldPg.DrawToBitmap(oldBmp, new Rectangle(0, 0, oldPg.Width, oldPg.Height)); }
            catch { oldBmp?.Dispose(); oldBmp = null; }
        }

        currentPage = name;
        foreach (var k in pages.Keys) pages[k].Visible = (k == name);
        if (pages.TryGetValue(name, out var pg)) pg.BringToFront();
        RefreshNavButtons();
        // Load photos the first time the Photos tab is opened (a scan can be slow).
        // If the grid was freed while minimized, just re-decode the thumbnails.
        if (name == "Photos")
        {
            if (!photosLoaded) { photosLoaded = true; ScanPhotos(); }
            else if (photoGrid.Controls.Count == 0 && photoAll.Count > 0) ApplyPhotoFilter();
        }
        // Refresh the VRCX page whenever it's opened (cheap; reads a throttled snapshot).
        if (name == "VRCX") { try { UpdateVrcxPage(); } catch { } }
        // The dashboard is only updated by the tick while it's the visible page, so
        // refresh it immediately on switch rather than waiting up to a second.
        if (name == "Dashboard") { try { UpdateDashboard(lastProc); } catch { } }

        // Kick off the crossfade: snapshot the freshly-shown page, dissolve old -> new.
        if (oldBmp != null && pages.TryGetValue(name, out var npg) && npg.Width > 4 && npg.Height > 4)
        {
            Bitmap newBmp = null;
            try { newBmp = new Bitmap(npg.Width, npg.Height); npg.DrawToBitmap(newBmp, new Rectangle(0, 0, npg.Width, npg.Height)); }
            catch { newBmp?.Dispose(); newBmp = null; }
            if (newBmp != null) StartPageXfade(oldBmp, newBmp); else oldBmp.Dispose();
        }
        else oldBmp?.Dispose();
    }

    void StartPageXfade(Bitmap oldBmp, Bitmap newBmp)
    {
        xfadeBmpOld?.Dispose(); xfadeBmpNew?.Dispose();
        xfadeBmpOld = oldBmp; xfadeBmpNew = newBmp; xfadeAlpha = 1.0;
        if (xfadeOverlay == null)
        {
            xfadeOverlay = new Panel { BackColor = Ui.Bg };
            Ui.SetDoubleBuffered(xfadeOverlay);
            xfadeOverlay.Paint += (s, e) =>
            {
                var g = e.Graphics;
                var r = new Rectangle(0, 0, xfadeOverlay.Width, xfadeOverlay.Height);
                if (xfadeBmpNew != null) g.DrawImage(xfadeBmpNew, r, 0, 0, xfadeBmpNew.Width, xfadeBmpNew.Height, GraphicsUnit.Pixel);
                if (xfadeBmpOld != null && xfadeAlpha > 0)
                {
                    var cm = new ColorMatrix { Matrix33 = (float)xfadeAlpha };
                    using var ia = new ImageAttributes(); ia.SetColorMatrix(cm);
                    g.DrawImage(xfadeBmpOld, r, 0, 0, xfadeBmpOld.Width, xfadeBmpOld.Height, GraphicsUnit.Pixel, ia);
                }
            };
            content.Controls.Add(xfadeOverlay);
        }
        xfadeOverlay.Bounds = content.ClientRectangle;
        xfadeOverlay.Visible = true;
        xfadeOverlay.BringToFront();
        xfadeOverlay.Invalidate();
    }

    // ========================================================================
    //  LOG (backing store; the Logs page wires the view)
    // ========================================================================
    internal bool LinePassesFilter(string line)
    {
        if (string.IsNullOrEmpty(logFilter)) return true;
        return line.ToLower().Contains(logFilter.ToLower());
    }

    internal void WriteLog(string msg)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        logLines.Add(line);
        if (logLines.Count > 5000) logLines.RemoveRange(0, 1000);
        if (logBox != null && LinePassesFilter(line))
        {
            logBox.AppendText(line + "\r\n");
            if (logAutoScroll)
            {
                logBox.SelectionStart = logBox.TextLength;
                logBox.ScrollToCaret();
            }
        }
    }

    internal void SetAppsStatus(string msg)
    {
        if (appsStatus != null) appsStatus.Text = msg;
    }

    // ========================================================================
    //  LIVE RE-THEMING (single palette; walk restyles everything consistently)
    // ========================================================================
    // Controls coloured by CLR type, with per-control .Name overrides. Labels use
    // a SOLID background matching their parent (page = Bg, card = Card) rather
    // than Color.Transparent - WinForms "transparent" labels are faked by painting
    // the parent, which on AutoScroll panels leaves opaque mismatched boxes.
    internal void ApplyRoleColor(Control c)
    {
        switch (c.Name)
        {
            case "page": c.BackColor = Ui.Bg; return;
            case "card": c.BackColor = Ui.Card; return;
            case "muted": c.BackColor = Ui.Bg; c.ForeColor = Ui.TextMuted; return;      // muted text on a page
            case "onCard": c.BackColor = Ui.Card; c.ForeColor = Ui.Text; return;
            case "onCardMuted": c.BackColor = Ui.Card; c.ForeColor = Ui.TextMuted; return;
            case "onCardAccent": c.BackColor = Ui.Card; c.ForeColor = Ui.AccentHover; return;   // hero metric numbers
            case "primaryBtn": Ui.StyleButton((Button)c, "primary"); return;
            case "dangerBtn": Ui.StyleButton((Button)c, "danger"); return;
            case "logBox": c.BackColor = Ui.LogBg; c.ForeColor = Ui.Text; return;
            case "title": c.BackColor = Ui.Bg; c.ForeColor = Ui.Text; return;           // page title, no box
            case "section": c.BackColor = Ui.Bg; c.ForeColor = Ui.AccentHover; return;   // dashboard band header
            case "statusbig": c.BackColor = Ui.Bg; return;                              // colour set dynamically
            case "chart": c.BackColor = Ui.Card; c.Invalidate(); return;
        }
        switch (c)
        {
            case Button btn: Ui.StyleButton(btn, "secondary"); return;
            case NumericUpDown: c.BackColor = Ui.InputBg; c.ForeColor = Ui.Text; return;
            case TextBox: c.BackColor = Ui.InputBg; c.ForeColor = Ui.Text; return;
            case ComboBox cmb: Ui.StyleCombo(cmb); return;
            case CheckBox: c.BackColor = Ui.Card; c.ForeColor = Ui.Text; return;
            case ListBox: c.BackColor = Ui.InputBg; c.ForeColor = Ui.Text; return;
            case Label: c.BackColor = Ui.Bg; c.ForeColor = Ui.Text; return;
            case FlowLayoutPanel: c.BackColor = Color.Transparent; return;
            case TableLayoutPanel: c.BackColor = Color.Transparent; return;
            case PictureBox: c.BackColor = Ui.Card; return;
            case Panel: c.BackColor = Ui.Card; return;   // bordered cards
        }
    }

    internal void ThemeWalk(Control ctrl)
    {
        foreach (Control child in ctrl.Controls)
        {
            ApplyRoleColor(child);
            if (child.Controls.Count > 0) ThemeWalk(child);
        }
    }

    internal void ApplyTheme()
    {
        BackColor = Ui.Bg;
        // Nav rail is coloured explicitly (ThemeWalk only covers the content host).
        foreach (var c in new Control[] { nav, navHeader, navFooter, navList, footGrid, statusRow, navTitle, navSubtitle, navStatusDot, navStatusText })
        {
            if (c != null) c.BackColor = Ui.Nav;
        }
        navTitle.ForeColor = Color.White;
        navSubtitle.ForeColor = Ui.NavText;
        navStatusText.ForeColor = Color.White;
        Ui.StyleButton(toggleButton, "primary");
        Ui.StyleButton(panicButton, "ghost");
        ThemeWalk(content);
        RefreshNavButtons();
        // ThemeWalk restyles every button to "secondary"; re-light the active Settings
        // category button so the selection survives a theme pass.
        if (!string.IsNullOrEmpty(setActiveCat))
        {
            foreach (var b in setCatButtons)
                if ((string)b.Tag == setActiveCat) Ui.StyleButton(b, "primary");
        }
        content.Invalidate(true);
    }
}
