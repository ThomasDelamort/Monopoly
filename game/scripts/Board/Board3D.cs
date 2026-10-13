using System;
using Godot;
using Monopoly.Core.Model;

namespace MonopolyGame;

/// <summary>
/// Builds the 3D board by walking the Core board's circular doubly linked list from the head (GO),
/// placing one tile per node with <see cref="BoardLayout"/>. Tile data (names, prices, colors) comes
/// from the list, models from the Blender exports when present (procedural low-poly otherwise).
/// </summary>
public partial class Board3D : Node3D
{
    private sealed class TileVisual
    {
        public required Node3D Root;
        public required MeshInstance3D Base;
        public required Node3D Buildings;
        public required MeshInstance3D OwnerStrip;
        public required MeshInstance3D Highlight;
        public Label3D? MortgageLabel;
        public int Houses;
    }

    private static Font? _font3D;
    private readonly Board _board;
    private readonly TileVisual?[] _tiles = new TileVisual?[40];

    public Board3D(Board board)
    {
        _board = board;
        Name = "Board3D";
    }

    public static Font Font3D => _font3D ??= new SystemFont
    {
        FontNames = new[] { "Bahnschrift", "Segoe UI", "Arial" },
        FontWeight = 700,
        MultichannelSignedDistanceField = true,
    };

    public static Vector3 DicePlaza => new(0, 0.09f, 0.2f);

    public override void _Ready()
    {
        BuildSlab();
        var head = _board.Tiles.Head!;
        var node = head;
        do
        {
            BuildTile(node.Value);
            node = node.Next;
        } while (node != head);
        BuildCenter();
    }

    // ------------------------------------------------------------------ public API

    public void SetOwner(int index, int? characterId)
    {
        var tile = _tiles[index];
        if (tile == null) return;
        tile.OwnerStrip.Visible = characterId.HasValue;
        if (characterId.HasValue) tile.OwnerStrip.MaterialOverride = Meshes.Mat(Palette.PlayerUi(characterId.Value), 0.4f, 0.8f);
    }

    public void SetMortgaged(int index, bool mortgaged)
    {
        var tile = _tiles[index];
        if (tile == null) return;
        tile.Base.MaterialOverride = Meshes.Mat(mortgaged ? new Color("8e9aa0") : Palette.TileBase);
        if (tile.MortgageLabel != null) tile.MortgageLabel.Visible = mortgaged;
    }

    public void SetBuildings(int index, int houses, bool animate)
    {
        var tile = _tiles[index];
        if (tile == null || tile.Houses == houses) return;
        tile.Houses = houses;
        foreach (var child in tile.Buildings.GetChildren()) child.QueueFree();
        if (houses == 0) return;
        if (houses == 5)
        {
            AddBuilding(tile.Buildings, hotel: true, Vector3.Zero, animate);
            return;
        }
        for (int i = 0; i < houses; i++) AddBuilding(tile.Buildings, hotel: false, new Vector3(-0.33f + i * 0.22f, 0, 0), animate && i == houses - 1);
    }

    public void Highlight(int index, bool on)
    {
        var tile = _tiles[index];
        if (tile != null) tile.Highlight.Visible = on;
    }

    public void ClearHighlights()
    {
        foreach (var tile in _tiles)
            if (tile != null) tile.Highlight.Visible = false;
    }

    // ------------------------------------------------------------------ slab & tiles

