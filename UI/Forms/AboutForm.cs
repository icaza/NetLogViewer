using NetLogViewer.UI.Theme;

namespace NetLogViewer.UI.Forms;

public sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "About NetLog Viewer";
        Size = new Size(460, 320);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = DarkTheme.Background;
        ForeColor = DarkTheme.TextPrimary;
        Font = DarkTheme.MainFont;

        var lblTitle = new Label
        {
            Text = "NetLog Viewer",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = DarkTheme.Accent,
            Location = new Point(24, 20),
            AutoSize = true
        };

        var lblVersion = new Label
        {
            Text = "Version 1.0 (.NET 10, C# 14) - 64-bit",
            ForeColor = DarkTheme.TextSecondary,
            Location = new Point(26, 56),
            AutoSize = true
        };

        var lblDesc = new Label
        {
            Text = "A high-performance, resource-efficient Windows desktop tool for inspecting and analyzing Chromium and Microsoft Edge NetLog export files (chrome://net-export / edge://net-export).\n\n" +
                   "• Streaming parser capable of handling 1.5M+ events under 100 MB RAM\n" +
                   "• Virtualized 60 FPS timeline and source inspector\n" +
                   "• Deep inspection: HTTP headers, raw events, parameters, and cURL generation\n" +
                   "• Fully local and secure: no data leaves your machine",
            Location = new Point(26, 90),
            Size = new Size(390, 130),
            ForeColor = DarkTheme.TextPrimary
        };

        var btnClose = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            Location = new Point(320, 235),
            Size = new Size(95, 32),
            BackColor = DarkTheme.Card,
            ForeColor = DarkTheme.TextBright,
            FlatStyle = FlatStyle.Flat
        };
        btnClose.FlatAppearance.BorderColor = DarkTheme.Border;

        Controls.Add(lblTitle);
        Controls.Add(lblVersion);
        Controls.Add(lblDesc);
        Controls.Add(btnClose);
        AcceptButton = btnClose;

        Load += (s, e) => DarkTheme.EnableImmersiveDarkMode(this);
    }
}

