using System;
using System.Collections;
using System.Collections.Generic;

namespace Monopoly.Core.DataStructures;

/// <summary>A node in a <see cref="CircularDoublyLinkedList{T}"/>. Next/Prev are never null while it is in a list.</summary>
public sealed class CdllNode<T>
{
    internal CdllNode(T value)
    {
        Value = value;
        Next = this;
        Prev = this;
    }

    public T Value { get; }
    public CdllNode<T> Next { get; internal set; }
    public CdllNode<T> Prev { get; internal set; }

    /// <summary>Position counted forward from the head (head = 0).</summary>
    public int Index { get; internal set; }

    public override string ToString() => $"[{Index}] {Value}";
}

/// <summary>
/// Circular doubly linked list: the last node's Next is the head and the head's Prev is the last node.
/// This is the game board: a token holds a node and walks Next (forward) or Prev (backward);
/// wrapping onto the head while walking forward means passing GO.
/// </summary>
public sealed class CircularDoublyLinkedList<T> : IEnumerable<T>
{
    public CdllNode<T>? Head { get; private set; }
    public int Count { get; private set; }

    public IDsTracer? Tracer { get; set; }
    public string TraceName { get; set; } = "Linked List";

    public CdllNode<T> AddLast(T value)
    {
        var node = new CdllNode<T>(value);
        if (Head == null)
        {
            Head = node;
        }
        else
        {
            var tail = Head.Prev;
            node.Prev = tail;
            node.Next = Head;
            tail.Next = node;
            Head.Prev = node;
        }
        node.Index = Count;
        Count++;
        Tracer?.Record(TraceName, "AddLast", value?.ToString() ?? "null", 1);
        return node;
    }

    public CdllNode<T> AddFirst(T value)
    {
        var node = AddLast(value);
        Head = node; // in a ring, adding at the tail then moving the head makes it first
        Reindex();
        return node;
    }

    public void Remove(CdllNode<T> node)
    {
        if (Head == null) throw new InvalidOperationException("List is empty.");
        if (Count == 1)
        {
            if (node != Head) throw new ArgumentException("Node does not belong to this list.", nameof(node));
            Head = null;
            Count = 0;
            return;
        }
        node.Prev.Next = node.Next;
        node.Next.Prev = node.Prev;
        if (node == Head) Head = node.Next;
        node.Next = node;
        node.Prev = node;
        Count--;
        Reindex();
        Tracer?.Record(TraceName, "Remove", node.Value?.ToString() ?? "null", Count);
    }

    /// <summary>
    /// Walks <paramref name="steps"/> nodes (negative = backward via Prev).
    /// <paramref name="wrapsForward"/> counts how many times the walk stepped onto the head going forward.
    /// </summary>
    public CdllNode<T> Step(CdllNode<T> from, int steps, out int wrapsForward)
    {
        wrapsForward = 0;
        var node = from;
        if (steps >= 0)
        {
            for (int i = 0; i < steps; i++)
            {
                node = node.Next;
                if (node == Head) wrapsForward++;
            }
        }
        else
        {
            for (int i = 0; i < -steps; i++) node = node.Prev;
        }
        Tracer?.Record(TraceName, steps >= 0 ? "Next×" + steps : "Prev×" + (-steps), $"{from.Value} → {node.Value}", Math.Abs(steps));
        return node;
    }

    /// <summary>
    /// Linear search forward around the ring, starting after <paramref name="from"/>.
    /// Returns the first match and its forward distance, or null after a full lap.
    /// </summary>
    public CdllNode<T>? FindNext(CdllNode<T> from, Predicate<T> match, out int distance)
    {
        var node = from.Next;
        for (distance = 1; distance <= Count; distance++, node = node.Next)
        {
            if (match(node.Value))
            {
                Tracer?.Record(TraceName, "FindNext", $"{from.Value} → {node.Value}", distance);
                return node;
            }
        }
        distance = -1;
        Tracer?.Record(TraceName, "FindNext", "no match", Count);
        return null;
    }

    /// <summary>Node at a position, walking from the head in whichever direction is shorter.</summary>
    public CdllNode<T> NodeAt(int index)
    {
        if (Head == null || (uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
        var node = Head;
        if (index <= Count / 2)
        {
            for (int i = 0; i < index; i++) node = node.Next;
        }
        else
        {
            for (int i = Count; i > index; i--) node = node.Prev;
        }
        return node;
    }

    /// <summary>Number of Next steps needed to get from one node to another (0..Count-1).</summary>
    public int ForwardDistance(CdllNode<T> from, CdllNode<T> to) => ((to.Index - from.Index) % Count + Count) % Count;

    public IEnumerator<T> GetEnumerator()
    {
        if (Head == null) yield break;
        var node = Head;
        do
        {
            yield return node.Value;
            node = node.Next;
        } while (node != Head);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void Reindex()
    {
        if (Head == null) return;
        var node = Head;
        int i = 0;
        do
        {
            node.Index = i++;
            node = node.Next;
        } while (node != Head);
    }
}
