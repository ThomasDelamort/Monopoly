using Monopoly.Core.DataStructures;

namespace Monopoly.Core.Model;

public sealed class Player
{
    public Player(int id, string name, int characterId, bool isBot, int cash, CdllNode<Tile> start)
    {
        Id = id;
        Name = name;
        CharacterId = characterId;
        IsBot = isBot;
        Cash = cash;
        Position = start;
    }

    /// <summary>Seat number 0-3.</summary>
    public int Id { get; }
    public string Name { get; }
    public int CharacterId { get; }
    public bool IsBot { get; }

    public int Cash { get; internal set; }

    /// <summary>The board node the token stands on.</summary>
    public CdllNode<Tile> Position { get; internal set; }

    public bool InJail { get; internal set; }
    public int JailTurns { get; internal set; }
    public int ConsecutiveDoubles { get; internal set; }

    public bool IsBankrupt { get; internal set; }

    /// <summary>Turn number on which the player went bankrupt (ranks later bankruptcies higher).</summary>
    public int BankruptOnTurn { get; internal set; }

    public DynamicArray<OwnableTile> Properties { get; } = new();
    public DynamicArray<Card> JailCards { get; } = new();

    public int HouseCount
    {
        get
        {
            int count = 0;
            foreach (var p in Properties)
                if (p is PropertyTile { Houses: > 0 and < 5 } pt) count += pt.Houses;
            return count;
        }
    }

    public int HotelCount
    {
        get
        {
            int count = 0;
            foreach (var p in Properties)
                if (p is PropertyTile { HasHotel: true }) count++;
            return count;
        }
    }

    /// <summary>Cash + property value (half if mortgaged) + what was paid for buildings.</summary>
    public int NetWorth
    {
        get
        {
            int worth = Cash;
            foreach (var p in Properties)
            {
                worth += p.IsMortgaged ? p.Price - p.MortgageValue : p.Price;
                if (p is PropertyTile pt) worth += pt.Houses * pt.HouseCost;
            }
            return worth;
        }
    }

    public override string ToString() => Name;
}
