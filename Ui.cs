using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AeroVRC;

// ============================================================================
//  THEME  (single stylized "Aero" navy/azure palette) + STYLING HELPERS
//  One fixed theme: deep-navy backgrounds, azure-blue accents, animated
//  sparkles. Rounded gradient buttons with animated hover/press.
// ============================================================================

public static class Ui
{
    // ===== Palette tokens =====
    public static readonly Color Bg = Color.FromArgb(13, 16, 34);
    public static readonly Color BgTop = Color.FromArgb(21, 26, 52);
    public static readonly Color Surface = Color.FromArgb(20, 24, 50);
    public static readonly Color Card = Color.FromArgb(25, 30, 58);
    public static readonly Color CardTop = Color.FromArgb(34, 40, 76);
    public static readonly Color Accent = Color.FromArgb(74, 156, 255);
    public static readonly Color AccentHover = Color.FromArgb(130, 198, 255);
    public static readonly Color Accent2 = Color.FromArgb(132, 118, 255);
    public static readonly Color Glow = Color.FromArgb(74, 156, 255);
    public static readonly Color Text = Color.FromArgb(231, 235, 245);
    public static readonly Color TextMuted = Color.FromArgb(150, 162, 198);
    public static readonly Color Border = Color.FromArgb(50, 58, 104);
    public static readonly Color BorderHi = Color.FromArgb(92, 112, 182);
    public static readonly Color Success = Color.FromArgb(74, 202, 142);
    public static readonly Color Danger = Color.FromArgb(240, 102, 114);
    public static readonly Color Warning = Color.FromArgb(242, 178, 74);
    public static readonly Color Stopped = Color.FromArgb(120, 130, 162);
    public static readonly Color BtnBg = Color.FromArgb(42, 50, 92);
    public static readonly Color BtnHover = Color.FromArgb(58, 70, 122);
    public static readonly Color InputBg = Color.FromArgb(22, 26, 50);
    public static readonly Color LogBg = Color.FromArgb(12, 14, 30);
    public static readonly Color Nav = Color.FromArgb(11, 13, 28);
    public static readonly Color NavTop = Color.FromArgb(17, 20, 44);
    public static readonly Color NavText = Color.FromArgb(152, 162, 198);
    public static readonly Color NavActive = Color.FromArgb(34, 46, 96);
    public static readonly Color NavHover = Color.FromArgb(22, 29, 58);
    public static readonly Color Spark = Color.FromArgb(96, 168, 255);

    // ===== Fonts =====
    public static readonly Font FontBig = new("Segoe UI Semibold", 20);
    public static readonly Font FontTitle = new("Segoe UI Semibold", 15);
    public static readonly Font FontValue = new("Segoe UI Semibold", 13);
    public static readonly Font FontHeader = new("Segoe UI Semibold", 10.5f);
    public static readonly Font FontBody = new("Segoe UI", 10);
    public static readonly Font FontMuted = new("Segoe UI", 9);
    public static readonly Font FontSmall = new("Segoe UI", 8.5f);
    public static readonly Font FontMono = new("Consolas", 9.5f);
    public static readonly Font FontNav = new("Segoe UI", 11);
    public static readonly Font FontNavActive = new("Segoe UI Semibold", 11);

    // ===== Geometry / colour helpers =====
    public static GraphicsPath RoundedPath(float x, float y, float w, float h, float rad)
    {
        var p = new GraphicsPath();
        float d = rad * 2;
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
    public static Color Blend(Color a, Color b, double t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t),
        (int)(a.G + (b.G - a.G) * t),
        (int)(a.B + (b.B - a.B) * t));
    public static Color Shift(Color c, int d) => Color.FromArgb(
        Math.Max(0, Math.Min(255, c.R + d)),
        Math.Max(0, Math.Min(255, c.G + d)),
        Math.Max(0, Math.Min(255, c.B + d)));

    // First opaque ancestor colour - used to paint anti-aliased rounded corners over
    // whatever actually sits behind the control (transparent flow panels walk up).
    public static Color OpaqueBack(Control c)
    {
        var p = c.Parent;
        while (p != null)
        {
            if (p.BackColor.A == 255) return p.BackColor;
            p = p.Parent;
        }
        return Bg;
    }

    // Turns on a control's protected double-buffering via reflection (flicker-free
    // custom paint).
    public static void SetDoubleBuffered(Control ctrl)
    {
        try
        {
            var pi = ctrl.GetType().GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            pi?.SetValue(ctrl, true, null);
        }
        catch { }
    }

    // ===== Animated button styling =====
    // Per-button animation state, keyed by the button itself. .Tag stays free.
    public class BtnFx
    {
        public string Kind;
        public double Hover;
        public double Target;
        public bool Press;
    }
    public static readonly Dictionary<Button, BtnFx> ButtonFx = new();
    public static readonly List<Button> FxButtons = new();

