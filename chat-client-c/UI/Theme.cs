namespace TempChat.UI;

internal static class Theme
{
    // ── Color palette — Telegram-inspired dark blue-gray + purple ────
    public static readonly Color Background  = Color.FromArgb(23,  33,  43);  // #17212b
    public static readonly Color ChatBg      = Color.FromArgb(31,  42,  54);  // #1f2a36
    public static readonly Color Surface     = Color.FromArgb(31,  42,  54);  // #1f2a36
    public static readonly Color Secondary   = Color.FromArgb(36,  47,  61);  // #242f3d
    public static readonly Color OwnBubble   = Color.FromArgb(65,  48, 120);  // muted purple
    public static readonly Color OwnBubble2  = Color.FromArgb(65,  48, 120);  // compat
    public static readonly Color OtherBubble = Color.FromArgb(24,  37,  51);  // #182533
    public static readonly Color InputBg     = Color.FromArgb(36,  47,  61);  // #242f3d
    public static readonly Color HeaderBg    = Color.FromArgb(23,  33,  43);  // #17212b
    public static readonly Color Text        = Color.FromArgb(230, 235, 240); // #e6ebf0
    public static readonly Color SubText     = Color.FromArgb(154, 167, 178); // #9aa7b2
    public static readonly Color Accent      = Color.FromArgb(46,  166, 255); // #2ea6ff  blue
    public static readonly Color AccentHover = Color.FromArgb(82,  185, 255); // lighter blue
    public static readonly Color AccentDark  = Color.FromArgb(26,  130, 210); // pressed blue
    public static readonly Color Purple      = Color.FromArgb(168, 110, 240); // accent purple
    public static readonly Color Border      = Color.FromArgb(42,   57,  73); // subtle divider

    // ── Typography ───────────────────────────────────────────────────
    public static readonly Font ChatFont   = new("Segoe UI", 10.5f);
    public static readonly Font TimeFont   = new("Segoe UI",  8.0f);
    public static readonly Font SenderFont = new("Segoe UI",  8.5f, FontStyle.Bold);
    public static readonly Font UiFont     = new("Segoe UI", 10.0f);
    public static readonly Font TitleFont  = new("Segoe UI", 22.0f, FontStyle.Bold);
    public static readonly Font HeaderFont = new("Segoe UI", 11.0f, FontStyle.Bold);
    public static readonly Font SubFont    = new("Segoe UI",  9.0f);

    // ── Factory helpers ──────────────────────────────────────────────

    public static RoundButton MakeButton(string text, bool primary = true)
        => new RoundButton(text, primary) { Height = 36 };

    public static TextBox MakeTextBox(string placeholder = "", bool password = false)
    {
        var tb = new TextBox
        {
            BackColor             = InputBg,
            ForeColor             = Text,
            BorderStyle           = BorderStyle.None,
            Font                  = UiFont,
            UseSystemPasswordChar = password,
            Height                = 22
        };
        if (!string.IsNullOrEmpty(placeholder))
        {
            tb.Tag = placeholder;
            ApplyPlaceholder(tb);
            tb.GotFocus  += (_, _) => RemovePlaceholder(tb);
            tb.LostFocus += (_, _) => ApplyPlaceholder(tb);
        }
        return tb;
    }

    private static void ApplyPlaceholder(TextBox tb)
    {
        if (string.IsNullOrEmpty(tb.Text) || tb.Text == (string?)tb.Tag)
        {
            tb.Text      = (string?)tb.Tag ?? "";
            tb.ForeColor = SubText;
        }
    }

    private static void RemovePlaceholder(TextBox tb)
    {
        if (tb.Text == (string?)tb.Tag)
        {
            tb.Text      = "";
            tb.ForeColor = Text;
        }
    }

    public static Panel MakeInputWrapper(Control inner)
    {
        var panel = new Panel
        {
            BackColor = InputBg,
            Padding   = new Padding(10, 7, 10, 7),
            Height    = 38
        };
        inner.Dock = DockStyle.Fill;
        panel.Controls.Add(inner);
        return panel;
    }
}
