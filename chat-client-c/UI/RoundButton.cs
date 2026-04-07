using System.Drawing.Drawing2D;

namespace TempChat.UI;

/// <summary>Custom painted button with pill-shaped rounded corners.</summary>
internal sealed class RoundButton : Control
{
    private bool _hover;
    private bool _pressed;
    private readonly bool _primary;

    public RoundButton(string text, bool primary = true)
    {
        _primary  = primary;
        base.Text = text;
        Height    = 36;
        Cursor    = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.ResizeRedraw, true);

        MouseEnter += (_, _) => { _hover   = true;  Invalidate(); };
        MouseLeave += (_, _) => { _hover   = false; Invalidate(); };
        MouseDown  += (_, _) => { _pressed = true;  Invalidate(); };
        MouseUp    += (_, e) =>
        {
            _pressed = false;
            Invalidate();
            if (e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location))
                OnClick(EventArgs.Empty);
        };
    }

    public new string Text
    {
        get => base.Text;
        set { base.Text = value; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Clear corners with parent background
        g.Clear(Parent?.BackColor ?? Theme.Background);

        Color bg = _primary
            ? (_pressed ? Theme.AccentDark  : _hover ? Theme.AccentHover : Theme.Accent)
            : (_pressed ? Color.FromArgb(31, 42, 54)   // surface — pressed
                        : _hover ? Color.FromArgb(42, 55, 71)   // surface hover
                                 : Theme.Secondary);

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Pill(rect);
        using var fill = new SolidBrush(bg);
        g.FillPath(fill, path);

        if (!_primary)
        {
            using var pen = new Pen(Theme.Border, 1f);
            g.DrawPath(pen, path);
        }

        Color textColor = _primary ? Theme.Text : Theme.Text;
        TextRenderer.DrawText(g, Text, Theme.UiFont, ClientRectangle, textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    private static GraphicsPath Pill(Rectangle r)
    {
        int rad = r.Height;
        var p   = new GraphicsPath();
        p.AddArc(r.X,           r.Y,                rad, rad, 180, 90);
        p.AddArc(r.Right - rad, r.Y,                rad, rad, 270, 90);
        p.AddArc(r.Right - rad, r.Bottom - rad,     rad, rad,   0, 90);
        p.AddArc(r.X,           r.Bottom - rad,     rad, rad,  90, 90);
        p.CloseFigure();
        return p;
    }
}
