using System;
using Monopoly.Core.Events;
using Monopoly.Core.Model;
using Monopoly.Core.Rules;

namespace Monopoly.Core.Tests;

public static class RulesTests
{
    /// <summary>Opening roll-off that keeps seat order (seat 0 rolls 12, the rest tie on 3).</summary>
    private static readonly int[] SeatOrderRolls = { 6, 6, 1, 2, 1, 2, 1, 2 };

    private static Game NewGame(params int[] dice)
    {
        var script = new int[SeatOrderRolls.Length + dice.Length];
        SeatOrderRolls.CopyTo(script, 0);
        dice.CopyTo(script, SeatOrderRolls.Length);
        var game = new Game(GameSetup.ForHumans(4, seed: 77), dice: new ScriptedDice(script));
        game.Start();
        return game;
    }

    private static T Tile<T>(Game g, int index) where T : Tile => (T)g.Board.NodeAt(index).Value;

    private static PropertyTile Prop(Game g, int index) => Tile<PropertyTile>(g, index);

    /// <summary>Plays the current turn without buying anything, then ends it.</summary>
    private static void FinishTurnPassively(Game g)
    {
        for (int guard = 0; g.Phase != Phase.PostRoll; guard++)
        {
            if (guard > 100) throw new Exception("turn did not finish");
            switch (g.Phase)
            {
                case Phase.AwaitRoll or Phase.InJail: g.Roll(); break;
                case Phase.BuyDecision: g.DeclineBuy(); break;
                case Phase.Auction: g.PassBid(); break;
                case Phase.RaiseFunds: g.DeclareBankruptcy(); break;
                default: throw new Exception("unexpected phase " + g.Phase);
            }
        }
        g.EndTurn();
    }

    // ------------------------------------------------------------------ setup & board

    [Test]
    public static void Setup_OneHuman_FillsThreeBotSeats()
    {
        var setup = GameSetup.ForHumans(1);
        Assert.False(setup.Seats[0].IsBot);
        for (int i = 1; i < 4; i++) Assert.True(setup.Seats[i].IsBot, $"seat {i} is a bot");

        var two = GameSetup.ForHumans(2);
        Assert.False(two.Seats[1].IsBot);
        Assert.True(two.Seats[2].IsBot && two.Seats[3].IsBot);

        var four = GameSetup.ForHumans(4);
        foreach (var seat in four.Seats) Assert.False(seat.IsBot);
        Assert.Throws<ArgumentOutOfRangeException>(() => GameSetup.ForHumans(0));
    }

    [Test]
    public static void Board_HasClassicLayout()
    {
        var board = Board.LoadClassic();
        Assert.Equal(40, board.Count);
        Assert.Equal(28, board.Ownables.Count);
        Assert.Equal(8, board.Groups.Count);
        Assert.Equal("GO", board.GoNode.Value.Name);
        Assert.Equal(10, board.JailNode.Index);
        Assert.Equal(39, board.ByName.Get("boardwalk").Index);
        Assert.Equal(2, board.PropertiesIn(ColorGroup.DarkBlue).Count);
        Assert.Equal(3, board.PropertiesIn(ColorGroup.Orange).Count);
        var (chance, chest) = CardLibrary.LoadClassic();
        Assert.Equal(16, chance.Length);
        Assert.Equal(16, chest.Length);
    }

    [Test]
    public static void TurnOrder_HighestOpeningRollFirst_TiesKeepSeatOrder()
    {
        var game = new Game(GameSetup.ForHumans(4, seed: 1), dice: new ScriptedDice(1, 1, 6, 5, 3, 3, 6, 5));
        game.Start();
        Assert.Equal(1, game.CurrentPlayer.Id);
        var waiting = game.TurnQueue.ToArray();
        Assert.Equal(3, waiting[0].Id);
        Assert.Equal(2, waiting[1].Id);
        Assert.Equal(0, waiting[2].Id);
    }

    // ------------------------------------------------------------------ moving, buying, rent

    [Test]
    public static void PassingGo_PaysSalary()
    {
        var g = NewGame(2, 3);
        var p = g.CurrentPlayer;
        g.ForcePosition(p, 38);
        g.Roll();
        Assert.Equal(3, p.Position.Index);
        Assert.Equal(1700, p.Cash);
        Assert.Equal(Phase.BuyDecision, g.Phase);
    }

