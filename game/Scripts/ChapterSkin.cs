using Godot;

namespace CardRPG;

/// <summary>Quiet, flat surfaces shared by the HUD, cards and field manual.</summary>
public static class ChapterSkin
{
    public static readonly Color PaperInk = new("253d3a"), PaperMuted = new("56665b");
    public static StyleBoxFlat Box(string name = "field-panel", int padding = 18, Color? tint = null)
    {
        var paper = name == "ledger-paper";
        var primary = name == "brass-button";
        var box = new StyleBoxFlat
        {
            BgColor = new Color(paper ? "f1e7ce" : primary ? "a84c37" : "e7dcc2") * (tint ?? Colors.White),
            BorderColor = new Color(paper ? "929c85" : "253d3a"),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 0, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 0,
            ShadowColor = new Color("253d3a40"), ShadowSize = 2, ShadowOffset = new Vector2(3,3),
            ContentMarginLeft = padding, ContentMarginRight = padding,
            ContentMarginTop = padding, ContentMarginBottom = padding
        };
        return box;
    }

    public static PanelContainer Panel(Node parent, string name = "field-panel", int padding = 18)
    {
        var panel = new PanelContainer { TextureFilter = CanvasItem.TextureFilterEnum.Nearest };
        panel.AddThemeStyleboxOverride("panel", Box(name, padding));
        parent.AddChild(panel);
        return panel;
    }
}
