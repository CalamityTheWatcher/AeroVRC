using System.Drawing.Drawing2D;

namespace AeroVRC;

// ============================================================================
//  TOAST NOTIFICATIONS (slide-in card, bottom-right; reused single form)
//  + WELCOME SCREEN (animated splash shown on startup)
// ============================================================================

public partial class MainForm
{
    // ===== Toasts =====
    Form toastForm;
    internal System.Windows.Forms.Timer toastTimer;
    class ToastState
    {
        public string Title = "";
        public string Msg = "";
        public Color? Accent;
        public double Elapsed;
        public double InT = 0.28, Hold = 4.6, OutT = 0.45;
    }
    readonly ToastState toastState = new();

    void DrawToast(Form tf, Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        int w = tf.Width, h = tf.Height;
        g.Clear(Ui.Card);
        using (var path = Ui.RoundedPath(0.5f, 0.5f, w - 1.5f, h - 1.5f, 12))
        using (var pen = new Pen(Ui.Border, 1))
            g.DrawPath(pen, path);
        var acc = toastState.Accent ?? Ui.Accent;
        // Type icon chip (glyph inferred from the accent colour).
        char gg = acc == Ui.Success ? '✓' : acc == Ui.Warning ? '⚠' : acc == Ui.Danger ? '✕' : 'ℹ';
        var chipR = new RectangleF(12, h / 2f - 15, 30, 30);
        using (var cp = Ui.RoundedPath(chipR.X, chipR.Y, chipR.Width, chipR.Height, 8))
        using (var cb = new SolidBrush(Color.FromArgb(48, acc)))
            g.FillPath(cb, cp);
        DrawGlyph(g, gg, chipR, acc, 16f);
        using (var tb = new SolidBrush(Ui.Text))
            g.DrawString(toastState.Title, Ui.FontHeader, tb, new RectangleF(52, 12, w - 60, 22));
        using var mb = new SolidBrush(Ui.TextMuted);
        g.DrawString(toastState.Msg, Ui.FontBody, mb, new RectangleF(52, 36, w - 60, h - 44));
    }

