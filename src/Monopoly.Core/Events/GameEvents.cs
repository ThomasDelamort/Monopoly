using Monopoly.Core.Model;

namespace Monopoly.Core.Events;

/// <summary>
/// Something that happened in the rules engine. The engine resolves a whole action synchronously and
/// enqueues these; the presentation layer dequeues them one by one (FIFO) and animates each.
/// </summary>
public abstract record GameEvent;

public sealed record TurnOrderDecidedEvent(Player[] Order, int[] RollsBySeat) : GameEvent;

public sealed record TurnStartedEvent(Player Player, int TurnNumber, int Round) : GameEvent;

public sealed record DiceRolledEvent(Player Player, int Die1, int Die2) : GameEvent
{
    public int Total => Die1 + Die2;
    public bool IsDoubles => Die1 == Die2;
}

/// <summary>Tile indices visited after leaving the start tile, in order. Direct = teleport (Go To Jail).</summary>
public sealed record MovedEvent(Player Player, int[] Path, bool Backward, bool Direct) : GameEvent;

public sealed record PassedGoEvent(Player Player, int Salary) : GameEvent;

/// <summary>From/To null means the bank.</summary>
public sealed record MoneyEvent(Player? From, Player? To, int Amount, string Reason) : GameEvent;

public sealed record PurchaseOfferedEvent(Player Player, OwnableTile Tile) : GameEvent;

public sealed record PropertyBoughtEvent(Player Player, OwnableTile Tile, int Price) : GameEvent;

public sealed record AuctionStartedEvent(OwnableTile Tile, Player[] Bidders) : GameEvent;

public sealed record AuctionBidEvent(Player Player, int Amount) : GameEvent;

public sealed record AuctionPassEvent(Player Player) : GameEvent;

public sealed record AuctionEndedEvent(OwnableTile Tile, Player? Winner, int Price) : GameEvent;

public sealed record CardDrawnEvent(Player Player, Card Card) : GameEvent;

public sealed record JailEvent(Player Player, bool Entered, string Reason) : GameEvent;

public sealed record BuildingChangedEvent(Player Owner, PropertyTile Tile, int Houses) : GameEvent;

public sealed record MortgageChangedEvent(Player Owner, OwnableTile Tile, bool Mortgaged) : GameEvent;

public sealed record OwnershipChangedEvent(OwnableTile Tile, Player? NewOwner) : GameEvent;

public sealed record DebtEvent(Player Debtor, Player? Creditor, int Amount, string Reason) : GameEvent;

public sealed record BankruptEvent(Player Player, Player? Creditor) : GameEvent;

public sealed record TurnEndedEvent(Player Player) : GameEvent;

public sealed record GameOverEvent(Player Winner, Player[] Ranking, string Reason) : GameEvent;

public sealed record LogEvent(string Text) : GameEvent;
