using Godot;

namespace MonopolyGame;

/// <summary>
/// Character portraits for the HUD. Uses the Blender-rendered sprites in res://assets/portraits/*.png
/// when present; otherwise renders the 3D character live into a small transparent SubViewport.
/// </summary>
public static class Portraits
{
    private static readonly Texture2D?[] Cache = new Texture2D?[4];
    private static Node? _host;

    public static void Initialize(Node host) => _host = host;

    public static Texture2D Get(int characterId)
    {
        if (Cache[characterId] is { } cached) return cached;
        string png = $"res://assets/portraits/{CharacterFactory.Name(characterId).ToLowerInvariant()}.png";
        if (ResourceLoader.Exists(png)) return Cache[characterId] = GD.Load<Texture2D>(png);

        var viewport = new SubViewport
        {
            Size = new Vector2I(160, 160),
            TransparentBg = true,
            OwnWorld3D = true,
            Msaa3D = Viewport.Msaa.Msaa4X,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        _host!.AddChild(viewport);
        viewport.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.ClearColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("d0dcf5"),
                AmbientLightEnergy = 0.7f,
            },
        });
        var light = new DirectionalLight3D { LightEnergy = 1.2f };
        viewport.AddChild(light);
        light.LookAtFromPosition(new Vector3(-1, 2, 2), Vector3.Zero, Vector3.Up);

        var model = CharacterFactory.Create(characterId);
        viewport.AddChild(model);
        model.AddChild(new VoxelAnimator { Target = model });
        model.RotationDegrees = new Vector3(0, 22, 0);

        var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 0.95f };
        viewport.AddChild(camera);
        camera.LookAtFromPosition(new Vector3(0.35f, 0.95f, 2.2f), new Vector3(0, 0.66f, 0), Vector3.Up);

        return Cache[characterId] = viewport.GetTexture();
    }

    public static TextureRect Rect(int characterId, float size)
    {
        return new TextureRect
        {
            Texture = Get(characterId),
            CustomMinimumSize = new Vector2(size, size),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
    }
}
