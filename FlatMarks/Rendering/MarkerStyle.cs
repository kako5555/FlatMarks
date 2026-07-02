using System.Numerics;

namespace FlatMarks.Rendering;

public enum MarkerShape
{
    Circle, // letters A-D
    Square, // numbers 1-4
}

/// <summary>
/// Static per-marker style constants and color helpers. All packed colors are ImGui-style
/// ABGR (0xAABBGGRR) — get this right or A and C swap colors (spec 1c warning).
/// </summary>
public static class MarkerStyle
{
    // Native waymark visual dimensions (from FFXIV / Waymark Studio): circle radius 1.25y,
    // square half-width 1.1y. We drive both from one Radius slider that represents the circle
    // radius, scaling the square by this ratio so both match the game at the default.
    public const float NativeCircleRadius = 1.25f;
    public const float NativeSquareHalfWidth = 1.1f;
    public const float SquareToCircleRatio = NativeSquareHalfWidth / NativeCircleRadius; // ~0.88

    /// <summary>Human-readable glyph label per index (0=A .. 7=Four). Used for config labels and glyph filenames.</summary>
    public static readonly string[] Labels = { "A", "B", "C", "D", "1", "2", "3", "4" };

    /// <summary>Index order that pairs each letter with its same-color number: A,1, B,2, C,3, D,4.</summary>
    public static readonly int[] ColorPairOrder = { 0, 4, 1, 5, 2, 6, 3, 7 };

    /// <summary>Shape per index: letters are circles, numbers are squares.</summary>
    public static readonly MarkerShape[] Shapes =
    {
        MarkerShape.Circle, MarkerShape.Circle, MarkerShape.Circle, MarkerShape.Circle,
        MarkerShape.Square, MarkerShape.Square, MarkerShape.Square, MarkerShape.Square,
    };

    /// <summary>
    /// Default full-alpha ABGR colors. A/1 red, B/2 yellow, C/3 blue, D/4 purple —
    /// matching the game's waymark color pairing.
    /// </summary>
    public static readonly uint[] DefaultColors =
    {
        0xFF5050FF, // A  red
        0xFF50D2FF, // B  yellow
        0xFFFFA050, // C  blue
        0xFFFF50C0, // D  purple
        0xFF5050FF, // 1  red
        0xFF50D2FF, // 2  yellow
        0xFFFFA050, // 3  blue
        0xFFFF50C0, // 4  purple
    };

    /// <summary>Glyph PNG filename for a marker index (matches res/ output).</summary>
    public static string GlyphFile(int index) => $"glyph_{Labels[index].ToLowerInvariant()}.png";

    // ---- Color packing helpers (ABGR <-> Vector4 RGBA for ImGui color pickers) ----

    public static Vector4 ToVector4(uint abgr)
    {
        var r = (abgr & 0xFF) / 255f;
        var g = ((abgr >> 8) & 0xFF) / 255f;
        var b = ((abgr >> 16) & 0xFF) / 255f;
        var a = ((abgr >> 24) & 0xFF) / 255f;
        return new Vector4(r, g, b, a);
    }

    public static uint FromVector4(Vector4 rgba)
    {
        var r = (uint)(System.Math.Clamp(rgba.X, 0f, 1f) * 255f + 0.5f);
        var g = (uint)(System.Math.Clamp(rgba.Y, 0f, 1f) * 255f + 0.5f);
        var b = (uint)(System.Math.Clamp(rgba.Z, 0f, 1f) * 255f + 0.5f);
        var a = (uint)(System.Math.Clamp(rgba.W, 0f, 1f) * 255f + 0.5f);
        return r | (g << 8) | (b << 16) | (a << 24);
    }

    /// <summary>Replace the alpha channel of an ABGR color with <paramref name="alpha"/> (0..1).</summary>
    public static uint WithAlpha(uint abgr, float alpha)
    {
        var a = (uint)(System.Math.Clamp(alpha, 0f, 1f) * 255f + 0.5f);
        return (abgr & 0x00FFFFFF) | (a << 24);
    }
}
