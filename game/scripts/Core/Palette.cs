using Godot;
using Monopoly.Core.Model;

namespace MonopolyGame;

/// <summary>Colors sampled from the concept art: navy night background, cyan HUD, mint tiles, grass board.</summary>
public static class Palette
{
    public static readonly Color Background = new("1b2742");
    public static readonly Color BackgroundDeep = new("141d33");
    public static readonly Color Panel = new(0.07f, 0.11f, 0.2f, 0.88f);
    public static readonly Color PanelSolid = new("17223b");
    public static readonly Color Border = new("5fc9ec");
    public static readonly Color Accent = new("8ce8ff");
    public static readonly Color Text = new("e2f6ff");
    public static readonly Color TextDim = new("8aaac2");
    public static readonly Color Gold = new("ffd166");
    public static readonly Color Good = new("6ee7a8");
    public static readonly Color Bad = new("ff6b6b");

    public static readonly Color TileBase = new("d3f1e5");
    public static readonly Color TileCorner = new("c6ebdd");
    public static readonly Color TileInk = new("27384a");
    public static readonly Color Grass = new("95bf55");
    public static readonly Color GrassDark = new("7da246");
    public static readonly Color Earth = new("7b5236");
    public static readonly Color EarthDark = new("5d3d28");
    public static readonly Color Road = new("5b6372");
    public static readonly Color Stone = new("8e8a98");

    public static Color Group(ColorGroup group) => group switch
    {
        ColorGroup.Brown => new Color("8a4f2c"),
        ColorGroup.LightBlue => new Color("9fd8f5"),
        ColorGroup.Pink => new Color("d6409a"),
        ColorGroup.Orange => new Color("f39223"),
        ColorGroup.Red => new Color("e5332f"),
        ColorGroup.Yellow => new Color("f6dd2d"),
        ColorGroup.Green => new Color("27a85a"),
        ColorGroup.DarkBlue => new Color("1f62b5"),
        _ => TileBase,
    };

    /// <summary>Two-tone glow ring per character (left → right gradient), as in the player cards.</summary>
    public static (Color A, Color B) Ring(int characterId) => characterId switch
    {
        0 => (new Color("d9fbff"), new Color("8ff0ff")),
        1 => (new Color("5ff5d2"), new Color("3fd6ff")),
        2 => (new Color("ff9a4a"), new Color("b45cff")),
        _ => (new Color("ffd34a"), new Color("ff3b3b")),
    };

    /// <summary>Single identifying color per character (UI chips, ownership markers).</summary>
    public static Color Player(int characterId) => characterId switch
    {
        0 => new Color("c9372f"),
        1 => new Color("f2b531"),
        2 => new Color("4669c9"),
        _ => new Color("1c1c22"),
    };

    public static Color PlayerUi(int characterId) => characterId switch
    {
        0 => new Color("ff6a5e"),
        1 => new Color("ffd34a"),
        2 => new Color("7d9bff"),
        _ => new Color("e6e6f0"),
    };
}