    [Test]
    public static void Buy_TransfersOwnershipAndCash()
    {
        var g = NewGame(1, 2);
        var p = g.CurrentPlayer;
        g.Roll(); // GO + 3 = Baltic Avenue
        Assert.True(g.CanBuy);
        g.Buy();
        var baltic = Prop(g, 3);
        Assert.True(baltic.Owner == p);
        Assert.Equal(1440, p.Cash);
        Assert.True(p.Properties.Contains(baltic));
        Assert.Equal(Phase.PostRoll, g.Phase);
    }

    [Test]
    public static void Rent_DoublesForFullSet_ThenFollowsHouses()
    {
        var g = NewGame();
        var owner = g.Players[1];
        var med = Prop(g, 1);
        var baltic = Prop(g, 3);
        g.ForceGive(baltic, owner);
        Assert.Equal(4, g.RentFor(baltic));
        g.ForceGive(med, owner);
        Assert.Equal(8, g.RentFor(baltic));
        baltic.Houses = 3;
        Assert.Equal(180, g.RentFor(baltic));
        baltic.Houses = 5;
        Assert.Equal(450, g.RentFor(baltic));
        baltic.IsMortgaged = true;
        Assert.Equal(0, g.RentFor(baltic));
    }

    [Test]
    public static void Railroads_Rent25_50_100_200()
    {
        var g = NewGame();
        var owner = g.Players[2];
        int[] expected = { 25, 50, 100, 200 };
        int[] railroads = { 5, 15, 25, 35 };
        for (int i = 0; i < 4; i++)
        {
            g.ForceGive(Tile<RailroadTile>(g, railroads[i]), owner);
            Assert.Equal(expected[i], g.RentFor(Tile<RailroadTile>(g, 5)));
        }
        Assert.Equal(400, g.RentFor(Tile<RailroadTile>(g, 5), doubleRailroadRent: true));
    }

    [Test]
    public static void Utility_Rent4xOr10xTheRoll()
    {
        var g = NewGame(3, 4);
        var p = g.CurrentPlayer;
        var owner = g.Players[1];
        g.ForceGive(Tile<UtilityTile>(g, 12), owner);
        g.ForcePosition(p, 5);
        g.Roll(); // lands on Electric Company with 7
        Assert.Equal(1500 - 28, p.Cash);
        Assert.Equal(1500 + 28, owner.Cash);
        g.ForceGive(Tile<UtilityTile>(g, 28), owner);
        Assert.Equal(70, g.RentFor(Tile<UtilityTile>(g, 12)));
    }

    [Test]
    public static void Rent_IsPaidToOwner()
    {
        var g = NewGame(1, 2);
        var p = g.CurrentPlayer;
        var owner = g.Players[3];
        g.ForceGive(Prop(g, 3), owner);
        g.Roll();
        Assert.Equal(1496, p.Cash);
        Assert.Equal(1504, owner.Cash);
        Assert.Equal(Phase.PostRoll, g.Phase);
    }

    // ------------------------------------------------------------------ auctions (queue)

    [Test]
    public static void Auction_QueueRotatesUntilOneBidderLeft()
    {
        var g = NewGame(1, 2);
        g.Roll();
        g.DeclineBuy();
        Assert.Equal(Phase.Auction, g.Phase);
        var auction = g.CurrentAuction!;
        Assert.Equal(4, auction.Bidders.Count);
        Assert.Equal(1, g.Actor.Id);

        g.Bid(10);  // seat 1 → back of the queue
        g.Bid(30);  // seat 2
        Assert.False(g.CanBid(30), "must outbid the high bid");
        g.PassBid(); // seat 3 drops out
        g.PassBid(); // seat 0 drops out
        Assert.Equal(1, g.Actor.Id);
        g.PassBid(); // seat 1 drops out: only the high bidder (seat 2) remains

        var baltic = Prop(g, 3);
        Assert.True(baltic.Owner == g.Players[2]);
        Assert.Equal(1470, g.Players[2].Cash);
        Assert.True(g.CurrentAuction == null);
        Assert.Equal(Phase.PostRoll, g.Phase);
    }

    [Test]
    public static void Auction_EveryonePasses_PropertyStaysWithBank()
    {
        var g = NewGame(1, 2);
        g.Roll();
        g.DeclineBuy();
        for (int i = 0; i < 4; i++) g.PassBid();
        Assert.True(Prop(g, 3).Owner == null);
        Assert.Equal(Phase.PostRoll, g.Phase);
    }

    // ------------------------------------------------------------------ building, mortgage, undo (stack)