    private void BuildSlab()
    {
        float size = BoardLayout.Half * 2f;
        Meshes.Box(this, new Vector3(size, 0.6f, size), new Vector3(0, -0.3f, 0), Palette.Earth, "Slab");
        Meshes.Box(this, new Vector3(size + 0.02f, 0.12f, size + 0.02f), new Vector3(0, -0.06f, 0), Palette.GrassDark, "SlabTop");
        Meshes.Box(this, new Vector3(BoardLayout.InnerHalf * 2, 0.08f, BoardLayout.InnerHalf * 2), new Vector3(0, 0.04f, 0), Palette.Grass, "Grass");

        // "⇄" links on the side faces between neighbouring nodes: the Next/Prev pointers made visible.
        for (int i = 0; i < 40; i++)
        {
            if (i % 10 == 9) continue; // the link into a corner sits on the corner's own face
            var a = BoardLayout.Frame(i);
            var b = BoardLayout.Frame(i + 1);
            var mid = (a.Center + b.Center) / 2f;
            var outward = a.Basis * Vector3.Back;
            var label = new Label3D
            {
                Text = "⇄",
                Font = Font3D,
                FontSize = 64,
                PixelSize = 0.004f,
                Modulate = new Color(0.85f, 0.95f, 1f, 0.75f),
                Shaded = false,
                Position = mid + outward * (BoardLayout.Corner / 2 + 0.011f) + Vector3.Down * 0.3f,
            };
            label.Rotation = new Vector3(0, a.Yaw, 0);
            AddChild(label);
        }
    }

    private void BuildTile(Tile tile)
    {
        var frame = BoardLayout.Frame(tile.Index);
        var root = new Node3D { Name = $"Node{tile.Index:00}", Transform = frame.Transform };
        AddChild(root);
        var size = frame.Size;
        float inner = -size.Y / 2f, outer = size.Y / 2f;
        float top = BoardLayout.TileTop;

        var baseMesh = Meshes.Box(root, new Vector3(size.X - 0.035f, top, size.Y - 0.035f), new Vector3(0, top / 2, 0),
            frame.IsCorner ? Palette.TileCorner : Palette.TileBase, "Base");

        var highlight = Meshes.Box(root, new Vector3(size.X - 0.01f, 0.02f, size.Y - 0.01f), new Vector3(0, top + 0.012f, 0),
            Palette.Accent with { A = 0.35f }, "Highlight", emission: 1.2f);
        highlight.Visible = false;
        highlight.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        var ownerStrip = Meshes.Box(root, new Vector3(size.X - 0.16f, 0.025f, 0.09f), new Vector3(0, top + 0.012f, outer - 0.09f), Colors.White, "Owner");
        ownerStrip.Visible = false;

        var buildings = new Node3D { Name = "Buildings", Position = new Vector3(0, top + 0.012f, inner + BoardLayout.BandDepth / 2 + 0.02f) };
        root.AddChild(buildings);

        var visual = new TileVisual { Root = root, Base = baseMesh, Buildings = buildings, OwnerStrip = ownerStrip, Highlight = highlight };
        _tiles[tile.Index] = visual;
        var ink = Palette.TileInk;

        switch (tile)
        {
            case PropertyTile p:
                Meshes.Box(root, new Vector3(size.X - 0.035f, top + 0.014f, BoardLayout.BandDepth), new Vector3(0, (top + 0.014f) / 2, inner + BoardLayout.BandDepth / 2 + 0.018f),
                    Palette.Group(p.Group), "Band", emission: 0.12f);
                Text(root, p.Name.ToUpperInvariant(), new Vector3(0, top + 0.004f, inner + BoardLayout.BandDepth + 0.3f), 24, ink);
                Text(root, Ui.Money(p.Price), new Vector3(0, top + 0.004f, outer - 0.28f), 30, ink);
                break;
            case RailroadTile r:
                Text(root, r.Name.ToUpperInvariant(), new Vector3(0, top + 0.004f, inner + 0.26f), 22, ink);
                Icon(root, "railroad", new Vector3(0, top, 0.08f), BuildTrain);
                Text(root, Ui.Money(r.Price), new Vector3(0, top + 0.004f, outer - 0.28f), 30, ink);
                break;
            case UtilityTile u:
                bool electric = u.Name.Contains("Electric");
                Text(root, u.Name.ToUpperInvariant(), new Vector3(0, top + 0.004f, inner + 0.26f), 22, ink);
                Icon(root, electric ? "electric" : "water", new Vector3(0, top, 0.1f), electric ? BuildBulb : BuildFaucet);
                Text(root, Ui.Money(u.Price), new Vector3(0, top + 0.004f, outer - 0.28f), 30, ink);
                break;
            case TaxTile t:
                Text(root, t.Name.ToUpperInvariant(), new Vector3(0, top + 0.004f, inner + 0.26f), 24, ink);
                Icon(root, t.Amount >= 200 ? "income_tax" : "luxury_tax", new Vector3(0, top, 0.08f), t.Amount >= 200 ? BuildTaxStack : BuildRing);
                Text(root, "PAY " + Ui.Money(t.Amount), new Vector3(0, top + 0.004f, outer - 0.28f), 26, ink);
                break;
            default:
                BuildSpecial(root, tile, frame, ink);
                break;
        }

        if (tile is OwnableTile)
        {
            var mortgage = Text(root, "MORTGAGED", new Vector3(0, top + 0.03f, 0.1f), 30, new Color("d62d2d"), rotY: 30);
            mortgage.Visible = false;
            visual.MortgageLabel = mortgage;
        }
    }

