using System;
using System.Collections;
using System.Collections.Generic;

namespace Monopoly.Core.DataStructures;

/// <summary>
/// Hash table with separate chaining. Average O(1) Put/Get/Remove; resizes (doubles) above a 0.75 load factor.
/// Used for: tile lookup by name, color group → its properties.
/// </summary>
public sealed class HashMap<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>> where TKey : notnull
{
    private const double MaxLoadFactor = 0.75;

    private sealed class Entry
    {
        public required TKey Key;
        public required TValue Value;
        public required int Hash;
        public Entry? Next;
    }

    private Entry?[] _buckets;
    private int _count;
    private readonly IEqualityComparer<TKey> _comparer;

    public HashMap(int capacity = 16, IEqualityComparer<TKey>? comparer = null)
    {
        int size = 1;
        while (size < Math.Max(4, capacity)) size <<= 1;
        _buckets = new Entry?[size];
        _comparer = comparer ?? EqualityComparer<TKey>.Default;
    }

    public IDsTracer? Tracer { get; set; }
    public string TraceName { get; set; } = "HashMap";

    /// <summary>When false only writes are traced (keeps hot read paths out of the DSA log).</summary>
    public bool TraceReads { get; set; } = true;

    public int Count => _count;
    public int BucketCount => _buckets.Length;

    public TValue this[TKey key]
    {
        get => Get(key);
        set => Put(key, value);
    }

    /// <summary>Inserts or overwrites.</summary>
    public void Put(TKey key, TValue value)
    {
        int hash = Hash(key);
        int bucket = BucketOf(hash);
        int probes = 0;
        for (var e = _buckets[bucket]; e != null; e = e.Next)
        {
            probes++;
            if (e.Hash == hash && _comparer.Equals(e.Key, key))
            {
                e.Value = value;
                Tracer?.Record(TraceName, "Put (update)", key.ToString() ?? "", probes);
                return;
            }
        }
        _buckets[bucket] = new Entry { Key = key, Value = value, Hash = hash, Next = _buckets[bucket] };
        _count++;
        Tracer?.Record(TraceName, "Put", key.ToString() ?? "", probes + 1);
        if (_count > _buckets.Length * MaxLoadFactor) Resize();
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        var entry = Find(key, out int probes);
        if (TraceReads) Tracer?.Record(TraceName, "Get", key.ToString() ?? "", probes);
        if (entry == null)
        {
            value = default!;
            return false;
        }
        value = entry.Value;
        return true;
    }

    public TValue Get(TKey key)
    {
        if (!TryGetValue(key, out var value)) throw new KeyNotFoundException($"Key '{key}' not found.");
        return value;
    }

    public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
    {
        if (TryGetValue(key, out var value)) return value;
        value = factory(key);
        Put(key, value);
        return value;
    }

    public bool ContainsKey(TKey key) => Find(key, out _) != null;

    public bool Remove(TKey key)
    {
        int hash = Hash(key);
        int bucket = BucketOf(hash);
        Entry? previous = null;
        for (var e = _buckets[bucket]; e != null; previous = e, e = e.Next)
        {
            if (e.Hash != hash || !_comparer.Equals(e.Key, key)) continue;
            if (previous == null) _buckets[bucket] = e.Next;
            else previous.Next = e.Next;
            _count--;
            Tracer?.Record(TraceName, "Remove", key.ToString() ?? "", 1);
            return true;
        }
        return false;
    }

    public void Clear()
    {
        Array.Clear(_buckets);
        _count = 0;
    }

    public TKey[] Keys()
    {
        var keys = new TKey[_count];
        int i = 0;
        foreach (var pair in this) keys[i++] = pair.Key;
        return keys;
    }

    public TValue[] Values()
    {
        var values = new TValue[_count];
        int i = 0;
        foreach (var pair in this) values[i++] = pair.Value;
        return values;
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        foreach (var head in _buckets)
            for (var e = head; e != null; e = e.Next)
                yield return new KeyValuePair<TKey, TValue>(e.Key, e.Value);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private Entry? Find(TKey key, out int probes)
    {
        int hash = Hash(key);
        probes = 0;
        for (var e = _buckets[BucketOf(hash)]; e != null; e = e.Next)
        {
            probes++;
            if (e.Hash == hash && _comparer.Equals(e.Key, key)) return e;
        }
        return null;
    }

    private int Hash(TKey key)
    {
        int h = _comparer.GetHashCode(key);
        return h ^ (h >>> 16); // spread high bits into the low bits used by the bucket mask
    }

    private int BucketOf(int hash) => hash & (_buckets.Length - 1);

    private void Resize()
    {
        var old = _buckets;
        _buckets = new Entry?[old.Length * 2];
        foreach (var head in old)
        {
            var e = head;
            while (e != null)
            {
                var next = e.Next;
                int bucket = BucketOf(e.Hash);
                e.Next = _buckets[bucket];
                _buckets[bucket] = e;
                e = next;
            }
        }
        Tracer?.Record(TraceName, "Resize", $"{old.Length} → {_buckets.Length} buckets", _count);
    }
}
