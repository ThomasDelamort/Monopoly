using System;

namespace Monopoly.Core.Algorithms;

public enum SortAlgorithm
{
    MergeSort,
    QuickSort,
    InsertionSort,
    HeapSort,
}

/// <summary>Work done by one sort call, shown in the DSA panel.</summary>
public readonly record struct SortStats(SortAlgorithm Algorithm, int Count, long Comparisons, long Writes)
{
    public string BigO => Algorithm switch
    {
        SortAlgorithm.MergeSort => "O(n log n)",
        SortAlgorithm.QuickSort => "O(n log n) avg",
        SortAlgorithm.InsertionSort => "O(n²)",
        SortAlgorithm.HeapSort => "O(n log n)",
        _ => "",
    };
}

/// <summary>
/// Hand-written comparison sorts. All sort in place, ascending by <c>comparison</c>.
/// Merge sort is stable (leaderboard ties keep seat order); quick sort and heap sort are not.
/// </summary>
public static class Sorting
{
    private struct Counter
    {
        public long Comparisons;
        public long Writes;
    }

    public static SortStats Sort<T>(Span<T> items, Comparison<T> comparison, SortAlgorithm algorithm) => algorithm switch
    {
        SortAlgorithm.MergeSort => MergeSort(items, comparison),
        SortAlgorithm.QuickSort => QuickSort(items, comparison),
        SortAlgorithm.InsertionSort => InsertionSort(items, comparison),
        SortAlgorithm.HeapSort => HeapSort(items, comparison),
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm)),
    };

    // ---------------------------------------------------------------- Merge sort (top-down, stable)

    public static SortStats MergeSort<T>(Span<T> items, Comparison<T> comparison)
    {
        var counter = new Counter();
        if (items.Length > 1)
        {
            var buffer = new T[items.Length];
            MergeSortRange(items, buffer, comparison, ref counter);
        }
        return new SortStats(SortAlgorithm.MergeSort, items.Length, counter.Comparisons, counter.Writes);
    }

    private static void MergeSortRange<T>(Span<T> items, Span<T> buffer, Comparison<T> comparison, ref Counter counter)
    {
        if (items.Length < 2) return;
        int mid = items.Length / 2;
        MergeSortRange(items[..mid], buffer[..mid], comparison, ref counter);
        MergeSortRange(items[mid..], buffer[mid..], comparison, ref counter);

        // Merge the two sorted halves into the buffer, then copy back.
        int left = 0, right = mid, write = 0;
        while (left < mid && right < items.Length)
        {
            counter.Comparisons++;
            // "<=" keeps equal items in their original order (stability).
            buffer[write++] = comparison(items[left], items[right]) <= 0 ? items[left++] : items[right++];
        }
        while (left < mid) buffer[write++] = items[left++];
        while (right < items.Length) buffer[write++] = items[right++];
        buffer[..items.Length].CopyTo(items);
        counter.Writes += items.Length;
    }

    // ---------------------------------------------------------------- Quick sort (median-of-three, Lomuto partition)

    public static SortStats QuickSort<T>(Span<T> items, Comparison<T> comparison)
    {
        var counter = new Counter();
        QuickSortRange(items, comparison, ref counter);
        return new SortStats(SortAlgorithm.QuickSort, items.Length, counter.Comparisons, counter.Writes);
    }

    private static void QuickSortRange<T>(Span<T> items, Comparison<T> comparison, ref Counter counter)
    {
        while (items.Length > 1)
        {
            int pivotIndex = Partition(items, comparison, ref counter);
            var left = items[..pivotIndex];
            var right = items[(pivotIndex + 1)..];
            // Recurse into the smaller side and loop on the larger one: O(log n) stack depth.
            if (left.Length < right.Length)
            {
                QuickSortRange(left, comparison, ref counter);
                items = right;
            }
            else
            {
                QuickSortRange(right, comparison, ref counter);
                items = left;
            }
        }
    }

    private static int Partition<T>(Span<T> items, Comparison<T> comparison, ref Counter counter)
    {
        int last = items.Length - 1;
        int mid = last / 2;
        // Median-of-three: order first/mid/last, then use the median as the pivot (moved to the end).
        if (Less(items[mid], items[0], comparison, ref counter)) Swap(items, mid, 0, ref counter);
        if (Less(items[last], items[0], comparison, ref counter)) Swap(items, last, 0, ref counter);
        if (Less(items[last], items[mid], comparison, ref counter)) Swap(items, last, mid, ref counter);
        Swap(items, mid, last, ref counter);

        var pivot = items[last];
        int store = 0;
        for (int i = 0; i < last; i++)
        {
            if (Less(items[i], pivot, comparison, ref counter)) Swap(items, i, store++, ref counter);
        }
        Swap(items, store, last, ref counter);
        return store;
    }

    // ---------------------------------------------------------------- Insertion sort (stable, great for tiny inputs)

    public static SortStats InsertionSort<T>(Span<T> items, Comparison<T> comparison)
    {
        var counter = new Counter();
        for (int i = 1; i < items.Length; i++)
        {
            var current = items[i];
            int j = i - 1;
            while (j >= 0)
            {
                counter.Comparisons++;
                if (comparison(items[j], current) <= 0) break;
                items[j + 1] = items[j];
                counter.Writes++;
                j--;
            }
            items[j + 1] = current;
            counter.Writes++;
        }
        return new SortStats(SortAlgorithm.InsertionSort, items.Length, counter.Comparisons, counter.Writes);
    }

    // ---------------------------------------------------------------- Heap sort (in place, max-heap)

    public static SortStats HeapSort<T>(Span<T> items, Comparison<T> comparison)
    {
        var counter = new Counter();
        int n = items.Length;
        for (int i = n / 2 - 1; i >= 0; i--) SiftDown(items, i, n, comparison, ref counter);
        for (int end = n - 1; end > 0; end--)
        {
            Swap(items, 0, end, ref counter); // largest goes to the back
            SiftDown(items, 0, end, comparison, ref counter);
        }
        return new SortStats(SortAlgorithm.HeapSort, n, counter.Comparisons, counter.Writes);
    }

    private static void SiftDown<T>(Span<T> items, int root, int size, Comparison<T> comparison, ref Counter counter)
    {
        while (true)
        {
            int largest = root;
            int left = root * 2 + 1;
            int right = left + 1;
            if (left < size && Less(items[largest], items[left], comparison, ref counter)) largest = left;
            if (right < size && Less(items[largest], items[right], comparison, ref counter)) largest = right;
            if (largest == root) return;
            Swap(items, root, largest, ref counter);
            root = largest;
        }
    }

    // ---------------------------------------------------------------- helpers

    private static bool Less<T>(T a, T b, Comparison<T> comparison, ref Counter counter)
    {
        counter.Comparisons++;
        return comparison(a, b) < 0;
    }

    private static void Swap<T>(Span<T> items, int i, int j, ref Counter counter)
    {
        if (i == j) return;
        (items[i], items[j]) = (items[j], items[i]);
        counter.Writes += 2;
    }
}
