using System.Collections.Generic;
using Godot;

namespace MonopolyGame;

/// <summary>Cached flat-shaded low-poly materials and primitive helpers for procedural fallbacks.</summary>
public static class Meshes
{
    private static readonly Dictionary<(Color, float, float), StandardMaterial3D> Materials = new();

    public static StandardMaterial3D Mat(Color color, float roughness = 0.85f, float emission = 0f)
    {
        var key = (color, roughness, emission);
        if (Materials.TryGetValue(key, out var mat)) return mat;
        mat = new StandardMaterial3D { AlbedoColor = color, Roughness = roughness };
        if (color.A < 1f) mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        if (emission > 0)
        {
            mat.EmissionEnabled = true;
            mat.Emission = color;
            mat.EmissionEnergyMultiplier = emission;
        }
        Materials[key] = mat;
        return mat;
    }

    public static MeshInstance3D Box(Node3D parent, Vector3 size, Vector3 position, Color color, string? name = null, float emission = 0f)
    {
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            Position = position,
            MaterialOverride = Mat(color, emission: emission),
        };
        if (name != null) mesh.Name = name;
        parent.AddChild(mesh);
        return mesh;
    }

    public static MeshInstance3D Cylinder(Node3D parent, float bottomRadius, float topRadius, float height, Vector3 position, Color color, int segments = 8)
    {
        var mesh = new MeshInstance3D
        {
            Mesh = new CylinderMesh { BottomRadius = bottomRadius, TopRadius = topRadius, Height = height, RadialSegments = segments, Rings = 1 },
            Position = position,
            MaterialOverride = Mat(color),
        };
        parent.AddChild(mesh);
        return mesh;
    }

    public static MeshInstance3D Prism(Node3D parent, Vector3 size, Vector3 position, Color color)
    {
        var mesh = new MeshInstance3D
        {
            Mesh = new PrismMesh { Size = size },
            Position = position,
            MaterialOverride = Mat(color),
        };
        parent.AddChild(mesh);
        return mesh;
    }

    /// <summary>Sets a flat color on the first mesh named <paramref name="meshName"/> (e.g. a car's paint).</summary>
    public static void Recolor(Node3D model, string meshName, Color color)
    {
        if (model.FindChild(meshName, true, false) is MeshInstance3D mesh) mesh.MaterialOverride = Mat(color, 0.35f);
        else if (model.GetChildCount() > 0 && model.GetChild(0) is MeshInstance3D first) first.MaterialOverride = Mat(color, 0.35f);
    }

    /// <summary>Loads a Blender-exported model if present, otherwise returns null so callers build a fallback.</summary>
    public static Node3D? TryLoadModel(string path)
    {
        if (!ResourceLoader.Exists(path)) return null;
        var scene = ResourceLoader.Load<PackedScene>(path);
        return scene?.Instantiate<Node3D>();
    }
}
