using System;
using System.Numerics;
using Dalamud.Plugin.Services;
using FlatMarks.Data;
using Dalamud.Bindings.ImGui;

namespace FlatMarks.Rendering;

/// <summary>
/// Strictly-fallback renderer used only when Pictomancy is unavailable/broken (config
/// <see cref="Configuration.ForceFallbackRenderer"/>). Projects world points to screen via
/// <see cref="IGameGui.WorldToScreen"/> and draws with the ImGui background draw list.
/// Lacks depth/UI clipping — do not use as the default (spec 1e).
/// </summary>
public sealed class FallbackRenderer
{
    private const int RingSegments = 48;

    private readonly Configuration config;
    private readonly IGameGui gameGui;

    // Reused per marker to avoid per-frame allocations.
    private readonly Vector2[] screenPoints = new Vector2[RingSegments];

    public FallbackRenderer(Configuration config, IGameGui gameGui)
    {
        this.config = config;
        this.gameGui = gameGui;
    }

    public void Draw(ReadOnlySpan<WaymarkState> markers, Vector3 cameraOrigin)
    {
        var drawList = ImGui.GetBackgroundDrawList();

        for (var i = 0; i < markers.Length; i++)
        {
            var wm = markers[i];
            if (!wm.Active || !config.MarkerEnabled[wm.Index]) continue;

            var center = wm.Position with { Y = wm.Position.Y + config.HeightOffset };
            if (Vector3.Distance(cameraOrigin, center) > config.MaxDistance) continue;

            var count = MarkerStyle.Shapes[wm.Index] == MarkerShape.Square ? 4 : RingSegments;
            if (!ProjectShape(center, config.Radius, count, MarkerStyle.Shapes[wm.Index])) continue;

            var fill = MarkerStyle.WithAlpha(config.MarkerColors[wm.Index], config.FillOpacity);
            var outline = MarkerStyle.WithAlpha(config.MarkerColors[wm.Index], config.OutlineOpacity);

            drawList.AddConvexPolyFilled(ref screenPoints[0], count, fill);
            if (config.OutlineThickness > 0f)
                drawList.AddPolyline(ref screenPoints[0], count, outline, ImDrawFlags.Closed, config.OutlineThickness);
        }
    }

    /// <summary>
    /// Projects the shape's perimeter points into <see cref="screenPoints"/>.
    /// Returns false (skip marker) if any point is behind the camera / fails to project.
    /// </summary>
    private bool ProjectShape(Vector3 center, float radius, int count, MarkerShape shape)
    {
        for (var s = 0; s < count; s++)
        {
            // Square: 4 corners rotated 45deg so it reads axis-aligned; circle: even ring.
            var angle = shape == MarkerShape.Square
                ? (MathF.PI / 4f) + s * (MathF.PI * 2f / 4f)
                : s * (MathF.PI * 2f / count);
            var extent = shape == MarkerShape.Square ? radius * 1.41421356f : radius;

            var world = center with
            {
                X = center.X + MathF.Cos(angle) * extent,
                Z = center.Z + MathF.Sin(angle) * extent,
            };

            if (!gameGui.WorldToScreen(world, out var screen))
                return false;
            screenPoints[s] = screen;
        }

        return true;
    }
}
