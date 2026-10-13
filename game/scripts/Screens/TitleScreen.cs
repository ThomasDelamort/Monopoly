using Godot;
using Monopoly.Core.Model;

namespace MonopolyGame;

public partial class TitleScreen : Screen
{
    public override string Title => "Title";

    public override void OnEnter()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var scene = CreateScene3D("TitleScene");
        scene.AddChild(new Board3D(Board.LoadClassic()));
        for (int i = 0; i < 4; i++)
        {
            var token = Token3D.Create(i, 1.0f);
            scene.AddChild(token);
            token.Position = new Vector3(-1.05f + i * 0.7f, 0.11f, 0.35f + (i % 2) * 0.25f);
            token.Face(new Vector3(1, 0, 1), true);
        }

        var menu = Ui.VBox(14);
        var logo = new PixelText("MONOPOLY", 11f, Palette.Accent);
        menu.AddChild(logo);
        menu.AddChild(Ui.Label("L O W - P O L Y   E D I T I O N", 22, Palette.Gold, bold: true, align: HorizontalAlignment.Center));
        menu.AddChild(Ui.Label("Data Structures & Algorithms in C#", 18, Palette.TextDim, align: HorizontalAlignment.Center));
        menu.AddChild(new Control { CustomMinimumSize = new Vector2(0, 18) });

        var play = Ui.Button("PLAY", () => App.Screens.Push(new PlayerCountScreen()), 26, new Vector2(340, 58));
        menu.AddChild(play);
        menu.AddChild(Ui.Button("SETTINGS", () => App.Screens.Push(new SettingsScreen()), 22, new Vector2(340, 50)));
        menu.AddChild(Ui.Button("QUIT", () => GetTree().Quit(), 22, new Vector2(340, 50)));
        menu.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        menu.AddChild(Ui.Label("Esc back · Q/E rotate · Wheel zoom · Tab DSA panel", 15, Palette.TextDim, align: HorizontalAlignment.Center));

        var frame = new FramePanel(menu, 28);
        var holder = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        holder.AddThemeConstantOverride("margin_left", 70);
        holder.SetAnchorsAndOffsetsPreset(LayoutPreset.LeftWide);
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.AddChild(frame);
        holder.AddChild(center);
        AddChild(holder);
        play.CallDeferred(Control.MethodName.GrabFocus);
    }

    public override void OnSceneShown()
    {
        var rig = App.CameraRig;
        rig.UserControl = false;
        rig.OrbitSpeed = 0.07f;
        rig.Focus(Vector3.Zero, 15f, instant: true);
        rig.Camera.HOffset = -3.6f;
        rig.Camera.VOffset = 0.4f;
    }

    public override void OnExit()
    {
        base.OnExit();
        App.CameraRig.OrbitSpeed = 0;
        App.CameraRig.Camera.HOffset = 0;
        App.CameraRig.Camera.VOffset = 0;
    }

    public override void OnCovered()
    {
        App.CameraRig.Camera.HOffset = 0;
    }

    public override void OnUncovered()
    {
        App.CameraRig.Camera.HOffset = -3.6f;
    }

    public override bool OnBack() => true; // nothing below the title
}
