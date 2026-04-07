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
    private int  _textHeight;
    private bool _isSingleLine;

    private const int HorizPad    = 14;
    private const int VertPad     = 8;
    private const int RowMargin   = 6;
    private const int Radius      = 14;
    private const int InlineTimeW = 46;

    // GDI+ StringFormat used for both MeasureString and DrawString — the only
    // way to guarantee that measurement and rendering produce identical line
    // breaks.  TextRenderer.MeasureText adds a fudge factor to the proposed
    // width, making it believe the column is wider than DrawText actually uses,
    // so it underestimates the number of lines needed → bubble too short → clipped.
    private static readonly StringFormat ContentSF = new()
    {
        Trimming      = StringTrimming.None,
        Alignment     = StringAlignment.Near,
        LineAlignment = StringAlignment.Near
    };

    // Shared off-screen surface used for GDI+ text measurement
    private static readonly Bitmap   MeasureBmp = new(1, 1);
    private static readonly Graphics MeasureG   = Graphics.FromImage(MeasureBmp);

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

    public void Relayout(int panelClientWidth)
    {
        int available = Math.Max(panelClientWidth, 150);
        Width = available;

        _textW   = (int)(available * 0.60);
        _bubbleW = _textW + HorizPad * 2;

        int senderH = _isOwn ? 0 : 18;

        // Single-line height via GDI+ font metrics — matches DrawString line spacing
        _singleLineH = (int)Math.Ceiling(Theme.ChatFont.GetHeight(MeasureG));

        // Single-line check: use the reduced width so the timestamp fits on the same row
        SizeF szSingle = MeasureG.MeasureString(_content, Theme.ChatFont,
            _textW - InlineTimeW, ContentSF);
        _isSingleLine = szSingle.Height <= _singleLineH + 2;

        if (_isSingleLine)
        {
            _textHeight = _singleLineH;
        }
        else
        {
            // Full-width measurement — GDI+ MeasureString and DrawString are
            // guaranteed to use identical layout so the measured height always
            // exactly covers what DrawString will render
            SizeF szFull = MeasureG.MeasureString(_content, Theme.ChatFont,
                _textW, ContentSF);
            _textHeight = (int)Math.Ceiling(szFull.Height);
        }

        int contentH = _isSingleLine
            ? _singleLineH
            : _textHeight + 4 + 14;

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

        using var path = RoundedPath(bubbleRect, Radius);
        using var fill = new SolidBrush(_isOwn ? Theme.OwnBubble : Theme.OtherBubble);
        g.FillPath(fill, path);

        using var highlight = new Pen(Color.FromArgb(18, 255, 255, 255), 1f);
        g.DrawPath(highlight, path);

        int tx = bx + HorizPad;
        int ty = VertPad;

        // Sender name (incoming only) — single-line, TextRenderer is fine here
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
            // Single-line: text left portion + timestamp right-aligned on same row
            TextRenderer.DrawText(g, _content, Theme.ChatFont,
                new Rectangle(tx, ty, _textW - InlineTimeW, _singleLineH),
                Theme.Text,
                TextFormatFlags.NoPrefix);

            int timeY = ty + (_singleLineH - 14) / 2;
            TextRenderer.DrawText(g, _time, Theme.TimeFont,
                new Rectangle(bx + HorizPad, timeY, _textW, 14),
                Theme.SubText,
                TextFormatFlags.Right | TextFormatFlags.NoPrefix);
        }
        else
        {
            // Multi-line: use GDI+ DrawString — guaranteed consistent with MeasureString
            using var textBrush = new SolidBrush(Theme.Text);
            g.DrawString(_content, Theme.ChatFont, textBrush,
                new RectangleF(tx, ty, _textW, _textHeight), ContentSF);

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
