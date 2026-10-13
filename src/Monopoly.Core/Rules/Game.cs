using System;
using System.Runtime.CompilerServices;
using Monopoly.Core.Algorithms;
using Monopoly.Core.DataStructures;
using Monopoly.Core.Events;
using Monopoly.Core.Model;

[assembly: InternalsVisibleTo("Monopoly.Core.Tests")]
[assembly: InternalsVisibleTo("Monopoly")] // the Godot game uses the test hooks for its --give debug switch

namespace Monopoly.Core.Rules;

public enum Phase
{
    NotStarted,
    /// <summary>Current player may roll (and manage properties first).</summary>
    AwaitRoll,
    /// <summary>Current player starts the turn in jail: pay, use a card, or roll for doubles.</summary>
    InJail,
    /// <summary>Landed on an unowned property: buy it or send it to auction.</summary>
    BuyDecision,
    Auction,
    /// <summary>A player owes more cash than they hold but could cover it by selling/mortgaging.</summary>
    RaiseFunds,
    /// <summary>Move resolved; current player may manage properties, then end the turn.</summary>
    PostRoll,
    GameOver,
}

public enum PortfolioOrder
{
    Board,
    Price,
    Name,
    Group,
    Rent,
}

public sealed record Debt(Player Debtor, Player? Creditor, int Amount, string Reason);

/// <summary>Extra rent rules for a landing caused by a "nearest Railroad/Utility" card.</summary>
public readonly record struct LandingContext(bool DoubleRailroadRent, bool UtilityTenTimes);

public sealed class Auction
{
    internal Auction(OwnableTile tile, ArrayQueue<Player> bidders)
    {
        Tile = tile;
        Bidders = bidders;
    }

    public OwnableTile Tile { get; }

    /// <summary>Bidders still in, in turn order. The front is the one who must bid or pass now.</summary>
    public ArrayQueue<Player> Bidders { get; }

    public int HighBid { get; internal set; }
    public Player? HighBidder { get; internal set; }
    public Player CurrentBidder => Bidders.Peek();
}

/// <summary>
/// The Monopoly rules engine. Every public action resolves synchronously, leaves the game in a
/// <see cref="Phase"/> that names who must act next (<see cref="Actor"/>), and enqueues
/// <see cref="GameEvent"/>s describing what happened for the presentation layer to animate.
/// </summary>
public sealed class Game
{
    private readonly IRandom _random;
    private readonly IRandom _dice;
    private readonly ArrayQueue<Debt> _debts;
    private Action? _afterDebts;
    private OwnableTile? _offer;
    private bool _rollAgain;
    private int _turnsThisRound;
    private int _playersThisRound;

    public Game(GameSetup setup, IRandom? random = null, IRandom? dice = null)
    {
        if (setup.Seats.Length is < 2 or > GameSetup.SeatCount) throw new ArgumentException("A game needs 2-4 seats.", nameof(setup));
        Setup = setup;
        _random = random ?? (setup.Seed == 0 ? XorShiftRandom.FromClock() : new XorShiftRandom(setup.Seed));
        _dice = dice ?? _random;

        DsLog = new DsLog(96);
        Board = Board.LoadClassic();
        Board.SetTracer(DsLog);

        var (chance, chest) = CardLibrary.LoadClassic();
        Chance = new Deck(DeckType.Chance, chance, _random, DsLog);
        CommunityChest = new Deck(DeckType.CommunityChest, chest, _random, DsLog);

        Players = new Player[setup.Seats.Length];
        for (int i = 0; i < Players.Length; i++)
        {
            var seat = setup.Seats[i];
            Players[i] = new Player(i, seat.Name, seat.CharacterId, seat.IsBot, setup.StartingCash, Board.GoNode);
        }

        TurnQueue = new ArrayQueue<Player>(GameSetup.SeatCount) { Tracer = DsLog, TraceName = "Turn Order (Queue)" };
        UndoStack = new ArrayStack<IGameCommand> { Tracer = DsLog, TraceName = "Undo (Stack)" };
        _debts = new ArrayQueue<Debt>(4) { Tracer = DsLog, TraceName = "Debts (Queue)" };
        Events = new ArrayQueue<GameEvent>(64);

        HousesInBank = setup.HousesInBank;
        HotelsInBank = setup.HotelsInBank;
        LandingOdds = LandingProbability.Compute(Board.Count, Board.JailNode.Index, Board.IndexOfKind(TileKind.GoToJail));
        CurrentPlayer = Players[0];
    }

