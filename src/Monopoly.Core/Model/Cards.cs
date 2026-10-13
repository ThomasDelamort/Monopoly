using System;
using Monopoly.Core.Algorithms;
using Monopoly.Core.DataStructures;

namespace Monopoly.Core.Model;

public enum DeckType
{
    Chance,
    CommunityChest,
}

public enum CardAction
{
    AdvanceTo,
    AdvanceToNearestRailroad,
    AdvanceToNearestUtility,
    MoveBack,
    GoToJail,
    Collect,
    Pay,
    PayEachPlayer,
    CollectFromEachPlayer,
    Repairs,
    GetOutOfJailFree,
}

public sealed class Card
{
    public Card(DeckType deck, string text, CardAction action, int value = 0, int value2 = 0)
    {
        Deck = deck;
        Text = text;
        Action = action;
        Value = value;
        Value2 = value2;
    }

    public DeckType Deck { get; }
    public string Text { get; }
    public CardAction Action { get; }

    /// <summary>Amount, steps, or target tile index, depending on <see cref="Action"/>.</summary>
    public int Value { get; }

    /// <summary>Per-hotel cost for <see cref="CardAction.Repairs"/>.</summary>
    public int Value2 { get; }

    public override string ToString() => Text;
}

/// <summary>
/// A card deck is a queue, exactly like the physical game: draw from the top (Dequeue)
/// and slide the card back under the bottom (Enqueue). Get-Out-Of-Jail-Free cards are kept
/// by the player and only re-enqueued when used.
/// </summary>
public sealed class Deck
{
    private readonly ArrayQueue<Card> _cards;

    public Deck(DeckType type, Card[] cards, IRandom random, IDsTracer? tracer)
    {
        Type = type;
        var shuffled = (Card[])cards.Clone();
        Shuffle.FisherYates<Card>(shuffled, random);
        _cards = new ArrayQueue<Card>(Math.Max(1, shuffled.Length));
        foreach (var card in shuffled) _cards.Enqueue(card);
        _cards.Tracer = tracer;
        _cards.TraceName = type == DeckType.Chance ? "Chance Deck (Queue)" : "Community Chest (Queue)";
    }

    public DeckType Type { get; }
    public int Count => _cards.Count;

    public Card Draw() => _cards.Dequeue();

    public void ReturnToBottom(Card card) => _cards.Enqueue(card);

    public Card[] ToArray() => _cards.ToArray();

    /// <summary>Test hook: moves the first matching card to the top, keeping the rest in order.</summary>
    internal void MoveToTop(Predicate<Card> match)
    {
        var cards = _cards.ToArray();
        int index = Array.FindIndex(cards, match);
        if (index < 0) throw new ArgumentException("No such card in the deck.");
        _cards.Clear();
        _cards.Enqueue(cards[index]);
        for (int i = 0; i < cards.Length; i++)
            if (i != index) _cards.Enqueue(cards[i]);
    }
}
