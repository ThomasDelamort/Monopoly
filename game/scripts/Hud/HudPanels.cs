using Godot;
using Monopoly.Core.Algorithms;
using Monopoly.Core.DataStructures;
using Monopoly.Core.Model;
using Monopoly.Core.Rules;

namespace MonopolyGame;

public static class Layout
{
    /// <summary>Anchors a control to a screen corner/edge with a margin, growing inward as its content grows.</summary>
    public static T Place<T>(T control, Control.LayoutPreset preset, float marginX, float marginY) where T : Control
    {
        control.SetAnchorsPreset(preset);
        bool right = preset is Control.LayoutPreset.TopRight or Control.LayoutPreset.BottomRight or Control.LayoutPreset.CenterRight;
        bool bottom = preset is Control.LayoutPreset.BottomLeft or Control.LayoutPreset.BottomRight or Control.LayoutPreset.CenterBottom;
        bool hCenter = preset is Control.LayoutPreset.CenterTop or Control.LayoutPreset.CenterBottom or Control.LayoutPreset.Center;
        bool vCenter = preset is Control.LayoutPreset.CenterLeft or Control.LayoutPreset.CenterRight or Control.LayoutPreset.Center;
        control.GrowHorizontal = hCenter ? Control.GrowDirection.Both : right ? Control.GrowDirection.Begin : Control.GrowDirection.End;
        control.GrowVertical = vCenter ? Control.GrowDirection.Both : bottom ? Control.GrowDirection.Begin : Control.GrowDirection.End;
        float x = hCenter ? 0 : right ? -marginX : marginX;
        float y = vCenter ? 0 : bottom ? -marginY : marginY;
        control.OffsetLeft = control.OffsetRight = x;
        control.OffsetTop = control.OffsetBottom = y;
        return control;
    }

    public static void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    public static PixelText Heading(string text, float pixel = 3f) => new(text, pixel, Palette.Accent) { SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
}

/// <summary>Top-left: the player whose turn it is.</summary>
public partial class PlayerCard : FramePanel
{
    private readonly TextureRect _portrait;
    private readonly Label _name;
    private readonly Label _cash;
    private readonly Label _where;
    private readonly Label _status;

    public PlayerCard()
    {
        CustomMinimumSize = new Vector2(360, 0);
        _portrait = Portraits.Rect(0, 104);
        _name = Ui.Label("", 24, Palette.Text, bold: true);
        _cash = Ui.Label("", 30, Palette.Gold, bold: true);
        _where = Ui.Label("", 16, Palette.TextDim);
        _status = Ui.Label("", 16, Palette.Accent);
        var info = Ui.VBox(0, _name, _cash, _where, _status);
        info.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(Ui.VBox(6, Layout.Heading("CURRENT PLAYER"), Ui.HBox(12, _portrait, info)));
    }

    public void Show(Game game, Player player)
    {
        _portrait.Texture = Portraits.Get(player.CharacterId);
        _name.Text = player.Name + (player.IsBot ? "  · BOT" : "");
        _name.AddThemeColorOverride("font_color", Palette.PlayerUi(player.CharacterId));
        _cash.Text = Ui.Money(player.Cash);
        _where.Text = $"On {player.Position.Value.Name}  (node {player.Position.Index})";
        string status = player.InJail ? $"In jail · attempt {player.JailTurns + 1}/3" : "";
        if (player.JailCards.Count > 0) status += (status.Length > 0 ? " · " : "") + $"Jail-free card ×{player.JailCards.Count}";
        if (player.Properties.Count > 0) status += (status.Length > 0 ? " · " : "") + $"{player.Properties.Count} deed{(player.Properties.Count == 1 ? "" : "s")}";
        _status.Text = status;
    }

    /// <summary>Shows the cash as of the event being animated (the engine is already ahead).</summary>
    public void OverrideCash(int cash) => _cash.Text = Ui.Money(cash);
}

/// <summary>Top-right: the turn-order queue, front first.</summary>
public partial class QueuePanel : FramePanel
{
    private readonly VBoxContainer _rows = Ui.VBox(6);

    public QueuePanel()
    {
        CustomMinimumSize = new Vector2(290, 0);
        AddChild(Ui.VBox(8, Layout.Heading("UP NEXT"), Ui.Label("Turn order · Queue (front → back)", 14, Palette.TextDim), _rows));
    }

