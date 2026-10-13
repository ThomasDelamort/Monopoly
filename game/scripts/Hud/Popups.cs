using Godot;
using Monopoly.Core.Model;
using Monopoly.Core.Rules;

namespace MonopolyGame;

/// <summary>Title deed for a property, railroad or utility.</summary>
public partial class DeedCard : FramePanel
{
    private readonly ColorRect _band = new() { CustomMinimumSize = new Vector2(0, 46) };
    private readonly Label _title = Ui.Label("", 20, Palette.BackgroundDeep, bold: true, align: HorizontalAlignment.Center);
    private readonly VBoxContainer _rows = Ui.VBox(3);

    public DeedCard()
    {
        CustomMinimumSize = new Vector2(300, 0);
        MouseFilter = MouseFilterEnum.Ignore;
        _title.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _band.AddChild(_title);
        AddChild(Ui.VBox(8, Layout.Heading("TITLE DEED"), _band, _rows));
        Visible = false;
    }

    public OwnableTile? Tile { get; private set; }

    public void Show(OwnableTile tile, Game game)
    {
        Tile = tile;
        Visible = true;
        _title.Text = tile.Name.ToUpperInvariant();
        var bandColor = tile is PropertyTile p ? Palette.Group(p.Group) : new Color("dfe8ee");
        _band.Color = bandColor;
        _title.AddThemeColorOverride("font_color", bandColor.Luminance > 0.55f ? Palette.BackgroundDeep : Colors.White);
        Layout.Clear(_rows);

        void Line(string left, string right, Color? color = null)
        {
            _rows.AddChild(Ui.HBox(8, Ui.Label(left, 16, color ?? Palette.Text), Ui.Spacer(), Ui.Label(right, 16, color ?? Palette.Text, bold: true)));
        }

        switch (tile)
        {
            case PropertyTile property:
                Line("Rent", Ui.Money(property.Rents[0]));
                Line("  with full color set", Ui.Money(property.Rents[0] * 2), Palette.TextDim);
                for (int h = 1; h <= 4; h++) Line($"With {h} house{(h > 1 ? "s" : "")}", Ui.Money(property.Rents[h]));
                Line("With HOTEL", Ui.Money(property.Rents[5]), Palette.Gold);
                Line("House / hotel cost", Ui.Money(property.HouseCost) + " each", Palette.TextDim);
                break;
            case RailroadTile:
                Line("Rent", "$25");
                Line("If 2 railroads owned", "$50");
                Line("If 3 railroads owned", "$100");
                Line("If 4 railroads owned", "$200");
                break;
            case UtilityTile:
                Line("One utility owned", "4 × dice");
                Line("Both utilities owned", "10 × dice");
                break;
        }
        Line("Price", Ui.Money(tile.Price), Palette.Accent);
        Line("Mortgage value", Ui.Money(tile.MortgageValue), Palette.TextDim);

        string owner = tile.Owner == null ? "For sale" : $"Owned by {tile.Owner.Name}";
        if (tile.IsMortgaged) owner += " · MORTGAGED";
        if (tile is PropertyTile { Houses: > 0 } built) owner += built.HasHotel ? " · hotel" : $" · {built.Houses} house(s)";
        _rows.AddChild(Ui.Label(owner, 16, tile.Owner == null ? Palette.Good : Palette.PlayerUi(tile.Owner.CharacterId), bold: true));
        if (tile.Owner != null && tile is not UtilityTile)
            _rows.AddChild(Ui.Label($"Rent due now: {Ui.Money(game.RentFor(tile))}", 15, Palette.Gold));
    }

    public void HideCard()
    {
        Visible = false;
        Tile = null;
    }
}

/// <summary>Chance / Community Chest card reveal.</summary>
public partial class CardPopup : FramePanel
{
    private readonly Label _deck = Ui.Label("", 28, Colors.White, bold: true, align: HorizontalAlignment.Center);
    private readonly Label _text = Ui.Label("", 22, Palette.Text, align: HorizontalAlignment.Center);
    private Tween? _tween;