    public GameSetup Setup { get; }
    public Board Board { get; }
    public Deck Chance { get; }
    public Deck CommunityChest { get; }

    /// <summary>In seat order.</summary>
    public Player[] Players { get; }

    /// <summary>Players waiting for their turn (the current player is dequeued while playing).</summary>
    public ArrayQueue<Player> TurnQueue { get; }

    public ArrayStack<IGameCommand> UndoStack { get; }
    public ArrayQueue<GameEvent> Events { get; }
    public DsLog DsLog { get; }

    /// <summary>Long-run landing probability per tile index (Markov chain).</summary>
    public double[] LandingOdds { get; }

    public Phase Phase { get; private set; } = Phase.NotStarted;
    public Player CurrentPlayer { get; private set; }
    public int TurnNumber { get; private set; }
    public int Round { get; private set; }
    public (int Die1, int Die2) LastRoll { get; private set; }
    public int HousesInBank { get; private set; }
    public int HotelsInBank { get; private set; }
    public Auction? CurrentAuction { get; private set; }
    public Player? Winner { get; private set; }
    public Player[]? FinalRanking { get; private set; }

    public OwnableTile? PendingPurchase => Phase == Phase.BuyDecision ? _offer : null;
    public Debt? CurrentDebt => Phase == Phase.RaiseFunds ? _debts.Peek() : null;
    public Debt[] PendingDebts => _debts.ToArray();

    /// <summary>Who has to make the next decision.</summary>
    public Player Actor => Phase switch
    {
        Phase.Auction => CurrentAuction!.CurrentBidder,
        Phase.RaiseFunds => _debts.Peek().Debtor,
        _ => CurrentPlayer,
    };

    public int ActivePlayerCount
    {
        get
        {
            int n = 0;
            foreach (var p in Players)
                if (!p.IsBankrupt) n++;
            return n;
        }
    }

    // ======================================================================================= start / turns

    public void Start()
    {
        Require(Phase == Phase.NotStarted, "start");
        var rolls = new int[Players.Length];
        foreach (var p in Players) rolls[p.Id] = RollDie() + RollDie();

        // Highest opening roll goes first; insertion sort is stable so ties keep seat order.
        var order = (Player[])Players.Clone();
        var stats = Sorting.InsertionSort<Player>(order, (a, b) => rolls[b.Id].CompareTo(rolls[a.Id]));
        DsLog.Record("Turn Order", "InsertionSort", "by opening roll", (int)stats.Comparisons);

        foreach (var p in order) TurnQueue.Enqueue(p);
        Emit(new TurnOrderDecidedEvent(order, rolls));
        BeginNextTurn();
    }

    public bool CanEndTurn => Phase == Phase.PostRoll;

    public void EndTurn()
    {
        Require(CanEndTurn, "end the turn");
        UndoStack.Clear();
        Emit(new TurnEndedEvent(CurrentPlayer));
        TurnQueue.Enqueue(CurrentPlayer);
        BeginNextTurn();
    }

    private void BeginNextTurn()
    {
        if (Phase == Phase.GameOver || CheckGameOver()) return;

        if (_turnsThisRound >= _playersThisRound)
        {
            Round++;
            _turnsThisRound = 0;
            _playersThisRound = TurnQueue.Count;
            if (Setup.MaxRounds > 0 && Round > Setup.MaxRounds)
            {
                Round = Setup.MaxRounds;
                EndGame($"Round limit ({Setup.MaxRounds}) reached");
                return;
            }
        }
        _turnsThisRound++;

        CurrentPlayer = TurnQueue.Dequeue();
        TurnNumber++;
        _rollAgain = false;
        CurrentPlayer.ConsecutiveDoubles = 0;
        UndoStack.Clear();
        Phase = CurrentPlayer.InJail ? Phase.InJail : Phase.AwaitRoll;
        Emit(new TurnStartedEvent(CurrentPlayer, TurnNumber, Round));
    }

    // ======================================================================================= rolling & moving

    public bool CanRoll => Phase is Phase.AwaitRoll or Phase.InJail;

