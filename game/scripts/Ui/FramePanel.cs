using Godot;

namespace MonopolyGame;

/// <summary>
/// HUD frame from the concept art: chamfered corners, translucent navy fill, thin cyan outline with
/// brighter corner brackets. Lays out its children like a MarginContainer.
/// </summary>
public partial class FramePanel : MarginContainer
{
    public Color Fill { get; set; } = Palette.Panel;
    public Color BorderColor { get; set; } = Palette.Border;
    public float Chamfer { get; set; } = 14f;
    public float BracketLength { get; set; } = 26f;
    public int Padding { get; set; } = 16;

    public FramePanel() { }

    public FramePanel(Control child, int padding = 16)
    {
        Padding = padding;
        AddChild(child);
    }

    public override void _Ready()
    {
        AddThemeConstantOverride("margin_left", Padding);
        AddThemeConstantOverride("margin_right", Padding);
        AddThemeConstantOverride("margin_top", Padding - 4);
        AddThemeConstantOverride("margin_bottom", Padding - 4);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) QueueRedraw();
    }

    public override void _Draw()
    {
        float w = Size.X, h = Size.Y, c = Mathf.Min(Chamfer, Mathf.Min(w, h) * 0.3f);
        var outline = new[]
        {
            new Vector2(c, 0), new Vector2(w - c, 0), new Vector2(w, c), new Vector2(w, h - c),
            new Vector2(w - c, h), new Vector2(c, h), new Vector2(0, h - c), new Vector2(0, c), new Vector2(c, 0),
        };
        DrawColoredPolygon(outline[..8], Fill);
        DrawPolyline(outline, BorderColor with { A = 0.45f }, 1.5f, true);

        // Bright brackets around each chamfered corner.
        float b = Mathf.Min(BracketLength, Mathf.Min(w, h) * 0.35f);
        var bright = BorderColor with { A = 0.95f };
        DrawPolyline(new[] { new Vector2(0, c + b), new Vector2(0, c), new Vector2(c, 0), new Vector2(c + b, 0) }, bright, 2.5f, true);
        DrawPolyline(new[] { new Vector2(w - c - b, 0), new Vector2(w - c, 0), new Vector2(w, c), new Vector2(w, c + b) }, bright, 2.5f, true);
        DrawPolyline(new[] { new Vector2(w, h - c - b), new Vector2(w, h - c), new Vector2(w - c, h), new Vector2(w - c - b, h) }, bright, 2.5f, true);
        DrawPolyline(new[] { new Vector2(c + b, h), new Vector2(c, h), new Vector2(0, h - c), new Vector2(0, h - c - b) }, bright, 2.5f, true);
    }
}