    // Shows (or replaces) the toast with a title + message. Non-blocking; fades
    // in, holds, fades out. Click to dismiss early. Safe no-op pre-UI.
    internal void ShowToast(string title, string msg, Color? accent = null)
    {
        accent ??= Ui.Accent;
        if (toastForm == null || toastForm.IsDisposed)
        {
            var tf = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false, TopMost = true,
                StartPosition = FormStartPosition.Manual,
                Size = new Size(334, 86), BackColor = Ui.Card, Opacity = 0,
            };
            if (Icon != null) tf.Icon = Icon;
            tf.Region = new Region(Ui.RoundedPath(0, 0, 334, 86, 12));
            Ui.SetDoubleBuffered(tf);
            tf.Paint += (s, e) => DrawToast((Form)s, e.Graphics);
            // click-to-dismiss: jump to the fade-out
            tf.Click += (s, e) =>
            {
                double t = toastState.InT + toastState.Hold;
                if (toastState.Elapsed < t) toastState.Elapsed = t;
            };
            toastForm = tf;
        }
        toastState.Title = title;
        toastState.Msg = msg;
        toastState.Accent = accent;
        toastState.Elapsed = 0.0;
        var wa = Screen.PrimaryScreen.WorkingArea;
        toastForm.Location = new Point(wa.Right - 334 - 16, wa.Bottom - 86 - 16);
        toastForm.Invalidate();
        if (!toastForm.Visible) toastForm.Show();
        toastForm.BringToFront();
        if (toastTimer == null)
        {
            toastTimer = new System.Windows.Forms.Timer { Interval = 30 };
            toastTimer.Tick += (s, e) =>
            {
                var st = toastState;
                var tf = toastForm;
                if (tf == null || tf.IsDisposed) { toastTimer.Stop(); return; }
                st.Elapsed += 0.03;
                double outStart = st.InT + st.Hold, outEnd = outStart + st.OutT;
                if (st.Elapsed < st.InT) tf.Opacity = Math.Min(1.0, st.Elapsed / st.InT);
                else if (st.Elapsed < outStart) tf.Opacity = 1.0;
                else if (st.Elapsed < outEnd) tf.Opacity = Math.Max(0.0, 1.0 - (st.Elapsed - outStart) / st.OutT);
                else { tf.Hide(); toastTimer.Stop(); }
            };
        }
        toastTimer.Start();
    }

    // ===== Welcome screen =====
    static double WEaseOut(double t)
    {
        if (t <= 0) return 0.0;
        if (t >= 1) return 1.0;
        return 1 - Math.Pow(1 - t, 3);
    }
    static double WEaseBack(double t)
    {
        if (t <= 0) return 0.0;
        if (t >= 1) return 1.0;
        const double c1 = 1.70158, c3 = c1 + 1;
        return 1 + c3 * Math.Pow(t - 1, 3) + c1 * Math.Pow(t - 1, 2);
    }

    static readonly Color azBlue = Color.FromArgb(64, 156, 255);
    static readonly Color azBright = Color.FromArgb(120, 194, 255);
    static readonly Color azDeep = Color.FromArgb(28, 92, 200);
    DateTime wStart = DateTime.Now;
    bool wClosing, wDontShow, wHoverBtn, wShown;
    class WParticle { public double X, Y, VX, VY; public int S, A; }
    List<WParticle> wParticles = new();
    readonly Rectangle wBtnRect = new(225, 402, 210, 48);
    readonly Rectangle wChkRect = new(200, 458, 260, 24);
    static readonly Font wFontTitle = new("Segoe UI Semibold", 30);
    static readonly Font wFontTag = new("Segoe UI", 12);
    static readonly Font wFontBullet = new("Segoe UI", 11);
    static readonly Font wFontBtn = new("Segoe UI Semibold", 12);
    static readonly Font wFontChk = new("Segoe UI", 9);
    static readonly string[] wBullets =
    {
        "Keeps VRChat running - auto-relaunches after any crash",
        "One-click launch for trackers, overlays & companion apps",
        "Tracks your playtime, worlds, photos, and friends",
        "Optimizes performance and handles the busywork for you",
    };

    void DrawWelcome(Panel panel, Graphics g)
    {
        int W = panel.Width, H = panel.Height;
        double cx = W / 2.0;
        double el = (DateTime.Now - wStart).TotalSeconds;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // background gradient
        var rf = new Rectangle(0, 0, W, H);
        using (var bg = new LinearGradientBrush(rf, Color.FromArgb(15, 17, 32), Color.FromArgb(26, 30, 60), 65.0f))
            g.FillRectangle(bg, rf);

        // drifting particles
        foreach (var p in wParticles)
        {
            using var pb = new SolidBrush(Color.FromArgb(p.A, azBlue));
            g.FillEllipse(pb, (float)p.X, (float)p.Y, p.S, p.S);
        }

        // entrance "pop": scale whole content about the upper-middle
        double sp = Math.Max(0, Math.Min(1, el / 0.4));
        double sc = 0.9 + 0.1 * WEaseBack(sp);
        var st = g.Save();
        g.TranslateTransform((float)cx, 210.0f);
        g.ScaleTransform((float)sc, (float)sc);
        g.TranslateTransform((float)-cx, -210.0f);

        using var ctrFmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        // ---- logo (glow + orb, pops in then gently pulses) ----
        const double logoY = 98.0;
        double la = WEaseOut(Math.Max(0, Math.Min(1, (el - 0.05) / 0.55)));
        double pulse = 0.5 + 0.5 * Math.Sin(el * 2.3);
        // halo
        for (int i = 5; i >= 1; i--)
        {
            double rad = (26.0 + i * 9.0) * (0.6 + 0.4 * la) + pulse * 4.0;
            int al = (int)((22 - i * 3) * la);
            if (al > 0)
            {
                using var gb = new SolidBrush(Color.FromArgb(al, azBlue));
                g.FillEllipse(gb, (float)(cx - rad), (float)(logoY - rad), (float)(rad * 2), (float)(rad * 2));
            }
        }
        // AeroVRC "A" orb - a glowing blue sphere with a soft inner highlight.
        double lsz = (74.0 + pulse * 3.0) * (0.35 + 0.65 * WEaseBack(Math.Max(0, Math.Min(1, (el - 0.05) / 0.6))));
        var orbRect = new RectangleF((float)(cx - lsz / 2), (float)(logoY - lsz / 2), (float)lsz, (float)lsz);
        using (var orb = new GraphicsPath())
        {
            orb.AddEllipse(orbRect);
            using (var pgb = new PathGradientBrush(orb))
            {
                // highlight offset up-left for a 3D sphere feel
                pgb.CenterPoint = new PointF((float)(cx - lsz * 0.16), (float)(logoY - lsz * 0.18));
                pgb.CenterColor = Color.FromArgb((int)(255 * la), azBright);
                pgb.SurroundColors = new[] { Color.FromArgb((int)(255 * la), azDeep) };
                g.FillPath(pgb, orb);
            }
            using var rimPen = new Pen(Color.FromArgb((int)(150 * la), azBright), 1.5f);
            g.DrawPath(rimPen, orb);
        }
        // specular dot
        using (var spec = new SolidBrush(Color.FromArgb((int)(150 * la), Color.White)))
            g.FillEllipse(spec, (float)(cx - lsz * 0.26), (float)(logoY - lsz * 0.28), (float)(lsz * 0.16), (float)(lsz * 0.12));
        // letter A
        using (var ab = new SolidBrush(Color.FromArgb((int)(255 * la), Color.White)))
        using (var af = new Font("Segoe UI Semibold", (float)(lsz * 0.5)))
            g.DrawString("A", af, ab, new RectangleF((float)(cx - lsz / 2), (float)(logoY - lsz / 2 - 2), (float)lsz, (float)lsz), ctrFmt);

        // fading, rising title + tagline
        var texts = new (string T, Font F, double Y, Color C, double S, double D)[]
        {
            ("Welcome to AeroVRC", wFontTitle, 178.0, Color.White, 0.45, 0.55),
            ("Your all-in-one VRChat companion", wFontTag, 234.0, azBright, 0.8, 0.5),
        };
        foreach (var tx in texts)
        {
            double tp = Math.Max(0, Math.Min(1, (el - tx.S) / tx.D));
            if (tp <= 0) continue;
            double te = WEaseOut(tp);
            int ta = (int)(255 * te);
            double tyoff = (1 - te) * 16;
            using var tbr = new SolidBrush(Color.FromArgb(ta, tx.C));
            var trect = new RectangleF(0, (float)(tx.Y + tyoff), W, 44);
            g.DrawString(tx.T, tx.F, tbr, trect, ctrFmt);
        }

        // ---- feature bullets, staggered (centred as a block) ----
        const double by = 278.0;
        double maxBulletW = 0.0;
        foreach (var bt in wBullets)
        {
            double bw = g.MeasureString(bt, wFontBullet).Width;
            if (bw > maxBulletW) maxBulletW = bw;
        }
        double bulletDotX = (W - (18.0 + maxBulletW)) / 2.0;
        double bulletTextX = bulletDotX + 18.0;
        for (int i = 0; i < wBullets.Length; i++)
        {
            double bs = 1.05 + i * 0.2;
            double p = Math.Max(0, Math.Min(1, (el - bs) / 0.5));
            if (p <= 0) continue;
            double e2 = WEaseOut(p);
            int a = (int)(255 * e2);
            double xoff = (1 - e2) * 18;
            double ry = by + i * 30;
            using (var dot = new SolidBrush(Color.FromArgb(a, azBlue)))
                g.FillEllipse(dot, (float)(bulletDotX + xoff), (float)(ry + 6), 7, 7);
            using var tb = new SolidBrush(Color.FromArgb(a, Color.FromArgb(214, 220, 236)));
            g.DrawString(wBullets[i], wFontBullet, tb, (float)(bulletTextX + xoff), (float)ry);
        }

        // ---- Get Started button ----
        double btnP = WEaseOut(Math.Max(0, Math.Min(1, (el - 2.0) / 0.5)));
        if (btnP > 0)
        {
            var r = wBtnRect;
            using var bp = Ui.RoundedPath(r.X + 0.5f, r.Y + 0.5f, r.Width - 1.5f, r.Height - 1.5f, 10);
            var c1 = wHoverBtn ? azBright : azBlue;
            var c2 = wHoverBtn ? azBlue : azDeep;
            var rr = new Rectangle(r.X, r.Y, r.Width, r.Height);
            double af2 = btnP;   // fade the button in via colour alpha
            using (var gb2 = new LinearGradientBrush(rr, Color.FromArgb((int)(255 * af2), c1), Color.FromArgb((int)(255 * af2), c2), 90.0f))
                g.FillPath(gb2, bp);
            using var tb = new SolidBrush(Color.FromArgb((int)(255 * af2), Color.White));
            var bf = new RectangleF(r.X, r.Y, r.Width, r.Height);
            g.DrawString("Get Started   →", wFontBtn, tb, bf, ctrFmt);
        }

        // ---- "don't show again" toggle (box + label centred as a group) ----
        double chkP = WEaseOut(Math.Max(0, Math.Min(1, (el - 2.25) / 0.5)));
        if (chkP > 0)
        {
            int a = (int)(200 * chkP);
            var cr = wChkRect;
            const string chkTxt = "Don't show this welcome again";
            double chkTw = g.MeasureString(chkTxt, wFontChk).Width;
            // Box left so the box(15) + gap + label group is centred about the panel.
            double chkX = (W - (21.5 + chkTw)) / 2.0;
            using (var bx = Ui.RoundedPath((float)chkX, cr.Y + 4.5f, 15, 15, 3))
            {
                using (var pen = new Pen(Color.FromArgb(a, 130, 150, 185), 1.4f))
                    g.DrawPath(pen, bx);
                if (wDontShow)
                {
                    using (var ck = new SolidBrush(Color.FromArgb(a, azBlue)))
                        g.FillPath(ck, bx);
                    using var cp = new Pen(Color.White, 1.8f);
                    g.DrawLines(cp, new[]
                    {
                        new PointF((float)(chkX + 2.5), cr.Y + 12),
                        new PointF((float)(chkX + 6.5), cr.Y + 16),
                        new PointF((float)(chkX + 12.5), cr.Y + 8),
                    });
                }
            }
            using var tb = new SolidBrush(Color.FromArgb(a, 150, 160, 185));
            g.DrawString(chkTxt, wFontChk, tb, (float)(chkX + 21.5), cr.Y + 4);
        }

        g.Restore(st);
    }

    void DismissWelcome()
    {
        if (wClosing) return;
        wClosing = true;
        if (wDontShow)
        {
            config.ShowWelcome = false;
            SaveConfig();
        }
    }

    internal void ShowWelcomeScreen()
    {
        if (wShown) return;   // Shown fires only once, but guard anyway
        wShown = true;
        wStart = DateTime.Now;
        wClosing = false;
        wDontShow = false;
        wHoverBtn = false;
        var rnd = new Random();
        wParticles = new List<WParticle>();
        for (int i = 0; i < 46; i++)
        {
            wParticles.Add(new WParticle
            {
                X = rnd.Next(0, 660), Y = rnd.Next(0, 480),
                VX = (rnd.NextDouble() - 0.5) * 0.35,
                VY = 0.2 + rnd.NextDouble() * 0.7,
                S = 1 + rnd.Next(0, 3), A = 18 + rnd.Next(0, 52),
            });
        }

        var wf = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.CenterScreen,
            Size = new Size(660, 480),
            BackColor = Color.FromArgb(15, 17, 32),
            ShowInTaskbar = false,
            TopMost = true,
            Opacity = 0,
            KeyPreview = true,
        };
        if (Icon != null) wf.Icon = Icon;
        wf.Region = new Region(Ui.RoundedPath(0, 0, 660, 480, 18));

        var wp = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(15, 17, 32) };
        Ui.SetDoubleBuffered(wp);
        wp.Paint += (s, e) => DrawWelcome((Panel)s, e.Graphics);
        wp.MouseMove += (s, e) =>
        {
            bool over = wBtnRect.Contains(e.Location);
            if (over != wHoverBtn) { wHoverBtn = over; wp.Invalidate(); }
            wp.Cursor = over || wChkRect.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
        };
        wp.MouseClick += (s, e) =>
        {
            double el = (DateTime.Now - wStart).TotalSeconds;
            if (wChkRect.Contains(e.Location) && el >= 2.2) { wDontShow = !wDontShow; wp.Invalidate(); return; }
            if (wBtnRect.Contains(e.Location) && el >= 1.9) DismissWelcome();
        };
        wf.Controls.Add(wp);
        wf.KeyDown += (s, e) =>
        {
            if (e.KeyCode is Keys.Enter or Keys.Escape or Keys.Space) DismissWelcome();
        };

        var wt = new System.Windows.Forms.Timer { Interval = 25 };
        wt.Tick += (s, e) =>
        {
            foreach (var p in wParticles)
            {
                p.Y -= p.VY;
                p.X += p.VX;
                if (p.Y < -6) { p.Y = 486; p.X = Random.Shared.Next(0, 660); }
            }
            if (wClosing)
            {
                double o = wf.Opacity - 0.10;
                if (o <= 0) { wt.Stop(); wf.Close(); return; }
                wf.Opacity = o;
            }
            else
            {
                double el = (DateTime.Now - wStart).TotalSeconds;
                wf.Opacity = Math.Min(1.0, el / 0.32);
            }
            wp.Invalidate();
        };
        wt.Start();
        wf.ShowDialog();
        wt.Stop();
        wt.Dispose();
        wf.Dispose();
    }

    // ========================================================================
    //  FX TIMER  (~30fps; drives hover fades, nav slider glide, status pulse)
    // ========================================================================
    bool fxSparkGate;
    void FxTick()
    {
        // Nothing is visible while minimized, so skip all animation - repainting an
        // off-screen window still makes DWM recomposite it and burns GPU.
        if (WindowState == FormWindowState.Minimized) return;

        // status dot pulse (only animates while monitoring)
        fxPhase += 0.035;
        if (fxPhase >= 1.0) fxPhase = 0.0;
        if (monitoring) navStatusDot.Invalidate();

        // animated monitoring accent line
        if (monitoring && monitorBar != null && monitorBar.Visible) monitorBar.Invalidate();

        // animated logo orb - only while focused + enabled (idle when blurred/off)
        if (config.Effects.LogoAnim && ContainsFocus)
        {
            logoPhase += 0.06;
            if (logoPhase > 6.283) logoPhase -= 6.283;
            navLogo.Invalidate();
        }

        // photo-tile hover eases (only the tiles currently entering/leaving)
        if (photoActive.Count > 0)
        {
            var done = new List<Panel>();
            foreach (var t in photoActive)
            {
                if (t.IsDisposed) { done.Add(t); continue; }
                var d0 = (PhotoTileData)t.Tag;
                double d = d0.HoverT - d0.Hover;
                if (Math.Abs(d) <= 0.03) { d0.Hover = d0.HoverT; done.Add(t); t.Invalidate(); }
                else { d0.Hover += d * 0.25; t.Invalidate(); }
            }
            foreach (var t in done) photoActive.Remove(t);
        }

        // Animated page sparkles - advance the drifting dots, repaint the visible
        // page only. Only animate while the window is focused; the full-page
        // repaint is throttled to every other tick (~15 FPS).
        if (config.Effects.Sparkles && ContainsFocus)
        {
            if (pages.TryGetValue(currentPage, out var pg) && pg.Visible && pg.Height > 0)
            {
                int pw = Math.Max(200, pg.Width), ph = Math.Max(200, pg.Height);
                bool down = sparkStyle != null && sparkStyle.Down;
                foreach (var p in pageParticles)
                {
                    if (down) p.Y += p.VY; else p.Y -= p.VY;
                    p.X += p.VX;
                    if (down) { if (p.Y > ph + 4) { p.Y = -4; p.X = sparkRnd.Next(0, pw); } }
                    else { if (p.Y < -4) { p.Y = ph + 4; p.X = sparkRnd.Next(0, pw); } }
                    if (p.X < -6) p.X = pw + 2;
                    else if (p.X > pw + 6) p.X = -2;
                }
                fxSparkGate = !fxSparkGate;
                if (fxSparkGate) pg.Invalidate();
            }
        }

        // nav slider glide - damped spring (slight overshoot) + stretch while moving
        if (navSlider.Visible)
        {
            double diff = navSliderTarget - navSliderPos;
            navSliderVel = (navSliderVel + diff * 0.30) * 0.70;
            navSliderPos += (float)navSliderVel;
            if (Math.Abs(diff) < 0.4 && Math.Abs(navSliderVel) < 0.4) { navSliderPos = navSliderTarget; navSliderVel = 0; }
            int stretch = (int)Math.Min(18, Math.Abs(navSliderVel) * 1.7);
            int h = 26 + stretch, top = (int)Math.Round(navSliderPos - stretch / 2.0);
            if (navSlider.Height != h) navSlider.Height = h;
            if (navSlider.Top != top) navSlider.Top = top;
        }

        // page crossfade: dissolve the captured old page away to reveal the new one
        if (xfadeOverlay != null && xfadeOverlay.Visible)
        {
            xfadeAlpha -= 0.16;
            if (xfadeAlpha <= 0)
            {
                xfadeAlpha = 0;
                xfadeOverlay.Visible = false; xfadeOverlay.SendToBack();
                xfadeBmpOld?.Dispose(); xfadeBmpNew?.Dispose(); xfadeBmpOld = xfadeBmpNew = null;
            }
            else xfadeOverlay.Invalidate();
        }

        // window launch fade-in
        if (Opacity < 1.0) Opacity = Math.Min(1.0, Opacity + 0.12);

        // button hover fades
        Ui.TickButtonFx();
    }
}
