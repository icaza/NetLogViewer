using NetLogViewer.UI.Theme;

namespace NetLogViewer.UI.Controls;

/// <summary>
/// Modern flat owner-drawn tab control styled for Dark Mode.
/// </summary>
public sealed class DarkTabControl : TabControl
{
    public DarkTabControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw |
            ControlStyles.OptimizedDoubleBuffer,
            true);

        SizeMode = TabSizeMode.Normal;
        ItemSize = new Size(120, 32);
        Padding = new Point(16, 4);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(DarkTheme.Background);

        // Draw tab headers
        for (int i = 0; i < TabCount; i++)
        {
            var tabRect = GetTabRect(i);
            bool isSelected = (SelectedIndex == i);

            // Tab background
            var tabBgColor = isSelected ? DarkTheme.Surface : DarkTheme.Card;
            using (var brush = new SolidBrush(tabBgColor))
            {
                g.FillRectangle(brush, tabRect);
            }

            // Tab border
            using (var pen = new Pen(DarkTheme.Border))
            {
                g.DrawRectangle(pen, tabRect);
            }

            // Active indicator bar
            if (isSelected)
            {
                using var accentBrush = new SolidBrush(DarkTheme.Accent);
                g.FillRectangle(accentBrush, tabRect.X, tabRect.Bottom - 3, tabRect.Width, 3);
            }

            // Tab Text
            var textColor = isSelected ? DarkTheme.TextBright : DarkTheme.TextSecondary;
            var textFont = isSelected ? DarkTheme.HeaderFont : DarkTheme.MainFont;

            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
            TextRenderer.DrawText(g, TabPages[i].Text, textFont, tabRect, textColor, flags);
        }

        // Draw border under the tab header strip
        if (TabCount > 0)
        {
            var contentRect = DisplayRectangle;
            using var borderPen = new Pen(DarkTheme.Border);
            g.DrawRectangle(borderPen, contentRect.X - 1, contentRect.Y - 1, contentRect.Width + 1, contentRect.Height + 1);
        }
    }
}

