using System;
using System.Collections;
using System.Collections.Generic;

namespace Monopoly.Core.DataStructures;

/// <summary>
/// LIFO stack on a growable array. Push/Pop/Peek are O(1) amortized.
/// Used for: menu screen navigation (Back pops) and per-turn undo of build/mortgage actions.
/// </summary>
public sealed class ArrayStack<T> : IEnumerable<T>
{
    private T[] _items;
    private int _count;

    public ArrayStack(int capacity = 8)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _items = new T[capacity];
    }

    public IDsTracer? Tracer { get; set; }
    public string TraceName { get; set; } = "Stack";

    public int Count => _count;
    public bool IsEmpty => _count == 0;

    public void Push(T item)
    {
        if (_count == _items.Length) Array.Resize(ref _items, _items.Length * 2);
        _items[_count++] = item;
        Tracer?.Record(TraceName, "Push", Describe(item), 1);
    }

    public T Pop()
    {
        if (_count == 0) throw new InvalidOperationException("Stack is empty.");
        var item = _items[--_count];
        _items[_count] = default!;
        Tracer?.Record(TraceName, "Pop", Describe(item), 1);
        return item;
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

    public T Peek()
    {
        if (_count == 0) throw new InvalidOperationException("Stack is empty.");
        return _items[_count - 1];
    }

    public bool TryPeek(out T item)
    {
        if (_count == 0)
        {
            item = default!;
            return false;
        }
        item = _items[_count - 1];
        return true;
    }

    public void Clear()
    {
        if (_count == 0) return;
        Array.Clear(_items, 0, _count);
        _count = 0;
        Tracer?.Record(TraceName, "Clear", "", 1);
    }

    /// <summary>Top first.</summary>
    public T[] ToArray()
    {
        var result = new T[_count];
        for (int i = 0; i < _count; i++) result[i] = _items[_count - 1 - i];
        return result;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = _count - 1; i >= 0; i--) yield return _items[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static string Describe(T item) => item?.ToString() ?? "null";
}