    private static Game GameWithLightBlues(out Player p)
    {
        var g = NewGame();
        p = g.CurrentPlayer;
        foreach (int i in new[] { 6, 8, 9 }) g.ForceGive(Prop(g, i), p);
        return g;
    }

    [Test]
    public static void Build_MustBeEven_HotelReturnsFourHouses()
    {
        var g = GameWithLightBlues(out var p);
        var oriental = Prop(g, 6);
        var vermont = Prop(g, 8);
        var connecticut = Prop(g, 9);

        Assert.True(g.CanBuild(oriental));
        g.Build(oriental);
        Assert.False(g.CanBuild(oriental), "cannot build a 2nd house before the others have one");
        g.Build(vermont);
        g.Build(connecticut);
        for (int level = 2; level <= 4; level++)
            foreach (var t in new[] { oriental, vermont, connecticut }) g.Build(t);

        Assert.Equal(32 - 12, g.HousesInBank);
        g.Build(oriental); // hotel
        Assert.True(oriental.HasHotel);
        Assert.Equal(32 - 12 + 4, g.HousesInBank);
        Assert.Equal(11, g.HotelsInBank);
        Assert.Equal(1500 - 13 * 50, p.Cash);

        Assert.False(g.CanSellBuilding(vermont), "sell evenly: the hotel lot goes first");
        Assert.True(g.CanSellBuilding(oriental));
    }

    [Test]
    public static void Build_RequiresFullSet()
    {
        var g = NewGame();
        var p = g.CurrentPlayer;
        g.ForceGive(Prop(g, 1), p);
        Assert.False(g.CanBuild(Prop(g, 1)));
        g.ForceGive(Prop(g, 3), g.Players[1]);
        Assert.False(g.CanBuild(Prop(g, 1)));
    }

    [Test]
    public static void Mortgage_BlockedByBuildings_UnmortgageCostsTenPercent()
    {
        var g = GameWithLightBlues(out var p);
        var oriental = Prop(g, 6);
        var vermont = Prop(g, 8);
        g.Build(oriental);
        Assert.False(g.CanMortgage(vermont), "group has a building");
        g.SellBuilding(oriental);
        Assert.True(g.CanMortgage(vermont));
        g.Mortgage(vermont);
        Assert.True(vermont.IsMortgaged);
        Assert.False(g.CanBuild(oriental), "cannot build while a group lot is mortgaged");
        int before = p.Cash;
        g.Unmortgage(vermont);
        Assert.Equal(before - 55, p.Cash);
        Assert.False(vermont.IsMortgaged);
    }

    [Test]
    public static void Undo_RevertsBuildAndMortgage()
    {
        var g = NewGame(1, 2);
        var p = g.CurrentPlayer;
        g.ForceGive(Prop(g, 37), p);
        g.ForceGive(Prop(g, 39), p);
        g.ForceGive(Tile<RailroadTile>(g, 5), p);

        g.Build(Prop(g, 37));
        g.Mortgage(Tile<RailroadTile>(g, 5));
        Assert.Equal(2, g.UndoStack.Count);
        Assert.Equal(1500 - 200 + 100, p.Cash);

        g.Undo(); // un-mortgage first (LIFO)
        Assert.False(Tile<RailroadTile>(g, 5).IsMortgaged);
        g.Undo();
        Assert.Equal(0, Prop(g, 37).Houses);
        Assert.Equal(1500, p.Cash);
        Assert.Equal(32, g.HousesInBank);
        Assert.False(g.CanUndo);

        g.Build(Prop(g, 39));
        g.Roll(); // rolling commits the turn's actions
        Assert.Equal(0, g.UndoStack.Count);
    }

    // ------------------------------------------------------------------ doubles & jail

    [Test]
    public static void ThirdDoubles_GoesStraightToJail()
    {
        var g = NewGame(5, 5, 5, 5, 3, 3);
        var p = g.CurrentPlayer;
        g.Roll();
        Assert.Equal(10, p.Position.Index);
        Assert.Equal(Phase.AwaitRoll, g.Phase, "doubles roll again");
        g.Roll();
        Assert.Equal(20, p.Position.Index);
        g.Roll();
        Assert.True(p.InJail);
        Assert.Equal(10, p.Position.Index);
        Assert.Equal(Phase.PostRoll, g.Phase);
        Assert.Equal(1500, p.Cash, "no GO salary on the way to jail");
    }

