using System;
using Godot;

namespace MonopolyGame;

/// <summary>Theme and small builders so screens read as layout, not boilerplate.</summary>
public static class Ui
{
    private static Font? _font;
    private static Font? _boldFont;
    private static Font? _monoFont;

    public static Font Font => _font ??= new SystemFont { FontNames = new[] { "Bahnschrift", "Segoe UI", "Arial" } };
    public static Font BoldFont => _boldFont ??= new SystemFont { FontNames = new[] { "Bahnschrift", "Segoe UI", "Arial" }, FontWeight = 700 };
    public static Font MonoFont => _monoFont ??= new SystemFont { FontNames = new[] { "Cascadia Mono", "Consolas", "Courier New" } };

    public static Theme CreateTheme()
    {
        var theme = new Theme { DefaultFont = Font, DefaultFontSize = 20 };

        StyleBoxFlat Box(Color bg, Color border, int borderWidth = 2) => new()
        {
            BgColor = bg,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
            ShadowColor = new Color(0, 0, 0, 0.25f),
            ShadowSize = 4,
        };

        var normal = Box(new Color(0.09f, 0.15f, 0.27f, 0.95f), Palette.Border with { A = 0.65f });
        var hover = Box(new Color(0.13f, 0.23f, 0.38f, 0.98f), Palette.Accent);
        var pressed = Box(Palette.Accent with { A = 0.9f }, Palette.Accent);
        var disabled = Box(new Color(0.08f, 0.11f, 0.18f, 0.7f), new Color(0.3f, 0.4f, 0.5f, 0.4f));
        var focus = Box(new Color(0, 0, 0, 0), Palette.Accent, 2);
        focus.DrawCenter = false;

        foreach (var type in new[] { "Button", "OptionButton", "CheckButton" })
        {
            theme.SetStylebox("normal", type, normal);
            theme.SetStylebox("hover", type, hover);
            theme.SetStylebox("pressed", type, pressed);
            theme.SetStylebox("hover_pressed", type, pressed);
            theme.SetStylebox("disabled", type, disabled);
            theme.SetStylebox("focus", type, focus);
            theme.SetColor("font_color", type, Palette.Text);
            theme.SetColor("font_hover_color", type, Colors.White);
            theme.SetColor("font_pressed_color", type, Palette.BackgroundDeep);
            theme.SetColor("font_hover_pressed_color", type, Palette.BackgroundDeep);
            theme.SetColor("font_disabled_color", type, new Color(0.5f, 0.58f, 0.66f, 0.7f));
            theme.SetColor("font_focus_color", type, Colors.White);
        }

        var line = Box(new Color(0.05f, 0.09f, 0.17f, 0.95f), Palette.Border with { A = 0.6f });
        theme.SetStylebox("normal", "LineEdit", line);
        theme.SetStylebox("focus", "LineEdit", Box(new Color(0.05f, 0.09f, 0.17f, 0.95f), Palette.Accent));
        theme.SetColor("font_color", "LineEdit", Palette.Text);
        theme.SetColor("font_placeholder_color", "LineEdit", Palette.TextDim);
        theme.SetColor("caret_color", "LineEdit", Palette.Accent);

        theme.SetColor("font_color", "Label", Palette.Text);
        theme.SetStylebox("panel", "PanelContainer", Box(Palette.Panel, Palette.Border with { A = 0.5f }));

        var popup = Box(Palette.PanelSolid, Palette.Border);
        theme.SetStylebox("panel", "PopupMenu", popup);
        theme.SetColor("font_color", "PopupMenu", Palette.Text);
        theme.SetColor("font_hover_color", "PopupMenu", Colors.White);
        theme.SetStylebox("hover", "PopupMenu", Box(new Color(0.16f, 0.3f, 0.45f), Palette.Accent, 0));

        var slider = Box(new Color(0.12f, 0.2f, 0.32f), Palette.Border with { A = 0.4f }, 1);
        slider.ContentMarginTop = slider.ContentMarginBottom = 3;
        theme.SetStylebox("slider", "HSlider", slider);
        var grabberArea = Box(Palette.Accent with { A = 0.7f }, Palette.Accent, 0);
        grabberArea.ContentMarginTop = grabberArea.ContentMarginBottom = 3;
        theme.SetStylebox("grabber_area", "HSlider", grabberArea);
        theme.SetStylebox("grabber_area_highlight", "HSlider", grabberArea);

        var scroll = Box(new Color(0, 0, 0, 0.2f), new Color(0, 0, 0, 0), 0);
        scroll.ContentMarginLeft = scroll.ContentMarginRight = 4;
        theme.SetStylebox("scroll", "VScrollBar", scroll);
        theme.SetStylebox("grabber", "VScrollBar", Box(Palette.Border with { A = 0.5f }, new Color(0, 0, 0, 0), 0));
        return theme;
    }

    // ------------------------------------------------------------------ builders

    public static Label Label(string text, int size = 20, Color? color = null, bool bold = false, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var label = new Label { Text = text, HorizontalAlignment = align, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", size);
        if (color.HasValue) label.AddThemeColorOverride("font_color", color.Value);
        if (bold) label.AddThemeFontOverride("font", BoldFont);
        return label;
    }

    public static Button Button(string text, Action onPressed, int fontSize = 20, Vector2? minSize = null)
    {
        var button = new Button { Text = text, CustomMinimumSize = minSize ?? new Vector2(0, 44), FocusMode = Control.FocusModeEnum.All };
        button.AddThemeFontSizeOverride("font_size", fontSize);
        button.AddThemeFontOverride("font", BoldFont);
        button.Pressed += () =>
        {
            Sfx.Play(Sfx.Kind.Click);
            onPressed();
        };
        return button;
    }

    public static VBoxContainer VBox(int separation = 8, params Control[] children)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", separation);
        foreach (var c in children) box.AddChild(c);
        return box;
    }

    public static HBoxContainer HBox(int separation = 8, params Control[] children)
    {
        var box = new HBoxContainer();
        box.AddThemeConstantOverride("separation", separation);
        foreach (var c in children) box.AddChild(c);
        return box;
    }

    public static MarginContainer Margin(Control child, int left, int top, int right, int bottom)
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", left);
        margin.AddThemeConstantOverride("margin_top", top);
        margin.AddThemeConstantOverride("margin_right", right);
        margin.AddThemeConstantOverride("margin_bottom", bottom);
        margin.AddChild(child);
        return margin;
    }

    public static Control Spacer(bool horizontal = true)
    {
        var spacer = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        if (horizontal) spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        else spacer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        return spacer;
    }

    public static ColorRect Chip(Color color, float size = 14) =>
        new() { Color = color, CustomMinimumSize = new Vector2(size, size), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };

    public static OptionButton Options(string[] items, int selected, Action<int> onSelected)
    {
        var options = new OptionButton { CustomMinimumSize = new Vector2(0, 40) };
        foreach (var item in items) options.AddItem(item);
        options.Selected = selected;
        options.ItemSelected += index => onSelected((int)index);
        return options;
    }

    public static string Money(int amount) => amount < 0 ? $"-${-amount:N0}" : $"${amount:N0}";
}
