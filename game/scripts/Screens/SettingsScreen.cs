using Godot;

namespace MonopolyGame;

public partial class SettingsScreen : Screen
{
    private readonly bool _overlay;

    public SettingsScreen(bool overlay = false)
    {
        _overlay = overlay;
    }

    public override string Title => "Settings";
    public override bool IsOverlay => _overlay;

    public override void OnEnter()
    {
        var dim = new ColorRect { Color = new Color(0.04f, 0.06f, 0.12f, _overlay ? 0.7f : 0.55f) };
        AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var s = App.Settings;
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 24);
        grid.AddThemeConstantOverride("v_separation", 14);

        void Row(string label, Control control)
        {
            grid.AddChild(Ui.Label(label, 20, Palette.Text));
            control.CustomMinimumSize = new Vector2(300, 40);
            grid.AddChild(control);
        }

        Row("Animation speed", Ui.Options(GameSettings.AnimationSpeedNames, s.AnimationSpeedIndex, i => s.AnimationSpeedIndex = i));
        Row("Bot thinking time", Ui.Options(GameSettings.BotDelayNames, s.BotDelayIndex, i => s.BotDelayIndex = i));
        Row("Round limit (new games)", Ui.Options(GameSettings.RoundLimitNames, s.RoundLimitIndex, i => s.RoundLimitIndex = i));

        var dsa = new CheckButton { ButtonPressed = s.ShowDsaPanel, Text = "Show" };
        dsa.Toggled += on => s.ShowDsaPanel = on;
        Row("DSA panel", dsa);

        var fullscreen = new CheckButton { ButtonPressed = s.Fullscreen, Text = "On" };
        fullscreen.Toggled += on =>
        {
            s.Fullscreen = on;
            s.ApplyDisplay();
        };
        Row("Fullscreen", fullscreen);

        var volume = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.05, Value = s.Volume };
        volume.ValueChanged += v =>
        {
            s.Volume = (float)v;
            s.ApplyDisplay();
        };
        Row("Volume", volume);

        var content = Ui.VBox(22,
            new PixelText("SETTINGS", 6f, Palette.Accent),
            grid,
            Ui.Button("DONE", () => App.Screens.Back(), 22, new Vector2(220, 50)));
        content.GetChild<Control>(2).SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

        var center = new CenterContainer();
        center.AddChild(new FramePanel(content, 30));
        AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public override void OnExit()
    {
        if (!App.Debug.IsAutomated && !App.Debug.AnimationSpeedIndex.HasValue) App.Settings.Save(); // test runs never persist
        base.OnExit();
    }
}
