using Godot;

namespace MonopolyGame;

/// <summary>
/// Procedural limb animation for a character with LegL/LegR/ArmL/ArmR/Head/Body nodes.
/// If the model carries baked clips (Blender export) with matching names, those are played instead.
/// </summary>
public partial class VoxelAnimator : Node
{
    public enum Mode
    {
        Idle,
        Walk,
        Cheer,
        Sad,
    }

    private Node3D? _legL, _legR, _armL, _armR, _head, _body;
    private AnimationPlayer? _player;
    private Mode _mode = Mode.Idle;
    private float _time;
    private readonly float _phase = GD.Randf() * 10f;

    public Node3D Target { get; set; } = null!;
    public float Speed { get; set; } = 1f;

    public override void _Ready()
    {
        _legL = Find("LegL");
        _legR = Find("LegR");
        _armL = Find("ArmL");
        _armR = Find("ArmR");
        _head = Find("Head");
        _body = Find("Body");
        _player = FindPlayer(Target);
        Play(Mode.Idle);
    }

    public void Play(Mode mode)
    {
        _mode = mode;
        if (_player == null) return;
        string clip = mode.ToString();
        if (_player.HasAnimation(clip))
        {
            _player.SpeedScale = Speed;
            _player.Play(clip, 0.15);
        }
    }

    public override void _Process(double delta)
    {
        if (_player != null && _player.HasAnimation(_mode.ToString())) return;
        _time += (float)delta * Speed;
        float t = _time + _phase;
        switch (_mode)
        {
            case Mode.Walk:
                float swing = Mathf.Sin(t * 12f) * 0.6f;
                SetX(_legL, swing);
                SetX(_legR, -swing);
                SetX(_armL, -swing * 0.8f);
                SetX(_armR, swing * 0.8f);
                SetZ(_armL, 0);
                SetZ(_armR, 0);
                SetX(_head, 0);
                break;
            case Mode.Cheer:
                float wave = Mathf.Sin(t * 10f) * 0.35f;
                SetX(_legL, 0);
                SetX(_legR, 0);
                SetX(_armL, Mathf.Pi * 0.95f);
                SetX(_armR, Mathf.Pi * 0.95f);
                SetZ(_armL, -0.25f + wave);
                SetZ(_armR, 0.25f - wave);
                SetX(_head, -0.15f);
                break;
            case Mode.Sad:
                SetX(_legL, 0);
                SetX(_legR, 0);
                SetX(_armL, 0.1f);
                SetX(_armR, 0.1f);
                SetZ(_armL, 0);
                SetZ(_armR, 0);
                SetX(_head, 0.45f);
                break;
            default:
                float breathe = Mathf.Sin(t * 2.2f);
                SetX(_legL, 0);
                SetX(_legR, 0);
                SetX(_armL, breathe * 0.08f);
                SetX(_armR, -breathe * 0.08f);
                SetZ(_armL, -0.05f);
                SetZ(_armR, 0.05f);
                SetX(_head, breathe * 0.04f);
                if (_body != null) _body.Scale = new Vector3(1, 1 + breathe * 0.015f, 1);
                break;
        }
    }

    private static void SetX(Node3D? node, float angle)
    {
        if (node != null) node.Rotation = node.Rotation with { X = angle };
    }

    private static void SetZ(Node3D? node, float angle)
    {
        if (node != null) node.Rotation = node.Rotation with { Z = angle };
    }

    private Node3D? Find(string name) => Target.FindChild(name, true, false) as Node3D;

    private static AnimationPlayer? FindPlayer(Node node)
    {
        if (node is AnimationPlayer player) return player;
        foreach (var child in node.GetChildren())
            if (FindPlayer(child) is { } found) return found;
        return null;
    }
}
