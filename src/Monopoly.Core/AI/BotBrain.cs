using System;
using Monopoly.Core.DataStructures;
using Monopoly.Core.Model;
using Monopoly.Core.Rules;

namespace Monopoly.Core.AI;

/// <summary>
/// Plays for whichever bot is the game's current <see cref="Game.Actor"/>, one atomic action per call
/// so the presentation layer can animate each decision. Strategy:
/// buy when it keeps a cash reserve (or completes/blocks a color set), value property by Markov landing
/// odds, build with a max-heap of rent gain per dollar, and raise cash with a min-heap of least-painful losses.
/// </summary>
public sealed class BotBrain
{
    private readonly Game _game;

    public BotBrain(Game game)
    {
        _game = game;
    }

    /// <summary>Performs one action for the current actor and returns a short description of it.</summary>
    public string Act()
    {
        var me = _game.Actor;
        switch (_game.Phase)
        {
            case Phase.InJail:
                if (_game.CanUseJailCard && EarlyGame())
                {
                    _game.UseJailCard();
                    return "uses a Get Out of Jail Free card";
                }
                if (_game.CanPayJailFine && EarlyGame() && me.Cash - _game.Setup.JailFine >= Reserve(me))
                {
                    _game.PayJailFine();
                    return "pays the jail fine";
                }
                _game.Roll();
                return "rolls for doubles";

            case Phase.AwaitRoll:
                if (TryUnmortgage(me)) return "unmortgages a property";
                _game.Roll();
                return "rolls";

            case Phase.BuyDecision:
                var offer = _game.PendingPurchase!;
                if (_game.CanBuy && ShouldBuy(me, offer))
                {
                    _game.Buy();
                    return "buys " + offer.Name;
                }
                _game.DeclineBuy();
                return "declines " + offer.Name;

            case Phase.Auction:
                var auction = _game.CurrentAuction!;
                int bid = auction.HighBid + BidIncrement(auction.Tile);
                if (bid <= MaxBid(me, auction.Tile) && _game.CanBid(bid))
                {
                    _game.Bid(bid);
                    return $"bids ${bid}";
                }
                _game.PassBid();
                return "passes";

            case Phase.RaiseFunds:
                if (_game.CanPayDebt)
                {
                    _game.PayDebt();
                    return "pays the debt";
                }
                if (TryRaiseCash(me)) return "raises cash";
                _game.DeclareBankruptcy();
                return "declares bankruptcy";

            case Phase.PostRoll:
                if (TryBuild(me)) return "builds";
                if (TryUnmortgage(me)) return "unmortgages a property";
                _game.EndTurn();
                return "ends the turn";

            default:
                throw new InvalidOperationException($"Bot cannot act in phase {_game.Phase}.");
        }
    }

    // ------------------------------------------------------------------ valuation

    private bool EarlyGame()
    {
        int unowned = 0;
        foreach (var o in _game.Board.Ownables)
            if (o.Owner == null) unowned++;
        return unowned > 8;
    }

    /// <summary>Cash to keep for rent: grows with the most dangerous rent on the board.</summary>
    private int Reserve(Player me)
    {
        int worstRent = 0;
        foreach (var o in _game.Board.Ownables)
            if (o.Owner != null && o.Owner != me) worstRent = Math.Max(worstRent, _game.RentFor(o));
        return Math.Clamp(100 + worstRent / 2, 100, 600);
    }

    private double Odds(Tile tile) => _game.LandingOdds[tile.Index] * _game.Board.Count; // 1.0 = an average tile

    private bool CompletesSetFor(Player player, OwnableTile tile)
    {
        if (tile is not PropertyTile p) return false;
        foreach (var other in _game.Board.PropertiesIn(p.Group))
            if (other != p && other.Owner != player) return false;
        return true;
    }

    private bool BlocksOpponent(Player me, OwnableTile tile)
    {
        if (tile is not PropertyTile p) return false;
        foreach (var opponent in _game.Players)
            if (opponent != me && !opponent.IsBankrupt && CompletesSetFor(opponent, p)) return true;
        return false;
    }

