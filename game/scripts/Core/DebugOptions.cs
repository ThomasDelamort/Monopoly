using System.Globalization;
using Godot;

namespace MonopolyGame;

/// <summary>
/// Command-line switches for automated runs, passed after "--":
/// --start title|count|seat|settings|game, --humans N, --autoplay, --seed N, --rounds N, --speed 0-2,
/// --shots DIR (save viewport PNGs), --shot-every SEC, --quit-after SEC,
/// --give 1,3,6 (deeds for seat 1), --script roll,buy,decline,manage,end (auto-press the human's buttons).
/// </summary>
public sealed class DebugOptions
{
    public string StartScreen { get; private set; } = "title";
    public int Humans { get; private set; } = 1;
    public bool Autoplay { get; private set; }
    public ulong Seed { get; private set; }
    public int Rounds { get; private set; }
    public int? AnimationSpeedIndex { get; private set; }
    public string? ShotsDirectory { get; private set; }
    public float ShotEvery { get; private set; } = 3f;
    public float QuitAfter { get; private set; }
    public int[] Give { get; private set; } = System.Array.Empty<int>();
    public System.Collections.Generic.Queue<string> Script { get; } = new();

    public bool IsAutomated => ShotsDirectory != null || QuitAfter > 0;

    public static DebugOptions Parse(string[] args)
    {
        var options = new DebugOptions();
        for (int i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : "";
            switch (args[i])
            {
                case "--start": options.StartScreen = Next(); break;
                case "--humans": options.Humans = Mathf.Clamp(int.Parse(Next()), 1, 4); break;
                case "--autoplay": options.Autoplay = true; break;
                case "--seed": options.Seed = ulong.Parse(Next()); break;
                case "--rounds": options.Rounds = int.Parse(Next()); break;
                case "--speed": options.AnimationSpeedIndex = Mathf.Clamp(int.Parse(Next()), 0, 2); break;
                case "--shots": options.ShotsDirectory = Next(); break;
                case "--shot-every": options.ShotEvery = float.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--quit-after": options.QuitAfter = float.Parse(Next(), CultureInfo.InvariantCulture); break;
                case "--give": options.Give = System.Array.ConvertAll(Next().Split(','), int.Parse); break;
                case "--script":
                    foreach (var step in Next().Split(',')) options.Script.Enqueue(step.Trim().ToLowerInvariant());
                    break;
            }
        }
        return options;
    }
}

/// <summary>Saves numbered viewport screenshots at an interval and quits after a time limit.</summary>
public partial class ScreenshotTaker : Node
{
    private readonly DebugOptions _options;
    private double _elapsed;
    private double _nextShot;
    private int _count;

    public ScreenshotTaker(DebugOptions options)
    {
        _options = options;
        _nextShot = options.ShotEvery;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_options.ShotsDirectory != null && _elapsed >= _nextShot)
        {
            _nextShot += _options.ShotEvery;
            DirAccess.MakeDirRecursiveAbsolute(_options.ShotsDirectory);
            var image = GetViewport().GetTexture().GetImage();
            string path = $"{_options.ShotsDirectory}/shot_{_count++:00}.png";
            image.SavePng(path);
            GD.Print($"[shot] {path} at {_elapsed:0.0}s");
        }
        if (_options.QuitAfter > 0 && _elapsed >= _options.QuitAfter) GetTree().Quit();
    }
}
