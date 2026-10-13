using System;
using System.Collections;
using System.Collections.Generic;

namespace Monopoly.Core.DataStructures;

/// <summary>
/// Growable array (like an ArrayList). Index access O(1), Add O(1) amortized, Insert/Remove O(n).
/// Used for player portfolios and as the buffer the sorting algorithms work on.
/// </summary>
public sealed class DynamicArray<T> : IEnumerable<T>
{
    private T[] _items;
    private int _count;

    public DynamicArray(int capacity = 4)
    {
        _items = new T[Math.Max(1, capacity)];
    }

    public DynamicArray(ReadOnlySpan<T> source) : this(source.Length)
    {
        source.CopyTo(_items);
        _count = source.Length;
    }

    public int Count => _count;

    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
            return _items[index];
        }
        set
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
            _items[index] = value;
        }
    }

    public void Add(T item)
    {
        EnsureCapacity(_count + 1);
        _items[_count++] = item;
    }

    public void Insert(int index, T item)
    {
        if ((uint)index > (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
        EnsureCapacity(_count + 1);
        Array.Copy(_items, index, _items, index + 1, _count - index);
        _items[index] = item;
        _count++;
    }

    public void RemoveAt(int index)
    {
        if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
        _count--;
        Array.Copy(_items, index + 1, _items, index, _count - index);
        _items[_count] = default!;
    }

    public bool Remove(T item)
    {
        int index = IndexOf(item);
        if (index < 0) return false;
        RemoveAt(index);
        return true;
    }

    public int IndexOf(T item)
    {
        var comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < _count; i++)
            if (comparer.Equals(_items[i], item)) return i;
        return -1;
    }

    public bool Contains(T item) => IndexOf(item) >= 0;

    public void Clear()
    {
        Array.Clear(_items, 0, _count);
        _count = 0;
    }

    public Span<T> AsSpan() => _items.AsSpan(0, _count);

    public T[] ToArray() => AsSpan().ToArray();

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < _count; i++) yield return _items[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void EnsureCapacity(int needed)
    {
        if (needed <= _items.Length) return;
        Array.Resize(ref _items, Math.Max(needed, _items.Length * 2));
    }
}
