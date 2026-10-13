using System;

namespace Monopoly.Core.Algorithms;

/// <summary>
/// Long-run probability of ending a move on each tile, by treating the board as a Markov chain and
/// iterating the distribution (dynamic programming over turns) until it converges.
/// The model: roll 2d6, move forward, and "Go To Jail" sends the token to Jail. Bots use these
/// odds to value properties (e.g. orange is landed on more often than dark blue).
/// </summary>
public static class LandingProbability
{
    /// <summary>P(2d6 = s) for s = 0..12.</summary>
    public static double DiceSumProbability(int sum) => sum is < 2 or > 12 ? 0 : (6 - Math.Abs(sum - 7)) / 36.0;

    public static double[] Compute(int tileCount, int jailIndex, int goToJailIndex, double tolerance = 1e-10, int maxIterations = 1000)
    {
        var current = new double[tileCount];
        var next = new double[tileCount];
        for (int i = 0; i < tileCount; i++) current[i] = 1.0 / tileCount;

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            Array.Clear(next);
            for (int from = 0; from < tileCount; from++)
            {
                if (current[from] == 0) continue;
                for (int sum = 2; sum <= 12; sum++)
                {
                    int to = (from + sum) % tileCount;
                    if (to == goToJailIndex) to = jailIndex;
                    next[to] += current[from] * DiceSumProbability(sum);
                }
            }

            double delta = 0;
            for (int i = 0; i < tileCount; i++) delta += Math.Abs(next[i] - current[i]);
            (current, next) = (next, current);
            if (delta < tolerance) break;
        }
        return current;
    }
}