    [Test]
    public static void GoToJailTile_SendsToJail_AndJailTurnUsesOptions()
    {
        var g = NewGame(3, 4);
        var p = g.CurrentPlayer;
        g.ForcePosition(p, 23);
        g.Roll(); // 23 + 7 = 30 Go To Jail
        Assert.True(p.InJail);
        Assert.Equal(10, p.Position.Index);
        g.EndTurn();
        while (g.CurrentPlayer != p) FinishTurnPassively(g);
        Assert.Equal(Phase.InJail, g.Phase);
        Assert.True(g.CanPayJailFine);
        Assert.False(g.CanUseJailCard);
        g.PayJailFine();
        Assert.False(p.InJail);
        Assert.Equal(Phase.AwaitRoll, g.Phase);
    }

    [Test]
    public static void Jail_ThirdFailedRoll_PaysFineThenMoves()
    {
        var setup = new GameSetup
        {
            Seats = new[] { new SeatConfig("A", 0, false), new SeatConfig("B", 1, false) },
            Seed = 3,
        };
        // Opening: A 12, B 3. Then A, B alternate failed jail rolls (1,2).
        var g = new Game(setup, dice: new ScriptedDice(6, 6, 1, 2, 1, 2, 1, 2, 1, 2, 1, 2, 1, 2));
        g.Start();
        var a = g.Players[0];
        var b = g.Players[1];
        g.ForceJail(a);
        g.ForceJail(b);

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            Assert.True(g.CurrentPlayer == a);
            g.Roll();
            Assert.True(a.InJail);
            Assert.Equal(attempt, a.JailTurns);
            g.EndTurn();
            g.Roll(); // B fails too
            g.EndTurn();
        }
        g.Roll(); // A's third failure: pay $50, move 3 to States Avenue
        Assert.False(a.InJail);
        Assert.Equal(13, a.Position.Index);
        Assert.Equal(1450, a.Cash);
        Assert.Equal(Phase.BuyDecision, g.Phase);
    }

    // ------------------------------------------------------------------ debts & bankruptcy

    [Test]
    public static void RaiseFunds_MortgageThenPay()
    {
        var g = NewGame(1, 3);
        var p = g.CurrentPlayer;
        p.Cash = 50;
        var boardwalk = Prop(g, 39);
        g.ForceGive(boardwalk, p);
        g.Roll(); // Income Tax $200
        Assert.Equal(Phase.RaiseFunds, g.Phase);
        Assert.True(g.Actor == p);
        Assert.False(g.CanPayDebt);
        Assert.False(g.CanBuild(boardwalk));
        g.Mortgage(boardwalk);
        Assert.True(g.CanPayDebt);
        g.PayDebt();
        Assert.Equal(50, p.Cash);
        Assert.Equal(Phase.PostRoll, g.Phase);
    }

    [Test]
    public static void Bankruptcy_TransfersEverythingToCreditor()
    {
        var g = NewGame(1, 2);
        var p = g.CurrentPlayer;
        var creditor = g.Players[1];
        p.Cash = 5;
        g.ForceGive(Prop(g, 1), p);
        var boardwalk = Prop(g, 39);
        g.ForceGive(boardwalk, creditor);
        g.ForceGive(Prop(g, 37), creditor);
        boardwalk.Houses = 5;
        g.ForcePosition(p, 36);
        g.Roll(); // Boardwalk with a hotel: $2000

        Assert.True(p.IsBankrupt);
        Assert.True(Prop(g, 1).Owner == creditor);
        Assert.Equal(1505, creditor.Cash);
        Assert.Equal(0, p.Properties.Count);
        Assert.Equal(3, g.ActivePlayerCount);
        Assert.True(g.CurrentPlayer != p, "turn passed on");
        foreach (var waiting in g.TurnQueue) Assert.True(waiting != p);
    }

    [Test]
    public static void LastPlayerStanding_Wins()
    {
        var setup = new GameSetup
        {
            Seats = new[] { new SeatConfig("A", 0, false), new SeatConfig("B", 1, false) },
            Seed = 3,
        };
        var g = new Game(setup, dice: new ScriptedDice(6, 6, 1, 2, 1, 2));
        g.Start();
        var a = g.Players[0];
        var b = g.Players[1];
        a.Cash = 1;
        g.ForceGive(Prop(g, 3), b);
        g.Roll();
        Assert.Equal(Phase.GameOver, g.Phase);
        Assert.True(g.Winner == b);
        Assert.True(g.FinalRanking![1] == a);
    }

    // ------------------------------------------------------------------ cards

    [Test]
    public static void Card_NearestRailroad_PaysDoubleRent()
    {
        var g = NewGame(1, 3);
        var p = g.CurrentPlayer;
        var owner = g.Players[2];
        g.ForceGive(Tile<RailroadTile>(g, 15), owner);
        g.Chance.MoveToTop(c => c.Action == CardAction.AdvanceToNearestRailroad);
        g.ForcePosition(p, 3);
        g.Roll(); // 7 = Chance → nearest railroad is 15
        Assert.Equal(15, p.Position.Index);
        Assert.Equal(1450, p.Cash);
        Assert.Equal(1550, owner.Cash);
    }

    [Test]
    public static void Card_GoBackThree_ChainsIntoCommunityChest()
    {
        var g = NewGame(1, 3);
        var p = g.CurrentPlayer;
        g.Chance.MoveToTop(c => c.Action == CardAction.MoveBack);
        g.CommunityChest.MoveToTop(c => c.Text.StartsWith("Bank error"));
        g.ForcePosition(p, 32);
        g.Roll(); // 36 Chance → back to 33 Community Chest → +$200
        Assert.Equal(33, p.Position.Index);
        Assert.Equal(1700, p.Cash);
        Assert.Equal(Phase.PostRoll, g.Phase);
    }

    [Test]
    public static void Card_Birthday_CollectsFromEveryOtherPlayer()
    {
        var g = NewGame(1, 1); // doubles: lands on Community Chest (2)
        var p = g.CurrentPlayer;
        g.CommunityChest.MoveToTop(c => c.Action == CardAction.CollectFromEachPlayer);
        g.Roll();
        Assert.Equal(1530, p.Cash);
        foreach (var other in g.Players)
            if (other != p) Assert.Equal(1490, other.Cash);
        Assert.Equal(Phase.AwaitRoll, g.Phase, "doubles still roll again");
    }

    [Test]
    public static void Card_GetOutOfJailFree_IsKeptThenReturnedToDeck()
    {
        var g = NewGame(1, 1);
        var p = g.CurrentPlayer;
        g.CommunityChest.MoveToTop(c => c.Action == CardAction.GetOutOfJailFree);
        g.Roll();
        Assert.Equal(1, p.JailCards.Count);
        Assert.Equal(15, g.CommunityChest.Count);
    }

    // ------------------------------------------------------------------ sorted & searched views

    [Test]
    public static void Standings_SortByNetWorth_BankruptLast()
    {
        var g = NewGame();
        g.Players[0].Cash = 100;
        g.Players[1].Cash = 900;
        g.Players[2].Cash = 500;
        g.Players[3].IsBankrupt = true;
        g.Players[3].Cash = 0;
        g.ForceGive(Prop(g, 39), g.Players[2]); // +400 net worth → 900, ties with seat 1
        var ranking = g.Standings(out var stats);
        Assert.Equal(1, ranking[0].Id, "stable merge sort keeps seat order on ties");
        Assert.Equal(2, ranking[1].Id);
        Assert.Equal(0, ranking[2].Id);
        Assert.Equal(3, ranking[3].Id);
        Assert.True(stats.Comparisons > 0);
    }

    [Test]
    public static void Portfolio_SortsAndPrefixSearches()
    {
        var g = NewGame();
        var p = g.CurrentPlayer;
        foreach (int i in new[] { 39, 1, 11, 16, 5, 37, 12 }) g.ForceGive(g.Board.NodeAt(i).Value as OwnableTile ?? throw new Exception(), p);

        var byPrice = g.Portfolio(p, PortfolioOrder.Price, Algorithms.SortAlgorithm.QuickSort, out _);
        Assert.Equal("Mediterranean Avenue", byPrice[0].Name);
        Assert.Equal("Boardwalk", byPrice[^1].Name);

        var hits = g.SearchPortfolio(p, "st.", out int comparisons);
        Assert.Equal(2, hits.Length);
        Assert.Equal("St. Charles Place", hits[0].Name);
        Assert.Equal("St. James Place", hits[1].Name);
        Assert.True(comparisons <= 6);

        Assert.Equal(0, g.SearchPortfolio(p, "zzz", out _).Length);
        Assert.Equal(7, g.SearchPortfolio(p, "", out _).Length);
    }

    [Test]
    public static void Events_AreQueuedInOrder()
    {
        var g = NewGame(1, 2);
        g.Events.Clear();
        g.Roll();
        Assert.True(g.Events.Dequeue() is DiceRolledEvent { Total: 3 });
        Assert.True(g.Events.Dequeue() is MovedEvent { Path.Length: 3 });
        Assert.True(g.Events.Dequeue() is PurchaseOfferedEvent);
    }
}