    public void Roll()
    {
        Require(CanRoll, "roll");
        UndoStack.Clear();
        var p = CurrentPlayer;
        int d1 = RollDie(), d2 = RollDie();
        LastRoll = (d1, d2);
        Emit(new DiceRolledEvent(p, d1, d2));
        bool doubles = d1 == d2;

        if (Phase == Phase.InJail)
        {
            _rollAgain = false; // doubles out of jail do not earn another roll
            if (doubles)
            {
                Release(p, "rolled doubles");
                MoveBy(p, d1 + d2);
                ResolveLanding(p, default);
                return;
            }
            p.JailTurns++;
            if (p.JailTurns < 3)
            {
                Emit(new LogEvent($"{p.Name} stays in jail ({p.JailTurns}/3)."));
                Phase = Phase.PostRoll;
                return;
            }
            // Third failed attempt: pay the fine, then move by this roll.
            _afterDebts = () =>
            {
                Release(p, "paid the fine after three tries");
                MoveBy(p, d1 + d2);
                ResolveLanding(p, default);
            };
            Charge(p, null, Setup.JailFine, "Jail fine");
            ProcessDebts();
            return;
        }

        if (doubles)
        {
            p.ConsecutiveDoubles++;
            if (p.ConsecutiveDoubles == 3)
            {
                _rollAgain = false;
                SendToJail(p, "rolled doubles three times");
                FinishResolution();
                return;
            }
        }
        _rollAgain = doubles;
        MoveBy(p, d1 + d2);
        ResolveLanding(p, default);
    }

    /// <summary>Walks the token node by node along the linked list (backward if negative).</summary>
    private void MoveBy(Player p, int steps)
    {
        if (steps == 0) return;
        var path = new int[Math.Abs(steps)];
        var start = p.Position;
        var node = start;
        int laps = 0;
        for (int i = 0; i < path.Length; i++)
        {
            node = steps > 0 ? node.Next : node.Prev;
            if (steps > 0 && node == Board.GoNode) laps++;
            path[i] = node.Index;
        }
        DsLog.Record("Board (Circular Doubly Linked List)", steps > 0 ? $"Next ×{steps}" : $"Prev ×{-steps}", $"{start.Value} → {node.Value}", path.Length);
        p.Position = node;
        Emit(new MovedEvent(p, path, steps < 0, false));

        for (int i = 0; i < laps; i++)
        {
            Emit(new PassedGoEvent(p, Setup.GoSalary));
            Transfer(null, p, Setup.GoSalary, "Passed GO");
        }
    }

    private void SendToJail(Player p, string reason)
    {
        p.Position = Board.JailNode;
        p.InJail = true;
        p.JailTurns = 0;
        p.ConsecutiveDoubles = 0;
        Emit(new MovedEvent(p, new[] { Board.JailNode.Index }, false, true));
        Emit(new JailEvent(p, true, reason));
    }

    private void Release(Player p, string reason)
    {
        p.InJail = false;
        p.JailTurns = 0;
        Emit(new JailEvent(p, false, reason));
    }

    // ======================================================================================= jail options

    public bool CanPayJailFine => Phase == Phase.InJail && CurrentPlayer.Cash >= Setup.JailFine;

    public void PayJailFine()
    {
        Require(CanPayJailFine, "pay the jail fine");
        UndoStack.Clear();
        Transfer(CurrentPlayer, null, Setup.JailFine, "Jail fine");
        Release(CurrentPlayer, "paid the fine");
        Phase = Phase.AwaitRoll;
    }

    public bool CanUseJailCard => Phase == Phase.InJail && CurrentPlayer.JailCards.Count > 0;

    public void UseJailCard()
    {
        Require(CanUseJailCard, "use a Get Out of Jail Free card");
        UndoStack.Clear();
        var cards = CurrentPlayer.JailCards;
        var card = cards[cards.Count - 1];
        cards.RemoveAt(cards.Count - 1);
        DeckOf(card).ReturnToBottom(card);
        Release(CurrentPlayer, "used a Get Out of Jail Free card");
        Phase = Phase.AwaitRoll;
    }

    // ======================================================================================= landing

    private void ResolveLanding(Player p, LandingContext context)
    {
        var tile = p.Position.Value;
        switch (tile)
        {
            case OwnableTile o when o.Owner == null:
                _offer = o;
                Phase = Phase.BuyDecision;
                Emit(new PurchaseOfferedEvent(p, o));
                return;
            case OwnableTile o when o.Owner == p:
                FinishResolution();
                return;
            case OwnableTile o when o.IsMortgaged:
                Emit(new LogEvent($"{o.Name} is mortgaged, so no rent is due."));
                FinishResolution();
                return;
            case OwnableTile o:
                int rent;
                if (o is UtilityTile && context.UtilityTenTimes)
                {
                    int a = RollDie(), b = RollDie();
                    Emit(new DiceRolledEvent(p, a, b));
                    rent = (a + b) * 10;
                }
                else
                {
                    rent = RentFor(o, context.DoubleRailroadRent);
                }
                Charge(p, o.Owner, rent, $"Rent for {o.Name}");
                ProcessDebts();
                return;
            case TaxTile t:
                Charge(p, null, t.Amount, t.Name);
                ProcessDebts();
                return;
        }

        switch (tile.Kind)
        {
            case TileKind.Chance:
                DrawCard(p, Chance);
                return;
            case TileKind.CommunityChest:
                DrawCard(p, CommunityChest);
                return;
            case TileKind.GoToJail:
                _rollAgain = false;
                SendToJail(p, "landed on Go To Jail");
                FinishResolution();
                return;
            default:
                FinishResolution();
                return;
        }
    }

