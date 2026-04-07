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

    // Cached layout values — computed in Relayout, consumed in OnPaint
    private int  _bubbleW;
    private int  _textW;
    private int  _singleLineH;
    private bool _isSingleLine;

    private const int HorizPad    = 14;
    private const int VertPad     = 8;
    private const int RowMargin   = 6;   // gap between rows
    private const int Radius      = 14;  // bubble corner radius
    private const int InlineTimeW = 46;  // px reserved for "HH:mm" on same line

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

        // Text wraps at 60% of available width; bubble adds horizontal padding
        _textW   = (int)(available * 0.60);
        _bubbleW = _textW + HorizPad * 2;

        int senderH = _isOwn ? 0 : 18;

        // Measure single-line height once using a tall reference string
        _singleLineH = TextRenderer.MeasureText("Tg", Theme.ChatFont,
            new Size(9999, 200), TextFormatFlags.NoPrefix).Height;

        // Measure actual text — if it fits on one line, display time inline
        var textSize = TextRenderer.MeasureText(
            _content, Theme.ChatFont,
            new Size(_textW, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

        _isSingleLine = textSize.Height <= _singleLineH + 2;

        int contentH = _isSingleLine
            ? _singleLineH                          // time sits on the same row
            : textSize.Height + 4 + 14;            // text + gap + time row below

        Height = VertPad + senderH + contentH + VertPad + RowMargin;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.ChatBg);
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int available = Width;
        int margin    = (int)(available * 0.04);
        int bx        = _isOwn ? available - _bubbleW - margin : margin;

        var bubbleRect = new Rectangle(bx, 0, _bubbleW, Height - RowMargin);

        // Bubble fill — solid, no gradient
        using var path = RoundedPath(bubbleRect, Radius);
        using var fill = new SolidBrush(_isOwn ? Theme.OwnBubble : Theme.OtherBubble);
        g.FillPath(fill, path);

        // Very subtle inner highlight for depth
        using var highlight = new Pen(Color.FromArgb(18, 255, 255, 255), 1f);
        g.DrawPath(highlight, path);

        int tx = bx + HorizPad;
        int ty = VertPad;

        // Sender name — incoming messages only
        if (!_isOwn)
        {
            TextRenderer.DrawText(g, _sender, Theme.SenderFont,
                new Rectangle(tx, ty, _textW, 18),
                Theme.Purple,
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            ty += 18;
        }

        if (_isSingleLine)
        {
            // Text takes left portion; timestamp sits right-aligned on same row
            TextRenderer.DrawText(g, _content, Theme.ChatFont,
                new Rectangle(tx, ty, _textW - InlineTimeW, _singleLineH),
                Theme.Text,
                TextFormatFlags.NoPrefix);

            // Time vertically centered within the single text line
            int timeY = ty + (_singleLineH - 14) / 2;
            TextRenderer.DrawText(g, _time, Theme.TimeFont,
                new Rectangle(bx + HorizPad, timeY, _textW, 14),
                Theme.SubText,
                TextFormatFlags.Right | TextFormatFlags.NoPrefix);
        }
        else
        {
            // Multi-line: text full width, timestamp below on its own row
            int textMaxH = Height - ty - 14 - VertPad - RowMargin;
            TextRenderer.DrawText(g, _content, Theme.ChatFont,
                new Rectangle(tx, ty, _textW, textMaxH),
                Theme.Text,
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(g, _time, Theme.TimeFont,
                new Rectangle(bx, Height - VertPad - 14 - RowMargin, _bubbleW - HorizPad, 14),
                Theme.SubText,
                TextFormatFlags.Right | TextFormatFlags.NoPrefix);
        }
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
            Top = target + (int)(16 * (1.0 - progress * (2 - progress)));
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