    private bool ShouldBuy(Player me, OwnableTile tile)
    {
        int left = me.Cash - tile.Price;
        if (CompletesSetFor(me, tile)) return left >= 50;
        if (BlocksOpponent(me, tile)) return left >= Math.Min(Reserve(me), 200);
        // Early on, land is cheap relative to the danger on the board: grab it.
        if (EarlyGame()) return left >= 75;
        return left >= Reserve(me);
    }

    private int MaxBid(Player me, OwnableTile tile)
    {
        double factor = 0.9 + 0.3 * (Odds(tile) - 1.0);
        if (tile is UtilityTile) factor = 0.75;
        if (CompletesSetFor(me, tile)) factor = 1.6;
        else if (BlocksOpponent(me, tile)) factor = Math.Max(factor, 1.1);
        int cap = me.Cash - Math.Min(Reserve(me), 150);
        return Math.Min((int)(tile.Price * factor), cap);
    }

    private static int BidIncrement(OwnableTile tile) => tile.Price >= 200 ? 20 : 10;

    // ------------------------------------------------------------------ building (max-heap)

    private readonly record struct BuildOption(PropertyTile Tile, double Score)
    {
        public override string ToString() => $"{Tile.Name} ({Score:0.00})";
    }

    private bool TryBuild(Player me)
    {
        var heap = BinaryHeap<BuildOption>.MaxHeap((a, b) => a.Score.CompareTo(b.Score));
        foreach (var o in me.Properties)
        {
            if (o is not PropertyTile p || !_game.CanBuild(p)) continue;
            // Houses are how games are won: build up to 3 aggressively, keep a fuller reserve beyond that.
            int keep = p.Houses < 3 ? Math.Min(Reserve(me), 150) : Reserve(me);
            if (me.Cash - p.HouseCost < keep) continue;
            double rentGain = p.Rents[p.Houses + 1] - _game.RentFor(p);
            heap.Push(new BuildOption(p, Odds(p) * rentGain / p.HouseCost));
        }
        if (heap.IsEmpty) return false;
        var best = heap.Pop();
        _game.DsLog.Record("Bot Build Plan (Max-Heap)", "Pop max", best.ToString(), heap.Count + 1);
        _game.Build(best.Tile);
        return true;
    }

    private bool TryUnmortgage(Player me)
    {
        OwnableTile? best = null;
        foreach (var o in me.Properties)
        {
            if (!_game.CanUnmortgage(o) || me.Cash - o.UnmortgageCost < Reserve(me) * 2) continue;
            if (best == null || Odds(o) > Odds(best)) best = o;
        }
        if (best == null) return false;
        _game.Unmortgage(best);
        return true;
    }

    // ------------------------------------------------------------------ raising cash (min-heap)

    private readonly record struct CashOption(OwnableTile Tile, bool SellBuilding, double Pain)
    {
        public override string ToString() => (SellBuilding ? "sell on " : "mortgage ") + Tile.Name;
    }

    private bool TryRaiseCash(Player me)
    {
        var heap = BinaryHeap<CashOption>.MinHeap((a, b) => a.Pain.CompareTo(b.Pain));
        foreach (var o in me.Properties)
        {
            if (o is PropertyTile p && _game.CanSellBuilding(p))
                heap.Push(new CashOption(p, true, 10 + Odds(p) * p.Houses));
            if (_game.CanMortgage(o))
            {
                double pain = Odds(o) * (o is PropertyTile pt && _game.OwnsFullGroup(me, pt.Group) ? 4 : 1);
                heap.Push(new CashOption(o, false, pain));
            }
        }
        if (heap.IsEmpty) return false;
        var choice = heap.Pop();
        _game.DsLog.Record("Bot Cash Plan (Min-Heap)", "Pop min", choice.ToString(), heap.Count + 1);
        if (choice.SellBuilding) _game.SellBuilding((PropertyTile)choice.Tile);
        else _game.Mortgage(choice.Tile);
        return true;
    }
}