    /// <summary>After a move is fully resolved: roll again on doubles, otherwise wait for End Turn.</summary>
    private void FinishResolution()
    {
        if (Phase == Phase.GameOver) return;
        if (CurrentPlayer.IsBankrupt)
        {
            BeginNextTurn();
            return;
        }
        Phase = _rollAgain && !CurrentPlayer.InJail ? Phase.AwaitRoll : Phase.PostRoll;
    }

    public int RentFor(OwnableTile tile, bool doubleRailroadRent = false)
    {
        var owner = tile.Owner;
        if (owner == null || tile.IsMortgaged) return 0;
        switch (tile)
        {
            case PropertyTile p:
                if (p.Houses > 0) return p.Rents[p.Houses];
                return OwnsFullGroup(owner, p.Group) ? p.Rents[0] * 2 : p.Rents[0];
            case RailroadTile:
                int railroads = CountOwned(owner, TileKind.Railroad);
                int rent = 25 << (railroads - 1);
                return doubleRailroadRent ? rent * 2 : rent;
            case UtilityTile:
                int total = LastRoll.Die1 + LastRoll.Die2;
                return total * (CountOwned(owner, TileKind.Utility) == 2 ? 10 : 4);
            default:
                return 0;
        }
    }

    public bool OwnsFullGroup(Player player, ColorGroup group)
    {
        foreach (var p in Board.PropertiesIn(group))
            if (p.Owner != player) return false;
        return true;
    }

    public int CountOwned(Player player, TileKind kind)
    {
        int n = 0;
        foreach (var o in player.Properties)
            if (o.Kind == kind) n++;
        return n;
    }

    // ======================================================================================= cards

    private Deck DeckOf(Card card) => card.Deck == DeckType.Chance ? Chance : CommunityChest;

    private void DrawCard(Player p, Deck deck)
    {
        var card = deck.Draw();
        Emit(new CardDrawnEvent(p, card));
        if (card.Action != CardAction.GetOutOfJailFree) deck.ReturnToBottom(card);

        switch (card.Action)
        {
            case CardAction.AdvanceTo:
                MoveBy(p, Board.Tiles.ForwardDistance(p.Position, Board.NodeAt(card.Value)));
                ResolveLanding(p, default);
                return;
            case CardAction.AdvanceToNearestRailroad:
            {
                Board.Tiles.FindNext(p.Position, t => t.Kind == TileKind.Railroad, out int distance);
                MoveBy(p, distance);
                ResolveLanding(p, new LandingContext(DoubleRailroadRent: true, UtilityTenTimes: false));
                return;
            }
            case CardAction.AdvanceToNearestUtility:
            {
                Board.Tiles.FindNext(p.Position, t => t.Kind == TileKind.Utility, out int distance);
                MoveBy(p, distance);
                ResolveLanding(p, new LandingContext(DoubleRailroadRent: false, UtilityTenTimes: true));
                return;
            }
            case CardAction.MoveBack:
                MoveBy(p, -card.Value);
                ResolveLanding(p, default);
                return;
            case CardAction.GoToJail:
                _rollAgain = false;
                SendToJail(p, "drew a Go to Jail card");
                FinishResolution();
                return;
            case CardAction.Collect:
                Transfer(null, p, card.Value, deck.Type == DeckType.Chance ? "Chance" : "Community Chest");
                FinishResolution();
                return;
            case CardAction.Pay:
                Charge(p, null, card.Value, deck.Type == DeckType.Chance ? "Chance" : "Community Chest");
                ProcessDebts();
                return;
            case CardAction.PayEachPlayer:
                foreach (var other in Players)
                    if (other != p && !other.IsBankrupt) Charge(p, other, card.Value, "Chairman of the Board");
                ProcessDebts();
                return;
            case CardAction.CollectFromEachPlayer:
                foreach (var other in Players)
                    if (other != p && !other.IsBankrupt) Charge(other, p, card.Value, "Birthday gift");
                ProcessDebts();
                return;
            case CardAction.Repairs:
                Charge(p, null, p.HouseCount * card.Value + p.HotelCount * card.Value2, "Property repairs");
                ProcessDebts();
                return;
            case CardAction.GetOutOfJailFree:
                p.JailCards.Add(card);
                FinishResolution();
                return;
        }
    }

