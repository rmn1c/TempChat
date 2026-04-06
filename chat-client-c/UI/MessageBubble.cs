using System.Drawing.Drawing2D;

namespace TempChat.UI;

internal sealed class MessageBubble : Control
{
    private readonly bool   _isOwn;
    private readonly string _sender;
    private readonly string _content;
    private readonly string _time;

    private const int HorizPad = 14;
    private const int VertPad  = 10;
    private const int Radius   = 18;

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

        Layout(panelClientWidth);
    }

    public void Layout(int panelClientWidth)
    {
        int availW  = Math.Max(panelClientWidth - 20, 120); // subtract scrollbar
        int bubbleW = (int)(availW * 0.66);
        int textW   = bubbleW - HorizPad * 2;

        bool showSender = !_isOwn;
        int senderH     = showSender ? 20 : 0;

        var textSize = TextRenderer.MeasureText(
            _content, Theme.ChatFont,
            new Size(textW, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

        var timeSize = TextRenderer.MeasureText(
            _time, Theme.TimeFont,
            new Size(textW, int.MaxValue),
            TextFormatFlags.NoPrefix);

        Width  = bubbleW;
        Height = VertPad + senderH + textSize.Height + 4 + timeSize.Height + VertPad;

        int margin = (int)(availW * 0.04);
        Left = _isOwn
            ? availW - bubbleW - margin
            : margin;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.ChatBg);
        g.SmoothingMode       = SmoothingMode.AntiAlias;
        g.TextRenderingHint   = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        using var path = RoundedPath(rect, Radius);

        if (_isOwn)
        {
            using var grad = new LinearGradientBrush(
                rect, Theme.OwnBubble, Theme.OwnBubble2,
                LinearGradientMode.ForwardDiagonal);
            g.FillPath(grad, path);
        }
        else
        {
            using var fill = new SolidBrush(Theme.OtherBubble);
            g.FillPath(fill, path);
        }

        // Subtle inner glow border
        using var borderPen = new Pen(Color.FromArgb(55, 255, 255, 255), 1f);
        g.DrawPath(borderPen, path);

        int x = HorizPad;
        int y = VertPad;

        // Sender name (other messages only)
        if (!_isOwn)
        {
            TextRenderer.DrawText(g, _sender, Theme.SenderFont,
                new Rectangle(x, y, Width - x * 2, 18),
                Theme.Accent,
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            y += 20;
        }

        // Message content
        TextRenderer.DrawText(g, _content, Theme.ChatFont,
            new Rectangle(x, y, Width - x * 2, Height - y - 26),
            Theme.Text,
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

        // Timestamp — bottom right
        TextRenderer.DrawText(g, _time, Theme.TimeFont,
            new Rectangle(0, Height - VertPad - 14, Width - HorizPad, 14),
            Theme.SubText,
            TextFormatFlags.Right | TextFormatFlags.NoPrefix);
    }

    public void AnimateIn()
    {
        int target = Top;
        Top += 18;
        int step = 0;
        var t = new System.Windows.Forms.Timer { Interval = 14 };
        t.Tick += (_, _) =>
        {
            step++;
            // Ease-out: big steps early, small steps late
            Top = target + (int)(15.0 * Math.Pow(1.0 - step / 8.0, 2));
            if (step >= 8) { Top = target; t.Stop(); t.Dispose(); }
        };
        t.Start();
    }

    private static GraphicsPath RoundedPath(Rectangle r, int rad)
    {
        var p = new GraphicsPath();
        p.AddArc(r.X,                 r.Y,                  rad * 2, rad * 2, 180, 90);
        p.AddArc(r.Right - rad * 2,   r.Y,                  rad * 2, rad * 2, 270, 90);
        p.AddArc(r.Right - rad * 2,   r.Bottom - rad * 2,   rad * 2, rad * 2,   0, 90);
        p.AddArc(r.X,                 r.Bottom - rad * 2,   rad * 2, rad * 2,  90, 90);
        p.CloseFigure();
        return p;
    }
}
