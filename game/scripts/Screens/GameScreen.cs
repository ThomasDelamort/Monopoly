using System;
using System.Threading.Tasks;
using Godot;
using Monopoly.Core.AI;
using Monopoly.Core.Events;
using Monopoly.Core.Model;
using Monopoly.Core.Rules;

namespace MonopolyGame;

/// <summary>
/// Plays a game. The rules engine resolves each action instantly and enqueues events; this screen's
/// loop dequeues them one at a time (FIFO) and animates them, then asks the bot or waits for the human.
/// </summary>
public partial class GameScreen : Screen
{
    private readonly GameSetup _setup;
    private readonly Token3D?[] _tokens = new Token3D?[4];
    private readonly int[] _shownCash = new int[4];
    private Game _game = null!;
    private BotBrain _bot = null!;
    private Board3D _board = null!;
    private Dice3D _dice = null!;
    private TaskCompletionSource? _human;
    private bool _running;
    private bool _paused;
    private bool _overview;
    private int _focusedPlayer = -1;

    private PlayerCard _playerCard = null!;
    private QueuePanel _queue = null!;
    private StandingsPanel _standings = null!;
    private DsaPanel _dsa = null!;
    private Banner _banner = null!;
    private EventLogPanel _log = null!;
    private ActionBar _actions = null!;
    private DeedCard _deed = null!;
    private CardPopup _card = null!;
    private AuctionPanel _auction = null!;
    private ManagePanel _manage = null!;
    private Control _modalDim = null!;

    public GameScreen(GameSetup setup)
    {
        _setup = setup;
    }

    public override string Title => "Game";
    public Game Game => _game;
    public bool AwaitingHuman => _human != null;
    private float Speed => App.Settings.AnimationSpeed;
    private bool Autoplay => App.Debug.Autoplay;

    public static GameSetup SetupFromDebug(DebugOptions debug, GameSettings settings)
    {
        var seats = debug.Autoplay ? GameSetup.AllBots(debug.Seed).Seats : GameSetup.ForHumans(debug.Humans, debug.Seed).Seats;
        return new GameSetup { Seats = seats, Seed = debug.Seed, MaxRounds = debug.Rounds > 0 ? debug.Rounds : settings.RoundLimit };
    }

    // ======================================================================================= lifecycle

