using System;
using System.IO;
using System.Text.Json;
using Monopoly.Core.DataStructures;

namespace Monopoly.Core.Model;

/// <summary>
/// The 40-tile board, stored as a circular doubly linked list, plus hash-map indexes over it.
/// Loaded from the embedded Data/board.json so names, prices and rents are data, not code.
/// </summary>
public sealed class Board
{
    private Board(CircularDoublyLinkedList<Tile> tiles)
    {
        Tiles = tiles;
        ByName = new HashMap<string, CdllNode<Tile>>(64, StringComparer.OrdinalIgnoreCase) { TraceName = "Tile Index (HashMap)" };
        Groups = new HashMap<ColorGroup, DynamicArray<PropertyTile>>(16) { TraceName = "Color Groups (HashMap)" };

        var node = tiles.Head!;
        do
        {
            var tile = node.Value;
            // Duplicate names (Chance ×3, Community Chest ×3) keep the first occurrence; lookups by index use NodeAt.
            if (!ByName.ContainsKey(tile.Name)) ByName.Put(tile.Name, node);
            if (tile is PropertyTile property) Groups.GetOrAdd(property.Group, _ => new DynamicArray<PropertyTile>(3)).Add(property);
            if (tile is OwnableTile ownable) Ownables.Add(ownable);
            if (tile.Kind == TileKind.Jail) JailNode = node;
            node = node.Next;
        } while (node != tiles.Head);

        if (JailNode == null) throw new InvalidDataException("Board has no Jail tile.");
    }

    public CircularDoublyLinkedList<Tile> Tiles { get; }
    public HashMap<string, CdllNode<Tile>> ByName { get; }
    public HashMap<ColorGroup, DynamicArray<PropertyTile>> Groups { get; }
    public DynamicArray<OwnableTile> Ownables { get; } = new(28);

    public CdllNode<Tile> GoNode => Tiles.Head!;
    public CdllNode<Tile> JailNode { get; } = null!;
    public int Count => Tiles.Count;

    public CdllNode<Tile> NodeAt(int index) => Tiles.NodeAt(index);

    public int IndexOfKind(TileKind kind)
    {
        foreach (var tile in Tiles)
            if (tile.Kind == kind) return tile.Index;
        return -1;
    }

    public void SetTracer(IDsTracer? tracer)
    {
        Tiles.Tracer = tracer;
        Tiles.TraceName = "Board (Circular Doubly Linked List)";
        ByName.Tracer = tracer;
        Groups.Tracer = tracer;
        Groups.TraceReads = false; // rent/build checks read groups constantly
    }

    public DynamicArray<PropertyTile> PropertiesIn(ColorGroup group) => Groups.Get(group);

    public static Board LoadClassic() => FromJson(EmbeddedData.Read("board.json"));

    public static Board FromJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var list = new CircularDoublyLinkedList<Tile>();
        int index = 0;
        foreach (var element in document.RootElement.EnumerateArray())
        {
            string name = element.GetProperty("name").GetString()!;
            var kind = Enum.Parse<TileKind>(element.GetProperty("kind").GetString()!);
            Tile tile = kind switch
            {
                TileKind.Property => new PropertyTile(index, name,
                    Enum.Parse<ColorGroup>(element.GetProperty("group").GetString()!),
                    element.GetProperty("price").GetInt32(),
                    element.GetProperty("house").GetInt32(),
                    ReadInts(element.GetProperty("rent"))),
                TileKind.Railroad => new RailroadTile(index, name, element.GetProperty("price").GetInt32()),
                TileKind.Utility => new UtilityTile(index, name, element.GetProperty("price").GetInt32()),
                TileKind.Tax => new TaxTile(index, name, element.GetProperty("amount").GetInt32()),
                _ => new Tile(index, name, kind),
            };
            list.AddLast(tile);
            index++;
        }
        if (list.Count == 0) throw new InvalidDataException("Board JSON has no tiles.");
        return new Board(list);
    }

    private static int[] ReadInts(JsonElement array)
    {
        var values = new int[array.GetArrayLength()];
        int i = 0;
        foreach (var item in array.EnumerateArray()) values[i++] = item.GetInt32();
        return values;
    }
}

public static class CardLibrary
{
    public static (Card[] Chance, Card[] CommunityChest) LoadClassic() => FromJson(EmbeddedData.Read("cards.json"));

    public static (Card[] Chance, Card[] CommunityChest) FromJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return (ReadDeck(document.RootElement.GetProperty("chance"), DeckType.Chance),
                ReadDeck(document.RootElement.GetProperty("communityChest"), DeckType.CommunityChest));
    }

    private static Card[] ReadDeck(JsonElement array, DeckType deck)
    {
        var cards = new Card[array.GetArrayLength()];
        int i = 0;
        foreach (var element in array.EnumerateArray())
        {
            cards[i++] = new Card(deck,
                element.GetProperty("text").GetString()!,
                Enum.Parse<CardAction>(element.GetProperty("action").GetString()!),
                element.TryGetProperty("value", out var v) ? v.GetInt32() : 0,
                element.TryGetProperty("value2", out var v2) ? v2.GetInt32() : 0);
        }
        return cards;
    }
}

internal static class EmbeddedData
{
    public static string Read(string fileName)
    {
        var assembly = typeof(EmbeddedData).Assembly;
        using var stream = assembly.GetManifestResourceStream("Monopoly.Core.Data." + fileName)
            ?? throw new FileNotFoundException("Embedded resource missing: " + fileName);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