    private void BuildSpecial(Node3D root, Tile tile, BoardLayout.TileFrame frame, Color ink)
    {
        float top = BoardLayout.TileTop + 0.004f;
        switch (tile.Kind)
        {
            case TileKind.Chance:
                Text(root, "CHANCE", new Vector3(0, top, -0.5f), 26, ink);
                Icon(root, "chance", new Vector3(0, BoardLayout.TileTop, 0.15f), parent =>
                    Text(parent, "?", new Vector3(0, 0.005f, 0), 150, new Color("f07f1a")));
                break;
            case TileKind.CommunityChest:
                Text(root, "COMMUNITY CHEST", new Vector3(0, top, -0.48f), 22, ink);
                Icon(root, "chest", new Vector3(0, BoardLayout.TileTop, 0.18f), BuildChest);
                break;
            case TileKind.Go:
                Text(root, "COLLECT $200 AS YOU PASS", new Vector3(-0.05f, top, -0.25f), 20, ink, 1.2f, rotY: 45);
                Text(root, "GO", new Vector3(0.05f, top, 0.05f), 120, new Color("d6262f"), 1.2f, rotY: 45);
                Icon(root, "go_arrow", new Vector3(0.2f, BoardLayout.TileTop, 0.5f), BuildGoArrow);
                break;
            case TileKind.Jail:
                Text(root, "JUST", new Vector3(0.62f, top, -0.1f), 22, ink, 0.6f, rotY: 90);
                Text(root, "VISITING", new Vector3(0.05f, top, 0.64f), 22, ink, 1.0f);
                Icon(root, "jail_cell", new Vector3(-0.27f, BoardLayout.TileTop, -0.27f), BuildJailCell);
                break;
            case TileKind.FreeParking:
                Text(root, "FREE", new Vector3(0, top, -0.45f), 34, ink, 1.2f, rotY: 45);
                Icon(root, "parking", new Vector3(0, BoardLayout.TileTop, 0.05f), BuildCar);
                Text(root, "PARKING", new Vector3(0.0f, top, 0.5f), 34, ink, 1.2f, rotY: 45);
                break;
            case TileKind.GoToJail:
                Text(root, "GO TO", new Vector3(0, top, -0.45f), 34, ink, 1.2f, rotY: 45);
                Icon(root, "police", new Vector3(0, BoardLayout.TileTop, 0.05f), BuildPoliceCap);
                Text(root, "JAIL", new Vector3(0.0f, top, 0.5f), 40, ink, 1.2f, rotY: 45);
                break;
        }
    }

    // ------------------------------------------------------------------ helpers

