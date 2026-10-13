using System;

namespace Monopoly.Core.Algorithms;

/// <summary>Index found (-1 if none) and how many comparisons it took.</summary>
public readonly record struct SearchResult(int Index, int Comparisons)
{
    public bool Found => Index >= 0;
}

/// <summary>Hand-written searches. The binary variants require input sorted consistently with <c>compare</c>.</summary>
public static class Searching
{
    /// <summary>O(n): first item matching the predicate.</summary>
    public static SearchResult Linear<T>(ReadOnlySpan<T> items, Predicate<T> match)
    {
        for (int i = 0; i < items.Length; i++)
            if (match(items[i])) return new SearchResult(i, i + 1);
        return new SearchResult(-1, items.Length);
    }

    /// <summary>
    /// O(log n): index of an item equal to <paramref name="key"/>, or -1.
    /// <paramref name="compare"/>(item, key) is negative when item sorts before key.
    /// </summary>
    public static SearchResult Binary<T, TKey>(ReadOnlySpan<T> sorted, TKey key, Func<T, TKey, int> compare)
    {
        int low = 0, high = sorted.Length - 1, comparisons = 0;
        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            int c = compare(sorted[mid], key);
            comparisons++;
            if (c == 0) return new SearchResult(mid, comparisons);
            if (c < 0) low = mid + 1;
            else high = mid - 1;
        }
        return new SearchResult(-1, comparisons);
    }

    /// <summary>O(log n): first index whose item is not before the key (Length if all are before).</summary>
    public static SearchResult LowerBound<T, TKey>(ReadOnlySpan<T> sorted, TKey key, Func<T, TKey, int> compare)
    {
        int low = 0, high = sorted.Length, comparisons = 0;
        while (low < high)
        {
            int mid = low + (high - low) / 2;
            comparisons++;
            if (compare(sorted[mid], key) < 0) low = mid + 1;
            else high = mid;
        }
        return new SearchResult(low, comparisons);
    }

    /// <summary>O(log n): first index whose item sorts after the key (Length if none).</summary>
    public static SearchResult UpperBound<T, TKey>(ReadOnlySpan<T> sorted, TKey key, Func<T, TKey, int> compare)
    {
        int low = 0, high = sorted.Length, comparisons = 0;
        while (low < high)
        {
            int mid = low + (high - low) / 2;
            comparisons++;
            if (compare(sorted[mid], key) <= 0) low = mid + 1;
            else high = mid;
        }
        return new SearchResult(low, comparisons);
    }
}
