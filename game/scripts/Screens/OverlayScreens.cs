using Godot;
using Monopoly.Core.Model;
using Monopoly.Core.Rules;

namespace MonopolyGame;

public partial class PauseScreen : Screen
{
    private readonly GameScreen _game;

    public PauseScreen(GameScreen game)
    {
        _game = game;
    }

    public override string Title => "Pause";
    public override bool IsOverlay => true;

    public override void OnEnter()
    {
        var dim = new ColorRect { Color = new Color(0.03f, 0.05f, 0.1f, 0.6f) };
        AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var resume = Ui.Button("RESUME", () => App.Screens.Back(), 22, new Vector2(320, 52));
        var content = Ui.VBox(14,
            new PixelText("PAUSED", 7f, Palette.Accent),
            new Control { CustomMinimumSize = new Vector2(0, 6) },
            resume,
            Ui.Button("SETTINGS", () => App.Screens.Push(new SettingsScreen(overlay: true)), 20, new Vector2(320, 48)),
            Ui.Button("QUIT TO MAIN MENU", () => App.Screens.ResetTo(new TitleScreen()), 20, new Vector2(320, 48)),
            Ui.Button("QUIT GAME", () => GetTree().Quit(), 20, new Vector2(320, 48)));
        var center = new CenterContainer();
        center.AddChild(new FramePanel(content, 30));
        AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        resume.CallDeferred(Control.MethodName.GrabFocus);
    }
}

/// <summary>Final ranking (merge-sorted by net worth, bankrupt players last) with replay options.</summary>
public partial class GameOverScreen : Screen
{
    private readonly Monopoly.Core.Rules.Game _game;
    private readonly GameSetup _setup;

    public GameOverScreen(Monopoly.Core.Rules.Game game, GameSetup setup)
    {
        _game = game;
        _setup = setup;
    }

    public override string Title => "Game Over";
    public override bool IsOverlay => true;
    public override bool OnBack() => true;

    public override void OnEnter()
    {
        var dim = new ColorRect { Color = new Color(0.03f, 0.05f, 0.1f, 0.45f), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var ranking = _game.FinalRanking ?? _game.Standings(out _);
        var rows = Ui.VBox(10);
        for (int i = 0; i < ranking.Length; i++)
        {
            Player p = ranking[i];
            var place = Ui.Label(i == 0 ? "1ST" : i == 1 ? "2ND" : i == 2 ? "3RD" : "4TH", 22, i == 0 ? Palette.Gold : Palette.TextDim, bold: true);
            place.CustomMinimumSize = new Vector2(60, 0);
            var name = Ui.Label(p.Name, 24, Palette.PlayerUi(p.CharacterId), bold: true);
            name.CustomMinimumSize = new Vector2(260, 0);
            rows.AddChild(Ui.HBox(14,
                place,
                Portraits.Rect(p.CharacterId, i == 0 ? 72 : 56),
                name,
                Ui.Spacer(),
                Ui.Label(p.IsBankrupt ? $"bankrupt (turn {p.BankruptOnTurn})" : $"net worth {Ui.Money(p.NetWorth)}", 20, p.IsBankrupt ? Palette.Bad : Palette.Text)));
        }

        var buttons = Ui.HBox(16,
            Ui.Button("PLAY AGAIN", () => App.Screens.ResetTo(new GameScreen(new GameSetup { Seats = _setup.Seats, MaxRounds = _setup.MaxRounds })), 22, new Vector2(240, 54)),
            Ui.Button("MAIN MENU", () => App.Screens.ResetTo(new TitleScreen()), 22, new Vector2(240, 54)));
        buttons.Alignment = BoxContainer.AlignmentMode.Center;

        var content = Ui.VBox(16,
            new PixelText("GAME OVER", 7f, Palette.Gold),
            Ui.Label($"{_game.Winner?.Name} wins after {_game.TurnNumber} turns ({_game.Round} rounds)", 20, Palette.Text, align: HorizontalAlignment.Center),
            Ui.Label("Final ranking: stable merge sort by net worth, bankrupt players last", 15, Palette.TextDim, align: HorizontalAlignment.Center),
            rows,
            buttons);
        var frame = new FramePanel(content, 32) { CustomMinimumSize = new Vector2(720, 0) };
        var holder = Layout.Place(new CenterContainer(), LayoutPreset.CenterBottom, 0, 40);
        holder.AddChild(frame);
        AddChild(holder);
    }
}