    private static Label3D Text(Node3D parent, string text, Vector3 position, int fontSize, Color color, float width = 0.86f, float rotY = 0)
    {
        var label = new Label3D
        {
            Text = text,
            Font = Font3D,
            FontSize = fontSize,
            PixelSize = 0.0042f,
            Modulate = color,
            OutlineSize = 0,
            Shaded = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Width = width / 0.0042f,
            Position = position,
            RotationDegrees = new Vector3(-90, rotY, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        parent.AddChild(label);
        return label;
    }

    /// <summary>Uses res://assets/models/props/icon_{name}.glb from Blender if it exists, otherwise the builder.</summary>
    private static void Icon(Node3D parent, string name, Vector3 position, Action<Node3D> fallback)
    {
        var holder = new Node3D { Name = "Icon_" + name, Position = position };
        parent.AddChild(holder);
        var model = Meshes.TryLoadModel($"res://assets/models/props/icon_{name}.glb");
        if (model != null) holder.AddChild(model);
        else fallback(holder);
    }

    private void AddBuilding(Node3D parent, bool hotel, Vector3 position, bool animate)
    {
        var model = Meshes.TryLoadModel(hotel ? "res://assets/models/props/hotel.glb" : "res://assets/models/props/house.glb");
        if (model == null)
        {
            model = new Node3D();
            var body = hotel ? new Color("d23a35") : new Color("3fae55");
            var roof = hotel ? new Color("8e1f1f") : new Color("24753a");
            var size = hotel ? new Vector3(0.62f, 0.2f, 0.22f) : new Vector3(0.17f, 0.14f, 0.17f);
            Meshes.Box(model, size, new Vector3(0, size.Y / 2, 0), body);
            var prism = Meshes.Prism(model, new Vector3(size.X + 0.03f, hotel ? 0.12f : 0.1f, size.Z + 0.03f), new Vector3(0, size.Y + (hotel ? 0.06f : 0.05f), 0), roof);
            prism.RotationDegrees = new Vector3(0, 90, 0);
            if (hotel) prism.Scale = new Vector3(size.Z / size.X + 0.1f, 1, size.X / size.Z);
        }
        model.Position = position;
        parent.AddChild(model);
        if (!animate) return;
        model.Scale = Vector3.One * 0.01f;
        var tween = model.CreateTween();
        tween.TweenProperty(model, "scale", Vector3.One, 0.35f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    // ------------------------------------------------------------------ procedural icon fallbacks

    private static void BuildTrain(Node3D p)
    {
        var black = new Color("2a2d36");
        Meshes.Box(p, new Vector3(0.42f, 0.13f, 0.18f), new Vector3(0, 0.1f, 0), black);
        Meshes.Box(p, new Vector3(0.14f, 0.12f, 0.18f), new Vector3(-0.14f, 0.22f, 0), black);
        Meshes.Cylinder(p, 0.04f, 0.05f, 0.1f, new Vector3(0.13f, 0.21f, 0), black);
        Meshes.Box(p, new Vector3(0.44f, 0.03f, 0.2f), new Vector3(0, 0.03f, 0), new Color("c43b3b"));
    }

    private static void BuildBulb(Node3D p)
    {
        var glass = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.12f, Height = 0.24f, RadialSegments = 10, Rings = 6 }, Position = new Vector3(0, 0.22f, 0), MaterialOverride = Meshes.Mat(new Color("ffe66b"), 0.4f, 1.4f) };
        p.AddChild(glass);
        Meshes.Cylinder(p, 0.06f, 0.06f, 0.1f, new Vector3(0, 0.07f, 0), new Color("9aa0a8"));
    }

    private static void BuildFaucet(Node3D p)
    {
        var steel = new Color("a8b3bd");
        Meshes.Cylinder(p, 0.05f, 0.05f, 0.22f, new Vector3(0, 0.11f, -0.05f), steel);
        Meshes.Box(p, new Vector3(0.07f, 0.07f, 0.22f), new Vector3(0, 0.2f, 0.05f), steel);
        Meshes.Box(p, new Vector3(0.18f, 0.03f, 0.04f), new Vector3(0, 0.25f, -0.05f), new Color("3a8fd8"));
        var drop = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 8, Rings = 4 }, Position = new Vector3(0, 0.08f, 0.14f), MaterialOverride = Meshes.Mat(new Color("4fb6ff"), 0.2f, 0.6f) };
        p.AddChild(drop);
    }

