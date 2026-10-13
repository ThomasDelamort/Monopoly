using Godot;

namespace MonopolyGame;

/// <summary>
/// Root node: owns the shared 3D stage and camera, the UI layer, and the stack of screens.
/// </summary>
public partial class Main : Node
{
    public static Main Instance { get; private set; } = null!;

    public GameSettings Settings { get; private set; } = null!;
    public DebugOptions Debug { get; private set; } = null!;
    public Stage Stage { get; private set; } = null!;
    public CameraRig CameraRig { get; private set; } = null!;

    /// <summary>Parent for each screen's own 3D content.</summary>
    public Node3D World { get; private set; } = null!;

    public Control UiRoot { get; private set; } = null!;
    public ScreenStack Screens { get; private set; } = null!;

    public override void _Ready()
    {
        Instance = this;
        Debug = DebugOptions.Parse(OS.GetCmdlineUserArgs());
        Settings = GameSettings.Load();
        if (Debug.AnimationSpeedIndex.HasValue) Settings.AnimationSpeedIndex = Debug.AnimationSpeedIndex.Value;
        if (!Debug.IsAutomated) Settings.ApplyDisplay();

        AddChild(new Sfx { Name = "Sfx" });
        Stage = new Stage { Name = "Stage" };
        AddChild(Stage);
        World = new Node3D { Name = "World" };
        AddChild(World);
        CameraRig = new CameraRig { Name = "CameraRig" };
        AddChild(CameraRig);

        var layer = new CanvasLayer { Name = "UI" };
        AddChild(layer);
        UiRoot = new Control { Name = "UiRoot", Theme = Ui.CreateTheme(), MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddChild(UiRoot);
        UiRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        Screens = new ScreenStack(UiRoot);
        Portraits.Initialize(this);

        switch (Debug.StartScreen)
        {
            case "count":
                Screens.Push(new TitleScreen());
                Screens.Push(new PlayerCountScreen());
                break;
            case "seat":
                Screens.Push(new TitleScreen());
                Screens.Push(new PlayerCountScreen());
                Screens.Push(new SeatSetupScreen(Debug.Humans));
                break;
            case "game":
                Screens.Push(new GameScreen(GameScreen.SetupFromDebug(Debug, Settings)));
                break;
            case "settings":
                Screens.Push(new TitleScreen());
                Screens.Push(new SettingsScreen());
                break;
            default:
                Screens.Push(new TitleScreen());
                break;
        }

        if (Debug.ShotsDirectory != null || Debug.QuitAfter > 0) AddChild(new ScreenshotTaker(Debug));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Screens.Back();
            GetViewport().SetInputAsHandled();
        }
    }
}