    public void Show(Game game, System.Func<Player, int> cashOf)
    {
        Layout.Clear(_rows);
        int position = 1;
        foreach (var player in game.TurnQueue)
        {
            var row = Ui.HBox(10,
                Ui.Label(position == 1 ? "FRONT" : $"#{position}", 13, position == 1 ? Palette.Gold : Palette.TextDim, bold: true),
                Portraits.Rect(player.CharacterId, 38),
                Ui.Label(player.Name, 18, Palette.PlayerUi(player.CharacterId), bold: true),
                Ui.Spacer(),
                Ui.Label(Ui.Money(cashOf(player)), 17, Palette.Text));
            row.GetChild<Label>(0).CustomMinimumSize = new Vector2(48, 0);
            _rows.AddChild(row);
            position++;
        }
        if (position == 1) _rows.AddChild(Ui.Label("(empty)", 16, Palette.TextDim));
    }
}

/// <summary>Bottom-left: everyone ranked by net worth with a stable merge sort.</summary>
public partial class StandingsPanel : FramePanel
{
    private readonly VBoxContainer _rows = Ui.VBox(4);
    private readonly Label _stats = Ui.Label("", 14, Palette.TextDim);

    public StandingsPanel()
    {
        CustomMinimumSize = new Vector2(360, 0);
        AddChild(Ui.VBox(6, Layout.Heading("STANDINGS"), _stats, _rows));
    }

    public void Show(Game game)
    {
        var ranking = game.Standings(out var stats, SortAlgorithm.MergeSort);
        _stats.Text = $"Merge sort by net worth · {stats.Comparisons} comparisons · {stats.BigO}";
        Layout.Clear(_rows);
        for (int i = 0; i < ranking.Length; i++)
        {
            var p = ranking[i];
            bool current = p == game.CurrentPlayer && game.Phase != Phase.GameOver;
            var row = Ui.HBox(10,
                Ui.Label($"{i + 1}.", 18, current ? Palette.Gold : Palette.TextDim, bold: true),
                Ui.Chip(Palette.PlayerUi(p.CharacterId)),
                Ui.Label(p.Name, 18, p.IsBankrupt ? Palette.TextDim : Palette.Text, bold: current),
                Ui.Spacer(),
                Ui.Label(p.IsBankrupt ? "BANKRUPT" : Ui.Money(p.NetWorth), 18, p.IsBankrupt ? Palette.Bad : Palette.Gold, bold: true));
            row.GetChild<Label>(0).CustomMinimumSize = new Vector2(26, 0);
            _rows.AddChild(row);
        }
    }
}

/// <summary>Top-center announcements that fade in and out.</summary>
public partial class Banner : Control
{
    private readonly FramePanel _frame;
    private readonly Label _text;
    private readonly Label _sub;
    private Tween? _tween;

    public Banner()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _text = Ui.Label("", 34, Palette.Text, bold: true, align: HorizontalAlignment.Center);
        _sub = Ui.Label("", 18, Palette.TextDim, align: HorizontalAlignment.Center);
        _frame = new FramePanel(Ui.VBox(2, _text, _sub), 22) { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_frame);
        Modulate = Colors.Transparent;
    }

    public void Show(string text, string subtitle = "", Color? color = null, float seconds = 1.6f)
    {
        _text.Text = text;
        _text.AddThemeColorOverride("font_color", color ?? Palette.Text);
        _sub.Text = subtitle;
        _sub.Visible = subtitle.Length > 0;
        _frame.ResetSize();
        _frame.Position = new Vector2(-_frame.Size.X / 2, 0);
        _tween?.Kill();
        _tween = CreateTween();
        _tween.TweenProperty(this, "modulate", Colors.White, 0.18f);
        _tween.TweenInterval(seconds);
        _tween.TweenProperty(this, "modulate", Colors.Transparent, 0.35f);
    }
}

/// <summary>Recent game messages, kept in a bounded queue (oldest drops off).</summary>
public partial class EventLogPanel : FramePanel
{
    private readonly ArrayQueue<string> _lines = new(7, bounded: true);
    private readonly VBoxContainer _rows = Ui.VBox(2);

    public EventLogPanel()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Padding = 12;
        Fill = new Color(0.05f, 0.08f, 0.15f, 0.62f);
        BorderColor = Palette.Border with { A = 0.45f };
        BracketLength = 10;
        CustomMinimumSize = new Vector2(372, 0);
        AddChild(_rows);
        Visible = false;
    }

    public void Add(string line)
    {
        Visible = true;
        _lines.Enqueue(line);
        Layout.Clear(_rows);
        int i = 0, n = _lines.Count;
        foreach (var text in _lines)
        {
            var label = Ui.Label(text, 15, Palette.Text);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = new Vector2(348, 0);
            label.Modulate = new Color(1, 1, 1, 0.35f + 0.65f * (i + 1) / n);
            label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.6f));
            label.AddThemeConstantOverride("shadow_offset_x", 1);
            label.AddThemeConstantOverride("shadow_offset_y", 1);
            _rows.AddChild(label);
            i++;
        }
    }
}
