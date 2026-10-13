using Godot;

namespace MonopolyGame;

/// <summary>
/// Orthographic isometric camera on a pivot. Smoothly follows a focus point; Q/E turn the view a
/// quarter at a time, the mouse wheel zooms. Can also orbit slowly (title screen).
/// </summary>
public partial class CameraRig : Node3D
{
    private const float Pitch = -0.6f; // ≈ -34°
    private Vector3 _targetPosition;
    private float _targetYaw = Mathf.Pi / 4;
    private float _targetSize = 16f;
    private float _yaw = Mathf.Pi / 4;

    public Camera3D Camera { get; private set; } = null!;
    public bool UserControl { get; set; }
    public float OrbitSpeed { get; set; }
    public float MinSize { get; set; } = 5f;
    public float MaxSize { get; set; } = 22f;

    public override void _Ready()
    {
        Camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = _targetSize,
            Near = 0.1f,
            Far = 300f,
            Position = new Vector3(0, 0, 60),
        };
        AddChild(Camera);
        Rotation = new Vector3(Pitch, _yaw, 0);
    }

    public void Focus(Vector3 position, float size, bool instant = false)
    {
        _targetPosition = position;
        _targetSize = Mathf.Clamp(size, MinSize, MaxSize);
        if (!instant) return;
        Position = _targetPosition;
        Camera.Size = _targetSize;
    }

    public void SetYaw(float yaw, bool instant = false)
    {
        _targetYaw = yaw;
        if (instant) _yaw = yaw;
    }

    public void RotateStep(int direction) => _targetYaw += direction * Mathf.Pi / 2;

    public void Zoom(float factor) => _targetSize = Mathf.Clamp(_targetSize * factor, MinSize, MaxSize);

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (OrbitSpeed != 0) _targetYaw += OrbitSpeed * dt;
        float k = 1f - Mathf.Exp(-dt * 5f);
        _yaw = Mathf.Lerp(_yaw, _targetYaw, OrbitSpeed != 0 ? 1f : k);
        Rotation = new Vector3(Pitch, _yaw, 0);
        Position = Position.Lerp(_targetPosition, k);
        Camera.Size = Mathf.Lerp(Camera.Size, _targetSize, k);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!UserControl) return;
        if (@event is InputEventMouseButton { Pressed: true } mouse)
        {
            if (mouse.ButtonIndex == MouseButton.WheelUp) Zoom(0.9f);
            else if (mouse.ButtonIndex == MouseButton.WheelDown) Zoom(1.1f);
        }
        else if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.Q) RotateStep(-1);
            else if (key.Keycode == Key.E) RotateStep(1);
        }
    }

    /// <summary>Point on the board plane under a screen position.</summary>
    public Vector3? PickBoard(Vector2 screenPosition, float planeHeight = BoardLayout.TileTop)
    {
        var origin = Camera.ProjectRayOrigin(screenPosition);
        var direction = Camera.ProjectRayNormal(screenPosition);
        if (Mathf.Abs(direction.Y) < 1e-4f) return null;
        float t = (planeHeight - origin.Y) / direction.Y;
        return t < 0 ? null : origin + direction * t;
    }
}
