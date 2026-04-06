namespace TempChat.UI;

internal static class Theme
{
    public static readonly Color Background  = Color.FromArgb(15,  15,  30);
    public static readonly Color ChatBg      = Color.FromArgb(17,  17,  34);
    public static readonly Color Surface     = Color.FromArgb(24,  26,  50);
    public static readonly Color OwnBubble   = Color.FromArgb(76,  39, 130);
    public static readonly Color OwnBubble2  = Color.FromArgb(108, 60, 168);
    public static readonly Color OtherBubble = Color.FromArgb(36,  40,  72);
    public static readonly Color InputBg     = Color.FromArgb(26,  28,  54);
    public static readonly Color HeaderBg    = Color.FromArgb(20,  22,  44);
    public static readonly Color Text        = Color.FromArgb(232, 232, 245);
    public static readonly Color SubText     = Color.FromArgb(145, 145, 185);
    public static readonly Color Accent      = Color.FromArgb(148,  82, 218);
    public static readonly Color AccentHover = Color.FromArgb(170, 105, 240);
    public static readonly Color AccentDark  = Color.FromArgb(100,  50, 160);
    public static readonly Color Border      = Color.FromArgb(48,  50,  88);

    public static readonly Font ChatFont   = new("Segoe UI",  10.5f);
    public static readonly Font TimeFont   = new("Segoe UI",   8.0f);
    public static readonly Font SenderFont = new("Segoe UI",   8.5f, FontStyle.Bold);
    public static readonly Font UiFont     = new("Segoe UI",  10.0f);
    public static readonly Font TitleFont  = new("Segoe UI",  22.0f, FontStyle.Bold);
    public static readonly Font HeaderFont = new("Segoe UI",  11.0f, FontStyle.Bold);
    public static readonly Font SubFont    = new("Segoe UI",   9.0f);

    public static Button MakeButton(string text, bool primary = true)
    {
        var b = new Button
        {
            Text      = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Accent : Surface,
            ForeColor = Text,
            Font      = UiFont,
            Cursor    = Cursors.Hand,
            Height    = 36
        };
        b.FlatAppearance.BorderColor    = primary ? AccentHover : Border;
        b.FlatAppearance.BorderSize     = 1;
        b.FlatAppearance.MouseOverBackColor  = primary ? AccentHover : Color.FromArgb(40, 44, 75);
        b.FlatAppearance.MouseDownBackColor  = primary ? AccentDark  : Color.FromArgb(30, 33, 60);
        return b;
    }

    public static TextBox MakeTextBox(string placeholder = "", bool password = false)
    {
        var tb = new TextBox
        {
            BackColor            = InputBg,
            ForeColor            = Text,
            BorderStyle          = BorderStyle.None,
            Font                 = UiFont,
            UseSystemPasswordChar = password,
            Height               = 22
        };
        // Placeholder hint via tag + events
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