    public CardPopup()
    {
        CustomMinimumSize = new Vector2(460, 220);
        MouseFilter = MouseFilterEnum.Ignore;
        Padding = 26;
        _text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _text.CustomMinimumSize = new Vector2(400, 0);
        AddChild(Ui.VBox(16, _deck, _text));
        Visible = false;
        PivotOffset = CustomMinimumSize / 2;
    }

    public void Show(Card card, string playerName, float seconds)
    {
        bool chance = card.Deck == DeckType.Chance;
        _deck.Text = chance ? "? CHANCE ?" : "COMMUNITY CHEST";
        _deck.AddThemeColorOverride("font_color", chance ? new Color("ffae4d") : new Color("7fb6ff"));
        BorderColor = chance ? new Color("ff9a3c") : new Color("5c9dff");
        Fill = chance ? new Color(0.22f, 0.12f, 0.05f, 0.96f) : new Color(0.06f, 0.11f, 0.26f, 0.96f);
        _text.Text = $"{playerName} draws:\n{card.Text}";
        QueueRedraw();
        Visible = true;
        Scale = new Vector2(0.6f, 0.6f);
        Modulate = Colors.Transparent;
        _tween?.Kill();
        _tween = CreateTween();
        _tween.TweenProperty(this, "scale", Vector2.One, 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _tween.Parallel().TweenProperty(this, "modulate", Colors.White, 0.2f);
        _tween.TweenInterval(seconds);
        _tween.TweenProperty(this, "modulate", Colors.Transparent, 0.25f);
        _tween.TweenCallback(Callable.From(() => Visible = false));
    }
}

/// <summary>Live auction: high bid, the bidder queue, and bid buttons when a human must decide.</summary>
public partial class AuctionPanel : FramePanel
{
    private readonly Label _title = Ui.Label("", 24, Palette.Gold, bold: true, align: HorizontalAlignment.Center);
    private readonly Label _high = Ui.Label("", 20, Palette.Text, align: HorizontalAlignment.Center);
    private readonly Label _queue = Ui.Label("", 15, Palette.TextDim, align: HorizontalAlignment.Center);
    private readonly HBoxContainer _buttons = Ui.HBox(10);

    public AuctionPanel()
    {
        CustomMinimumSize = new Vector2(560, 0);
        Padding = 22;
        _buttons.Alignment = BoxContainer.AlignmentMode.Center;
        _queue.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        AddChild(Ui.VBox(10, Layout.Heading("AUCTION", 4f), _title, _high, _queue, _buttons));
        Visible = false;
    }

    public void Show(Game game, GameScreen screen)
    {
        var auction = game.CurrentAuction;
        if (auction == null)
        {
            Visible = false;
            return;
        }
        Visible = true;
        _title.Text = $"{auction.Tile.Name} (list price {Ui.Money(auction.Tile.Price)})";
        _high.Text = auction.HighBidder == null ? "No bids yet" : $"High bid {Ui.Money(auction.HighBid)} by {auction.HighBidder.Name}";
        string queue = "Bidders queue: ";
        foreach (var bidder in auction.Bidders) queue += (bidder == auction.CurrentBidder ? "▶ " : "") + bidder.Name + "   ";
        _queue.Text = queue;

        Layout.Clear(_buttons);
        var actor = game.Actor;
        if (screen.AwaitingHuman && !actor.IsBot)
        {
            foreach (int raise in new[] { 10, 50, 100 })
            {
                int amount = auction.HighBid + raise;
                var button = Ui.Button($"BID {Ui.Money(amount)}", () => screen.HumanDo(() => game.Bid(amount)), 18, new Vector2(130, 46));
                button.Disabled = !game.CanBid(amount);
                _buttons.AddChild(button);
            }
            _buttons.AddChild(Ui.Button("PASS", () => screen.HumanDo(game.PassBid), 18, new Vector2(110, 46)));
            _buttons.AddChild(Ui.Label($"{actor.Name}: {Ui.Money(actor.Cash)}", 16, Palette.TextDim));
        }
        else
        {
            _buttons.AddChild(Ui.Label($"{actor.Name} is deciding…", 17, Palette.TextDim));
        }
    }
}