    public static void StyleButton(Button btn, string kind = "secondary")
    {
        bool isNew = !ButtonFx.TryGetValue(btn, out var st);
        if (isNew) { st = new BtnFx { Kind = kind }; ButtonFx[btn] = st; }
        else st.Kind = kind;

        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.Cursor = Cursors.Hand;
        if (kind != "ghost") btn.Font = FontBody;
        switch (kind)
        {
            case "primary": btn.BackColor = Accent; btn.ForeColor = Color.White; break;
            case "danger": btn.BackColor = Danger; btn.ForeColor = Color.White; break;
            case "ghost": btn.BackColor = Nav; btn.ForeColor = Danger; break;
            default: btn.BackColor = BtnBg; btn.ForeColor = Text; break;
        }
        btn.FlatAppearance.MouseOverBackColor = btn.BackColor;
        btn.FlatAppearance.MouseDownBackColor = btn.BackColor;

        if (isNew)
        {
            FxButtons.Add(btn);
            btn.MouseEnter += (s, e) => { if (ButtonFx.TryGetValue((Button)s, out var f)) f.Target = 1.0; };
            btn.MouseLeave += (s, e) => { if (ButtonFx.TryGetValue((Button)s, out var f)) { f.Target = 0.0; f.Press = false; } ((Button)s).Invalidate(); };
            btn.MouseDown += (s, e) => { if (ButtonFx.TryGetValue((Button)s, out var f)) f.Press = true; ((Button)s).Invalidate(); };
            btn.MouseUp += (s, e) => { if (ButtonFx.TryGetValue((Button)s, out var f)) f.Press = false; ((Button)s).Invalidate(); };
            btn.EnabledChanged += (s, e) => ((Button)s).Invalidate();
            btn.Paint += PaintButton;
        }
        btn.Invalidate();
    }

    static void PaintButton(object sender, PaintEventArgs e)
    {
        var s = (Button)sender;
        if (!ButtonFx.TryGetValue(s, out var st)) return;
        var g = e.Graphics;
        int w = s.Width, h = s.Height;
        if (w < 4 || h < 4) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        // Kind colours (fixed palette).
        Color baseC, hov, fg; Color? bord;
        switch (st.Kind)
        {
            case "primary": baseC = Accent; hov = AccentHover; fg = Color.White; bord = null; break;
            case "danger": baseC = Danger; hov = Shift(Danger, 22); fg = Color.White; bord = null; break;
            case "ghost": baseC = Nav; hov = Shift(Nav, 16); fg = Danger; bord = Color.FromArgb(120, 70, 74); break;
            default: baseC = BtnBg; hov = BtnHover; fg = Text; bord = Border; break;
        }
        var c = Blend(baseC, hov, st.Hover);
        if (st.Press) c = Shift(c, -12);
        if (!s.Enabled) { c = baseC; fg = TextMuted; }
        // corners show whatever is behind the button
        g.Clear(OpaqueBack(s));
        var path = RoundedPath(0.5f, 0.5f, w - 1.5f, h - 1.5f, 7);
        var rectF = new RectangleF(0, 0, w, h);
        if (st.Kind == "primary" && s.Enabled)
        {
            // vivid azure -> violet diagonal that brightens on hover, with a glowing rim
            double hv = st.Hover;
            var c1 = Blend(Shift(Accent, 18), AccentHover, hv);
            var c2 = Blend(Accent2, Shift(Accent2, 32), hv);
            if (st.Press) { c1 = Shift(c1, -12); c2 = Shift(c2, -12); }
            using (var gb = new LinearGradientBrush(rectF, c1, c2, 28.0f)) g.FillPath(gb, path);
            using (var rim = new Pen(Color.FromArgb(160, Blend(AccentHover, Color.White, 0.4)), 1.4f)) g.DrawPath(rim, path);
        }
        else
        {
            // soft vertical gradient = the "slight 3D" body
            using (var gb = new LinearGradientBrush(rectF, Shift(c, 12), Shift(c, -10), 90.0f)) g.FillPath(gb, path);
            if (bord.HasValue) { using var pen = new Pen(bord.Value, 1); g.DrawPath(pen, path); }
        }
        // top highlight + bottom shade complete the depth
        using (var hl = new Pen(Color.FromArgb(42, 255, 255, 255), 1)) g.DrawLine(hl, 7, 1, w - 8, 1);
        using (var sh = new Pen(Color.FromArgb(58, 0, 0, 0), 1)) g.DrawLine(sh, 7, h - 2, w - 8, h - 2);
        path.Dispose();
        int ty = st.Press ? 1 : 0;
        var tr = new Rectangle(0, ty, w, h);
        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
        TextRenderer.DrawText(g, s.Text, s.Font, tr, fg, flags);
    }

