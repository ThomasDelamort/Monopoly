using Godot;
using Monopoly.Core.Rules;

namespace MonopolyGame;

/// <summary>
/// Four pedestals, one per seat, each with its character on a glowing ring and a "PLAYER: n" plate,
/// as in the character art. Seats 1..N are human, the rest bots. ◀ ▶ swaps characters between seats.
/// </summary>
public partial class SeatSetupScreen : Screen
{
    private const float Spacing = 2.7f;
    private readonly int _humans;
    private readonly int[] _characters = { 0, 1, 2, 3 };
    private readonly string[] _names = new string[4];
    private readonly Token3D?[] _tokens = new Token3D?[4];
    private readonly Vector3[] _pedestalTops = new Vector3[4];
    private readonly Control[] _columns = new Control[4];
    private readonly Label[] _characterLabels = new Label[4];
    private readonly Label?[] _botNames = new Label?[4];

    public SeatSetupScreen(int humans)
    {
        _humans = humans;
        for (int i = 0; i < 4; i++) _names[i] = i < humans ? $"Player {i + 1}" : BotName(i);
    }

    public override string Title => "Seats";

    private string BotName(int seat) => "Bot " + GameSetup.CharacterNames[_characters[seat]];

    public override void OnEnter()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var scene = CreateScene3D("SeatScene");
        var right = new Vector3(Mathf.Cos(Mathf.Pi / 4), 0, -Mathf.Sin(Mathf.Pi / 4)); // camera's screen-right at yaw 45°
        for (int seat = 0; seat < 4; seat++)
        {
            var center = right * ((seat - 1.5f) * Spacing);
            var pedestal = Meshes.TryLoadModel("res://assets/models/props/pedestal.glb") ?? BuildPedestal();
            pedestal.Position = center;
            scene.AddChild(pedestal);
            _pedestalTops[seat] = center + Vector3.Up * 0.62f;
            SpawnToken(seat);
        }

        var header = new PixelText("CHOOSE YOUR CHARACTERS", 5f, Palette.Accent);
        header.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
        header.Position = new Vector2(header.Position.X, 34);
        AddChild(header);
        var hint = Ui.Label($"{_humans} human{(_humans == 1 ? "" : "s")} + {4 - _humans} bot{(4 - _humans == 1 ? "" : "s")}", 20, Palette.TextDim, align: HorizontalAlignment.Center);
        hint.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
        hint.Position = new Vector2(hint.Position.X, 84);
        hint.GrowHorizontal = GrowDirection.Both;
        AddChild(hint);

        for (int seat = 0; seat < 4; seat++) AddChild(_columns[seat] = BuildColumn(seat));

