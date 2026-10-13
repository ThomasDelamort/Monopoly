using System;
using System.Collections.Generic;
using Godot;

namespace MonopolyGame;

/// <summary>
/// Tiny synthesized sound effects (no audio files): each sound is generated once as 16-bit PCM
/// into an AudioStreamWav and played from a small pool of players.
/// </summary>
public partial class Sfx : Node
{
    public enum Kind
    {
        Click,
        Dice,
        Hop,
        Coin,
        Pay,
        Card,
        Jail,
        Build,
        Fanfare,
        Bankrupt,
    }

    private const int Rate = 22050;
    private static Sfx? _instance;
    private readonly Dictionary<Kind, AudioStreamWav> _streams = new();
    private readonly List<AudioStreamPlayer> _pool = new();
    private int _next;

    public override void _Ready()
    {
        _instance = this;
        for (int i = 0; i < 8; i++)
        {
            var player = new AudioStreamPlayer();
            AddChild(player);
            _pool.Add(player);
        }
        foreach (Kind kind in Enum.GetValues<Kind>()) _streams[kind] = Build(kind);
    }

    public static void Play(Kind kind, float pitch = 1f)
    {
        if (_instance == null || !_instance.IsInsideTree()) return;
        var player = _instance._pool[_instance._next++ % _instance._pool.Count];
        player.Stream = _instance._streams[kind];
        player.PitchScale = pitch;
        player.Play();
    }

    private static AudioStreamWav Build(Kind kind)
    {
        var rng = new Random((int)kind * 7919);
        float[] samples = kind switch
        {
            Kind.Click => Tone(0.05f, t => Square(t, 1200) * Decay(t, 0.05f) * 0.25f),
            Kind.Hop => Tone(0.09f, t => Sine(t, 520 + 900 * t) * Decay(t, 0.09f) * 0.35f),
            Kind.Dice => Tone(0.45f, t => (float)(rng.NextDouble() * 2 - 1) * Clicks(t) * 0.35f),
            Kind.Coin => Tone(0.35f, t => (Sine(t, t < 0.07f ? 988 : 1319) * 0.4f + Sine(t, 2637) * 0.08f) * Decay(t, 0.35f)),
            Kind.Pay => Tone(0.3f, t => Sine(t, 660 - 500 * t) * Decay(t, 0.3f) * 0.35f),
            Kind.Card => Tone(0.22f, t => (float)(rng.NextDouble() * 2 - 1) * Decay(t, 0.22f) * 0.18f * (1 - t * 3)),
            Kind.Jail => Tone(0.6f, t => (Square(t, 196) * 0.2f + Square(t, 185) * 0.2f) * Decay(t, 0.6f)),
            Kind.Build => Tone(0.25f, t => (Sine(t, 300) * 0.5f + (float)(rng.NextDouble() - 0.5) * 0.3f) * Decay(t, 0.12f)),
            Kind.Fanfare => Tone(1.2f, Fanfare),
            Kind.Bankrupt => Tone(0.9f, t => Sine(t, 300 * MathF.Pow(0.5f, t * 1.5f)) * Decay(t, 0.9f) * 0.4f),
            _ => Tone(0.05f, _ => 0),
        };

        var data = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short s = (short)(Math.Clamp(samples[i], -1f, 1f) * short.MaxValue);
            data[i * 2] = (byte)(s & 0xff);
            data[i * 2 + 1] = (byte)((s >> 8) & 0xff);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = data };
    }

    private static float[] Tone(float seconds, Func<float, float> generator)
    {
        var samples = new float[(int)(seconds * Rate)];
        for (int i = 0; i < samples.Length; i++) samples[i] = generator(i / (float)Rate);
        return samples;
    }

    private static float Sine(float t, float hz) => MathF.Sin(2 * MathF.PI * hz * t);
    private static float Square(float t, float hz) => Sine(t, hz) >= 0 ? 1 : -1;
    private static float Decay(float t, float length) => MathF.Max(0, 1 - t / length) * MathF.Max(0, 1 - t / length);

    private static float Clicks(float t)
    {
        // A few bouncing impacts that get closer together.
        float[] hits = { 0f, 0.12f, 0.21f, 0.28f, 0.33f, 0.37f };
        float env = 0;
        foreach (var h in hits)
            if (t >= h) env = MathF.Max(env, MathF.Exp(-(t - h) * 60));
        return env;
    }

    private static float Fanfare(float t)
    {
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
        int index = Math.Min((int)(t / 0.18f), notes.Length - 1);
        float local = t - index * 0.18f;
        float length = index == notes.Length - 1 ? 0.66f : 0.18f;
        return (Sine(t, notes[index]) * 0.3f + Sine(t, notes[index] * 2) * 0.08f) * MathF.Max(0, 1 - local / length);
    }
}