    // ======================================================================================= buying & auctions

    public bool CanBuy => Phase == Phase.BuyDecision && _offer != null && CurrentPlayer.Cash >= _offer.Price;

    public void Buy()
    {
        Require(CanBuy, "buy");
        var tile = _offer!;
        _offer = null;
        UndoStack.Clear();
        Transfer(CurrentPlayer, null, tile.Price, $"Bought {tile.Name}");
        GiveProperty(tile, CurrentPlayer);
        Emit(new PropertyBoughtEvent(CurrentPlayer, tile, tile.Price));
        FinishResolution();
    }

    public bool CanDeclineBuy => Phase == Phase.BuyDecision;

    /// <summary>Declining sends the property to auction among all players (including the decliner).</summary>
    public void DeclineBuy()
    {
        Require(CanDeclineBuy, "decline");
        var tile = _offer!;
        _offer = null;
        UndoStack.Clear();

        var bidders = new ArrayQueue<Player>(GameSetup.SeatCount) { Tracer = DsLog, TraceName = "Auction Bidders (Queue)" };
        foreach (var p in TurnQueue)
            if (!p.IsBankrupt && p.Cash > 0) bidders.Enqueue(p);
        if (CurrentPlayer.Cash > 0) bidders.Enqueue(CurrentPlayer);

        CurrentAuction = new Auction(tile, bidders);
        Emit(new AuctionStartedEvent(tile, bidders.ToArray()));
        if (bidders.Count == 0)
        {
            EndAuction();
            return;
        }
        Phase = Phase.Auction;
    }

    public bool CanBid(int amount) =>
        Phase == Phase.Auction && amount > CurrentAuction!.HighBid && amount <= CurrentAuction.CurrentBidder.Cash;

    /// <summary>Front bidder raises: dequeue, record the bid, and go to the back of the queue.</summary>
    public void Bid(int amount)
    {
        Require(CanBid(amount), "bid " + amount);
        var auction = CurrentAuction!;
        var bidder = auction.Bidders.Dequeue();
        auction.HighBid = amount;
        auction.HighBidder = bidder;
        auction.Bidders.Enqueue(bidder);
        Emit(new AuctionBidEvent(bidder, amount));
        CheckAuctionOver();
    }

    public bool CanPassBid => Phase == Phase.Auction;

    /// <summary>Front bidder drops out of the auction (dequeued and not re-enqueued).</summary>
    public void PassBid()
    {
        Require(CanPassBid, "pass");
        var bidder = CurrentAuction!.Bidders.Dequeue();
        Emit(new AuctionPassEvent(bidder));
        CheckAuctionOver();
    }

    private void CheckAuctionOver()
    {
        var auction = CurrentAuction!;
        if (auction.Bidders.Count == 0 || (auction.Bidders.Count == 1 && auction.Bidders.Peek() == auction.HighBidder))
            EndAuction();
    }

    private void EndAuction()
    {
        var auction = CurrentAuction!;
        CurrentAuction = null;
        if (auction.HighBidder != null)
        {
            Transfer(auction.HighBidder, null, auction.HighBid, $"Won auction for {auction.Tile.Name}");
            GiveProperty(auction.Tile, auction.HighBidder);
        }
        Emit(new AuctionEndedEvent(auction.Tile, auction.HighBidder, auction.HighBid));
        FinishResolution();
    }

    private void GiveProperty(OwnableTile tile, Player player)
    {
        tile.Owner = player;
        player.Properties.Add(tile);
        Emit(new OwnershipChangedEvent(tile, player));
    }

    // ======================================================================================= building / mortgaging (undoable)

    /// <summary>True if <paramref name="player"/> is the one acting and the phase allows managing properties.</summary>
    public bool IsManaging(Player player) => CanManage(player, spending: false);

    private bool CanManage(Player player, bool spending) =>
        player == Actor && !player.IsBankrupt && Phase switch
        {
            Phase.AwaitRoll or Phase.InJail or Phase.BuyDecision or Phase.PostRoll => true,
            Phase.RaiseFunds => !spending,
            _ => false,
        };

