using System;
using Godot;
using Monopoly.Core.DataStructures;
using Monopoly.Core.Model;
using Monopoly.Core.Rules;

namespace MonopolyGame;

/// <summary>
/// Bottom-right: live view of the data structures driving the game. Tabs show the board's circular
/// doubly linked list around the current token, the queues, the stacks, and the raw operation log.
/// </summary>
public partial class DsaPanel : FramePanel
{
    private enum Tab
    {
        List,
        Queues,
        Stacks,
        Log,
    }

    private readonly VBoxContainer _body = Ui.VBox(6);
    private readonly Button[] _tabButtons = new Button[4];
    private Tab _tab = Tab.List;
    private Game? _game;
    private Func<string[]>? _menuStack;

    public DsaPanel()
    {
        CustomMinimumSize = new Vector2(520, 250);
        var tabs = Ui.HBox(6);
        string[] names = { "LINKED LIST", "QUEUES", "STACKS", "OP LOG" };
        for (int i = 0; i < 4; i++)
        {
            var tab = (Tab)i;
            var button = Ui.Button(names[i], () => Select(tab), 14, new Vector2(0, 32));
            button.ToggleMode = true;
            button.FocusMode = FocusModeEnum.None;
            _tabButtons[i] = button;
            tabs.AddChild(button);
        }
        AddChild(Ui.VBox(8, Ui.HBox(10, Layout.Heading("DSA"), Ui.Spacer(), tabs), _body));
        _tabButtons[0].ButtonPressed = true;
    }

    public void Bind(Game game, Func<string[]> menuStack)
    {
        _game = game;
        _menuStack = menuStack;
        game.DsLog.Recorded += _ => _logDirty = true;
        Refresh();
    }

    private bool _logDirty;

    public override void _Process(double delta)
    {
        if (!_logDirty || _tab != Tab.Log || !Visible) return;
        _logDirty = false;
        Refresh();
    }

    private void Select(Tab tab)
    {
        _tab = tab;
        for (int i = 0; i < 4; i++) _tabButtons[i].SetPressedNoSignal(i == (int)tab);
        Refresh();
    }

    public void Refresh()
    {
        if (_game == null || !IsInsideTree()) return;
        Layout.Clear(_body);
        switch (_tab)
        {
            case Tab.List: ShowList(_game); break;
            case Tab.Queues: ShowQueues(_game); break;
            case Tab.Stacks: ShowStacks(_game); break;
            default: ShowLog(_game); break;
        }
    }

    // ------------------------------------------------------------------ linked list

    private void ShowList(Game game)
    {
        var who = game.Phase == Phase.GameOver && game.Winner != null ? game.Winner : game.CurrentPlayer;
        var current = who.Position;
        _body.AddChild(Ui.Label($"Circular Doubly Linked List · {game.Board.Count} nodes · head = {game.Board.GoNode.Value.Name}", 14, Palette.TextDim));

        var chain = Ui.HBox(4);
        chain.Alignment = BoxContainer.AlignmentMode.Center;
        var nodes = new[] { current.Prev.Prev, current.Prev, current, current.Next, current.Next.Next };
        for (int i = 0; i < nodes.Length; i++)
        {
            if (i > 0) chain.AddChild(Ui.Label("⇄", 22, Palette.Accent, bold: true));
            chain.AddChild(NodeBox(nodes[i], nodes[i] == current, game));
        }
        _body.AddChild(chain);

        _body.AddChild(Ui.Label($"{who.Name}'s token holds node {current.Index}. Moving = following Next pointers; \"Go back 3\" follows Prev.", 14, Palette.Text));
        var last = game.DsLog.LastFor("Board (Circular Doubly Linked List)");
        if (last.HasValue)
            _body.AddChild(Ui.Label($"Last traversal: {last.Value.Operation} ({last.Value.Detail}) · {last.Value.Steps} pointer hops · O(k)", 14, Palette.Gold));
    }

    private static Control NodeBox(CdllNode<Tile> node, bool current, Game game)
    {
        var tile = node.Value;
        var color = tile is PropertyTile p ? Palette.Group(p.Group) : Palette.TextDim;
        string name = tile.Name.Length > 13 ? tile.Name[..12] + "…" : tile.Name;
        var box = Ui.VBox(2,
            Ui.Label($"Node {node.Index}", 13, current ? Palette.Gold : Palette.Accent, bold: true, align: HorizontalAlignment.Center),
            new ColorRect { Color = color, CustomMinimumSize = new Vector2(0, 5) },
            Ui.Label(name, 12, Palette.Text, align: HorizontalAlignment.Center));
        var frame = new FramePanel(box, 8)
        {
            Chamfer = 7,
            BracketLength = 8,
            CustomMinimumSize = new Vector2(84, 0),
            BorderColor = current ? Palette.Gold : Palette.Border,
            Fill = current ? new Color(0.2f, 0.18f, 0.08f, 0.9f) : Palette.Panel,
        };
        int occupants = 0;
        foreach (var player in game.Players)
            if (!player.IsBankrupt && player.Position == node) occupants++;
        if (occupants > 0) box.AddChild(Ui.Label(new string('●', occupants), 10, Palette.Good, align: HorizontalAlignment.Center));
        return frame;
    }

