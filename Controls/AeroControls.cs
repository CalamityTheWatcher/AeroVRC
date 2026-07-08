using System.Drawing.Drawing2D;

namespace AeroVRC;

// ============================================================================
//  CUSTOM ON-BRAND CONTROLS (verbatim port of the embedded C# from the script)
//  An azure checkbox and a dark chevron stepper - the default WinForms
//  CheckBox/NumericUpDown can't be themed (white boxes, light spinner buttons).
//  Both keep the standard API (.Checked / .Value etc.).
// ============================================================================

public class AeroCheckBox : CheckBox
{
    public static Color BoxOff = Color.FromArgb(26, 30, 54);
    public static Color BoxBorder = Color.FromArgb(72, 82, 124);
    public static Color BoxOn = Color.FromArgb(64, 156, 255);
    public static Color BoxOnHi = Color.FromArgb(120, 194, 255);
    public static Color HoverBorder = Color.FromArgb(110, 130, 180);
    private bool _hover = false;

    public AeroCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
    }
    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    public override Size GetPreferredSize(Size proposedSize)
    {
        Size ts = TextRenderer.MeasureText(Text, Font);
        return new Size(18 + 10 + ts.Width + 2, Math.Max(22, ts.Height + 8));
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        int sz = 18;
        int by = (Height - sz) / 2;
        RectangleF box = new RectangleF(1f, by + 0.5f, sz, sz);
        using (GraphicsPath p = Rounded(box, 4f))
        {
            if (Checked)
            {
                using (LinearGradientBrush b = new LinearGradientBrush(new Rectangle(1, by, sz + 1, sz + 2), BoxOnHi, BoxOn, 90f))
                    g.FillPath(b, p);
                using (Pen hp = new Pen(Color.FromArgb(80, 255, 255, 255), 1f)) g.DrawPath(hp, p);
                using (Pen pen = new Pen(Color.White, 2.1f))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                    g.DrawLines(pen, new PointF[] { new PointF(5.5f, by + 9.5f), new PointF(8.5f, by + 12.5f), new PointF(14f, by + 6f) });
                }
            }
            else
            {
                using (SolidBrush b = new SolidBrush(BoxOff)) g.FillPath(b, p);
                using (Pen pen = new Pen(_hover ? HoverBorder : BoxBorder, 1.4f)) g.DrawPath(pen, p);
            }
        }
        Rectangle tr = new Rectangle(sz + 10, 0, Width - sz - 10, Height);
        TextRenderer.DrawText(g, Text, Font, tr, ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordEllipsis);
    }
    private static GraphicsPath Rounded(RectangleF r, float rad)
    {
        GraphicsPath p = new GraphicsPath(); float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }
}

public class AeroStepper : UserControl
{
    public static Color FieldBg = Color.FromArgb(26, 30, 54);
    public static Color FieldBorder = Color.FromArgb(72, 82, 124);
    public static Color Chev = Color.FromArgb(150, 160, 194);
    public static Color Accent = Color.FromArgb(64, 156, 255);
    private TextBox _tb;
    private decimal _min = 0m, _max = 100m, _val = 0m, _inc = 1m;
    private int _dec = 0;
    private int _hoverBtn = 0;
    private System.Windows.Forms.Timer _repeat;
    private int _repeatDir = 0;
    public event EventHandler ValueChanged;

