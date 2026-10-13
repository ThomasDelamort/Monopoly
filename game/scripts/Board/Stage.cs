using Godot;

namespace MonopolyGame;

/// <summary>Shared 3D surroundings: night-navy environment with glow, a key light with soft shadows,
/// the fading cyan grid floor and drifting dust motes.</summary>
public partial class Stage : Node3D
{
    public DirectionalLight3D Sun { get; private set; } = null!;

    public override void _Ready()
    {
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = Palette.Background,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("c4d2f0"),
            AmbientLightEnergy = 0.62f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            TonemapExposure = 1.05f,
            GlowEnabled = true,
            GlowIntensity = 0.55f,
            GlowBloom = 0.04f,
            GlowHdrThreshold = 0.9f,
            GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Softlight,
            AdjustmentEnabled = true,
            AdjustmentSaturation = 1.0f,
        };
        AddChild(new WorldEnvironment { Environment = environment });

        Sun = new DirectionalLight3D
        {
            LightEnergy = 1.15f,
            LightColor = new Color("fff3e0"),
            ShadowEnabled = true,
            ShadowBlur = 1.6f,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel2Splits,
            DirectionalShadowMaxDistance = 60f,
        };
        AddChild(Sun);
        Sun.LookAtFromPosition(new Vector3(-7, 11, 5), Vector3.Zero, Vector3.Up);

        var fill = new DirectionalLight3D { LightEnergy = 0.25f, LightColor = new Color("8fb8ff"), ShadowEnabled = false };
        AddChild(fill);
        fill.LookAtFromPosition(new Vector3(6, 5, -8), Vector3.Zero, Vector3.Up);

        var floorMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/grid_floor.gdshader") };
        floorMaterial.SetShaderParameter("base_color", Palette.Background);
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(160, 160) },
            MaterialOverride = floorMaterial,
            Position = new Vector3(0, -0.75f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });

        var dust = new CpuParticles3D
        {
            Amount = 70,
            Lifetime = 9,
            Preprocess = 9,
            Mesh = new QuadMesh { Size = new Vector2(0.06f, 0.06f) },
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(14, 3, 14),
            Direction = Vector3.Up,
            Spread = 60,
            Gravity = Vector3.Zero,
            InitialVelocityMin = 0.05f,
            InitialVelocityMax = 0.18f,
            Position = new Vector3(0, 2, 0),
            Color = new Color(0.55f, 0.85f, 1f, 0.55f),
        };
        dust.MaterialOverride = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        AddChild(dust);
    }
}
