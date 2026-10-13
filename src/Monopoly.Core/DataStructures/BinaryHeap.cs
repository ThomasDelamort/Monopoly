using System;

namespace Monopoly.Core.DataStructures;

/// <summary>
/// Array-backed binary heap (priority queue). Push/Pop O(log n), Peek O(1).
/// The root is the item that compares smallest; use <see cref="MaxHeap"/> for the largest.
/// Used by bots: best house to build (max-heap) and cheapest asset to give up when raising cash (min-heap).
/// </summary>
public sealed class BinaryHeap<T>
{
    private T[] _items;
    private int _count;
    private readonly Comparison<T> _comparison;

    public BinaryHeap(Comparison<T> comparison, int capacity = 16)
    {
        _comparison = comparison;
        _items = new T[Math.Max(1, capacity)];
    }

    public static BinaryHeap<T> MinHeap(Comparison<T> comparison) => new(comparison);

    public static BinaryHeap<T> MaxHeap(Comparison<T> comparison) => new((a, b) => comparison(b, a));

    public IDsTracer? Tracer { get; set; }
    public string TraceName { get; set; } = "Heap";

    public int Count => _count;
    public bool IsEmpty => _count == 0;

    public void Push(T item)
    {
        if (_count == _items.Length) Array.Resize(ref _items, _items.Length * 2);
        _items[_count] = item;
        int swaps = SiftUp(_count++);
        Tracer?.Record(TraceName, "Push", item?.ToString() ?? "null", swaps + 1);
    }

    public T Peek()
    {
        if (_count == 0) throw new InvalidOperationException("Heap is empty.");
        return _items[0];
    }

    public T Pop()
    {
        if (_count == 0) throw new InvalidOperationException("Heap is empty.");
        var root = _items[0];
        _items[0] = _items[--_count];
        _items[_count] = default!;
        int swaps = _count > 0 ? SiftDown(0) : 0;
        Tracer?.Record(TraceName, "Pop", root?.ToString() ?? "null", swaps + 1);
        return root;
    }

    public bool TryPop(out T item)
    {
        if (_count == 0)
        {
            item = default!;
            return false;
        }
        item = Pop();
        return true;
    }

    public void Clear()
    {
        Array.Clear(_items, 0, _count);
        _count = 0;
    }

    private int SiftUp(int index)
    {
        int swaps = 0;
        while (index > 0)
        {
            int parent = (index - 1) / 2;
            if (_comparison(_items[index], _items[parent]) >= 0) break;
            (_items[index], _items[parent]) = (_items[parent], _items[index]);
            index = parent;
            swaps++;
        }
        return swaps;
    }

    private int SiftDown(int index)
    {
        int swaps = 0;
        while (true)
        {
            int left = index * 2 + 1;
            if (left >= _count) break;
            int smallest = left;
            int right = left + 1;
            if (right < _count && _comparison(_items[right], _items[left]) < 0) smallest = right;
            if (_comparison(_items[smallest], _items[index]) >= 0) break;
            (_items[index], _items[smallest]) = (_items[smallest], _items[index]);
            index = smallest;
            swaps++;
        }
        return swaps;
    }
}