    public bool CanBuild(PropertyTile tile)
    {
        var owner = tile.Owner;
        if (owner == null || !CanManage(owner, spending: true)) return false;
        if (tile.Houses >= 5 || owner.Cash < tile.HouseCost || !OwnsFullGroup(owner, tile.Group)) return false;
        foreach (var other in Board.PropertiesIn(tile.Group))
        {
            if (other.IsMortgaged) return false;
            if (other.Houses < tile.Houses) return false; // build evenly: only on the least-developed lots
        }
        return tile.Houses == 4 ? HotelsInBank > 0 : HousesInBank > 0;
    }

    public void Build(PropertyTile tile)
    {
        Require(CanBuild(tile), "build on " + tile.Name);
        Execute(new BuildCommand(tile));
    }

    public bool CanSellBuilding(PropertyTile tile)
    {
        var owner = tile.Owner;
        if (owner == null || tile.Houses == 0 || !CanManage(owner, spending: false)) return false;
        foreach (var other in Board.PropertiesIn(tile.Group))
            if (other.Houses > tile.Houses) return false; // sell evenly: only from the most-developed lots
        return !tile.HasHotel || HousesInBank >= 4;
    }

    public void SellBuilding(PropertyTile tile)
    {
        Require(CanSellBuilding(tile), "sell a building on " + tile.Name);
        Execute(new SellBuildingCommand(tile));
    }

    public bool CanMortgage(OwnableTile tile)
    {
        var owner = tile.Owner;
        if (owner == null || tile.IsMortgaged || !CanManage(owner, spending: false)) return false;
        if (tile is PropertyTile property)
            foreach (var other in Board.PropertiesIn(property.Group))
                if (other.Houses > 0) return false; // the whole color group must be free of buildings
        return true;
    }

    public void Mortgage(OwnableTile tile)
    {
        Require(CanMortgage(tile), "mortgage " + tile.Name);
        Execute(new MortgageCommand(tile));
    }

    public bool CanUnmortgage(OwnableTile tile)
    {
        var owner = tile.Owner;
        return owner != null && tile.IsMortgaged && CanManage(owner, spending: true) && owner.Cash >= tile.UnmortgageCost;
    }

    public void Unmortgage(OwnableTile tile)
    {
        Require(CanUnmortgage(tile), "unmortgage " + tile.Name);
        Execute(new UnmortgageCommand(tile));
    }

    public bool CanUndo => UndoStack.TryPeek(out var command) && command.CanUndo(this);

    /// <summary>Pops the most recent build/sell/mortgage action and reverses it.</summary>
    public void Undo()
    {
        Require(CanUndo, "undo");
        UndoStack.Pop().Undo(this);
    }

    private void Execute(IGameCommand command)
    {
        command.Execute(this);
        UndoStack.Push(command);
    }

    internal void ChangeBuildings(PropertyTile tile, int delta, int cashDelta, string reason)
    {
        var owner = tile.Owner!;
        int before = tile.Houses;
        int after = before + delta;
        if (delta > 0)
        {
            if (after == 5) { HotelsInBank--; HousesInBank += 4; } // 4 houses go back when the hotel goes up
            else HousesInBank--;
        }
        else
        {
            if (before == 5) { HotelsInBank++; HousesInBank -= 4; }
            else HousesInBank++;
        }
        tile.Houses = after;
        if (cashDelta < 0) Transfer(owner, null, -cashDelta, reason);
        else Transfer(null, owner, cashDelta, reason);
        Emit(new BuildingChangedEvent(owner, tile, after));
    }

    internal void SetMortgaged(OwnableTile tile, bool mortgaged, int cashDelta, string reason)
    {
        var owner = tile.Owner!;
        tile.IsMortgaged = mortgaged;
        if (cashDelta < 0) Transfer(owner, null, -cashDelta, reason);
        else Transfer(null, owner, cashDelta, reason);
        Emit(new MortgageChangedEvent(owner, tile, mortgaged));
    }

    // ======================================================================================= money, debts, bankruptcy

    /// <summary>Cash the player could have after selling every building and mortgaging everything.</summary>
    public int LiquidationValue(Player player)
    {
        int value = player.Cash;
        foreach (var o in player.Properties)
        {
            if (o is PropertyTile p) value += p.Houses * p.HouseCost / 2;
            if (!o.IsMortgaged) value += o.MortgageValue;
        }
        return value;
    }

    public bool CanPayDebt => Phase == Phase.RaiseFunds && _debts.Peek().Debtor.Cash >= _debts.Peek().Amount;

