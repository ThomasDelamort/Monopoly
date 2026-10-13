using System;
using Godot;
using Monopoly.Core.Rules;

namespace MonopolyGame;

/// <summary>Bottom-center buttons: exactly the actions the rules engine allows for the human in this phase.</summary>
public partial class ActionBar : FramePanel
{
    private readonly HBoxContainer _row = Ui.HBox(10);

    public ActionBar()
    {
        Padding = 12;
        _row.Alignment = BoxContainer.AlignmentMode.Center;
        AddChild(_row);
    }

    public void Show(GameScreen screen, Game game)
    {
        Layout.Clear(_row);
        if (game.Phase == Phase.GameOver)
        {
            Visible = false;
            return;
        }
        Visible = true;
        var actor = game.Actor;
        if (!screen.AwaitingHuman || actor.IsBot)
        {
            _row.AddChild(Portraits.Rect(actor.CharacterId, 36));
            _row.AddChild(Ui.Label(actor.IsBot ? $"{actor.Name} is thinking…" : "…", 18, Palette.TextDim));
            return;
        }

        void Add(string text, Action action, bool enabled = true, bool primary = false, bool danger = false)
        {
            var button = Ui.Button(text, () => screen.HumanDo(action), 18, new Vector2(0, 50));
            button.Disabled = !enabled;
            if (primary || danger)
            {
                var style = (StyleBoxFlat)button.GetThemeStylebox("normal").Duplicate();
                style.BorderColor = danger ? Palette.Bad : Palette.Gold;
                style.BgColor = danger ? new Color(0.3f, 0.08f, 0.1f, 0.95f) : new Color(0.24f, 0.2f, 0.06f, 0.95f);
                button.AddThemeStyleboxOverride("normal", style);
            }
            _row.AddChild(button);
        }

        _row.AddChild(Portraits.Rect(actor.CharacterId, 40));
        switch (game.Phase)
        {
            case Phase.AwaitRoll:
                Add(actor.ConsecutiveDoubles > 0 ? "DOUBLES! ROLL AGAIN  [Space]" : "ROLL DICE  [Space]", game.Roll, primary: true);
                break;
            case Phase.InJail:
                Add("ROLL FOR DOUBLES  [Space]", game.Roll, primary: true);
                Add($"PAY {Ui.Money(game.Setup.JailFine)} FINE", game.PayJailFine, game.CanPayJailFine);
                if (actor.JailCards.Count > 0) Add("USE JAIL-FREE CARD", game.UseJailCard, game.CanUseJailCard);
                break;
            case Phase.BuyDecision:
                var offer = game.PendingPurchase!;
                Add($"BUY {offer.Name.ToUpperInvariant()} · {Ui.Money(offer.Price)}", game.Buy, game.CanBuy, primary: true);
                Add("SEND TO AUCTION", game.DeclineBuy);
                break;
            case Phase.Auction:
                _row.AddChild(Ui.Label("Place your bid in the auction panel", 18, Palette.TextDim));
                return;
            case Phase.RaiseFunds:
                var debt = game.CurrentDebt!;
                _row.AddChild(Ui.Label($"You owe {Ui.Money(debt.Amount)} ({debt.Reason})", 17, Palette.Bad));
                Add($"PAY {Ui.Money(debt.Amount)}", game.PayDebt, game.CanPayDebt, primary: true);
                Add("DECLARE BANKRUPTCY", game.DeclareBankruptcy, danger: true);
                break;
            case Phase.PostRoll:
                Add("END TURN  [Space]", game.EndTurn, primary: true);
                break;
        }
        var manage = Ui.Button("MANAGE  [M]", screen.OpenManage, 18, new Vector2(0, 50));
        _row.AddChild(manage);
        if (game.UndoStack.Count > 0) Add($"UNDO ({game.UndoStack.Peek().Label})", game.Undo, game.CanUndo);
    }
}
