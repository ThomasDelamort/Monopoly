using System;
using System.Collections.Generic;
using Monopoly.Core.Algorithms;
using Monopoly.Core.DataStructures;

namespace Monopoly.Core.Tests;

public static class DataStructureTests
{
    // ------------------------------------------------------------------ ArrayQueue

    [Test]
    public static void Queue_IsFifo_AcrossWrapAroundAndGrowth()
    {
        var q = new ArrayQueue<int>(2);
        q.Enqueue(1);
        q.Enqueue(2);
        Assert.Equal(1, q.Dequeue());
        q.Enqueue(3); // wraps around the buffer
        q.Enqueue(4); // forces growth while wrapped
        q.Enqueue(5);
        Assert.SequenceEqual(new[] { 2, 3, 4, 5 }, q.ToArray());
        Assert.Equal(2, q.Peek());
        Assert.Equal(4, q.PeekAt(2));
        Assert.Equal(4, q.Count);
    }

    [Test]
    public static void Queue_Bounded_DropsOldest()
    {
        var q = new ArrayQueue<int>(3, bounded: true);
        for (int i = 1; i <= 5; i++) q.Enqueue(i);
        Assert.SequenceEqual(new[] { 3, 4, 5 }, q.ToArray());
        Assert.Equal(3, q.Capacity);
    }

    [Test]
    public static void Queue_RemoveAll_KeepsOrder()
    {
        var q = new ArrayQueue<int>(4);
        foreach (var n in new[] { 1, 2, 3, 4, 5, 6 }) q.Enqueue(n);
        q.Dequeue();
        Assert.Equal(2, q.RemoveAll(n => n % 3 == 0));
        Assert.SequenceEqual(new[] { 2, 4, 5 }, q.ToArray());
    }

    [Test]
    public static void Queue_Empty_Throws()
    {
        var q = new ArrayQueue<string>();
        Assert.Throws<InvalidOperationException>(() => q.Dequeue());
        Assert.Throws<InvalidOperationException>(() => q.Peek());
        Assert.False(q.TryDequeue(out _));
    }

    [Test]
    public static void Queue_Tracer_RecordsOperations()
    {
        var log = new DsLog(10);
        var q = new ArrayQueue<string> { Tracer = log, TraceName = "Turns" };
        q.Enqueue("Ann");
        q.Dequeue();
        var ops = log.Recent();
        Assert.Equal(2, ops.Length);
        Assert.Equal("Enqueue", ops[0].Operation);
        Assert.Equal("Dequeue", ops[1].Operation);
        Assert.Equal("Ann", log.LastFor("Turns")!.Value.Detail);
    }

    // ------------------------------------------------------------------ ArrayStack

    [Test]
    public static void Stack_IsLifo()
    {
        var s = new ArrayStack<int>(1);
        for (int i = 1; i <= 5; i++) s.Push(i);
        Assert.SequenceEqual(new[] { 5, 4, 3, 2, 1 }, s.ToArray());
        Assert.Equal(5, s.Pop());
        Assert.Equal(4, s.Peek());
        Assert.Equal(4, s.Count);
        s.Clear();
        Assert.True(s.IsEmpty);
        Assert.Throws<InvalidOperationException>(() => s.Pop());
        Assert.False(s.TryPeek(out _));
    }

    // ------------------------------------------------------------------ DynamicArray

