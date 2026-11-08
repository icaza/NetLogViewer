using NetLogViewer.UI.Theme;

namespace NetLogViewer.UI.Controls;

/// <summary>
/// High-performance flicker-free virtualized ListView supporting native Windows dark scrollbars.
/// </summary>
public sealed class DoubleBufferedListView : ListView
{
    public DoubleBufferedListView()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint,
            true);

        View = View.Details;
        FullRowSelect = true;
        GridLines = false;
        BackColor = DarkTheme.Surface;
        ForeColor = DarkTheme.TextPrimary;
        Font = DarkTheme.MainFont;
        BorderStyle = BorderStyle.None;
        HeaderStyle = ColumnHeaderStyle.Clickable;
        OwnerDraw = true;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            DarkTheme.SetWindowTheme(Handle, "DarkMode_Explorer", null);
        }
        catch { }
    }

    protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
    {
        var g = e.Graphics;
        using var bgBrush = new SolidBrush(DarkTheme.Card);
        g.FillRectangle(bgBrush, e.Bounds);

        using var borderPen = new Pen(DarkTheme.Border);
        g.DrawLine(borderPen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        g.DrawLine(borderPen, e.Bounds.Right - 1, e.Bounds.Top + 4, e.Bounds.Right - 1, e.Bounds.Bottom - 5);

        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis;
        var textRect = new Rectangle(e.Bounds.Left + 8, e.Bounds.Top, e.Bounds.Width - 12, e.Bounds.Height);
        TextRenderer.DrawText(g, e.Header?.Text ?? "", DarkTheme.HeaderFont, textRect, DarkTheme.TextBright, flags);
    }

    protected override void OnDrawItem(DrawListViewItemEventArgs e)
    {
        // Handled in subitem draw
    }

    protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
    {
        var g = e.Graphics;
        bool isSelected = e.Item?.Selected ?? false;

        // Background
        Color bgColor = isSelected ? DarkTheme.Selection : (e.ItemIndex % 2 == 0 ? DarkTheme.Surface : DarkTheme.Card);
        using (var brush = new SolidBrush(bgColor))
        {
            g.FillRectangle(brush, e.Bounds);
        }

        // Draw text
        string text = e.SubItem?.Text ?? "";
        Color textColor = isSelected ? DarkTheme.TextBright : DarkTheme.TextPrimary;

        // Highlight errors or statuses if column is status or error
        if (!isSelected)
        {
            if (text.StartsWith("200") || text == "OK")
                textColor = DarkTheme.SuccessGreen;
            else if (text.StartsWith("30"))
                textColor = DarkTheme.InfoBlue;
            else if (text.StartsWith("40") || text.StartsWith("50") || text.Contains("ERR_", StringComparison.OrdinalIgnoreCase))
                textColor = DarkTheme.ErrorRed;
        }

        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis;
        var textRect = new Rectangle(e.Bounds.Left + 6, e.Bounds.Top, e.Bounds.Width - 10, e.Bounds.Height);
        TextRenderer.DrawText(g, text, DarkTheme.MainFont, textRect, textColor, flags);
    }
}
