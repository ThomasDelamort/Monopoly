using System.Threading.Tasks;
using Godot;

namespace MonopolyGame;

/// <summary>A character standing on a glowing ring, with sparkles while it is the active player.</summary>
public partial class Token3D : Node3D
{
    private static Shader? _ringShader;

    private Node3D _model = null!;
    private VoxelAnimator _animator = null!;
    private ShaderMaterial _ringMaterial = null!;
    private CpuParticles3D _sparkles = null!;
    private float _faceYaw;

    public int CharacterId { get; private set; }
    public float ModelScale { get; set; } = 0.72f;

    public static Token3D Create(int characterId, float scale = 0.72f)
    {
        var token = new Token3D { CharacterId = characterId, ModelScale = scale, Name = "Token_" + CharacterFactory.Name(characterId) };
        return token;
    }

    public override void _Ready()
    {
        _model = CharacterFactory.Create(CharacterId);
        _model.Scale = Vector3.One * ModelScale;
        AddChild(_model);
        _animator = new VoxelAnimator { Target = _model };
        AddChild(_animator);

        _ringShader ??= GD.Load<Shader>("res://shaders/glow_ring.gdshader");
        var (a, b) = Palette.Ring(CharacterId);
        _ringMaterial = new ShaderMaterial { Shader = _ringShader };
        _ringMaterial.SetShaderParameter("color_a", a);
        _ringMaterial.SetShaderParameter("color_b", b);
        var ring = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(1, 1) * ModelScale * 1.15f },
            MaterialOverride = _ringMaterial,
            Position = new Vector3(0, 0.012f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(ring);

        _sparkles = new CpuParticles3D
        {
            Emitting = false,
            Amount = 14,
            Lifetime = 1.4,
            Mesh = new QuadMesh { Size = new Vector2(0.05f, 0.05f) },
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Ring,
            EmissionRingRadius = 0.42f * ModelScale,
            EmissionRingInnerRadius = 0.36f * ModelScale,
            EmissionRingHeight = 0.02f,
            EmissionRingAxis = Vector3.Up,
            Direction = Vector3.Up,
            Spread = 25,
            Gravity = Vector3.Zero,
            InitialVelocityMin = 0.25f,
            InitialVelocityMax = 0.6f,
            ScaleAmountMin = 0.6f,
            ScaleAmountMax = 1.3f,
            Color = b.Lerp(Colors.White, 0.4f),
        };
        _sparkles.MaterialOverride = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(1, 1, 1, 0.9f),
        };
        AddChild(_sparkles);
    }

    public float AnimationSpeed
    {
        set => _animator.Speed = value;
    }

    public void SetActive(bool active)
    {
        _ringMaterial.SetShaderParameter("active", active ? 1f : 0f);
        _sparkles.Emitting = active;
    }

    public void Play(VoxelAnimator.Mode mode) => _animator.Play(mode);

    public void Face(Vector3 direction, bool instant = false)
    {
        direction.Y = 0;
        if (direction.LengthSquared() < 1e-4f) return;
        _faceYaw = Mathf.Atan2(direction.X, direction.Z);
        if (instant) _model.Rotation = _model.Rotation with { Y = _faceYaw };
    }

    public override void _Process(double delta)
    {
        float y = Mathf.LerpAngle(_model.Rotation.Y, _faceYaw, (float)Mathf.Min(1.0, delta * 12));
        _model.Rotation = _model.Rotation with { Y = y };
    }

    /// <summary>One arc jump to the next node of the board.</summary>
    public async Task Hop(Vector3 target, float duration)
    {
        var start = Position;
        Face(target - start);
        var tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(t =>
        {
            var p = start.Lerp(target, t);
            p.Y += Mathf.Sin(t * Mathf.Pi) * 0.32f;
            Position = p;
            float squash = 1f - Mathf.Sin(t * Mathf.Pi) * 0.08f;
            _model.Scale = new Vector3(ModelScale / Mathf.Sqrt(squash), ModelScale * (2f - squash), ModelScale / Mathf.Sqrt(squash));
        }), 0f, 1f, duration).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        await ToSignal(tween, Tween.SignalName.Finished);
        _model.Scale = Vector3.One * ModelScale;
    }

    /// <summary>Shrink away and reappear somewhere else (Go To Jail).</summary>
    public async Task Teleport(Vector3 target, float duration)
    {
        var tween = CreateTween();
        tween.TweenProperty(_model, "scale", Vector3.One * 0.01f, duration * 0.45f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
        await ToSignal(tween, Tween.SignalName.Finished);
        Position = target;
        tween = CreateTween();
        tween.TweenProperty(_model, "scale", Vector3.One * ModelScale, duration * 0.55f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    /// <summary>Slides to a new slot on the same tile (when tokens rearrange).</summary>
    public void SlideTo(Vector3 target, float duration)
    {
        var tween = CreateTween();
        tween.TweenProperty(this, "position", target, duration).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    public async Task FallOut(float duration)
    {
        Play(VoxelAnimator.Mode.Sad);
        var tween = CreateTween();
        tween.TweenProperty(this, "position", Position + Vector3.Down * 0.15f, duration);
        tween.Parallel().TweenProperty(_model, "scale", Vector3.One * ModelScale * 0.85f, duration);
        await ToSignal(tween, Tween.SignalName.Finished);
        SetActive(false);
        _ringMaterial.SetShaderParameter("intensity", 0.3f);
    }
}