    public void PayDebt()
    {
        Require(CanPayDebt, "pay the debt");
        UndoStack.Clear();
        ProcessDebts();
    }

    public bool CanDeclareBankruptcy => Phase == Phase.RaiseFunds;

    public void DeclareBankruptcy()
    {
        Require(CanDeclareBankruptcy, "declare bankruptcy");
        UndoStack.Clear();
        var debt = _debts.Dequeue();
        GoBankrupt(debt.Debtor, debt.Creditor is { IsBankrupt: false } c ? c : null);
        if (Phase == Phase.GameOver) return;
        ProcessDebts();
    }

    private void Transfer(Player? from, Player? to, int amount, string reason)
    {
        if (amount <= 0) return;
        if (from != null) from.Cash -= amount;
        if (to != null) to.Cash += amount;
        Emit(new MoneyEvent(from, to, amount, reason));
    }

    private void Charge(Player debtor, Player? creditor, int amount, string reason)
    {
        if (amount > 0) _debts.Enqueue(new Debt(debtor, creditor, amount, reason));
    }

    /// <summary>
    /// Settles queued debts in FIFO order. Pays directly when cash allows; pauses in RaiseFunds when the
    /// debtor could cover it by selling/mortgaging; otherwise the debtor goes bankrupt to the creditor.
    /// When the queue is empty, runs the pending continuation (or finishes the move).
    /// </summary>
    private void ProcessDebts()
    {
        while (_debts.Count > 0)
        {
            var debt = _debts.Peek();
            if (debt.Debtor.IsBankrupt)
            {
                _debts.Dequeue();
                continue;
            }
            var creditor = debt.Creditor is { IsBankrupt: false } c ? c : null;
            if (debt.Debtor.Cash >= debt.Amount)
            {
                _debts.Dequeue();
                Transfer(debt.Debtor, creditor, debt.Amount, debt.Reason);
                continue;
            }
            if (LiquidationValue(debt.Debtor) >= debt.Amount)
            {
                Phase = Phase.RaiseFunds;
                Emit(new DebtEvent(debt.Debtor, creditor, debt.Amount, debt.Reason));
                return;
            }
            _debts.Dequeue();
            GoBankrupt(debt.Debtor, creditor);
            if (Phase == Phase.GameOver) return;
        }

        var next = _afterDebts;
        _afterDebts = null;
        if (next != null) next();
        else FinishResolution();
    }

    private void GoBankrupt(Player player, Player? creditor)
    {
        // Buildings are sold back to the bank at half price first.
        foreach (var o in player.Properties)
        {
            if (o is not PropertyTile { Houses: > 0 } p) continue;
            player.Cash += p.Houses * p.HouseCost / 2;
            if (p.HasHotel) HotelsInBank++;
            else HousesInBank += p.Houses;
            p.Houses = 0;
            Emit(new BuildingChangedEvent(player, p, 0));
        }

        Emit(new BankruptEvent(player, creditor));
        if (creditor != null)
        {
            Transfer(player, creditor, player.Cash, $"{player.Name}'s bankruptcy");
            foreach (var o in player.Properties)
            {
                o.Owner = creditor;
                creditor.Properties.Add(o);
                Emit(new OwnershipChangedEvent(o, creditor));
            }
            foreach (var card in player.JailCards) creditor.JailCards.Add(card);
        }
        else
        {
            foreach (var o in player.Properties)
            {
                o.Owner = null;
                o.IsMortgaged = false;
                Emit(new OwnershipChangedEvent(o, null));
            }
            foreach (var card in player.JailCards) DeckOf(card).ReturnToBottom(card);
        }

        player.Cash = 0;
        player.Properties.Clear();
        player.JailCards.Clear();
        player.InJail = false;
        player.IsBankrupt = true;
        player.BankruptOnTurn = TurnNumber;
        TurnQueue.RemoveAll(p => p == player);
        if (player == CurrentPlayer) _afterDebts = null;
        CheckGameOver();
    }

    private bool CheckGameOver()
    {
        if (Phase == Phase.GameOver) return true;
        if (ActivePlayerCount > 1) return false;
        EndGame("Last player standing");
        return true;
    }

    private void EndGame(string reason)
    {
        var ranking = Standings(out _);
        Winner = ranking[0];
        FinalRanking = ranking;
        Phase = Phase.GameOver;
        CurrentAuction = null;
        _offer = null;
        _debts.Clear();
        _afterDebts = null;
        Emit(new GameOverEvent(Winner, ranking, reason));
    }