    [Test]
    public static void DynamicArray_AddInsertRemove()
    {
        var a = new DynamicArray<string>(1);
        a.Add("b");
        a.Add("d");
        a.Insert(0, "a");
        a.Insert(2, "c");
        Assert.SequenceEqual(new[] { "a", "b", "c", "d" }, a.ToArray());
        Assert.True(a.Remove("b"));
        a.RemoveAt(0);
        Assert.SequenceEqual(new[] { "c", "d" }, a.ToArray());
        Assert.Equal(1, a.IndexOf("d"));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = a[2]);
    }

    // ------------------------------------------------------------------ HashMap

    [Test]
    public static void HashMap_PutGetOverwriteRemove()
    {
        var map = new HashMap<string, int>();
        map.Put("Boardwalk", 400);
        map["Park Place"] = 350;
        map.Put("Boardwalk", 401);
        Assert.Equal(2, map.Count);
        Assert.Equal(401, map["Boardwalk"]);
        Assert.True(map.Remove("Boardwalk"));
        Assert.False(map.ContainsKey("Boardwalk"));
        Assert.False(map.Remove("Boardwalk"));
        Assert.Throws<KeyNotFoundException>(() => map.Get("Boardwalk"));
    }

    [Test]
    public static void HashMap_ResizesAndKeepsAllEntries()
    {
        var map = new HashMap<int, int>(4);
        for (int i = 0; i < 1000; i++) map.Put(i, i * i);
        Assert.Equal(1000, map.Count);
        Assert.True(map.BucketCount >= 1000 / 0.75);
        for (int i = 0; i < 1000; i++) Assert.Equal(i * i, map.Get(i));
    }

    private sealed class CollidingComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => x == y;
        public int GetHashCode(string obj) => 42; // every key in one bucket
    }

    [Test]
    public static void HashMap_HandlesCollisionsByChaining()
    {
        var map = new HashMap<string, int>(8, new CollidingComparer());
        for (int i = 0; i < 20; i++) map.Put("k" + i, i);
        Assert.True(map.Remove("k7"));
        for (int i = 0; i < 20; i++)
        {
            if (i == 7) Assert.False(map.ContainsKey("k7"));
            else Assert.Equal(i, map.Get("k" + i));
        }
    }

    // ------------------------------------------------------------------ BinaryHeap

    [Test]
    public static void Heap_MinAndMaxOrdering()
    {
        var rng = new XorShiftRandom(7);
        var min = BinaryHeap<int>.MinHeap((a, b) => a.CompareTo(b));
        var max = BinaryHeap<int>.MaxHeap((a, b) => a.CompareTo(b));
        var values = new int[200];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = rng.Next(-1000, 1000);
            min.Push(values[i]);
            max.Push(values[i]);
        }
        Array.Sort(values);
        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal(values[i], min.Pop());
            Assert.Equal(values[values.Length - 1 - i], max.Pop());
        }
        Assert.True(min.IsEmpty);
        Assert.Throws<InvalidOperationException>(() => min.Pop());
    }

    // ------------------------------------------------------------------ CircularDoublyLinkedList

    private static CircularDoublyLinkedList<int> Ring(int n)
    {
        var list = new CircularDoublyLinkedList<int>();
        for (int i = 0; i < n; i++) list.AddLast(i);
        return list;
    }

    [Test]
    public static void Cdll_SingleNode_PointsToItself()
    {
        var list = Ring(1);
        var head = list.Head!;
        Assert.True(head.Next == head && head.Prev == head);
        var landed = list.Step(head, 5, out int wraps);
        Assert.True(landed == head);
        Assert.Equal(5, wraps);
    }

    [Test]
    public static void Cdll_Empty()
    {
        var list = new CircularDoublyLinkedList<int>();
        Assert.True(list.Head == null);
        Assert.Equal(0, list.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => list.NodeAt(0));
        foreach (var _ in list) throw new AssertionException("Empty list enumerated an item");
    }

    [Test]
    public static void Cdll_LinksAreCircularBothWays()
    {
        var list = Ring(40);
        var head = list.Head!;
        Assert.Equal(39, head.Prev.Value);
        Assert.Equal(0, head.Prev.Next.Value);
        var node = head;
        for (int i = 0; i < 40; i++) node = node.Next;
        Assert.True(node == head, "40 Next steps return to the head");
        for (int i = 0; i < 40; i++) node = node.Prev;
        Assert.True(node == head, "40 Prev steps return to the head");
    }

    [Test]
    public static void Cdll_StepCountsWrapsForwardOnly()
    {
        var list = Ring(40);
        var from = list.NodeAt(36);
        var to = list.Step(from, 7, out int wraps);
        Assert.Equal(3, to.Value);
        Assert.Equal(1, wraps);

        var exactlyOnHead = list.Step(from, 4, out wraps);
        Assert.Equal(0, exactlyOnHead.Value);
        Assert.Equal(1, wraps);

        var back = list.Step(list.NodeAt(2), -3, out wraps);
        Assert.Equal(39, back.Value);
        Assert.Equal(0, wraps);
    }

    [Test]
    public static void Cdll_FindNext_WrapsAroundTheRing()
    {
        var list = Ring(40);
        var found = list.FindNext(list.NodeAt(36), v => v % 10 == 5, out int distance);
        Assert.Equal(5, found!.Value);
        Assert.Equal(9, distance);
        Assert.True(list.FindNext(list.Head!, v => v > 100, out distance) == null);
        Assert.Equal(-1, distance);
    }

    [Test]
    public static void Cdll_NodeAtAndDistance()
    {
        var list = Ring(40);
        for (int i = 0; i < 40; i++) Assert.Equal(i, list.NodeAt(i).Value);
        Assert.Equal(5, list.ForwardDistance(list.NodeAt(38), list.NodeAt(3)));
        Assert.Equal(0, list.ForwardDistance(list.NodeAt(7), list.NodeAt(7)));
    }

    [Test]
    public static void Cdll_RemoveAndAddFirst_Reindex()
    {
        var list = Ring(4); // 0 1 2 3
        list.Remove(list.Head!);
        Assert.Equal(1, list.Head!.Value);
        Assert.Equal(0, list.Head.Index);
        list.AddFirst(9);
        var values = new List<int>(list);
        Assert.SequenceEqual(new[] { 9, 1, 2, 3 }, values);
        Assert.Equal(3, list.NodeAt(3).Index);
        Assert.Equal(3, list.Head.Prev.Value);
    }
}
