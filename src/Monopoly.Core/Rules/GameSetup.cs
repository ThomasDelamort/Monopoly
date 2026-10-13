using System;

namespace Monopoly.Core.Rules;

public sealed record SeatConfig(string Name, int CharacterId, bool IsBot);

public sealed class GameSetup
{
    public const int SeatCount = 4;

    public static readonly string[] CharacterNames = { "Tycoon", "Skater", "Student", "Showman" };

    public required SeatConfig[] Seats { get; init; }

    /// <summary>0 = seed from the clock.</summary>
    public ulong Seed { get; init; }

    public int StartingCash { get; init; } = 1500;
    public int GoSalary { get; init; } = 200;
    public int JailFine { get; init; } = 50;
    public int HousesInBank { get; init; } = 32;
    public int HotelsInBank { get; init; } = 12;

    /// <summary>0 = play until one player is left; otherwise the richest player wins after this many rounds.</summary>
    public int MaxRounds { get; init; }

    /// <summary>
    /// The menu rule: there are always 4 tokens. Seats 1..<paramref name="humanCount"/> are humans,
    /// the rest are filled with bots (1 human → 3 bots, 4 humans → no bots).
    /// </summary>
    public static GameSetup ForHumans(int humanCount, ulong seed = 0, string[]? names = null, int[]? characters = null)
    {
        if (humanCount is < 1 or > SeatCount) throw new ArgumentOutOfRangeException(nameof(humanCount));
        var seats = new SeatConfig[SeatCount];
        for (int i = 0; i < SeatCount; i++)
        {
            bool bot = i >= humanCount;
            int character = characters != null ? characters[i] : i;
            string name = names?[i] is { Length: > 0 } custom ? custom : bot ? $"Bot {CharacterNames[character]}" : $"Player {i + 1}";
            seats[i] = new SeatConfig(name, character, bot);
        }
        return new GameSetup { Seats = seats, Seed = seed };
    }

    public static GameSetup AllBots(ulong seed, int maxRounds = 0)
    {
        var seats = new SeatConfig[SeatCount];
        for (int i = 0; i < SeatCount; i++) seats[i] = new SeatConfig($"Bot {CharacterNames[i]}", i, true);
        return new GameSetup { Seats = seats, Seed = seed, MaxRounds = maxRounds };
    }
}
