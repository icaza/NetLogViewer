using System.Runtime.InteropServices;

namespace NetLogViewer.UI.Theme;

/// <summary>
/// Modern Dark Theme palette and Windows 11/10 immersive dark mode integration.
/// </summary>
public static class DarkTheme
{
    // Deep dark palette inspired by VS Code and Windows 11 Terminal
    public static readonly Color Background = Color.FromArgb(30, 30, 30);           // #1E1E1E
    public static readonly Color Surface = Color.FromArgb(37, 37, 38);              // #252526
    public static readonly Color Card = Color.FromArgb(45, 45, 48);                 // #2D2D30
    public static readonly Color CardHover = Color.FromArgb(58, 58, 62);            // #3A3A3E
    public static readonly Color Border = Color.FromArgb(62, 62, 66);               // #3E3E42
    public static readonly Color BorderLight = Color.FromArgb(80, 80, 85);          // #505055
    public static readonly Color Accent = Color.FromArgb(0, 120, 212);              // #0078D4
    public static readonly Color AccentHover = Color.FromArgb(20, 140, 235);        // #148CEB
    public static readonly Color Selection = Color.FromArgb(38, 79, 120);           // #264F78

    // Text colors
    public static readonly Color TextPrimary = Color.FromArgb(224, 224, 224);       // #E0E0E0
    public static readonly Color TextSecondary = Color.FromArgb(160, 160, 160);     // #A0A0A0
    public static readonly Color TextMuted = Color.FromArgb(115, 115, 115);         // #737373
    public static readonly Color TextBright = Color.FromArgb(255, 255, 255);        // #FFFFFF

    // Status badges
    public static readonly Color SuccessGreen = Color.FromArgb(137, 209, 133);      // #89D185
    public static readonly Color SuccessBg = Color.FromArgb(30, 70, 32);            // #1E4620
    public static readonly Color WarningYellow = Color.FromArgb(229, 192, 123);     // #E5C07B
    public static readonly Color WarningBg = Color.FromArgb(80, 65, 20);            // #504114
    public static readonly Color ErrorRed = Color.FromArgb(244, 135, 113);          // #F48771
    public static readonly Color ErrorBg = Color.FromArgb(90, 29, 29);              // #5A1D1D
    public static readonly Color InfoBlue = Color.FromArgb(55, 148, 255);           // #3794FF
    public static readonly Color InfoBg = Color.FromArgb(20, 50, 90);               // #14325A

    public static readonly Font MainFont = new("Segoe UI", 9.25f, FontStyle.Regular);
    public static readonly Font HeaderFont = new("Segoe UI", 10.5f, FontStyle.Bold);
    public static readonly Font CodeFont = new("Cascadia Code", 9.0f, FontStyle.Regular);
    public static readonly Font SmallFont = new("Segoe UI", 8.25f, FontStyle.Regular);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    public static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string? pszSubIdList);

    public static void EnableImmersiveDarkMode(Form form)
    {
        if (form == null || !form.IsHandleCreated) return;

        int useImmersiveDarkMode = 1;
        // 20 is DWMWA_USE_IMMERSIVE_DARK_MODE on Win10 20H1+ and Win11
        // 19 is used on older Windows 10 builds
        int hr = DwmSetWindowAttribute(form.Handle, 20, ref useImmersiveDarkMode, sizeof(int));
        if (hr != 0)
        {
            DwmSetWindowAttribute(form.Handle, 19, ref useImmersiveDarkMode, sizeof(int));
        }
    }

    public static void ApplyDarkThemeToControl(Control ctrl)
    {
        ctrl.BackColor = Surface;
        ctrl.ForeColor = TextPrimary;
        ctrl.Font = MainFont;

        if (ctrl is Form or Panel or SplitContainer)
        {
            ctrl.BackColor = Background;
        }
        else if (ctrl is TextBox tb)
        {
            tb.BackColor = Card;
            tb.ForeColor = TextBright;
            tb.BorderStyle = BorderStyle.FixedSingle;
        }
        else if (ctrl is Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = Card;
            btn.ForeColor = TextBright;
            btn.FlatAppearance.BorderColor = Border;
            btn.FlatAppearance.MouseOverBackColor = CardHover;
            btn.FlatAppearance.MouseDownBackColor = Selection;
        }
        else if (ctrl is ComboBox cb)
        {
            cb.BackColor = Card;
            cb.ForeColor = TextBright;
            cb.FlatStyle = FlatStyle.Flat;
        }

        foreach (Control child in ctrl.Controls)
        {
            ApplyDarkThemeToControl(child);
        }
    }
}
