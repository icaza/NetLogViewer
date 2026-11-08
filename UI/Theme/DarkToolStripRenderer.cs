namespace NetLogViewer.UI.Theme;

/// <summary>
/// Custom ProfessionalColorTable providing a dark palette for MenuStrip, ToolStrip, and StatusStrip.
/// </summary>
public sealed class DarkColorTable : ProfessionalColorTable
{
    public override Color MenuStripGradientBegin => DarkTheme.Surface;
    public override Color MenuStripGradientEnd => DarkTheme.Surface;
    public override Color ToolStripGradientBegin => DarkTheme.Surface;
    public override Color ToolStripGradientMiddle => DarkTheme.Surface;
    public override Color ToolStripGradientEnd => DarkTheme.Surface;
    public override Color ToolStripBorder => DarkTheme.Border;

    public override Color StatusStripGradientBegin => DarkTheme.Surface;
    public override Color StatusStripGradientEnd => DarkTheme.Surface;

    public override Color MenuBorder => DarkTheme.Border;
    public override Color MenuItemBorder => DarkTheme.Border;
    public override Color MenuItemSelected => DarkTheme.CardHover;
    public override Color MenuItemSelectedGradientBegin => DarkTheme.CardHover;
    public override Color MenuItemSelectedGradientEnd => DarkTheme.CardHover;
    public override Color MenuItemPressedGradientBegin => DarkTheme.Card;
    public override Color MenuItemPressedGradientMiddle => DarkTheme.Card;
    public override Color MenuItemPressedGradientEnd => DarkTheme.Card;

    public override Color ToolStripDropDownBackground => DarkTheme.Surface;
    public override Color ImageMarginGradientBegin => DarkTheme.Surface;
    public override Color ImageMarginGradientMiddle => DarkTheme.Surface;
    public override Color ImageMarginGradientEnd => DarkTheme.Surface;

    public override Color ButtonSelectedHighlight => DarkTheme.CardHover;
    public override Color ButtonSelectedGradientBegin => DarkTheme.CardHover;
    public override Color ButtonSelectedGradientMiddle => DarkTheme.CardHover;
    public override Color ButtonSelectedGradientEnd => DarkTheme.CardHover;
    public override Color ButtonSelectedBorder => DarkTheme.BorderLight;

    public override Color ButtonPressedHighlight => DarkTheme.Selection;
    public override Color ButtonPressedGradientBegin => DarkTheme.Selection;
    public override Color ButtonPressedGradientMiddle => DarkTheme.Selection;
    public override Color ButtonPressedGradientEnd => DarkTheme.Selection;
    public override Color ButtonPressedBorder => DarkTheme.Accent;

    public override Color ButtonCheckedHighlight => DarkTheme.Selection;
    public override Color ButtonCheckedGradientBegin => DarkTheme.Selection;
    public override Color ButtonCheckedGradientMiddle => DarkTheme.Selection;
    public override Color ButtonCheckedGradientEnd => DarkTheme.Selection;

    public override Color SeparatorDark => DarkTheme.Border;
    public override Color SeparatorLight => Color.Transparent;
}

public sealed class DarkToolStripRenderer : ToolStripProfessionalRenderer
{
    public DarkToolStripRenderer() : base(new DarkColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? DarkTheme.TextPrimary : DarkTheme.TextMuted;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = DarkTheme.TextSecondary;
        base.OnRenderArrow(e);
    }
}

