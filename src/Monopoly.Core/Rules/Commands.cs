using Monopoly.Core.Model;

namespace Monopoly.Core.Rules;

/// <summary>
/// A reversible property-management action. Executed commands are pushed on the game's undo stack
/// so a player can take back builds/mortgages until they roll, pay a debt or end the turn.
/// </summary>
public interface IGameCommand
{
    string Label { get; }
    Player Owner { get; }
    bool CanUndo(Game game);
    void Execute(Game game);
    void Undo(Game game);
}

internal sealed class BuildCommand : IGameCommand
{
    private readonly PropertyTile _tile;

    public BuildCommand(PropertyTile tile)
    {
        _tile = tile;
        Owner = tile.Owner!;
    }

    public Player Owner { get; }
    public string Label => _tile.Houses == 4 ? $"Build hotel on {_tile.Name}" : $"Build house on {_tile.Name}";

    public bool CanUndo(Game game) => game.IsManaging(Owner) && (!_tile.HasHotel || game.HousesInBank >= 4);
    public void Execute(Game game) => game.ChangeBuildings(_tile, +1, -_tile.HouseCost, $"Built on {_tile.Name}");
    public void Undo(Game game) => game.ChangeBuildings(_tile, -1, +_tile.HouseCost, $"Undo build on {_tile.Name}");
    public override string ToString() => Label;
}

internal sealed class SellBuildingCommand : IGameCommand
{
    private readonly PropertyTile _tile;

    public SellBuildingCommand(PropertyTile tile)
    {
        _tile = tile;
        Owner = tile.Owner!;
    }

    public Player Owner { get; }
    public string Label => _tile.HasHotel ? $"Sell hotel on {_tile.Name}" : $"Sell house on {_tile.Name}";

    public bool CanUndo(Game game) =>
        game.IsManaging(Owner) && Owner.Cash >= _tile.HouseCost / 2 &&
        (_tile.Houses == 4 ? game.HotelsInBank > 0 : game.HousesInBank > 0);

    public void Execute(Game game) => game.ChangeBuildings(_tile, -1, +_tile.HouseCost / 2, $"Sold building on {_tile.Name}");
    public void Undo(Game game) => game.ChangeBuildings(_tile, +1, -_tile.HouseCost / 2, $"Undo sale on {_tile.Name}");
    public override string ToString() => Label;
}

internal sealed class MortgageCommand : IGameCommand
{
    private readonly OwnableTile _tile;

    public MortgageCommand(OwnableTile tile)
    {
        _tile = tile;
        Owner = tile.Owner!;
    }

    public Player Owner { get; }
    public string Label => $"Mortgage {_tile.Name}";

    public bool CanUndo(Game game) => game.IsManaging(Owner) && Owner.Cash >= _tile.MortgageValue;
    public void Execute(Game game) => game.SetMortgaged(_tile, true, +_tile.MortgageValue, $"Mortgaged {_tile.Name}");
    public void Undo(Game game) => game.SetMortgaged(_tile, false, -_tile.MortgageValue, $"Undo mortgage of {_tile.Name}");
    public override string ToString() => Label;
}

internal sealed class UnmortgageCommand : IGameCommand
{
    private readonly OwnableTile _tile;
    private readonly int _paid;

    public UnmortgageCommand(OwnableTile tile)
    {
        _tile = tile;
        Owner = tile.Owner!;
        _paid = tile.UnmortgageCost;
    }

    public Player Owner { get; }
    public string Label => $"Unmortgage {_tile.Name}";

    public bool CanUndo(Game game) => game.IsManaging(Owner);
    public void Execute(Game game) => game.SetMortgaged(_tile, false, -_paid, $"Unmortgaged {_tile.Name}");
    public void Undo(Game game) => game.SetMortgaged(_tile, true, +_paid, $"Undo unmortgage of {_tile.Name}");
    public override string ToString() => Label;
}
