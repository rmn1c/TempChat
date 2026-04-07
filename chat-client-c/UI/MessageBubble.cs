using System.Drawing.Drawing2D;

namespace TempChat.UI;

/// <summary>
/// Full-width row control that draws a chat bubble at the left or right.
/// Being full-width eliminates the "shadow rectangle" artifact from exposed
/// control corners bleeding through against the scroll-panel background.
/// </summary>
internal sealed class MessageBubble : Control
{
    private readonly bool   _isOwn;
    private readonly string _sender;
    private readonly string _content;
    private readonly string _time;

    private const int HorizPad  = 14;
    private const int VertPad   = 8;
    private const int RowMargin = 6;   // gap between rows
    private const int Radius    = 14;  // bubble corner radius

    public MessageBubble(string sender, string content, string time,
                         bool isOwn, int panelClientWidth)
    {
        _sender  = sender;
        _content = content;
        _time    = time;
        _isOwn   = isOwn;

        SetStyle(ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.ResizeRedraw, true);

        BackColor = Theme.ChatBg;
        Left      = 0;
        Relayout(panelClientWidth);
    }

    /// <summary>Called on window resize to reposition/resize without recreating.</summary>
    public void Relayout(int panelClientWidth)
    {
        int available = Math.Max(panelClientWidth, 150);
        Width = available;

        int bubbleW  = (int)(available * 0.65);
        int textW    = bubbleW - HorizPad * 2;
        bool showSndr = !_isOwn;
        int senderH  = showSndr ? 18 : 0;

        var textSize = TextRenderer.MeasureText(
            _content, Theme.ChatFont,
            new Size(textW, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

        int timeH  = 14;
        Height = VertPad + senderH + textSize.Height + 4 + timeH + VertPad + RowMargin;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.ChatBg);
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int available = Width;
        int bubbleW   = (int)(available * 0.65);
        int margin    = (int)(available * 0.04);
        int bx        = _isOwn ? available - bubbleW - margin : margin;

        var bubbleRect = new Rectangle(bx, 0, bubbleW, Height - RowMargin);

        // Draw bubble — solid fill, no gradient
        using var path = RoundedPath(bubbleRect, Radius);
        Color bubbleColor = _isOwn ? Theme.OwnBubble : Theme.OtherBubble;
        using var fill = new SolidBrush(bubbleColor);
        g.FillPath(fill, path);

        // Very subtle inner highlight (1px, low-alpha white) for depth
        using var highlight = new Pen(Color.FromArgb(18, 255, 255, 255), 1f);
        g.DrawPath(highlight, path);

        // Content Y cursor inside the bubble
        int tx = bx + HorizPad;
        int ty = VertPad;

        // Sender name — incoming messages only
        if (!_isOwn)
        {
            TextRenderer.DrawText(g, _sender, Theme.SenderFont,
                new Rectangle(tx, ty, bubbleW - HorizPad * 2, 18),
                Theme.Accent,
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            ty += 18;
        }

        // Message text
        int textW    = bubbleW - HorizPad * 2;
        int textMaxH = Height - ty - 14 - VertPad - RowMargin;
        TextRenderer.DrawText(g, _content, Theme.ChatFont,
            new Rectangle(tx, ty, textW, textMaxH),
            Theme.Text,
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

        // Timestamp — bottom-right of bubble, muted
        TextRenderer.DrawText(g, _time, Theme.TimeFont,
            new Rectangle(bx, Height - VertPad - 14 - RowMargin, bubbleW - HorizPad, 14),
            Theme.SubText,
            TextFormatFlags.Right | TextFormatFlags.NoPrefix);
    }

    public void AnimateIn()
    {
        int target = Top;
        Top += 16;
        int step = 0;
        var t = new System.Windows.Forms.Timer { Interval = 14 };
        t.Tick += (_, _) =>
        {
            step++;
            double progress = step / 9.0;
            Top = target + (int)(16 * (1.0 - progress * (2 - progress))); // ease-out quad
            if (step >= 9) { Top = target; t.Stop(); t.Dispose(); }
        };
        t.Start();
    }

    private static GraphicsPath RoundedPath(Rectangle r, int rad)
    {
        int d = rad * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X,         r.Y,          d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y,          d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d,   0, 90);
        p.AddArc(r.X,         r.Bottom - d, d, d,  90, 90);
        p.CloseFigure();
        return p;
    }
}