    public override void OnEnter()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _game = new Game(_setup);
        _bot = new BotBrain(_game);
        foreach (var p in _game.Players) _shownCash[p.Id] = p.Cash;
        Build3D();
        BuildHud();
        foreach (int index in App.Debug.Give)
            if (_game.Board.NodeAt(index).Value is OwnableTile deed && deed.Owner == null) _game.ForceGive(deed, _game.Players[0]);
        _game.Start();
        RunLoop();
    }

    public override void OnSceneShown()
    {
        var rig = App.CameraRig;
        rig.UserControl = true;
        rig.OrbitSpeed = 0;
        rig.Camera.HOffset = 0;
        rig.Camera.VOffset = 0;
        rig.SetYaw(Mathf.Pi / 4, instant: true);
        rig.Focus(Vector3.Zero, 16f, instant: true);
    }

    public override void OnExit()
    {
        _running = false;
        _human?.TrySetResult();
        App.CameraRig.UserControl = false;
        base.OnExit();
    }

    public override void OnCovered() => _paused = true;
    public override void OnUncovered() => _paused = false;

    public override bool OnBack()
    {
        if (_manage.Visible)
        {
            CloseManage();
            return true;
        }
        if (_game.Phase != Phase.GameOver) App.Screens.Push(new PauseScreen(this));
        return true;
    }

    // ======================================================================================= main loop

    private async void RunLoop()
    {
        _running = true;
        try
        {
            await Wait(0.4f);
            while (_running)
            {
                while (_paused && _running) await NextFrame();
                if (!_running) return;

                if (_game.Events.Count > 0)
                {
                    await Present(_game.Events.Dequeue());
                    continue;
                }

                RefreshHud();
                if (_game.Phase == Phase.GameOver)
                {
                    await Wait(1.2f);
                    if (_running) App.Screens.Push(new GameOverScreen(_game, _setup));
                    return;
                }

                var actor = _game.Actor;
                if (actor.IsBot || Autoplay)
                {
                    await Wait(App.Settings.BotDelay);
                    while (_paused && _running) await NextFrame();
                    if (!_running) return;
                    _bot.Act();
                    continue;
                }

                _human = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                RefreshHud();
                if (App.Debug.Script.Count > 0) RunScriptStep();
                await _human.Task;
            }
        }
        catch (Exception e)
        {
            GD.PushError(e.ToString());
            _banner.Show("Something went wrong", e.Message, Palette.Bad, 6f);
        }
    }

    /// <summary>Runs a human's choice against the rules engine, then lets the loop animate the result.</summary>
    public void HumanDo(Action action)
    {
        if (_human == null) return;
        try
        {
            action();
        }
        catch (InvalidOperationException e)
        {
            _banner.Show("Not allowed right now", e.Message, Palette.Bad);
            return;
        }
        var pending = _human;
        _human = null;
        RefreshHud();
        pending.TrySetResult();
    }

    /// <summary>Debug: presses the next scripted button for the human after a short pause.</summary>
    private async void RunScriptStep()
    {
        await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
        if (!AwaitingHuman || App.Debug.Script.Count == 0) return;
        string step = App.Debug.Script.Dequeue();
        GD.Print($"[script] {step} in {_game.Phase}");
        switch (step)
        {
            case "roll": HumanDo(_game.Roll); break;
            case "buy": HumanDo(_game.Buy); break;
            case "decline": HumanDo(_game.DeclineBuy); break;
            case "end": HumanDo(_game.EndTurn); break;
            case "bid": HumanDo(() => _game.Bid(_game.CurrentAuction!.HighBid + 10)); break;
            case "pass": HumanDo(_game.PassBid); break;
            case "manage": OpenManage(); RunScriptStep(); break;
            case "close": CloseManage(); RunScriptStep(); break;
            case "build":
                foreach (var tile in _game.Actor.Properties)
                    if (tile is PropertyTile p && _game.CanBuild(p)) { HumanDo(() => _game.Build(p)); break; }
                break;
        }
    }

    private async Task Wait(float seconds)
    {
        if (seconds <= 0 || !IsInsideTree()) return;
        await ToSignal(GetTree().CreateTimer(seconds / Speed), SceneTreeTimer.SignalName.Timeout);
    }

    private async Task NextFrame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    // ======================================================================================= event animation

    private async Task Present(GameEvent gameEvent)
    {
        switch (gameEvent)
        {
            case TurnOrderDecidedEvent order:
            {
                string text = "";
                for (int i = 0; i < order.Order.Length; i++)
                    text += $"{i + 1}. {order.Order[i].Name} ({order.RollsBySeat[order.Order[i].Id]})   ";
                _banner.Show("TURN ORDER", "Opening rolls, insertion-sorted: " + text, Palette.Gold, 2.4f);
                Log("Turn order decided by opening rolls (insertion sort).");
                await Wait(2.2f);
                break;
            }
            case TurnStartedEvent turn:
            {
                foreach (var token in _tokens) token?.SetActive(false);
                var current = Token(turn.Player);
                current.SetActive(true);
                FocusOn(turn.Player, 11f);
                _banner.Show($"{turn.Player.Name.ToUpperInvariant()}'S TURN", $"Round {turn.Round}" + (turn.Player.InJail ? " · in jail" : ""), Palette.PlayerUi(turn.Player.CharacterId), 1.0f);
                Log($"— {turn.Player.Name}'s turn (round {turn.Round}) —");
                RefreshHud();
                await Wait(0.6f);
                break;
            }
            case DiceRolledEvent dice:
                App.CameraRig.Focus(Board3D.DicePlaza, 9f);
                await _dice.Roll(dice.Die1, dice.Die2, Speed);
                Log($"{dice.Player.Name} rolled {dice.Die1} + {dice.Die2} = {dice.Total}{(dice.IsDoubles ? " (doubles!)" : "")}");
                if (dice.IsDoubles) _banner.Show("DOUBLES!", $"{dice.Die1} + {dice.Die2}", Palette.Gold, 0.8f);
                await Wait(0.35f);
                break;
            case MovedEvent move:
                await AnimateMove(move);
                break;
            case PassedGoEvent go:
                Sfx.Play(Sfx.Kind.Coin, 1.2f);
                Float(go.Player, $"GO! +{Ui.Money(go.Salary)}", Palette.Good);
                Log($"{go.Player.Name} passed GO and collected {Ui.Money(go.Salary)}.");
                break;
            case MoneyEvent money:
                await AnimateMoney(money);
                break;
            case PurchaseOfferedEvent offer:
                _deed.Show(offer.Tile, _game);
                _board.Highlight(offer.Tile.Index, true);
                Log($"{offer.Player.Name} may buy {offer.Tile.Name} for {Ui.Money(offer.Tile.Price)}.");
                break;
            case PropertyBoughtEvent bought:
                Sfx.Play(Sfx.Kind.Build, 1.3f);
                _deed.Show(bought.Tile, _game);
                _banner.Show("SOLD!", $"{bought.Player.Name} bought {bought.Tile.Name}", Palette.PlayerUi(bought.Player.CharacterId), 1.0f);
                Log($"{bought.Player.Name} bought {bought.Tile.Name}.");
                await Wait(0.9f);
                _deed.HideCard();
                break;
            case OwnershipChangedEvent owner:
                _board.SetOwner(owner.Tile.Index, owner.NewOwner?.CharacterId);
                break;
            case AuctionStartedEvent auction:
                Log($"{auction.Tile.Name} goes to auction.");
                _banner.Show("AUCTION", auction.Tile.Name, Palette.Gold, 1.0f);
                ShowModal(_auction);
                _auction.Show(_game, this);
                await Wait(0.6f);
                break;
            case AuctionBidEvent bid:
                Sfx.Play(Sfx.Kind.Click, 1.4f);
                Float(bid.Player, "BID " + Ui.Money(bid.Amount), Palette.Gold);
                Log($"{bid.Player.Name} bids {Ui.Money(bid.Amount)}.");
                _auction.Show(_game, this);
                await Wait(0.35f);
                break;
            case AuctionPassEvent pass:
                Log($"{pass.Player.Name} passes.");
                _auction.Show(_game, this);
                await Wait(0.25f);
                break;
            case AuctionEndedEvent ended:
                HideModal(_auction);
                _deed.HideCard();
                if (ended.Winner != null)
                {
                    _banner.Show("SOLD AT AUCTION", $"{ended.Winner.Name} wins {ended.Tile.Name} for {Ui.Money(ended.Price)}", Palette.Gold, 1.4f);
                    Log($"{ended.Winner.Name} won {ended.Tile.Name} for {Ui.Money(ended.Price)}.");
                }
                else
                {
                    _banner.Show("NO BIDS", $"{ended.Tile.Name} stays with the bank", Palette.TextDim, 1.2f);
                    Log($"Nobody bid on {ended.Tile.Name}.");
                }
                _board.ClearHighlights();
                await Wait(1.0f);
                break;
            case CardDrawnEvent drawn:
                Sfx.Play(Sfx.Kind.Card);
                _card.Show(drawn.Card, drawn.Player.Name, 2.0f / Speed);
                Log($"{drawn.Player.Name} drew: {drawn.Card.Text}");
                await Wait(2.4f);
                break;
            case JailEvent jail:
                if (jail.Entered)
                {
                    Sfx.Play(Sfx.Kind.Jail);
                    _banner.Show("GO TO JAIL!", $"{jail.Player.Name} {jail.Reason}", Palette.Bad, 1.4f);
                    Token(jail.Player).Play(VoxelAnimator.Mode.Sad);
                }
                else
                {
                    _banner.Show("FREE!", $"{jail.Player.Name} {jail.Reason}", Palette.Good, 1.0f);
                    var token = Token(jail.Player);
                    token.Play(VoxelAnimator.Mode.Idle);
                    token.SlideTo(BoardLayout.TokenSpot(10, jail.Player.Id, inJail: false), 0.3f);
                }
                Log($"{jail.Player.Name} {jail.Reason}.");
                await Wait(1.0f);
                break;
            case BuildingChangedEvent building:
                Sfx.Play(Sfx.Kind.Build);
                _board.SetBuildings(building.Tile.Index, building.Houses, animate: true);
                Log(building.Houses == 5 ? $"{building.Owner.Name} put a hotel on {building.Tile.Name}." : $"{building.Tile.Name} now has {building.Houses} house(s).");
                if (_manage.Visible) _manage.Refresh();
                await Wait(0.35f);
                break;
            case MortgageChangedEvent mortgage:
                _board.SetMortgaged(mortgage.Tile.Index, mortgage.Mortgaged);
                Log($"{mortgage.Owner.Name} {(mortgage.Mortgaged ? "mortgaged" : "unmortgaged")} {mortgage.Tile.Name}.");
                if (_manage.Visible) _manage.Refresh();
                break;
            case DebtEvent debt:
                _banner.Show("RAISE FUNDS", $"{debt.Debtor.Name} owes {Ui.Money(debt.Amount)} ({debt.Reason})", Palette.Bad, 1.6f);
                Log($"{debt.Debtor.Name} must raise {Ui.Money(debt.Amount)}: sell or mortgage, or go bankrupt.");
                await Wait(0.8f);
                break;
            case BankruptEvent bankrupt:
                Sfx.Play(Sfx.Kind.Bankrupt);
                _banner.Show("BANKRUPT!", bankrupt.Creditor != null ? $"{bankrupt.Player.Name}'s assets go to {bankrupt.Creditor.Name}" : $"{bankrupt.Player.Name}'s deeds return to the bank", Palette.Bad, 2f);
                Log($"{bankrupt.Player.Name} is bankrupt and leaves the turn queue.");
                await Token(bankrupt.Player).FallOut(0.8f / Speed);
                break;
            case TurnEndedEvent ended:
                Token(ended.Player).SetActive(false);
                _board.ClearHighlights();
                break;
            case GameOverEvent over:
                Sfx.Play(Sfx.Kind.Fanfare);
                foreach (var token in _tokens) token?.SetActive(false);
                var winner = Token(over.Winner);
                winner.SetActive(true);
                winner.Play(VoxelAnimator.Mode.Cheer);
                FocusOn(over.Winner, 7f);
                _banner.Show($"{over.Winner.Name.ToUpperInvariant()} WINS!", over.Reason, Palette.Gold, 3f);
                Log($"Game over: {over.Winner.Name} wins ({over.Reason}).");
                await Wait(1.5f);
                break;
            case LogEvent log:
                Log(log.Text);
                break;
        }
        RefreshHud(light: true);
    }

    private async Task AnimateMove(MovedEvent move)
    {
        var player = move.Player;
        var token = Token(player);
        int destination = move.Path[^1];
        if (move.Direct)
        {
            await token.Teleport(BoardLayout.TokenSpot(destination, player.Id, inJail: destination == 10), 0.7f / Speed);
            FocusOn(player, 10f);
            return;
        }
        token.Play(VoxelAnimator.Mode.Walk);
        float hop = Mathf.Clamp((move.Path.Length > 12 ? 0.16f : 0.26f) / Speed, 0.06f, 0.3f);
        for (int i = 0; i < move.Path.Length; i++)
        {
            var spot = BoardLayout.TokenSpot(move.Path[i], player.Id);
            if (!_overview) App.CameraRig.Focus(spot, 9.5f);
            await token.Hop(spot, hop);
            Sfx.Play(Sfx.Kind.Hop, 0.9f + 0.03f * i);
        }
        token.Play(VoxelAnimator.Mode.Idle);
        token.Face(new Vector3(1, 0, 1));
        _board.ClearHighlights();
        _board.Highlight(destination, true);
    }

    private async Task AnimateMoney(MoneyEvent money)
    {
        if (money.From != null)
        {
            _shownCash[money.From.Id] -= money.Amount;
            Float(money.From, "-" + Ui.Money(money.Amount), Palette.Bad);
        }
        if (money.To != null)
        {
            _shownCash[money.To.Id] += money.Amount;
            Float(money.To, "+" + Ui.Money(money.Amount), Palette.Good);
        }
        Sfx.Play(money.To != null ? Sfx.Kind.Coin : Sfx.Kind.Pay);
        if (money.Reason != "Passed GO")
        {
            string who = money.From?.Name ?? "Bank";
            string to = money.To?.Name ?? "the bank";
            Log($"{who} paid {to} {Ui.Money(money.Amount)} · {money.Reason}");
        }
        RefreshHud(light: true);
        await Wait(money.From != null && money.To != null ? 0.7f : 0.35f);
    }

    private Token3D Token(Player player) => _tokens[player.Id]!;

    private void FocusOn(Player player, float size)
    {
        _focusedPlayer = player.Id;
        App.CameraRig.Focus(_overview ? Vector3.Zero : Token(player).Position, _overview ? 16f : size);
    }

    private void Float(Player player, string text, Color color)
    {
        var token = Token(player);
        var label = new Label3D
        {
            Text = text,
            Font = Board3D.Font3D,
            FontSize = 64,
            PixelSize = 0.0065f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate = color,
            OutlineSize = 14,
            OutlineModulate = new Color(0.05f, 0.08f, 0.15f, 0.85f),
            NoDepthTest = true,
            Position = token.Position + Vector3.Up * 1.05f,
        };
        Scene3D!.AddChild(label);
        var tween = label.CreateTween();
        tween.TweenProperty(label, "position", label.Position + Vector3.Up * 0.8f, 1.4f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.Parallel().TweenProperty(label, "modulate:a", 0f, 1.4f).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(label.QueueFree));
    }

    private void Log(string text) => _log.Add(text);

    // ======================================================================================= input

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_paused) return;
        if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            switch (key.Keycode)
            {
                case Key.Space when AwaitingHuman && !_manage.Visible:
                    if (_game.CanRoll) HumanDo(_game.Roll);
                    else if (_game.CanEndTurn) HumanDo(_game.EndTurn);
                    break;
                case Key.Tab:
                    App.Settings.ShowDsaPanel = !App.Settings.ShowDsaPanel;
                    _dsa.Visible = App.Settings.ShowDsaPanel;
                    break;
                case Key.M:
                    if (_manage.Visible) CloseManage();
                    else OpenManage();
                    break;
                case Key.V:
                    _overview = !_overview;
                    if (_focusedPlayer >= 0) FocusOn(_game.Players[_focusedPlayer], 11f);
                    break;
                default:
                    return;
            }
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click && !_manage.Visible)
        {
            var point = App.CameraRig.PickBoard(click.Position);
            int index = point.HasValue ? BoardLayout.IndexAt(point.Value) : -1;
            if (index >= 0 && _game.Board.NodeAt(index).Value is OwnableTile tile)
            {
                if (_deed.Visible && _deed.Tile == tile) _deed.HideCard();
                else _deed.Show(tile, _game);
            }
            else if (_game.Phase != Phase.BuyDecision)
            {
                _deed.HideCard();
            }
        }
    }

    public void OpenManage()
    {
        ShowModal(_manage);
        _manage.Open();
    }

    public void CloseManage()
    {
        _manage.Close();
        HideModal(_manage);
    }

    // ======================================================================================= building the scene and HUD

    private void Build3D()
    {
        var scene = CreateScene3D("GameScene");
        _board = new Board3D(_game.Board);
        scene.AddChild(_board);
        _dice = new Dice3D { Position = Board3D.DicePlaza };
        scene.AddChild(_dice);
        foreach (var player in _game.Players)
        {
            var token = Token3D.Create(player.CharacterId);
            scene.AddChild(token);
            token.Position = BoardLayout.TokenSpot(0, player.Id);
            token.Face(new Vector3(1, 0, 1), instant: true);
            token.AnimationSpeed = Speed;
            _tokens[player.Id] = token;
        }
    }

    private void BuildHud()
    {
        _playerCard = Layout.Place(new PlayerCard(), LayoutPreset.TopLeft, 18, 18);
        AddChild(_playerCard);
        _log = Layout.Place(new EventLogPanel(), LayoutPreset.TopLeft, 24, 236);
        AddChild(_log);
        _queue = Layout.Place(new QueuePanel(), LayoutPreset.TopRight, 18, 18);
        AddChild(_queue);
        _deed = Layout.Place(new DeedCard(), LayoutPreset.CenterRight, 18, 0);
        AddChild(_deed);
        _standings = Layout.Place(new StandingsPanel(), LayoutPreset.BottomLeft, 18, 18);
        AddChild(_standings);
        _dsa = Layout.Place(new DsaPanel(), LayoutPreset.BottomRight, 18, 18);
        _dsa.Visible = App.Settings.ShowDsaPanel;
        AddChild(_dsa);
        _actions = Layout.Place(new ActionBar(), LayoutPreset.CenterBottom, 0, 18);
        AddChild(_actions);
        _banner = Layout.Place(new Banner(), LayoutPreset.CenterTop, 0, 22);
        AddChild(_banner);

        _modalDim = new ColorRect { Color = new Color(0.03f, 0.05f, 0.1f, 0.55f), Visible = false, MouseFilter = MouseFilterEnum.Stop };
        AddChild(_modalDim);
        _modalDim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _auction = Layout.Place(new AuctionPanel(), LayoutPreset.Center, 0, 0);
        AddChild(_auction);
        _manage = Layout.Place(new ManagePanel(this), LayoutPreset.Center, 0, 0);
        AddChild(_manage);
        _card = Layout.Place(new CardPopup(), LayoutPreset.Center, 0, 0);
        AddChild(_card);

        _dsa.Bind(_game, () =>
        {
            var screens = App.Screens.ToArray();
            var names = new string[screens.Length];
            for (int i = 0; i < screens.Length; i++) names[i] = screens[i].Title;
            return names;
        });
    }

    private void ShowModal(Control modal)
    {
        _modalDim.Visible = modal == _manage;
        modal.Visible = true;
    }

    private void HideModal(Control modal)
    {
        modal.Visible = false;
        if (modal == _manage) _modalDim.Visible = false;
    }

    private void RefreshHud(bool light = false)
    {
        if (!IsInsideTree()) return;
        var current = _game.Phase == Phase.GameOver && _game.Winner != null ? _game.Winner : _game.CurrentPlayer;
        _playerCard.Show(_game, current);
        _playerCard.OverrideCash(_shownCash[current.Id]);
        _queue.Show(_game, p => _shownCash[p.Id]);
        if (!light) _standings.Show(_game);
        _dsa.Refresh();
        _actions.Show(this, _game);
        if (_auction.Visible) _auction.Show(_game, this);
        if (_manage.Visible) _manage.Refresh();
        if (_game.Phase == Phase.BuyDecision && _game.PendingPurchase is { } offer && _deed.Tile != offer) _deed.Show(offer, _game);
    }
}
