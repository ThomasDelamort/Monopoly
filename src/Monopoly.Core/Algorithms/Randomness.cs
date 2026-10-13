using System;

namespace Monopoly.Core.Algorithms;

public interface IRandom
{
    /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
    int Next(int minInclusive, int maxExclusive);
}

/// <summary>
/// xorshift64* generator: tiny, fast and fully deterministic for a given seed on every platform,
/// so a seeded game (or test) always replays identically.
/// </summary>
public sealed class XorShiftRandom : IRandom
{
    private ulong _state;

    public XorShiftRandom(ulong seed)
    {
        _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
    }

    public static XorShiftRandom FromClock() => new((ulong)DateTime.UtcNow.Ticks);

    public ulong NextUInt64()
    {
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        return _state * 0x2545F4914F6CDD1DUL;
    }

    public int Next(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        ulong range = (ulong)(maxExclusive - minInclusive);
        // Rejection sampling removes modulo bias.
        ulong limit = ulong.MaxValue - ulong.MaxValue % range;
        ulong value;
        do value = NextUInt64(); while (value >= limit);
        return minInclusive + (int)(value % range);
    }
}

public static class Shuffle
{
    /// <summary>Fisher–Yates: every permutation equally likely, O(n), in place.</summary>
    public static void FisherYates<T>(Span<T> items, IRandom random)
    {
        for (int i = items.Length - 1; i > 0; i--)
        {
            int j = random.Next(0, i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