        var bar = Ui.HBox(18,
            Ui.Button("◀  BACK", () => App.Screens.Back(), 20, new Vector2(200, 50)),
            Ui.Button("START GAME  ▶", StartGame, 24, new Vector2(300, 56)));
        bar.Alignment = BoxContainer.AlignmentMode.Center;
        bar.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom);
        bar.GrowHorizontal = GrowDirection.Both;
        bar.GrowVertical = GrowDirection.Begin;
        bar.Position = bar.Position with { Y = bar.Position.Y - 30 };
        AddChild(bar);
    }

    public override void OnSceneShown()
    {
        var rig = App.CameraRig;
        rig.UserControl = false;
        rig.OrbitSpeed = 0;
        rig.SetYaw(Mathf.Pi / 4, instant: true);
        rig.Focus(new Vector3(0, 1.4f, 0), 8.6f, instant: true);
        rig.Camera.HOffset = 0;
        rig.Camera.VOffset = -0.5f;
    }

    public override void OnExit()
    {
        base.OnExit();
        App.CameraRig.Camera.VOffset = 0;
    }

    public override void _Process(double delta)
    {
        // Keep each seat's UI column under its pedestal.
        var camera = App.CameraRig.Camera;
        for (int seat = 0; seat < 4; seat++)
        {
            var column = _columns[seat];
            if (column == null) continue;
            var anchor = camera.UnprojectPosition(_pedestalTops[seat] + Vector3.Down * 0.9f);
            column.Position = new Vector2(anchor.X - column.Size.X / 2, anchor.Y + 6);
        }
    }

    private Control BuildColumn(int seat)
    {
        bool human = seat < _humans;
        var box = Ui.VBox(8);
        box.CustomMinimumSize = new Vector2(250, 0);

        var plate = new FramePanel(new PixelText($"PLAYER: {seat + 1}", 4f, Palette.Accent), 14) { Chamfer = 10, BracketLength = 14 };
        plate.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        box.AddChild(plate);

        var badge = Ui.Label(human ? "HUMAN" : "BOT", 16, human ? Palette.Good : new Color("c7a6ff"), bold: true, align: HorizontalAlignment.Center);
        box.AddChild(badge);

        if (human)
        {
            var nameEdit = new LineEdit { Text = _names[seat], MaxLength = 14, Alignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(230, 40) };
            nameEdit.TextChanged += text => _names[seat] = text;
            nameEdit.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            box.AddChild(nameEdit);
        }
        else
        {
            var name = Ui.Label(_names[seat], 19, Palette.Text, align: HorizontalAlignment.Center);
            name.CustomMinimumSize = new Vector2(230, 40);
            _botNames[seat] = name;
            box.AddChild(name);
        }

        _characterLabels[seat] = Ui.Label(GameSetup.CharacterNames[_characters[seat]].ToUpperInvariant(), 18, Palette.Gold, bold: true, align: HorizontalAlignment.Center);
        _characterLabels[seat].CustomMinimumSize = new Vector2(120, 0);
        var picker = Ui.HBox(6,
            Ui.Button("◀", () => Cycle(seat, -1), 18, new Vector2(44, 40)),
            _characterLabels[seat],
            Ui.Button("▶", () => Cycle(seat, +1), 18, new Vector2(44, 40)));
        picker.Alignment = BoxContainer.AlignmentMode.Center;
        box.AddChild(picker);

        var frame = new FramePanel(box, 12) { Fill = new Color(0.07f, 0.11f, 0.2f, 0.72f) };
        frame.MouseFilter = MouseFilterEnum.Pass;
        return frame;
    }

    /// <summary>Gives the seat the next/previous character, swapping with whichever seat had it.</summary>
    private void Cycle(int seat, int direction)
    {
        int wanted = (_characters[seat] + direction + 4) % 4;
        int other = System.Array.IndexOf(_characters, wanted);
        (_characters[seat], _characters[other]) = (_characters[other], _characters[seat]);
        foreach (int s in new[] { seat, other })
        {
            SpawnToken(s);
            _characterLabels[s].Text = GameSetup.CharacterNames[_characters[s]].ToUpperInvariant();
            if (s >= _humans)
            {
                _names[s] = BotName(s);
                if (_botNames[s] != null) _botNames[s]!.Text = _names[s];
            }
        }
        Sfx.Play(Sfx.Kind.Hop);
    }

    private void SpawnToken(int seat)
    {
        _tokens[seat]?.QueueFree();
        var token = Token3D.Create(_characters[seat], 1.5f);
        Scene3D!.AddChild(token);
        token.Position = _pedestalTops[seat];
        token.Face(new Vector3(0.6f, 0, 1f), instant: true);
        token.CallDeferred(Token3D.MethodName.SetActive, true);
        _tokens[seat] = token;
    }

    private void StartGame()
    {
        var seats = new SeatConfig[4];
        for (int i = 0; i < 4; i++)
        {
            string name = string.IsNullOrWhiteSpace(_names[i]) ? (i < _humans ? $"Player {i + 1}" : BotName(i)) : _names[i].Trim();
            seats[i] = new SeatConfig(name, _characters[i], IsBot: i >= _humans);
        }
        var setup = new GameSetup { Seats = seats, Seed = 0, MaxRounds = App.Settings.RoundLimit };
        App.Screens.ResetTo(new GameScreen(setup));
    }

    /// <summary>Grass slab on a ring of stone blocks, like the character cards.</summary>
    private static Node3D BuildPedestal()
    {
        var root = new Node3D();
        Meshes.Box(root, new Vector3(1.9f, 0.22f, 1.9f), new Vector3(0, 0.51f, 0), Palette.Grass);
        Meshes.Box(root, new Vector3(1.92f, 0.06f, 1.92f), new Vector3(0, 0.39f, 0), Palette.GrassDark);
        for (int i = 0; i < 4; i++)
        for (int side = 0; side < 4; side++)
        {
            float t = -0.72f + i * 0.48f;
            var pos = side switch
            {
                0 => new Vector3(t, 0.18f, 0.72f),
                1 => new Vector3(t, 0.18f, -0.72f),
                2 => new Vector3(0.72f, 0.18f, t),
                _ => new Vector3(-0.72f, 0.18f, t),
            };
            Meshes.Box(root, new Vector3(0.45f, 0.36f, 0.45f), pos, Palette.Stone.Lightened((i + side) % 2 * 0.06f));
        }
        Meshes.Box(root, new Vector3(1.4f, 0.36f, 1.4f), new Vector3(0, 0.18f, 0), Palette.Stone.Darkened(0.1f));
        return root;
    }
}
