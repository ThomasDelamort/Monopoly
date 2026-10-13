using System;
using Monopoly.Core.AI;
using Monopoly.Core.Model;
using Monopoly.Core.Rules;

namespace Monopoly.Core.Tests;

public static class SimulationTests
{
    [Test]
    public static void Simulation_500BotGames_KeepInvariants()
    {
        int bankruptcyFinishes = 0, roundLimitFinishes = 0;
        long totalActions = 0, totalTurns = 0;

        for (ulong seed = 1; seed <= 500; seed++)
        {
            var game = new Game(GameSetup.AllBots(seed, maxRounds: 250));
            var bot = new BotBrain(game);
            game.Start();
            CheckInvariants(game, seed);

            int actions = 0;
            while (game.Phase != Phase.GameOver)
            {
                bot.Act();
                game.Events.Clear();
                CheckInvariants(game, seed);
                if (++actions > 50_000) throw new AssertionException($"seed {seed}: game did not finish");
            }

            Assert.True(game.Winner != null, "a winner is declared");
            Assert.True(game.FinalRanking![0] == game.Winner);
            if (game.ActivePlayerCount == 1) bankruptcyFinishes++;
            else roundLimitFinishes++;
            totalActions += actions;
            totalTurns += game.TurnNumber;
        }

        Console.WriteLine($"        500 games: {bankruptcyFinishes} won by bankrupting rivals, {roundLimitFinishes} by round limit; " +
                          $"avg {totalTurns / 500} turns, {totalActions / 500} bot actions");
        Assert.True(bankruptcyFinishes > 0, "bots manage to bankrupt each other");
    }

    [Test]
    public static void Simulation_IsDeterministicForASeed()
    {
        string Run()
        {
            var game = new Game(GameSetup.AllBots(seed: 42, maxRounds: 60));
            var bot = new BotBrain(game);
            game.Start();
            while (game.Phase != Phase.GameOver) bot.Act();
            var summary = "";
            foreach (var p in game.Players) summary += $"{p.Name}:{p.Cash}:{p.Properties.Count};";
            return summary + game.TurnNumber;
        }

        Assert.Equal(Run(), Run());
    }

    private static void CheckInvariants(Game game, ulong seed)
    {
        string where = $"seed {seed}, turn {game.TurnNumber}";
        int housesOnBoard = 0, hotelsOnBoard = 0;

        foreach (var tile in game.Board.Ownables)
        {
            if (tile.Owner != null)
            {
                Assert.False(tile.Owner.IsBankrupt, $"{where}: {tile.Name} owned by a bankrupt player");
                Assert.True(tile.Owner.Properties.Contains(tile), $"{where}: {tile.Name} missing from its owner's portfolio");
            }
            if (tile is not PropertyTile p) continue;
            Assert.True(p.Houses is >= 0 and <= 5, $"{where}: {p.Name} has {p.Houses} buildings");
            if (p.Houses == 5) hotelsOnBoard++;
            else housesOnBoard += p.Houses;

            if (p.Houses > 0)
            {
                Assert.True(p.Owner != null && game.OwnsFullGroup(p.Owner, p.Group), $"{where}: {p.Name} built without a full set");
                int min = 5, max = 0;
                foreach (var other in game.Board.PropertiesIn(p.Group))
                {
                    Assert.False(other.IsMortgaged, $"{where}: {other.Name} mortgaged in a built group");
                    min = Math.Min(min, other.Houses);
                    max = Math.Max(max, other.Houses);
                }
                Assert.True(max - min <= 1, $"{where}: uneven building in {p.Group} ({min}..{max})");
            }
        }

        Assert.Equal(game.Setup.HousesInBank, housesOnBoard + game.HousesInBank, $"{where}: house supply");
        Assert.Equal(game.Setup.HotelsInBank, hotelsOnBoard + game.HotelsInBank, $"{where}: hotel supply");

        int owned = 0;
        foreach (var player in game.Players)
        {
            Assert.True(player.Cash >= 0, $"{where}: {player.Name} has negative cash {player.Cash}");
            if (player.IsBankrupt)
            {
                Assert.Equal(0, player.Properties.Count, $"{where}: bankrupt {player.Name} still owns property");
                Assert.Equal(0, player.Cash);
            }
            foreach (var tile in player.Properties)
                Assert.True(tile.Owner == player, $"{where}: {player.Name} lists {tile.Name} owned by someone else");
            owned += player.Properties.Count;
        }
        Assert.True(owned <= 28, $"{where}: more than 28 properties owned");

        // Every active player is either the current player or waiting in the turn queue (exactly once).
        if (game.Phase != Phase.GameOver)
        {
            int queued = 0;
            foreach (var p in game.TurnQueue)
            {
                Assert.False(p.IsBankrupt, $"{where}: bankrupt player still queued");
                Assert.True(p != game.CurrentPlayer, $"{where}: current player is also queued");
                queued++;
            }
            Assert.Equal(game.ActivePlayerCount, queued + 1, $"{where}: turn queue size");
        }
    }
}
