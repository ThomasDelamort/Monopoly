using Godot;
using Monopoly.Core.Algorithms;
using Monopoly.Core.Model;
using Monopoly.Core.Rules;

namespace MonopolyGame;

/// <summary>
/// Build / sell / mortgage screen. The list is sorted with the algorithm the player picks
/// (merge, quick, insertion or heap sort) and filtered with a binary-search prefix lookup;
/// both report how many comparisons they took.
/// </summary>
public partial class ManagePanel : FramePanel
{
    private static readonly string[] SortNames = { "Board order", "Price", "Name", "Color group", "Rent" };
    private static readonly string[] AlgorithmNames = { "Merge sort", "Quick sort", "Insertion sort", "Heap sort" };

    private readonly GameScreen _screen;
    private readonly Label _header = Ui.Label("", 24, Palette.Text, bold: true);
    private readonly Label _stats = Ui.Label("", 15, Palette.Gold);
    private readonly VBoxContainer _list = Ui.VBox(6);
    private readonly LineEdit _search = new() { PlaceholderText = "Search name… (binary search)", CustomMinimumSize = new Vector2(300, 40) };
    private int _order;
    private int _algorithm;

    public ManagePanel(GameScreen screen)
    {
        _screen = screen;
        var close = new System.Action(screen.CloseManage);
        Padding = 24;
        CustomMinimumSize = new Vector2(980, 560);
        MouseFilter = MouseFilterEnum.Stop;

        var controls = Ui.HBox(10,
            Ui.Label("Sort by", 16, Palette.TextDim),
            Ui.Options(SortNames, 0, i => { _order = i; Refresh(); }),
            Ui.Label("using", 16, Palette.TextDim),
            Ui.Options(AlgorithmNames, 0, i => { _algorithm = i; Refresh(); }),
            Ui.Spacer(),
            _search);
        _search.TextChanged += _ => Refresh();

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 340), SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_list);

        var footer = Ui.HBox(10, Ui.Spacer(),
            Ui.Button("UNDO", () => _screen.HumanDo(() => _screen.Game.Undo()), 18, new Vector2(140, 46)),
            Ui.Button("CLOSE  [Esc]", close, 18, new Vector2(170, 46)));

        AddChild(Ui.VBox(12, Ui.HBox(12, Layout.Heading("MANAGE PROPERTIES", 4f), Ui.Spacer(), _header), controls, _stats, scroll, footer));
        Visible = false;
    }

    public void Open()
    {
        Visible = true;
        Refresh();
    }

    public void Close() => Visible = false;

    public void Refresh()
    {
        if (!Visible) return;
        var game = _screen.Game;
        var player = game.Actor;
        _header.Text = $"{player.Name} · {Ui.Money(player.Cash)}";
        bool canAct = _screen.AwaitingHuman && !player.IsBot;

        OwnableTile[] items;
        if (_search.Text.Trim().Length > 0)
        {
            items = game.SearchPortfolio(player, _search.Text, out int comparisons);
            _stats.Text = $"Binary search (lower bound) on the name-sorted list: {comparisons} comparisons for {player.Properties.Count} deeds · O(log n)";
        }
        else
        {
            var algorithm = (SortAlgorithm)_algorithm;
            items = game.Portfolio(player, (PortfolioOrder)_order, algorithm, out var stats);
            _stats.Text = $"{AlgorithmNames[_algorithm]} sorted {stats.Count} deeds by {SortNames[_order].ToLowerInvariant()}: {stats.Comparisons} comparisons, {stats.Writes} writes · {stats.BigO}";
        }

        Layout.Clear(_list);
        if (items.Length == 0)
        {
            _list.AddChild(Ui.Label(player.Properties.Count == 0 ? "You don't own any property yet." : "No matches.", 18, Palette.TextDim));
            return;
        }

        foreach (var tile in items)
        {
            var chip = Ui.Chip(tile is PropertyTile p ? Palette.Group(p.Group) : new Color("c9d3da"), 18);
            var name = Ui.Label(tile.Name, 18, tile.IsMortgaged ? Palette.TextDim : Palette.Text, bold: true);
            name.CustomMinimumSize = new Vector2(250, 0);
            string buildings = tile switch
            {
                PropertyTile { HasHotel: true } => "HOTEL",
                PropertyTile { Houses: > 0 } h => $"{h.Houses} house{(h.Houses > 1 ? "s" : "")}",
                PropertyTile pt => game.OwnsFullGroup(player, pt.Group) ? "full set" : "",
                RailroadTile => "railroad",
                _ => "utility",
            };
            var info = Ui.Label(tile.IsMortgaged ? "MORTGAGED" : buildings, 16, tile.IsMortgaged ? Palette.Bad : Palette.Accent);
            info.CustomMinimumSize = new Vector2(120, 0);
            var rent = Ui.Label(tile is UtilityTile ? "4×/10× dice" : "rent " + Ui.Money(game.RentFor(tile)), 16, Palette.Gold);
            rent.CustomMinimumSize = new Vector2(120, 0);

            var row = Ui.HBox(10, chip, name, info, rent, Ui.Spacer());
            if (tile is PropertyTile property)
            {
                row.AddChild(Action(property.Houses == 4 ? $"+HOTEL {Ui.Money(property.HouseCost)}" : $"+HOUSE {Ui.Money(property.HouseCost)}",
                    canAct && game.CanBuild(property), () => game.Build(property)));
                row.AddChild(Action($"SELL +{Ui.Money(property.HouseCost / 2)}", canAct && game.CanSellBuilding(property), () => game.SellBuilding(property)));
            }
            if (tile.IsMortgaged)
                row.AddChild(Action($"UNMORTGAGE {Ui.Money(tile.UnmortgageCost)}", canAct && game.CanUnmortgage(tile), () => game.Unmortgage(tile)));
            else
                row.AddChild(Action($"MORTGAGE +{Ui.Money(tile.MortgageValue)}", canAct && game.CanMortgage(tile), () => game.Mortgage(tile)));
            _list.AddChild(row);
        }
    }

    private Button Action(string text, bool enabled, System.Action action)
    {
        var button = Ui.Button(text, () => _screen.HumanDo(action), 15, new Vector2(0, 38));
        button.Disabled = !enabled;
        return button;
    }
}
