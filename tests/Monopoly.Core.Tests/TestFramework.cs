using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Monopoly.Core.Algorithms;

namespace Monopoly.Core.Tests;

/// <summary>Marks a public static or instance method with no parameters as a test.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute { }

public sealed class AssertionException : Exception
{
    public AssertionException(string message) : base(message) { }
}

public static class Assert
{
    public static void True(bool condition, string message = "Expected true")
    {
        if (!condition) throw new AssertionException(message);
    }

    public static void False(bool condition, string message = "Expected false") => True(!condition, message);

    public static void Equal<T>(T expected, T actual, string? message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new AssertionException($"{message ?? "Values differ"}: expected <{expected}> but was <{actual}>");
    }

    public static void SequenceEqual<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string? message = null)
    {
        if (expected.Count != actual.Count)
            throw new AssertionException($"{message ?? "Sequences differ"}: length {expected.Count} vs {actual.Count}");
        for (int i = 0; i < expected.Count; i++)
            if (!EqualityComparer<T>.Default.Equals(expected[i], actual[i]))
                throw new AssertionException($"{message ?? "Sequences differ"} at [{i}]: expected <{expected[i]}> but was <{actual[i]}>");
    }

    public static void Near(double expected, double actual, double tolerance, string? message = null)
    {
        if (Math.Abs(expected - actual) > tolerance)
            throw new AssertionException($"{message ?? "Values differ"}: expected {expected} ± {tolerance} but was {actual}");
    }

    public static TException Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            throw new AssertionException($"Expected {typeof(TException).Name} but got {ex.GetType().Name}: {ex.Message}");
        }
        throw new AssertionException($"Expected {typeof(TException).Name} but nothing was thrown");
    }
}

/// <summary>Dice that return a fixed script, then fall back to a seeded generator.</summary>
public sealed class ScriptedDice : IRandom
{
    private readonly int[] _values;
    private readonly XorShiftRandom _fallback = new(12345);
    private int _next;

    public ScriptedDice(params int[] values)
    {
        _values = values;
    }

    public int Next(int minInclusive, int maxExclusive) =>
        _next < _values.Length ? _values[_next++] : _fallback.Next(minInclusive, maxExclusive);
}

public static class Program
{
    public static int Main(string[] args)
    {
        string? filter = args.Length > 0 ? args[0] : null;
        int passed = 0, failed = 0;
        var total = Stopwatch.StartNew();

        foreach (var type in typeof(Program).Assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.GetCustomAttribute<TestAttribute>() == null) continue;
                string name = $"{type.Name}.{method.Name}";
                if (filter != null && !name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;

                var watch = Stopwatch.StartNew();
                try
                {
                    object? instance = method.IsStatic ? null : Activator.CreateInstance(type);
                    method.Invoke(instance, null);
                    passed++;
                    Console.WriteLine($"  PASS  {name} ({watch.ElapsedMilliseconds} ms)");
                }
                catch (TargetInvocationException ex)
                {
                    failed++;
                    var inner = ex.InnerException ?? ex;
                    Console.WriteLine($"  FAIL  {name}: {inner.GetType().Name}: {inner.Message}");
                    if (inner is not AssertionException) Console.WriteLine(inner.StackTrace);
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"{passed} passed, {failed} failed in {total.ElapsedMilliseconds} ms");
        return failed == 0 ? 0 : 1;
    }
}
