using Godot;
using Monopoly.Core.Rules;

namespace MonopolyGame;

/// <summary>
/// The four playable characters. Uses the Blender MCP exports in res://assets/models/characters/*.glb;
/// if a file is missing, builds an equivalent voxel figure from boxes so the game still runs.
/// Either way the figure is ~1 unit tall, faces +Z, and has nodes named LegL/LegR/ArmL/ArmR/Head/Body
/// with origins at the joints, which <see cref="VoxelAnimator"/> animates.
/// </summary>
public static class CharacterFactory
{
    public static string Name(int characterId) => GameSetup.CharacterNames[characterId];

    public static string ModelPath(int characterId) => $"res://assets/models/characters/{Name(characterId).ToLowerInvariant()}.glb";

    public static Node3D Create(int characterId)
    {
        var model = Meshes.TryLoadModel(ModelPath(characterId)) ?? BuildVoxel(characterId);
        model.Name = Name(characterId);
        return model;
    }

    private static readonly Color Skin = new("f2b994");
    private static readonly Color Eye = new("3a2620");
    private static readonly Color HairBrown = new("6b4026");
    private static readonly Color ShoeBrown = new("5a3524");
    private static readonly Color Navy = new("2e3a66");
    private static readonly Color White = new("eef2f4");

    private sealed record Outfit(Color Top, Color TopAccent, Color Pants, Color Shoes, Color Hair, Color Sleeve);

    private static Outfit OutfitFor(int id) => id switch
    {
        0 => new Outfit(new Color("c4333a"), White, Navy, ShoeBrown, HairBrown, new Color("c4333a")),
        1 => new Outfit(new Color("eab43a"), new Color("d8d8d8"), new Color("2f3d6e"), ShoeBrown, HairBrown, new Color("eab43a")),
        2 => new Outfit(new Color("2d3f7a"), new Color("c3343a"), new Color("3d3f48"), ShoeBrown, HairBrown, new Color("c3343a")),
        _ => new Outfit(new Color("c62f2f"), White, new Color("1d1d22"), new Color("15151a"), new Color("18181c"), new Color("c62f2f")),
    };