    // ------------------------------------------------------------------ queues

    private void ShowQueues(Game game)
    {
        var turn = "front → ";
        foreach (var p in game.TurnQueue) turn += $"[{p.Name}] → ";
        _body.AddChild(Row("Turn order", turn + "back", $"current: {game.CurrentPlayer.Name} (dequeued, re-enqueued at End Turn)"));
        _body.AddChild(Row("Chance deck", $"{game.Chance.Count} cards, face down", "Draw = Dequeue · return under = Enqueue · O(1)"));
        _body.AddChild(Row("Community Chest", $"{game.CommunityChest.Count} cards, face down", "Shuffled once with Fisher–Yates"));
        if (game.CurrentAuction is { } auction)
        {
            var bidders = "";
            foreach (var b in auction.Bidders) bidders += $"[{b.Name}] ";
            _body.AddChild(Row("Auction bidders", bidders, "bid → back of queue · pass → leave queue"));
        }
        var debts = game.PendingDebts;
        if (debts.Length > 0)
        {
            var text = "";
            foreach (var d in debts) text += $"[{d.Debtor.Name} owes {Ui.Money(d.Amount)}] ";
            _body.AddChild(Row("Debts", text, "settled first-in, first-out"));
        }
        _body.AddChild(Row("Game events", $"{game.Events.Count} waiting", "engine enqueues · animator dequeues"));
    }

    // ------------------------------------------------------------------ stacks

    private void ShowStacks(Game game)
    {
        var undo = game.UndoStack.ToArray();
        string undoText = undo.Length == 0 ? "(empty)" : "";
        for (int i = 0; i < undo.Length; i++) undoText += (i == 0 ? "TOP ▸ " : " ▸ ") + undo[i].Label;
        _body.AddChild(Row("Undo (Stack)", undoText, "Build/sell/mortgage push · Undo pops · cleared when you roll or end the turn"));

        var menus = _menuStack?.Invoke() ?? Array.Empty<string>();
        string menuText = "";
        for (int i = 0; i < menus.Length; i++) menuText += (i == 0 ? "TOP ▸ " : " ▸ ") + menus[i];
        _body.AddChild(Row("Menu screens (Stack)", menuText, "Esc pops back to the previous screen"));
        _body.AddChild(Row("Hash maps", $"Tile index: {game.Board.ByName.Count} keys / {game.Board.ByName.BucketCount} buckets",
            $"Color groups: {game.Board.Groups.Count} keys · separate chaining · O(1) average"));
        _body.AddChild(Row("Bot planning (Heaps)", "Max-heap: best house to build · Min-heap: cheapest asset to give up", "Push/Pop O(log n)"));
    }

    // ------------------------------------------------------------------ op log

    private void ShowLog(Game game)
    {
        var ops = game.DsLog.Recent();
        int shown = 0;
        string? previous = null;
        for (int i = ops.Length - 1; i >= 0 && shown < 9; i--)
        {
            var op = ops[i];
            string key = op.Structure + op.Operation + op.Detail;
            if (key == previous) continue;
            previous = key;
            var line = Ui.HBox(8,
                Ui.Label(op.Structure, 13, Palette.Accent, bold: true),
                Ui.Label(op.Operation, 13, Palette.Gold),
                Ui.Label(op.Detail, 13, Palette.Text),
                Ui.Spacer(),
                Ui.Label($"{op.Steps} step{(op.Steps == 1 ? "" : "s")}", 12, Palette.TextDim));
            line.GetChild<Label>(2).ClipText = true;
            line.GetChild<Label>(2).SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _body.AddChild(line);
            shown++;
        }
        if (shown == 0) _body.AddChild(Ui.Label("No operations yet.", 14, Palette.TextDim));
    }

    private static Control Row(string title, string value, string note)
    {
        var valueLabel = Ui.Label(value, 14, Palette.Text);
        valueLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        valueLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var titleLabel = Ui.Label(title, 14, Palette.Accent, bold: true);
        titleLabel.CustomMinimumSize = new Vector2(150, 0);
        titleLabel.VerticalAlignment = VerticalAlignment.Top;
        return Ui.VBox(0, Ui.HBox(8, titleLabel, valueLabel), Ui.Label("    " + note, 12, Palette.TextDim));
    }
}
