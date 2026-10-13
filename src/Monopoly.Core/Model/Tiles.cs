using System;

namespace Monopoly.Core.Model;

public enum TileKind
{
    Go,
    Property,
    Railroad,
    Utility,
    Tax,
    Chance,
    CommunityChest,
    Jail,
    FreeParking,
    GoToJail,
}

public enum ColorGroup
{
    None,
    Brown,
    LightBlue,
    Pink,
    Orange,
    Red,
    Yellow,
    Green,
    DarkBlue,
}

public class Tile
{
    public Tile(int index, string name, TileKind kind)
    {
        Index = index;
        Name = name;
        Kind = kind;
    }

    public int Index { get; }
    public string Name { get; }
    public TileKind Kind { get; }

    public override string ToString() => Name;
}

public abstract class OwnableTile : Tile
{
    protected OwnableTile(int index, string name, TileKind kind, int price) : base(index, name, kind)
    {
        Price = price;
    }

    public int Price { get; }
    public Player? Owner { get; internal set; }
    public bool IsMortgaged { get; internal set; }

    public int MortgageValue => Price / 2;

    /// <summary>Mortgage value plus 10% interest, rounded up.</summary>
    public int UnmortgageCost => (MortgageValue * 11 + 9) / 10;
}

public sealed class PropertyTile : OwnableTile
{
    public PropertyTile(int index, string name, ColorGroup group, int price, int houseCost, int[] rents)
        : base(index, name, TileKind.Property, price)
    {
        if (rents.Length != 6) throw new ArgumentException("Rent table needs 6 entries (base, 1-4 houses, hotel).", nameof(rents));
        Group = group;
        HouseCost = houseCost;
        Rents = rents;
    }

    public ColorGroup Group { get; }
    public int HouseCost { get; }

    /// <summary>Rent with 0..4 houses, then with a hotel (index 5).</summary>
    public int[] Rents { get; }

    /// <summary>0-4 houses; 5 means a hotel.</summary>
    public int Houses { get; internal set; }

    public bool HasHotel => Houses == 5;
}

public sealed class RailroadTile : OwnableTile
{
    public RailroadTile(int index, string name, int price) : base(index, name, TileKind.Railroad, price) { }
}

public sealed class UtilityTile : OwnableTile
{
    public UtilityTile(int index, string name, int price) : base(index, name, TileKind.Utility, price) { }
}

public sealed class TaxTile : Tile
{
    public TaxTile(int index, string name, int amount) : base(index, name, TileKind.Tax)
    {
        Amount = amount;
    }

    public int Amount { get; }
}
