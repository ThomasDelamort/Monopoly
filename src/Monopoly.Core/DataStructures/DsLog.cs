using System;

namespace Monopoly.Core.DataStructures;

/// <summary>Receives a record of every operation a traced data structure performs.</summary>
public interface IDsTracer
{
    void Record(string structure, string operation, string detail, int steps);
}

/// <summary>One traced data-structure operation, e.g. "Turn Queue · Dequeue · Alice · 1 step".</summary>
public readonly record struct DsOperation(long Sequence, string Structure, string Operation, string Detail, int Steps);

/// <summary>
/// Bounded log of the most recent data-structure operations, shown by the in-game DSA panel.
/// Backed by a bounded <see cref="ArrayQueue{T}"/>: when full, the oldest entry is dropped.
/// </summary>
public sealed class DsLog : IDsTracer
{
    private readonly ArrayQueue<DsOperation> _recent;
    private long _sequence;

    public DsLog(int capacity = 64)
    {
        _recent = new ArrayQueue<DsOperation>(capacity, bounded: true);
    }

    public event Action<DsOperation>? Recorded;

    public int Count => _recent.Count;

    public void Record(string structure, string operation, string detail, int steps)
    {
        var op = new DsOperation(++_sequence, structure, operation, detail, steps);
        _recent.Enqueue(op);
        Recorded?.Invoke(op);
    }

    /// <summary>Oldest first.</summary>
    public DsOperation[] Recent() => _recent.ToArray();

    /// <summary>Most recent operation on the named structure (linear search from the newest end).</summary>
    public DsOperation? LastFor(string structure)
    {
        var all = _recent.ToArray();
        for (int i = all.Length - 1; i >= 0; i--)
            if (all[i].Structure == structure) return all[i];
        return null;
    }
}
