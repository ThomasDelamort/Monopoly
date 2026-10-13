using Godot;

namespace MonopolyGame;

/// <summary>Player preferences, stored in user://settings.cfg.</summary>
public sealed class GameSettings
{
    private const string FilePath = "user://settings.cfg";

    public static readonly float[] AnimationSpeeds = { 1f, 1.75f, 3f };
    public static readonly string[] AnimationSpeedNames = { "Normal", "Fast", "Turbo" };
    public static readonly float[] BotDelays = { 0.9f, 0.5f, 0.15f };
    public static readonly string[] BotDelayNames = { "Relaxed", "Normal", "Quick" };
    public static readonly int[] RoundLimits = { 0, 30, 60, 100 };
    public static readonly string[] RoundLimitNames = { "Off (last one standing)", "30 rounds", "60 rounds", "100 rounds" };

    public int AnimationSpeedIndex { get; set; }
    public int BotDelayIndex { get; set; } = 1;
    public int RoundLimitIndex { get; set; }
    public bool ShowDsaPanel { get; set; } = true;
    public bool Fullscreen { get; set; }
    public float Volume { get; set; } = 0.7f;

    public float AnimationSpeed => AnimationSpeeds[Mathf.Clamp(AnimationSpeedIndex, 0, AnimationSpeeds.Length - 1)];
    public float BotDelay => BotDelays[Mathf.Clamp(BotDelayIndex, 0, BotDelays.Length - 1)];
    public int RoundLimit => RoundLimits[Mathf.Clamp(RoundLimitIndex, 0, RoundLimits.Length - 1)];

    public static GameSettings Load()
    {
        var settings = new GameSettings();
        var file = new ConfigFile();
        if (file.Load(FilePath) != Error.Ok) return settings;
        settings.AnimationSpeedIndex = (int)file.GetValue("game", "animation_speed", 0);
        settings.BotDelayIndex = (int)file.GetValue("game", "bot_delay", 1);
        settings.RoundLimitIndex = (int)file.GetValue("game", "round_limit", 0);
        settings.ShowDsaPanel = (bool)file.GetValue("game", "show_dsa_panel", true);
        settings.Fullscreen = (bool)file.GetValue("display", "fullscreen", false);
        settings.Volume = (float)file.GetValue("audio", "volume", 0.7f);
        return settings;
    }

    public void Save()
    {
        var file = new ConfigFile();
        file.SetValue("game", "animation_speed", AnimationSpeedIndex);
        file.SetValue("game", "bot_delay", BotDelayIndex);
        file.SetValue("game", "round_limit", RoundLimitIndex);
        file.SetValue("game", "show_dsa_panel", ShowDsaPanel);
        file.SetValue("display", "fullscreen", Fullscreen);
        file.SetValue("audio", "volume", Volume);
        file.Save(FilePath);
    }

    public void ApplyDisplay()
    {
        DisplayServer.WindowSetMode(Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        AudioServer.SetBusVolumeDb(0, Mathf.LinearToDb(Mathf.Max(Volume, 0.0001f)));
    }
}