    public AeroStepper()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Width = 74; Height = 27;
        _tb = new TextBox();
        _tb.BorderStyle = BorderStyle.None;
        _tb.BackColor = FieldBg; _tb.ForeColor = Color.FromArgb(228, 231, 240);
        _tb.TextAlign = HorizontalAlignment.Left;
        _tb.Text = Fmt(_val);
        _tb.KeyPress += Tb_KeyPress;
        _tb.KeyDown += Tb_KeyDown;
        _tb.LostFocus += delegate { Commit(); };
        Controls.Add(_tb);
        _repeat = new System.Windows.Forms.Timer(); _repeat.Interval = 100;
        _repeat.Tick += delegate { Step(_repeatDir); };
        LayoutField();
    }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public decimal Minimum { get { return _min; } set { _min = value; if (_val < _min) Value = _min; } }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public decimal Maximum { get { return _max; } set { _max = value; if (_val > _max) Value = _max; } }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public decimal Increment { get { return _inc; } set { _inc = value; } }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int DecimalPlaces { get { return _dec; } set { _dec = value; _tb.Text = Fmt(_val); } }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public decimal Value
    {
        get { return _val; }
        set
        {
            decimal v = value; if (v < _min) v = _min; if (v > _max) v = _max;
            bool changed = (v != _val);
            _val = v; _tb.Text = Fmt(_val);
            if (changed && ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }
    }
    private string Fmt(decimal v) { return _dec > 0 ? v.ToString("F" + _dec) : ((long)v).ToString(); }
    private void LayoutField() { if (_tb == null) return; _tb.Font = this.Font; _tb.Location = new Point(9, (Height - _tb.Height) / 2); _tb.Width = Width - 9 - 20; }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); LayoutField(); }
    protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutField(); Invalidate(); }
    private void Step(int dir) { Value = _val + (dir > 0 ? _inc : -_inc); }
    private void Commit() { decimal v; if (decimal.TryParse(_tb.Text, out v)) Value = v; else _tb.Text = Fmt(_val); }
    private void Tb_KeyPress(object s, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != '-' && !(e.KeyChar == '.' && _dec > 0)) e.Handled = true;
    }
    private void Tb_KeyDown(object s, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter) { Commit(); e.Handled = true; }
        else if (e.KeyCode == Keys.Up) { Step(1); e.Handled = true; }
        else if (e.KeyCode == Keys.Down) { Step(-1); e.Handled = true; }
    }
    private int BtnAt(Point p) { if (p.X < Width - 20) return 0; return p.Y < Height / 2 ? 1 : 2; }
    protected override void OnMouseMove(MouseEventArgs e) { int h = BtnAt(e.Location); if (h != _hoverBtn) { _hoverBtn = h; Invalidate(); } base.OnMouseMove(e); }
    protected override void OnMouseLeave(EventArgs e) { if (_hoverBtn != 0) { _hoverBtn = 0; Invalidate(); } base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        int b = BtnAt(e.Location);
        if (b != 0) { Step(b == 1 ? 1 : -1); _repeatDir = (b == 1 ? 1 : -1); _repeat.Start(); } else { _tb.Focus(); }
        base.OnMouseDown(e);
    }
    protected override void OnMouseUp(MouseEventArgs e) { _repeat.Stop(); base.OnMouseUp(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (GraphicsPath p = Rounded(r, 6f))
        {
            using (SolidBrush b = new SolidBrush(FieldBg)) g.FillPath(b, p);
            using (Pen pen = new Pen(FieldBorder, 1f)) g.DrawPath(pen, p);
        }
        int bx = Width - 20;
        using (Pen pen = new Pen(FieldBorder, 1f)) g.DrawLine(pen, bx, 4, bx, Height - 4);
        DrawChev(g, bx, 0, Height / 2, true, _hoverBtn == 1);
        DrawChev(g, bx, Height / 2, Height / 2, false, _hoverBtn == 2);
    }
    private void DrawChev(Graphics g, int x, int y, int h, bool up, bool hover)
    {
        int w = 20;
        if (hover) { using (SolidBrush b = new SolidBrush(Color.FromArgb(45, Accent))) g.FillRectangle(b, x + 1, y + 1, w - 2, h - 2); }
        Color c = hover ? Accent : Chev;
        float cx = x + w / 2f; float cy = y + h / 2f;
        using (Pen pen = new Pen(c, 1.6f))
        {
            pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
            if (up) g.DrawLines(pen, new PointF[] { new PointF(cx - 3.5f, cy + 2f), new PointF(cx, cy - 2f), new PointF(cx + 3.5f, cy + 2f) });
            else g.DrawLines(pen, new PointF[] { new PointF(cx - 3.5f, cy - 2f), new PointF(cx, cy + 2f), new PointF(cx + 3.5f, cy - 2f) });
        }
    }
    private static GraphicsPath Rounded(RectangleF r, float rad)
    {
        GraphicsPath p = new GraphicsPath(); float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }
}

// Dark colour table for the Photos right-click menu.
public class AeroMenuColors : ProfessionalColorTable
{
    static Color Bg = Color.FromArgb(30, 34, 62);
    static Color Hov = Color.FromArgb(44, 54, 92);
    static Color Bord = Color.FromArgb(52, 60, 100);
    public override Color ToolStripDropDownBackground => Bg;
    public override Color MenuBorder => Bord;
    public override Color MenuItemBorder => Hov;
    public override Color MenuItemSelected => Hov;
    public override Color MenuItemSelectedGradientBegin => Hov;
    public override Color MenuItemSelectedGradientEnd => Hov;
    public override Color ImageMarginGradientBegin => Bg;
    public override Color ImageMarginGradientMiddle => Bg;
    public override Color ImageMarginGradientEnd => Bg;
    public override Color SeparatorDark => Bord;
    public override Color SeparatorLight => Bord;
}