    private static Node3D BuildVoxel(int id)
    {
        var o = OutfitFor(id);
        var root = new Node3D();

        // Legs pivot at the hips.
        foreach (var (name, x) in new[] { ("LegL", -0.085f), ("LegR", 0.085f) })
        {
            var leg = new Node3D { Name = name, Position = new Vector3(x, 0.34f, 0) };
            root.AddChild(leg);
            Meshes.Box(leg, new Vector3(0.14f, 0.3f, 0.16f), new Vector3(0, -0.15f, 0), o.Pants);
            if (id == 3) Meshes.Box(leg, new Vector3(0.145f, 0.05f, 0.165f), new Vector3(0, -0.27f, 0), White); // white socks
            Meshes.Box(leg, new Vector3(0.15f, 0.07f, 0.22f), new Vector3(0, -0.305f, 0.03f), o.Shoes);
        }

        var body = new Node3D { Name = "Body", Position = new Vector3(0, 0.34f, 0) };
        root.AddChild(body);
        Meshes.Box(body, new Vector3(0.38f, 0.32f, 0.22f), new Vector3(0, 0.16f, 0), o.Top);
        Meshes.Box(body, new Vector3(0.12f, 0.2f, 0.01f), new Vector3(0, 0.2f, 0.112f), o.TopAccent); // shirt / zip / drawstrings
        if (id == 0) Meshes.Box(body, new Vector3(0.3f, 0.14f, 0.2f), new Vector3(0, -0.05f, -0.03f), o.Top); // tailcoat
        if (id == 1)
        {
            Meshes.Box(body, new Vector3(0.025f, 0.12f, 0.02f), new Vector3(-0.05f, 0.14f, 0.12f), White);
            Meshes.Box(body, new Vector3(0.025f, 0.12f, 0.02f), new Vector3(0.05f, 0.14f, 0.12f), White);
        }
        if (id == 2) Meshes.Box(body, new Vector3(0.3f, 0.3f, 0.12f), new Vector3(0, 0.17f, -0.17f), new Color("7b3fc4")); // backpack
        if (id == 3)
        {
            Meshes.Box(body, new Vector3(0.03f, 0.3f, 0.01f), new Vector3(-0.07f, 0.16f, 0.113f), new Color("141418"));
            Meshes.Box(body, new Vector3(0.03f, 0.3f, 0.01f), new Vector3(0.07f, 0.16f, 0.113f), new Color("141418"));
        }

        foreach (var (name, x) in new[] { ("ArmL", -0.25f), ("ArmR", 0.25f) })
        {
            var arm = new Node3D { Name = name, Position = new Vector3(x, 0.62f, 0) };
            root.AddChild(arm);
            Meshes.Box(arm, new Vector3(0.11f, 0.26f, 0.13f), new Vector3(0, -0.12f, 0), o.Sleeve);
            bool glove = id == 3 && name == "ArmR";
            Meshes.Box(arm, new Vector3(0.11f, 0.09f, 0.12f), new Vector3(0, -0.29f, 0), glove ? new Color("f4f4ff") : Skin, emission: glove ? 0.6f : 0f);
        }

        var head = new Node3D { Name = "Head", Position = new Vector3(0, 0.66f, 0) };
        root.AddChild(head);
        Meshes.Box(head, new Vector3(0.36f, 0.32f, 0.34f), new Vector3(0, 0.16f, 0), Skin);
        Meshes.Box(head, new Vector3(0.37f, 0.1f, 0.35f), new Vector3(0, 0.29f, -0.005f), o.Hair);
        Meshes.Box(head, new Vector3(0.37f, 0.24f, 0.08f), new Vector3(0, 0.18f, -0.14f), o.Hair);
        Meshes.Box(head, new Vector3(0.05f, 0.07f, 0.01f), new Vector3(-0.08f, 0.16f, 0.172f), Eye);
        Meshes.Box(head, new Vector3(0.05f, 0.07f, 0.01f), new Vector3(0.08f, 0.16f, 0.172f), Eye);
        Meshes.Box(head, new Vector3(0.04f, 0.05f, 0.05f), new Vector3(0, 0.1f, 0.19f), Skin.Darkened(0.08f));
        Meshes.Box(head, new Vector3(0.03f, 0.08f, 0.06f), new Vector3(-0.19f, 0.15f, 0), Skin.Darkened(0.05f));
        Meshes.Box(head, new Vector3(0.03f, 0.08f, 0.06f), new Vector3(0.19f, 0.15f, 0), Skin.Darkened(0.05f));

        switch (id)
        {
            case 0: // top hat with red band
                Meshes.Box(head, new Vector3(0.46f, 0.04f, 0.44f), new Vector3(0, 0.33f, 0), new Color("373f63"));
                Meshes.Box(head, new Vector3(0.3f, 0.06f, 0.3f), new Vector3(0, 0.38f, 0), new Color("b5262c"));
                Meshes.Box(head, new Vector3(0.3f, 0.26f, 0.3f), new Vector3(0, 0.54f, 0), new Color("373f63"));
                break;
            case 1:
            case 2: // backwards cap
                var cap = id == 1 ? new Color("4f9d5a") : new Color("4c63ad");
                Meshes.Box(head, new Vector3(0.39f, 0.13f, 0.37f), new Vector3(0, 0.36f, 0), cap);
                Meshes.Box(head, new Vector3(0.3f, 0.03f, 0.16f), new Vector3(0, 0.31f, -0.25f), cap.Darkened(0.1f));
                break;
            default: // fedora
                Meshes.Box(head, new Vector3(0.52f, 0.035f, 0.5f), new Vector3(0, 0.33f, 0), new Color("1e1e24"));
                Meshes.Box(head, new Vector3(0.34f, 0.05f, 0.32f), new Vector3(0, 0.37f, 0), new Color("3a3a44"));
                Meshes.Box(head, new Vector3(0.34f, 0.12f, 0.32f), new Vector3(0, 0.45f, 0), new Color("1e1e24"));
                Meshes.Box(head, new Vector3(0.39f, 0.18f, 0.06f), new Vector3(0, 0.13f, -0.17f), o.Hair); // longer hair
                break;
        }
        return root;
    }
}