    // ======================================================================================= sorted / searched views

    /// <summary>Active players by net worth (desc), then bankrupt players (latest first).</summary>
    public Player[] Standings(out SortStats stats, SortAlgorithm algorithm = SortAlgorithm.MergeSort)
    {
        var buffer = new DynamicArray<Player>(Players);
        stats = Sorting.Sort(buffer.AsSpan(), CompareStanding, algorithm);
        DsLog.Record("Leaderboard", algorithm.ToString(), $"{Players.Length} players by net worth", (int)stats.Comparisons);
        return buffer.ToArray();
    }

    private static int CompareStanding(Player a, Player b)
    {
        if (a.IsBankrupt != b.IsBankrupt) return a.IsBankrupt ? 1 : -1;
        if (a.IsBankrupt) return b.BankruptOnTurn.CompareTo(a.BankruptOnTurn);
        return b.NetWorth.CompareTo(a.NetWorth);
    }

    public OwnableTile[] Portfolio(Player player, PortfolioOrder order, SortAlgorithm algorithm, out SortStats stats)
    {
        var buffer = new DynamicArray<OwnableTile>(player.Properties.AsSpan());
        Comparison<OwnableTile> comparison = order switch
        {
            PortfolioOrder.Price => (a, b) => a.Price != b.Price ? a.Price.CompareTo(b.Price) : a.Index.CompareTo(b.Index),
            PortfolioOrder.Name => CompareByName,
            PortfolioOrder.Group => (a, b) => GroupKey(a) != GroupKey(b) ? GroupKey(a).CompareTo(GroupKey(b)) : a.Index.CompareTo(b.Index),
            PortfolioOrder.Rent => (a, b) => RentFor(b).CompareTo(RentFor(a)),
            _ => (a, b) => a.Index.CompareTo(b.Index),
        };
        stats = Sorting.Sort(buffer.AsSpan(), comparison, algorithm);
        DsLog.Record("Portfolio", algorithm.ToString(), $"{player.Name} by {order}", (int)stats.Comparisons);
        return buffer.ToArray();
    }

    /// <summary>Prefix search: sort by name, binary-search the lower bound, then scan the matching block.</summary>
    public OwnableTile[] SearchPortfolio(Player player, string prefix, out int comparisons)
    {
        var sorted = new DynamicArray<OwnableTile>(player.Properties.AsSpan());
        Sorting.MergeSort(sorted.AsSpan(), CompareByName);
        if (string.IsNullOrWhiteSpace(prefix))
        {
            comparisons = 0;
            return sorted.ToArray();
        }

        prefix = prefix.Trim();
        var bound = Searching.LowerBound<OwnableTile, string>(sorted.AsSpan(), prefix, ComparePrefix);
        comparisons = bound.Comparisons;
        var matches = new DynamicArray<OwnableTile>();
        for (int i = bound.Index; i < sorted.Count; i++)
        {
            comparisons++;
            if (ComparePrefix(sorted[i], prefix) != 0) break;
            matches.Add(sorted[i]);
        }
        DsLog.Record("Portfolio Search", "Binary search (lower bound)", $"\"{prefix}\" → {matches.Count} match(es)", comparisons);
        return matches.ToArray();
    }

    private static int CompareByName(OwnableTile a, OwnableTile b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);

    private static int ComparePrefix(OwnableTile tile, string prefix) =>
        string.Compare(tile.Name, 0, prefix, 0, prefix.Length, StringComparison.OrdinalIgnoreCase);

    private static int GroupKey(OwnableTile tile) => tile switch
    {
        PropertyTile p => (int)p.Group,
        RailroadTile => 100,
        _ => 200,
    };

    // ======================================================================================= helpers

    private int RollDie() => _dice.Next(1, 7);

    private void Emit(GameEvent gameEvent) => Events.Enqueue(gameEvent);

    private void Require(bool condition, string action)
    {
        if (!condition) throw new InvalidOperationException($"Cannot {action} now (phase {Phase}, actor {Actor.Name}).");
    }

    // Test hooks ------------------------------------------------------------------------------

    internal void ForceGive(OwnableTile tile, Player player) => GiveProperty(tile, player);

    internal void ForcePosition(Player player, int index) => player.Position = Board.NodeAt(index);

    internal void ForceJail(Player player)
    {
        player.Position = Board.JailNode;
        player.InJail = true;
        player.JailTurns = 0;
        if (player == CurrentPlayer && Phase == Phase.AwaitRoll) Phase = Phase.InJail;
    }
}
