using Godot;

namespace MonopolyGame;

/// <summary>
/// Where each of the 40 linked-list nodes sits in 3D. The board is a square ring on the XZ plane:
/// GO is the corner nearest +X/+Z and indices run clockwise when seen from above (−X along the bottom,
/// then −Z up the left side...). Every tile has a local frame whose +Z points away from the board center.
/// </summary>
public static class BoardLayout
{
    public const float TileWidth = 1.0f;
    public const float Corner = 1.6f;
    public const float Half = (9 * TileWidth + 2 * Corner) / 2f;
    public const float TileTop = 0.14f;
    public const float BandDepth = 0.34f;

    /// <summary>Half-size of the grass area inside the ring of tiles.</summary>
    public const float InnerHalf = Half - Corner;

    public readonly record struct TileFrame(int Index, Vector3 Center, int Side, bool IsCorner, float Yaw)
    {
        public Basis Basis => new(Vector3.Up, Yaw);
        public Transform3D Transform => new(Basis, Center);

        /// <summary>Local X × local Z footprint.</summary>
        public Vector2 Size => IsCorner ? new Vector2(Corner, Corner) : new Vector2(TileWidth, Corner);

        public Vector3 ToWorld(Vector3 local) => Center + Basis * local;
    }

    public static TileFrame Frame(int index)
    {
        int side = index / 10;
        int offset = index % 10;
        float yaw = -side * Mathf.Pi / 2f; // each side is side 0 rotated a further quarter turn
        Vector3 local = offset == 0
            ? new Vector3(Half - Corner / 2, 0, Half - Corner / 2)
            : new Vector3(Half - Corner - (offset - 0.5f) * TileWidth, 0, Half - Corner / 2);
        return new TileFrame(index, new Basis(Vector3.Up, yaw) * local, side, offset == 0, yaw);
    }

    /// <summary>Standing spot for a token. Slots 0-3 share a tile without overlapping; jailed tokens go in the cell.</summary>
    public static Vector3 TokenSpot(int index, int slot, bool inJail = false)
    {
        var frame = Frame(index);
        float sx = slot % 2 == 0 ? -0.22f : 0.22f;
        float sz = slot / 2 == 0 ? 0.05f : 0.45f;
        Vector3 local;
        if (frame.IsCorner)
        {
            if (index == 10 && inJail) local = new Vector3(-0.33f + sx * 0.8f, 0, -0.33f + (slot / 2 == 0 ? -0.13f : 0.13f));
            else if (index == 10) local = slot < 2 ? new Vector3(0.62f, 0, -0.35f + slot * 0.42f) : new Vector3(-0.35f + (slot - 2) * 0.42f, 0, 0.62f);
            else local = new Vector3(sx * 1.4f, 0, (slot / 2 == 0 ? -0.3f : 0.3f));
        }
        else
        {
            local = new Vector3(sx, 0, sz);
        }
        return frame.ToWorld(local) + Vector3.Up * TileTop;
    }

    /// <summary>Tile index under a point on the board, or -1.</summary>
    public static int IndexAt(Vector3 point)
    {
        for (int i = 0; i < 40; i++)
        {
            var frame = Frame(i);
            var local = frame.Basis.Inverse() * (point - frame.Center);
            var size = frame.Size;
            if (Mathf.Abs(local.X) <= size.X / 2 && Mathf.Abs(local.Z) <= size.Y / 2) return i;
        }
        return -1;
    }
}