    private static void BuildTaxStack(Node3D p)
    {
        for (int i = 0; i < 3; i++) Meshes.Box(p, new Vector3(0.3f, 0.05f, 0.16f), new Vector3((i - 1) * 0.02f, 0.03f + i * 0.055f, 0), new Color("6fbf73"));
        Meshes.Box(p, new Vector3(0.06f, 0.16f, 0.17f), new Vector3(0, 0.09f, 0), new Color("f2f2e6"));
    }

    private static void BuildRing(Node3D p)
    {
        var band = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.09f, OuterRadius = 0.13f, Rings = 12, RingSegments = 6 }, Position = new Vector3(0, 0.15f, 0), RotationDegrees = new Vector3(90, 0, 0), MaterialOverride = Meshes.Mat(new Color("f1c232"), 0.3f) };
        p.AddChild(band);
        var gem = new MeshInstance3D { Mesh = new PrismMesh { Size = new Vector3(0.09f, 0.09f, 0.09f) }, Position = new Vector3(0, 0.31f, 0), RotationDegrees = new Vector3(180, 0, 0), MaterialOverride = Meshes.Mat(new Color("9ee8ff"), 0.1f, 0.8f) };
        p.AddChild(gem);
    }

    private static void BuildChest(Node3D p)
    {
        Meshes.Box(p, new Vector3(0.36f, 0.18f, 0.24f), new Vector3(0, 0.09f, 0), new Color("2f6fd0"));
        Meshes.Box(p, new Vector3(0.38f, 0.07f, 0.26f), new Vector3(0, 0.21f, 0), new Color("1f4fa0"));
        Meshes.Box(p, new Vector3(0.06f, 0.08f, 0.02f), new Vector3(0, 0.15f, 0.13f), new Color("f1c232"));
    }

    private static void BuildGoArrow(Node3D p)
    {
        var red = new Color("d6262f");
        Meshes.Box(p, new Vector3(0.7f, 0.03f, 0.12f), new Vector3(0.1f, 0.02f, 0), red);
        var head = Meshes.Prism(p, new Vector3(0.3f, 0.26f, 0.03f), new Vector3(-0.3f, 0.02f, 0), red);
        head.RotationDegrees = new Vector3(90, 0, 90);
        p.RotationDegrees = new Vector3(0, 45, 0);
    }

    private static void BuildJailCell(Node3D p)
    {
        Meshes.Box(p, new Vector3(0.95f, 0.02f, 0.95f), new Vector3(0, 0.01f, 0), new Color("f39223"));
        var bar = new Color("3a3d46");
        for (int i = 0; i < 6; i++)
        {
            float t = -0.42f + i * 0.168f;
            Meshes.Box(p, new Vector3(0.03f, 0.42f, 0.03f), new Vector3(t, 0.21f, 0.45f), bar);
            Meshes.Box(p, new Vector3(0.03f, 0.42f, 0.03f), new Vector3(0.45f, 0.21f, t), bar);
        }
        Meshes.Box(p, new Vector3(0.95f, 0.04f, 0.04f), new Vector3(0, 0.42f, 0.45f), bar);
        Meshes.Box(p, new Vector3(0.04f, 0.04f, 0.95f), new Vector3(0.45f, 0.42f, 0), bar);
    }

    private static void BuildCar(Node3D p)
    {
        Meshes.Box(p, new Vector3(0.44f, 0.1f, 0.22f), new Vector3(0, 0.09f, 0), new Color("d63a3a"));
        Meshes.Box(p, new Vector3(0.22f, 0.09f, 0.2f), new Vector3(-0.03f, 0.18f, 0), new Color("9fd8f5"));
        foreach (var x in new[] { -0.14f, 0.14f })
        foreach (var z in new[] { -0.11f, 0.11f })
        {
            var wheel = Meshes.Cylinder(p, 0.045f, 0.045f, 0.04f, new Vector3(x, 0.045f, z), new Color("22232a"), 10);
            wheel.RotationDegrees = new Vector3(90, 0, 0);
        }
        p.RotationDegrees = new Vector3(0, 45, 0);
    }

    private static void BuildPoliceCap(Node3D p)
    {
        Meshes.Cylinder(p, 0.2f, 0.2f, 0.08f, new Vector3(0, 0.12f, 0), new Color("1f3f8f"), 12);
        Meshes.Cylinder(p, 0.17f, 0.17f, 0.09f, new Vector3(0, 0.05f, 0), new Color("17306e"), 12);
        Meshes.Box(p, new Vector3(0.22f, 0.02f, 0.1f), new Vector3(0, 0.02f, 0.18f), new Color("101827"));
        Meshes.Box(p, new Vector3(0.07f, 0.07f, 0.02f), new Vector3(0, 0.1f, 0.18f), new Color("f1c232"));
    }

    // ------------------------------------------------------------------ center diorama

    private void BuildCenter()
    {
        var decor = Meshes.TryLoadModel("res://assets/models/board/board_center.glb");
        if (decor != null)
        {
            decor.Name = "Center";
            AddChild(decor);
            return;
        }

        var center = new Node3D { Name = "Center", Position = new Vector3(0, 0.08f, 0) };
        AddChild(center);
        float ring = 2.05f, road = 0.42f, inner = BoardLayout.InnerHalf;

        Meshes.Box(center, new Vector3(2.6f, 0.03f, 2.6f), new Vector3(0, 0.015f, 0.2f), new Color("d9efe6"), "Plaza");

        void Road(Vector3 size, Vector3 position)
        {
            Meshes.Box(center, size, position, Palette.Road);
            bool alongX = size.X > size.Z;
            float length = alongX ? size.X : size.Z;
            for (float d = -length / 2 + 0.2f; d < length / 2 - 0.1f; d += 0.42f)
                Meshes.Box(center, alongX ? new Vector3(0.18f, 0.012f, 0.035f) : new Vector3(0.035f, 0.012f, 0.18f),
                    position + (alongX ? new Vector3(d, 0.016f, 0) : new Vector3(0, 0.016f, d)), new Color("e8e4d0"));
        }
        Road(new Vector3(ring * 2 + road, 0.02f, road), new Vector3(0, 0.01f, -ring));
        Road(new Vector3(ring * 2 + road, 0.02f, road), new Vector3(0, 0.01f, ring + 0.2f));
        Road(new Vector3(road, 0.02f, ring * 2 + 0.2f), new Vector3(-ring, 0.01f, 0.1f));
        Road(new Vector3(road, 0.02f, ring * 2 + 0.2f), new Vector3(ring, 0.01f, 0.1f));
        Road(new Vector3(inner - ring - road / 2, 0.02f, road), new Vector3(-(ring + inner) / 2 - road / 4, 0.01f, -0.6f));
        Road(new Vector3(road, 0.02f, inner - ring - road / 2), new Vector3(0.7f, 0.01f, ring + 0.2f + (inner - ring) / 2));

        BuildVault(center, new Vector3(1.35f, 0, -3.2f));
        BuildApartment(center, new Vector3(3.15f, 0, 1.2f));
        var cage = Meshes.TryLoadModel("res://assets/models/props/jail_cage.glb");
        if (cage == null)
        {
            cage = new Node3D();
            BuildJailCell(cage);
        }
        cage.Position = new Vector3(-3.3f, 0, 3.3f);
        cage.Scale = Vector3.One * 1.3f;
        center.AddChild(cage);
        foreach (var (pos, yaw, color) in new[] { (new Vector3(-ring, 0.02f, -0.9f), 0f, new Color("4a8fe0")), (new Vector3(0.8f, 0.02f, -ring), 90f, new Color("f2c94c")), (new Vector3(ring, 0.02f, 1.4f), 0f, new Color("d63a3a")) })
        {
            var car = Meshes.TryLoadModel("res://assets/models/props/car.glb");
            if (car == null)
            {
                car = new Node3D();
                BuildCar(car);
                car.RotationDegrees = Vector3.Zero;
            }
            car.Position = pos;
            car.Scale = Vector3.One * 0.8f;
            car.RotationDegrees = new Vector3(0, yaw, 0);
            center.AddChild(car);
            Meshes.Recolor(car, "Body", color);
        }

        var houseSpots = new[] { new Vector3(-1.1f, 0, -2.75f), new Vector3(-2.75f, 0, -1.6f), new Vector3(-2.8f, 0, 1.2f), new Vector3(-1.2f, 0, 2.95f), new Vector3(1.6f, 0, 2.95f), new Vector3(2.8f, 0, -1.4f), new Vector3(-0.2f, 0, -2.8f) };
        foreach (var spot in houseSpots) BuildCottage(center, spot);

        var rng = new RandomNumberGenerator { Seed = 7 };
        int planted = 0;
        for (int attempt = 0; attempt < 600 && planted < 42; attempt++)
        {
            var pos = new Vector3(rng.RandfRange(-inner + 0.3f, inner - 0.3f), 0, rng.RandfRange(-inner + 0.3f, inner - 0.3f));
            if (Mathf.Abs(pos.X) < ring + 0.55f && Mathf.Abs(pos.Z - 0.1f) < ring + 0.75f) continue; // plaza + ring road
            if (Mathf.Abs(pos.Z + 0.6f) < 0.45f && pos.X < 0) continue; // west spoke
            if (Mathf.Abs(pos.X - 0.7f) < 0.45f && pos.Z > 0) continue; // south spoke
            if (pos.DistanceTo(new Vector3(1.35f, 0, -3.2f)) < 1.1f || pos.DistanceTo(new Vector3(3.15f, 0, 1.2f)) < 0.9f) continue;
            if (pos.DistanceTo(new Vector3(-3.3f, 0, 3.3f)) < 1.0f) continue;
            bool crowded = false;
            foreach (var spot in houseSpots) crowded |= pos.DistanceTo(spot) < 0.45f;
            if (crowded) continue;
            BuildTree(center, pos, rng.RandfRange(0.75f, 1.2f));
            planted++;
        }
    }

    private static void BuildTree(Node3D parent, Vector3 position, float scale)
    {
        var tree = Meshes.TryLoadModel("res://assets/models/props/pine.glb") ?? new Node3D();
        tree.Position = position;
        tree.Scale = Vector3.One * scale;
        tree.RotationDegrees = new Vector3(0, GD.Randf() * 360f, 0);
        parent.AddChild(tree);
        if (tree.GetChildCount() > 0) return;
        Meshes.Cylinder(tree, 0.05f, 0.05f, 0.18f, new Vector3(0, 0.09f, 0), new Color("6b4628"), 6);
        Meshes.Cylinder(tree, 0.3f, 0f, 0.42f, new Vector3(0, 0.36f, 0), new Color("2f8f46"), 7);
        Meshes.Cylinder(tree, 0.24f, 0f, 0.36f, new Vector3(0, 0.58f, 0), new Color("37a34f"), 7);
        Meshes.Cylinder(tree, 0.16f, 0f, 0.3f, new Vector3(0, 0.78f, 0), new Color("41b55a"), 7);
    }

    private static void BuildCottage(Node3D parent, Vector3 position)
    {
        var model = Meshes.TryLoadModel("res://assets/models/props/cottage.glb");
        if (model != null)
        {
            model.Position = position;
            parent.AddChild(model);
            return;
        }
        var house = new Node3D { Position = position };
        parent.AddChild(house);
        Meshes.Box(house, new Vector3(0.42f, 0.3f, 0.36f), new Vector3(0, 0.15f, 0), new Color("f1ede4"));
        var roof = Meshes.Prism(house, new Vector3(0.5f, 0.22f, 0.42f), new Vector3(0, 0.41f, 0), new Color("d9473f"));
        roof.RotationDegrees = new Vector3(0, 90, 0);
        roof.Scale = new Vector3(0.86f, 1, 1.17f);
        Meshes.Box(house, new Vector3(0.09f, 0.16f, 0.02f), new Vector3(0.06f, 0.08f, 0.185f), new Color("7a4a2c"));
        Meshes.Box(house, new Vector3(0.08f, 0.08f, 0.02f), new Vector3(-0.1f, 0.18f, 0.185f), new Color("9fd8f5"));
    }

    private static void BuildVault(Node3D parent, Vector3 position)
    {
        var vault = Meshes.TryLoadModel("res://assets/models/props/vault.glb");
        if (vault != null)
        {
            vault.Position = position;
            parent.AddChild(vault);
            return;
        }
        vault = new Node3D { Position = position };
        parent.AddChild(vault);
        var steel = new Color("8d939f");
        Meshes.Box(vault, new Vector3(1.4f, 1.3f, 1.3f), new Vector3(0, 0.65f, 0), steel);
        Meshes.Box(vault, new Vector3(1.0f, 0.95f, 0.04f), new Vector3(0, 0.65f, 0.66f), steel.Lightened(0.1f));
        var dial = Meshes.Cylinder(vault, 0.2f, 0.2f, 0.08f, new Vector3(-0.15f, 0.7f, 0.7f), new Color("3e434d"), 16);
        dial.RotationDegrees = new Vector3(90, 0, 0);
        var knob = Meshes.Cylinder(vault, 0.08f, 0.08f, 0.1f, new Vector3(-0.15f, 0.7f, 0.76f), new Color("c9ced6"), 12);
        knob.RotationDegrees = new Vector3(90, 0, 0);
        Meshes.Box(vault, new Vector3(0.06f, 0.36f, 0.06f), new Vector3(0.3f, 0.7f, 0.72f), new Color("c9ced6"));
        Meshes.Box(vault, new Vector3(1.46f, 0.08f, 1.36f), new Vector3(0, 1.32f, 0), steel.Darkened(0.15f));
    }

    private static void BuildApartment(Node3D parent, Vector3 position)
    {
        var model = Meshes.TryLoadModel("res://assets/models/props/apartment.glb");
        if (model != null)
        {
            model.Position = position;
            parent.AddChild(model);
            return;
        }
        var building = new Node3D { Position = position };
        parent.AddChild(building);
        Meshes.Box(building, new Vector3(0.8f, 1.5f, 0.8f), new Vector3(0, 0.75f, 0), new Color("c0574a"));
        Meshes.Box(building, new Vector3(0.86f, 0.08f, 0.86f), new Vector3(0, 1.52f, 0), new Color("8f3a33"));
        for (int floor = 0; floor < 4; floor++)
        for (int col = 0; col < 2; col++)
        {
            Meshes.Box(building, new Vector3(0.16f, 0.16f, 0.02f), new Vector3(-0.18f + col * 0.36f, 0.35f + floor * 0.3f, 0.405f), new Color("ffe59a"), emission: 0.5f);
            Meshes.Box(building, new Vector3(0.02f, 0.16f, 0.16f), new Vector3(0.405f, 0.35f + floor * 0.3f, -0.18f + col * 0.36f), new Color("ffe59a"), emission: 0.5f);
        }
    }
}
