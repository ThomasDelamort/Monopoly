using System;
using System.Collections;
using System.Collections.Generic;

namespace Monopoly.Core.DataStructures;

/// <summary>
/// FIFO queue on a circular array buffer. Enqueue/Dequeue/Peek are O(1) (amortized for growth).
/// In bounded mode the capacity is fixed and enqueuing into a full queue drops the oldest item.
/// Used for: turn order, Chance/Community Chest decks, auction bidders, pending debts, game events, logs.
/// </summary>
public sealed class ArrayQueue<T> : IEnumerable<T>
{
    private T[] _items;
    private int _head;
    private int _count;
    private readonly bool _bounded;

    public ArrayQueue(int capacity = 8, bool bounded = false)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _items = new T[capacity];
        _bounded = bounded;
    }

    public IDsTracer? Tracer { get; set; }
    public string TraceName { get; set; } = "Queue";

    public int Count => _count;
    public bool IsEmpty => _count == 0;
    public int Capacity => _items.Length;

    public void Enqueue(T item)
    {
        if (_count == _items.Length)
        {
            if (_bounded) DequeueSilently();
            else Grow();
        }
        _items[(_head + _count) % _items.Length] = item;
        _count++;
        Tracer?.Record(TraceName, "Enqueue", Describe(item), 1);
    }

    public T Dequeue()
    {
        if (_count == 0) throw new InvalidOperationException("Queue is empty.");
        var item = DequeueSilently();
        Tracer?.Record(TraceName, "Dequeue", Describe(item), 1);
        return item;
    }

    public bool TryDequeue(out T item)
    {
        if (_count == 0)
        {
            item = default!;
            return false;
        }
        item = Dequeue();
        return true;
    }

    public T Peek()
    {
        if (_count == 0) throw new InvalidOperationException("Queue is empty.");
        return _items[_head];
    }

    /// <summary>Item at <paramref name="offset"/> positions behind the front (0 = front).</summary>
    public T PeekAt(int offset)
    {
        if ((uint)offset >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(offset));
        return _items[(_head + offset) % _items.Length];
    }

    /// <summary>Removes every matching item while keeping the order of the rest. O(n).</summary>
    public int RemoveAll(Predicate<T> match)
    {
        int write = 0;
        int removed = 0;
        for (int read = 0; read < _count; read++)
        {
            var item = _items[(_head + read) % _items.Length];
            if (match(item))
            {
                removed++;
                continue;
            }
            _items[(_head + write) % _items.Length] = item;
            write++;
        }
        for (int i = write; i < _count; i++) _items[(_head + i) % _items.Length] = default!;
        _count = write;
        if (removed > 0) Tracer?.Record(TraceName, "RemoveAll", $"{removed} removed", _count + removed);
        return removed;
    }

    public void Clear()
    {
        Array.Clear(_items);
        _head = 0;
        _count = 0;
        Tracer?.Record(TraceName, "Clear", "", 1);
    }

    /// <summary>Front first.</summary>
    public T[] ToArray()
    {
        var result = new T[_count];
        for (int i = 0; i < _count; i++) result[i] = _items[(_head + i) % _items.Length];
        return result;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < _count; i++) yield return _items[(_head + i) % _items.Length];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private T DequeueSilently()
    {
        var item = _items[_head];
        _items[_head] = default!;
        _head = (_head + 1) % _items.Length;
        _count--;
        return item;
    }

    private void Grow()
    {
        var bigger = new T[_items.Length * 2];
        for (int i = 0; i < _count; i++) bigger[i] = _items[(_head + i) % _items.Length];
        _items = bigger;
        _head = 0;
    }

    private static string Describe(T item) => item?.ToString() ?? "null";
}
