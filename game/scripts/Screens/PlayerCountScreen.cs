using System;
using Godot;

namespace MonopolyGame;

/// <summary>Pick 1-4 human players. There are always 4 tokens: empty seats are filled with bots.</summary>
public partial class PlayerCountScreen : Screen
{
    public override string Title => "Players";

    public override void OnEnter()
    {
        var dim = new ColorRect { Color = new Color(0.05f, 0.08f, 0.15f, 0.55f), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var column = Ui.VBox(26);
        column.AddChild(new PixelText("HOW MANY PLAYERS?", 6f, Palette.Accent));
        column.AddChild(Ui.Label("Humans take the first seats. Empty seats are filled with bots, so there are always 4 tokens.", 19, Palette.TextDim, align: HorizontalAlignment.Center));

        var cards = Ui.HBox(22);
        cards.Alignment = BoxContainer.AlignmentMode.Center;
        for (int humans = 1; humans <= 4; humans++)
        {
            int count = humans;
            cards.AddChild(new CountCard(count, () => App.Screens.Push(new SeatSetupScreen(count))));
        }
        column.AddChild(cards);

        var back = Ui.Button("◀  BACK", () => App.Screens.Back(), 20, new Vector2(200, 46));
        back.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        column.AddChild(back);

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.AddChild(column);
        AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    /// <summary>A clickable card: big pixel digit plus a row of seat icons (human / bot).</summary>
    private sealed partial class CountCard : FramePanel
    {
        private readonly Action _onPick;
        private readonly int _humans;

        public CountCard(int humans, Action onPick)
        {
            _humans = humans;
            _onPick = onPick;
            Padding = 22;
            CustomMinimumSize = new Vector2(250, 300);
            MouseFilter = MouseFilterEnum.Stop;
            MouseDefaultCursorShape = CursorShape.PointingHand;
            FocusMode = FocusModeEnum.All;

            var box = Ui.VBox(14);
            box.Alignment = BoxContainer.AlignmentMode.Center;
            box.MouseFilter = MouseFilterEnum.Ignore;
            box.AddChild(new PixelText(humans.ToString(), 16f, Palette.Accent));
            box.AddChild(Ui.Label(humans == 1 ? "PLAYER" : "PLAYERS", 22, Palette.Text, bold: true, align: HorizontalAlignment.Center));

            var seats = Ui.HBox(6);
            seats.Alignment = BoxContainer.AlignmentMode.Center;
            for (int i = 0; i < 4; i++)
            {
                bool human = i < humans;
                var seat = new PanelContainer { CustomMinimumSize = new Vector2(42, 42), MouseFilter = MouseFilterEnum.Ignore };
                var style = new StyleBoxFlat
                {
                    BgColor = human ? Palette.PlayerUi(i) with { A = 0.85f } : new Color(0.2f, 0.25f, 0.36f, 0.6f),
                    CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
                };
                seat.AddThemeStyleboxOverride("panel", style);
                var tag = Ui.Label(human ? "P" + (i + 1) : "BOT", human ? 16 : 12, human ? Palette.BackgroundDeep : Palette.TextDim, bold: true, align: HorizontalAlignment.Center);
                seat.AddChild(tag);
                seats.AddChild(seat);
            }
            box.AddChild(seats);
            int bots = 4 - humans;
            box.AddChild(Ui.Label(bots == 0 ? "No bots" : bots == 1 ? "+ 1 bot" : $"+ {bots} bots", 18, Palette.TextDim, align: HorizontalAlignment.Center));
            AddChild(box);

            MouseEntered += () => SetHover(true);
            MouseExited += () => SetHover(false);
            FocusEntered += () => SetHover(true);
            FocusExited += () => SetHover(false);
        }

        private void SetHover(bool hover)
        {
            BorderColor = hover ? Palette.Gold : Palette.Border;
            Fill = hover ? new Color(0.12f, 0.2f, 0.34f, 0.95f) : Palette.Panel;
            QueueRedraw();
            if (hover) Sfx.Play(Sfx.Kind.Hop, 1.6f);
        }

        public override void _GuiInput(InputEvent @event)
        {
            bool click = @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left };
            bool key = @event is InputEventKey { Pressed: true, Keycode: Key.Enter or Key.KpEnter or Key.Space };
            if (!click && !key) return;
            Sfx.Play(Sfx.Kind.Click);
            AcceptEvent();
            _onPick();
        }

        public override string ToString() => $"{_humans} humans";
    }
}
