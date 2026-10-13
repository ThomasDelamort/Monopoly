using System;
using Monopoly.Core.Algorithms;

namespace Monopoly.Core.Tests;

public static class AlgorithmTests
{
    private static readonly SortAlgorithm[] AllSorts =
        { SortAlgorithm.MergeSort, SortAlgorithm.QuickSort, SortAlgorithm.InsertionSort, SortAlgorithm.HeapSort };

    [Test]
    public static void Sorts_MatchReference_OnRandomData()
    {
        var rng = new XorShiftRandom(99);
        foreach (int size in new[] { 0, 1, 2, 3, 7, 40, 257 })
        {
            var data = new int[size];
            for (int i = 0; i < size; i++) data[i] = rng.Next(-50, 50); // many duplicates
            var expected = (int[])data.Clone();
            Array.Sort(expected);
            foreach (var algorithm in AllSorts)
            {
                var copy = (int[])data.Clone();
                var stats = Sorting.Sort<int>(copy, (a, b) => a.CompareTo(b), algorithm);
                Assert.SequenceEqual(expected, copy, $"{algorithm} n={size}");
                Assert.Equal(size, stats.Count);
            }
        }
    }

    [Test]
    public static void Sorts_HandleSortedAndReversedInput()
    {
        var ascending = new int[100];
        for (int i = 0; i < 100; i++) ascending[i] = i;
        foreach (var algorithm in AllSorts)
        {
            var reversed = (int[])ascending.Clone();
            Array.Reverse(reversed);
            Sorting.Sort<int>(reversed, (a, b) => a.CompareTo(b), algorithm);
            Assert.SequenceEqual(ascending, reversed, algorithm.ToString());
        }
    }

    [Test]
    public static void MergeAndInsertionSort_AreStable()
    {
        var items = new (int Key, int Seq)[] { (2, 0), (1, 1), (2, 2), (1, 3), (0, 4), (2, 5) };
        foreach (var algorithm in new[] { SortAlgorithm.MergeSort, SortAlgorithm.InsertionSort })
        {
            var copy = ((int, int)[])items.Clone();
            Sorting.Sort<(int Key, int Seq)>(copy, (a, b) => a.Key.CompareTo(b.Key), algorithm);
            for (int i = 1; i < copy.Length; i++)
                if (copy[i - 1].Item1 == copy[i].Item1)
                    Assert.True(copy[i - 1].Item2 < copy[i].Item2, $"{algorithm} kept equal keys in order");
        }
    }

    [Test]
    public static void InsertionSort_CountsComparisons()
    {
        var sorted = new[] { 1, 2, 3, 4, 5 };
        var stats = Sorting.InsertionSort<int>(sorted, (a, b) => a.CompareTo(b));
        Assert.Equal(4L, stats.Comparisons, "best case is n-1 comparisons");
    }

    [Test]
    public static void BinarySearch_FindsOrReportsMissing()
    {
        var data = new[] { 1, 3, 5, 7, 9, 11, 13 };
        for (int i = 0; i < data.Length; i++)
        {
            var result = Searching.Binary<int, int>(data, data[i], (a, b) => a.CompareTo(b));
            Assert.Equal(i, result.Index);
            Assert.True(result.Comparisons <= 3, "log2(7) rounds up to 3");
        }
        Assert.False(Searching.Binary<int, int>(data, 4, (a, b) => a.CompareTo(b)).Found);
        Assert.False(Searching.Binary<int, int>(Array.Empty<int>(), 4, (a, b) => a.CompareTo(b)).Found);
    }

    [Test]
    public static void LowerAndUpperBound()
    {
        var data = new[] { 1, 2, 2, 2, 5, 8 };
        Assert.Equal(1, Searching.LowerBound<int, int>(data, 2, (a, b) => a.CompareTo(b)).Index);
        Assert.Equal(4, Searching.UpperBound<int, int>(data, 2, (a, b) => a.CompareTo(b)).Index);
        Assert.Equal(6, Searching.LowerBound<int, int>(data, 9, (a, b) => a.CompareTo(b)).Index);
        Assert.Equal(0, Searching.UpperBound<int, int>(data, 0, (a, b) => a.CompareTo(b)).Index);
    }

    [Test]
    public static void LinearSearch()
    {
        var data = new[] { "GO", "Baltic", "Chance" };
        var hit = Searching.Linear<string>(data, s => s.StartsWith("Ch"));
        Assert.Equal(2, hit.Index);
        Assert.Equal(3, hit.Comparisons);
        Assert.False(Searching.Linear<string>(data, s => s == "Jail").Found);
    }

    [Test]
    public static void FisherYates_IsAPermutation_AndDeterministic()
    {
        var a = new int[52];
        var b = new int[52];
        for (int i = 0; i < 52; i++) a[i] = b[i] = i;
        Shuffle.FisherYates<int>(a, new XorShiftRandom(5));
        Shuffle.FisherYates<int>(b, new XorShiftRandom(5));
        Assert.SequenceEqual(a, b, "same seed, same order");
        var sorted = (int[])a.Clone();
        Array.Sort(sorted);
        for (int i = 0; i < 52; i++) Assert.Equal(i, sorted[i]);
        bool moved = false;
        for (int i = 0; i < 52; i++) moved |= a[i] != i;
        Assert.True(moved, "shuffle changed the order");
    }

    [Test]
    public static void Random_StaysInRange_AndCoversIt()
    {
        var rng = new XorShiftRandom(1);
        var seen = new bool[6];
        for (int i = 0; i < 10_000; i++)
        {
            int roll = rng.Next(1, 7);
            Assert.True(roll is >= 1 and <= 6);
            seen[roll - 1] = true;
        }
        foreach (var s in seen) Assert.True(s);
    }

    [Test]
    public static void LandingProbability_IsADistribution_JailMostVisited()
    {
        var odds = LandingProbability.Compute(40, jailIndex: 10, goToJailIndex: 30);
        double sum = 0;
        int best = 0;
        for (int i = 0; i < 40; i++)
        {
            sum += odds[i];
            if (odds[i] > odds[best]) best = i;
        }
        Assert.Near(1.0, sum, 1e-9);
        Assert.Equal(10, best);
        Assert.Near(0.0, odds[30], 1e-12, "nobody ends a move on Go To Jail");
        Assert.True(odds[19] > odds[37], "New York Avenue (orange) beats Park Place");
        Assert.Near(1.0 / 36, LandingProbability.DiceSumProbability(2), 1e-12);
        Assert.Near(6.0 / 36, LandingProbability.DiceSumProbability(7), 1e-12);
    }
}
