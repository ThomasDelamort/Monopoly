using Godot;
using Monopoly.Core.DataStructures;

namespace MonopolyGame;

/// <summary>A full-window UI screen. A screen may own 3D content (<see cref="Scene3D"/>) under Main.World.</summary>
public abstract partial class Screen : Control
{
    protected static Main App => Main.Instance;

    /// <summary>Overlay screens leave the screen beneath them visible (pause, game over, settings in-game).</summary>
    public virtual bool IsOverlay => false;

    /// <summary>Short name for the menu-stack visualization.</summary>
    public abstract string Title { get; }

    /// <summary>This screen's 3D content, if any. The top-most screen that has one is shown.</summary>
    public Node3D? Scene3D { get; protected set; }

    public virtual void OnEnter() { }

    public virtual void OnExit()
    {
        Scene3D?.QueueFree();
        Scene3D = null;
    }

    public virtual void OnCovered() { }
    public virtual void OnUncovered() { }

    /// <summary>Called when this screen's 3D content becomes the visible one: set up the camera here.</summary>
    public virtual void OnSceneShown() { }

    /// <summary>Return true to consume Esc/Back instead of popping the screen.</summary>
    public virtual bool OnBack() => false;

    protected Node3D CreateScene3D(string name)
    {
        Scene3D = new Node3D { Name = name };
        App.World.AddChild(Scene3D);
        return Scene3D;
    }
}

/// <summary>
/// Menu navigation as a stack: Push opens a screen on top, Back/Esc pops to the previous one.
/// Built on the hand-written <see cref="ArrayStack{T}"/>.
/// </summary>
public sealed class ScreenStack
{
    private readonly ArrayStack<Screen> _stack = new() { TraceName = "Menu Screens (Stack)" };
    private readonly Control _root;
    private Node3D? _shownScene;

    public ScreenStack(Control root)
    {
        _root = root;
    }

    public int Count => _stack.Count;
    public Screen? Top => _stack.TryPeek(out var top) ? top : null;

    /// <summary>Top first.</summary>
    public Screen[] ToArray() => _stack.ToArray();

    public void Push(Screen screen)
    {
        if (Top is { } top)
        {
            top.OnCovered();
            if (!screen.IsOverlay) top.Visible = false;
        }
        _root.AddChild(screen);
        screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _stack.Push(screen);
        screen.OnEnter();
        Refresh3D();
    }

    public void Pop()
    {
        if (_stack.Count <= 1) return;
        var screen = _stack.Pop();
        screen.OnExit();
        screen.QueueFree();
        var top = Top!;
        top.Visible = true;
        top.OnUncovered();
        Refresh3D();
    }

    /// <summary>Esc / Back: let the top screen handle it, otherwise pop.</summary>
    public void Back()
    {
        var top = Top;
        if (top == null || top.OnBack()) return;
        Sfx.Play(Sfx.Kind.Click, 0.8f);
        Pop();
    }

    /// <summary>Clears the whole stack and starts over with one screen.</summary>
    public void ResetTo(Screen screen)
    {
        while (_stack.TryPop(out var old))
        {
            old.OnExit();
            old.QueueFree();
        }
        _shownScene = null;
        Push(screen);
    }

    private void Refresh3D()
    {
        Node3D? show = null;
        Screen? owner = null;
        foreach (var screen in _stack) // top → bottom
        {
            if (screen.Scene3D == null) continue;
            show = screen.Scene3D;
            owner = screen;
            break;
        }
        foreach (var screen in _stack)
            if (screen.Scene3D != null) screen.Scene3D.Visible = screen.Scene3D == show;
        if (show != null && show != _shownScene) owner!.OnSceneShown();
        _shownScene = show;
    }
}