    // Advances all hover animations one frame; called by the shared fx timer.
    public static void TickButtonFx()
    {
        List<Button> dead = null;
        foreach (var b in FxButtons)
        {
            if (b.IsDisposed) { (dead ??= new List<Button>()).Add(b); continue; }
            if (!ButtonFx.TryGetValue(b, out var st)) continue;
            double diff = st.Target - st.Hover;
            if (Math.Abs(diff) > 0.02)
            {
                st.Hover += diff * 0.28;
                b.Invalidate();
            }
            else if (st.Hover != st.Target)
            {
                st.Hover = st.Target;
                b.Invalidate();
            }
        }
        if (dead != null)
        {
            foreach (var b in dead)
            {
                FxButtons.Remove(b);
                ButtonFx.Remove(b);
            }
        }
    }

    // Dark-themes a ComboBox to match the custom checkboxes/steppers: flat, dark
    // field, and an owner-drawn dropdown list (accent-highlighted selection).
    // Idempotent - safe to call on every re-theme.
    static readonly HashSet<ComboBox> ComboStyled = new();
    public static void StyleCombo(ComboBox cmb)
    {
        cmb.FlatStyle = FlatStyle.Flat;
        cmb.BackColor = InputBg;
        cmb.ForeColor = Text;
        cmb.DrawMode = DrawMode.OwnerDrawFixed;
        if (ComboStyled.Add(cmb))
        {
            cmb.DrawItem += (sender, e) =>
            {
                var s = (ComboBox)sender;
                var g = e.Graphics;
                bool sel = (e.State & DrawItemState.Selected) != 0;
                var bg = sel ? Accent : InputBg;
                var fg = sel ? Color.White : Text;
                using (var bb = new SolidBrush(bg)) g.FillRectangle(bb, e.Bounds);
                if (e.Index >= 0 && e.Index < s.Items.Count)
                {
                    using var tb = new SolidBrush(fg);
                    g.DrawString(s.Items[e.Index]?.ToString() ?? "", s.Font, tb, e.Bounds.X + 2, e.Bounds.Y + 1);
                }
            };
        }
        cmb.Invalidate();
    }

    // A rounded surface panel used as a "card" - solid fill (labels sit flush on
    // it), rounded border, and highlight/shade edges for a soft raised look.
    public static Panel NewCard()
    {
        var p = new Panel();
        p.BackColor = Card;
        p.Paint += (sender, e) =>
        {
            var s = (Panel)sender;
            var g = e.Graphics;
            int w = s.Width, h = s.Height;
            if (w < 6 || h < 6) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(OpaqueBack(s));
            var path = RoundedPath(0.5f, 0.5f, w - 1.5f, h - 1.5f, 10);
            // Solid body (child labels sit flush on Card, so no gradient here); depth
            // comes from the backdrop showing through + a lit top edge below.
            using (var fill = new SolidBrush(Card)) g.FillPath(fill, path);
            using (var pen = new Pen(Border, 1)) g.DrawPath(pen, path);
            // brighter, accent-tinted highlight along the very top edge = "lit from above"
            using (var hl = new Pen(Color.FromArgb(40, 255, 255, 255), 1)) g.DrawLine(hl, 10, 1, w - 11, 1);
            using (var hl2 = new Pen(Color.FromArgb(60, BorderHi), 1))
            {
                g.DrawLine(hl2, 3, 6, 3, h - 7);
                g.DrawLine(hl2, w - 4, 6, w - 4, h - 7);
            }
            using (var sh = new Pen(Color.FromArgb(55, 0, 0, 0), 1)) g.DrawLine(sh, 10, h - 2, w - 11, h - 2);
            path.Dispose();
        };
        p.Resize += (s, e) => ((Panel)s).Invalidate();
        return p;
    }

    // ===== Native bits =====
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    // Dark, on-brand title bar (Windows 11 DWM): immersive dark mode + caption/border
    // colours matching the nav rail. Falls back silently on older Windows.
    public static void SetDarkTitleBar(Form f)
    {
        try
        {
            var h = f.Handle;
            int on = 1;
            DwmSetWindowAttribute(h, 20, ref on, 4);       // DWMWA_USE_IMMERSIVE_DARK_MODE
            int cref = (Nav.B << 16) | (Nav.G << 8) | Nav.R;   // COLORREF = 0x00BBGGRR
            DwmSetWindowAttribute(h, 35, ref cref, 4);     // DWMWA_CAPTION_COLOR
            DwmSetWindowAttribute(h, 34, ref cref, 4);     // DWMWA_BORDER_COLOR
        }
        catch { }
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern int SystemParametersInfo(int action, int uparam, string vparam, int winini);

    public static bool SetWallpaper(string path)
    {
        try { SystemParametersInfo(20, 0, path, 3); return true; }
        catch { return false; }
    }
}
