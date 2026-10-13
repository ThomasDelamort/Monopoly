using System.Threading.Tasks;
using Godot;

namespace MonopolyGame;

/// <summary>
/// Two dice that tumble onto the center plaza and settle on the values the rules engine already rolled.
/// Face layout: 1 = +Y, 6 = −Y, 2 = +X, 5 = −X, 3 = +Z, 4 = −Z (opposite faces sum to 7).
/// </summary>
public partial class Dice3D : Node3D
{
    private const float Size = 0.46f;
    private readonly Node3D[] _dice = new Node3D[2];

    public override void _Ready()
    {
        for (int i = 0; i < 2; i++)
        {
            _dice[i] = Meshes.TryLoadModel("res://assets/models/props/die.glb") ?? BuildDie();
            _dice[i].Position = new Vector3(i == 0 ? -0.45f : 0.45f, Size / 2 + 0.1f, 0);
            AddChild(_dice[i]);
        }
        ShowFaces(5, 2, instant: true);
    }

    public async Task Roll(int a, int b, float speed)
    {
        float duration = 0.85f / speed;
        var rng = new RandomNumberGenerator();
        var tasks = new Task[2];
        for (int i = 0; i < 2; i++)
        {
            var die = _dice[i];
            var landing = new Vector3((i == 0 ? -0.42f : 0.42f) + rng.RandfRange(-0.15f, 0.15f), Size / 2 + 0.1f, rng.RandfRange(-0.3f, 0.3f));
            var final = new Quaternion(FaceBasis(i == 0 ? a : b).Rotated(Vector3.Up, rng.RandfRange(-0.6f, 0.6f)).Orthonormalized());
            var spinAxis = new Vector3(rng.RandfRange(-1, 1), rng.RandfRange(-1, 1), rng.RandfRange(-1, 1)).Normalized();
            if (spinAxis.LengthSquared() < 0.5f) spinAxis = Vector3.Right;
            float spinAngle = rng.RandfRange(8f, 14f);
            var start = landing + new Vector3(rng.RandfRange(-1.2f, 1.2f), 2.6f, rng.RandfRange(1.0f, 1.8f));
            tasks[i] = Tumble(die, start, landing, spinAxis, spinAngle, final, duration);
        }
        Sfx.Play(Sfx.Kind.Dice);
        await Task.WhenAll(tasks);
    }

    private async Task Tumble(Node3D die, Vector3 start, Vector3 landing, Vector3 spinAxis, float spinAngle, Quaternion final, float duration)
    {
        var tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(t =>
        {
            // Position: drop with a bounce. Rotation: decaying spin that ends exactly on the final face.
            float drop = Mathf.Min(t / 0.55f, 1f);
            var pos = start.Lerp(landing, 1 - (1 - drop) * (1 - drop));
            if (t > 0.55f) pos.Y += Mathf.Abs(Mathf.Sin((t - 0.55f) / 0.45f * Mathf.Pi * 2f)) * 0.25f * (1 - t);
            die.Position = pos;
            float remaining = 1 - Mathf.SmoothStep(0, 1, t);
            var spinNow = new Quaternion(spinAxis, spinAngle * remaining * remaining);
            die.Quaternion = (spinNow * final).Normalized();
        }), 0f, 1f, duration);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    public void ShowFaces(int a, int b, bool instant)
    {
        _dice[0].Basis = FaceBasis(a);
        _dice[1].Basis = FaceBasis(b);
    }

    private static Basis FaceBasis(int value) => value switch
    {
        1 => Basis.Identity,
        6 => new Basis(Vector3.Right, Mathf.Pi),
        2 => new Basis(Vector3.Back, Mathf.Pi / 2),
        5 => new Basis(Vector3.Back, -Mathf.Pi / 2),
        3 => new Basis(Vector3.Right, -Mathf.Pi / 2),
        _ => new Basis(Vector3.Right, Mathf.Pi / 2),
    };

    private static Node3D BuildDie()
    {
        var die = new Node3D();
        var body = new MeshInstance3D { Mesh = new BoxMesh { Size = Vector3.One * Size }, MaterialOverride = Meshes.Mat(new Color("f7f5f0"), 0.5f) };
        die.AddChild(body);
        var pipMesh = new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.04f, Height = 0.012f, RadialSegments = 10, Rings = 1 };
        var pipMat = Meshes.Mat(new Color("23232b"));
        void Face(Vector3 normal, Vector3 u, Vector3 v, int value)
        {
            float s = 0.115f;
            Vector2[] spots = value switch
            {
                1 => new[] { Vector2.Zero },
                2 => new[] { new Vector2(-1, -1), new Vector2(1, 1) },
                3 => new[] { new Vector2(-1, -1), Vector2.Zero, new Vector2(1, 1) },
                4 => new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) },
                5 => new[] { new Vector2(-1, -1), new Vector2(1, -1), Vector2.Zero, new Vector2(-1, 1), new Vector2(1, 1) },
                _ => new[] { new Vector2(-1, -1), new Vector2(-1, 0), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(1, 0), new Vector2(1, 1) },
            };
            foreach (var spot in spots)
            {
                var pip = new MeshInstance3D { Mesh = pipMesh, MaterialOverride = pipMat };
                // Cylinder axis (+Y) must point along the face normal.
                var basis = new Basis(u, normal, u.Cross(normal));
                pip.Transform = new Transform3D(basis.Orthonormalized(), normal * (Size / 2) + (u * spot.X + v * spot.Y) * s);
                die.AddChild(pip);
            }
        }
        Face(Vector3.Up, Vector3.Right, Vector3.Back, 1);
        Face(Vector3.Down, Vector3.Right, Vector3.Back, 6);
        Face(Vector3.Right, Vector3.Up, Vector3.Back, 2);
        Face(Vector3.Left, Vector3.Up, Vector3.Back, 5);
        Face(Vector3.Back, Vector3.Right, Vector3.Up, 3);
        Face(Vector3.Forward, Vector3.Right, Vector3.Up, 4);
        return die;
    }
}
